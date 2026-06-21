# 🧭 The ARD Treasure Hunt Workshop

> Build an Agentic Resource Discovery client from scratch — guided by an AI agent, but understanding every step.

You start with **one URL**. By the end you'll have walked a hidden trail across three discovery mechanisms, spoken a real wire protocol to three servers, rendered an emerging-spec UI component, and saved a trophy with your name on it — and you'll be able to *explain how all of it works*.

This is a **"vibe-coding-but-understanding"** workshop. You'll let an AI agent (Claude Code, GitHub Copilot, Cursor, …) generate each piece of code — that's the fast, modern way to build. But every lab then makes you **read, run, and verify** what the agent produced, and answer questions that prove you understand it. Generating code you can't explain is just gambling; this workshop teaches you to *drive*.

---

## Who this is for

C# developers who want a concrete, fun introduction to the **agentic web** stack:

- **ARD** — Agentic Resource Discovery (how agents *find* capabilities)
- **MCP** — the Model Context Protocol (how agents *talk to* them)
- **MCP Apps** — interactive UI inside an MCP conversation

No prior MCP/ARD knowledge is assumed. You should be comfortable with C#, `async/await`, HTTP, and JSON.

---

## The method: the learning loop

Every lab runs the same five-beat loop. Don't skip the inspect/verify/explain beats — that's where the learning is.

```
   🧠 UNDERSTAND  →  🛠️ PROMPT  →  🔬 INSPECT  →  ✅ VERIFY  →  💬 EXPLAIN
   read the         ask your AI    read the      run it &      answer the
   concept          to build it    code it       check the     "understand-it"
   first                           wrote         output        questions
```

1. **🧠 Understand** — read the *Concept* section so you know what you're asking for.
2. **🛠️ Prompt** — feed the suggested prompt to your agent (tweak it; make it yours).
3. **🔬 Inspect** — open what it generated and read it. Does it match the concept?
4. **✅ Verify** — run the *Checkpoint*. Did you get the expected code/output?
5. **💬 Explain** — answer the *Understand-it* questions out loud or in writing.

---

## Prerequisites

- **.NET 9 SDK** or newer — check with `dotnet --version`.
- An **AI coding agent** in your editor/terminal.
- **WebView2 Runtime** (only for Lab 05; pre-installed on Windows 11).
- Internet access — the three challenge servers are live on Azure.

> **Spoiler discipline.** This repo's [`src/`](../src/) contains the *full solution* — treat it as the **answer key**: try each lab first, then compare. Each lab links the exact reference file at the bottom. For the (non-spoiler) spec and background links, see [`docs/REFERENCES.md`](../docs/REFERENCES.md).

---

## The labs

| Lab | You build | Outcome | Core concept |
|-----|-----------|---------|--------------|
| [**00**](00-setup-and-primer.md) — Setup & primer | your project + dev loop | builds & runs | What ARD/MCP are; the trail |
| [**01**](01-well-known-and-mcp.md) — Speak MCP | well-known discovery + MCP client | 🔑 `Rip and tear!` | well-known URIs, JSON-RPC over SSE |
| [**02**](02-dns-txt.md) — Discovery via DNS | DNS TXT resolver | 🔑 `Sean Astrakhan` | DNS-over-HTTPS, manifest pointers |
| [**03**](03-dns-srv-registry.md) — Dynamic discovery | SRV + registry search | 🔑 `1337 h4x0r` (text) | SRV records, ARD registries |
| [**04**](04-mcp-apps-award.md) — MCP Apps | read the UI resource | the award HTML + structured code | `resources/*`, `_meta.ui` |
| [**05**](05-render-and-screenshot.md) — Render it | WebView2 host | 🏆 a **PNG** with your name | the MCP Apps host handshake |
| [**06**](06-mcp-json-and-orchestration.md) — Wire it up | `mcp.json` + orchestrator | one-command walk + editor configs | clients, end-to-end orchestration |

Each lab is **self-contained and cumulative**: by Lab 06 your code looks a lot like [`src/`](../src/). If you fall behind, you can copy the relevant reference file and continue.

---

## Two ways to run it

- **🟢 From scratch (recommended).** Start with an empty `src/` and build each project as the labs direct. Maximum learning.
- **🔵 Compare-and-contrast.** Read each lab, then study the matching reference file and explain *why* it's written that way. Faster; good for a 90-minute session.

---

## Facilitator notes (running it as a group)

- **Format.** Pairs work great — one "driver" prompts the agent, one "navigator" inspects and reads the concept aloud. Swap each lab.
- **Timing.** ~3 hours full; ~90 min for Labs 00–03 (all three codes) as a "lightning" version. Lab 05 (rendering) is the natural stretch goal.
- **Checkpoints.** Each completion code is a built-in, unambiguous checkpoint — have people drop their code in chat to keep pace synchronized.
- **Common blockers.** The `-32000 Not Acceptable` error (Lab 01), mojibake on the trophy emoji (Lab 04), and a white screenshot (Lab 05) are *deliberate teaching moments* — each lab's **Gotchas** section covers them.
- **Hardware.** Labs 00–04 are cross-platform. Lab 05 (WebView2) needs Windows; on macOS/Linux, substitute Playwright (the lab notes how) or treat it as a read-along.

---

## Glossary (one line each)

- **ARD** — a "phone book for agents": discover *which* resource can do a task, then connect over its native protocol.
- **`ai-catalog.json`** — the ARD manifest a publisher hosts; its `entries[]` point at resources.
- **MCP card** (`application/mcp-server+json`) — how to connect to one MCP server (endpoint + tools).
- **MCP** — JSON-RPC 2.0 between an agent and a server (tools, resources, prompts). Here it rides **streamable-HTTP**.
- **streamable-HTTP** — MCP over HTTP where responses may come back as Server-Sent Events.
- **MCP Apps** — an MCP extension (SEP-1865, `io.modelcontextprotocol/ui`) that lets a tool return an interactive HTML UI.

➡️ **Start here: [Lab 00 — Setup & primer](00-setup-and-primer.md)**
