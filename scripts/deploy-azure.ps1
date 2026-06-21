# deploy-azure.ps1 — stand up your own hunt on Azure: Container Apps (compute) + Azure DNS.
#
#   az login ; azd auth login
#   ./scripts/deploy-azure.ps1 -ZoneName example.com -HostLabel hunt -Location swedencentral
#
# Steps: (1) azd up provisions + deploys the 4 servers + Ard.Artifacts as Container Apps;
#        (2) reads their FQDNs; (3) provisions the Azure DNS zone + _catalog TXT / _search SRV +
#        custom-domain records via infra/dns.bicep; (4) prints the name servers to delegate and the
#        commands to bind the custom domain + run the smoke test.
#
# NOTE: validated by `az bicep build` + `azd infra generate`, but NOT by a live deploy here. Resource
# naming/outputs can vary by azd version — adjust the `az containerapp list` filters if a lookup is empty.
param(
    [Parameter(Mandatory)][string]$ZoneName,     # the domain you own + will delegate, e.g. example.com
    [string]$HostLabel = "hunt",                  # hunt.example.com ; use "@" for the zone apex
    [string]$Location  = "swedencentral"
)
$ErrorActionPreference = "Stop"

# 1) Provision + deploy the Aspire app to Azure Container Apps.
azd env set AZURE_LOCATION $Location | Out-Null
azd up

# 2) Discover the resource group + the deployed container apps.
$rg = ((azd env get-values | Select-String '^AZURE_RESOURCE_GROUP=') -replace '.*=', '').Trim('"')
if (-not $rg) { throw "Could not read AZURE_RESOURCE_GROUP from 'azd env get-values'." }

function Get-AppName([string]$match) {
    az containerapp list -g $rg --query "[?contains(name,'$match')].name | [0]" -o tsv
}
function Get-Fqdn([string]$name) {
    az containerapp show -g $rg -n $name --query "properties.configuration.ingress.fqdn" -o tsv
}

$searchName    = Get-AppName "challenge3-search"
$artifactsName = Get-AppName "artifacts"
$searchFqdn    = Get-Fqdn $searchName
$artifactsFqdn = Get-Fqdn $artifactsName
$verifyId      = az containerapp show -g $rg -n $artifactsName --query "properties.customDomainVerificationId" -o tsv
$envName       = az containerapp show -g $rg -n $artifactsName --query "properties.environmentId" -o tsv | Split-Path -Leaf

# 3) Provision Azure DNS (zone + ARD records + custom-domain records).
az deployment group create -g $rg -f infra/dns.bicep `
    -p zoneName=$ZoneName hostLabel=$HostLabel searchFqdn=$searchFqdn artifactsFqdn=$artifactsFqdn `
       artifactsCustomDomainVerificationId=$verifyId | Out-Null

$ns   = az network dns zone show -g $rg -n $ZoneName --query "nameServers" -o tsv
$hunt = if ($HostLabel -eq "@") { $ZoneName } else { "$HostLabel.$ZoneName" }

Write-Host ""
Write-Host "=== ONE-TIME: at your registrar, delegate $ZoneName to these Azure name servers ===" -ForegroundColor Yellow
$ns | ForEach-Object { Write-Host "    $_" }
Write-Host ""
Write-Host "After delegation propagates (dig NS $ZoneName), bind the custom domain + free managed cert:" -ForegroundColor Cyan
Write-Host "    az containerapp hostname add  -g $rg -n $artifactsName --hostname $hunt"
Write-Host "    az containerapp hostname bind -g $rg -n $artifactsName --hostname $hunt --environment $envName --validation-method CNAME"
Write-Host ""
Write-Host "Then solve your very own hunt (real https + public DNS):" -ForegroundColor Green
Write-Host "    dotnet run --project src/Ard.Walker -- --domain $hunt"
Write-Host ""
Write-Host "Tear it all down when done:  azd down --force --purge ; az network dns zone delete -g $rg -n $ZoneName" -ForegroundColor DarkGray
