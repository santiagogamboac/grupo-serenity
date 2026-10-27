# 17 — Reserva directa con pago en línea (Wompi): reemplazo del WordPress

**Estado: HECHO** (2026-10-26) · aprobado el 2026-10-18. Plan autoaprobado (ver preferencia del dueño: definir, aprobar, ejecutar y autoprobar).

## Contexto

El sitio en producción (`gruposerenity.com`) es un WordPress en hosting compartido. El 2026-10-18 se cayó entero:
Contact Form 7 se actualizó a una versión que exige PHP ≥ 8.3 y el hosting corre PHP 7.4. El dueño no quiere nada de
WordPress: este proyecto (.NET + React) debe cubrir las mismas funcionalidades.

Inventario del WordPress (detectado por los `readme.txt` públicos de los plugins; el sitio y el Internet Archive estaban
caídos, así que no se pudo ver el contenido):

| Plugin | Función | Decisión |
|---|---|---|
| Elementor (+Pro) | Editor de páginas | Ya cubierto: contenido 100 % administrable desde el panel. |
| Click to Chat | Botón de WhatsApp | Ya cubierto (`reservas.whatsappUrl`). |
| Contact Form 7 | Formulario de contacto + correo | Formulario ya existe (solicitudes). **Falta el correo** → fase F. |
| WP Hotel Booking + Booking Calendar | Habitaciones, tarifas, calendario, reserva directa | **Esta feature.** |
| WooCommerce (`/shop`) + WooPayments | Tienda y cobro | El dueño confirma que **la tienda no se usaba**: no se replica. El cobro se hace con **Wompi** (WooPayments no acepta cuentas de Colombia). |
| Jetpack, LiteSpeed Cache | Infraestructura de WP | No aplica. |

Migración de datos: el dueño eligió **empezar sin migrar**; habitaciones y tarifas se cargan desde el panel.

## Modelo

- **Alojamiento** (unidad reservable, equivale a la "room" de WP Hotel Booking), pertenece a un proyecto: nombre,
  descripción, capacidad, huéspedes incluidos, precio por noche, recargo por huésped extra, tarifa de aseo, estancia
  mínima, publicado, orden y un **anuncio de Airbnb opcional** (sus bloqueos iCal también lo ocupan).
- **Tarifas por temporada**: rangos `[desde, hasta)` con un precio por noche que reemplaza al base (temporada alta).
- **Bloqueos manuales** por alojamiento (mantenimiento, uso propio).
- **Reserva**: código público (`SER-XXXXXX`), token secreto de consulta, alojamiento, llegada/salida, huéspedes, datos del
  huésped (nombre, correo, teléfono, nota), desglose (noches, subtotal, aseo, total en COP enteros), estado y datos del pago.
  Estados: `pendiente_pago` (retiene las fechas hasta `expira_en`), `confirmada`, `cancelada`, `expirada`, `revision`
  (pago aprobado que ya no se puede honrar automáticamente: monto distinto o fechas tomadas → el equipo decide/reembolsa).
- Dinero en pesos enteros (`BIGINT`); a Wompi se envía `total * 100` centavos.

Doble reserva: `pg_advisory_xact_lock` por alojamiento al crear/confirmar + restricción de exclusión
(`btree_gist`, `daterange && daterange`) sobre reservas vivas como red de seguridad. Además se respetan los bloqueos de
Airbnb (iCal del anuncio vinculado) y los manuales. La ventana de retraso de Airbnb (horas) sigue existiendo: el feed iCal de
salida (fase E) la reduce del lado de Airbnb.

## Pagos (Wompi Web Checkout)

- El servidor arma la URL `https://checkout.wompi.co/p/` con `public-key`, `currency=COP`, `amount-in-cents`, `reference`
  (= código de la reserva), `signature:integrity` = SHA256(`reference + amountInCents + currency + expirationTime + integritySecret`),
  `redirect-url` (= `/reserva/{codigo}?t={token}`), `expiration-time` (= `expira_en` UTC) y `customer-data:*`.
- Webhook `POST /api/public/pagos/wompi/eventos`: valida el checksum (propiedades de `signature.properties` + `timestamp` +
  events secret, SHA256), es idempotente y siempre responde 200 si la firma es válida. `APPROVED` → confirma (o `revision`);
  `DECLINED/VOIDED/ERROR` → libera las fechas (cancelada con motivo).
- Al volver del checkout (`?id=<transacción>`), la página de la reserva pide al API que consulte la transacción a Wompi
  (`GET /v1/transactions/{id}`, con la llave privada) — así funciona también en local, donde el webhook no llega.
- Worker: expira retenciones vencidas y reconsulta las pendientes que ya tienen transacción.
- Sin llaves de Wompi configuradas el pago en línea queda **apagado**: la reserva se crea igual (retención larga) y el equipo
  la confirma desde el panel cuando recibe el pago por otro medio.

## Fases y checklist

### A. Base de datos y dominio
- [x] `V003__reserva_directa.sql`: `alojamientos`, `alojamiento_tarifas`, `alojamiento_bloqueos`, `reservas` (+ exclusión).
- [x] Cotizador puro (noches, temporadas, huésped extra, aseo, estancia mínima) con tests unitarios.

### B. API pública
- [x] Alojamientos publicados en `GET /api/public/home` (`reservaDirecta`) + `GET /api/public/alojamientos/{id}/disponibilidad`
      (en lugar del `GET /proyectos/{slug}/alojamientos` previsto: la landing ya trae todo en una sola llamada).
