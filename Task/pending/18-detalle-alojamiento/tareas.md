# 18 — Tareas y estado (fuente de verdad para retomar)

Leyenda: `[ ]` pendiente · `[~]` en curso · `[x]` hecho y verificado. Actualizar **en cada paso** con fecha y evidencia.
Si la sesión se corta, continúa desde la primera tarea no marcada `[x]` y relee `diseno.md`.

Coordinación: serenity-24 es dueña del cableado de la reserva en la landing y el admin (ver la lista de rutas en
`analisis-y-plan.md`). Antes de tocar uno de sus archivos se le avisa. Nada de `git stash`, `checkout` ni `reset`:
el árbol de trabajo es compartido.

## T0. Especificación
- [x] 2026-10-26 `analisis-y-plan.md` (aprobado con galería), `diseno.md`, `tareas.md`.

## T1. Galería — backend (D1)
- [x] 2026-10-26 V005 `alojamiento_imagenes` + repositorio + `AlojamientoImagenes.cs` (agregar/alt/eliminar/reordenar) +
      endpoints en `AdminReservasController` + `imagenes` en `AlojamientoAdminDto` y `AlojamientoPublicoDto` (Home) +
      `media.EstaEnUsoAsync` cuenta la galería. `dotnet build` OK.
- [ ] Bloque del smoke para la galería (smoke.mjs es de serenity-24: coordinar) y prueba contra el stack.

## T2. Cierre de doble reserva (D2)
- [x] 2026-10-26 `CalendarioAlojamientos.SincronizarEnVivoAsync` (sin turno, timeout 10 s) en crear reserva y en
      `NuevoPago`; `ChocaConBloqueosAsync` → cancela con motivo y 409 antes de mandar a Wompi.
- [x] 2026-10-26 `FixedTimeEquals` del token en el feed.
- [x] 2026-10-26 `tests/CalendarioAirbnbTests.cs`: feed 200/404 (falso con DispatchProxy), CRLF, sin `airbnb`,
      export real de Airbnb (Reserved / Not available) y regla de cruce semiabierta. `dotnet test`: 133/133.

## T3. Detalle de alojamiento — frontend (D3)
- [x] 2026-10-26 Tipos (`ImagenAlojamiento`), `TEXTOS.alojamiento`, prop `alojamientoFijoId` en ReservaEnLinea,
      ruta `/alojamientos/:id`, tarjetas en Destinos y galería en el admin (con permiso de serenity-24, tras su commit 83cc79e).
- [x] 2026-10-26 Escritos (archivos nuevos): `pages/AlojamientoPage.tsx`, `features/public/alojamiento/{GaleriaAlojamiento,
      TarjetasAlojamientos}.tsx`, bloque CSS `Detalle de alojamiento` en `index.css`, admin
      `features/admin/pages/GaleriaAlojamientoAdmin.tsx`. **Falta** (esperando el commit de serenity-24, que ya dio permiso):
      prop `alojamientoFijoId` en `ReservaEnLinea.tsx`, `ImagenAlojamiento`/`imagenes` en `lib/types.ts`, bloque
      `TEXTOS.alojamiento` en `textos.ts`, ruta en `App.tsx`, montar `TarjetasAlojamientos` en `Destinos.tsx`;
      y, si lo aprueba, `imagenes` en `admin/tipos.ts` + montar la galería en `AlojamientosProyecto.tsx`.
- [ ] Prueba en navegador: 1440/820/390, sin scroll horizontal, flujo de reserva sin pago en línea (local).

## T4. Entradas y admin (D4)
- [x] 2026-10-26 Tarjetas en Destinos.
- [x] 2026-10-26 Galería en el admin del alojamiento.

## T5. Verificación y entrega
- [x] 2026-10-26 `dotnet test` 133/133, `pnpm check` 0 errores, `pnpm test` 13/13, `vite build` OK; api local
      reconstruida (V005 aplicada; `/api/public/home` devuelve `imagenes`).
- [x] 2026-10-26 Navegador: `/alojamientos/5` a 1440 y 390 px, sin scroll horizontal ni errores de consola (se corrigió un
      useEffect que devolvía valor y el salto de línea de "Volver" en móvil).
- [ ] `node scripts/smoke.mjs` contra el stack + bloque de galería en el smoke; reservar de punta a punta con Wompi sandbox.
- [ ] Commit propio (después del de serenity-24) y `git push origin main`.
- [ ] Mover la carpeta a `Task/done/` cuando T1–T5 estén en `[x]`. Producción (Wompi, dominio, Airbnb) queda en
      `analisis-y-plan.md` §5 E–F: requiere al dueño.

## Bitácora
- 2026-10-26: commit + push a origin/main. Para el despliegue: `A_EJECUTAR_PARA_SEBASTIAN.md` (prompt con manejo de
  conflictos; su PC tiene los cambios de despliegue de la VPS).
- 2026-10-26: T1 y T2 hechos (backend con 133/133 tests). Se escribe T3 en archivos nuevos; las ediciones en
  archivos de serenity-24 esperan su commit (acordado por mensaje).
- 2026-10-26: plan aprobado con galería. Se acuerda el reparto con serenity-24: ella hace commit primero, yo después y
  luego el push. CalendarioReserva ya se exporta desde ReservaEnLinea.tsx. V005 es mía.
