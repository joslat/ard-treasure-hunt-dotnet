# Run your own treasure hunt

This repo doesn't just *solve* Andreas Adner's hunt — it can **host one**. The four
challenge servers are vendored under [`servers/`](../servers) (his work, see [`NOTICE`](../NOTICE)),
and a small amount of .NET glue lets you stand up the whole chain — three MCP servers, a
search registry, the well-known catalog, the MCP cards, and the DNS records — either
**fully locally for free** or **on your own Azure subscription**.

---

## 🖥️ Fully local (`$0`, no domain, no cloud) — via .NET Aspire

One command starts everything and gives you a dashboard. The tricky part of ARD —
public DNS + a `.well-known` at a domain root — is handled locally by two tiny .NET
services so nothing leaves your machine.

```
 Ard.AppHost (Aspire)
   ├─ challenge1-mcp        (Andreas's TS server — well-known leg)
   ├─ challenge2-mcp        (Andreas's TS server — DNS-TXT leg)
   ├─ challenge3-mcp        (Andreas's TS server — the MCP App award)
   ├─ challenge3-search     (Andreas's TS server — POST /search)
   ├─ Ard.Artifacts (.NET)  → serves /.well-known/ai-catalog.json + the MCP cards,
   │                          generated from Aspire's resolved endpoints (no hard-coded ports)
   ├─ Ard.MockDoH   (.NET)  → answers the dns.google JSON shape for the
   │                          _catalog TXT + _search SRV records
   └─ walker        (.NET)  → the existing solver, pointed at the local stack
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node 20+](https://nodejs.org).
(No Aspire workload or templates needed — the AppHost is an SDK-style project restored from NuGet on build.)

```powershell
# 1. one-time: build the vendored TS servers
./scripts/setup-local.ps1

# 2. start the whole hunt + dashboard
dotnet run --project src/Ard.AppHost
```

Open the dashboard URL it prints, then press **Start** on the `walker` resource — it walks
all three discovery mechanisms against the local stack and prints the three codes
(`Rip and tear!` · `Sean Astrakhan` · `1337 h4x0r`). Everything is `http://localhost:*`;
the local-mode wiring (the `ARD_SCHEME=http`, `ARD_DOH_RESOLVER`, and `ARD_SEED_DOMAIN` environment variables) is injected into the walker by the
AppHost via service discovery.

> **How the local DNS works:** real ARD uses DNS-over-HTTPS to public resolvers. `Ard.MockDoH`
> mimics the `dns.google /resolve` JSON API and serves the `_catalog._agents` TXT and
> `_search._agents` SRV records pointing at the local services — so the *protocol* is faithful
> (real MCP/SSE, real catalog/card JSON, real DoH shape); only the DNS root of trust is local.

---

## ☁️ On your own Azure subscription — via `azd` + Bicep

The **same AppHost** deploys to **Azure Container Apps** (scale-to-zero → **~$0 idle**). The four TS
servers and `Ard.Artifacts` ship as containers; the local-only `Ard.MockDoH` + `walker` are excluded
from the published app (Azure uses **real** DNS). `Ard.Artifacts` serves the well-known catalog + the
MCP cards, and you bind **your custom domain** to it so `https://{domain}/.well-known/ai-catalog.json`
resolves. A small [`infra/dns.bicep`](../infra/dns.bicep) provisions the **Azure DNS zone** with the
`_catalog` TXT and `_search` SRV records (plus the custom-domain records).

```
 Azure Container Apps (scale-to-zero)        Azure DNS zone ({domain})
   ├─ challenge1-mcp   (external ingress)       _catalog._agents  TXT → https://{domain}/c2/ai-catalog.json
   ├─ challenge2-mcp   (external ingress)       _search._agents   SRV → {search-app}.azurecontainerapps.io:443
   ├─ challenge3-mcp   (external ingress)       asuid.{host}      TXT → (cert ownership proof)
   ├─ challenge3-search(external ingress)       {host}            CNAME → {artifacts-app}.azurecontainerapps.io
   └─ Ard.Artifacts    (← your custom domain; serves /.well-known + /cards + /c2)
```

### Prerequisites
- The [**.NET 10 SDK**](https://dotnet.microsoft.com/download) (`azd up` runs `dotnet` locally to generate the Aspire manifest and publish the `Ard.Artifacts` image), [`azd`](https://aka.ms/azd) + [`az`](https://aka.ms/azcli) + a **running Docker daemon** (the TS servers ship via `.PublishAsDockerFile()`, so `azd up` builds their images locally), and an Azure subscription.
- **A domain you own** (or a subdomain, e.g. `hunt.example.com`) that you can **delegate to Azure DNS**.
  This is the one step no IaC can do for you — your registrar controls the NS records.

### Deploy (one script)
```powershell
az login ; azd auth login
./scripts/deploy-azure.ps1 -ZoneName example.com -HostLabel hunt -Location swedencentral
```
The script runs `azd up` (which creates the resource group, Container Apps environment, registry, and the
5 container apps from zero), provisions `infra/dns.bicep` (the DNS zone + records), then prints the **four
Azure name servers** to set at your registrar.

Then — the **one human step** — delegate `example.com` to those name servers at your registrar. Once
delegation has propagated and the records resolve, run the **second script** to bind the custom domain +
free managed cert (it polls until the binding is secured):
```powershell
./scripts/bind-domain.ps1 -ZoneName example.com -HostLabel hunt
```

### Solve your own hunt
```powershell
dotnet run --project src/Ard.Walker -- --domain hunt.example.com
```
Real `https`, real public DNS-over-HTTPS, real MCP — your infrastructure end to end.

> **First run resolves empty?** Public resolvers (`dns.google`/`cloudflare-dns.com`) negative-cache a name
> that previously didn't exist. Azure publishes the new `_catalog`/`_search` records within ~60s, but give
> the public DoH resolvers a few minutes to pick them up before the first walk.

### Cost & teardown
Scale-to-zero Container Apps are **~$0 when idle**; the Azure DNS zone is ~$0.50/mo. Tear everything down — delete the DNS zone first, since `azd down` removes the whole resource group it lives in:
```powershell
az network dns zone delete -g <rg> -n example.com --yes
azd down --force --purge
```

### Honest caveats (the IaC was authored to Azure's documented patterns + validated by `bicep build` / `azd infra generate`, but not live-deployed here)
- **Apex vs subdomain** is handled automatically: a subdomain hunt (`-HostLabel hunt`, the default) provisions
  a CNAME; an apex hunt (`-HostLabel "@"`) provisions an **A record** to the Container Apps environment static IP
  (the script fetches it and the bicep fails clearly if it's missing).
- Container app **resource names/outputs** can vary by `azd` version; the script resolves them by name filter and
  asserts exactly one match — if a lookup throws, adjust the `az containerapp list` filter in `Get-AppName`.
