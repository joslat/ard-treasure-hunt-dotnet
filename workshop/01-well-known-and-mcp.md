# Lab 01 — Speak MCP (Well-Known URI → code #1)

⏱️ ~45 min · 🎯 **Goal:** discover the first server from the well-known catalog, build a minimal MCP client, and call its tool to earn **`Rip and tear!`**.

This is the biggest lab — it builds the **MCP client** that every later lab reuses. Take your time here.

---

## 🧠 Understand

### Step A — Well-Known URI discovery

The simplest ARD mechanism: the manifest lives at a fixed path on the publisher's domain.

```
GET https://nullpointer.se/.well-known/ai-catalog.json
→ entries[0].url  →  GET that URL  →  an MCP card (application/mcp-server+json)
```

The **catalog** points at a **card**; the card contains the actual connection info:

```jsonc
{
  "type": "application/mcp-server+json",
  "endpoint": { "url": "https://ard-281f1ff05c2d4870.azurewebsites.net/mcp",
                "transport": "streamable-http" },
  "tools": [ { "name": "reveal_challenge_one", "description": "Returns the challenge-1 solution text." } ]
}
```

So discovery is a **two-hop fetch**: catalog → card → endpoint.

### Step B — MCP over streamable-HTTP (the wire protocol)

You POST JSON-RPC 2.0 messages to the endpoint. In JSON-RPC, a message with an `id` is a **request** (it expects a reply); a message with no `id` is a **notification** (fire-and-forget) — that's why `notifications/initialized` below carries no id. The sequence:

| # | Method | id? | Purpose |
|---|--------|-----|---------|
| 1 | `initialize` | yes | negotiate protocol/capabilities |
| 2 | `notifications/initialized` | no | tell the server you're ready |
| 3 | `tools/list` | yes | discover tools |
| 4 | `tools/call` | yes | call `reveal_challenge_one` |

An `initialize` request body looks like:

```jsonc
{ "jsonrpc": "2.0", "id": 1, "method": "initialize",
  "params": { "protocolVersion": "2025-06-18", "capabilities": {},
              "clientInfo": { "name": "ard-client", "version": "1.0.0" } } }
```

**Three rules that will bite you if you miss them** (and are the whole point of this lab):

1. The `Accept` header **must contain both** `application/json` **and** `text/event-stream`. Send only one and you get `-32000 Not Acceptable`.
2. Responses come back as **Server-Sent Events**: a line `event: message` then a line `data: {…json-rpc…}`. You must strip the `data:` prefix and parse the rest.
3. These servers are **stateless** — they don't issue an `Mcp-Session-Id`. Don't block waiting for one.

A raw response looks like this — an `event:` line you ignore, and a `data:` line whose 5-char prefix you strip before parsing the rest as JSON-RPC:

```
event: message                              ← SSE line: ignore
data: {"result":{"content":[{"type":"text","text":"…Completion code: \"Rip and tear!\"…"}]},"jsonrpc":"2.0","id":99}
└──┬─┘ strip this prefix, then JsonDocument.Parse the rest
```

---

## 🛠️ Build it

You'll write two things in `Ard.Core`: an **MCP client** and a tiny **ARD resolver**, then call them from `Ard.Walker`.

> 💬 **Prompt 1 — the MCP client.**
> *"In `Ard.Core`, add `McpHttpClient` taking an `HttpClient` and an endpoint URL. Implement streamable-HTTP MCP (JSON-RPC 2.0): methods `InitializeAsync`, `ListToolsAsync`, `CallToolAsync(name, args)`. For every POST set `Accept: application/json, text/event-stream`. Read the response as raw bytes and decode UTF-8. If the content-type is `text/event-stream`, reassemble the JSON from the `data:` lines, then `JsonDocument.Parse`. Use an incrementing id; send `notifications/initialized` (no id) after initialize. Don't require an `Mcp-Session-Id`. Return the `result` element; throw on a JSON-RPC `error`."*

