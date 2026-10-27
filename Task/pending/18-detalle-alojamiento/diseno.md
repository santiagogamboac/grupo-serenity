# 18 — Diseño técnico

Requisitos y decisiones: `analisis-y-plan.md`. Estado de ejecución: `tareas.md`.

## D1. Galería del alojamiento (backend)

- Migración `V005__alojamiento_imagenes.sql`. Copia el patrón de `proyecto_imagenes`:
  ```sql
  CREATE TABLE alojamiento_imagenes (
      id             BIGSERIAL PRIMARY KEY,
      alojamiento_id BIGINT       NOT NULL REFERENCES alojamientos (id) ON DELETE CASCADE,
      media_id       BIGINT       NOT NULL REFERENCES media (id) ON DELETE RESTRICT,
      texto_alt      VARCHAR(200) NOT NULL DEFAULT '',
      orden          INTEGER      NOT NULL DEFAULT 0
  );
  ```
  Además, índices por `(alojamiento_id, orden, id)` y por `media_id`.
  `alojamientos.imagen_media_id` se conserva como **portada** (compatibilidad). La galería son fotos adicionales.
- Repositorio (`IAlojamientoRepository`): `ImagenesAsync(ids)`, `AgregarImagenAsync`, `ActualizarAltImagenAsync`,
  `EliminarImagenAsync`, `ReordenarImagenesAsync`. Todas filtran por `alojamiento_id`.
- Comandos MediatR en `Features/Reservas/AlojamientoImagenes.cs` (uno por caso de uso, con su validador):
  agregar (valida que el media exista y sea imagen; alt ≤ 200), alt, eliminar, reordenar (mismo conjunto de ids).
- Admin API (`AdminReservasController`, junto al CRUD de alojamientos), con la misma forma que la de proyectos:
  `POST /api/admin/alojamientos/{id}/imagenes {mediaId, textoAlt}` → `AlojamientoAdminDto`;
  `PUT .../imagenes/orden {ids}`, `PUT .../imagenes/{imagenId} {textoAlt}`, `DELETE .../imagenes/{imagenId}`.
- DTOs: `AlojamientoAdminDto.Imagenes: AlojamientoImagenDto(Id, MediaId, Url, TextoAlt, Orden)[]`.
  En el público, `AlojamientoPublicoDto.Imagenes: ImagenPublicaDto(Id, Url, TextoAlt)[]`, sin `mediaId` (regla `ParaPublico`).
- Borrar un alojamiento borra su galería (CASCADE). El media queda en la biblioteca.

## D2. Cierre de la ventana de doble reserva

- `CalendarioAlojamientos.OcupacionAsync(a, desde, hasta, ct, forzar = false)`: con `forzar`, sincroniza el anuncio
  vinculado sin pedir turno, con un timeout de 10 s (`CancellationTokenSource.CreateLinkedTokenSource`). Si falla, se
  registra un warning y se sigue con el último estado (los bloqueos anteriores no se borran: lo garantiza `AnuncioSyncService`).
- `CrearReservaCommand` y `NuevoPagoReservaCommand` llaman con `forzar: true`. `NuevoPago` revisa que las fechas de la
  reserva no choquen con bloqueos `airbnb`/`manual`. Si chocan, cancela la reserva con el motivo
  "Las fechas se ocuparon en otro canal" y responde 409 sin URL de pago.
- Feed `.ics`: tras buscar por token, `CryptographicOperations.FixedTimeEquals`.

## D3. Página de detalle (frontend)

- Ruta `/alojamientos/:id` → `pages/AlojamientoPage.tsx`, cargada con `lazy` igual que el admin. Datos de
  `useHome().data` (`reservaDirecta.alojamientos`, `proyectos`, `contenido`). Si no hay alojamiento con ese id, muestra
  `NotFound`.
- Componentes en `features/public/alojamiento/`:
  - `GaleriaAlojamiento.tsx`: foto grande + miniaturas (portada + galería; si no hay ninguna, las fotos del proyecto) y
    visor a pantalla completa con teclado.
  - `FichaAlojamiento.tsx`: nombre, destino, descripción y los datos clave (capacidad, huéspedes incluidos, estancia mínima, aseo).
  - `TarjetaReserva.tsx`: calendario (`CalendarioReserva` de `ReservaEnLinea.tsx`), huéspedes, cotización
    (`useCotizacion`), formulario (nombre, correo, teléfono, nota, acepto condiciones, honeypot), `useCrearReserva` →
    `window.location.assign(checkoutUrl)` o, sin pago en línea, navegar a `/reserva/:codigo?t=`.
- Estilos: bloque `/* Detalle de alojamiento */` en `index.css` con los tokens de `.site-shell--glass` (vidrio claro).
  La tarjeta de reserva es sticky en escritorio. En móvil hay una barra inferior fija con el precio y "Reservar", que
  hace scroll hasta la tarjeta.
- Textos de mecánica en `textos.ts` → `TEXTOS.alojamiento.*` (archivo de serenity-24: se agregan claves al final y se le avisa).
- Tipos: `ImagenPublica` y `AlojamientoPublico.imagenes` en `lib/types.ts` (de serenity-24: cambio mínimo, con aviso).

## D4. Puntos de entrada

- Destinos: tarjetas de alojamientos del proyecto activo → `/alojamientos/:id` (en `Destinos.tsx`, de serenity-24, con aviso).
- Admin: sección de galería en el editor del alojamiento (`AlojamientosProyecto.tsx`, de serenity-24). Se hace como
  componente nuevo `features/admin/components/GaleriaAlojamientoAdmin.tsx` y se monta con un cambio de una línea.
