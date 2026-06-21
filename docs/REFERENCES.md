# References & credits

This repo is a **.NET / C# solution and teaching kit** built around Andreas Adner's *Agentic Resource Discovery (ARD) treasure hunt*. This page collects the (non-spoiler) external resources the toolkit and workshop build on.

> ⚠️ **Spoiler note:** the rest of this repository (the `README`, the `workshop/`, and `artifacts/run/award.png`) *does* contain the solution — the completion codes and the full discovery path. If you want to solve the hunt yourself first, start from the single clue `https://nullpointer.se/.well-known/ai-catalog.json` and come back afterwards.

---

## ARD — Agentic Resource Discovery

- Spec (v0.9 draft): <https://agenticresourcediscovery.org/spec/>
- Home / overview: <https://agenticresourcediscovery.org/>
- Spec repo (source of truth): <https://github.com/ards-project/ard-spec>
- `ai-catalog` standard (ARD builds on it): <https://github.com/Agent-Card/ai-catalog>
- Microsoft announcement: <https://commandline.microsoft.com/agentic-resource-discovery-specification-ard/>
- Google: <https://developers.googleblog.com/announcing-the-agentic-resource-discovery-specification/>
- Hugging Face: <https://huggingface.co/blog/agentic-resource-discovery-launch>

## MCP & MCP Apps

- Core MCP spec: <https://modelcontextprotocol.io/>
- MCP Apps docs (extension overview): <https://modelcontextprotocol.io/docs/extensions/apps>
- MCP Apps repo — spec + SDK (SEP-1865, ext id `io.modelcontextprotocol/ui`): <https://github.com/modelcontextprotocol/ext-apps>
- MCP Apps announcement: <https://blog.modelcontextprotocol.io/posts/2026-01-26-mcp-apps/>
- MCP-UI client (alternative host framework): `@mcp-ui/client`

## Editors & hosts

- MCP Apps in VS Code (Insiders-first): <https://code.visualstudio.com/blogs/2026/01/26/mcp-apps-support>
- VS Code MCP config reference (`mcp.json`): <https://code.visualstudio.com/docs/agents/reference/mcp-configuration>
- Visual Studio MCP servers: <https://learn.microsoft.com/visualstudio/ide/mcp-servers>
- Claude — connect to remote MCP servers (Connectors): <https://modelcontextprotocol.io/docs/develop/connect-remote-servers>

## .NET

- WebView2 — `CallDevToolsProtocolMethodAsync`: <https://learn.microsoft.com/dotnet/api/microsoft.web.webview2.core.corewebview2.calldevtoolsprotocolmethodasync>
- Chrome DevTools Protocol — `Page.captureScreenshot`: <https://chromedevtools.github.io/devtools-protocol/tot/Page/#method-captureScreenshot>
- `ModelContextProtocol` C# SDK (alternative to the thin client here): <https://github.com/modelcontextprotocol/csharp-sdk>

## The hunt

- **Andreas Adner** (Microsoft MVP) — blog <https://nullpointer.se/>. The treasure hunt, the three hidden MCP servers, and the award MCP App are his work; this repository is an independent .NET study/solution + workshop built around it.
