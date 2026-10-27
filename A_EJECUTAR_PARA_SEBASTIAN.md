# A ejecutar para Sebastián

Despliegue de la versión nueva de Grupo Serenity desde **tu PC**. Allí están los cambios del despliegue
(configuración de la VPS), así que al sincronizar con GitHub **puede haber conflictos**. Copia el prompt de abajo
completo en Claude Code, abierto en la carpeta del proyecto en tu PC.

## Qué trae esta versión (rama `main` de `origin`)

- `83cc79e`: sitio sin WordPress, reserva en línea conectada, admin de reservas y archivos de producción
  (`docker-compose.prod.yml`, `caddy/`, `scripts/deploy.sh`, `scripts/backup.sh`, `.env.prod.example`, `DEPLOY.md`).
- El commit siguiente: rediseño con vidrio claro, página de detalle `/alojamientos/:id` con galería, galería en el admin,
  sincronización en vivo con Airbnb antes de cobrar y migración **`V005__alojamiento_imagenes.sql`**.
- Migraciones nuevas que corren solas al arrancar la api: `V004__sin_wordpress.sql` y `V005__alojamiento_imagenes.sql`.

## Prompt para Claude Code (copiar todo)

```text
Necesito desplegar a producción la última versión de origin/main del proyecto Serenity. En esta PC hay cambios del
despliegue (configuración de la VPS) que pueden chocar con lo nuevo. Trabaja con cuidado y por pasos, y detente a
preguntarme en los puntos marcados con ⛔.

1. Diagnóstico (solo lectura)
   - `git status`, `git branch -vv`, `git log --oneline -5` y `git fetch origin`. Luego
     `git log --oneline HEAD..origin/main` y `git log --oneline origin/main..HEAD`.
   - Dime qué cambios locales hay (commiteados o no) y cuáles archivos tocan también los commits nuevos de origin/main
     (`git diff --name-only HEAD...origin/main`).

2. Proteger mis cambios locales (no pierdas nada)
   - Si hay cambios sin commitear, crea la rama `respaldo-deploy-<fecha>` desde el estado actual y commitéalos ahí.
     Nunca hagas `git reset --hard`, `git clean`, `git checkout -- .` ni `git stash drop`.
   - NUNCA commitees `.env`, contraseñas, llaves de Wompi ni nada con secretos. Si `.env` aparece como no ignorado,
     avísame.

3. Integrar origin/main
   - Vuelve a `main` y haz `git merge origin/main` (merge, no rebase, para conservar el historial).
   - Si hay conflictos, resuélvelos así:
     • Código de la app (Backend/src, Frontend/client, Task/, migraciones V###): gana origin/main. Nunca edites una
       migración ya aplicada.
     • Archivos de despliegue (docker-compose*.yml, deploy/, caddy/, scripts/deploy.sh, scripts/backup.sh,
       .env.prod.example, DEPLOY.md, nginx.conf): combina. Conserva lo específico de nuestra VPS (puertos, redes, nombres
       de contenedor, dominio) y agrega lo nuevo de origin/main.
     • ⛔ Muéstrame cada conflicto de despliegue y tu propuesta antes de guardarla.
   - IMPORTANTE sobre la VPS: hoy (51.161.114.45, https://serenity.51.161.114.45.sslip.io) la comparte con
     urbanos-rurales. El nginx público de ese proyecto es dueño de 80/443, y Serenity entra por la red Docker `edge`
     (ver deploy/docker-compose.yml si existe en esta PC). El `docker-compose.prod.yml` nuevo trae Caddy en 80/443:
     NO lo levantes si urbanos-nginx sigue ahí. Usa la variante sin `caddy` que explica DEPLOY.md
     ("Si el servidor ya tiene otro proxy en 80/443…"). ⛔ Confírmame cuál de las dos configuraciones vas a usar.

4. Verificación local antes de desplegar
   - `cd Backend && dotnet build Serenity.slnx && dotnet test Serenity.slnx`
   - `cd Frontend && pnpm install && pnpm check && pnpm test && pnpm exec vite build`
   - Si algo falla, detente y muéstrame el error. No despliegues código roto.

5. Subir el resultado
   - ⛔ Muéstrame el `git log --oneline -5` final y pídeme confirmación. Luego `git push origin main` (sin --force).
     Si el push se rechaza porque origin avanzó, repite el paso 3; nunca uses force.

6. Desplegar en la VPS (⛔ confirmo antes de cada comando que reinicie servicios)
   - Respaldo previo de la BD de producción: `bash scripts/backup.sh`, o un `pg_dump` del contenedor de Postgres a un
     archivo con fecha. Verifica que el archivo exista y no esté vacío.
   - `git pull` en el servidor y luego el despliegue con la configuración elegida en el paso 3 (`bash scripts/deploy.sh`
     o `docker compose -f … up -d --build`, según corresponda).
   - Revisa `.env` en el servidor: variables nuevas de `.env.prod.example` (WOMPI_*, SMTP_*, CORREO_EQUIPO,
     PUBLIC_ORIGIN/UrlSitio). Sin llaves de Wompi el pago en línea queda apagado; no es un error. ⛔ No inventes valores:
     pregúntame los que falten.

7. Verificación en producción
   - Logs de la api: deben aparecer V004 y V005 aplicadas, sin errores.
   - `curl -s https://<dominio>/api/public/home`: responde 200, `reservaDirecta.alojamientos[*].imagenes` existe y ya
     no aparece nada de WordPress.
   - En el navegador: el inicio (vidrio claro), `/alojamientos/<id>` de un alojamiento publicado, `/admin` (login,
     alojamientos con su "Galería") y `/api/public/ical/<token>.ics` (el enlace del feed se ve en el admin del
     alojamiento).
   - Resúmeme lo hecho, la URL y cualquier pendiente.
```

## Pendientes que no dependen del código
- Llaves de Wompi (primero sandbox y después producción) y registrar en el dashboard de Wompi la URL de eventos
  `https://<dominio>/api/public/pagos/wompi/eventos`.
- Dominio definitivo. Hay que fijarlo **antes** de pegar el feed `.ics` en Airbnb: si después cambia, hay que reconectar
  cada calendario.
- Cargar en el panel los alojamientos reales con sus fotos y vincular cada uno con su anuncio de Airbnb.
- Plan y estado completos: `Task/pending/18-detalle-alojamiento/` (`tareas.md` es el estado vivo).
