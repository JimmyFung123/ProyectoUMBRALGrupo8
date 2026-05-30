# UMBRAL — Arranque completo
#
# Uso desde la raiz del repo:
#   .\scripts\start.ps1            (todo: infra + migraciones + servicios + fronts + tunel movil)
#   .\scripts\start.ps1 -SkipInfra (asume Postgres + RabbitMQ ya corriendo)
#   .\scripts\start.ps1 -SkipMigrations
#   .\scripts\start.ps1 -BackOnly  (no levanta los front-ends ni el tunel)
#   .\scripts\start.ps1 -NoTunnel  (no expone el front participante por Cloudflare)
#
# Cada servicio .NET y cada front-end se abre en su propia ventana de PowerShell
# para que puedas leer sus logs por separado.
#
# Tunel movil: por defecto levanta un Cloudflare Quick Tunnel apuntando al front
# participante (5174) y muestra una URL https://...trycloudflare.com para entrar
# desde el celular (cert valido, sin instalar nada). La URL cambia en cada
# arranque. Requiere cloudflared (winget install Cloudflare.cloudflared).

[CmdletBinding()]
param(
    [switch]$SkipInfra,
    [switch]$SkipMigrations,
    [switch]$BackOnly,
    [switch]$NoTunnel
)

$ErrorActionPreference = 'Stop'

# ── 0. Ubicarse en la raiz del repo ─────────────────────────────────────────
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot
Write-Host "==> Repo raiz: $repoRoot" -ForegroundColor Cyan

# ── 1. Verificar herramientas ───────────────────────────────────────────────
function Test-Tool($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Write-Host "ERROR: '$name' no esta instalado. $hint" -ForegroundColor Red
        exit 1
    }
}

if (-not $SkipInfra) { Test-Tool 'docker' 'Instala Docker Desktop.' }
Test-Tool 'dotnet' 'Instala .NET 10 SDK.'
if (-not $BackOnly) { Test-Tool 'npm' 'Instala Node.js 20+.' }

# Localiza cloudflared aunque el PATH de esta sesion no se haya refrescado tras
# instalarlo con winget (busca el alias en PATH y luego en los paquetes WinGet).
function Get-CloudflaredPath {
    $cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $direct = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Cloudflare.cloudflared_Microsoft.Winget.Source_8wekyb3d8bbwe\cloudflared.exe"
    if (Test-Path $direct) { return $direct }
    $found = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter 'cloudflared.exe' -ErrorAction SilentlyContinue |
             Select-Object -First 1
    if ($found) { return $found.FullName }
    return $null
}

# dotnet-ef como herramienta global
if (-not $SkipMigrations) {
    $efInstalled = dotnet tool list --global 2>$null | Select-String 'dotnet-ef'
    if (-not $efInstalled) {
        Write-Host "==> Instalando dotnet-ef global..." -ForegroundColor Yellow
        dotnet tool install --global dotnet-ef | Out-Null
    }
}

