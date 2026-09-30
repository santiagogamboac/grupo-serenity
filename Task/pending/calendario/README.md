# Sincronizar el calendario de reservas de Airbnb — alternativas y multi-anuncio (2026-09-29)

**Objetivo:** que las fechas ocupadas en Airbnb se reflejen en el sitio, y saber **a qué anuncio pertenece** cada calendario
cuando hay varios. Contexto: `../16-airbnb-calendario-resenas.md`. Reseñas: `../reviews/README.md`.

## 1. Qué es y qué no es el iCal de Airbnb (según el centro de ayuda de Airbnb)
- Es **por anuncio**: se elige el anuncio en Calendario y se exporta/importa su enlace. No hay un iCal "de toda la cuenta".
- Una URL de importación debe terminar en `.ics`.
- Los días bloqueados mezclan reservas confirmadas y bloqueos del anfitrión (preparación, aviso previo, estancia mínima):
  **desde el feed no se distingue "reservado" de "no disponible"**. Sin datos del huésped ni precios.
- Frecuencia: Airbnb dice que sincroniza los calendarios conectados **cada ~3 horas** (otras páginas de ayuda dicen 2 h) y hay
  un refresco manual con límite de uso. Es un límite de Airbnb, no nuestro.
- Solo importa 2 años de historial; un calendario importado no se puede pausar (hay que desconectar y reconectar).
- Airbnb tiene una guía para **vincular calendarios de varios anuncios** del mismo inmueble (ej. casa completa + habitación) y
  evitar dobles reservas.

## 2. Alternativas

| # | Vía | Latencia Airbnb→Serenity | Serenity→Airbnb | Costo | Nota |
|---|---|---|---|---|---|
| A | **iCal por anuncio + sondeo propio** (recomendada para empezar) | Nuestro sondeo (5 min) + retraso del feed de Airbnb (no documentado) | Airbnb lee nuestro `.ics` cada ~3 h | 0 | Suficiente si la reserva final ocurre en Airbnb. |
| B | A + **consulta al instante** al abrir el selector de fechas / antes de confirmar (caché 1–2 min) | Igual que A, pero sin sumar nuestro intervalo | Igual | 0 | Mejora barata: sumar a A. |
| C | A/B + **bloqueo provisional** de solicitudes en Serenity | — | Sitio bloquea al instante lo pedido; el equipo bloquea a mano en Airbnb (inmediato) | 0 | Cierra la ventana de doble reserva sin API. |
| D | **Channel manager / PMS** (Hostaway, Guesty, Hospitable, Smoobu, Lodgify…) | Minutos o segundos (API oficial) | Igual | Mensual | Único tiempo real de verdad; permite reservar directo en el sitio. |
| E | Airbnb API directa | — | — | — | No disponible para cuentas normales (programa de socios cerrado a solicitudes). |

Recomendación: **A + B + C**. Pasar a D solo si hay reserva directa en el sitio o varios canales de venta.

## 3. ¿A qué publicación pertenece un calendario? (varios anuncios)
El `.ics` **no dice** de qué anuncio es (y sus `UID` de evento no lo identifican): la relación la definimos nosotros.

### Regla
> **Cada URL iCal se registra explícitamente contra un anuncio; nunca se infiere del contenido del `.ics`.**

- La URL de exportación de Airbnb tiene la forma `https://www.airbnb.com/calendar/ical/<ID_ANUNCIO>.ics?s=<secreto>`
  (formato a **confirmar con una URL real**). El `<ID_ANUNCIO>` coincide con el número de `airbnb.com/rooms/<ID_ANUNCIO>`
  (ej. el anuncio de referencia: `rooms/557255594023352394`). Sirve como **verificación cruzada**: al guardar, el admin pega
  la URL del anuncio y la del iCal; si los IDs no coinciden, se rechaza (evita cruzar calendarios).
- Un **proyecto** de Serenity (destino/finca) puede tener **varios anuncios** (cabañas, habitaciones, casa completa).

