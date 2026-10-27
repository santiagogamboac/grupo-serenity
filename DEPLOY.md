# Despliegue de producción — www.gruposerenity.com

Reemplaza el WordPress del hosting compartido por este stack (React + .NET + PostgreSQL + MinIO) en un VPS con Docker.
Todo corre en contenedores; lo único expuesto a internet es **Caddy** (80/443), que emite y renueva solo el certificado
HTTPS de `gruposerenity.com` y `www.gruposerenity.com`.

```
internet ──► caddy (80/443, HTTPS) ──► web (nginx: SPA, /api, /media) ──► api (.NET) ──► postgres
                                                                    └──► minio (imágenes)
backup (diario): pg_dump + copia del volumen de MinIO ──► ./backups
```

Archivos: `docker-compose.yml` (base) + `docker-compose.prod.yml` (producción), `caddy/Caddyfile`, `.env.prod.example`,
`scripts/deploy.sh`, `scripts/backup.sh`.

Sigue los pasos **en este orden**.

## 1. VPS con Docker

- Linux (Ubuntu 24.04 LTS o Debian 12). Mínimo **2 vCPU, 4 GB de RAM y 40 GB de disco**: el build de .NET y Node se hace
  en el servidor.
- IP pública fija (IPv4; IPv6 si el proveedor la da).
- Instala Docker Engine y el plugin de Compose (**≥ 2.24**: `docker-compose.prod.yml` usa `!reset`):
  ```bash
  curl -fsSL https://get.docker.com | sh
  sudo usermod -aG docker $USER     # cierra sesión y vuelve a entrar
  docker compose version
  ```
- Firewall: abre solo 22, 80 y 443 (TCP) y 443/UDP (HTTP/3). Nada más: Postgres, MinIO y la API no publican puertos.
- Si el servidor ya tiene otro proxy en 80/443 (por ejemplo, un nginx compartido con otro proyecto), no levantes `caddy`:
  quita ese servicio del override y haz que el proxy existente reenvíe el dominio a `web:80` por una red Docker
  compartida, enviando `X-Forwarded-For` y `X-Forwarded-Proto`.

## 2. Clonar el repo y llenar `.env`

```bash
git clone <url-del-repo> serenity && cd serenity
cp .env.prod.example .env
nano .env
```

Reemplaza **todo lo que dice `CAMBIAR`**: `deploy.sh` se niega a desplegar si queda alguno. Para generar secretos:

```bash
openssl rand -base64 48 | tr -d '\n=+/' | cut -c1-48     # uno distinto para POSTGRES_PASSWORD, MINIO_PASSWORD y JWT_KEY
```

