# deploy-azure.ps1 — stand up your own hunt on Azure: Container Apps (compute) + Azure DNS.
#
#   az login ; azd auth login
#   ./scripts/deploy-azure.ps1 -ZoneName example.com -HostLabel hunt -Location swedencentral
#
# Builds everything from zero: (1) `azd up` creates the resource group, Container Apps environment,
# registry, identity, and the 5 container apps (4 servers + Ard.Artifacts) from the Dockerfiles;
# (2) reads their FQDNs; (3) `infra/dns.bicep` creates the Azure DNS zone + _catalog TXT / _search SRV +
# the custom-domain records. Then it prints the name servers to delegate (the ONE human gate) and tells
# you to run scripts/bind-domain.ps1 once delegation has propagated. See docs/SELFHOST.md.
#
# NOTE: validated by `az bicep build` + `azd infra generate`, not by a live deploy here.
param(
    [Parameter(Mandatory)][string]$ZoneName,     # the domain you own + will delegate, e.g. example.com
    [string]$HostLabel = "hunt",                  # hunt.example.com ; use "@" for the zone apex
    [string]$Location  = "swedencentral",
    [string]$EnvName   = "ard-hunt"               # azd environment name (created if it doesn't exist)
)
$ErrorActionPreference = "Stop"
# Native (azd/az) calls don't auto-throw on a non-zero exit, so each critical one below is followed by an
# explicit $LASTEXITCODE guard with a friendly message — identical behavior on Windows PowerShell 5.1 and 7+.

# Anchor to the repo root so relative paths (infra/dns.bicep, azure.yaml) resolve from any cwd.
Set-Location (Split-Path -Parent $PSScriptRoot)

# 0) Preflight: the tools must be present and Docker must be running (azd builds the server images locally).
foreach ($t in @('azd', 'az', 'docker')) {
    if (-not (Get-Command $t -ErrorAction SilentlyContinue)) { throw "Required tool '$t' is not on PATH." }
}
docker info *> $null
if ($LASTEXITCODE -ne 0) { throw "The Docker daemon is not running — azd needs it to build the server container images." }

# 1) Ensure an azd environment exists before setting values on it, then provision + deploy from zero.
# `azd env list` exits non-zero on a fresh clone (no .azure state) — that's expected. Tolerate any
# non-JSON/empty output by defaulting to $null so we fall through to `azd env new`.
$existing = try { azd env list --output json 2>$null | ConvertFrom-Json } catch { $null }
if (-not ($existing | Where-Object { $_.Name -eq $EnvName })) {
    azd env new $EnvName --location $Location | Out-Null   # prompts for a subscription if none is set
    if ($LASTEXITCODE -ne 0) { throw "azd env new failed ($LASTEXITCODE)." }
}
azd env select $EnvName | Out-Null
if ($LASTEXITCODE -ne 0) { throw "azd env select failed ($LASTEXITCODE)." }
azd env set AZURE_LOCATION $Location | Out-Null
if ($LASTEXITCODE -ne 0) { throw "azd env set AZURE_LOCATION failed ($LASTEXITCODE)." }
azd up -e $EnvName
if ($LASTEXITCODE -ne 0) { throw "azd up failed ($LASTEXITCODE)." }

# 2) Discover the resource group. The default Aspire infra does NOT output AZURE_RESOURCE_GROUP, so derive
#    it from the env name (azd's rg-<env> convention) or the Container Apps environment id, then verify it.
$vals = azd env get-values
function Get-EnvVal([string]$key) { (($vals | Select-String "^$key=").Line -replace "^$key=", '').Trim('"') }
$rg = Get-EnvVal 'AZURE_RESOURCE_GROUP'
if (-not $rg) { $en = Get-EnvVal 'AZURE_ENV_NAME'; if ($en) { $rg = "rg-$en" } }
if (-not $rg) { $cae = Get-EnvVal 'AZURE_CONTAINER_APPS_ENVIRONMENT_ID'; if ($cae -match '/resourceGroups/([^/]+)/') { $rg = $Matches[1] } }
if (-not $rg) { throw "Could not determine the resource group (AZURE_RESOURCE_GROUP / AZURE_ENV_NAME / AZURE_CONTAINER_APPS_ENVIRONMENT_ID all empty)." }
if ((az group exists -n $rg).Trim() -ne 'true') { throw "Derived resource group '$rg' does not exist — 'azd up' may not have completed. Set AZURE_RESOURCE_GROUP explicitly." }

