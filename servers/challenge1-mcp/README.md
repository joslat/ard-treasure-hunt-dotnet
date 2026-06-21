# challenge1-mcp (S1)

Streamable-HTTP MCP server for **Challenge 1** (well-known catalog). Exposes one tool,
`reveal_challenge_one`, that returns the challenge-1 solution text.

## Run locally

```bash
npm install
npm run build
npm start          # listens on :3000, POST /mcp
```

Quick smoke test (initialize handshake):

```bash
curl -sS http://localhost:3000/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}'
```

`GET /healthz` returns `ok` for the Azure App Service health probe.

## Deploy to Azure App Service (Linux, Node 20+) — prebuilt

We deploy **prebuilt** to avoid the Oryx build hitting Azure's ~230s gateway timeout. The deploy is
a fast file-copy of the compiled output + production deps:

```powershell
# app: SCM_DO_BUILD_DURING_DEPLOYMENT=false, startup "npm start", always-on
npm run build                 # compile -> dist/
npm prune --omit=dev          # keep only runtime deps in node_modules
Compress-Archive package.json,dist,node_modules deploy.zip -Force
az webapp deploy -g ard_treasurehunt_rg -n ard-challenge1-mcp --src-path deploy.zip --type zip --track-status false
npm install                   # restore dev deps locally for next build
```

Notes:
- A `504 GatewayTimeout` from `az webapp deploy` is usually just the status-tracking call — the
  copy still lands. **Verify by hitting the live `/mcp`**, not by trusting the exit message.
- Deploy **one app at a time** (the B1 plan hosts both MCP apps; concurrent deploys starve it).
- The app listens on `process.env.PORT`. `GET /healthz` → `ok` for the health probe.
- Host is baked into `artifacts/cards/challenge1.mcp.json` (`endpoint.url`).

## Cloning for the next text server

Copy this folder, then change only the **Challenge config** block at the top of `src/index.ts`
(server name, tool name/title/description, solution text).
