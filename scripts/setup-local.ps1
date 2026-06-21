# One-time setup for the local self-hosted hunt: install + build the vendored TS servers.
# After this, run:  dotnet run --project src/Ard.AppHost
$ErrorActionPreference = "Stop"
$servers = "challenge1-mcp", "challenge2-mcp", "challenge3-mcp", "challenge3-search"
foreach ($s in $servers) {
    Write-Host "==> building $s" -ForegroundColor Cyan
    Push-Location (Join-Path $PSScriptRoot "../servers/$s")
    try {
        npm install --no-audit --no-fund
        npm run build
    }
    finally { Pop-Location }
}
Write-Host "`nServers built. Start the whole hunt with:" -ForegroundColor Green
Write-Host "  dotnet run --project src/Ard.AppHost" -ForegroundColor Green
