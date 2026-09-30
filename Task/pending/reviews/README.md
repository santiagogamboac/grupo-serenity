# Reseñas de Airbnb en el sitio — alternativas (investigado 2026-09-29)

**Objetivo:** mostrar reseñas reales de Airbnb (por proyecto/anuncio) para dar confianza. Sigue sin haber decisión;
esto compara las vías. Contexto general y datos técnicos: `../16-airbnb-calendario-resenas.md`.

## 0. Sobre "el perfil verificado"
La verificación de identidad del perfil **no** da acceso a la API. La API de Airbnb es un programa de socios
(channel managers, PMS, software). Fuentes secundarias (Elfsight, Smoobu, Hospitable, comunidad Airbnb) coinciden en que hoy
**no acepta solicitudes espontáneas**: Airbnb contacta a los socios. Elfsight menciona escribir al soporte global (sección
Prohost). Lo único fiable es **preguntarle a Airbnb** (ver paso A).

## 1. Alternativas, de mejor a peor para Serenity

| # | Vía | Automática | Costo | Riesgo | Comentario |
|---|---|---|---|---|---|
| 1 | **Manual asistido** (panel admin, ya existe el CRUD) | No | 0 | Ninguno | Base de todo: sirve de respaldo de las demás. |
| 2 | **Channel manager / PMS** con su API (Hostaway, Guesty, Hospitable, Smoobu, Lodgify…) | Sí (minutos) | Mensual por anuncio/comisión | Bajo (es socio oficial) | Única vía automática "oficial". Resuelve también el calendario en tiempo casi real. Verificar que el plan elegido **exponga reseñas en su API**. |
| 3 | **Widget de terceros** (Elfsight, Trustindex, EmbedSocial…) | Sí | Mensual | Medio: no se sabe cómo obtienen los datos; diseño ajeno, fuera de nuestro control y sin datos propios en la BD | Rápido, pero contradice la regla del proyecto (contenido en BD, sin componentes externos con texto). |
| 4 | **Scraper como servicio** (Apify: actores "Airbnb Reviews Scraper") | Sí (programable) | Centavos: p. ej. un actor cobra ~US$0,0003/reseña (≈ US$0,30 por 1.000) | Medio-alto: los ToS de Airbnb prohíben acceso automatizado (lo dice el propio actor); puede romperse | Se llama desde el backend con la URL/ID del anuncio y se hace *upsert* en `resenas`. Datos personales del huésped: dejar activado el recorte. Hacerlo solo con conocimiento del cliente. |
| 5 | **Correos de notificación** de Airbnb → borrador de reseña | Semi | 0 | Bajo | Requiere correo/buzón (Task 15, no existe) y el formato del correo puede cambiar. |
| 6 | **Exportar "tus datos"** de Airbnb (Cuenta → Privacidad) | No | 0 | Ninguno | **Sin verificar** si incluye reseñas. Solo sirve como carga inicial. |
| 7 | **Reseñas de Google** (API Places, oficial) | Sí | Cuota gratis + pago por exceso | Bajo | No son de Airbnb, pero suman confianza si hay ficha de Google. Complemento, no reemplazo. |

## 2. Recomendación
1. **Ya:** vía 1 (manual) + modelo de datos preparado para más orígenes.
2. **Paralelo, costo cero:** paso A (preguntar a Airbnb) y paso B (ver qué integraciones ofrece su cuenta).
3. **Si quieren automático:** vía 2 si necesitan también calendario en tiempo real o venta directa; vía 4 si solo quieren
   reseñas y aceptan el riesgo de ToS. No recomiendo vía 3.

### Paso A — Preguntar a Airbnb (lo hace el dueño de la cuenta)
- Soporte de Airbnb (chat/teléfono): "¿Puede mi cuenta de anfitrión acceder a la API o a un feed de reseñas para mostrarlas en mi
  sitio web? ¿Qué proveedores oficiales lo permiten?" Guardar la respuesta escrita en este README.

### Paso B — Mirar qué ofrece la cuenta
- Airbnb → Cuenta → Privacidad y uso compartido / Aplicaciones o integraciones conectadas, y en el modo anfitrión la sección
  de software/conectividad. Anotar qué proveedores aparecen: son los que tienen acceso real.

## 3. Modelo de datos (común a todas las vías)
Sobre `resenas` (migración `V00x`):
- `proyecto_id BIGINT NULL` (FK) — a qué proyecto pertenece; NULL = general.
- `anuncio_id BIGINT NULL` (FK a `proyecto_anuncios`, ver `../calendario/README.md`).
- `origen VARCHAR(20) NOT NULL DEFAULT 'manual' CHECK (origen IN ('manual','airbnb','google'))`.
- `id_externo VARCHAR(80) NULL` + `UNIQUE(origen, id_externo)` para *upsert* sin duplicar.
- `fecha_resena DATE NULL`, `url_origen VARCHAR(500) NULL`.
- Regla: lo que llega automático entra con `publicada = false` (moderación) salvo que el cliente pida lo contrario; nunca se
  edita el texto de una reseña importada; solo nombre de pila del huésped.
- Público: no exponer `id_externo`.

## 4. Pendiente de decidir con el cliente
- [ ] Respuesta de Airbnb (paso A) y proveedores visibles en su cuenta (paso B).
- [ ] ¿Presupuesto mensual para un PMS/channel manager? ¿Ya usan alguno?
- [ ] ¿Aceptan el riesgo de ToS de un scraper (vía 4) o se limitan a manual/PMS?
- [ ] ¿Mostrar puntuación global (ej. 4,9 · 87 reseñas) y enlace "ver en Airbnb"?

## Fuentes consultadas
- Elfsight, "How to get and use Airbnb API": https://elfsight.com/blog/how-to-get-and-use-airbnb-api-partnership-and-integration/
- Smoobu, "What is the Airbnb API?": https://www.smoobu.com/en/blog/airbnb-api/
- Hospitable, "Airbnb partnerships": https://hospitable.com/airbnb-partnerships
- Apify, ejemplo de actor: https://apify.com/scrapersdelight/airbnb-reviews-scraper/api
