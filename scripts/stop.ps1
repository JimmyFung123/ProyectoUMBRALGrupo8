# UMBRAL — Apagado completo
#
# Uso desde la raiz del repo:
#   .\scripts\stop.ps1              (servicios + infra)
#   .\scripts\stop.ps1 -KeepInfra   (solo cierra las ventanas de servicios)
#   .\scripts\stop.ps1 -KeepData    (apaga infra pero conserva el volumen de Postgres)

[CmdletBinding()]
param(
    [switch]$KeepInfra,
    [switch]$KeepData
)

$ErrorActionPreference = 'Continue'

# ── Ubicarse en la raiz ─────────────────────────────────────────────────────
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# ── 1. Cerrar ventanas PowerShell de UMBRAL ─────────────────────────────────
Write-Host "==> Cerrando procesos dotnet / node ligados a UMBRAL..." -ForegroundColor Cyan

# Matar todos los dotnet run y node de Vite que se hayan lanzado desde el repo
Get-Process dotnet -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $cmdPath = $_.Path
        if ($cmdPath -and $cmdPath -like "*$repoRoot*") {
            $_.Kill()
        }
    } catch { }
}

# Vite + npm levantan procesos node; los matamos por CWD si podemos
Get-CimInstance Win32_Process -Filter "Name='node.exe' OR Name='dotnet.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine -like "*$repoRoot*" } |
    ForEach-Object {
        try { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue } catch { }
    }

# Cerrar ventanas PowerShell con titulo "UMBRAL - ..." (las que lanzo start.ps1)
Get-Process powershell -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -like 'UMBRAL - *' } |
    ForEach-Object {
        try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch { }
    }

# Apagar el build-server de .NET (VBCSCompiler / MSBuild) que sobrevive a los
# procesos dotnet run y mantiene cacheado UMBRAL.Auth.dll, lo cual genera
# "file is being used by another process" al intentar rebuildear con cambios
# en la libreria compartida.
Write-Host "==> Apagando build-server de .NET (VBCSCompiler / MSBuild)..." -ForegroundColor Cyan
try {
    dotnet build-server shutdown 2>&1 | Out-Null
} catch { }
Get-Process VBCSCompiler, MSBuild -ErrorAction SilentlyContinue |
    ForEach-Object {
        try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch { }
    }

Write-Host "    Procesos cerrados." -ForegroundColor Green

# ── 2. Bajar infra ──────────────────────────────────────────────────────────
if (-not $KeepInfra) {
    if ($KeepData) {
        Write-Host "==> docker compose down (conservando volumen de Postgres)..." -ForegroundColor Cyan
        docker compose down
    } else {
        Write-Host "==> docker compose down (sin tocar volumenes)..." -ForegroundColor Cyan
        docker compose down
        Write-Host "    Tip: para borrar tambien la BD, corre: docker compose down -v" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "UMBRAL detenido." -ForegroundColor Green
