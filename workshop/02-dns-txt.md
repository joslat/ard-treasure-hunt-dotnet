# Lab 02 — Discovery via DNS (TXT → code #2)

⏱️ ~30 min · 🎯 **Goal:** follow the "DNS" hint, resolve a TXT record to a manifest, and earn **`Sean Astrakhan`**.

You already have the MCP client from Lab 01. This lab only adds a new **discovery front-end**; the *connect → call tool* tail is identical. That repetition is the point — ARD swaps the discovery mechanism, MCP stays the same.

---

## 🧠 Understand

### DNS as a discovery channel

Instead of hosting a file at a well-known path, a publisher can publish a **DNS record**. ARD uses Service-Binding–style names under `_agents`:

- `_catalog._agents.{domain}` **TXT** → points at a static `ai-catalog.json` (this lab)
- `_search._agents.{domain}` **SRV** → points at a registry (Lab 03). You'll build `ResolveSrvAsync` now (same DoH call, just type 33) so Lab 03 can reuse it; its `priority weight port target` fields and base-URL derivation are explained there.

The TXT record carries a `url=` value:

```
_catalog._agents.nullpointer.se.  TXT  "url=https://…/54d334…/ai-catalog.json"
```

So the flow is: **resolve TXT → parse `url=` → fetch that catalog → (same as Lab 01) card → endpoint → tool.**

### DNS without `dig`: DNS-over-HTTPS

You don't need a DNS library. Public resolvers expose DNS over plain HTTPS (RFC 8484, JSON form):

```
GET https://dns.google/resolve?name=_catalog._agents.nullpointer.se&type=TXT
```

```jsonc
{ "Status": 0,
  "Answer": [ { "name": "_catalog._agents.nullpointer.se.", "type": 16, "TTL": 3600,
                "data": "url=https://…/ai-catalog.json" } ] }
```

Record-type numbers you'll need: **TXT = 16**, **SRV = 33**. Note the JSON keys here are **PascalCase** (`Status`, `Answer`, `TTL`) — different from ARD's camelCase. (Case-insensitive JSON options handle both.)

---

## 🛠️ Build it

> 💬 **Prompt 1 — DNS-over-HTTPS.**
> *"In `Ard.Core`, add `DnsOverHttps(HttpClient)` with `ResolveTxtAsync(name)` and `ResolveSrvAsync(name)`. Query `https://dns.google/resolve?name=…&type=16` (TXT) and `type=33` (SRV); fall back to `https://cloudflare-dns.com/dns-query` with `Accept: application/dns-json`. Parse the `Answer[].data` values. For SRV, parse `priority weight port target` into a record and add a helper that builds `https://{target}` (dropping a `:443`). Strip surrounding quotes from TXT data."*

> 💬 **Prompt 2 — the TXT discovery path.**
> *"In `ArdResolver`, add `ResolveDnsCatalogAsync(domain)`: TXT-resolve `_catalog._agents.{domain}`, find the value starting with `url=`, strip that prefix, and fetch + return the catalog (reuse `FetchCatalogAsync`)."*

> 💬 **Prompt 3 — earn code #2.**
> *"In `Ard.Walker`, add the challenge-2 leg: resolve the DNS catalog for `nullpointer.se`, take the first entry, fetch its card, connect, and call the tool. Print the result."*

---

## 🔬 Inspect what you got

- [ ] Does `ResolveTxtAsync` actually filter to the `data` field, and does the walker pick the value that **starts with `url=`** (not blindly take `Answer[0]`)?
- [ ] Does it strip the **quotes** TXT records are often wrapped in?
- [ ] Notice your challenge-2 code is *almost identical* to challenge-1 after discovery. Could you factor the shared "card → connect → call → extract code" tail into one method? (The reference does — see `HuntRunner.SolveFromEntryUrlAsync`.)

---

## ✅ Checkpoint

The `dns` toolkit command (if you added it) only **dumps the records** — it doesn't connect or emit a code:

```powershell
dotnet run --project src/Ard.Walker -- dns nullpointer.se     # prints the TXT + SRV records only
```

To earn the code, run your **challenge-2 leg** (or the full `walk`). Expected result text:

```
…Completion code: "Sean Astrakhan" … Hint: DNS again — but this time an SRV record pointing to a search endpoint...
```

🔑 **Code #2: `Sean Astrakhan`** — the hint now says **DNS SRV → search** (Lab 03).

Verify the DNS hop directly:

```powershell
curl "https://dns.google/resolve?name=_catalog._agents.nullpointer.se&type=TXT"
```

---

## ⚠️ Gotchas

- **Taking the wrong TXT record** — a name can have several TXT records (SPF, verification, …). Filter for the one starting with `url=`.
- **Trailing dot** in DNS names (`nullpointer.se.`) is normal — don't strip it from the *query*, but do `TrimEnd('.')` on an SRV **target** before building a URL.
- **Type numbers** — `16` is TXT, `33` is SRV. Easy to swap.

---

## 🤔 Understand-it questions

1. The well-known mechanism (Lab 01) ships a *file*; the TXT mechanism ships a *pointer to a file*. What does the extra layer of indirection buy a publisher?
2. DNS records have a TTL (here 3600s). What does that imply for how "live" ARD discovery is versus, say, editing a file?
3. Both DoH resolvers return the same data. Why might a robust client try **two** providers?

---

## 📂 Reference

- [`src/Ard.Core/DnsOverHttps.cs`](../src/Ard.Core/DnsOverHttps.cs) — `ResolveTxtAsync`, multi-chunk TXT handling.
- [`src/Ard.Core/ArdResolver.cs`](../src/Ard.Core/ArdResolver.cs) — `ResolveDnsCatalogAsync`.

➡️ **Next: [Lab 03 — Dynamic discovery](03-dns-srv-registry.md)** — from a record to a searchable registry.
