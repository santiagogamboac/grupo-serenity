# 02 — Base de datos: migrador SQL + esquema + seeds

**Depende de:** 01 · **Estado:** done — 2026-09-27

## Alcance
- `SqlMigrator` (Infrastructure): aplica `Migrations/V###__*.sql` (embebidos) en orden, registra en `schema_migrations`, protegido con `pg_advisory_lock`, cada migración en una transacción. Corre al arrancar la API (con reintentos mientras Postgres levanta).
- `V001__esquema_inicial.sql`: `usuarios`, `refresh_tokens`, `media`, `secciones`, `proyectos`, `proyecto_imagenes`, `resenas`, `valores`, `solicitudes`, `app_state`. `varchar + CHECK` en lugar de enums. Índices por `orden`, `estado`, `creado_en`, `lower(correo)`.
- `DatabaseSeeder` idempotente:
  - Admin inicial desde `Admin__Correo` / `Admin__Password` (solo si no existe ningún usuario). Nunca hay contraseña por defecto en Production.
  - Contenido inicial (`seed/contenido.json` + `seed/images/*`): sube imágenes al almacenamiento, inserta secciones, 2 proyectos (Santa Marta / La Dorada), 3 reseñas, 4 valores. Marca `app_state('seed_inicial_v1')` para no repetir aunque el admin borre cosas.

## Aceptación
- [x] Postgres vacío → API arranca → esquema completo creado; segundo arranque no re-ejecuta migraciones.
- [x] Dos instancias arrancando a la vez no chocan (advisory lock).
- [x] Tras el seed, `GET /api/public/home` devuelve exactamente el contenido que hoy está hardcodeado en `Home.tsx`.
- [x] Reiniciar la API no duplica datos; borrar un proyecto y reiniciar no lo resucita.

## Evidencia (2026-09-27)
V001 aplicada sobre Postgres vacío (compose, desde cero con `down -v`). 2 instancias arrancando a la vez contra una BD vacía: solo una migró y sembró (1 usuario, 2 proyectos, 3 reseñas, 4 valores, 1 migración). Reinicio de contenedores: sin re-seed; un proyecto borrado no reapareció. El seed dejó la landing visualmente igual a la original (`Task/evidencia/before-*.png` vs `after-*.png`; +2 px de alto en escritorio, +78 px en móvil por el campo nuevo de contacto).
