# 11 — Frontend: sitio público 100 % dinámico

**Depende de:** 09, 10 · **Estado:** done — 2026-09-27

## Alcance
- Extraer cada dato hardcodeado de `Home.tsx` (textos, imágenes, reseñas, destinos, redes, WhatsApp, año, número de destinos, opciones del select) → API.
- `Home.tsx` (210 líneas monolíticas) → secciones en `client/src/features/public/`.
- `@tanstack/react-query`, cliente axios único, tipos del contrato, `RichTitle` (`\n` y `*em*`), estados de carga (skeleton) y error (reintentar).
- Pestañas de destino generadas desde `proyectos` (icono/etiqueta/foto/franja).
- Formulario → `POST /api/public/solicitudes` con validación (react-hook-form + zod), campo de contacto, honeypot, toast de éxito/error.
- `document.title` desde `marca.tituloSitio`.
- Ya no queda ninguna URL `gruposerenity.com/wp-content` en el código.

## Aceptación
- [x] `grep -r "wp-content" Frontend/client` = 0 resultados; ningún texto de marketing literal en componentes.
- [x] La página se ve igual que antes (comparación visual con capturas).
- [x] Editar un texto en admin → se ve en la landing al recargar.
- [x] Enviar el formulario crea la solicitud en admin.
- [x] `pnpm check` y `pnpm build` verdes; tests vitest de `RichTitle`.

## Evidencia (2026-09-27)
`grep` de `wp-content|gruposerenity|wa.link|facebook.com|instagram.com` y de textos de marketing en `client/src` → 0. `pnpm check` limpio; `pnpm test` 8/8 (`RichTitle`/`parseRich`); `vite build` OK (dentro del Docker build). Navegador: página idéntica al original (capturas en `Task/evidencia/`), editar texto en admin se ve al recargar, formulario crea solicitud, validación inline, 0 mensajes de consola. Extras: campo “Correo o WhatsApp” (D11), se quitó el `<script>` de analítica sin configurar (`%VITE_ANALYTICS_ENDPOINT%`, daba 404 en consola). El `<title>` estático de `index.html` queda solo como respaldo; en runtime lo pone `marca.tituloSitio`.
