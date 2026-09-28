# Contrato de API (v1)

Base: `/api`. JSON camelCase. Errores: RFC 7807 (`application/problem+json`); validación →
400 con `extensions.errors[{propertyName, errorMessage}]`; 401/404/409 según el caso; 413 si el cuerpo excede el límite; 429 por rate limit.

Imágenes: `MediaDto = { id: number, url: string, nombreOriginal: string }`.

## Público (anónimo)

Formato de titulares: `
` = salto de línea, `*palabra*` = énfasis. Las propiedades opcionales (`?`) se omiten
cuando no hay valor (no viajan como `null`). La API pública nunca incluye los `xxxMediaId` internos.
Respuesta con `Cache-Control: no-cache`.

### `GET /api/public/home`
```jsonc
{
  "contenido": {
    "marca":    { "nombre", "tituloSitio", "leyenda", "logo": MediaDto?, "logoAlt", "pajaro": MediaDto? },
    "navegacion": { "items": [ { "texto", "href" } ], "ctaTexto", "ctaHref" },
    "anuncio":  { "activo", "texto", "ctaTexto", "ctaHref" },
    "hero":     { "kicker", "titulo", "texto", "ctaPrimarioTexto", "ctaPrimarioHref",
                  "ctaSecundarioTexto", "ctaSecundarioHref",
                  "imagenPrincipal": MediaDto?, "imagenPrincipalAlt",
                  "imagenSecundaria": MediaDto?, "imagenSecundariaAlt",
                  "notaDestinos", "selloLinea1", "selloCentral", "selloLinea3" },
    "intro":    { "etiqueta", "titulo", "lead", "texto", "enlaceTexto", "enlaceHref", "cita", "firma" },
    "valores":  { "etiqueta", "titulo", "texto" },
    "destinos": { "etiqueta", "titulo", "texto", "franjaEtiqueta", "franjaTitulo", "chipMarca" },
    "resenas":  { "etiqueta", "titulo", "calificacion", "detalle", "enlaceTexto", "enlaceUrl" },
    "reservas": { "etiqueta", "titulo", "texto", "whatsappTexto", "whatsappUrl", "nota",
                  "formTitulo", "formSubtitulo", "botonTexto", "aviso",
                  "exitoTitulo", "exitoDescripcion" },
    "footer":   { "eslogan", "redesTitulo", "facebookUrl", "instagramUrl", "youtubeUrl",
                  "copyright", "frase", "sitioActualTexto", "sitioActualUrl" }
  },
  "proyectos": [ { "id", "slug", "nombre", "etiqueta", "icono", "ubicacionEtiqueta", "titulo",
                   "descripcion", "metaLugar", "metaEstancia", "urlReserva", "ctaTexto",
                   "imagenes": [ { "id", "url", "textoAlt" } ] } ],   // solo publicados, por orden
  "valores":   [ { "id", "titulo", "descripcion", "icono", "variante" } ],
  "resenas":   [ { "id", "autor", "contexto", "texto", "calificacion" } ]
}
```

### `POST /api/public/solicitudes` — rate limit 5/min/IP
Body: `{ nombre, contacto, fechaLlegada (yyyy-MM-dd, ≥ hoy), huespedes (1..20), proyectoId?: number|null, destinoTexto?: string, sitioWeb?: string /*honeypot*/ }`
→ `201 { id }`. Honeypot lleno → `202` sin persistir.

## Auth

| Método | Ruta | Auth | Body → Respuesta |
|---|---|---|---|
| POST | `/api/auth/login` | — | `{correo, password}` → `{user:{id,nombre,correo,rol}, tokens:{accessToken, refreshToken, expiresIn}}`; 401 credenciales; 423 bloqueado (5 fallos → 15 min); 20 req/min/IP |
| POST | `/api/auth/refresh` | — | `{refreshToken}` → `tokens` (rotación) |
| POST | `/api/auth/logout` | Bearer | — → 204 (revoca refresh tokens) |
| GET | `/api/auth/me` | Bearer | → `user` |
| POST | `/api/auth/cambiar-password` | Bearer | `{passwordActual, passwordNueva}` → 204 |

## Admin (Bearer, rol admin)

| Recurso | Endpoints |
|---|---|
| Dashboard | `GET /api/admin/dashboard` → `{solicitudesNuevas, solicitudesTotal, proyectosPublicados, proyectosTotal, resenasPublicadas, valoresActivos}` |
| Catálogos | `GET /api/admin/catalogos` → `{iconos:[], variantesValor:[], estadosSolicitud:[]}` |
| Contenido | `GET /api/admin/contenido` → las 10 secciones (`marca, navegacion, anuncio, hero, intro, valores, destinos, resenas, reservas, footer`), con `xxxMediaId` y la imagen expandida · `PUT /api/admin/contenido/{clave}` (body = sección; imágenes por `xxxMediaId`) |
| Proyectos | `GET/POST /api/admin/proyectos` · `GET/PUT/DELETE /api/admin/proyectos/{id}` · `PUT /api/admin/proyectos/orden {ids}` · `POST /{id}/imagenes {mediaId,textoAlt}` · `PUT /{id}/imagenes/orden {ids}` · `PUT /{id}/imagenes/{imagenId} {textoAlt}` · `DELETE /{id}/imagenes/{imagenId}` |
| Reseñas | `GET/POST /api/admin/resenas` · `GET/PUT/DELETE /{id}` · `PUT /orden {ids}` |
| Valores | `GET/POST /api/admin/valores` · `GET/PUT/DELETE /{id}` · `PUT /orden {ids}` |
| Solicitudes | `GET /api/admin/solicitudes?estado=&q=&page=&pageSize=` → `PagedResult` · `GET /{id}` · `PUT /{id} {estado, notas}` · `DELETE /{id}` |
| Medios | `POST /api/admin/media` (multipart `archivo`, `≤10 MB`, jpeg/png/webp/gif) → `MediaDto` · `GET /api/admin/media?page=&pageSize=` · `DELETE /{id}` (409 si está en uso) |

`PagedResult<T> = { items, page, pageSize, totalCount, totalPages }`.

## Utilidades
`GET /health` → 200 cuando la API y la BD responden.
