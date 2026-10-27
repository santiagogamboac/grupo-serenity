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
                  "exitoTitulo", "exitoDescripcion",
                  "reservaTitulo", "reservaSubtitulo", "reservaBotonTexto", "condiciones", "reservaSinPagoTexto" },
    "footer":   { "eslogan", "redesTitulo", "facebookUrl", "instagramUrl", "youtubeUrl",
                  "copyright", "frase" }
  },
  "proyectos": [ { "id", "slug", "nombre", "etiqueta", "icono", "ubicacionEtiqueta", "titulo",
                   "descripcion", "metaLugar", "metaEstancia", "urlReserva", "ctaTexto",
                   "imagenes": [ { "id", "url", "textoAlt" } ] } ],   // solo publicados, por orden
  "valores":   [ { "id", "titulo", "descripcion", "icono", "variante" } ],
  "resenas":   [ { "id", "autor", "contexto", "texto", "calificacion" } ],
  // Reserva en línea: alojamientos publicados (de proyectos publicados). Sin alojamientos el sitio solo ofrece la consulta.
  "reservaDirecta": { "pagoEnLinea": boolean,   // false = Wompi sin configurar (el equipo confirma a mano)
                      "alojamientos": [ { "id", "proyectoId", "nombre", "descripcion", "imagen": MediaDto|null,
                                          "capacidad", "huespedesIncluidos", "precioNoche", "recargoHuespedExtra",
                                          "tarifaAseo", "estanciaMinima" } ] }
}
```

`urlReserva` es un enlace externo opcional; si el proyecto tiene alojamientos publicados, el botón del destino lleva a
`#reservas` con ese destino elegido. El sitio no enlaza a ninguna URL del WordPress anterior.

### `POST /api/public/solicitudes` — rate limit 5/min/IP
Body: `{ nombre, contacto, fechaLlegada (yyyy-MM-dd, ≥ hoy), huespedes (1..20), proyectoId?: number|null, destinoTexto?: string, sitioWeb?: string /*honeypot*/ }`
→ `201 { id }`. Honeypot lleno → `202` sin persistir.

### Reserva directa (Wompi)
Fechas `yyyy-MM-dd`; rangos `[desde, hasta)` (la salida no es noche ocupada). Montos en pesos enteros (COP).

| Método | Ruta | Body → Respuesta |
|---|---|---|
| GET | `/api/public/alojamientos/{id}/disponibilidad?desde=&hasta=` | → `{alojamientoId, desde, hasta, ocupado:[{desde, hasta}]}` (reservas vivas + bloqueos manuales + iCal de Airbnb del anuncio vinculado) |
| POST | `/api/public/reservas/cotizar` | `{alojamientoId, llegada, salida, huespedes}` → `{cotizacion:{alojamientoId, llegada, salida, huespedes, noches, tramos:[{desde, hasta, noches, precioNoche, temporada?}], huespedesExtra, recargoHuespedes, subtotal, tarifaAseo, total, moneda}, disponible}` · rate limit |
| POST | `/api/public/reservas` | `{alojamientoId, llegada, salida, huespedes, nombre, correo, telefono, nota?, sitioWeb /*honeypot*/}` → `201 {codigo, token, total, pagoEnLinea, checkoutUrl?, expiraEn?}`; `409` si las fechas ya no están libres; honeypot → `202` · rate limit |
| GET | `/api/public/reservas/{codigo}?t={token}` | → `{codigo, estado, motivoEstado?, alojamientoNombre, proyectoNombre, llegada, salida, noches, huespedes, nombre, subtotal, tarifaAseo, total, moneda, expiraEn?, pagoEnLinea, pagoEstado?, puedePagar}`; `404` con token incorrecto · `Cache-Control: no-store` |
| POST | `/api/public/reservas/{codigo}/pago?t=` | → `{checkoutUrl}` (nuevo intento mientras la retención siga vigente) |
| POST | `/api/public/reservas/{codigo}/verificar?t=` | `{transaccionId}` → reserva (consulta la transacción en Wompi; al volver del checkout con `?id=`) |
| POST | `/api/public/pagos/wompi/eventos` | Evento de Wompi firmado (checksum SHA256) → `200` (idempotente); `401` firma inválida |
| GET | `/api/public/ical/{token}.ics` | Feed iCalendar del alojamiento (reservas vivas + bloqueos manuales) para importar en Airbnb |

