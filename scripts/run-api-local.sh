#!/usr/bin/env bash
# Arranca la API local contra los contenedores de compose (postgres/minio). Uso: bash scripts/run-api-local.sh
cd "$(dirname "$0")/.."
set -a; . ./.env; set +a
export ConnectionStrings__Default="Host=localhost;Port=${POSTGRES_PORT:-5434};Database=serenity_db;Username=serenity_user;Password=$POSTGRES_PASSWORD"
export Jwt__Key="$JWT_KEY" Admin__Correo="$ADMIN_CORREO" Admin__Password="$ADMIN_PASSWORD"
export Storage__Endpoint="http://localhost:${MINIO_API_PORT:-9012}" Storage__AccessKey="$MINIO_USER" Storage__SecretKey="$MINIO_PASSWORD"
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:${API_PORT:-8081}"
exec dotnet run --project Backend/src/Serenity.Api --no-launch-profile
