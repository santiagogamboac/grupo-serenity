# 16 — Integración con Airbnb: calendario y reseñas (PLAN, SIN CONFIRMAR)

**Depende de:** 06, 07, 09 · **Estado:** pending — plan de investigación; requiere datos del cliente (ver §6).

> **Actualización 2026-09-29:** el detalle y las alternativas quedaron en `reviews/README.md` (reseñas) y
> `calendario/README.md` (calendario, multi-anuncio y cómo asociar cada iCal a su anuncio). Donde difieran, mandan esos.

## 1. Objetivo
1. **Calendario:** que las fechas ocupadas en Airbnb se reflejen en el sitio (y a la inversa) para evitar doble reserva.
2. **Reseñas:** mostrar en la landing/página de cada proyecto las reseñas reales de Airbnb para dar confianza.

La reserva **sigue ocurriendo donde ya ocurre** (botón `url_reserva` del proyecto → Airbnb, o solicitud al equipo).
No se reemplaza el flujo de reserva; el sitio solo muestra disponibilidad y confianza.

## 2. Realidad técnica de Airbnb (lo importante)
| Necesidad | ¿Se puede? | Cómo |
|---|---|---|
| API pública para anfitriones individuales | **No.** La API de Airbnb es privada: solo para socios de conectividad aprobados (PMS / channel managers). No hay llaves de desarrollador para una cuenta normal. | — |
| Calendario (fechas ocupadas) | **Sí** | **iCal**: cada anuncio tiene un enlace `.ics` exportable (Airbnb → Disponibilidad → Sincronizar calendarios → *Exportar*). Es lectura de solo fechas bloqueadas/reservadas (sin datos del huésped, sin precios). Airbnb lo refresca cada varias horas, no es tiempo real. |
| Enviar al calendario de Airbnb lo que se reserva fuera (sitio propio) | **Sí, parcial** | Serenity publica **su propio `.ics`** por proyecto; se pega en Airbnb (*Importar calendario*). Airbnb lo consulta periódicamente (retraso de horas). |
| Reseñas por API oficial | **No** con cuenta normal. Solo vía socio aprobado. | — |
| Reseñas raspando la página pública (scraping) | Técnicamente posible, **no recomendado**: viola los Términos de Airbnb, se rompe con cada cambio, puede bloquear IP/cuenta, y Airbnb pagina/oculta parte con JS. | Descartado como solución principal. |
| Reseñas vía channel manager / PMS (Hostaway, Guesty, Hospitable, Lodgify, Smoobu…) | **Sí**, es el camino "legal" automático. Ellos son socios de Airbnb y exponen reseñas por su API. | Costo mensual del PMS. |
| Reseñas copiadas manualmente al panel (ya existe `resenas` CRUD) | **Sí, hoy** | Se pega autor/texto/calificación desde Airbnb. Cero riesgo. Con enlace "ver en Airbnb". |
| Embeber la publicación de Airbnb en el sitio | Airbnb no ofrece widget/iframe oficial. Además rompería la lógica de reserva propia — **descartado**. | — |

**Conclusión:** calendario = iCal (gratis, viable). Reseñas = elegir entre (A) manual asistido, (B) PMS con API, (C) scraping (no).

## 3. Estado actual del código (base sobre la que se construye)
- `resenas(autor, contexto, texto, calificacion 1..5, orden, publicada)`: CRUD + público solo `publicada`. **Sin `proyecto_id` ni origen.**
- `proyectos(..., url_reserva, ...)`: aquí va el enlace al anuncio de Airbnb; **no hay** ID de anuncio ni URL iCal.
- `solicitudes`: contacto + fecha + huéspedes, sin validar contra disponibilidad.
- Sin correo/SMTP (Task 15), sin jobs en segundo plano.

## 4. Diseño propuesto

### Fase 1 — Calendario iCal (independiente de las reseñas)
- Migración `V002__airbnb.sql`:
  - `proyectos`: `airbnb_room_id VARCHAR(30) NULL`, `airbnb_ical_url VARCHAR(600) NULL` (secreto: no sale en la API pública).
  - `disponibilidad_bloqueos(id, proyecto_id, desde DATE, hasta DATE, origen ('airbnb'|'manual'|'solicitud'), uid_externo, actualizado_en)` con `UNIQUE(proyecto_id, uid_externo)`.
- `IAirbnbCalendarioClient` (HttpClient + parser iCal, p. ej. paquete `Ical.Net`) en `Infrastructure`.
- `BackgroundService` **AirbnbSyncWorker**: cada N min (config `Airbnb:SyncMinutos`, def. 30) descarga cada `.ics`, reemplaza bloqueos `origen='airbnb'` del proyecto en transacción; si falla, conserva los anteriores y registra log (nunca vacía por error de red).
- API:
  - Público `GET /api/public/proyectos/{slug}/disponibilidad?desde&hasta` → rangos ocupados (sin uid ni URL).
  - Público `GET /api/public/proyectos/{slug}/calendario.ics?token=…` → feed de Serenity para importar en Airbnb (solo bloqueos `manual`/`solicitud`, nunca reexportar los de Airbnb para no crear bucles).
  - Admin: campos en el formulario de proyecto, botón "Sincronizar ahora", estado de última sincronización/error, bloqueos manuales (crear/borrar).
