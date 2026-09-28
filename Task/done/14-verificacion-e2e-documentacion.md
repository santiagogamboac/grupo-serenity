# 14 — Verificación E2E + documentación

**Depende de:** todo · **Estado:** done — 2026-09-27

## Alcance
- `scripts/smoke.mjs`: recorre la API completa (login, CRUDs, subida, solicitud, borrado) contra el stack de docker; sale ≠ 0 si algo falla.
- Pasada E2E con Playwright sobre `http://localhost:8090` (público + admin) con capturas.
- `README.md` raíz (arranque, credenciales dev, puertos, estructura, cómo cambiar a S3 real), `CLAUDE.md` con comandos y convenciones.

## Aceptación
- [x] `node scripts/smoke.mjs` → todo verde. `dotnet test` verde. `pnpm check`/`pnpm test`/`pnpm build` verdes.
- [x] README permite a alguien nuevo levantar todo con 2 comandos.

## Evidencia (2026-09-27)
`node scripts/smoke.mjs` → 101/101 contra la API directa **y** a través de nginx (`SMOKE_BASE=http://localhost:8090`). `dotnet test` 66/66; `pnpm check`, `pnpm test`, build OK. `README.md` y `CLAUDE.md` en la raíz.
