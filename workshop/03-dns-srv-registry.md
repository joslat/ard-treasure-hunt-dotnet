# Lab 03 — Dynamic discovery (DNS SRV → registry `/search` → code #3)

⏱️ ~35 min · 🎯 **Goal:** resolve an SRV record to a **registry**, query its `/search` API, and reach the third server for **`1337 h4x0r`**.

This is ARD's **dynamic** layer. The first two labs found a *file*; this one finds a *service* that searches across many resources.

---

## 🧠 Understand

### Static vs. dynamic discovery

| | Static (Labs 01–02) | Dynamic (this lab) |
|---|---|---|
| You discover… | a manifest file | a **registry endpoint** |
| Then you… | read `entries[]` | **`POST /search`** with a query |
| Trust anchor | domain ownership | the registry (which crawled manifests) |

### SRV → registry base URL

```
_search._agents.nullpointer.se.  SRV  0 0 443 ard-01943f89c6ed4fbc.azurewebsites.net.
```

SRV fields are `priority weight port target`. Here: port `443`, target `ard-01943f89c6ed4fbc.azurewebsites.net` → base URL `https://ard-01943f89c6ed4fbc.azurewebsites.net` (port 443 is implied by https, so drop it).

### The ARD registry search API (ARD §7)

```http
POST https://ard-01943f89c6ed4fbc.azurewebsites.net/search
Content-Type: application/json

{ "query": { "text": "treasure hunt challenge" }, "pageSize": 10 }
```

Response — ranked catalog entries, each with a relevance `score` (0–100):

```jsonc
{ "results": [ { "identifier": "urn:ai:nullpointer.se:server:challenge-three",
                 "type": "application/mcp-server+json",
                 "url": "https://…/52409d….mcp.json", "score": 100 } ],
  "referrals": [], "pageToken": null }
```

Then it's the familiar tail: top result → card → endpoint → `reveal_challenge_three`.

> **Reality check:** this registry only implements `POST /search`. `GET /agents` and `GET /` return *Cannot GET*. The `text` can be anything reasonably on-topic — the single challenge entry scores 100.

---

## 🛠️ Build it

You already wrote `ResolveSrvAsync` in Lab 02. Now use it.

> 💬 **Prompt 1 — registry discovery + search.**
> *"In `ArdResolver`, add `SearchAsync(registryBase, queryText, pageSize)` that POSTs `{ query:{ text }, pageSize }` to `{registryBase}/search` and deserializes `{ results[], referrals[], pageToken }` (each result has identifier/type/url/score). Then add `ResolveDnsRegistryAsync(domain, queryText)`: SRV-resolve `_search._agents.{domain}`, build the base URL from the first record, and call `SearchAsync`."*

> 💬 **Prompt 2 — earn code #3 (text form).**
> *"In `Ard.Walker`, add the challenge-3 leg: resolve the registry, take the highest-`score` result, fetch its card, connect, call the tool. Print the result — and also print `structuredContent.code` if present."*

---

## 🔬 Inspect what you got

- [ ] Does the SRV→URL helper **drop `:443`** but keep a non-standard port if one ever appears?
- [ ] Does the walker sort by **`score`** (descending) rather than assuming `results[0]`?
- [ ] Look at this tool's result: it has **both** a `content[].text` fallback *and* a `structuredContent` object `{ code, message }`. Which one did you read? (The structured one is the robust choice — see Lab 04.)

---

## ✅ Checkpoint

```powershell
dotnet run --project src/Ard.Walker        # if walk now does all three legs
```

Expected challenge-3 output:

```
structuredContent.code = "1337 h4x0r"
```

🔑 **Code #3: `1337 h4x0r`** — you've reached the treasure (in text form). Labs 04–05 turn that into the actual **trophy UI**.

Verify the registry directly:

```powershell
curl -X POST https://ard-01943f89c6ed4fbc.azurewebsites.net/search `
  -H "Content-Type: application/json" `
  -d '{"query":{"text":"treasure hunt challenge"},"pageSize":10}'
```

---

## ⚠️ Gotchas

- **Assuming `GET /agents` works** — it doesn't here. Only `POST /search` is implemented. Stick to the spec'd search call.
- **Ignoring `score`** — always pick the top-scored result; a registry can return several.
- **SRV parsing** — it's four space-separated fields; the target ends with a `.` you must trim.

---

## 🤔 Understand-it questions

1. Static discovery trusts the **domain**; dynamic discovery trusts the **registry**. What new risks does introducing a registry create, and how does the `score` + ranked results model help (or not)?
2. The query text `"treasure hunt challenge"` is fuzzy, yet it returns score 100. What does "semantic" search let an agent do that exact-match discovery can't?
3. All three mechanisms converge on the **same** "card → MCP → tool" tail. If you were designing ARD, why keep discovery and invocation cleanly separated like this?

---

## 📂 Reference

- [`src/Ard.Core/ArdResolver.cs`](../src/Ard.Core/ArdResolver.cs) — `ResolveDnsRegistryAsync`, `SearchAsync`.
- [`src/Ard.Core/Models.cs`](../src/Ard.Core/Models.cs) — `SearchRequest`/`SearchResponse`/`SearchResult`, `SrvRecord.ToBaseUrl()`.

➡️ **Next: [Lab 04 — MCP Apps](04-mcp-apps-award.md)** — the third tool isn't just text; it serves a UI.
