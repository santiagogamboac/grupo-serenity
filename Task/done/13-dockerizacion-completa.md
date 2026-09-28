# 13 — Dockerización completa

**Depende de:** 01–12 · **Estado:** done — 2026-09-27

## Alcance
- `Backend/src/Serenity.Api/Dockerfile` (multi-stage, usuario no root, healthcheck).
- `Frontend/Dockerfile` (node build → nginx) + `nginx.conf` (SPA fallback, `/api` → api, `/media` → minio, gzip, cache de assets con hash).
- `docker-compose.yml` raíz: `postgres` (5434), `minio` (9012/9013), `minio-init`, `api` (8081), `web` (8090); healthchecks y `depends_on: service_healthy`; volúmenes nombrados.
- `.env.example` con todos los secretos/puertos; `.dockerignore` en ambos.

## Aceptación
- [x] Desde cero (`docker compose down -v && docker compose up --build -d`) todo queda healthy sin pasos manuales.
- [x] `http://localhost:8090` sirve la landing con datos e imágenes; `/admin` funciona; datos persisten tras `docker compose restart`.

## Evidencia (2026-09-27)
`docker compose down -v && docker compose up --build -d` → postgres, minio, api y web `healthy` sin pasos manuales; `http://localhost:8090` sirve landing e imágenes por `/media`, `/admin/*` cae al SPA, persistencia tras `docker compose restart`. Se corrigió el healthcheck de `web` (`localhost` resolvía a IPv6) y se agregó `favicon.ico → 204`.
