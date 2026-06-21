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

## ☁️ On your own Azure subscription — via `azd` + Bicep *(coming next)*

The same AppHost deploys to **Azure Container Apps** with `azd up` (scale-to-zero, ~$0 idle),
plus a small `dns.bicep` for the **Azure DNS zone + `_catalog` TXT + `_search` SRV** and an
**Azure Static Web App** serving the `.well-known` catalog + cards at your domain root.

The one manual step ARD can't automate: **own a domain and delegate it (or a subdomain) to
Azure DNS** (a one-time registrar NS change). Everything else is `azd up`.

*(The Azure path lands in a follow-up — this doc will gain the exact `azd` steps then.)*