Estados: `pendiente_pago` (retiene fechas hasta `expiraEn`) · `confirmada` · `cancelada` · `expirada` · `revision`
(pago aprobado que no se pudo aplicar solo: lo decide el equipo).

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
| Dashboard | `GET /api/admin/dashboard` → `{solicitudesNuevas, solicitudesTotal, proyectosPublicados, proyectosTotal, resenasPublicadas, valoresActivos, reservasPendientesPago, reservasEnRevision, reservasProximas}` |
| Catálogos | `GET /api/admin/catalogos` → `{iconos:[], variantesValor:[], estadosSolicitud:[], estadosReserva:[], secciones:[]}` |
| Contenido | `GET /api/admin/contenido` → las 10 secciones (`marca, navegacion, anuncio, hero, intro, valores, destinos, resenas, reservas, footer`), con `xxxMediaId` y la imagen expandida · `PUT /api/admin/contenido/{clave}` (body = sección; imágenes por `xxxMediaId`) |
| Proyectos | `GET/POST /api/admin/proyectos` · `GET/PUT/DELETE /api/admin/proyectos/{id}` · `PUT /api/admin/proyectos/orden {ids}` · `POST /{id}/imagenes {mediaId,textoAlt}` · `PUT /{id}/imagenes/orden {ids}` · `PUT /{id}/imagenes/{imagenId} {textoAlt}` · `DELETE /{id}/imagenes/{imagenId}` |
| Reseñas | `GET/POST /api/admin/resenas` · `GET/PUT/DELETE /{id}` · `PUT /orden {ids}` |
| Valores | `GET/POST /api/admin/valores` · `GET/PUT/DELETE /{id}` · `PUT /orden {ids}` |
| Solicitudes | `GET /api/admin/solicitudes?estado=&q=&page=&pageSize=` → `PagedResult` · `GET /{id}` · `PUT /{id} {estado, notas}` · `DELETE /{id}` |
| Alojamientos | `GET/POST /api/admin/proyectos/{proyectoId}/alojamientos` · `PUT/DELETE /api/admin/alojamientos/{id}` (body `{nombre, descripcion, imagenMediaId?, capacidad, huespedesIncluidos, precioNoche, recargoHuespedExtra, tarifaAseo, estanciaMinima, anuncioId?, publicado}`; respuesta con `imagen`, `orden`, `urlIcal`, `tarifas[]`) · `POST /api/admin/alojamientos/{id}/tarifas {nombre, desde, hasta, precioNoche}` · `DELETE /api/admin/tarifas/{id}` · `GET/POST /api/admin/alojamientos/{id}/bloqueos {desde, hasta, motivo?}` · `DELETE /api/admin/bloqueos/{id}` |
| Reservas | `GET /api/admin/reservas?estado=&q=&page=&pageSize=` → `PagedResult` · `GET /{id}` · `POST /{id}/confirmar` (pago manual) · `POST /{id}/cancelar {motivo, avisarHuesped}` · `PUT /{id}/notas {notas}` — todas devuelven la reserva con datos del huésped, del pago y `enlaceHuesped` |
| Medios | `POST /api/admin/media` (multipart `archivo`, `≤10 MB`, jpeg/png/webp/gif) → `MediaDto` · `GET /api/admin/media?page=&pageSize=` · `DELETE /{id}` (409 si está en uso) |

`PagedResult<T> = { items, page, pageSize, totalCount, totalPages }`.

## Utilidades
`GET /health` → 200 cuando la API y la BD responden.
