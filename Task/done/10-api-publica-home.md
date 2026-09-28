# 10 — API pública `GET /api/public/home`

**Depende de:** 05–08 · **Estado:** done — 2026-09-27

## Alcance
Endpoint agregado y de solo lectura: secciones con imágenes expandidas + proyectos publicados con galería + valores activos + reseñas publicadas, todo ordenado. Cabecera `Cache-Control: public, max-age=30`.

## Aceptación
- [x] Forma idéntica al contrato; sin campos internos (`mediaId`, `creado_en`…).
- [x] Pocas queries en una sola conexión; < 100 ms local.

## Evidencia (2026-09-27)
Forma verificada por smoke (10 secciones, sin `xxxMediaId`, imágenes expandidas y descargables). Cambio respecto al plan: `Cache-Control: no-cache` en lugar de `max-age=30`, para que lo guardado en el admin se vea al recargar.
