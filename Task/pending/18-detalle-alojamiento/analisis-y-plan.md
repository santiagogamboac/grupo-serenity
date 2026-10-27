# 18 — Detalle de alojamiento, cobro con Wompi en producción y calendario Airbnb en dos vías

**Estado: APROBADO** (2026-10-26) con galería. Estado vivo de la ejecución: `tareas.md`.
Base: `17-reserva-directa-wompi.md` (reserva + Wompi, ya implementado en backend) y `calendario/README.md` (importación iCal).
Reparto con la sesión paralela (serenity-24): ella termina el cableado de la reserva en la landing (`Reservas.tsx`,
`ReservaEnLinea.tsx`, `/reserva/:codigo`, admin de alojamientos y reservas). Esta tarea crea archivos **nuevos** para el
detalle y solo toca los suyos con aviso previo.

## 1. Análisis (Fase 1, solo lectura)

**Stack:** .NET 10 (Clean Architecture, MediatR, Dapper, migraciones SQL embebidas `V###__*.sql` aplicadas al arrancar),
PostgreSQL 16, MinIO/S3, React 19 + Vite + TanStack Query. Endpoints = controllers delgados → `Result` → HTTP.
Workers = `BackgroundService` en `Api/Workers`.

**Modelo (ya existe, V002 + V003):**
| Pide el documento | Existe como | Diferencia |
|---|---|---|
| Propiedad | `alojamientos` (unidad reservable de un `proyecto`: precio, capacidad, aseo, estancia mínima, 1 imagen) | — |
| `AirbnbIcalUrl` | `anuncios.ical_url` (secreta, validada contra `/rooms/<id>`, solo hosts de Airbnb), ligado por `alojamientos.anuncio_id` | Vive en el anuncio, no en el alojamiento (un anuncio = un alojamiento, `UNIQUE`). |
| `CalendarToken` | `alojamientos.token_ical` (48 hex, `RandomNumberGenerator`) | — |
| `LastAirbnbSyncAt` | `anuncios.ultima_sync_ok` / `ultima_sync_error` | — |
| `ExternalBlock` | `disponibilidad_bloqueos (anuncio_id, desde, hasta, origen, uid_externo, UNIQUE(anuncio_id, uid_externo))` | — |
| Reserva | `reservas` (estados `pendiente_pago/confirmada/cancelada/expirada/revision`, exclusión gist anti-solape) | — |

**Disponibilidad hoy:** `AlojamientoRepository.OcupacionAsync` = UNIÓN de reservas vivas (confirmadas + pendientes de pago
no vencidas) + bloqueos manuales + bloqueos de Airbnb del anuncio vinculado. Cruce semiabierto `[llegada, salida)`
(`daterange &&`): el día de salida y el de llegada no chocan. Crear reserva = cotizar → refresco del iCal de Airbnb
(con turno de 120 s) → `CrearSiDisponibleAsync` con `pg_advisory_xact_lock` + exclusión gist. Al aprobarse el pago,
`ConfirmarSiDisponibleAsync` vuelve a validar; si ya no hay cupo → estado `revision` (reembolsar o reubicar).
Retención: 30 min con Wompi, 24 h sin pago en línea; un worker expira las vencidas.

**Exportación .ics:** `GET /api/public/ical/{token}.ics` por alojamiento: CRLF, `VALUE=DATE`, DTEND exclusivo, UID estable,
**sin** los bloqueos de Airbnb (evita el bucle), sin datos del huésped; 404 con token inválido. La URL se ve en el admin.
**Importación:** `AnuncioSyncService` + `AirbnbSyncWorker` (cada 5 min), "Sincronizar ahora" en el admin, reemplazo
transaccional por UID y, si la descarga falla, se conservan los bloqueos anteriores. El parser es propio (`Common/Ical.cs`), con tests.

**Despliegue:** stack en la VPS con HTTPS público `https://serenity.51.161.114.45.sslip.io` (el feed responde 404 con token
falso ✔). En producción: `pagoEnLinea=false` (**faltan las llaves de Wompi**) y **0 alojamientos** cargados.

## 2. Brechas frente al documento pedido

