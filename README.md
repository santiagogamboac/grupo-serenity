# Grupo Serenity — sitio + backend + panel admin

Landing de Grupo Serenity con **todo el contenido administrable**: destinos (proyectos), reseñas,
valores, textos, imágenes y bandeja de solicitudes de reserva. Arquitectura hermana de
`urbanos-rurales` (.NET 10, Clean Architecture, CQRS/MediatR, Dapper, PostgreSQL, MinIO).

```
Serenity/
├─ Backend/    API .NET 10 (Serenity.slnx) — Domain · Application · Infrastructure · Api · Tests
├─ Frontend/   SPA Vite + React 19 (repo grupo-serenity-modern) — sitio público + panel /admin
├─ Task/       plan de trabajo por features (pending/ → done/) y contrato de API
├─ scripts/    smoke.mjs (E2E de la API) · run-api-local.sh
└─ docker-compose.yml
```

## Arranque (Docker) — 2 comandos

```bash
cp .env.example .env        # y cambia POSTGRES_PASSWORD, MINIO_PASSWORD, JWT_KEY y ADMIN_*
docker compose up --build -d
```

| Qué | URL |
|---|---|
| Sitio público | http://localhost:8090 |
| Panel admin | http://localhost:8090/admin (usuario = `ADMIN_CORREO` / `ADMIN_PASSWORD` de tu `.env`) |
| API directa | http://localhost:8081 (`/health`) |
| Consola MinIO | http://localhost:9013 |
| Postgres | `localhost:5434` (`serenity_user` / `serenity_db`) |

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
node scripts/smoke.mjs                                        # E2E de toda la API (~100 comprobaciones)
SMOKE_BASE=http://localhost:8090 node scripts/smoke.mjs       # …a través de nginx
cd Frontend && pnpm check && pnpm test && pnpm build          # tipos, vitest, build
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
| Imágenes | Admin → **Imágenes** (biblioteca; también se suben desde cada formulario) |

Formato de titulares: **Enter** = salto de línea, `*palabra*` = cursiva.

## Configuración (variables de entorno)

| Variable | Para qué |
|---|---|
| `POSTGRES_PASSWORD`, `MINIO_PASSWORD` | Credenciales de la infraestructura (obligatorias) |
| `JWT_KEY` | Secreto de firma (≥ 32 caracteres). En `Production` la API **no arranca** si sigue en `CHANGE_ME` |
| `ADMIN_CORREO`, `ADMIN_PASSWORD`, `ADMIN_NOMBRE` | Admin inicial; solo se crea si no existe ningún usuario |
| `MEDIA_PUBLIC_BASE` | Base pública de las imágenes (`/media` por defecto; un CDN absoluto en producción) |
| `PUBLIC_ORIGIN` | Origen permitido por CORS (irrelevante detrás de nginx: mismo origen) |

**Cambiar a S3 / Cloudflare R2 / Spaces:** la sección `Storage__*` de la API habla protocolo S3.
Cambia `Storage__Endpoint`, `Storage__Bucket`, `Storage__AccessKey/SecretKey`, y
`Storage__UrlPublicaBase` (URL pública del bucket o CDN). El bucket debe tener lectura pública.

## Seguridad (resumen)

JWT de 60 min + refresh token opaco rotativo (se guarda solo su hash); bloqueo de cuenta tras 5
fallos (15 min); rate limit por IP en login y formulario; honeypot anti-bots; contraseñas con
BCrypt; subida de imágenes validada por firma binaria (sin SVG); enlaces editables restringidos a
`http(s)`, `mailto`, `tel`, `#ancla`, `/ruta`; el contenido editable se renderiza como texto
(nunca HTML). Contenedor de la API sin root.

## Pendiente

Ver `Task/pending/`: **correo y notificaciones (sin confirmar: faltan datos del cliente)** y el
backlog post-MVP (gestión de usuarios admin, disponibilidad/pagos, SEO por proyecto, multi-idioma).
