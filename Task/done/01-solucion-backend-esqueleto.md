# 01 — Solución backend + esqueleto Clean Architecture

**Depende de:** — · **Estado:** done — 2026-09-27

## Alcance
- `Backend/Serenity.slnx` con `Serenity.Domain`, `Serenity.Application`, `Serenity.Infrastructure`, `Serenity.Api`, `tests/Serenity.Tests`.
- Paquetes iguales a urbanos: MediatR 12.5, FluentValidation 11.12, Dapper 2.1, Npgsql 10, BCrypt.Net-Next, JWT Bearer 10, Serilog, AWSSDK.S3, Scalar/OpenAPI.
- Building blocks: `Result`/`Result<T>`, `PagedResult<T>`, `PaginationParams`, `SlugGenerator`, `ValidationBehavior`, `ErrorHandlingMiddleware`, `DbConnectionFactory`.
- `Program.cs`: Serilog, JWT, CORS, rate limiting, health check, OpenAPI (solo Development), ProblemDetails.
- `appsettings.json` con placeholders; secretos por variables de entorno.

## Aceptación
- [x] `dotnet build Serenity.slnx` sin errores ni warnings de nullable.
- [x] `dotnet test` corre (aunque sea con los tests de SlugGenerator).
- [x] Referencias entre proyectos respetan Api → App/Infra, Infra → App, App → Domain.

## Evidencia (2026-09-27)
`dotnet build Serenity.slnx` → 0 advertencias, 0 errores. `dotnet test` → 66/66. Referencias verificadas en los csproj.
