# bind-domain.ps1 — the single scripted step AFTER you delegate your domain: bind the custom domain to
# the Ard.Artifacts container app + provision the free managed certificate.
#
#   ./scripts/bind-domain.ps1 -ZoneName example.com -HostLabel hunt
#
# Run ONLY after you've delegated $ZoneName to the Azure name servers printed by deploy-azure.ps1 AND the
# CNAME/A + asuid TXT records resolve publicly. It re-derives the same resources deploy-azure.ps1 created,
# fails fast if DNS isn't live yet, then runs `hostname add` + `hostname bind` and polls until Secured.
param(
    [Parameter(Mandatory)][string]$ZoneName,
    [string]$HostLabel = "hunt",
    [string]$EnvName   = "ard-hunt"
)
$ErrorActionPreference = "Stop"
# Native (azd/az) calls don't auto-throw on non-zero exit; the mutating ones below carry explicit
# $LASTEXITCODE guards (consistent on Windows PowerShell 5.1 and 7+).
Set-Location (Split-Path -Parent $PSScriptRoot)

azd env select $EnvName | Out-Null
$vals = azd env get-values
function Get-EnvVal([string]$key) { (($vals | Select-String "^$key=").Line -replace "^$key=", '').Trim('"') }
$rg = Get-EnvVal 'AZURE_RESOURCE_GROUP'
if (-not $rg) { $en = Get-EnvVal 'AZURE_ENV_NAME'; if ($en) { $rg = "rg-$en" } }
if (-not $rg) { $cae = Get-EnvVal 'AZURE_CONTAINER_APPS_ENVIRONMENT_ID'; if ($cae -match '/resourceGroups/([^/]+)/') { $rg = $Matches[1] } }
if (-not $rg) { throw "Could not determine the resource group from azd env '$EnvName'." }

function Get-AppName([string]$match) {
    $arr = @(az containerapp list -g $rg --query "[?contains(name,'$match')].name" -o tsv | Where-Object { $_ })
    if ($arr.Count -ne 1) { throw "Expected exactly one container app matching '$match' in '$rg', found $($arr.Count)." }
    $arr[0]
}
$artifactsName = Get-AppName "artifacts"
$envResName    = az containerapp show -g $rg -n $artifactsName --query "properties.environmentId" -o tsv | Split-Path -Leaf
if (-not $envResName) { throw "Empty environmentId for '$artifactsName'." }

$hunt       = if ($HostLabel -eq "@") { $ZoneName } else { "$HostLabel.$ZoneName" }
$validation = if ($HostLabel -eq "@") { "HTTP" } else { "CNAME" }
$asuid      = if ($HostLabel -eq "@") { "asuid.$ZoneName" } else { "asuid.$hunt" }

# Verify DNS is live before binding (bind fails until the records resolve publicly + delegation propagated).
try { if (-not (Resolve-DnsName -Name $asuid -Type TXT -ErrorAction Stop)) { throw } }
catch { throw "asuid TXT for '$asuid' does not resolve yet. Delegate $ZoneName to the Azure name servers and wait for propagation before binding. If you already created the records, your local resolver may be negative-caching a prior NXDOMAIN: run 'ipconfig /flushdns' (or query an authoritative server) and retry." }
if ($HostLabel -ne "@") {
    try { Resolve-DnsName -Name $hunt -Type CNAME -ErrorAction Stop | Out-Null }
    catch { throw "CNAME for '$hunt' does not resolve yet. Wait for delegation/propagation before binding. If you already created the records, your local resolver may be negative-caching a prior NXDOMAIN: run 'ipconfig /flushdns' (or query an authoritative server) and retry." }
}

# Idempotent: only add the hostname if it isn't already bound.
$already = az containerapp hostname list -g $rg -n $artifactsName --query "[?name=='$hunt'].name | [0]" -o tsv
if (-not $already) {
    Write-Host "Adding hostname $hunt to $artifactsName ..." -ForegroundColor Cyan
    az containerapp hostname add -g $rg -n $artifactsName --hostname $hunt | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "hostname add failed ($LASTEXITCODE)." }
}
Write-Host "Binding $hunt + provisioning the free managed cert (this can take several minutes) ..." -ForegroundColor Cyan
az containerapp hostname bind -g $rg -n $artifactsName --hostname $hunt --environment $envResName --validation-method $validation | Out-Null
if ($LASTEXITCODE -ne 0) { throw "hostname bind failed ($LASTEXITCODE)." }

# Poll until the binding reports secured (SniEnabled).
$secured = $false
for ($i = 0; $i -lt 30; $i++) {
    $status = az containerapp hostname list -g $rg -n $artifactsName --query "[?name=='$hunt'].bindingType | [0]" -o tsv
    if ($status -eq "SniEnabled") { Write-Host "Custom domain $hunt is Secured." -ForegroundColor Green; $secured = $true; break }
    Start-Sleep -Seconds 20
}
if (-not $secured) {
    Write-Warning "Binding for $hunt is not yet Secured after ~10 min (last bindingType: '$status'). Managed-cert issuance can take longer and can get stuck Pending — re-run this script, or check: az containerapp hostname list -g $rg -n $artifactsName -o table"
}
Write-Host ""
Write-Host "Solve your very own hunt:  dotnet run --project src/Ard.Walker -- --domain $hunt" -ForegroundColor Green
