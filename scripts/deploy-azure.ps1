# deploy-azure.ps1 — stand up your own hunt on Azure: Container Apps (compute) + Azure DNS.
#
#   az login ; azd auth login
#   ./scripts/deploy-azure.ps1 -ZoneName example.com -HostLabel hunt -Location swedencentral
#
# Steps: (1) create/select an azd environment + `azd up` provisions + deploys the 4 servers +
#        Ard.Artifacts as Container Apps; (2) reads their FQDNs; (3) provisions the Azure DNS zone +
#        _catalog TXT / _search SRV + custom-domain records via infra/dns.bicep; (4) prints the name
#        servers to delegate and the commands to bind the custom domain + run the smoke test.
#
# NOTE: validated by `az bicep build` + `azd infra generate`, but NOT by a live deploy here. Resource
# naming/outputs can vary by azd version — adjust the `az containerapp list` filters if a lookup is empty.
param(
    [Parameter(Mandatory)][string]$ZoneName,     # the domain you own + will delegate, e.g. example.com
    [string]$HostLabel = "hunt",                  # hunt.example.com ; use "@" for the zone apex
    [string]$Location  = "swedencentral",
    [string]$EnvName   = "ard-hunt"               # azd environment name (created if it doesn't exist)
)
$ErrorActionPreference = "Stop"

# Anchor to the repo root so relative paths (infra/dns.bicep, azure.yaml, src/...) resolve from any cwd.
Set-Location (Split-Path -Parent $PSScriptRoot)

# 1) Ensure an azd environment exists before setting values on it (azd env set/up operate on a
#    specific environment's .env, which only exists after env creation), then provision + deploy.
$existing = azd env list --output json 2>$null | ConvertFrom-Json
if (-not ($existing | Where-Object { $_.Name -eq $EnvName })) {
    azd env new $EnvName --location $Location | Out-Null   # prompts for a subscription if none is set
}
azd env select $EnvName | Out-Null
azd env set AZURE_LOCATION $Location | Out-Null
azd up -e $EnvName

# 2) Discover the resource group. The default (subscription-scoped) Aspire infra does NOT output
#    AZURE_RESOURCE_GROUP, so derive it from the env name (azd's rg-<env> convention) or the CAE id.
$vals = azd env get-values
function Get-EnvVal([string]$key) { (($vals | Select-String "^$key=") -replace '.*=', '').Trim('"') }
$rg = Get-EnvVal 'AZURE_RESOURCE_GROUP'
if (-not $rg) { $en = Get-EnvVal 'AZURE_ENV_NAME'; if ($en) { $rg = "rg-$en" } }
if (-not $rg) { $cae = Get-EnvVal 'AZURE_CONTAINER_APPS_ENVIRONMENT_ID'; if ($cae -match '/resourceGroups/([^/]+)/') { $rg = $Matches[1] } }
if (-not $rg) { throw "Could not determine the resource group (AZURE_RESOURCE_GROUP / AZURE_ENV_NAME / AZURE_CONTAINER_APPS_ENVIRONMENT_ID all empty)." }

function Get-AppName([string]$match) { az containerapp list -g $rg --query "[?contains(name,'$match')].name | [0]" -o tsv }
function Get-Fqdn([string]$name)     { az containerapp show -g $rg -n $name --query "properties.configuration.ingress.fqdn" -o tsv }

$searchName    = Get-AppName "challenge3-search"
$artifactsName = Get-AppName "artifacts"
if (-not $searchName)    { throw "No container app matching 'challenge3-search' found in '$rg'. Adjust the Get-AppName filter." }
if (-not $artifactsName) { throw "No container app matching 'artifacts' found in '$rg'. Adjust the Get-AppName filter." }

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
$envStaticIp = if ($HostLabel -eq "@") { az containerapp env show -g $rg -n $envResName --query "properties.staticIp" -o tsv } else { "" }

# 3) Provision Azure DNS (zone + ARD records + custom-domain records).
az deployment group create -g $rg -f infra/dns.bicep `
    -p zoneName=$ZoneName hostLabel=$HostLabel searchFqdn=$searchFqdn artifactsFqdn=$artifactsFqdn `
       artifactsCustomDomainVerificationId=$verifyId envStaticIp=$envStaticIp | Out-Null

$ns   = az network dns zone show -g $rg -n $ZoneName --query "nameServers" -o tsv
$hunt = if ($HostLabel -eq "@") { $ZoneName } else { "$HostLabel.$ZoneName" }
# Apex (A record) uses HTTP cert validation; subdomains (CNAME) use CNAME validation.
$validation  = if ($HostLabel -eq "@") { "HTTP" } else { "CNAME" }
$resolveHint = if ($HostLabel -eq "@") { "nslookup -type=A $hunt  and  nslookup -type=TXT asuid.$ZoneName" } else { "nslookup -type=CNAME $hunt  and  nslookup -type=TXT asuid.$hunt" }

Write-Host ""
Write-Host "=== ONE-TIME: at your registrar, delegate $ZoneName to these Azure name servers ===" -ForegroundColor Yellow
$ns | ForEach-Object { Write-Host "    $_" }
Write-Host ""
Write-Host "After delegation propagates (dig NS $ZoneName) AND both records resolve ($resolveHint)," -ForegroundColor Cyan
Write-Host "bind the custom domain + free managed cert (issuance can take several minutes):" -ForegroundColor Cyan
Write-Host "    az containerapp hostname add  -g $rg -n $artifactsName --hostname $hunt"
Write-Host "    az containerapp hostname bind -g $rg -n $artifactsName --hostname $hunt --environment $envResName --validation-method $validation"
Write-Host ""
Write-Host "Then solve your very own hunt (real https + public DNS):" -ForegroundColor Green
Write-Host "    dotnet run --project src/Ard.Walker -- --domain $hunt"
Write-Host ""
Write-Host "Tear it all down when done:  azd down --force --purge ; az network dns zone delete -g $rg -n $ZoneName" -ForegroundColor DarkGray
