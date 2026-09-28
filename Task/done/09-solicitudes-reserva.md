# 09 — Solicitudes de reserva (público + admin)

**Depende de:** 06 · **Estado:** done — 2026-09-27

## Alcance
- `POST /api/public/solicitudes` (anónimo, rate limit 5/min/IP, honeypot `sitioWeb`, validación: nombre, contacto correo **o** teléfono, fecha ≥ hoy, huéspedes 1–20, `proyectoId` existente o `destinoTexto`).
- Admin: listado paginado con filtros `estado`/`q`, detalle, cambio de `estado` (nueva → contactada → cerrada | descartada) + `notas`, eliminar.
- Dashboard: contadores.

## Aceptación
- [x] Solicitud válida → 201 y visible en admin como `nueva`.
- [x] Fecha pasada, contacto inválido, honeypot → 400 / 202 sin persistir.
- [x] 6.ª solicitud del minuto → 429.
- [x] Filtros y paginación correctos.

## Evidencia (2026-09-27)
Smoke: 201, fecha pasada / contacto inválido / sin destino / proyecto inexistente → 400, honeypot → 202 sin persistir, filtros, búsqueda con comodines literales, estado+notas, borrado, ráfaga → 429. Navegador: formulario real de la landing creó la solicitud; en admin se cambió a “contactada” con notas. 20 tests unitarios de validación (incluye borde de zona horaria Colombia UTC-5).
