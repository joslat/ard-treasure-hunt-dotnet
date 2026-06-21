# Lab 06 — Wire it up (orchestration + `mcp.json` + clients)

⏱️ ~30 min · 🎯 **Goal:** collapse the whole hunt into one command, and make the three servers usable from Claude, VS Code, and Visual Studio.

You've earned all three codes and rendered the trophy. This lab is about **packaging**: an orchestrator that walks the trail unattended, and the config files that let real AI clients connect.

---

## 🧠 Understand

### Orchestration — one seed, full traversal

So far each leg is its own bit of walker code. The reference factors the shared pattern into a `HuntRunner` that takes **only the seed domain** and returns a structured `HuntReport`:

```
RunAsync("nullpointer.se")
  ├─ SolveChallengeOne   (well-known)  → ChallengeResult { mechanism, endpoint, tool, code, … }
  ├─ SolveChallengeTwo   (DNS TXT)      → ChallengeResult
  └─ SolveChallengeThree (DNS SRV→search→MCP App) → ChallengeResult { + awardHtml }
```

The win is the **shared tail**: `SolveFromEntryUrlAsync(cardUrl)` does *card → connect → tools/list → call → extract code* once, and challenges 1 and 2 both call it. Only the **discovery head** differs per challenge — which is the whole lesson of ARD in one method layout.

### `mcp.json` — letting *other* clients connect

Your code connects programmatically. But the same three servers can be dropped into any MCP host via config. **The schema differs by client:**

| Client | File | Key | Remote HTTP? |
|--------|------|-----|--------------|
| VS Code | `.vscode/mcp.json` | `servers` | ✅ `{ "type":"http", "url":"…" }` |
| Visual Studio | `mcp.json` (solution) | `servers` | ✅ same |
| Claude Code (CLI) | `.mcp.json` | `mcpServers` | ✅ `{ "type":"http", "url":"…" }` |
| Claude Desktop | `claude_desktop_config.json` | `mcpServers` | ⚠️ **no native HTTP** — needs the `mcp-remote` bridge, or use the **Connectors UI** |

> **Claude Desktop reality:** its config file only validates **stdio** servers, so a remote URL must be wrapped: `{ "command":"npx", "args":["-y","mcp-remote","https://…/mcp"] }`. The *preferred* path is the UI: **Settings → Connectors → Add custom connector → paste the URL** (skip OAuth — these are no-auth).

---

## 🛠️ Build it

> 💬 **Prompt 1 — the orchestrator.**
> *"In `Ard.Core`, add `HuntRunner(HttpClient, Action<string>? log)` with `RunAsync(seedDomain)` that solves all three challenges and returns a `HuntReport` (list of `ChallengeResult` with number, mechanism, discovery steps, endpoint, tool, code, message; challenge 3 also carries the award HTML). Factor the shared 'card → connect → call → extract code' tail into one private method that challenges 1 and 2 reuse. Extract the code from `structuredContent.code` or a regex on the text."*

> 💬 **Prompt 2 — make `walk` the default command.**
> *"In `Ard.Walker`, make the default command run `HuntRunner.RunAsync`, print a summary table of the three codes, and save `hunt-report.json` + `award.html` to an output dir."*

> 💬 **Prompt 3 — config + an `mcp.json` reader.**
> *"Create `mcp.json` (and `.vscode/mcp.json`) in the `servers` schema, `.mcp.json` in the `mcpServers` schema, and `config/claude_desktop_config.json` using the `mcp-remote` bridge — all listing the three challenge endpoints. Then add `McpConfig.Load(path)` to `Ard.Core` that reads either schema, and a `servers` command to the walker that connects to each configured server and calls its tools."*

---

## 🔬 Inspect what you got

- [ ] Does `HuntRunner` truly take **only the seed**, with no hard-coded `azurewebsites`/blob URLs in the challenge legs? (Everything should flow from discovery.)
- [ ] Do challenges 1 and 2 call the **same** shared tail method?
- [ ] Does `McpConfig.Load` accept **both** `servers` and `mcpServers`?
- [ ] Are the endpoints in all four config files identical?

