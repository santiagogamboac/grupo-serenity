# 12 — Frontend: panel de administración

**Depende de:** 03–09 · **Estado:** done — 2026-09-27

## Alcance
Rutas `/admin/*` (wouter) sobre shadcn/ui: login, guard, layout con sidebar, y pantallas:
Dashboard · Proyectos (lista, crear/editar, galería con subida, reordenar, publicar) · Reseñas · Valores · Contenido del sitio (una pestaña por sección, selector de imagen desde biblioteca/subida) · Solicitudes (filtros, detalle, estado, notas) · Medios · Cambiar contraseña.
Auth: access token en memoria + refresh en `localStorage`, interceptor 401 con refresh único en vuelo.

## Aceptación
- [x] Flujo completo probado en navegador real (Playwright): login → crear proyecto con 2 imágenes → aparece en la landing → editar → despublicar → borrar.
- [x] Ruta `/admin/*` sin sesión redirige a login. Logout limpia tokens.
- [x] Responsive básico (≥ 375 px) y sin errores en consola.

## Evidencia (2026-09-27)
Playwright sobre `http://localhost:8090`: login (mala clave → mensaje; buena → resumen), recarga conserva sesión (refresh), crear proyecto → subir imagen + elegir de biblioteca → visible en la landing → despublicar/republicar → borrar, editar contenido (portada), cambiar estado de solicitud, cerrar sesión limpia el token y `/admin/*` redirige a login. Móvil 390 px verificado (`admin-proyectos-390.png`). Capturas en `Task/evidencia/`. También recorridas en navegador: crear reseña y valor desde el diálogo, borrar una imagen en uso (toast “está en uso”, 409) y validación del formulario de cambio de contraseña. Se corrigió un warning de accesibilidad (`DialogDescription` faltante).
