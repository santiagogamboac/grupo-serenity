# ─── Build ────────────────────────────────────────────────────────────────────
# Contexto de build: la carpeta Backend/ (ver docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Primero solo los .csproj: la capa de restore se cachea mientras no cambien dependencias.
COPY Directory.Build.props ./
COPY src/Serenity.Domain/Serenity.Domain.csproj src/Serenity.Domain/
COPY src/Serenity.Application/Serenity.Application.csproj src/Serenity.Application/
COPY src/Serenity.Infrastructure/Serenity.Infrastructure.csproj src/Serenity.Infrastructure/
COPY src/Serenity.Api/Serenity.Api.csproj src/Serenity.Api/
RUN dotnet restore src/Serenity.Api/Serenity.Api.csproj

COPY src src
COPY seed seed
RUN dotnet publish src/Serenity.Api/Serenity.Api.csproj -c Release -o /app --no-restore

# ─── Runtime ──────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
# icu-libs: normalización Unicode correcta para los slugs (acentos → ASCII).
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /app .

# El chequeo usa wget (incluido en busybox/alpine); /health consulta Postgres.
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=10 \
  CMD wget -qO- http://localhost:8080/health || exit 1

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Serenity.Api.dll"]
