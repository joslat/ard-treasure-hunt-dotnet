# Lab 00 — Setup & primer

⏱️ ~20 min · 🎯 **Goal:** a working dev loop and a clear mental model of ARD + MCP before you write a line of code.

---

## 🧠 Understand: what are we even building?

### ARD — a phone book for agents

An AI agent that wants to *do* something needs to **find a capability** first. **Agentic Resource Discovery (ARD)** answers exactly one question — *"what resource is available for this task?"* — and then gets out of the way so you connect over that resource's own protocol (MCP, A2A, REST). It is **discovery, not execution**.

Two layers:

- **Static discovery** — a publisher hosts a JSON manifest (`ai-catalog.json`) on their own domain. *Domain ownership is the trust anchor.*
- **Dynamic discovery** — a **registry** crawls those manifests and exposes a searchable REST API (`POST /search`).

The key object is the **catalog entry** inside `ai-catalog.json → entries[]`:

```jsonc
{
  "identifier": "urn:ai:nullpointer.se:server:challenge-one",  // domain-anchored URN
  "type": "application/mcp-server+json",                        // what kind of resource
  "url": "https://…/76041….mcp.json"                           // pointer to the artifact (the "card")
}
```

ARD defines **four ways** a publisher can advertise that manifest. This hunt uses three of them — and each is one lab:

1. **Well-Known URI** — `https://{domain}/.well-known/ai-catalog.json` → **Lab 01**
2. **DNS** (TXT → static manifest) → **Lab 02**
3. **DNS** (SRV → dynamic registry `/search`) → **Lab 03**
4. *(HTML `<link>` and `robots.txt` `Agentmap:` exist too, but aren't used here.)*

### MCP — how you actually talk to the thing you found

Once ARD hands you an **MCP card**, you connect to its endpoint and speak the **Model Context Protocol**: JSON-RPC 2.0, here over **streamable-HTTP**. The handshake is always:

```
initialize → notifications/initialized → tools/list → tools/call
```

Each challenge server exposes exactly one tool — `reveal_challenge_*` — that returns a completion code and a hint to the next mechanism.

### The trail you're about to walk

```
nullpointer.se ──1. well-known──▶ MCP server #1 ─▶ "Rip and tear!"   (hint: DNS)
               ──2. DNS TXT────▶ MCP server #2 ─▶ "Sean Astrakhan"  (hint: DNS SRV → search)
               ──3. DNS SRV────▶ registry /search ─▶ MCP server #3 ─▶ "1337 h4x0r" + 🏆 MCP App
```

---

## 🛠️ Build it: project skeleton

You'll build a small solution with a **library** (the protocol) and a **console app** (the driver). Labs 01–03 only need these two; Lab 05 adds a WinForms app.

> 💬 **Prompt your agent:**
> *"Create a .NET 9 solution called `ARDChallenge` with two projects: a class library `Ard.Core` (net9.0) and a console app `Ard.Walker` (net9.0) that references it. Enable nullable and implicit usings. Put projects under `src/`. Make `Ard.Walker` print 'ARD walker ready'."*

If your tooling lacks a template (e.g. the `sln`/`winforms` templates aren't installed), have your agent **author the `.csproj` and `.slnx`/`.sln` files by hand** — they're a few lines each. (That's exactly what the reference solution does.)

---

## ✅ Checkpoint

```powershell
dotnet --version                       # 9.x or newer
dotnet build                           # 0 errors
dotnet run --project src/Ard.Walker    # → ARD walker ready
```

Also confirm you can reach the first clue (raw HTTP, no code yet):

```powershell
curl https://nullpointer.se/.well-known/ai-catalog.json
```

You should see a JSON document with one `entries[]` item whose `type` is `application/mcp-server+json`. **That single entry is the whole game's seed.**

---

## ⚠️ Gotchas

- **.NET 10 surprise:** `dotnet new sln` may produce an empty `ARDChallenge.slnx` (the new XML solution format) and not auto-add projects. If so, add the `<Project Path=…>` lines yourself.
- **No `winforms` template?** Don't fight it — hand-write the `.csproj` (you'll do this in Lab 05).

---

## 🤔 Understand-it questions

1. ARD is "discovery, not execution." After ARD gives you an MCP card, **what protocol** do you use to actually call the tool?
2. Why is **domain ownership** a reasonable trust anchor for static discovery?
3. The seed catalog entry has a `url` field. What do you expect to find when you GET it — and what *won't* be there (hint: the endpoint)?

---

## 📂 Reference

- Solution + project files: [`ARDChallenge.slnx`](../ARDChallenge.slnx), [`src/Ard.Core/Ard.Core.csproj`](../src/Ard.Core/Ard.Core.csproj), [`src/Ard.Walker/Ard.Walker.csproj`](../src/Ard.Walker/Ard.Walker.csproj)
- Primer in depth: the [ARD spec](https://agenticresourcediscovery.org/spec/) and [`docs/REFERENCES.md`](../docs/REFERENCES.md)

➡️ **Next: [Lab 01 — Speak MCP](01-well-known-and-mcp.md)** — turn that seed into your first completion code.
