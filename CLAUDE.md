# CLAUDE.md

Guía para trabajar en este repositorio. Complementa `README.md` (arranque y operación).

## Qué es

Sitio de Grupo Serenity con contenido 100 % administrable. `Backend/` (.NET 10) + `Frontend/` (Vite/React,
repo aparte `grupo-serenity-modern`, ignorado por el `.gitignore` de la raíz) + `docker-compose.yml`.
El plan y las decisiones de diseño están en `Task/00-plan-general.md`; el contrato HTTP en `Task/contrato-api.md`.
Flujo de trabajo: cada feature es un `.md` en `Task/pending/`; al cumplir su checklist se mueve a `Task/done/`.

## Comandos

```bash
docker compose up --build -d                  # todo el stack (requiere .env: copiar de .env.example)
docker compose up -d postgres minio minio-init && bash scripts/run-api-local.sh   # API local
cd Backend && dotnet build Serenity.slnx && dotnet test Serenity.slnx
node scripts/smoke.mjs                        # E2E de la API contra el stack levantado
cd Frontend && pnpm check && pnpm test && pnpm exec vite build
```

En Windows `pkill` no existe: para parar la API local usa `Get-Process Serenity.Api | Stop-Process`. Si compilas
mientras la API corre, falla el copiado de DLLs (archivo bloqueado).

## Backend (`Backend/src`)

Clean Architecture estricta: `Api → Application, Infrastructure`; `Infrastructure → Application`; `Application → Domain`.

- **CQRS con MediatR**: un archivo por caso de uso en `Application/Features/<Area>/` con el request (`record`), su
  `AbstractValidator` y el handler. Controllers delgados (`ApiControllerBase`): solo traducen `Result` → HTTP.
- **Errores esperados** = `Result`/`Result<T>` con `ErrorTipo` (Validacion→400, NoAutorizado→401, Bloqueado→423,
  NoEncontrado→404, Conflicto→409). Las excepciones (validación FluentValidation, 500) las convierte
  `ErrorHandlingMiddleware` en ProblemDetails.
- **Datos = Dapper** (sin EF). Filas como clases con `init` (no records posicionales). `DefaultTypeMap.MatchNamesWithUnderscores`
  mapea `snake_case`↔`PascalCase`: no hace falta `AS Alias`. Enums = `varchar + CHECK` (sin enums nativos).
- **Migraciones**: SQL versionado embebido en `Infrastructure/Migrations/V###__*.sql`; `SqlMigrator` las aplica al
  arrancar bajo `pg_advisory_lock` (el seed corre dentro del mismo lock). Para cambiar el esquema: nuevo archivo
  `V002__...sql`, **nunca editar uno ya aplicado**. Sin datos de desarrollo en las migraciones: eso es del seeder.
- **Contenido de la landing**: `Application/Features/Contenido/Secciones.cs` define cada sección como record + validador
  (`Contenido.cs`). Agregar un campo = propiedad en el record + regla en su validador + `contenidoSchema.ts` en el admin +
  tipo en `Frontend/client/src/lib/types.ts` + `seed/contenido.json`. Las imágenes se guardan como `xxxMediaId` y se
  expanden a `MediaDto` al leer; los ids internos no salen en la API pública (`ParaPublico`).
- **Seed**: `Api/Seeding/DatabaseSeeder.cs` (idempotente, marca `app_state.seed_inicial_v1`) reutiliza los mismos comandos
  del admin con `seed/contenido.json` + `seed/images/`. Campos `xxxArchivo` del JSON se suben y se convierten en `xxxMediaId`.
- **Almacenamiento**: `IAlmacenamientoObjetos` sobre S3 (MinIO en dev). El SDK cierra el stream tras `PutObject`: toma
  tamaños antes de subir.
- Íconos permitidos: `Domain/Catalogos/Catalogos.cs` ↔ `Frontend/client/src/lib/icons.ts` (mantener sincronizados).

## Frontend (`Frontend/client/src`)

- Datos con TanStack Query + un único axios (`lib/api.ts`, baseURL `/api`, refresh de sesión con una sola petición en vuelo).
- Sitio público: `pages/Home.tsx` compone `features/public/sections/*`. **No agregar texto editorial ni URLs de imagen en
  componentes**: va a la base de datos. Solo textos de mecánica de UI (etiquetas de formulario, aria) en `features/public/textos.ts`.
- Titulares: `\n` = salto, `*x*` = `<em>` (`lib/rich.ts`, `components/RichTitle.tsx`).
- Panel: `features/admin/` (rutas en `AdminApp.tsx`, cargado con `lazy`). Formularios de contenido generados desde
  `contenidoSchema.ts`. UI con shadcn (`components/ui`).
- `index.css`: los resets globales están dentro de `@layer base` para que los utilities de Tailwind (admin) les ganen; las
  clases del sitio público no usan capa. No sacarlos de la capa.
- En dev, `vite.config.ts` proxya `/api` → `:8081` y `/media` → MinIO `:9012`; en docker lo hace `nginx.conf`.
- Convenciones: español para dominio/UI; Prettier del repo.

## Cosas que muerden

- Puertos: este stack usa 5434/9012/9013/8081/8090 porque `urbanos-rurales` ocupa 5433/9002/8080/5173.
- Heredocs de bash muy largos (> ~130 líneas) fallan en esta shell: crear archivos grandes con la herramienta de escritura.
- `Frontend/` es otro repo git: los cambios ahí se commitean en ese repo, no en la raíz.
- Correo/notificaciones (`Task/pending/15`) **no está implementado ni confirmado**; no asumir que existe SMTP.