### Modelo
Nueva tabla `proyecto_anuncios` (migración `V00x`):
```
id              BIGSERIAL PK
proyecto_id     BIGINT NOT NULL REFERENCES proyectos(id) ON DELETE CASCADE
nombre          VARCHAR(120) NOT NULL          -- "Cabaña del lago", "Habitación 2"
airbnb_room_id  VARCHAR(30)  NOT NULL UNIQUE   -- extraído de la URL /rooms/<id>
url_anuncio     VARCHAR(500) NOT NULL          -- pública, para "ver en Airbnb"
ical_url        VARCHAR(600) NULL              -- SECRETA: nunca en API pública ni en logs
ultima_sync_ok  TIMESTAMPTZ NULL
ultima_sync_error TEXT NULL
publicado       BOOLEAN NOT NULL DEFAULT TRUE
```
`disponibilidad_bloqueos(id, anuncio_id, desde, hasta, origen 'airbnb'|'manual'|'solicitud', uid_externo, actualizado_en,
UNIQUE(anuncio_id, uid_externo))`.

### Reglas de negocio con varios anuncios
1. **Bloqueos y sincronización por anuncio**: cada `.ics` reemplaza solo los bloqueos `origen='airbnb'` de *su* anuncio, en
   transacción; si la descarga falla se conservan los anteriores.
2. **Disponibilidad a nivel proyecto** (lo que ve el visitante): configurable por proyecto —
   *"alguno libre"* (default: el día está libre si al menos un anuncio lo está) o *"anuncio concreto"* (el visitante elige).
3. **Anuncios que se solapan** (casa completa vs. habitación): usar el vínculo de calendarios de Airbnb; en Serenity, un
   campo opcional `bloquea_a` (lista de anuncios) para propagar el bloqueo. **No implementar hasta que el cliente confirme
   que tiene esa situación** (YAGNI).
4. **Feed de salida** `calendario.ics?token=…` **por anuncio** (token propio), solo con bloqueos `manual`/`solicitud`.
   **Nunca** re-exportar los `airbnb` (crearía un bucle de sincronización).
5. **Solicitud** (`solicitudes`): añadir `anuncio_id NULL`; si el rango cae en ocupado → 409.
6. **Reseñas** se ligan a `anuncio_id` (ver `../reviews/README.md`), así el sitio muestra las del anuncio correcto.

### Admin (UX)
Dentro de cada proyecto: lista de anuncios → agregar (nombre, URL del anuncio de Airbnb, URL iCal) → validación cruzada de ID →
botón "Probar y sincronizar" (muestra n.º de bloqueos leídos y error si lo hay) → estado de última sincronización.

## 4. Riesgos
- **Feed = bloqueo genérico**: no se puede mostrar "reservado" vs "no disponible". Se muestra siempre "no disponible".
- **Secreto**: `ical_url` da acceso al calendario; cifrar en reposo si es posible y no loguearlo.
- **Desfase de Airbnb**: hasta horas en ambos sentidos; mitigado con B y C, eliminado solo con D.
- **Bucle**: nunca reimportar el propio feed de Serenity como origen `airbnb`.
- **Zonas horarias**: eventos de día completo con fin exclusivo (`DTEND` = día siguiente); tests en frontera (UTC-5 Colombia).

## 5. Necesito del cliente
- [ ] URLs de todos los anuncios de Airbnb y a qué proyecto/destino pertenece cada uno.
- [ ] Enlace iCal de exportación de cada uno (Airbnb → Calendario → elegir anuncio → Disponibilidad → Sincronizar calendarios → Exportar).
- [ ] ¿Hay anuncios que se solapan (casa completa + habitaciones)?
- [ ] ¿Reserva final en Airbnb o por solicitud al equipo? ¿Se venderá también por otros canales?
- [ ] ¿El visitante debe elegir anuncio o solo ver disponibilidad del destino?

## 6. Aceptación
- [ ] Cada anuncio se sincroniza con su propio `.ics`; ID de la URL iCal ≠ ID del anuncio → rechazado al guardar.
- [ ] Dos anuncios en un proyecto: bloqueos independientes; disponibilidad de proyecto según la regla elegida.
- [ ] `.ics` caído → se conservan bloqueos y el admin ve el error; la API pública nunca expone `ical_url`.
- [ ] Solicitud sobre fechas ocupadas → 409; feed de salida por anuncio aceptado por Airbnb en una prueba real.
- [ ] Tests: parser (día completo, fin exclusivo, zona horaria), reemplazo transaccional, no-bucle, solapamiento.

## Fuentes
- Centro de ayuda de Airbnb, "Sincroniza tu calendario de Airbnb con otros sitios web": https://www.airbnb.ca/help/article/99
- Airbnb Help (importar/exportar, 3 h, límites): mismo artículo; enlaces relacionados "Linking calendars for multiple listings".
