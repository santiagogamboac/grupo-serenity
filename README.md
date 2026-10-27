# Grupo Serenity — sitio + backend + panel admin

Sitio de Grupo Serenity con **todo el contenido administrable**: destinos (proyectos), reseñas,
valores, textos, imágenes, bandeja de solicitudes y **reserva directa** (alojamientos con temporadas, calendario,
pago con Wompi, avisos por correo y calendario iCal para Airbnb). Reemplaza por completo el WordPress anterior:
no depende de WordPress, PHP ni plugins. Arquitectura hermana de
`urbanos-rurales` (.NET 10, Clean Architecture, CQRS/MediatR, Dapper, PostgreSQL, MinIO).

```
Serenity/
├─ Backend/    API .NET 10 (Serenity.slnx) — Domain · Application · Infrastructure · Api · Tests
├─ Frontend/   SPA Vite + React 19 — sitio público + panel /admin
├─ Task/       plan de trabajo por features (pending/ → done/) y contrato de API
├─ scripts/    smoke.mjs (E2E de la API) · run-api-local.sh · deploy.sh · backup.sh
├─ caddy/      Caddyfile (HTTPS de producción)
├─ docker-compose.yml        stack de desarrollo
└─ docker-compose.prod.yml   override de producción (Caddy, sin puertos internos, backups) — ver DEPLOY.md
```

## Arranque (Docker) — 2 comandos

```bash
cp .env.example .env        # y cambia POSTGRES_PASSWORD, MINIO_PASSWORD, JWT_KEY y ADMIN_*
docker compose --profile dev up --build -d   # --profile dev agrega Mailpit (bandeja de correo en :8027)
docker compose restart web                   # tras recrear la api: nginx guarda la IP vieja
```

| Qué | URL |
|---|---|
| Sitio público | http://localhost:8090 |
| Panel admin | http://localhost:8090/admin (usuario = `ADMIN_CORREO` / `ADMIN_PASSWORD` de tu `.env`) |
| API directa | http://localhost:8081 (`/health`) |
| Consola MinIO | http://localhost:9013 |
| Postgres | `localhost:5434` (`serenity_user` / `serenity_db`) |
| Correos (Mailpit, `--profile dev`) | http://localhost:8027 |

Al primer arranque la API crea el esquema, el usuario admin y **siembra el contenido que antes
estaba hardcodeado** (textos, 2 destinos, 3 reseñas, 4 valores, imágenes descargadas del sitio
actual en `Backend/seed/images/`). Los arranques siguientes no repiten nada.

Los puertos son distintos a los de `urbanos-rurales` (5433/9002/8080) para poder correr ambos.

## Desarrollo sin Docker para la app

```bash
docker compose up -d postgres minio minio-init     # solo la infraestructura
bash scripts/run-api-local.sh                       # API en http://localhost:8081 (lee .env)
cd Frontend && pnpm install && pnpm exec vite --port 5180   # /api y /media se proxyan solos
```

## Pruebas

```bash
cd Backend && dotnet test Serenity.slnx                       # unitarias (dominio + aplicación)
node scripts/smoke.mjs                                        # E2E de toda la API (~215 comprobaciones)
SMOKE_BASE=http://localhost:8090 node scripts/smoke.mjs       # …a través de nginx
SMOKE_SOLO_PUBLICO=1 SMOKE_BASE=https://www.gruposerenity.com node scripts/smoke.mjs   # solo lectura (producción)
cd Frontend && pnpm check && pnpm test && pnpm exec vite build   # tipos, vitest, build
```

`smoke.mjs` deja la base como la encontró. Espera ~1 min en un punto porque prueba el límite
de 5 solicitudes/min por IP.

## Qué se administra y dónde

| Sección del sitio | Dónde se edita |
|---|---|
| Destinos (“Playa o montaña”) | Admin → **Proyectos** (crear, editar, publicar, ordenar, galería) |
| Reseñas | Admin → **Reseñas** |
| “Lo que nos mueve” | Admin → **Valores** (tarjetas) + **Contenido** (encabezado) |
| Todos los textos, logo, colibrí, portada, menú, pie | Admin → **Contenido del sitio** |
| Solicitudes del formulario | Admin → **Solicitudes** (estado y notas) |
| Alojamientos, temporadas, bloqueos, enlace iCal para Airbnb | Admin → **Proyectos** → (proyecto) → *Alojamientos* |
| Reservas en línea (confirmar, cancelar, notas, enlace del huésped) | Admin → **Reservas** |
| Condiciones / política de cancelación | Admin → **Contenido del sitio** → Reservas |
| Imágenes | Admin → **Imágenes** (biblioteca; también se suben desde cada formulario) |

Formato de titulares: **Enter** = salto de línea, `*palabra*` = cursiva.

## Configuración (variables de entorno)

| Variable | Para qué |
|---|---|
| `POSTGRES_PASSWORD`, `MINIO_PASSWORD` | Credenciales de la infraestructura (obligatorias) |
| `JWT_KEY` | Secreto de firma (≥ 32 caracteres). En `Production` la API **no arranca** si sigue en `CHANGE_ME` |
| `ADMIN_CORREO`, `ADMIN_PASSWORD`, `ADMIN_NOMBRE` | Admin inicial; solo se crea si no existe ningún usuario |
| `MEDIA_PUBLIC_BASE` | Base pública de las imágenes (`/media` por defecto; un CDN absoluto en producción) |
| `PUBLIC_ORIGIN` | Origen público del sitio: CORS, regreso desde Wompi, feed `.ics` y enlaces de los correos |
| `WOMPI_*` | Llaves de Wompi (`WOMPI_AMBIENTE=sandbox\|produccion`). Vacías = pago en línea apagado (confirmación manual) |
| `SMTP_*`, `CORREO_EQUIPO` | Correo saliente y destinatarios de los avisos. Sin `SMTP_HOST` no se envía nada |
| `RESERVAS_MINUTOS_RETENCION(_SIN_PAGO)` | Cuánto se retienen las fechas mientras se paga |

**Cambiar a S3 / Cloudflare R2 / Spaces:** la sección `Storage__*` de la API habla protocolo S3.
Cambia `Storage__Endpoint`, `Storage__Bucket`, `Storage__AccessKey/SecretKey`, y
`Storage__UrlPublicaBase` (URL pública del bucket o CDN). El bucket debe tener lectura pública.

## Seguridad (resumen)

JWT de 60 min + refresh token opaco rotativo (se guarda solo su hash); bloqueo de cuenta tras 5
fallos (15 min); rate limit por IP en login y formulario; honeypot anti-bots; contraseñas con
BCrypt; subida de imágenes validada por firma binaria (sin SVG); enlaces editables restringidos a
`http(s)`, `mailto`, `tel`, `#ancla`, `/ruta`; el contenido editable se renderiza como texto
(nunca HTML). Contenedor de la API sin root.

## Producción

`docker-compose.prod.yml` + `scripts/deploy.sh` + `.env.prod.example`. Guía paso a paso (VPS, DNS, Wompi, Airbnb y baja
del WordPress) en **[DEPLOY.md](DEPLOY.md)**.

## Pendiente

Ver `Task/pending/`. Del dueño: llaves de Wompi de producción, SMTP real, servidor + DNS y la política de cancelación.
