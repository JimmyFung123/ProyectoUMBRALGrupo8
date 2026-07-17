# UMBRAL — Detener el stack de DESPLIEGUE (Docker)
#
# Baja todo lo que levanta el deploy: infra (Postgres, RabbitMQ, Keycloak, Mailpit)
# + los 6 microservicios + los 2 fronts (+ el túnel cloudflared si estaba activo).
#
# Uso:
#   .\scripts\stop-deploy.ps1                 # elimina contenedores; CONSERVA los datos
#   .\scripts\stop-deploy.ps1 -SoloApagar     # 'stop' (no elimina; re-enciende rápido con 'start')
#   .\scripts\stop-deploy.ps1 -BorrarDatos    # ⚠️ TAMBIÉN borra los volúmenes (Postgres/Keycloak)
#
# Volver a levantar:  .\scripts\deploy.ps1   (o docker compose ... up -d)

[CmdletBinding()]
param(
    [switch]$SoloApagar,    # usa 'stop' en vez de 'down' (no recrea contenedores al volver)
    [switch]$BorrarDatos    # añade -v: elimina los volúmenes con datos. IRREVERSIBLE.
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# --profile tunnel incluye al servicio cloudflared en la operación, por si se
# levantó con el túnel.
$composeArgs = @('-f', 'docker-compose.yml', '-f', 'docker-compose.deploy.yml', '--profile', 'tunnel')

if ($SoloApagar) {
    Write-Host "==> Apagando contenedores (stop, sin eliminar)..." -ForegroundColor Cyan
    docker compose @composeArgs stop
    if ($LASTEXITCODE -ne 0) { Write-Host "ERROR al detener." -ForegroundColor Red; exit 1 }
    Write-Host "[OK] Detenidos. Re-encender:  docker compose -f docker-compose.yml -f docker-compose.deploy.yml start" -ForegroundColor Green
    exit 0
}

if ($BorrarDatos) {
    Write-Host "##############################################################" -ForegroundColor Yellow
    Write-Host " ADVERTENCIA: vas a ELIMINAR los volúmenes (datos de Postgres" -ForegroundColor Yellow
    Write-Host " y Keycloak se PIERDEN). Ctrl+C para cancelar; sigo en 5s..." -ForegroundColor Yellow
    Write-Host "##############################################################" -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    Write-Host "==> Eliminando contenedores, red Y volúmenes..." -ForegroundColor Cyan
    docker compose @composeArgs down -v
} else {
    Write-Host "==> Eliminando contenedores y red (los datos en volúmenes se CONSERVAN)..." -ForegroundColor Cyan
    docker compose @composeArgs down
}
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR al bajar el stack." -ForegroundColor Red; exit 1 }

# Confirmación: ¿quedó algún contenedor del proyecto vivo?
$left = docker compose @composeArgs ps -q 2>$null
if ([string]::IsNullOrWhiteSpace($left)) {
    Write-Host "[OK] Stack del deploy detenido. No quedan contenedores del proyecto activos." -ForegroundColor Green
} else {
    Write-Host "[!] Aún quedan contenedores activos:" -ForegroundColor Yellow
    docker compose @composeArgs ps
}
