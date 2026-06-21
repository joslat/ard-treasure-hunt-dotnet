# challenge3-mcp (S3) — MCP Apps award server

Streamable-HTTP MCP server for **Challenge 3** (the prize), built with the MCP Apps SDK
(`@modelcontextprotocol/ext-apps`). One tool, `reveal_challenge_three`, linked to a bundled HTML
UI resource (`ui://challenge-three/award.html`).

- **UI** (MCP Apps-capable hosts): a trophy, the heading *"Congrats, you solved the Agentic
  Resource Discovery (ARD) challenge!"*, a **name input** for the player, and a read-only
  **completion code** box showing `1337 h4x0r` (with a copy button).
- **Text fallback** (non-Apps hosts): `For this one, you need MCP Apps...` — intentionally does
  **not** reveal the code, so a player genuinely needs MCP Apps to finish.

## Layout

```
award.html        # UI entry HTML (vite input)
src/mcp-app.ts    # client: App lifecycle, theme, code/copy/greeting
src/award.css     # award styling (uses host style variables w/ fallbacks)
server.ts         # registerAppTool + registerAppResource (createServer)
main.ts           # Express + Streamable HTTP (stateless) at /mcp, /healthz
```

## Build & run

```bash
npm install
npm run build      # vite bundles award.html -> dist/award.html (single file) + tsc compiles server
npm start          # node dist/main.js  (listens on PORT, default 3000)
```

`build` = `cross-env INPUT=award.html vite build && tsc -p tsconfig.server.json`. The server reads
`dist/award.html` at runtime via `registerAppResource`.

## Deploy (prebuilt, same as S1/S2)

```bash
npm run build
npm prune --omit=dev
Compress-Archive package.json,dist,node_modules deploy.zip -Force
az webapp deploy -g ard_treasurehunt_rg -n <random-host> --src-path deploy.zip --type zip --track-status false
npm install        # restore dev deps locally
```

Verify by hitting the live `/mcp`: `tools/call reveal_challenge_three` returns the fallback text +
`structuredContent {code, message}`; `resources/read ui://challenge-three/award.html` returns the
HTML containing the code. (A `502/504` from `az webapp deploy` is usually just the tracking call.)
