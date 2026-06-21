# UMBRAL — Deploy con health-check y rollback automático
#
# Lo llama el workflow de GitHub Actions (self-hosted runner) en cada push a
# develop, o podés correrlo a mano:
#
#   .\scripts\deploy.ps1                  # despliega el HEAD actual
#   .\scripts\deploy.ps1 -Ref <sha>       # despliega un commit puntual
#   .\scripts\deploy.ps1 -NoRollback      # no revierte si falla (para depurar)
#
# Flujo: build + migraciones + up  ->  health-check de los 8 contenedores.
#   • Si TODO responde  -> guarda este commit como "último estable".
#   • Si algo falla      -> git checkout al último estable + redeploy (rollback).

[CmdletBinding()]
param(
    [string]$Ref,
    [int]$HealthTimeoutSec = 150,
    [switch]$NoRollback
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$composeArgs  = @('-f', 'docker-compose.yml', '-f', 'docker-compose.deploy.yml')
# El estado vive FUERA del repo: actions/checkout hace 'git clean -ffdx' y
# borraría cualquier carpeta dentro del repo (incluso ignorada), perdiendo el
# "último estable". Override opcional con la env var UMBRAL_DEPLOY_STATE.
$stateDir     = if ($env:UMBRAL_DEPLOY_STATE) { $env:UMBRAL_DEPLOY_STATE } else { Join-Path $HOME '.umbral-deploy' }
$lastGoodFile = Join-Path $stateDir 'last-good-commit'
New-Item -ItemType Directory -Force -Path $stateDir | Out-Null

# Servicios .NET con migraciones EF -> nombre de su base de datos.
$migrationDbs = [ordered]@{
    MissionService = 'umbral_missions'
    StageService   = 'umbral_stages'
    ClueService    = 'umbral_clues'
    TeamService    = 'umbral_teams'
    SessionService = 'umbral_sessions'
}

# Endpoints de salud (cualquier respuesta HTTP = el contenedor está vivo).
$healthUrls = [ordered]@{
    ApiGateway        = 'http://localhost:5080'
    MissionService    = 'http://localhost:5091'
    SessionService    = 'http://localhost:5092'
    StageService      = 'http://localhost:5093'
    ClueService       = 'http://localhost:5094'
    TeamService       = 'http://localhost:5095'
    UserService       = 'http://localhost:5096'
    'Front operador'  = 'http://localhost:5173'
    'Front particip.' = 'http://localhost:5174'
}

# ── Helpers ──────────────────────────────────────────────────────────────────

# Carga POSTGRES_PASSWORD desde .env (las migraciones corren en el host).
function Get-PgPassword {
    $envFile = Join-Path $repoRoot '.env'
    if (Test-Path $envFile) {
        $line = Select-String -Path $envFile -Pattern '^\s*POSTGRES_PASSWORD\s*=' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($line) { return ($line.Line -replace '^\s*POSTGRES_PASSWORD\s*=', '').Trim() }
    }
    return '12345'
}

function Test-ServiceUp([string]$url, [int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5 | Out-Null
            return $true                              # 2xx/3xx
        } catch {
            if ($null -ne $_.Exception.Response) { return $true }  # 4xx/5xx = vivo
            Start-Sleep -Seconds 3                     # conexión rechazada -> reintentar
        }
    }
    return $false
}

function Test-AllHealthy {
    $allUp = $true
    foreach ($name in $healthUrls.Keys) {
        if (Test-ServiceUp $healthUrls[$name] $HealthTimeoutSec) {
            Write-Host "   [OK] $name" -ForegroundColor Green
        } else {
            Write-Host "   [X ] $name no respondió" -ForegroundColor Red
            $allUp = $false
        }
    }
    return $allUp
}

function Invoke-Deploy([string]$label) {
    Write-Host "==> [$label] Infra (postgres, rabbit, keycloak, mailpit)..." -ForegroundColor Cyan
    docker compose @composeArgs up -d postgres keycloak-db rabbitmq keycloak mailpit
    if ($LASTEXITCODE -ne 0) { throw "docker compose (infra) falló" }

    Write-Host "==> [$label] Esperando a Postgres..." -ForegroundColor Cyan
    $ok = $false
    for ($i = 0; $i -lt 30 -and -not $ok; $i++) {
        Start-Sleep -Seconds 2
        docker exec umbral-postgres pg_isready -U postgres *> $null
        $ok = ($LASTEXITCODE -eq 0)
    }
    if (-not $ok) { throw "Postgres no respondió" }

    Write-Host "==> [$label] Migraciones EF..." -ForegroundColor Cyan
    $pgPass = Get-PgPassword
    foreach ($svc in $migrationDbs.Keys) {
        $db = $migrationDbs[$svc]
        $env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=$db;Username=postgres;Password=$pgPass"
        Push-Location "UMBRAL_Back-end/$svc"
        try { dotnet ef database update } finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) { throw "migración falló en $svc" }
    }
    Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue

    Write-Host "==> [$label] Build + up de servicios y fronts..." -ForegroundColor Cyan
    docker compose @composeArgs up -d --build
    if ($LASTEXITCODE -ne 0) { throw "docker compose (apps) falló" }
}

# ── Flujo principal ────────────────────────────────────────────────────────────

$target = if ($Ref) { $Ref } else { (git rev-parse HEAD).Trim() }
$previousGood = if (Test-Path $lastGoodFile) { (Get-Content $lastGoodFile -Raw).Trim() } else { $null }
Write-Host "==> Desplegando $target (último estable previo: $previousGood)" -ForegroundColor Cyan

$deployError = $null
try { Invoke-Deploy "deploy" } catch { $deployError = $_; Write-Host "ERROR en deploy: $_" -ForegroundColor Red }

if (-not $deployError -and (Test-AllHealthy)) {
    Set-Content -Path $lastGoodFile -Value $target
    Write-Host "`n[OK] Deploy exitoso. Último estable = $target" -ForegroundColor Green
    exit 0
}

# ── Rollback ─────────────────────────────────────────────────────────────────
Write-Host "`n[!] El deploy de $target no quedó sano." -ForegroundColor Yellow
if ($NoRollback) { Write-Host "    -NoRollback activo; no se revierte." -ForegroundColor Yellow; exit 1 }
if (-not $previousGood -or $previousGood -eq $target) {
    Write-Host "    No hay un commit estable previo distinto; no se puede hacer rollback." -ForegroundColor Yellow
    exit 1
}

Write-Host "==> Rollback al último estable: $previousGood" -ForegroundColor Yellow
git checkout --force $previousGood
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: no se pudo hacer checkout de $previousGood" -ForegroundColor Red; exit 2 }

try { Invoke-Deploy "rollback" } catch { Write-Host "ERROR en rollback: $_" -ForegroundColor Red; exit 2 }

if (Test-AllHealthy) {
    Write-Host "`n[OK] Rollback exitoso. Corriendo el último estable: $previousGood" -ForegroundColor Green
    exit 1   # exit != 0 para que el CI marque el push como fallido
}
Write-Host "`n[X] Ni el rollback quedó sano. Revisar manualmente (docker compose ps / logs)." -ForegroundColor Red
exit 2
