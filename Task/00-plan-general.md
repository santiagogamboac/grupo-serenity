# Plan general — Backend MVP de Grupo Serenity

**Estado: APROBADO** (2026-09-27). El dueño del proyecto delegó la aprobación del plan; este
documento y las features de `pending/` son el plan de trabajo vigente.

## 1. Objetivo

El frontend (`Frontend/`, repo `santiagogamboac/grupo-serenity-modern`, Vite + React 19 +
wouter + shadcn) es una landing de una sola página con **todo el contenido hardcodeado**
(textos, imágenes remotas de gruposerenity.com, reseñas, destinos, redes sociales) y un
formulario de reserva que solo muestra un toast.

El MVP entrega:

1. **Backend** (`Backend/`) réplica de la arquitectura de `urbanos-rurales`: .NET 10, Clean
   Architecture, CQRS/MediatR, Dapper + PostgreSQL, FluentValidation, JWT, Serilog, MinIO (S3).
2. **Cero datos hardcodeados** en la página pública: todo sale de la API. Las imágenes se
   descargaron a `Backend/seed/images/`, se suben al almacenamiento en el primer arranque.
3. **Panel de administración** (`/admin` en el mismo frontend) con CRUD de **proyectos**
   (destinos), reseñas, valores, edición de todo el contenido de la landing, biblioteca de
   imágenes y bandeja de solicitudes de reserva.
4. **Conexión front ↔ back**: la landing consume la API; el formulario de reserva crea una
   solicitud real que el admin gestiona.
5. **Todo dockerizado**: `docker compose up --build` levanta postgres + minio + api + web.
6. **Correo/notificaciones**: al final, en `pending/`, sin confirmar (ver feature 15).

Fuera de alcance del MVP: pasarela de pagos, motor de reservas/disponibilidad, multi-idioma,
gestión de usuarios admin (queda en backlog), SEO por proyecto.

## 2. Arquitectura

```
Serenity/
├─ Backend/                     .NET 10 (Serenity.slnx)
│  ├─ src/Serenity.Domain         entidades, enums (sin dependencias)
│  ├─ src/Serenity.Application    MediatR, DTOs, validators, interfaces
│  ├─ src/Serenity.Infrastructure Dapper repos, migrador SQL, seeder, S3, JWT, BCrypt
│  ├─ src/Serenity.Api            controllers, Program.cs, middleware, Dockerfile
│  ├─ tests/Serenity.Tests        xUnit (dominio + aplicación)
│  └─ seed/                       imágenes + contenido inicial
├─ Frontend/                    repo existente (Vite SPA) + panel admin
├─ Task/                        este plan (pending/ → done/)
├─ scripts/                     smoke test E2E de la API
├─ docker-compose.yml           postgres · minio · minio-init · api · web
└─ .env.example
```

Referencias entre proyectos: `Api → Application, Infrastructure`, `Infrastructure →
Application`, `Application → Domain` (igual que urbanos).

### Decisiones (y por qué se apartan de urbanos, cuando lo hacen)

