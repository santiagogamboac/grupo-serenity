# Guía de despliegue — Grupo Serenity

Cómo está organizado el proyecto, cómo se prepara cada ambiente y cómo se lleva un cambio desde tu PC hasta **pruebas**
y luego a **producción**. Complementa a [`DEPLOY.md`](DEPLOY.md) (instalación de una VPS nueva, DNS, Wompi, Airbnb y baja
del WordPress) y a [`README.md`](README.md) (desarrollo local).

> **En una línea:** haces `git push`, doble clic en `deploy-pruebas.bat`, revisas pruebas, haces merge a `main` y
> doble clic en `deploy-produccion.bat`.

---

## Índice

1. [Mapa rápido](#1-mapa-rápido)
2. [Organización de los proyectos](#2-organización-de-los-proyectos)
3. [Preparación (una sola vez)](#3-preparación-una-sola-vez)
4. [Flujo entre ambientes: local → pruebas → producción](#4-flujo-entre-ambientes-local--pruebas--producción)
5. [Qué hace el script por dentro](#5-qué-hace-el-script-por-dentro)
6. [Configuración y parámetros](#6-configuración-y-parámetros)
7. [Base de datos y datos entre ambientes](#7-base-de-datos-y-datos-entre-ambientes)
8. [Revertir un despliegue](#8-revertir-un-despliegue)
9. [Operación diaria en el servidor](#9-operación-diaria-en-el-servidor)
10. [Solución de problemas](#10-solución-de-problemas)
11. [Checklist de despliegue](#11-checklist-de-despliegue)

---

## 1. Mapa rápido

| Quiero… | Hago… |
|---|---|
| Probar en mi PC | `docker compose --profile dev up --build -d` → http://localhost:8090 |
| Verificar antes de subir | `dotnet test`, `pnpm check && pnpm test && pnpm exec vite build` (ver [4.1](#41-verificar-en-local)) |
| Desplegar una rama en pruebas | `deploy-pruebas.bat -Rama feature/mi-cambio` |
| Desplegar `main` en pruebas | doble clic en `deploy-pruebas.bat` |
| Desplegar a producción | doble clic en `deploy-produccion.bat` (pide escribir `produccion`) |
| Ver logs en el servidor | [sección 9](#9-operación-diaria-en-el-servidor) |
| Deshacer un despliegue | [sección 8](#8-revertir-un-despliegue) |

---

## 2. Organización de los proyectos

### 2.1 El repositorio

Un solo repositorio (`github.com/santiagogamboac/grupo-serenity`) con backend, frontend y despliegue:

```
grupo-serenity/
├─ Backend/                      API .NET 10 (Serenity.slnx)
│  ├─ src/Serenity.Domain/           entidades y catálogos (sin dependencias)
│  ├─ src/Serenity.Application/      casos de uso CQRS (MediatR): un archivo por caso en Features/<Área>/
│  ├─ src/Serenity.Infrastructure/   Dapper + PostgreSQL, S3/MinIO, correo; Migrations/V###__*.sql
│  ├─ src/Serenity.Api/              controllers, workers (Airbnb, reservas), seeder, Dockerfile
│  ├─ tests/Serenity.Tests/          pruebas unitarias
│  └─ seed/                          contenido inicial (contenido.json + images/)
├─ Frontend/                     SPA Vite + React 19
│  ├─ client/src/features/public/    sitio público (secciones, reserva)
│  ├─ client/src/features/admin/     panel /admin
│  ├─ nginx.conf                     sirve la SPA y hace proxy de /api → api y /media → minio
│  └─ Dockerfile
├─ Task/                         plan por features (pending/ → done/) y contrato de la API
├─ caddy/Caddyfile               HTTPS automático (solo si Serenity tiene la VPS para él solo)
├─ scripts/
│  ├─ deploy-remoto.ps1              ★ despliegue desde Windows por SSH
│  ├─ deploy.ambientes.psd1          ★ configuración de los ambientes (sin secretos)
│  ├─ deploy.sh                      despliegue que corre EN el servidor (lo invoca el .ps1)
│  ├─ backup.sh                      backup diario (contenedor `backup`)
│  ├─ smoke.mjs                      pruebas E2E de la API (modo solo lectura para producción)
│  └─ run-api-local.sh, destinos-ejemplo.mjs
├─ deploy-produccion.bat         ★ doble clic: despliega producción
├─ deploy-pruebas.bat            ★ doble clic: despliega pruebas
├─ docker-compose.yml            stack base (desarrollo y base de los demás)
├─ docker-compose.prod.yml       override de producción: Caddy en 80/443, sin puertos internos, backups
├─ docker-compose.pruebas.yml    override de pruebas: sin puertos, sin nombres fijos, red del proxy
├─ .env.example                  plantilla de desarrollo
├─ .env.prod.example             plantilla de producción
└─ .env.pruebas.example          plantilla de pruebas
```

Los `.env` reales **nunca** se suben a git (están en `.gitignore`): viven solo en tu PC (desarrollo) y en cada clon del
servidor.

### 2.2 Qué corre en cada ambiente

Cada ambiente es el mismo conjunto de contenedores, construido desde el código del clon:

```
            ┌──────────────────────────── stack Serenity ────────────────────────────┐
proxy  ───► │ web (nginx: SPA + /api + /media) ──► api (.NET) ──► postgres           │
público     │                                              └────► minio (imágenes)   │
            │ minio-init (crea el bucket y termina)                                   │
            └─────────────────────────────────────────────────────────────────────────┘
```

| Servicio | Imagen | Datos persistentes |
|---|---|---|
| `postgres` | postgres:16-alpine | volumen `<proyecto>_postgres_data` |
| `minio` | minio | volumen `<proyecto>_minio_data` |
| `api` | se construye de `Backend/` | — (al arrancar aplica migraciones y siembra si la base está vacía) |
| `web` | se construye de `Frontend/` | — |
| `caddy`, `backup` | solo con `docker-compose.prod.yml` | `caddy_data`, `./backups` |

### 2.3 Los tres ambientes

| | Local (desarrollo) | Pruebas | Producción |
|---|---|---|---|
| Dónde | tu PC (Docker Desktop) | VPS `51.161.114.45` | VPS `51.161.114.45` |
| Carpeta | tu clon | `~/serenity-pruebas` | `~/serenity` (verifícalo, [3.2](#32-servidor-producción)) |
| Proyecto compose | `serenity` | `serenity-pruebas` | `serenity` |
| Archivos compose | `docker-compose.yml` | `docker-compose.yml` + `docker-compose.pruebas.yml` | los de producción ([3.2](#32-servidor-producción)) |
| `.env` desde | `.env.example` | `.env.pruebas.example` | `.env.prod.example` |
| URL | http://localhost:8090 | `https://pruebas.<dominio>` | `https://www.<dominio>` |
| Wompi | sandbox o vacío | **sandbox** | producción |
| Correo | Mailpit (http://localhost:8027) | vacío o buzón de pruebas | SMTP real |
| Respaldo antes de desplegar | — | no | sí (`backups/predeploy-*.sql.gz`) |
| Backup diario | — | no | sí (si usa `docker-compose.prod.yml`) |
| Cómo se despliega | `docker compose up --build -d` | `deploy-pruebas.bat` | `deploy-produccion.bat` |

Pruebas y producción están **completamente separados**: otra carpeta, otro `.env` con otros secretos, otro proyecto
compose y por eso otros contenedores y otros volúmenes (otra base de datos y otras imágenes). Desplegar o borrar pruebas
no toca producción.

### 2.4 La VPS compartida

La VPS también aloja **urbanos-rurales**. Su nginx público (`urbanos-nginx`) y su certbot son los dueños de los puertos
80/443; Serenity no publica puertos: entra por la red Docker `edge`.

```
                          VPS 51.161.114.45
 internet ─► 80/443 ─► urbanos-nginx ─┬─► urbanos-rurales …
                       (certbot)      │
                         red "edge"   ├─► serenity-web           ─► stack "serenity"          (producción)
                                      └─► serenity-pruebas-web   ─► stack "serenity-pruebas"  (pruebas)
```

Por eso **producción no puede usar `docker-compose.prod.yml` tal cual en esta VPS**: ese override levanta Caddy en
80/443, que ya son de urbanos-nginx. Caddy y `docker-compose.prod.yml` completos sirven cuando Serenity tenga una VPS
propia ([`DEPLOY.md`](DEPLOY.md)).

### 2.5 Archivos de despliegue y quién los usa

| Archivo | Dónde se usa | Para qué |
|---|---|---|
| `deploy-produccion.bat`, `deploy-pruebas.bat` | tu PC | lanzadores: llaman al `.ps1` con el ambiente y pasan tus parámetros |
| `scripts/deploy-remoto.ps1` | tu PC | revisa el repo, conecta por SSH, prepara el clon, confirma y llama a `deploy.sh` |
| `scripts/deploy.ambientes.psd1` | tu PC | servidor, carpeta, rama y compose de cada ambiente (en git) |
| `scripts/deploy.ambientes.local.psd1` | tu PC | tus ajustes: usuario SSH, llave (fuera de git) |
| `scripts/deploy.sh` | servidor | build, arranque, `/health`, smoke y respaldo previo |
| `docker-compose.pruebas.yml` | servidor (pruebas) | aísla el stack de pruebas y lo conecta a `edge` |
| `.env` del clon | servidor | secretos y URLs de ese ambiente |

---

## 3. Preparación (una sola vez)

### 3.1 Tu PC (Windows)

1. **Git y el repo clonado**, con `origin` apuntando a GitHub y permiso de push.
2. **Cliente OpenSSH**: abre PowerShell y escribe `ssh -V`. Si no existe: *Configuración → Sistema → Características
   opcionales → Agregar → Cliente OpenSSH*.
3. **Llave SSH** (si no tienes una):
   ```powershell
   ssh-keygen -t ed25519 -C "tu-correo"          # Enter en todo; queda en $env:USERPROFILE\.ssh\id_ed25519
   ```
4. **Autoriza tu llave en el servidor** (pide la contraseña del usuario por última vez):
   ```powershell
   type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh USUARIO@51.161.114.45 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys"
   ```
   Comprueba que ya entra **sin contraseña**: `ssh USUARIO@51.161.114.45 "docker ps --format '{{.Names}}'"`.
   El script exige esto (usa SSH en modo no interactivo para la verificación).
5. **Tus ajustes locales**: crea `scripts/deploy.ambientes.local.psd1` (está en `.gitignore`):
   ```powershell
   @{
       produccion = @{ Usuario = 'ubuntu' }
       pruebas    = @{ Usuario = 'ubuntu' }
       # Si tu llave no es la predeterminada:  Llave = 'C:\Users\yo\.ssh\serenity'
   }
   ```
   Solo pones las claves que cambian; el resto sale de `deploy.ambientes.psd1`.

### 3.2 Servidor: producción

Producción ya está corriendo en la VPS; aquí solo se trata de que el script apunte a **la misma carpeta y los mismos
archivos compose** que se usan hoy.

1. Averigua carpeta y archivos del stack actual (en la VPS):
   ```bash
   docker inspect serenity-web --format \
     '{{index .Config.Labels "com.docker.compose.project"}} | {{index .Config.Labels "com.docker.compose.project.working_dir"}} | {{index .Config.Labels "com.docker.compose.project.config_files"}}'
   ```
   (si el contenedor se llama distinto, búscalo con `docker ps --format '{{.Names}}'`).
   Salida de ejemplo: `serenity | /home/ubuntu/serenity | /home/ubuntu/serenity/deploy/docker-compose.yml`.
2. Pon esos datos en `scripts/deploy.ambientes.psd1` → `produccion`:
   - `Directorio` = la carpeta (`~/serenity` o la ruta absoluta).
   - `ComposeFiles` = los archivos, **relativos a esa carpeta** y separados por espacio (p. ej. `deploy/docker-compose.yml`).
   - `Proyecto` = el nombre del proyecto si no es `serenity`.
   Haz commit de ese cambio: es configuración del equipo, no secreta.
3. **Los archivos compose de producción deben estar en git.** Si `deploy/docker-compose.yml` solo existe en el servidor
   (o en la PC de quien desplegó), súbelo al repo; si no, el `git pull` del despliegue choca con él o lo pierde.
4. Revisa el `.env` de producción en el servidor. `deploy.sh` exige:
   - ninguna variable con `CAMBIAR`;
   - `ASPNETCORE_ENVIRONMENT=Production`;
   - `PUBLIC_ORIGIN=https://…` (el dominio público; arma CORS, regreso de Wompi, feed `.ics` y enlaces de correo).

   Si ese servidor usaba otra variable para el dominio (p. ej. `SERENITY_DOMAIN`), agrega también `PUBLIC_ORIGIN`.
5. Prueba sin riesgo con la confirmación: corre `deploy-produccion.bat`, revisa el resumen (servidor, carpeta,
   compose, comando) y escribe cualquier cosa distinta de `produccion` para cancelar. Hasta ese punto solo hizo
   `git pull` y verificaciones.

> El script se detiene solo si `ComposeFiles` incluye `docker-compose.prod.yml` y el puerto 80 lo tiene otro contenedor:
> evita que Caddy intente quitarle 80/443 a urbanos-nginx.

### 3.3 Servidor: pruebas

1. **DNS**: un registro `A` para el subdominio de pruebas (p. ej. `pruebas.gruposerenity.com`) → `51.161.114.45`.
   Mientras no haya dominio definitivo sirve uno de sslip: `pruebas.serenity.51.161.114.45.sslip.io`.
2. **Clon y `.env`** (en la VPS):
   ```bash
   git clone https://github.com/santiagogamboac/grupo-serenity.git ~/serenity-pruebas
   cd ~/serenity-pruebas
   cp .env.pruebas.example .env
   nano .env
   ```
   - Reemplaza todo lo que dice `CAMBIAR`, con **secretos distintos a los de producción**:
     `openssl rand -base64 48 | tr -d '\n=+/' | cut -c1-48`.
   - `PUBLIC_ORIGIN=https://pruebas.<dominio>`.
   - Wompi: solo llaves **sandbox** (o vacías).
   - Correo: vacío, o un `CORREO_EQUIPO` de prueba.
   - `PROXY_RED=edge` y `WEB_ALIAS=serenity-pruebas-web` (los valores por defecto).
3. **Revisa la red `edge`** antes del primer despliegue:
   ```bash
   docker network inspect edge --format '{{range .Containers}}{{.Name}} {{end}}'
   ```
   En `edge` solo deben estar el proxy y los `web` de cada stack. Si ves contenedores `api`/`minio` de producción en
   esa red, avisa antes de seguir: el nginx de `web` resuelve `api` y `minio` por nombre, y en una red compartida pruebas
   podría alcanzar los de producción.
4. **Server block en urbanos-nginx** para el subdominio, con el mismo estilo y el mismo método de certificados
   (certbot) que los demás sitios de la VPS. Lo esencial:
   ```nginx
   server {
       listen 443 ssl;
       http2 on;
       server_name pruebas.gruposerenity.com;
       ssl_certificate     /etc/letsencrypt/live/pruebas.gruposerenity.com/fullchain.pem;
       ssl_certificate_key /etc/letsencrypt/live/pruebas.gruposerenity.com/privkey.pem;
       client_max_body_size 20m;                       # subida de imágenes del panel

       # Pruebas no debe indexarse en buscadores.
       add_header X-Robots-Tag "noindex, nofollow" always;

       location / {
           resolver 127.0.0.11 valid=10s;
           set $serenity_pruebas http://serenity-pruebas-web:80;
           proxy_pass $serenity_pruebas;
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
       }
   }
   ```
   Más el bloque del puerto 80 con el desafío ACME y la redirección a HTTPS, igual que los otros sitios. El `resolver` +
   variable hacen que nginx no falle al arrancar si el stack de pruebas está apagado.
   Valida y recarga: `docker exec urbanos-nginx nginx -t && docker exec urbanos-nginx nginx -s reload`.
5. **Ajusta la URL** de pruebas en `scripts/deploy.ambientes.psd1` → `pruebas.Url`.
6. **Primer despliegue**: `deploy-pruebas.bat`. La primera vez la API crea el esquema, el usuario admin
   (`ADMIN_CORREO`/`ADMIN_PASSWORD` del `.env` de pruebas) y siembra el contenido inicial.

---

## 4. Flujo entre ambientes: local → pruebas → producción

```
  tu PC                       GitHub                         VPS
 ───────                     ────────                       ─────
 1. cambio + verificación
 2. git push rama ─────────► feature/x
 3. deploy-pruebas.bat -Rama feature/x ───────────────────► ~/serenity-pruebas  (checkout feature/x, build, up)
 4. revisar pruebas  ◄──────────────────────────────────── https://pruebas.<dominio>
 5. merge a main ──────────► main
 6. deploy-pruebas.bat ────────────────────────────────────► ~/serenity-pruebas  (main)      [opcional]
 7. deploy-produccion.bat ─────────────────────────────────► ~/serenity          (respaldo, main, build, up)
 8. revisar producción ◄─────────────────────────────────── https://www.<dominio>
```

La regla: **a producción solo llega `main`, y solo después de verlo funcionar en pruebas.** Los dos ambientes
construyen desde el mismo commit de GitHub, así que lo que pasó en pruebas es exactamente lo que se despliega.

### 4.1 Verificar en local

```bash
cd Backend && dotnet build Serenity.slnx && dotnet test Serenity.slnx
cd Frontend && pnpm check && pnpm test && pnpm exec vite build
# Con el stack local arriba (docker compose --profile dev up --build -d):
SMOKE_BASE=http://localhost:8090 node scripts/smoke.mjs
```

No subas nada que falle aquí.

### 4.2 Subir la rama

```bash
git checkout -b feature/mi-cambio      # si aún no estás en una rama
git commit -am "…"
git push -u origin feature/mi-cambio
```

El servidor despliega lo que está **en GitHub**: los cambios sin commitear o sin push no viajan. El script te avisa si
los hay.

### 4.3 Desplegar la rama en pruebas

```powershell
.\deploy-pruebas.bat -Rama feature/mi-cambio
```

Revisa el resumen y responde `s`. Al terminar verás `OK: pruebas desplegado en N s -> https://pruebas.…`.

### 4.4 Revisar pruebas

- El smoke de solo lectura ya corrió dentro del despliegue (contenido, imágenes, SPA, que no haya restos de WordPress).
- A mano, según el cambio: el inicio, `/alojamientos/<id>`, `/admin` (login y la sección que tocaste), una reserva de
  punta a punta con Wompi **sandbox** (tarjeta de prueba `4242 4242 4242 4242`), el feed `/api/public/ical/<token>.ics`.
- Si el cambio trae una migración nueva (`V00N__*.sql`), confirma en los logs que se aplicó:
  ```bash
  cd ~/serenity-pruebas && docker compose -p serenity-pruebas -f docker-compose.yml -f docker-compose.pruebas.yml logs api | grep -i migr
  ```

¿Algo mal? Corrige en tu PC, `git push` y repite 4.3. Pruebas se puede desplegar cuantas veces haga falta.

### 4.5 Integrar a `main`

Merge de la rama a `main` (PR en GitHub o local: `git checkout main && git merge feature/mi-cambio && git push`).

### 4.6 (Opcional) Pruebas con `main`

```powershell
.\deploy-pruebas.bat
```

Deja pruebas en el mismo commit que irá a producción. Recomendado si en `main` entraron otros cambios a la vez, o si el
merge tuvo conflictos.

### 4.7 Desplegar a producción

```powershell
.\deploy-produccion.bat
```

El resumen aparece en **rojo**; para seguir hay que escribir `produccion`. El despliegue:
1. hace `git pull` de `main` en `~/serenity`;
2. **respalda la base** en `~/serenity/backups/predeploy-AAAA-MM-DD_HHMMSS.sql.gz` (y se detiene si el respaldo queda vacío);
3. construye las imágenes y recrea los contenedores (el sitio se corta unos segundos mientras arranca la api);
4. reinicia `web`, espera `/health` y corre el smoke de solo lectura.

### 4.8 Revisar producción

- Abre el sitio y `/admin`, y repite lo esencial de 4.4 **sin** pagos reales (salvo que sea parte de la prueba).
- Si algo falla: [sección 8](#8-revertir-un-despliegue).

### Cambios urgentes (hotfix)

Mismo camino, más corto: rama `hotfix/x` desde `main` → `deploy-pruebas.bat -Rama hotfix/x` → revisión rápida → merge →
`deploy-produccion.bat`. No despliegues a producción ramas que no sean `main`.

---

## 5. Qué hace el script por dentro

`deploy-produccion.bat` / `deploy-pruebas.bat` → `scripts/deploy-remoto.ps1 -Ambiente …`:

| # | Dónde | Qué | Si falla |
|---|---|---|---|
| 1 | tu PC | lee `deploy.ambientes.psd1` (+ `.local.psd1` + parámetros) y exige usuario, servidor, carpeta, rama y compose | se detiene; nada cambió |
| 2 | tu PC | `git fetch` de la rama; avisa de cambios sin commitear y commits sin push; muestra el commit a desplegar | se detiene si la rama no existe en `origin` |
| 3 | tu PC → VPS | prueba SSH sin contraseña | se detiene; nada cambió |
| 4 | VPS | `cd` al clon, `git fetch` + `checkout` de la rama + `git pull --ff-only`; comprueba `.env` y archivos compose; detecta choque de Caddy con el puerto 80 | se detiene **sin reiniciar nada** (solo pudo actualizar el código del clon) |
| 5 | tu PC | resumen y confirmación (`produccion` / `s`) | cancelado; los contenedores siguen igual |
| 6 | VPS | `scripts/deploy.sh`: valida `.env`, respaldo previo (producción), `build --pull`, `up -d --remove-orphans`, reinicia `web`, espera `/health` (2 min), smoke por la red interna y por HTTPS | muestra los últimos logs de la api y el comando para verlos |

Detalles útiles:
- `git pull --ff-only`: si el clon del servidor tiene commits propios o cambios que chocan, **no** fuerza nada; se
  detiene y hay que resolverlo a mano en el servidor.
- `deploy.sh` corre con `SIN_PULL=1` porque el `.ps1` ya actualizó el código en el paso 4; así se ejecuta siempre la
  versión de `deploy.sh` de la rama desplegada.
- El smoke se ejecuta en un contenedor `node:22-alpine` dentro de la red del propio stack (`<proyecto>_default`), no en
  `edge`.

---

## 6. Configuración y parámetros

### 6.1 `scripts/deploy.ambientes.psd1`

| Clave | Producción (por defecto) | Pruebas (por defecto) | Significado |
|---|---|---|---|
| `Servidor` | `51.161.114.45` | `51.161.114.45` | IP, nombre o `Host` de `~/.ssh/config` |
| `Usuario` | `CAMBIAR` | `CAMBIAR` | usuario SSH (mejor en el `.local.psd1`) |
| `Puerto` | `22` | `22` | puerto SSH |
| `Llave` | vacío | vacío | llave privada; vacío = la predeterminada |
| `Directorio` | `~/serenity` | `~/serenity-pruebas` | clon del repo en el servidor |
| `Rama` | `main` | `main` | rama de `origin` |
| `ComposeFiles` | `docker-compose.yml docker-compose.prod.yml` | `docker-compose.yml docker-compose.pruebas.yml` | archivos compose en orden |
| `Proyecto` | vacío (`serenity`) | `serenity-pruebas` | nombre del proyecto compose |
| `EnvFile` | `.env` | `.env` | variables del ambiente dentro del clon |
| `Url` | `https://www.gruposerenity.com` | `https://pruebas.gruposerenity.com` | solo para el mensaje final |
| `Respaldo` | `$true` | `$false` | `pg_dump` antes de actualizar |

### 6.2 Parámetros del script (y de los `.bat`)

| Parámetro | Ejemplo | Uso |
|---|---|---|
| `-Rama` | `-Rama feature/x` | desplegar otra rama (pruebas) |
| `-Usuario`, `-Servidor`, `-Puerto`, `-Llave` | `-Usuario ubuntu` | sobrescribir la conexión por una vez |
| `-SinPull` | | desplegar lo que ya hay en el clon, sin `git pull` (p. ej. tras un `git checkout <commit>` manual, [8.2](#82-volver-a-un-commit-anterior-sin-revert)) |
| `-SinConfirmar` | | sin preguntas, para automatizar. **Evítalo en producción.** |

```powershell
.\deploy-pruebas.bat -Rama feature/galeria -Usuario ubuntu
.\scripts\deploy-remoto.ps1 -Ambiente produccion -Llave C:\Users\yo\.ssh\serenity
Get-Help .\scripts\deploy-remoto.ps1 -Detailed
```

### 6.3 Variables de `deploy.sh` (si lo corres a mano en el servidor)

```bash
cd ~/serenity-pruebas
COMPOSE_FILES='docker-compose.yml docker-compose.pruebas.yml' PROYECTO=serenity-pruebas bash scripts/deploy.sh
```

`SIN_PULL=1`, `RAMA`, `ENV_FILE`, `COMPOSE_FILES`, `COMPOSE_EXTRA`, `PROYECTO`, `RESPALDO_PREVIO=1`. Sin variables se
comporta como antes (producción con `docker-compose.prod.yml`).

---

## 7. Base de datos y datos entre ambientes

### 7.1 Migraciones

- Viven en `Backend/src/Serenity.Infrastructure/Migrations/V###__*.sql` y la api las aplica **sola al arrancar**.
- Son **solo hacia adelante**: no hay "bajar" una migración. Nunca edites una ya aplicada; crea la siguiente
  (`V006__…sql`).
- Por eso cada despliegue a producción hace un respaldo previo: es la forma de volver atrás si una migración sale mal.

### 7.2 Ramas con migraciones en pruebas

Si despliegas en pruebas una rama con `V006__x.sql` y luego esa migración cambia (o la rama se descarta), la base de
pruebas queda con una versión que ya no existe en el código. En pruebas la solución es **reiniciar su base**:

```bash
cd ~/serenity-pruebas
docker compose -p serenity-pruebas -f docker-compose.yml -f docker-compose.pruebas.yml down -v   # borra base e imágenes DE PRUEBAS
```

y volver a desplegar: arranca vacía y se siembra con el contenido inicial. Revisa dos veces el `-p serenity-pruebas`
antes de usar `down -v`: **nunca** lo corras en `~/serenity`.

### 7.3 Copiar datos de producción a pruebas (opcional)

Útil para probar con el contenido real. Ten en cuenta:
- La base de producción tiene datos personales de huéspedes (nombres, correos, teléfonos). Trátala como producción.
- Antes de copiar, deja **vacíos** `SMTP_HOST` y las llaves de Wompi en el `.env` de pruebas, o pruebas enviará correos
  reales a huéspedes reales.
- Los anuncios de Airbnb se seguirán sincronizando desde pruebas (solo lectura del iCal; no afecta Airbnb).

```bash
# En la VPS. Usa el backup diario o el predeploy más reciente de producción.
P=~/serenity/backups/serenity-db-AAAA-MM-DD.sql.gz
C="docker compose -p serenity-pruebas -f docker-compose.yml -f docker-compose.pruebas.yml"
cd ~/serenity-pruebas
$C stop api
gunzip -c "$P" | $C exec -T postgres psql -U serenity_user -d serenity_db
# Imágenes (si quieres las mismas):
$C stop minio
docker run --rm -v serenity-pruebas_minio_data:/data -v ~/serenity/backups:/b alpine \
  sh -c 'rm -rf /data/* /data/.[!.]* 2>/dev/null; tar -xzf /b/serenity-media-AAAA-MM-DD.tar.gz -C /data'
$C start minio && $C start api && $C restart web
```

Los usuarios del panel también se copian: en pruebas entrarás con las credenciales de producción.

### 7.4 Nunca de pruebas a producción

El contenido de producción se edita en el `/admin` de producción. No se copia la base de pruebas a producción.

---

## 8. Revertir un despliegue

### 8.1 Lo normal: `git revert`

```bash
git revert <commit-malo>        # en tu PC, sobre main
git push
```
Luego `deploy-pruebas.bat` (rápido) y `deploy-produccion.bat`. Queda registro en el historial y no hay que tocar el
servidor a mano.

### 8.2 Volver a un commit anterior sin revert

Para salir del paso mientras se prepara el revert (en la VPS):

```bash
cd ~/serenity
git log --oneline -5                 # elige el commit bueno
git checkout <commit-bueno>          # queda en "detached HEAD"
```

y desde tu PC `deploy-produccion.bat -SinPull`. Después, con el arreglo ya en `main`: en la VPS `git checkout main` y un
despliegue normal.

### 8.3 Si una migración dañó datos

Volver el código atrás **no** deshace una migración. Restaura el respaldo previo del despliegue:

```bash
cd ~/serenity
C="docker compose <los mismos -p y -f de producción>"
ls -lh backups/predeploy-*        # el de la hora del despliegue
$C stop api
gunzip -c backups/predeploy-AAAA-MM-DD_HHMMSS.sql.gz | $C exec -T postgres psql -U serenity_user -d serenity_db
```

y despliega el código anterior (8.2) **antes** de arrancar la api, o la api volverá a aplicar la migración. Lo que se
haya registrado entre el despliegue y la restauración (reservas, solicitudes) se pierde: revisa el panel de Wompi y la
bandeja de correo para recuperarlo a mano.

---

## 9. Operación diaria en el servidor

Define el comando una vez por sesión:

```bash
# Pruebas
cd ~/serenity-pruebas && C="docker compose -p serenity-pruebas -f docker-compose.yml -f docker-compose.pruebas.yml"
# Producción (con los ComposeFiles reales de producción)
cd ~/serenity && C="docker compose -f docker-compose.yml -f docker-compose.prod.yml"
```

| Qué | Comando |
|---|---|
| Estado | `$C ps` |
| Logs en vivo | `$C logs -f api` (también `web`, `postgres`, `minio`) |
| Reiniciar la api | `$C restart api && $C restart web` (nginx guarda la IP vieja de la api) |
| Versión desplegada | `git log -1 --oneline` |
| Espacio en disco | `df -h /` y `docker system df` |
| Limpiar imágenes viejas | `docker image prune -f` (no toca volúmenes) |
| Backups | `ls -lh backups/` (copia `backups/` fuera del servidor con regularidad) |

El primer despliegue después de muchos cambios tarda más (build de .NET y Node en la VPS). Las imágenes viejas se
acumulan: un `docker image prune -f` de vez en cuando.

---

## 10. Solución de problemas

| Mensaje / síntoma | Causa | Qué hacer |
|---|---|---|
| `Falta 'Usuario' para …` | no hay usuario SSH configurado | `scripts/deploy.ambientes.local.psd1` o `-Usuario` ([3.1](#31-tu-pc-windows)) |
| `No hay conexión SSH sin contraseña…` | llave no autorizada, usuario o puerto mal | `ssh USUARIO@51.161.114.45` a mano; repite 3.1 paso 4 |
| `No existe ~/serenity-pruebas en el servidor` | falta el clon | [3.3](#33-servidor-pruebas) paso 2 |
| `Falta .env en el clon` | primer despliegue del ambiente | copia la plantilla y llénala |
| `Falta docker-compose.pruebas.yml en el clon` | la rama no tiene ese archivo (anterior a este cambio) | despliega una rama que lo tenga |
| `El puerto 80 ya es de otro contenedor…` | `ComposeFiles` de producción incluye Caddy en la VPS compartida | ajusta `ComposeFiles` ([3.2](#32-servidor-producción)) |
| `fatal: Not possible to fast-forward` | el clon del servidor tiene commits o cambios propios | en el servidor: `git status`, `git log origin/main..HEAD`; súbelos al repo o descártalos con cuidado |
| `Quedan valores 'CAMBIAR' en .env` | `.env` incompleto | llena esas variables |
| `debe tener ASPNETCORE_ENVIRONMENT=Production` / `PUBLIC_ORIGIN debe empezar por https://` | `.env` de otro formato | agrega esas variables |
| `El respaldo … quedó vacío` | `pg_dump` falló (postgres caído o sin disco) | `$C logs postgres`, `df -h`; no se tocó nada |
| `/health no respondió en 2 minutos` | la api no arranca (migración, `.env`, base) | el script muestra los logs; `$C logs --tail=200 api` |
| Smoke por HTTPS: `todavía no responde` | DNS o certificado aún no listos | revisa DNS y el server block/certbot en urbanos-nginx |
| 502 en el sitio tras desplegar | nginx con la IP vieja de la api, o el proxy público no ve `web` | `$C restart web`; `docker network inspect edge` |
| El admin de pruebas muestra datos de producción | `web` de pruebas resuelve `api` de producción por `edge` | [3.3](#33-servidor-pruebas) paso 3: saca `api`/`minio` de producción de `edge` |
| `container name … is already in use` | dos stacks con el mismo `container_name` | pruebas debe usar `docker-compose.pruebas.yml` y `Proyecto = serenity-pruebas` |
| Acentos raros en la consola | consola sin UTF-8 | usa Windows Terminal; el script ya fija UTF-8 |
| `no se puede cargar … la ejecución de scripts está deshabilitada` | ExecutionPolicy | usa los `.bat`, que ya la omiten |

---

## 11. Checklist de despliegue

**Antes**
- [ ] `dotnet test`, `pnpm check`, `pnpm test`, `vite build` en verde.
- [ ] Cambios commiteados y con `git push`.
- [ ] Si hay migración nueva: revisada, y probada en pruebas.

**Pruebas**
- [ ] `deploy-pruebas.bat -Rama <rama>` terminó en `OK`.
- [ ] Revisión manual de lo que cambió (sitio, admin, reserva sandbox si aplica).
- [ ] Logs de la api sin errores.

**Producción**
- [ ] Merge a `main` hecho y empujado.
- [ ] (Opcional) `deploy-pruebas.bat` con `main`.
- [ ] Horario de poco tráfico (el sitio se corta unos segundos).
- [ ] `deploy-produccion.bat` terminó en `OK` y creó `backups/predeploy-…`.
- [ ] Sitio, `/admin` y lo que cambió funcionan en `https://www.<dominio>`.
- [ ] Avisar al equipo qué se desplegó (`git log -1 --oneline`).
