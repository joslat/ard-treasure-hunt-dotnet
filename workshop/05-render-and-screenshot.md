# Lab 05 — Render it (become the MCP Apps host → 🏆 PNG)

⏱️ ~50 min · 🎯 **Goal:** host the award like a real MCP Apps client — theme it, fill in your name, and capture it as a **PNG**.

This is the capstone. In Lab 04 you *fetched* the UI; now you *run* it the way VS Code Insiders or Claude would, by playing **host**.

> 🪟 **Platform note:** this lab uses **WebView2** (Windows). On macOS/Linux, swap in Playwright for .NET (same idea: load the HTML, answer the handshake via an injected page, screenshot). The concepts are identical.

---

## 🧠 Understand

### Who's who in MCP Apps

The award SPA is a **guest** that expects a **host** around it. They talk over `postMessage` using JSON-RPC: because the guest runs inside the iframe, it reaches the host via `window.parent.postMessage`, and the host replies to `event.source`. Your job is the host side.

```
 MCP Server (challenge 3)        Your Host (WebView2 wrapper)        The View (award iframe)
        │  resources/read              │                                   │
        │  ───────────────▶ award.html │                                   │
        │                              │  render in sandboxed iframe ─────▶ │
        │                              │ ◀──── ui/initialize ───────────────│
        │                              │ ───── host context (theme) ──────▶ │
        │                              │ ───── ui/notifications/tool-result▶ │  (code: 1337 h4x0r)
```

### The host handshake (SEP-1865), reverse-engineered

1. **View → host:** `ui/initialize` with `{ appInfo, appCapabilities, protocolVersion: "2026-01-26" }`.
2. **Host → view:** the result with `hostContext`:
   ```jsonc
   { "protocolVersion":"2026-01-26",
     "hostCapabilities": { "openLinks":{}, "logging":{}, "downloadFile":{} },
     "hostContext": { "theme":"dark", "displayMode":"inline",
       "styles": { "variables": { "--mcp-ui-color-accent":"#ffd24a",
                                  "--mcp-ui-color-background":"#141430" } } } }
   ```
3. **View → host:** `ui/notifications/initialized`. Then it themes itself from those `--mcp-ui-color-*` variables and emits `ui/notifications/size-changed`.

The **name field + greeting** ("Nice work, {name}! 🎉") and **Copy** button are *local* interactivity — so to pre-fill your name you inject a tiny script that sets `#player-name` and dispatches an `input` event.

### Security model (why a sandbox)

A host must treat server-supplied UI as untrusted: render it in a **sandboxed iframe** (the reference uses `sandbox="allow-scripts allow-popups allow-forms"` — note the absence of `allow-same-origin`), so it can't touch your DOM/cookies/storage — only `postMessage`. The award is self-contained, so a restrictive CSP is satisfied trivially. This is non-negotiable in a real host and a great thing to internalize here. One consequence: a sandboxed iframe wants a real **origin**, so instead of a `file://` path we map our temp folder to a fake `https` host (`SetVirtualHostNameToFolderMapping` → `https://appassets.ard/`) and navigate there.

### Capturing the PNG

WebView2 is Chromium under the hood, so we drive it with the same **Chrome DevTools Protocol (CDP)** browsers expose for automation — via WebView2's `CallDevToolsProtocolMethodAsync`:

- `Page.captureScreenshot` with `captureBeyondViewport:true` — reliable full-content PNG that **works off-screen** (unlike `CapturePreviewAsync`, which only grabs the visible viewport).
- Wait **past** `NavigationCompleted` for actual paint (two `requestAnimationFrame`s + `document.fonts.ready`).
- Pin the device pixel ratio with `Emulation.setDeviceMetricsOverride` for a crisp, deterministic image; clip to the card's measured bounds for a tight crop.

---

## 🛠️ Build it

> 💬 **Prompt 1 — the WinForms + WebView2 host.**
> *"Create a WinForms project `Ard.AwardApp` (net9.0-windows, `UseWindowsForms`, reference `Ard.Core`, add the `Microsoft.Web.WebView2` package). Author the `.csproj` by hand if no template. A form hosts a `WebView2` filling the window."*

