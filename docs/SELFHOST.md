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

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), [Node 20+](https://nodejs.org),
and the Aspire templates (`dotnet new install Aspire.ProjectTemplates`).

```powershell
# 1. one-time: build the vendored TS servers
./scripts/setup-local.ps1

# 2. start the whole hunt + dashboard
dotnet run --project src/Ard.AppHost
```

Open the dashboard URL it prints, then press **Start** on the `walker` resource — it walks
all three discovery mechanisms against the local stack and prints the three codes
(`Rip and tear!` · `Sean Astrakhan` · `1337 h4x0r`). Everything is `http://localhost:*`;
the client's `--local` wiring (an `http` scheme + the mock-DoH resolver) is injected by the
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
- [`azd`](https://aka.ms/azd) + [`az`](https://aka.ms/azcli) + Docker, and an Azure subscription.
- **A domain you own** (or a subdomain, e.g. `hunt.example.com`) that you can **delegate to Azure DNS**.
  This is the one step no IaC can do for you — your registrar controls the NS records.

### Deploy (one script)
```powershell
az login ; azd auth login
./scripts/deploy-azure.ps1 -ZoneName example.com -HostLabel hunt -Location swedencentral
```
The script runs `azd up` (compute), provisions `infra/dns.bicep` (DNS), then prints:
1. the **four Azure name servers** to set at your registrar (one-time delegation), and
2. the two commands to **bind your custom domain + a free managed cert** once delegation has propagated:
   ```powershell
   az containerapp hostname add  -g <rg> -n <artifacts-app> --hostname hunt.example.com
   az containerapp hostname bind -g <rg> -n <artifacts-app> --hostname hunt.example.com --environment <env> --validation-method CNAME
   ```

### Solve your own hunt
```powershell
dotnet run --project src/Ard.Walker -- --domain hunt.example.com
```
Real `https`, real public DNS-over-HTTPS, real MCP — your infrastructure end to end.

### Cost & teardown
Scale-to-zero Container Apps are **~$0 when idle**; the Azure DNS zone is ~$0.50/mo. Tear everything down with:
```powershell
azd down --force --purge
az network dns zone delete -g <rg> -n example.com
```

### Honest caveats (verify on your first deploy — this was authored, not live-deployed here)
- **Apex domains** (`-HostLabel "@"`) can't use a CNAME; add an **A record** to the Container Apps
  environment static IP instead (`az containerapp env show ... --query properties.staticIp`). Subdomains
  (the default) use the CNAME the script provisions.
- The MCP **card URLs** are built from the resolved container endpoints — confirm they come out as the
  **public** `*.azurecontainerapps.io` FQDNs (the servers use external ingress) so an external client can reach them.
- Container app **resource names/outputs** vary by `azd` version; the script looks them up by name filter —
  adjust the `az containerapp list` filters if a lookup returns empty.
