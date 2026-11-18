# Ambientes de despliegue para scripts/deploy-remoto.ps1 (deploy-produccion.bat / deploy-pruebas.bat).
# Sin secretos: los secretos viven en el .env de cada clon en el servidor.
# Para valores propios de tu PC (usuario SSH, llave) crea scripts/deploy.ambientes.local.psd1 con la misma forma y
# solo las claves que cambian; está en .gitignore y se mezcla encima de este archivo.
#
#   Servidor      IP o nombre del servidor (o un Host de ~/.ssh/config)
#   Usuario       usuario SSH
#   Puerto        puerto SSH
#   Llave         ruta a la llave privada (vacío = la de ssh-agent / ~/.ssh)
#   Directorio    clon del repo en el servidor (uno por ambiente: comparten VPS pero no carpeta)
#   Rama          rama de origin a desplegar
#   ComposeFiles  archivos compose, en orden, separados por espacio (relativos al clon)
#   Proyecto      nombre del proyecto compose (vacío = el de docker-compose.yml: serenity)
#   EnvFile       archivo de variables dentro del clon
#   Url           URL pública, para el resumen final
#   Respaldo      pg_dump de la base antes de actualizar (backups/predeploy-*.sql.gz en el clon)
@{
    produccion = @{
        Servidor     = '51.161.114.45'
        Usuario      = 'CAMBIAR'
        Puerto       = 22
        Llave        = ''
        Directorio   = '~/serenity'
        Rama         = 'main'
        # En la VPS compartida con urbanos-rurales (urbanos-nginx es dueño de 80/443) NO uses docker-compose.prod.yml
        # tal cual: levanta Caddy en 80/443. Pon aquí los archivos que usa hoy ese servidor (ver DEPLOY.md).
        ComposeFiles = 'docker-compose.yml docker-compose.prod.yml'
        Proyecto     = ''
        EnvFile      = '.env'
        Url          = 'https://www.gruposerenity.com'
        Respaldo     = $true
    }
    pruebas = @{
        Servidor     = '51.161.114.45'
        Usuario      = 'CAMBIAR'
        Puerto       = 22
        Llave        = ''
        Directorio   = '~/serenity-pruebas'
        Rama         = 'main'
        ComposeFiles = 'docker-compose.yml docker-compose.pruebas.yml'
        Proyecto     = 'serenity-pruebas'
        EnvFile      = '.env'
        Url          = 'https://pruebas.gruposerenity.com'
        Respaldo     = $false
    }
}