# ── 2. Levantar Postgres + RabbitMQ ─────────────────────────────────────────
if (-not $SkipInfra) {
    Write-Host "==> Levantando Postgres y RabbitMQ (docker compose up -d)..." -ForegroundColor Cyan
    docker compose up -d
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: docker compose fallo." -ForegroundColor Red
        exit 1
    }

    # Esperar a que Postgres acepte conexiones
    Write-Host "==> Esperando a Postgres..." -ForegroundColor Cyan
    $attempts = 0
    do {
        Start-Sleep -Seconds 2
        $attempts++
        docker exec umbral-postgres pg_isready -U postgres 2>&1 | Out-Null
        $ready = ($LASTEXITCODE -eq 0)
        if ($attempts -gt 30) {
            Write-Host "ERROR: Postgres no respondio tras 60s." -ForegroundColor Red
            exit 1
        }
    } while (-not $ready)
    Write-Host "    Postgres listo." -ForegroundColor Green

    # Esperar a RabbitMQ
    Write-Host "==> Esperando a RabbitMQ..." -ForegroundColor Cyan
    $attempts = 0
    do {
        Start-Sleep -Seconds 2
        $attempts++
        docker exec umbral-rabbitmq rabbitmq-diagnostics ping 2>&1 | Out-Null
        $ready = ($LASTEXITCODE -eq 0)
        if ($attempts -gt 30) {
            Write-Host "ERROR: RabbitMQ no respondio tras 60s." -ForegroundColor Red
            exit 1
        }
    } while (-not $ready)
    Write-Host "    RabbitMQ listo." -ForegroundColor Green

    # Esperar a Keycloak (puerto 8090). El healthcheck del contenedor usa el
    # puerto 9000 interno; aca probamos contra el endpoint OIDC publico para
    # asegurarnos que el realm UMBRAL ya esta cargado.
    Write-Host "==> Esperando a Keycloak..." -ForegroundColor Cyan
    $attempts = 0
    do {
        Start-Sleep -Seconds 3
        $attempts++
        $ready = $false
        try {
            $resp = Invoke-WebRequest -Uri 'http://localhost:8090/realms/umbral/.well-known/openid-configuration' `
                       -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            if ($resp.StatusCode -eq 200) { $ready = $true }
        } catch { }
        if ($attempts -gt 30) {
            Write-Host "ERROR: Keycloak no respondio tras ~90s. Verifica logs con 'docker logs umbral-keycloak'." -ForegroundColor Red
            exit 1
        }
    } while (-not $ready)
    Write-Host "    Keycloak listo. Admin console: http://localhost:8090 (admin / admin)" -ForegroundColor Green
    Write-Host "    Admin UMBRAL: admin@umbral.local / Umbral2026!" -ForegroundColor Green
}

# ── 3. Restaurar y migrar ───────────────────────────────────────────────────
if (-not $SkipMigrations) {
    Write-Host "==> dotnet restore..." -ForegroundColor Cyan
    dotnet restore UMBRAL_Back-end | Out-Null

    $services = @(
        'MissionService',
        'StageService',
        'ClueService',
        'TeamService',
        'SessionService'
    )

    foreach ($svc in $services) {
        Write-Host "==> Migrando $svc..." -ForegroundColor Cyan
        Push-Location "UMBRAL_Back-end\$svc"
        try {
            dotnet ef database update
            if ($LASTEXITCODE -ne 0) {
                Write-Host "ERROR: migracion fallo en $svc." -ForegroundColor Red
                Pop-Location
                exit 1
            }
        } finally {
            Pop-Location
        }
    }
    Write-Host "    Todas las migraciones aplicadas." -ForegroundColor Green
}

# ── 4. npm install de los front-ends (si node_modules falta) ────────────────
if (-not $BackOnly) {
    foreach ($front in 'UMBRAL_Front-end', 'UMBRAL_Front-end_Participantes') {
        if (-not (Test-Path "$front\node_modules")) {
            Write-Host "==> npm install en $front..." -ForegroundColor Cyan
            Push-Location $front
            try { npm install } finally { Pop-Location }
        }
    }
}

# ── 5. Arrancar servicios en ventanas separadas ─────────────────────────────
function Start-InNewWindow($title, $workDir, $command) {
    $abs = Join-Path $repoRoot $workDir
    $psArgs = @(
        '-NoExit',
        '-Command',
        "`$host.UI.RawUI.WindowTitle='$title'; Set-Location '$abs'; $command"
    )
    Start-Process powershell -ArgumentList $psArgs | Out-Null
}

Write-Host "==> Abriendo ventanas de servicios..." -ForegroundColor Cyan

Start-InNewWindow 'UMBRAL - MissionService (5091)' 'UMBRAL_Back-end\MissionService' 'dotnet run'
Start-InNewWindow 'UMBRAL - StageService (5093)'   'UMBRAL_Back-end\StageService'   'dotnet run'
Start-InNewWindow 'UMBRAL - ClueService (5094)'    'UMBRAL_Back-end\ClueService'    'dotnet run'
Start-InNewWindow 'UMBRAL - TeamService (5095)'    'UMBRAL_Back-end\TeamService'    'dotnet run'
Start-InNewWindow 'UMBRAL - UserService (5096)'    'UMBRAL_Back-end\UserService'    'dotnet run'
Start-Sleep -Seconds 2  # dar tiempo a que los servicios satelite arranquen primero
Start-InNewWindow 'UMBRAL - SessionService (5092)' 'UMBRAL_Back-end\SessionService' 'dotnet run'

