# 19 — Configuración de cuentas: Wompi y Airbnb (paso a paso, desde el navegador)

**Estado: LISTO PARA EMPEZAR** cuando se cumplan los requisitos de §0. Sin cambios de código: solo configuración, datos y
verificación. Marca cada casilla con fecha y evidencia; si la sesión se corta, retoma desde la primera sin marcar.
El código que se configura aquí ya está en `main` (ver `17-reserva-directa-wompi.md` y `../done/18-detalle-alojamiento/`).

## Reglas (para cualquier persona o agente que lo ejecute)
- Nunca escribas en el navegador ni en el chat contraseñas, códigos de verificación ni datos de tarjetas reales. Si Wompi o
  Airbnb piden login, 2FA, CAPTCHA o aceptar términos, **detente** y pídele al dueño que lo haga.
- Las llaves de Wompi y los enlaces iCal de Airbnb son **secretos**: van solo en el `.env` del servidor o en el panel, nunca
  en el repo, en capturas compartidas ni en mensajes.
- ⛔ = esperar la confirmación del dueño antes de seguir.
- Trabaja en una pestaña nueva de Chrome (Claude in Chrome), con la sesión del dueño ya iniciada.

## 0. Requisitos previos (bloquean todo lo demás)
- [ ] **Despliegue al día**: producción corre `main` ≥ `4c0e87d` (galería, sync en vivo, V004/V005). Lo hace Sebastián con
      `A_EJECUTAR_PARA_SEBASTIAN.md`. Para verificarlo, `GET https://<dominio>/api/public/home` debe traer
      `reservaDirecta.alojamientos[*].imagenes`.
- [ ] **Dominio definitivo** decidido y con HTTPS (hoy: `https://serenity.51.161.114.45.sslip.io`, que es temporal).
      ⛔ No sigas a §2 ni §3 con el dominio temporal: la URL de eventos de Wompi y el feed que se pega en Airbnb dependen de
      él, y si cambia hay que reconfigurar todo.
- [ ] `PUBLIC_ORIGIN` / `Reservas:UrlSitio` del `.env` de producción = dominio definitivo. Si no, los enlaces `.ics` y el
      regreso de Wompi apuntan mal. Se comprueba con la URL "Feed iCal" del panel: debe empezar por ese dominio.
- [ ] **Alojamientos reales cargados** en el panel (`/admin` → Proyectos → proyecto → Alojamientos): nombre, precio,
      capacidad, huéspedes incluidos, aseo, estancia mínima, temporadas, portada y galería, y "Publicado".

## 1. Wompi — sandbox (pruebas sin dinero real)
1. [ ] Abrir https://comercios.wompi.co → iniciar sesión (lo hace el dueño) → modo **Pruebas** (sandbox).
2. [ ] *Desarrolladores → Programadores*: identificar las 4 llaves de prueba: llave pública `pub_test_…`, llave privada
       `prv_test_…`, secreto de **integridad** `test_integrity_…` y secreto de **eventos** `test_events_…`.
       ⛔ No copiarlas al chat: el dueño o Sebastián las pega directamente en el `.env` del servidor
       (`WOMPI_AMBIENTE=sandbox` y las cuatro `WOMPI_*` según `.env.prod.example`) y redespliega la api.
