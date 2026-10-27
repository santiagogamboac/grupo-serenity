#!/bin/sh
# Backup diario de Serenity (corre dentro del contenedor `backup` de docker-compose.prod.yml).
#   /backups/serenity-db-AAAA-MM-DD.sql.gz      pg_dump de la base
#   /backups/serenity-media-AAAA-MM-DD.tar.gz   volumen de MinIO (imágenes)
# Conserva BACKUP_RETENCION_DIAS días. Restaurar: ver DEPLOY.md.
set -eu

HORA="${BACKUP_HORA:-03:00}"
RETENCION="${BACKUP_RETENCION_DIAS:-14}"

respaldar() {
  dia=$(date +%F)
  echo "[backup] $(date '+%F %T') inicio"
  pg_dump --no-owner --clean --if-exists | gzip -9 > "/backups/.db-$dia.tmp"
  mv "/backups/.db-$dia.tmp" "/backups/serenity-db-$dia.sql.gz"
  tar -czf "/backups/.media-$dia.tmp" -C /minio .
  mv "/backups/.media-$dia.tmp" "/backups/serenity-media-$dia.tar.gz"
  find /backups -name 'serenity-*' -type f -mtime "+$RETENCION" -delete
  echo "[backup] $(date '+%F %T') listo: $(ls -1 /backups | grep -c serenity-) archivos en /backups"
}

# Al arrancar: si hoy no hay backup, hacer uno (también sirve para comprobar que funciona).
[ -f "/backups/serenity-db-$(date +%F).sql.gz" ] || respaldar || echo "[backup] ERROR en el backup inicial"

while true; do
  ahora=$(date +%s)
  objetivo=$(date -d "$(date +%F) $HORA" +%s)
  [ "$objetivo" -gt "$ahora" ] || objetivo=$((objetivo + 86400))
  sleep $((objetivo - ahora))
  respaldar || echo "[backup] ERROR: el backup falló; se reintenta mañana"
done
