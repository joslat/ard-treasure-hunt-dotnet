// infra/dns.bicep — the Azure DNS half of a self-hosted ARD hunt (the part azd/Aspire doesn't do).
//
// Creates the DNS zone you delegate, plus the two ARD discovery records (_catalog TXT, _search SRV)
// and the records that bind your custom domain to the Ard.Artifacts container app (so the well-known
// catalog is served at https://{hunt domain}/.well-known/ai-catalog.json).
//
// The FQDNs + verification id come from the Container Apps that `azd up` deployed — scripts/deploy-azure.ps1
// reads them and passes them in. Deploy at the SUBSCRIPTION/RESOURCE-GROUP scope:
//   az deployment group create -g <rg> -f infra/dns.bicep -p @infra/dns.parameters.json

targetScope = 'resourceGroup'

@description('The Azure DNS zone you own and will delegate (e.g. example.com).')
param zoneName string

@description('Host label for the hunt under the zone — e.g. "hunt" for hunt.example.com, or "@" for the zone apex.')
param hostLabel string = 'hunt'

@description('Public FQDN of the challenge3-search Container App (the SRV target).')
param searchFqdn string

@description('Public FQDN of the Ard.Artifacts Container App (the custom-domain CNAME target).')
param artifactsFqdn string

@description('customDomainVerificationId of the Ard.Artifacts Container App (az containerapp show --query properties.customDomainVerificationId).')
param artifactsCustomDomainVerificationId string

var isApex = hostLabel == '@'
var huntFqdn = isApex ? zoneName : '${hostLabel}.${zoneName}'
var catalogRecordName = isApex ? '_catalog._agents' : '_catalog._agents.${hostLabel}'
var searchRecordName = isApex ? '_search._agents' : '_search._agents.${hostLabel}'
var asuidRecordName = isApex ? 'asuid' : 'asuid.${hostLabel}'

resource zone 'Microsoft.Network/dnsZones@2018-05-01' = {
  name: zoneName
  location: 'global'
}

// --- ARD discovery records ---

// Challenge 2: TXT pointing at the challenge-2 manifest served by Ard.Artifacts at /c2/ai-catalog.json.
resource catalogTxt 'Microsoft.Network/dnsZones/TXT@2018-05-01' = {
  parent: zone
  name: catalogRecordName
  properties: {
    TTL: 3600
    TXTRecords: [
      { value: [ 'url=https://${huntFqdn}/c2/ai-catalog.json' ] }
    ]
  }
}

// Challenge 3: SRV pointing at the search service (HTTPS :443, POST /search at the root).
resource searchSrv 'Microsoft.Network/dnsZones/SRV@2018-05-01' = {
  parent: zone
  name: searchRecordName
  properties: {
    TTL: 3600
    SRVRecords: [
      { priority: 0, weight: 0, port: 443, target: searchFqdn }
    ]
  }
}

// --- Custom-domain binding for Ard.Artifacts (serves the well-known catalog + cards at the hunt root) ---

// Ownership proof for the Container Apps managed certificate.
resource asuidTxt 'Microsoft.Network/dnsZones/TXT@2018-05-01' = {
  parent: zone
  name: asuidRecordName
  properties: {
    TTL: 3600
    TXTRecords: [
      { value: [ artifactsCustomDomainVerificationId ] }
    ]
  }
}

// Subdomain hunts point the host at the container app via CNAME. An apex hunt (hostLabel "@") cannot
// use a CNAME — add an A record to the Container Apps environment static IP instead (see docs/SELFHOST.md).
resource artifactsCname 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = if (!isApex) {
  parent: zone
  name: hostLabel
  properties: {
    TTL: 3600
    CNAMERecord: {
      cname: artifactsFqdn
    }
  }
}

@description('Delegate your domain by pointing the registrar NS records at these Azure name servers.')
output nameServers array = zone.properties.nameServers

@description('The full hunt domain the client should seed from.')
output huntDomain string = huntFqdn