- `POST /api/public/solicitudes`: si el rango cae en fechas ocupadas → 409 con mensaje claro.
- Frontend: selector de fecha del formulario deshabilita días ocupados (fechas desde la API, sin texto editorial en componentes).
- Limitación a comunicar: **la sincronización tiene retraso de horas en ambos sentidos**; una doble reserva sigue siendo posible en esa ventana. Solución total exige PMS/channel manager.

### Fase 2 — Reseñas
Migración adicional sobre `resenas`: `proyecto_id BIGINT NULL` (FK), `origen VARCHAR(20) NOT NULL DEFAULT 'manual' CHECK (origen IN ('manual','airbnb'))`, `fecha_resena DATE NULL`, `url_origen VARCHAR(500) NULL`, `id_externo VARCHAR(80) NULL` + `UNIQUE(origen, id_externo)`.

Opciones (elegir en §6; el modelo de datos sirve a todas):
- **A — Manual asistido (recomendada para arrancar, 0 costo/0 riesgo):** el admin pega reseñas desde Airbnb; el formulario ya existe, se agrega selector de proyecto, fecha y enlace. Sitio muestra sello "Reseña verificada en Airbnb" + promedio y n.º de reseñas (campos de la sección `resenas`, editables).
- **B — Automática vía PMS/channel manager:** `IResenasProveedor` con una implementación por proveedor elegido; worker que hace *upsert* por `id_externo`; el admin puede ocultar (`publicada=false`) sin borrar. Requiere cuenta paga del PMS conectada a Airbnb y su API key.
- **C — Scraping:** no se implementa.
- Público: `home` y página de proyecto muestran reseñas `publicada` filtradas por `proyecto_id` (o generales si es NULL); orden por `orden` luego fecha.

## 5. Riesgos / cosas a decidir con criterio
- **Términos de Airbnb:** las reseñas son contenido del huésped; mostrarlas con atribución y enlace es práctica común, pero conviene copiar tal cual (sin editar) y no inventar ninguna. Nunca fabricar reseñas.
- **Privacidad:** mostrar solo nombre de pila del huésped; nada de datos de contacto.
- **Secreto:** `airbnb_ical_url` da acceso al calendario; no exponerla en API pública ni logs.
- **Doble reserva:** ver limitación de Fase 1.
- **Costo:** Fase 1 gratis; Fase 2-B mensualidad del PMS.

## 6. Qué necesito del cliente (bloqueante)
- [ ] Lista de **anuncios de Airbnb** (URL de cada uno) y a qué **proyecto** de Serenity corresponde cada uno.
- [ ] Acceso (o que el anfitrión copie) el **enlace iCal de exportación** de cada anuncio (Airbnb → Calendario → Disponibilidad → Sincronizar calendarios → *Exportar calendario*).
- [ ] ¿La reserva final se hace en Airbnb (botón) o por solicitud al equipo? (afecta si se envía nuestro `.ics` de vuelta).
- [ ] Decisión reseñas: **A manual** ahora / **B PMS** (¿ya usan alguno? ¿cuál?).
- [ ] Si A: quién y con qué frecuencia las actualiza; ¿mostrar puntuación global de Airbnb (ej. 4,9 · 87 reseñas)?
- [ ] Confirmar que quieren el sello/enlace visible hacia Airbnb (efecto: ayuda confianza, pero saca tráfico del sitio).

## 7. Aceptación
- [ ] Bloqueo creado en Airbnb aparece en `disponibilidad` en ≤ 1 ciclo de sync y deshabilita el día en el formulario.
- [ ] URL iCal caída/ inválida → se conservan los bloqueos previos y el admin ve el error.
- [ ] Solicitud sobre fechas ocupadas → 409.
- [ ] `.ics` de Serenity válido y aceptado por Airbnb (prueba con un anuncio real).
- [ ] Reseñas visibles por proyecto, ocultables desde admin, con enlace de origen; API pública sin `airbnb_ical_url` ni `id_externo`.
- [ ] Tests: parser iCal (rangos, todo el día, fin exclusivo), reemplazo transaccional, filtro de reseñas, validación de solapamiento; smoke E2E actualizado.

## 8. Orden de trabajo
1. Fase 1 completa (independiente y de valor inmediato). 2. Reseñas opción A (migración + selector de proyecto + UI). 3. Opción B solo si el cliente contrata PMS.
