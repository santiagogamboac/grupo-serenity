# A ejecutar para Sebastián — Dominio de producción con HTTPS (certbot)

Para **cuando se defina el dominio definitivo** (por ejemplo `gruposerenity.com`). Hoy el sitio corre en
`https://serenity.51.161.114.45.sslip.io`, en la VPS `51.161.114.45` que comparte con **urbanos-rurales**: el nginx público
de ese proyecto (`urbanos-nginx`) y su **certbot** son dueños de 80/443, y Serenity entra por la red Docker `edge` como
`serenity-web` (ver `deploy/docker-compose.yml`).

Hazlo **después** del despliegue de `A_EJECUTAR_PARA_SEBASTIAN.md`. Copia el prompt completo en Claude Code, en tu PC o
por SSH en la VPS.

## Prompt para Claude Code (copiar todo)

```text
Necesito publicar Grupo Serenity con su dominio definitivo y HTTPS de Let's Encrypt (certbot), en la VPS 51.161.114.45.
Dominio: <DOMINIO>   (ej. gruposerenity.com; el canónico será https://www.<DOMINIO>)
Trabaja por pasos, explícame cada cambio antes de hacerlo y detente en los ⛔. Nunca muestres ni escribas en el chat
contraseñas, llaves de Wompi ni secretos del .env.

0. Diagnóstico (solo lectura)
   - En la VPS: `docker ps` (confirma que existen urbanos-nginx, el contenedor de certbot o el certbot del sistema, y
     serenity-web), `docker network inspect edge` (serenity-web debe estar en esa red) y `ss -ltnp | grep -E ':80|:443'`
     (quién publica 80/443).
   - Encuentra la config de urbanos-nginx (volumen montado en /etc/nginx/conf.d o similar), el server block actual de
     serenity.51.161.114.45.sslip.io y cómo se emiten sus certificados: webroot (/.well-known/acme-challenge),
     plugin --nginx o contenedor certbot con volumen compartido. Muéstrame lo que encuentres. No cambies nada aún.
   - Si 80/443 NO son de urbanos-nginx (Serenity tiene la VPS sola): avísame, porque entonces lo más simple es Caddy
     (docker-compose.prod.yml, HTTPS automático) y no certbot. ⛔ Decidimos juntos.

1. DNS (lo hace el dueño del dominio; tú solo verificas) ⛔
   - Registros A: `<DOMINIO>` y `www.<DOMINIO>` → 51.161.114.45 (AAAA solo si la VPS tiene IPv6 configurada).
   - CUIDADO: si el dominio hoy apunta al hosting del WordPress viejo, NO toques los registros MX, TXT (SPF/DKIM/DMARC)
     ni los de correo: solo los A de la web. Si el correo del dominio está en ese hosting, debe seguir funcionando.
   - Baja el TTL a 300 un rato antes del cambio, si se puede.
   - Verifica con `dig +short <DOMINIO> A` y `dig +short www.<DOMINIO> A` (o nslookup) hasta que ambos den
     51.161.114.45. No sigas hasta que resuelvan.

2. Server block HTTP en urbanos-nginx (antes del certificado)
   - Crea un archivo nuevo, ej. `serenity-<DOMINIO>.conf`, con el mismo estilo que el bloque sslip existente:
     `server_name <DOMINIO> www.<DOMINIO>;`, la location del desafío ACME igual a la de los otros sitios y, para lo
     demás, proxy a `http://serenity-web:80` con `proxy_set_header Host $host; X-Real-IP $remote_addr;
     X-Forwarded-For $proxy_add_x_forwarded_for; X-Forwarded-Proto $scheme;` y `client_max_body_size 20m` (subida de
     imágenes del panel). No toques los bloques de urbanos-rurales.
   - `docker exec urbanos-nginx nginx -t`. ⛔ Solo si da "syntax is ok", haz `docker exec urbanos-nginx nginx -s reload`.

3. Certificado con certbot ⛔ (confirma el comando antes de ejecutarlo)
   - Usa el MISMO método que ya usan los otros dominios de la VPS (webroot o --nginx), con
     `-d <DOMINIO> -d www.<DOMINIO>` y el correo del dueño para los avisos.
   - Primero `--dry-run` (o `--staging`); si pasa, el real.
   - Verifica que quedó en el ciclo de renovación existente: `certbot renew --dry-run` o el equivalente del contenedor.
     Si la renovación no recarga nginx sola, agrega el deploy-hook que ya usen los otros sitios.

4. Server block HTTPS y redirecciones
   - Bloque 443 (`listen 443 ssl; http2 on;`) con `ssl_certificate` y `ssl_certificate_key` del paso 3, los mismos
     parámetros TLS que el resto de la VPS, y el mismo proxy a serenity-web.
   - Redirecciones 301: http://<DOMINIO> y http://www.<DOMINIO> → https://www.<DOMINIO>; https://<DOMINIO> →
     https://www.<DOMINIO> (conserva $request_uri).
   - Deja el bloque sslip funcionando por ahora (o redirígelo con 301 al dominio nuevo). ⛔ Pregúntame qué prefiero.
   - HSTS: empieza con `max-age=300` y súbelo a 31536000 solo después de una semana estable. Nada de `preload` sin
     confirmarlo.
   - `nginx -t` y luego ⛔ reload.

5. Serenity con el dominio nuevo
   - En el `.env` de Serenity en la VPS, cambia `SERENITY_DOMAIN=www.<DOMINIO>` (es el que arma `Reservas__UrlSitio` y
     el CORS). Si la configuración desplegada usa `PUBLIC_ORIGIN`/`DOMINIO` (docker-compose.prod.yml), ajusta esas.
     No toques ninguna otra variable.
   - Recrea solo la api, con el comando de despliegue que se use hoy (p. ej.
     `docker compose -f deploy/docker-compose.yml up -d api`), y si nginx guarda la IP vieja reinicia serenity-web.
   - Verifica en el panel (/admin → Proyectos → un alojamiento → sección Airbnb) que "Calendario del sitio → Airbnb"
     ahora empiece por https://www.<DOMINIO>/api/public/ical/.

6. Verificación
   - `curl -sI http://<DOMINIO>` → 301 a https://www.<DOMINIO>/ ; `curl -sI https://<DOMINIO>` → 301 ;
     `curl -s -o /dev/null -w '%{http_code}' https://www.<DOMINIO>/api/public/home` → 200.
   - Certificado válido para ambos nombres: `echo | openssl s_client -connect www.<DOMINIO>:443 -servername
     www.<DOMINIO> 2>/dev/null | openssl x509 -noout -subject -ext subjectAltName -dates`.
   - Si el repo lo permite: `SMOKE_SOLO_PUBLICO=1 SMOKE_BASE=https://www.<DOMINIO> node scripts/smoke.mjs` (solo
     lecturas; no escribe en producción).
   - urbanos-rurales sigue funcionando: abre su dominio y revisa que responda igual que antes.

7. Lo que depende del dominio (avísame para hacerlo en orden; ver Task/pending/19-configuracion-wompi-airbnb.md)
   - Wompi: actualizar la URL de eventos a https://www.<DOMINIO>/api/public/pagos/wompi/eventos (sandbox y producción).
   - Airbnb: si ya se importó en Airbnb el feed con la URL sslip, hay que quitar ese calendario importado y volver a
     importar el feed con la URL nueva, anuncio por anuncio.
   - Resúmeme: archivos de nginx creados o cambiados, comando de certbot usado, fecha de vencimiento del certificado y
     cómo se renueva.
```

## Notas para el dueño
- El cambio de DNS deja de mostrar el WordPress viejo en ese dominio: es lo esperado. Si el **correo** del dominio está en
  el hosting viejo, hay que conservar sus registros MX/TXT; no se migra con esto.
- Si más adelante Serenity se muda a una VPS propia, no hace falta certbot: `docker-compose.prod.yml` trae Caddy, que
  emite y renueva el certificado solo (ver `DEPLOY.md`).