1. **Sincronización "justo antes de cobrar":** hoy el refresco se hace al crear la reserva con un turno de 120 s, pero el cobro en
   Wompi puede llegar hasta 30 min después. → Forzar la sincronización (sin turno, con timeout corto) al crear la reserva **y** al
   generar cada URL de pago (`/reservas/{codigo}/pago`). Si Airbnb ya ocupó las fechas → 409 *antes* de mandar al huésped a Wompi.
2. **Test de integración del feed** (token válido → 200 + `.ics` válido; inválido → 404): hoy solo lo cubre el smoke. → Agregarlo.
3. **Parser:** el documento sugiere una librería (Ical.Net). El parser propio ya está probado con feeds de Airbnb y no trae
   dependencias. **Propuesta: mantenerlo** y sumar fixtures reales ("Reserved", "Airbnb (Not available)") ⛔ CONFIRMAR.
4. **Reservas pendientes en el feed de salida:** hoy se exportan (retienen 30 min). Es lo seguro, aunque Airbnb lea el feed cada
   ~3 h. El documento pide solo las confirmadas. **Propuesta: exportar confirmadas + pendientes** ⛔ CONFIRMAR.
5. **Comparación del token en tiempo constante:** se busca por índice único (token de 192 bits). Riesgo práctico nulo; se puede
   comparar con `FixedTimeEquals` tras la búsqueda sin costo. → Hacerlo.
6. **Wompi en producción:** faltan las llaves (sandbox → producción), registrar la URL de eventos en el dashboard de Wompi y
   `Reservas:UrlSitio` con el dominio definitivo.
7. **Detalle por alojamiento en el sitio:** no existe (ver §4).

## 3. ¿Dónde administrar para no duplicar? (recomendación)

- **El panel de Serenity es la fuente de verdad del sitio.** Cada **alojamiento** (fotos, descripción, precio, capacidad,
  temporadas, bloqueos) se administra en un solo lugar, y su vínculo con Airbnb (URL del anuncio + iCal de exportación)
  va **dentro del mismo formulario del alojamiento**. Hoy "Anuncios" y "Alojamientos" son dos listas por proyecto: se unifican en
  la UI (cada alojamiento tiene una sección "Airbnb"; las tablas no cambian).
- **El calendario es uno solo:** Serenity guarda la unión (reservas web + bloqueos manuales + Airbnb). Airbnb importa nuestro
  `.ics` y nosotros el suyo. La disponibilidad no se duplica.
- **Lo que sí se mantiene doble es el texto y las fotos del anuncio en Airbnb.** Airbnb no tiene API para anfitriones, así
  que nada puede escribir allí. Es lo normal con iCal.
- **Cuándo cambiar:** con más de unos 5 anuncios, ocupación alta o más canales (Booking, Vrbo), conviene un channel manager
  (Hospitable, Smoobu, Hostaway…). Ellos sí usan la API de Airbnb: contenido, precios y calendario en tiempo real. En ese caso
  la fuente de verdad pasa a ser el channel manager y el sitio lee de su API. Hoy no hace falta.

## 4. Detalle de alojamiento (frontend)

- Ruta nueva `/alojamientos/:id` (página `pages/AlojamientoPage.tsx` + `features/public/alojamiento/*`, archivos nuevos).
  Datos: `useHome().data.reservaDirecta.alojamientos` (sin endpoint nuevo). Si el id no existe o no está publicado → NotFound.
- Contenido, con el mismo lenguaje de vidrio claro de la landing:
  galería/foto principal, nombre, proyecto/destino, descripción, capacidad y huéspedes incluidos, "desde $X / noche",
  aseo y estancia mínima. Tarjeta fija de reserva (sticky en escritorio, barra inferior en móvil) con **calendario de
  disponibilidad** (`CalendarioReserva`, que exporta serenity-24), huéspedes, **cotización en vivo** (`useCotizacion`),
  datos del huésped y "Reservar y pagar" → `useCrearReserva` → redirección a Wompi (o aviso de "fechas apartadas" sin pago
  en línea) → `/reserva/:codigo`.
- Entrada: en la sección Destinos, tarjetas de los alojamientos del proyecto activo (foto, precio, capacidad, "Ver detalle").
  Ese cambio va en `Destinos.tsx`, que es de serenity-24, y se coordina con ella.
