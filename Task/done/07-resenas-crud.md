# 07 — Reseñas: CRUD

**Depende de:** 03 · **Estado:** done — 2026-09-27

## Alcance
`resenas(autor, contexto, texto, calificacion 1..5, orden, publicada)`; CRUD + reordenar; público solo `publicada = true`.

## Aceptación
- [x] CRUD completo verificado por API.
- [x] Calificación fuera de 1–5 → 400. Despublicar la oculta de `home`.

## Evidencia (2026-09-27)
Smoke: CRUD completo, calificación 6 → 400, despublicar la oculta de `home`, reordenar, 404 al re-borrar.