| # | Decisión | Motivo |
|---|---|---|
| D1 | Stack idéntico a urbanos: .NET 10, MediatR, Dapper, FluentValidation, Serilog, BCrypt, JWT | Petición explícita: "muy similar a urbanos". |
| D2 | **Migraciones SQL versionadas, aplicadas por la propia API al arrancar** (tabla `schema_migrations`, advisory lock). Sin EF Core | `pending.md` de urbanos documenta el dolor: `PendingModelChangesWarning`, `dev-setup.ps1` roto, Npgsql enums con doble registro. Un solo `docker compose up` debe dejar todo funcionando, sin pasos manuales. Runtime sigue siendo Dapper. |
| D3 | Sin enums nativos de Postgres: `varchar` + `CHECK` | Evita la trampa del doble registro de enums de urbanos. |
| D4 | PostgreSQL 16 plano (`postgres:16-alpine`), sin PostGIS | No hay datos geoespaciales. |
| D5 | **Subida de imágenes por multipart a la API**, que las guarda en MinIO (S3) | Una sola llamada desde el admin, sin CORS contra MinIO ni presign en 3 pasos; para un panel de un solo admin y imágenes ≤ 10 MB es suficiente. El almacenamiento sigue siendo S3-compatible (cambiar a S3/R2 = solo config). |
| D6 | Las imágenes se sirven por **`/media/*`** (nginx → MinIO) y la API devuelve URLs públicas construidas desde `Storage:UrlPublicaBase` | Mismo origen, sin exponer MinIO, portable a CDN. |
| D7 | Contenido de la landing en **secciones JSONB tipadas** (`secciones`) + tablas para listas (`proyectos`, `resenas`, `valores`) | Cada texto editable sin tocar código; listas con CRUD real. Cada sección tiene un record C# con validador. |
| D8 | Endpoint público agregado `GET /api/public/home` | La landing es 1 página: 1 request, sin cascadas. |
| D9 | Frontend: `@tanstack/react-query` + axios para datos; admin con shadcn/ui ya presente | Mismo patrón de datos que urbanos. |
| D10 | Puertos host distintos a urbanos (postgres 5434, minio 9012/9013, api 8081, web 8090) | Los contenedores de urbanos están corriendo en esta máquina. |
| D11 | Se agrega un campo **"Correo o WhatsApp"** al formulario de reserva | El formulario original no captura ningún canal de contacto, así que la solicitud sería inutilizable. |
| D12 | Formato de títulos: `\n` = salto de línea, `*texto*` = énfasis (`<em>`) | El diseño usa `<br/>` y `<em>` en los titulares; el admin los edita como texto plano. |
| D13 | Se quitaron `Microsoft.AspNetCore.OpenApi` y Scalar del backend | Traían `Microsoft.OpenApi` con una vulnerabilidad alta conocida (NU1903, sin versión parcheada disponible) y solo servían en desarrollo. El contrato vive en `Task/contrato-api.md`. |
| D14 | Los textos de mecánica de UI (etiquetas del formulario, aria-labels, mensajes de error) quedan en `features/public/textos.ts`; todo lo editorial va a la BD | "Cero hardcode" aplica al contenido; las etiquetas de un formulario no son contenido administrable. |
| D15 | Se agregó la sección `navegacion` (menú) y los enlaces de los CTA de portada/intro a las secciones editables | Estaban hardcodeados en `Home.tsx`. |

## 3. Modelo de datos (V001)

`usuarios`, `refresh_tokens`, `media`, `secciones(clave, contenido jsonb)`, `proyectos`,
`proyecto_imagenes`, `resenas`, `valores`, `solicitudes`, `app_state`, `schema_migrations`.
Detalle en `pending/02-base-de-datos-migrador-seed.md`.

Secciones JSON: `marca`, `anuncio`, `hero`, `intro`, `valores`, `destinos`, `resenas`,
`reservas`, `footer`.

## 4. Contrato de API

Ver `Task/contrato-api.md`.

## 5. Features y orden de ejecución

| # | Feature | Depende de |
|---|---|---|
| 01 | Solución backend + esqueleto Clean Architecture | — |
| 02 | Base de datos: migrador + esquema + seeds | 01 |
| 03 | Autenticación admin (JWT, refresh, lockout) | 02 |
| 04 | Almacenamiento de imágenes (MinIO) + biblioteca de medios | 02, 03 |
| 05 | Contenido del sitio (secciones editables) | 04 |
| 06 | Proyectos CRUD + galería | 04 |
| 07 | Reseñas CRUD | 03 |
| 08 | Valores CRUD | 03 |
| 09 | Solicitudes de reserva (público + admin) | 06 |
| 10 | API pública `home` | 05–08 |
| 11 | Frontend: sitio público 100 % dinámico | 10, 09 |
| 12 | Frontend: panel de administración | 03–09 |
| 13 | Dockerización completa (compose, Dockerfiles, nginx) | 01–12 |
| 14 | Verificación E2E + documentación | todo |
| 15 | Correo y notificaciones (**sin confirmar**) | 09 — siempre al final |
| 99 | Backlog post-MVP | — |

Cada feature se mueve de `pending/` a `done/` **solo** cuando su checklist de aceptación
está verificado (build + prueba real contra los contenedores). Al mover, se anota la fecha y
la evidencia al final del archivo.

## 6. Convenciones

- Idioma del dominio: español (tablas, DTOs, entidades), como urbanos.
- Controllers delgados; toda la lógica en handlers MediatR; errores por `Result` o excepción
  → `ErrorHandlingMiddleware` (ProblemDetails).
- Sin `TODO` sin dueño; sin secretos en el repo (`.env.example` con placeholders).
- Nada de `git push`; los cambios quedan en el working tree para revisión.
