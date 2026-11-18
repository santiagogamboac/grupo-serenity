#!/usr/bin/env bash
# Despliegue de producción de Grupo Serenity. Se corre EN EL SERVIDOR, desde el repo clonado:
#
#   bash scripts/deploy.sh
#
# 1. git pull  2. build + up -d (docker-compose.yml + docker-compose.prod.yml)  3. reinicia web
# 4. espera /health  5. smoke de solo lectura (chequeos públicos) por la red interna y, si el DNS ya apunta
#    aquí, también contra PUBLIC_ORIGIN por HTTPS.
#
# Variables opcionales: SIN_PULL=1 (no hace git pull) · RAMA (rama a desplegar; por defecto la actual) · ENV_FILE
# (por defecto .env) · COMPOSE_FILES (archivos compose separados por espacio; por defecto docker-compose.yml
# docker-compose.prod.yml) · COMPOSE_EXTRA (otro override, p. ej. para ensayar donde 80/443 están ocupados) ·
# PROYECTO (nombre del proyecto compose, p. ej. serenity-pruebas) · RESPALDO_PREVIO=1 (pg_dump antes de actualizar).
# Desde Windows lo invoca scripts/deploy-remoto.ps1 por SSH (deploy-produccion.bat / deploy-pruebas.bat).
set -euo pipefail
# Git Bash (Windows) reescribe rutas tipo /scripts/... de los argumentos de docker; en Linux no tiene efecto.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")/.."
ENV_FILE="${ENV_FILE:-.env}"
COMPOSE=(docker compose --env-file "$ENV_FILE")
if [ -n "${PROYECTO:-}" ]; then COMPOSE+=(-p "$PROYECTO"); fi
for f in ${COMPOSE_FILES:-docker-compose.yml docker-compose.prod.yml}; do COMPOSE+=(-f "$f"); done
if [ -n "${COMPOSE_EXTRA:-}" ]; then COMPOSE+=(-f "$COMPOSE_EXTRA"); fi

paso() { printf '\n\033[1;36m▸ %s\033[0m\n' "$*"; }
falla() { printf '\n\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

# ─── Verificaciones previas ───────────────────────────────────────────────────
[ -f "$ENV_FILE" ] || falla "Falta $ENV_FILE: cp .env.prod.example .env y llena los secretos."
if grep -q 'CAMBIAR' "$ENV_FILE"; then
  falla "Quedan valores 'CAMBIAR' en $ENV_FILE: $(grep -o '^[A-Z_]*=CAMBIAR' "$ENV_FILE" | cut -d= -f1 | tr '\n' ' ')"
fi
grep -q '^ASPNETCORE_ENVIRONMENT=Production' "$ENV_FILE" || falla "$ENV_FILE debe tener ASPNETCORE_ENVIRONMENT=Production."
PUBLIC_ORIGIN=$(grep '^PUBLIC_ORIGIN=' "$ENV_FILE" | cut -d= -f2- | tr -d '\r')
[[ "$PUBLIC_ORIGIN" == https://* ]] || falla "PUBLIC_ORIGIN debe empezar por https:// (está en '$PUBLIC_ORIGIN')."

# ─── 1. Código ────────────────────────────────────────────────────────────────
if [ "${SIN_PULL:-0}" != "1" ]; then
  if [ -n "${RAMA:-}" ] && [ "$(git rev-parse --abbrev-ref HEAD)" != "$RAMA" ]; then
    paso "git checkout $RAMA"
    git fetch origin "$RAMA"
    git checkout "$RAMA"
  fi
  paso "git pull"
  git pull --ff-only
fi
echo "Versión: $(git log -1 --format='%h %s')"

# ─── Respaldo previo de la base (si el stack ya estaba corriendo) ─────────────
if [ "${RESPALDO_PREVIO:-0}" = "1" ]; then
  if [ -n "$("${COMPOSE[@]}" ps -q --status running postgres 2>/dev/null)" ]; then
    paso "Respaldo previo de la base"
    mkdir -p backups
    RESPALDO="backups/predeploy-$(date +%Y-%m-%d_%H%M%S).sql.gz"
    "${COMPOSE[@]}" exec -T postgres pg_dump -U serenity_user -d serenity_db --no-owner --clean --if-exists | gzip > "$RESPALDO"
    # Un gzip vacío pesa ~20 bytes: por debajo de 1 KB el dump falló.
    [ "$(wc -c < "$RESPALDO")" -gt 1024 ] || falla "El respaldo $RESPALDO quedó vacío; no se despliega."
    echo "✓ $RESPALDO ($(du -h "$RESPALDO" | cut -f1))"
  else
    echo "(postgres no está corriendo: primer despliegue, sin respaldo previo)"
  fi
fi

# ─── 2. Build + arranque ──────────────────────────────────────────────────────
paso "Build de imágenes"
"${COMPOSE[@]}" build --pull

paso "Levantando el stack"
"${COMPOSE[@]}" up -d --remove-orphans

# ─── 3. nginx guarda la IP vieja de la api si se recreó: reiniciar web ────────
paso "Reiniciando web"
"${COMPOSE[@]}" restart web

# ─── 4. Salud ─────────────────────────────────────────────────────────────────
paso "Esperando /health"
for i in $(seq 1 60); do
  if "${COMPOSE[@]}" exec -T web wget -qO- http://127.0.0.1/health >/dev/null 2>&1; then
    echo "✓ /health responde (intento $i)"
    break
  fi
  [ "$i" = 60 ] && { "${COMPOSE[@]}" logs --tail=80 api; falla "/health no respondió en 2 minutos."; }
  sleep 2
done

# ─── 5. Smoke de solo lectura ─────────────────────────────────────────────────
# Por la red interna (no depende del DNS ni del certificado).
# La red propia del proyecto (<proyecto>_default): en una red compartida (edge) "web" podría ser el de otro stack.
REDES="$(docker inspect "$("${COMPOSE[@]}" ps -q web)" --format '{{range $k, $v := .NetworkSettings.Networks}}{{$k}} {{end}}')"
RED="$(tr ' ' '\n' <<<"$REDES" | grep '_default$' | head -1 || true)"
RED="${RED:-$(awk '{print $1}' <<<"$REDES")}"
paso "Smoke público por la red interna (http://web)"
docker run --rm --network "$RED" -v "$PWD/scripts:/scripts:ro" -e SMOKE_SOLO_PUBLICO=1 -e SMOKE_BASE=http://web \
  node:22-alpine node /scripts/smoke.mjs

# Por internet, si el dominio ya apunta a este servidor.
paso "Smoke público contra $PUBLIC_ORIGIN"
if curl -fsS -o /dev/null --max-time 15 "$PUBLIC_ORIGIN/health"; then
  docker run --rm --network host -v "$PWD/scripts:/scripts:ro" -e SMOKE_SOLO_PUBLICO=1 -e "SMOKE_BASE=$PUBLIC_ORIGIN" \
    node:22-alpine node /scripts/smoke.mjs
else
  echo "⚠ $PUBLIC_ORIGIN todavía no responde por HTTPS (¿DNS sin apuntar aquí o certificado en emisión?)."
  echo "  Revisa: ${COMPOSE[*]} logs caddy"
fi

paso "Estado"
"${COMPOSE[@]}" ps
echo -e "\n✓ Despliegue terminado."
