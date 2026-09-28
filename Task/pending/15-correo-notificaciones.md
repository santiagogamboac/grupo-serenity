# 15 — Correo y notificaciones (SIN CONFIRMAR)

**Depende de:** 09 · **Estado:** pending — se deja **siempre para el final** y **no se da por confirmado**
(no hay credenciales SMTP reales ni buzón de destino definidos; depende de datos del cliente).

## Alcance propuesto
- `ICorreoService` (SMTP, config `Correo:*`) con stub que solo loguea si no hay `Host` (patrón urbanos).
- Aviso al equipo cuando entra una solicitud (`Notificaciones:CorreoSolicitudes`).
- Acuse de recibo al huésped si dejó un correo.
- Recuperación de contraseña del admin (`recuperar-password` / `restablecer-password`), anti-enumeración.

## Por confirmar con el cliente
- [ ] Servidor SMTP / buzón remitente / dominio (SPF, DKIM).
- [ ] Destinatario(s) de las solicitudes.
- [ ] Texto/plantilla de los correos.

## Aceptación (cuando se confirme)
- [ ] Correo real recibido en buzón de prueba; con SMTP vacío la API no falla (solo loguea).