# Resolve a container app by a name substring, asserting exactly one match (azd may add a suffix).
function Get-AppName([string]$match) {
    $arr = @(az containerapp list -g $rg --query "[?contains(name,'$match')].name" -o tsv | Where-Object { $_ })
    if ($arr.Count -ne 1) { throw "Expected exactly one container app matching '$match' in '$rg', found $($arr.Count): $($arr -join ', ')." }
    $arr[0]
}
function Get-Fqdn([string]$name) { az containerapp show -g $rg -n $name --query "properties.configuration.ingress.fqdn" -o tsv }

$searchName    = Get-AppName "challenge3-search"
$artifactsName = Get-AppName "artifacts"
$searchFqdn    = Get-Fqdn $searchName
$artifactsFqdn = Get-Fqdn $artifactsName
$verifyId      = az containerapp show -g $rg -n $artifactsName --query "properties.customDomainVerificationId" -o tsv
$envResName    = az containerapp show -g $rg -n $artifactsName --query "properties.environmentId" -o tsv | Split-Path -Leaf
# Native az calls can return "" with exit code 0 on a no-match; assert non-empty so we never emit empty DNS records.
if (-not $searchFqdn)    { throw "Empty ingress FQDN for '$searchName' (is ingress external?)." }
if (-not $artifactsFqdn) { throw "Empty ingress FQDN for '$artifactsName' (is ingress external?)." }
if (-not $verifyId)      { throw "Empty customDomainVerificationId for '$artifactsName'." }
if (-not $envResName)    { throw "Empty environmentId for '$artifactsName'." }
# Apex hunts bind via an A record to the Container Apps environment static IP.
$envStaticIp = ""
if ($HostLabel -eq "@") {
    $envStaticIp = az containerapp env show -g $rg -n $envResName --query "properties.staticIp" -o tsv
    if (-not $envStaticIp) { throw "Empty environment staticIp — required for an apex hunt (hostLabel '@')." }
}

# 3) Provision Azure DNS (zone + ARD records + custom-domain records).
az deployment group create -g $rg -f infra/dns.bicep `
    -p zoneName=$ZoneName hostLabel=$HostLabel searchFqdn=$searchFqdn artifactsFqdn=$artifactsFqdn `
       artifactsCustomDomainVerificationId=$verifyId envStaticIp=$envStaticIp | Out-Null
if ($LASTEXITCODE -ne 0) { throw "DNS deployment (infra/dns.bicep) failed ($LASTEXITCODE)." }

$ns          = az network dns zone show -g $rg -n $ZoneName --query "nameServers" -o tsv
$hunt        = if ($HostLabel -eq "@") { $ZoneName } else { "$HostLabel.$ZoneName" }
$resolveHint = if ($HostLabel -eq "@") { "nslookup -type=A $hunt  and  nslookup -type=TXT asuid.$ZoneName" } else { "nslookup -type=CNAME $hunt  and  nslookup -type=TXT asuid.$hunt" }

Write-Host ""
Write-Host "=== ONE-TIME: at your registrar, delegate $ZoneName to these Azure name servers ===" -ForegroundColor Yellow
$ns | ForEach-Object { Write-Host "    $_" }
Write-Host ""
Write-Host "After delegation propagates (dig NS $ZoneName) AND both records resolve ($resolveHint)," -ForegroundColor Cyan
Write-Host "run the second script to bind the custom domain + free managed cert (issuance can take several minutes):" -ForegroundColor Cyan
Write-Host "    ./scripts/bind-domain.ps1 -ZoneName $ZoneName -HostLabel $HostLabel -EnvName $EnvName"
Write-Host ""
Write-Host "Then solve your very own hunt (real https + public DNS):" -ForegroundColor Green
Write-Host "    dotnet run --project src/Ard.Walker -- --domain $hunt"
Write-Host ""
Write-Host "Tear it all down when done:  az network dns zone delete -g $rg -n $ZoneName --yes ; azd down --force --purge" -ForegroundColor DarkGray