---

## ✅ Checkpoint

```powershell
# One command, whole hunt:
dotnet run --project src/Ard.Walker
#   Challenge 1 · Well-Known URI            code : Rip and tear!
#   Challenge 2 · DNS TXT → manifest        code : Sean Astrakhan
#   Challenge 3 · DNS SRV → search → MCP App code : 1337 h4x0r

# Connect to everything in mcp.json:
dotnet run --project src/Ard.Walker -- servers --config mcp.json
```

Then add a server to a real client and call `reveal_challenge_three` from chat:

- **Claude Code:** the repo's `.mcp.json` is auto-detected — run `claude` here and ask it to call the tool.
- **VS Code:** `.vscode/mcp.json` is auto-detected — Start the server, accept trust, use Copilot Chat (agent mode). For the trophy to render as a real MCP App, use **VS Code Insiders** with `chat.mcp.apps.enabled`.
- **Claude Desktop:** Settings → Connectors → Add custom connector → paste `https://ard-b0a72356268a4fae.azurewebsites.net/mcp`.

---

## ⚠️ Gotchas

- **Wrong key per client** — `servers` (VS Code/VS) vs `mcpServers` (Claude). Using the wrong one means the client sees no servers.
- **Claude Desktop + a bare URL** — it won't work directly; use `mcp-remote` or the Connectors UI. On Windows the bridge may need `"command":"npx.cmd"`.
- **Trust prompts** — editors re-prompt to trust a server whenever its config or tool list changes (anti-"rug-pull"). Expected.

---

## 🤔 Understand-it questions

1. `HuntRunner` factors a shared tail used by challenges 1 & 2. Restate, in one sentence, what's *common* across all ARD mechanisms and what's *unique* to each.
2. Why do VS Code and Claude use different top-level keys for essentially the same data? What does that suggest about the maturity of the MCP-config ecosystem in 2026?
3. You connected to these servers three ways: your own client, the walker's `servers` command, and a real AI host. What does ARD + MCP give you that makes a server this *portable* across clients?

---

## 🎓 You're done — what you can now do

- Explain **ARD's three discovery mechanisms** and when each is used.
- Speak **MCP over streamable-HTTP** by hand (SSE, dual `Accept`, UTF-8, stateless).
- Query an **ARD registry** and rank results.
- Read an **MCP Apps** UI resource and host it (handshake, theme, sandbox).
- Capture a rendered component to **PNG**, and wire servers into real **AI clients**.

### 🚀 Stretch goals

- **Swap the thin client for the official `ModelContextProtocol` C# SDK** and compare ergonomics.
- **Add an agent loop:** plug the three tools into `Microsoft.Extensions.AI` so an LLM walks the trail conversationally and ends with the rendered award.
- **Cross-platform render:** redo Lab 05 with Playwright for .NET so it runs on macOS/Linux.
- **Harden discovery:** add the two unused ARD mechanisms (HTML `<link rel="ai-catalog">` and `robots.txt` `Agentmap:`).
- **Make it a chat host:** build an "ARD Explorer Chat" — a chat UI that is itself an MCP Apps host, so the award renders *inline* in the conversation.

---

## 📂 Reference

- [`src/Ard.Core/HuntRunner.cs`](../src/Ard.Core/HuntRunner.cs) — `RunAsync`, `SolveFromEntryUrlAsync` (the shared tail).
- [`src/Ard.Core/McpConfig.cs`](../src/Ard.Core/McpConfig.cs) — both-schema loader.
- [`mcp.json`](../mcp.json) · [`.vscode/mcp.json`](../.vscode/mcp.json) · [`.mcp.json`](../.mcp.json) · [`config/claude_desktop_config.json`](../config/claude_desktop_config.json)

⬅️ Back to the [workshop index](README.md) · 📚 Links & credit: [`docs/REFERENCES.md`](../docs/REFERENCES.md)