> 💬 **Prompt 2 — build the two local documents.**
> *"Add a helper that (a) injects a script into the award HTML before `</body>` that sets `#player-name` to a given name and dispatches an `input` event; and (b) builds a host wrapper page with a `sandbox=\"allow-scripts allow-popups allow-forms\"` iframe pointing at `award.html`, plus a script that listens for `message` events: on `ui/initialize` reply with a dark `hostContext` (gold `--mcp-ui-color-accent` `#ffd24a`), then post `ui/notifications/tool-result` with `{code, message}`. After the handshake, post a `{type:'ard-ready'}` message to `window.chrome.webview` so the .NET side knows rendering is done."*

> 💬 **Prompt 3 — render + screenshot.**
> *"In the form: fetch the award via `Ard.Core` (`HuntRunner.FetchAwardAsync` or discovery), write `award.html` + `host.html` to a temp dir, map it with `SetVirtualHostNameToFolderMapping`, and navigate to `https://appassets.ard/host.html`. After the `ard-ready` message, wait two animation frames + fonts.ready, then call `Page.captureScreenshot` (captureBeyondViewport, deviceScaleFactor 2) and save the bytes as PNG. Support `--screenshot <path>` to run offscreen and exit, and `--name <name>`."*

---

## 🔬 Inspect what you got

- [ ] Is the iframe actually **sandboxed**? (`sandbox="allow-scripts…"`, no `allow-same-origin` unless you have a reason.)
- [ ] Does the host reply to `ui/initialize` with `hostContext.styles.variables` containing `--mcp-ui-color-*`?
- [ ] Does capture wait for **paint** (rAF/fonts), not just `NavigationCompleted`?
- [ ] Did it reach for `CapturePreviewAsync`? That only captures the visible viewport and is unreliable off-screen — prefer the DevTools method.

---

## ✅ Checkpoint

```powershell
dotnet run --project src/Ard.AwardApp -- --screenshot ard-output/award.png --name "Your Name"
```

Open `award.png`: a **dark card, gold trophy, gold headline, your name, code `1337 h4x0r`, and "Nice work, Your Name! 🎉"**. 🏆

Then run it **without** `--screenshot` for the interactive window — change the name, hit *Reveal / Reload*, *Save PNG…*.

```powershell
dotnet run --project src/Ard.AwardApp
```

---

## ⚠️ Gotchas

- **White / blank screenshot** → you captured before paint. Wait for `ard-ready` + two `requestAnimationFrame`s. Also set the WebView's `DefaultBackgroundColor` so there's no white flash.
- **Theme didn't apply** → your `ui/initialize` reply is malformed or posted to the wrong window. Post back to `event.source`; put the vars under `hostContext.styles.variables`.
- **Name not filled** → the injected script ran before the DOM existed, or didn't dispatch `input`. Run on `DOMContentLoaded` and re-apply once after a short delay.
- **`SetVirtualHostNameToFolderMapping` 0x80070002** → call it *after* `EnsureCoreWebView2Async`, and make sure the temp folder exists.
- **Headless logging silent** → a WinExe has no console; `AttachConsole(-1)` at startup so `--screenshot` logs to the terminal.

---

## 🤔 Understand-it questions

1. Why must the View run in a **sandboxed** iframe, and what's the only channel left for it to talk to the host?
2. The host *could* just `srcdoc` the HTML at top level instead of in an iframe. What breaks about the security model — and about `window.parent` messaging — if you do?
3. `Page.captureScreenshot` works while the window is off-screen; `CapturePreviewAsync` doesn't. What does that tell you about *where* each one reads pixels from?
4. The code is already baked into the HTML. So what does sending `ui/notifications/tool-result` actually buy you for a more dynamic MCP App?

---

## 📂 Reference

- [`src/Ard.AwardApp/AwardHostHtml.cs`](../src/Ard.AwardApp/AwardHostHtml.cs) — `BuildAwardDocument` (name inject) + `BuildHostPage` (the host shim).
- [`src/Ard.AwardApp/AwardForm.cs`](../src/Ard.AwardApp/AwardForm.cs) — virtual-host mapping, `OnWebMessage`, `CaptureToFileAsync`.
- [`src/Ard.AwardApp/AwardSource.cs`](../src/Ard.AwardApp/AwardSource.cs) — acquisition strategies + offline fallback.

➡️ **Next: [Lab 06 — Wire it up](06-mcp-json-and-orchestration.md)** — make it one command, and open the servers in Claude.
