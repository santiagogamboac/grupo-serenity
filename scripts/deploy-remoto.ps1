<#
.SYNOPSIS
    Despliega Grupo Serenity en la VPS por SSH, desde Windows: producción o pruebas.

.DESCRIPTION
    Corre en tu PC; el trabajo pesado (build y arranque de contenedores) ocurre en el servidor.
      1. Revisa el repo local: avisa de cambios sin commitear o commits sin push (no se desplegarían).
      2. Prueba la conexión SSH.
      3. En el servidor: git fetch + checkout de la rama + git pull --ff-only en el clon del ambiente, y verifica que
         existan el .env y los archivos compose (y, si va Caddy, que 80/443 no sean de otro contenedor).
      4. Pide confirmación (en producción hay que escribir "produccion").
      5. Corre scripts/deploy.sh en el servidor: respaldo previo opcional, build, up -d, reinicio de web, espera de
         /health y smoke de solo lectura.
    La configuración de cada ambiente está en scripts/deploy.ambientes.psd1 (y .local.psd1, si existe).

.EXAMPLE
    .\scripts\deploy-remoto.ps1 -Ambiente pruebas
.EXAMPLE
    .\scripts\deploy-remoto.ps1 -Ambiente pruebas -Rama feature/x
.EXAMPLE
    .\scripts\deploy-remoto.ps1 -Ambiente produccion -Usuario ubuntu -Llave ~/.ssh/serenity
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('produccion', 'pruebas')]
    [string]$Ambiente,
    [string]$Servidor,
    [string]$Usuario,
    [int]$Puerto,
    [string]$Llave,
    [string]$Rama,
    # No pide confirmación (para automatizar). Úsalo con cuidado en producción.
    [switch]$SinConfirmar,
    # Despliega lo que ya está en el clon del servidor, sin git pull.
    [switch]$SinPull
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$inicio = Get-Date

function Paso([string]$texto) { Write-Host "`n> $texto" -ForegroundColor Cyan }
function Aviso([string]$texto) { Write-Host "  ! $texto" -ForegroundColor Yellow }
function Falla([string]$texto) {
    Write-Host "`nx $texto" -ForegroundColor Red
    exit 1
}
# Comillas simples para bash: 'a'\''b'. Un "~/" inicial se deja fuera para que bash lo expanda.
function Q([string]$s) {
    if ($s.StartsWith('~/')) { return '~/' + (Q $s.Substring(2)) }
    return "'" + $s.Replace("'", "'\''") + "'"
}

# ─── Configuración ────────────────────────────────────────────────────────────
# Como Import-PowerShellDataFile (solo datos), sin depender del módulo: lanzado desde pwsh, Windows PowerShell 5.1
# hereda un PSModulePath que no lo encuentra.
function Leer-Psd1([string]$ruta) {
    $bloque = [scriptblock]::Create([IO.File]::ReadAllText($ruta))
    $bloque.CheckRestrictedLanguage([string[]]@(), [string[]]@('true', 'false'), $false)
    return & $bloque
}
$raiz = Split-Path -Parent $PSScriptRoot
$cfg = (Leer-Psd1 (Join-Path $PSScriptRoot 'deploy.ambientes.psd1'))[$Ambiente]
$local = Join-Path $PSScriptRoot 'deploy.ambientes.local.psd1'
if (Test-Path $local) {
    $extra = (Leer-Psd1 $local)[$Ambiente]
    if ($extra) { foreach ($k in $extra.Keys) { $cfg[$k] = $extra[$k] } }
}
foreach ($k in 'Servidor', 'Usuario', 'Puerto', 'Llave', 'Rama') {
    if ($PSBoundParameters.ContainsKey($k)) { $cfg[$k] = $PSBoundParameters[$k] }
}
foreach ($k in 'Servidor', 'Usuario', 'Directorio', 'Rama', 'ComposeFiles') {
    if ([string]::IsNullOrWhiteSpace($cfg[$k]) -or $cfg[$k] -match 'CAMBIAR') {
        Falla "Falta '$k' para $Ambiente. Defínelo en scripts/deploy.ambientes.local.psd1 o con -$k."
    }
}
$esProd = $Ambiente -eq 'produccion'
$destino = "$($cfg.Usuario)@$($cfg.Servidor)"
$sshArgs = @('-p', "$($cfg.Puerto)", '-o', 'ServerAliveInterval=30')
if ($cfg.Llave) { $sshArgs += @('-i', $cfg.Llave) }
if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) {
    Falla "No está 'ssh'. Instala el cliente OpenSSH de Windows (Configuración > Aplicaciones > Características opcionales)."
}

# ─── 1. Repo local ────────────────────────────────────────────────────────────
Paso "Repo local ($($cfg.Rama))"
Push-Location $raiz
try {
    git fetch --quiet origin $cfg.Rama
    if ($LASTEXITCODE -ne 0) { Falla "git fetch origin $($cfg.Rama) falló (¿existe la rama en origin?)." }
    $remota = git log -1 --format='%h %s' "origin/$($cfg.Rama)"
    if (git status --porcelain) { Aviso 'Hay cambios sin commitear en tu PC: no se despliegan (el servidor toma origin).' }
    git rev-parse --verify --quiet "refs/heads/$($cfg.Rama)" | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $sinPush = [int](git rev-list --count "origin/$($cfg.Rama)..$($cfg.Rama)")
        if ($sinPush -gt 0) { Aviso "$sinPush commit(s) de $($cfg.Rama) sin push: no se despliegan. Haz git push primero si los quieres." }
    }
}
finally { Pop-Location }
Write-Host "  Se desplegará origin/$($cfg.Rama): $remota"