- SEO/UX: `document.title` con el nombre, scroll al inicio, estados de carga/error, accesible y responsive (390/820/1440 px).
- ⛔ CONFIRMAR **galería de varias fotos**: hoy un alojamiento tiene **una** imagen. Para una galería hace falta la migración
  `V005__alojamiento_imagenes.sql` (V004 es de serenity-24), su CRUD en el admin y el campo en la API pública. Sin eso,
  el detalle muestra la foto del alojamiento y, como respaldo, las del proyecto.

## 5. Fases y checklist

> Checklist **original** del plan (no se actualiza). Estado real: `tareas.md`. A–D quedaron hechas el 2026-10-26, salvo la
> sección "Airbnb" unificada dentro del alojamiento (D, primera casilla). E–F pasaron a `../19-configuracion-wompi-airbnb.md`.

### A. Backend: cierre de la ventana de doble reserva
- [ ] `CalendarioAlojamientos.OcupacionAsync(..., forzar: true)` en crear reserva y nueva URL de pago (timeout 10 s; si Airbnb
      no responde, se usa el último sync y se registra en el log).
- [ ] 409 claro si Airbnb ocupó las fechas entre la cotización y el pago.
- [ ] Feed: `FixedTimeEquals` del token; tests de integración del endpoint (200/404, CRLF, sin bloqueos `airbnb`).
- [ ] Fixtures de un `.ics` real de Airbnb (Reserved / Not available) en `AnunciosTests`.

### B. (Opcional, si se confirma) Galería del alojamiento
- [ ] `V005__alojamiento_imagenes.sql`, repositorio, CRUD admin, `imagenes[]` en `AlojamientoPublicoDto` + seed.

### C. Frontend público: detalle de alojamiento
- [ ] `/alojamientos/:id` con galería, ficha, calendario, cotización, reserva y redirección a Wompi.
- [ ] Tarjetas de alojamientos en Destinos (coordinado) y enlace desde la reserva en línea.
- [ ] `pnpm check` (sin errores nuevos), `pnpm test`, `vite build`, prueba en navegador del flujo completo con Wompi **sandbox**.

### D. Admin
- [ ] Sección "Airbnb" dentro del alojamiento (URL del anuncio, iCal de Airbnb, feed propio para copiar, "Sincronizar ahora",
      última sincronización y bloqueos importados). Coordinado con serenity-24 (`AlojamientosProyecto.tsx`).

### E. Producción ⛔ CONFIRMAR antes de desplegar
- [ ] Llaves sandbox de Wompi → prueba punta a punta → llaves de producción. Registrar `https://<dominio>/api/public/pagos/wompi/eventos`.
- [ ] Decidir el dominio definitivo **antes** de pegar el feed en Airbnb: si después cambia, hay que reconectar cada calendario.
- [ ] Cargar alojamientos reales en el panel.

### F. Airbnb desde el navegador (Claude in Chrome), con las confirmaciones del documento
- [ ] Elegir anuncio(s) ⛔, copiar su iCal de exportación ⛔ → guardarlo por el panel → sincronizar y comparar bloqueos.
- [ ] Importar en Airbnb el feed del alojamiento con el nombre "Página web propia" ⛔ antes de guardar.
- [ ] Si Airbnb pide login, verificación o CAPTCHA, se detiene y lo hace el dueño.

## Limitaciones conocidas
- Airbnb lee los calendarios importados cada ~2–3 h: una reserva web tarda horas en bloquear Airbnb. Con ocupación alta,
  deja un día de margen entre reservas o bloquea a mano en Airbnb las reservas web del mismo día.
- El iCal no distingue "reservado" de "no disponible" ni trae precios ni huéspedes.

## Necesito del dueño
- [ ] Confirmar las propuestas ⛔ (parser, pendientes en el feed, galería).
- [ ] Llaves de Wompi sandbox (y luego producción) y acceso al dashboard para registrar la URL de eventos.
- [ ] Dominio definitivo del sitio.
- [ ] Lista de anuncios de Airbnb y a qué alojamiento corresponde cada uno.