> 💬 **Prompt 2 — well-known discovery.**
> *"In `Ard.Core`, add `ArdResolver(HttpClient)` with a shared `FetchCatalogAsync(url)` that GETs and deserializes an `ai-catalog.json`, a `ResolveWellKnownAsync(domain)` that builds `https://{domain}/.well-known/ai-catalog.json` and calls `FetchCatalogAsync`, and a `FetchCardAsync(url)` that GETs an MCP card. Add small DTOs (`AiCatalog`, `CatalogEntry`, `McpServerCard`, `McpEndpoint`). Use case-insensitive, camelCase JSON options."*

> 💬 **Prompt 3 — wire it up in the walker.**
> *"In `Ard.Walker`, resolve the well-known catalog for `nullpointer.se`, fetch the first entry's card, connect with `McpHttpClient`, run initialize + tools/list, call the first tool, and print the result text."*

---

## 🔬 Inspect what you got

Before running, **read the generated `McpHttpClient`** and check the three rules:

- [ ] Does it add **both** media types to `Accept`? (Search for `text/event-stream`.)
- [ ] Does it decode bytes as **UTF-8** explicitly, not rely on the default? (Look for `Encoding.UTF8`.)
- [ ] Does it **parse SSE** — find lines starting with `data:` and strip the prefix?
- [ ] If your agent reached for the official `ModelContextProtocol` SDK instead — that's valid! But for this lab, building the thin client teaches you what the SDK hides. You can always swap later.

---

## ✅ Checkpoint

Run *your* minimal walker from Prompt 3 (it solves just challenge 1 and prints the tool's result text):

```powershell
dotnet run --project src/Ard.Walker
```

Expected — the result text contains the code in quotes:

```
…Completion code: "Rip and tear!" … Hint for the next ones: DNS...
```

🔑 **Code #1: `Rip and tear!`** — and the hint points you at **DNS** (Lab 02).

> ℹ️ The *finished* `src/Ard.Walker` in this repo walks all three legs at once and prints a per-challenge **summary table of codes** (the full hint text goes to `ard-output/hunt-report.json`, not stdout). You'll build that orchestration in Lab 06 — for now, your own single-leg walker prints the text above.

Sanity-check the wire yourself with raw HTTP:

```powershell
curl -X POST https://ard-281f1ff05c2d4870.azurewebsites.net/mcp `
  -H "Content-Type: application/json" `
  -H "Accept: application/json, text/event-stream" `
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"c","version":"1"}}}'
```

You'll see the `event: message` / `data:` framing with your own eyes.

---

## ⚠️ Gotchas

- **`-32000 Not Acceptable`** → your `Accept` header is missing one of the two media types. This is the #1 mistake.
- **`JsonException` on the response** → you tried to parse the raw SSE text as JSON. Strip the `data:` prefix first.
- **Mojibake later** (em-dashes/emoji) → you let `HttpContent.ReadAsStringAsync` guess the encoding. Read bytes, `Encoding.UTF8.GetString`.
- **Azure cold start** → the first call to a sleeping server can take a few seconds; give your `HttpClient` a generous timeout (e.g. 90s).

---

## 🤔 Understand-it questions

1. Why does MCP send `notifications/initialized` *without* an `id`, while `initialize` has one? (What distinguishes a JSON-RPC notification from a request?)
2. The completion code arrives inside `content[0].text`. Challenge 3 will instead use a `structuredContent` field. **Why** might a server prefer structured output over parsing text?
3. You extracted the code with a regex on the text. What's fragile about that, and how would `structuredContent` fix it?

---

## 📂 Reference

- [`src/Ard.Core/McpHttpClient.cs`](../src/Ard.Core/McpHttpClient.cs) — note the `ExtractJson` SSE reassembly and the dual-`Accept` headers.
- [`src/Ard.Core/ArdResolver.cs`](../src/Ard.Core/ArdResolver.cs) — `FetchCatalogAsync`, `ResolveWellKnownAsync`, `FetchCardAsync`.
- [`src/Ard.Core/Models.cs`](../src/Ard.Core/Models.cs) · [`src/Ard.Core/Json.cs`](../src/Ard.Core/Json.cs)

➡️ **Next: [Lab 02 — Discovery via DNS](02-dns-txt.md)** — follow the "DNS" hint.
