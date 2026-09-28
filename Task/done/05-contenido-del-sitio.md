# 05 — Contenido del sitio (secciones editables)

**Depende de:** 04 · **Estado:** done — 2026-09-27

## Alcance
- Records tipados por sección: `marca`, `anuncio`, `hero`, `intro`, `valores`, `destinos`, `resenas`, `reservas`, `footer` (ver `contrato-api.md`).
- `GET /api/admin/contenido`, `PUT /api/admin/contenido/{clave}` con validador por sección (longitudes, URLs http/https/`#ancla`, mediaId existente).
- Imágenes de sección (`logo`, `pajaro`, `imagenPrincipal`, `imagenSecundaria`) por `xxxMediaId`; las lecturas devuelven `MediaDto` expandido.
- Clave desconocida → 404.

## Aceptación
- [x] PUT de cada sección persiste y se refleja en `GET /api/public/home`.
- [x] `mediaId` inexistente → 400; URL `javascript:` → 400.
- [x] Tests de validadores.

## Evidencia (2026-09-27)
Smoke: GET admin con las 10 secciones, PUT hero refleja en `home`, restaura; sección desconocida → 404; `javascript:` → 400; `mediaId` inexistente → 400; navegación vacía → 400. Editado también desde el navegador (Contenido → Portada → “Guardar cambios”).