Debe quedar:
- `PUBLIC_ORIGIN=https://www.gruposerenity.com`, `ASPNETCORE_ENVIRONMENT=Production` y `DOMINIO=gruposerenity.com`.
- `ACME_EMAIL`: un correo real (Let's Encrypt avisa ahí si un certificado falla).
- `ADMIN_CORREO` y `ADMIN_PASSWORD`: el primer usuario del panel. Cambia la clave al entrar.
- **Wompi** (`WOMPI_AMBIENTE=produccion` y las cuatro llaves de producción del dashboard → *Desarrolladores*). Si las
  dejas vacías, el sitio funciona igual: las reservas quedan apartadas y el equipo las confirma a mano.
- **SMTP real** (Google Workspace, Zoho, Brevo, Amazon SES…) y `CORREO_EQUIPO` (quién recibe los avisos, separados por
  coma). Sin `SMTP_HOST` no se envían correos (las solicitudes y reservas igual quedan en el panel).

## 3. Correr `deploy.sh`

```bash
bash scripts/deploy.sh
```

Hace `git pull`, construye las imágenes, levanta el stack con `restart: unless-stopped`, reinicia `web` (nginx guarda
la IP vieja de la api), espera `/health` y corre el **smoke de solo lectura** por la red interna (17 chequeos públicos:
contenido, imágenes, la SPA, que no quede nada del WordPress…). Mientras el DNS no apunte al VPS, el chequeo por HTTPS
avisa y sigue; es normal.

Al primer arranque la API crea el esquema, el usuario admin y siembra el contenido inicial (textos, destinos, reseñas,
valores e imágenes). Para futuras versiones basta con volver a correr `bash scripts/deploy.sh`.

## 4. DNS: `gruposerenity.com` y `www` → VPS

En el proveedor del dominio:

| Tipo | Nombre | Valor |
|---|---|---|
| A | `@` | IPv4 del VPS |
| A | `www` | IPv4 del VPS |
| AAAA | `@` y `www` | IPv6 del VPS (solo si el VPS tiene IPv6; si no, **borra** los AAAA viejos del hosting) |

- Borra los registros A/AAAA/CNAME que apuntaban al hosting de WordPress. No toques los **MX** ni los TXT del correo.
- Baja antes el TTL a 300 s para que el cambio se propague rápido.
- En cuanto el DNS apunte al VPS, Caddy obtiene el certificado solo. Compruébalo:
  ```bash
  docker compose -f docker-compose.yml -f docker-compose.prod.yml logs caddy | grep -i certificate
  SMOKE_SOLO_PUBLICO=1 SMOKE_BASE=https://www.gruposerenity.com node scripts/smoke.mjs   # o vuelve a correr deploy.sh
  ```
- `gruposerenity.com` redirige a `https://www.gruposerenity.com`, y los enlaces viejos de la tienda (`/shop/…`) llevan a
  `/#reservas`.

## 5. Wompi: URL de eventos

En el dashboard de Wompi (modo **producción**) → *Desarrolladores* → *URL de eventos*:

```
https://www.gruposerenity.com/api/public/pagos/wompi/eventos
```

Sin esta URL los pagos igual se confirman cuando el huésped vuelve del checkout (el sitio consulta la transacción), pero
el webhook cubre a quien cierra la pestaña antes de volver. Haz una reserva real de bajo valor para probar el ciclo
completo y anúlala desde el dashboard de Wompi.

## 6. Cargar alojamientos, tarifas y política de cancelación (`/admin`)

En `https://www.gruposerenity.com/admin`:
1. **Proyectos** → cada destino → *Alojamientos* → *Agregar alojamiento*: nombre, foto, precio por noche, aseo, capacidad,
   huéspedes incluidos, recargo y estancia mínima. Luego *Temporadas y calendario*: temporadas con su precio (fin de año,
   Semana Santa, puentes) y fechas bloqueadas (mantenimiento, uso propio).
2. **Contenido del sitio** → *Reservas* → **Condiciones y política de cancelación**: el huésped debe aceptarla antes de
   pagar. Revisa también los demás textos de la reserva.
3. Si un proyecto tiene alojamientos publicados, su botón en *Destinos* lleva a la reserva en línea. El campo
   *URL de reserva* del proyecto queda solo para un enlace externo opcional.

## 7. Airbnb: enlace `.ics` de cada alojamiento

Para que lo que se reserva en el sitio se bloquee en Airbnb:
1. Admin → Proyectos → alojamiento → *Temporadas y calendario* → **Calendario para Airbnb** → *Copiar*.
2. En Airbnb → el anuncio → *Calendario* → *Disponibilidad* → *Sincronizar calendarios* → *Importar calendario* → pega el
   enlace (`https://www.gruposerenity.com/api/public/ical/<token>.ics`). Es privado: no lo compartas.
3. En sentido contrario (lo de Airbnb bloquea el sitio): registra el anuncio en *Anuncios de Airbnb* del proyecto con su
   enlace iCal de exportación y vincúlalo al alojamiento.

## 8. Dar de baja el WordPress (al final)

Solo **después** de comprobar que `https://www.gruposerenity.com` y `https://gruposerenity.com` responden con el sitio
nuevo (candado válido, reserva de prueba hecha, correo de aviso recibido):
1. Descarga un respaldo completo del hosting (archivos + base de datos) y guárdalo.
2. Revisa que el correo del dominio no dependa del hosting (los MX). Si depende, migra el correo antes de cancelar.
3. Cancela el plan de hosting.

## Operación

```bash
C="docker compose -f docker-compose.yml -f docker-compose.prod.yml"
$C ps                      # estado
$C logs -f api             # logs de la API (también web, caddy, backup)
bash scripts/deploy.sh     # actualizar a la última versión de main
```

**Backups**: el contenedor `backup` deja todos los días a las `BACKUP_HORA` (por defecto 03:00, hora de Colombia)
`backups/serenity-db-AAAA-MM-DD.sql.gz` y `backups/serenity-media-AAAA-MM-DD.tar.gz`, y borra los de más de
`BACKUP_RETENCION_DIAS` días. Al arrancar hace uno si hoy no hay ninguno. **Copia `backups/` fuera del servidor**
(rclone, rsync o el snapshot del proveedor).

Restaurar:
```bash
C="docker compose -f docker-compose.yml -f docker-compose.prod.yml"
gunzip -c backups/serenity-db-AAAA-MM-DD.sql.gz | $C exec -T postgres psql -U serenity_user -d serenity_db
$C stop minio && docker run --rm -v serenity_minio_data:/data -v "$PWD/backups:/b" alpine \
  sh -c 'rm -rf /data/* /data/.[!.]* 2>/dev/null; tar -xzf /b/serenity-media-AAAA-MM-DD.tar.gz -C /data' && $C start minio
```

## Pendiente del dueño

- Llaves de Wompi de producción y registrar la URL de eventos (paso 5).
- Datos de un SMTP real y el correo del equipo (paso 2).
- VPS y acceso al DNS del dominio (pasos 1 y 4).
- Texto de la política de cancelación y reembolso (paso 6).
