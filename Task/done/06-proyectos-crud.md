# 06 — Proyectos (destinos): CRUD + galería

**Depende de:** 04 · **Estado:** done — 2026-09-27

## Alcance
- CRUD completo, slug único autogenerado (sufijo numérico si colisiona), `publicado`, `orden` (+ endpoint de reordenar).
- Campos: `nombre`, `etiqueta` (texto de la pestaña), `icono` (whitelist), `ubicacionEtiqueta`, `titulo`, `descripcion`, `metaLugar`, `metaEstancia`, `urlReserva`, `ctaTexto`.
- Galería ordenada (`proyecto_imagenes`): agregar (mediaId + alt), reordenar, editar alt, quitar. La 1.ª imagen es la foto principal; la 2.ª la de la franja.
- Eliminar proyecto: solicitudes asociadas conservan `destino_texto` (FK `ON DELETE SET NULL`).

## Aceptación
- [x] Crear → aparece en `home` solo si `publicado`.
- [x] Dos proyectos con el mismo nombre → slugs distintos.
- [x] Reordenar cambia el orden de las pestañas en `home`.
- [x] Icono fuera de la lista → 400.

## Evidencia (2026-09-27)
Smoke: crear (slug), duplicado → `-2`, icono/URL/nombre inválidos → 400, galería (agregar/reordenar/alt/quitar), reordenar proyectos y reflejo en `home`, despublicar oculta, 404. Navegador: proyecto “Cartagena” creado con 2 imágenes (una subida, una de la biblioteca) apareció como 3.ª pestaña; despublicar/republicar y borrar desde la lista.