if (-not $BackOnly) {
    Start-InNewWindow 'UMBRAL - Front Operador (5173)'      'UMBRAL_Front-end'              'npm run dev'
    Start-InNewWindow 'UMBRAL - Front Participante (5174)' 'UMBRAL_Front-end_Participantes' 'npm run dev'
}

# ── 5b. Tunel Cloudflare para el front participante (acceso movil HTTPS) ──────
# El front participante sirve API + SignalR por proxy desde su propio origen
# (ver vite.config: proxy /api,/team-api,/hubs + allowedHosts), asi que un solo
# tunel al 5174 basta: HTTPS con cert valido, sin CORS ni mixed content.
$tunnelUrl = $null
if (-not $BackOnly -and -not $NoTunnel) {
    $cf = Get-CloudflaredPath
    if (-not $cf) {
        Write-Host "==> cloudflared no esta instalado; omito el tunel movil." -ForegroundColor Yellow
        Write-Host "    Instalalo con: winget install Cloudflare.cloudflared" -ForegroundColor DarkGray
    } else {
        $tunnelLog = Join-Path $env:TEMP 'umbral-cloudflared.log'
        if (Test-Path $tunnelLog) { Remove-Item $tunnelLog -Force -ErrorAction SilentlyContinue }
        Write-Host "==> Levantando tunel Cloudflare (front participante 5174)..." -ForegroundColor Cyan
        # Ventana propia: deja ver los logs y mantiene vivo el proceso. Tee escribe
        # el log a un archivo que sondeamos para extraer la URL del tunel.
        $cfCmd = "`$host.UI.RawUI.WindowTitle='UMBRAL - Cloudflared (movil)'; " +
                 "& '$cf' tunnel --url http://localhost:5174 --no-autoupdate 2>&1 | " +
                 "Tee-Object -FilePath '$tunnelLog'"
        Start-Process powershell -ArgumentList @('-NoExit', '-Command', $cfCmd) | Out-Null

        # Esperar a que cloudflared imprima la URL (hasta ~25s).
        $attempts = 0
        do {
            Start-Sleep -Seconds 1
            $attempts++
            if (Test-Path $tunnelLog) {
                $m = Select-String -Path $tunnelLog -Pattern 'https://[a-z0-9-]+\.trycloudflare\.com' -ErrorAction SilentlyContinue |
                     Select-Object -First 1
                if ($m) { $tunnelUrl = $m.Matches[0].Value }
            }
        } while (-not $tunnelUrl -and $attempts -lt 25)

        if ($tunnelUrl) {
            Write-Host "    Tunel listo: $tunnelUrl" -ForegroundColor Green
        } else {
            Write-Host "    El tunel tarda en responder; revisa la ventana 'UMBRAL - Cloudflared (movil)'." -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host " UMBRAL arriba" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host " Backend:"
Write-Host "   MissionService  http://localhost:5091/swagger"
Write-Host "   SessionService  http://localhost:5092/swagger"
Write-Host "   StageService    http://localhost:5093/swagger"
Write-Host "   ClueService     http://localhost:5094/swagger"
Write-Host "   TeamService     http://localhost:5095/swagger"
Write-Host "   UserService     http://localhost:5096/swagger  (HU-23)"
Write-Host " Infra:"
Write-Host "   PostgreSQL      localhost:5432   (postgres / 12345)"
Write-Host "   RabbitMQ UI     http://localhost:15672  (guest / guest)"
Write-Host "   Keycloak        http://localhost:8090   (admin / admin)"
Write-Host "                   Admin UMBRAL: admin@umbral.local / Umbral2026!"
if (-not $BackOnly) {
    Write-Host " Front:"
    Write-Host "   Operador       http://localhost:5173"
    Write-Host "   Participante   http://localhost:5174"
    if ($tunnelUrl) {
        Write-Host "   Movil (HTTPS)  $tunnelUrl" -ForegroundColor Cyan
    } elseif (-not $NoTunnel) {
        Write-Host "   Movil (HTTPS)  (tunel iniciando; ver ventana 'UMBRAL - Cloudflared (movil)')" -ForegroundColor DarkGray
    }
}
Write-Host ""
Write-Host " Para parar todo:  .\scripts\stop.ps1" -ForegroundColor Yellow
Write-Host ""
