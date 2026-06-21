# Lab 04 — MCP Apps (read the award UI resource)

⏱️ ~30 min · 🎯 **Goal:** understand the **MCP Apps** extension and pull the challenge-3 award out of the server — both its structured code and its HTML UI resource.

You have all three codes. Now you'll discover that challenge 3 is special: its tool is an **MCP App** — it carries an interactive UI, not just text.

---

## 🧠 Understand

### What MCP Apps adds

[MCP Apps](https://modelcontextprotocol.io/docs/extensions/apps) (SEP-1865, extension id `io.modelcontextprotocol/ui`) lets a tool return a **rendered HTML component** instead of plain text — surfaced *inside* the conversation. Three pieces wire it together:

1. **Tool `_meta`** advertises a UI resource:
   ```jsonc
   "_meta": { "ui": { "resourceUri": "ui://challenge-three/award.html" },
              "ui/resourceUri": "ui://challenge-three/award.html" }
   ```
2. **`outputSchema`** describes the structured result the UI is fed:
   ```jsonc
   "outputSchema": { "type":"object", "properties": { "code":{"type":"string"}, "message":{"type":"string"} },
                     "required":["code","message"] }
   ```
3. The **UI resource** itself, served via MCP's `resources/*` methods with mime type `text/html;profile=mcp-app`.

So the same `tools/call` you already do returns **both** a text fallback *and* `structuredContent`:

```jsonc
{ "content": [ { "type":"text", "text":"For this one, you need MCP Apps..." } ],
  "structuredContent": { "code":"1337 h4x0r",
                         "message":"Congrats, you solved the Agentic Resource Discovery (ARD) challenge!" } }
```

A non-Apps client shows the text. An Apps-capable host renders the trophy. **Progressive enhancement** — one response degrades gracefully: a plain client uses the text, a rich host uses the structured data + UI, and neither needs a separate tool.

### Reading the UI resource

Two more MCP methods (same client, same transport):

```
resources/list  → [ { uri: "ui://challenge-three/award.html", mimeType: "text/html;profile=mcp-app" } ]
resources/read  { uri }  → contents[0].text  ← the full self-contained HTML SPA (~344 KB)
```

The HTML has the trophy, headline, and the code **server-rendered into the markup** (`value="1337 h4x0r"`). The theme and the greeting come later, from the host (Lab 05).

---

## 🛠️ Build it

> 💬 **Prompt 1 — resource methods.**
> *"Extend `McpHttpClient` with `ListResourcesAsync()` (`resources/list`) and `ReadResourceTextAsync(uri)` (`resources/read`) that returns `contents[0].text`. Same SSE + UTF-8 handling as the other calls."*

> 💬 **Prompt 2 — read structured output + the award.**
> *"After calling `reveal_challenge_three`, read `structuredContent.code` and `.message`. Then `resources/list`, find the resource whose uri starts with `ui://`, `resources/read` it, and save the HTML to `ard-output/award.html` as UTF-8 (no BOM)."*

---

## 🔬 Inspect what you got

- [ ] Open the saved `award.html`. Find `id="completion-code"` — is `value="1337 h4x0r"` baked in?
- [ ] Search it for `--mcp-ui-color-` — note the CSS variables with **fallback** values (e.g. `var(--mcp-ui-color-accent,#ffd24a)`). These are what the host overrides in Lab 05.
- [ ] Find `player-greeting` and `player-name` — the interactive bits the host/user drive.
- [ ] Did you read the code from `structuredContent` (robust) or re-parse the text? Prefer structured.

---

## ✅ Checkpoint

```powershell
dotnet run --project src/Ard.Walker -- award
```

Expected:

```
code: 1337 h4x0r
message: Congrats, you solved the Agentic Resource Discovery (ARD) challenge!
award HTML (~325,000 chars) saved: …/award.html
```

Open `award.html` in a browser — you'll see the trophy and code, but **un-themed** (it falls back to its default dark CSS). Theming + your name is exactly what Lab 05 adds by acting as a *host*.

---

## ⚠️ Gotchas

- **Mojibake on the trophy 🏆 / em-dashes** — the classic UTF-8 trap. The SSE body is UTF-8; if you saved it via a default-encoding string you'll get `Ã°Å¸ÂÆ' ` garbage. Read **bytes**, decode **UTF-8**, write with `UTF8Encoding(false)`.
- **Huge payload** — the resource is ~344 KB in a single SSE `data:` line. Your reassembly must handle one very long line (don't truncate).
- **Hard-coding the uri** — read it from `resources/list` rather than assuming `ui://challenge-three/award.html`.

---

## 🤔 Understand-it questions

1. The tool returns *both* text and `structuredContent`. How does this single design let it serve a dumb terminal **and** a rich host without two different tools?
2. The completion code is baked into the HTML *and* available in `structuredContent`. Why might a server send it both ways?
3. `resources/read` returns a fully self-contained SPA (no external scripts). Why is "self-contained" important for something a host will run in a **sandbox** — an isolated iframe with no access to the host page, covered in Lab 05?

---

## 📂 Reference

- [`src/Ard.Core/McpHttpClient.cs`](../src/Ard.Core/McpHttpClient.cs) — `ListResourcesAsync`, `ReadResourceTextAsync`.
- [`src/Ard.Core/HuntRunner.cs`](../src/Ard.Core/HuntRunner.cs) — `FetchAwardAsync`, `GetStructuredCode`, `FindUiResourceUri`.
- A captured copy to study: [`artifacts/captured/award.html`](../artifacts/captured/award.html).

➡️ **Next: [Lab 05 — Render it](05-render-and-screenshot.md)** — become the host and make the trophy real.
