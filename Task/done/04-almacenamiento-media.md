# 04 — Almacenamiento de imágenes (MinIO/S3) + biblioteca de medios

**Depende de:** 02, 03 · **Estado:** done — 2026-09-27

## Alcance
- `IAlmacenamientoObjetos` + `S3AlmacenamientoObjetos` (AWSSDK.S3, path-style, config `Storage:*`).
- `POST /api/admin/media` multipart: valida tamaño (≤ 10 MB), content-type y **firma binaria** (jpeg/png/webp/gif; sin SVG), guarda con clave `yyyy/MM/{guid}.{ext}`.
- `GET /api/admin/media` paginado, `DELETE /api/admin/media/{id}` → 409 si la imagen está referenciada (secciones o proyectos).
- URL pública = `Storage:UrlPublicaBase + / + key` (`/media` detrás de nginx).
- Bucket creado y en lectura pública por `minio-init` (compose).

## Aceptación
- [x] Subir PNG/JPEG válido → `MediaDto` con URL que responde 200 con el mismo contenido.
- [x] Subir `.exe` renombrado a `.jpg`, archivo > 10 MB o SVG → 400.
- [x] Borrar imagen en uso → 409; borrar libre → objeto eliminado del bucket y de la BD.

## Evidencia (2026-09-27)
Smoke: PNG válido → 201 y descarga idéntica byte a byte; `.jpg` con texto, SVG, imagen > 10 MB y petición sin archivo → 400/413; borrar en uso → 409; borrar libre → objeto ausente del bucket (404). Nota: se descubrió que el SDK de S3 cierra el stream tras `PutObject` (bug corregido: tamaño capturado antes de subir).