- [x] `POST /api/public/reservas/cotizar` y `POST /api/public/reservas` (rate limit, honeypot, 409 si las fechas se ocupan).
- [x] `GET /api/public/reservas/{codigo}?t=token`, `POST .../verificar` (consulta la transacción en Wompi) y `POST .../pago` (reintento).
- [x] `pagoEnLinea` en la respuesta para que el sitio sepa si redirige a Wompi.

### C. Wompi
- [x] Opciones `Wompi:*`, firma de integridad, validación de eventos (tests con los ejemplos de la documentación).
- [x] Webhook idempotente + cliente HTTP de transacciones + worker de expiración/reconciliación.

### D. Admin API
- [x] CRUD de alojamientos por proyecto, tarifas por temporada y bloqueos manuales.
- [x] Reservas: listar (filtros estado/búsqueda/fechas), detalle, confirmar (pago manual), cancelar, notas.
- [x] Dashboard: reservas confirmadas próximas, pendientes de pago y en revisión.

### E. Feed iCal de salida
- [x] `GET /api/public/ical/{tokenIcal}.ics` por alojamiento (reservas vivas + bloqueos manuales) para importarlo en Airbnb.

### F. Correo (reemplazo de Contact Form 7) — opcional por configuración
- [x] `IEnviadorCorreo` SMTP; sin `Smtp:Host` no envía (solo registra). Mailpit en docker-compose para desarrollo.
- [x] Avisos: al equipo (nueva solicitud, nueva reserva, reserva pagada) y al huésped (reserva confirmada).

### G. Frontend público
- [x] Sección de reservas con dos modos: **Reservar en línea** (alojamiento, calendario con días ocupados, cotización,
      datos y pago) y **Enviar consulta** (formulario actual). Si no hay alojamientos publicados, solo consulta.
- [x] Página `/reserva/:codigo` (vuelta de Wompi): estado, desglose y verificación del pago.
- [x] Textos editoriales nuevos en la sección `reservas` (BD + seed + `contenidoSchema.ts`).

### H. Frontend admin
- [x] Alojamientos dentro del formulario de proyecto (con tarifas, bloqueos y el enlace iCal para Airbnb).
- [x] Página **Reservas** (lista, detalle, acciones) y contadores en el menú/resumen.

### I. Verificación y documentación
- [x] `dotnet test`, `pnpm check && pnpm test && vite build`, bloque nuevo del smoke (incluye webhook firmado).
- [x] Prueba en navegador del flujo completo; `contrato-api.md`, `README.md`, `CLAUDE.md`, `.env.example`.

### J. Sin WordPress (2026-10-26)
- [x] Fuera el enlace "Visitar sitio actual" (`footer.sitioActual*`: record, validador, seed, admin, tipos, `Pie.tsx`).
- [x] `urlReserva` vacía en el seed; `V004__sin_wordpress.sql` limpia bases ya sembradas (footer y URLs a
      `gruposerenity.com`). Con alojamientos publicados, el botón del destino lleva a `#reservas` con el destino elegido.
- [x] `grep -ri "wp-content\|wordpress\|gruposerenity.com/shop\|sitioActual"` sobre Backend/, Frontend/client y scripts/:
      sin resultados en código (solo el comentario histórico de `Notificador.cs`). El smoke lo verifica en `/api/public/home`.
- [x] Producción: `docker-compose.prod.yml` (Caddy HTTPS, sin puertos internos, backup diario), `.env.prod.example`,
      `scripts/deploy.sh` (smoke de solo lectura con `SMOKE_SOLO_PUBLICO=1`) y `DEPLOY.md`.

### Verificación (2026-10-26)
- `dotnet test`: 123 OK. `pnpm check` limpio, `pnpm test` 13 OK, `vite build` OK.
- `SMOKE_BASE=http://localhost:8090 node scripts/smoke.mjs`: **215 OK, 0 fallidas** (incluye el webhook firmado).
- Navegador (1440 y 390 px): alojamiento con temporada y bloqueo creados en el admin → botón del destino → reserva con
  el destino preseleccionado → calendario con días bloqueados → cotización (4 noches de temporada + 1 base + huésped
  extra + aseo = $3.180.000) → checkout de Wompi con monto y firma → `/reserva/CODIGO?t=TOKEN` → evento firmado
  PENDING → APPROVED: la página pasa a "¡Reserva confirmada!" sin recargar. Admin: confirmar, cancelar con aviso, notas,
  bloqueos, copiar enlace iCal y enlace del huésped. Correos en Mailpit. Sin errores de consola en el sitio.
- Ensayo de producción local (`deploy.sh` con override de puertos): HTTPS con HSTS, apex → www, `/shop/` → `/#reservas`,
  solo Caddy expuesto, backup restaurado en un Postgres limpio, IP real del visitante llega a la API.

## Sin confirmar / pendiente del dueño
- Llaves de Wompi (sandbox y producción) y registrar la URL de eventos en el dashboard de Wompi.
- Servidor SMTP real y correo del equipo que recibe los avisos.
- Política de cancelación/reembolso (texto editable en el panel; el reembolso se hace desde el dashboard de Wompi).
- Despliegue: dónde se aloja el stack para reemplazar el hosting de WordPress (fuera de esta feature).