# ─── 2. SSH ───────────────────────────────────────────────────────────────────
Paso "Conexión SSH a $destino"
& ssh @sshArgs -o BatchMode=yes -o ConnectTimeout=15 $destino 'echo ok' | Out-Null
if ($LASTEXITCODE -ne 0) {
    Falla ("No hay conexión SSH sin contraseña con $destino (puerto $($cfg.Puerto)). Prueba 'ssh $destino' a mano y " +
        "agrega tu llave pública a ~/.ssh/authorized_keys del servidor, o pásala con -Llave.")
}
Write-Host '  ok'

# ─── 3. Preparar el clon del servidor ─────────────────────────────────────────
Paso "Preparando $($cfg.Directorio) en el servidor"
$dir = Q $cfg.Directorio
$rama = Q $cfg.Rama
$pasos = @("cd $dir 2>/dev/null || { echo 'No existe $($cfg.Directorio) en el servidor: clona ahi el repo (DEPLOY.md).'; exit 3; }")
if (-not $SinPull) {
    $pasos += "git fetch origin $rama"
    $pasos += "if [ `$(git rev-parse --abbrev-ref HEAD) != $rama ]; then git checkout $rama; fi"
    $pasos += 'git pull --ff-only'
}
$pasos += 'echo Version: $(git log -1 --format=''%h %s'')'
$pasos += "test -f $(Q $cfg.EnvFile) || { echo 'Falta $($cfg.EnvFile) en el clon: copia .env.prod.example o .env.pruebas.example y llena los secretos.'; exit 4; }"
foreach ($f in ($cfg.ComposeFiles -split '\s+')) {
    $pasos += "test -f $(Q $f) || { echo 'Falta $f en el clon (¿rama sin ese archivo?).'; exit 5; }"
}
$pasos += 'docker compose version --short'
if ($cfg.ComposeFiles -match 'docker-compose\.prod\.yml') {
    # Caddy publica 80/443: si otro contenedor (p. ej. urbanos-nginx) ya los tiene, el up fallaría a medias.
    # Sin comillas dobles: Windows PowerShell 5.1 las pasa mal a ssh.
    $pasos += "if docker ps --filter publish=80 --format '{{.Names}}' | grep -qv '^serenity_caddy`$'; then " +
    "echo 'El puerto 80 ya es de otro contenedor y docker-compose.prod.yml levanta Caddy en 80/443. " +
    "Pon en ComposeFiles los archivos sin Caddy de este servidor:'; docker ps --filter publish=80 --format '{{.Names}}'; exit 6; fi"
}
& ssh @sshArgs $destino ('set -e; ' + ($pasos -join '; '))
if ($LASTEXITCODE -ne 0) { Falla "La preparación del servidor falló (código $LASTEXITCODE). Nada se reinició." }

# ─── 4. Confirmación ──────────────────────────────────────────────────────────
$vars = @("SIN_PULL=1", "ENV_FILE=$(Q $cfg.EnvFile)", "COMPOSE_FILES=$(Q $cfg.ComposeFiles)")
if ($cfg.Proyecto) { $vars += "PROYECTO=$(Q $cfg.Proyecto)" }
if ($cfg.Respaldo) { $vars += 'RESPALDO_PREVIO=1' }
$comando = "cd $dir && $($vars -join ' ') bash scripts/deploy.sh"

Write-Host ''
Write-Host "  Ambiente : $Ambiente" -ForegroundColor $(if ($esProd) { 'Red' } else { 'Green' })
Write-Host "  Servidor : ${destino}:$($cfg.Directorio)"
Write-Host "  Compose  : $($cfg.ComposeFiles)$(if ($cfg.Proyecto) { "  (proyecto $($cfg.Proyecto))" })"
Write-Host "  Respaldo : $(if ($cfg.Respaldo) { 'sí, antes de actualizar' } else { 'no' })"
Write-Host "  Comando  : $comando"
if (-not $SinConfirmar) {
    if ($esProd) {
        if ((Read-Host "`nEsto reinicia PRODUCCIÓN. Escribe 'produccion' para continuar") -ne 'produccion') { Falla 'Cancelado.' }
    }
    elseif ((Read-Host "`n¿Desplegar en pruebas? (s/n)") -notmatch '^[sS]') { Falla 'Cancelado.' }
}

# ─── 5. Despliegue ────────────────────────────────────────────────────────────
Paso 'Desplegando (scripts/deploy.sh en el servidor)'
& ssh @sshArgs -t $destino $comando
$codigo = $LASTEXITCODE
$duracion = [int]((Get-Date) - $inicio).TotalSeconds
if ($codigo -ne 0) {
    $logs = "docker compose$(if ($cfg.Proyecto) { " -p $($cfg.Proyecto)" }) " +
    (($cfg.ComposeFiles -split '\s+' | ForEach-Object { "-f $_" }) -join ' ') + ' logs --tail=100 api web'
    Falla "El despliegue en $Ambiente falló (código $codigo, $duracion s). Revisa en el servidor: cd $($cfg.Directorio) && $logs"
}
Write-Host "`nOK: $Ambiente desplegado en $duracion s -> $($cfg.Url)" -ForegroundColor Green
