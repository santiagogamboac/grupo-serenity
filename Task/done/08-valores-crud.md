# 08 — Valores ("Lo que nos mueve"): CRUD

**Depende de:** 03 · **Estado:** done — 2026-09-27

## Alcance
`valores(titulo, descripcion, icono, variante normal|alta|ancha, orden, activo)`; CRUD + reordenar; icono whitelist compartida con el front vía `GET /api/admin/catalogos`.

## Aceptación
- [x] CRUD completo; variante o icono inválido → 400; inactivo no sale en `home`.

## Evidencia (2026-09-27)
Smoke: CRUD, variante/icono inválidos → 400, inactivo oculto de `home`.