3. [ ] *Desarrolladores → URL de eventos*: registrar `https://<dominio>/api/public/pagos/wompi/eventos` ⛔ (antes de guardar).
4. [ ] Verificar: `GET /api/public/home` → `reservaDirecta.pagoEnLinea = true`.
5. [ ] Prueba de punta a punta en el sitio: `/alojamientos/<id>` → fechas libres → datos → "Reservar y pagar" → checkout de
       Wompi → tarjeta de prueba **APROBADA** de la documentación de Wompi (https://docs.wompi.co, "Datos de prueba") →
       regreso a `/reserva/<código>`.
       - [ ] La página muestra **Reserva confirmada** y el panel (`/admin` → Reservas) también.
       - [ ] El evento llegó: la reserva tiene transacción y método de pago, y los logs de la api muestran el evento firmado.
       - [ ] Esas noches aparecen ocupadas en el calendario del alojamiento.
       - [ ] Correo al huésped y al equipo, si hay SMTP configurado (si no, no aplica).
6. [ ] Prueba con tarjeta **DECLINADA**: la reserva sigue apartada, se puede reintentar el pago y, al vencer la retención
       (30 min), queda **expirada** y las fechas se liberan.
7. [ ] Prueba de reembolso o anulación: ver cómo se hace en el dashboard (el reembolso se hace allí, no desde el sitio).

## 2. Wompi — producción ⛔ (solo con §1 completa y el visto bueno del dueño)
1. [ ] El dueño completa la verificación de la cuenta de Wompi (datos de la empresa y la cuenta bancaria), si falta.
2. [ ] Llaves de **producción** (`pub_prod_…`, `prv_prod_…`, integridad y eventos) en el `.env` con
       `WOMPI_AMBIENTE=produccion` → redesplegar.
3. [ ] Registrar la URL de eventos también en el modo **Producción** del dashboard (es independiente del sandbox).
4. [ ] Prueba real con un valor bajo (alojamiento de prueba despublicado después, o una reserva real) y su reembolso.
5. [ ] Texto de condiciones y cancelación revisado en el panel (Contenido → Reservas → `condiciones`).

## 3. Airbnb — calendario en dos vías (Claude in Chrome)
Por cada alojamiento del sitio que también se publica en Airbnb:
1. [ ] Abrir https://www.airbnb.com/hosting/listings → listar los anuncios. ⛔ El dueño confirma qué anuncio corresponde a
       qué alojamiento del sitio. Anotar la tabla aquí (nombre del anuncio → `rooms/<id>` → alojamiento del sitio),
       **sin** enlaces iCal.
2. [ ] **Exportar desde Airbnb**: *Calendario* → elegir el anuncio → *Disponibilidad* → *Sincronizar calendarios* (o
       *Conectar calendarios*) → *Exportar calendario* → copiar la URL `https://www.airbnb.com/calendar/ical/<id>.ics?s=…`.
       ⛔ Mostrársela al dueño **una sola vez** y confirmar antes de guardarla.
3. [ ] **Vincular en el panel**: `/admin` → Proyectos → proyecto → *Anuncios de Airbnb* → agregar (nombre, URL del anuncio
       `airbnb.com/rooms/<id>`, URL iCal). El panel rechaza un iCal de otro anuncio (los ids deben coincidir). Luego, en
       *Alojamientos*, editar el alojamiento y elegir ese anuncio en "Anuncio de Airbnb".
4. [ ] *Sincronizar ahora* → anotar cuántos bloqueos se importaron y compararlos con el calendario de Airbnb del mismo anuncio
       (las noches bloqueadas deben coincidir). En `/alojamientos/<id>` esas noches deben verse "No disponible".
5. [ ] **Importar en Airbnb nuestro feed**: copiar del panel la URL "Feed iCal" del alojamiento
       (`https://<dominio>/api/public/ical/<token>.ics`) → en Airbnb, mismo anuncio → *Sincronizar calendarios* →
       *Importar calendario* → pegar la URL → nombre **"Página web propia"**. ⛔ Confirmar antes de guardar.
6. [ ] Verificar en Airbnb que aparece el calendario importado. Airbnb lo refresca cada ~2–3 h: volver más tarde y
       comprobar que una reserva o un bloqueo manual del sitio aparece bloqueado allá.
7. [ ] Si Airbnb pide login, verificación, CAPTCHA o aceptar términos → detenerse y pedírselo al dueño.

## 4. Prueba cruzada final (anti doble reserva)
- [ ] Bloqueo creado en Airbnb → en ≤ 5 min (o al instante con *Sincronizar ahora*) el sitio lo muestra ocupado y no deja
      reservar esas fechas.
- [ ] Reserva confirmada en el sitio → aparece bloqueada en Airbnb tras su refresco (≤ ~3 h). Mientras tanto, con ocupación
      alta, bloquearla a mano en Airbnb.
- [ ] Simulación de carrera: con una reserva pendiente de pago, bloquear esas fechas en Airbnb y pedir un nuevo pago →
      el sitio debe responder "esas fechas se acaban de ocupar en otro canal", cancelar sin cobro y no abrir Wompi.

## Limitaciones conocidas
- Airbnb importa calendarios cada ~2–3 h: hay una ventana en la que una reserva web aún no bloquea Airbnb.
- El iCal no distingue "reservado" de "no disponible" y no trae precios ni datos del huésped.
- Sin API de Airbnb, el contenido del anuncio (fotos, textos, precios de Airbnb) se sigue editando en Airbnb.

## Bitácora
- 2026-10-26: documento creado. Bloqueado por §0 (despliegue de Sebastián, dominio definitivo, alojamientos reales).
