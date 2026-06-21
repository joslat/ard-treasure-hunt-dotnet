using System.Text.Json;
using Ard.Core;

namespace Ard.AwardApp;

/// <summary>
/// Builds the two local documents the WebView2 renders:
/// <list type="number">
///   <item><b>award.html</b> — the server's MCP App, with a tiny script injected to pre-fill the player name;</item>
///   <item><b>host.html</b> — a wrapper that embeds the award in a <b>sandboxed iframe</b> and runs the host
///         side of the MCP Apps lifecycle (SEP-1865): answers <c>ui/initialize</c> with theme + capabilities,
///         then pushes <c>ui/notifications/tool-input</c> and <c>ui/notifications/tool-result</c>.</item>
/// </list>
/// This mirrors what an MCP Apps host (e.g. VS Code Insiders) does during the SEP-1865 lifecycle.
/// </summary>
public static class AwardHostHtml
{
    /// <summary>Inject a name-prefill script into the award document, just before <c>&lt;/body&gt;</c>.</summary>
    public static string BuildAwardDocument(string awardHtml, string name)
    {
        var nameJson = JsonSerializer.Serialize(name);
        var inject = $$"""
            <script>
            (function () {
              var NAME = {{nameJson}};
              function apply() {
                var el = document.getElementById('player-name');
                if (el) {
                  el.value = NAME;
                  el.dispatchEvent(new Event('input', { bubbles: true }));
                  el.dispatchEvent(new Event('change', { bubbles: true }));
                }
                var g = document.getElementById('player-greeting');
                if (g && !g.textContent) { g.textContent = 'Nice work, ' + NAME + '! 🎉'; }
                measure();
              }
              function measure() {
                var el = document.querySelector('main.award') || document.querySelector('.award') || document.body;
                if (!el) return;
                var r = el.getBoundingClientRect();
                try {
                  parent.postMessage({ type: 'ard-card-rect',
                    left: r.left, top: r.top, width: r.width, height: r.height }, '*');
                } catch (e) {}
              }
              if (document.readyState !== 'loading') setTimeout(apply, 40);
              else document.addEventListener('DOMContentLoaded', function () { setTimeout(apply, 40); });
              setTimeout(apply, 600);
              setTimeout(measure, 900);
              window.addEventListener('resize', measure);
            })();
            </script>
            """;

        var idx = awardHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? awardHtml.Insert(idx, inject) : awardHtml + inject;
    }

    /// <summary>Build the host wrapper page that drives the MCP Apps handshake for the embedded award.</summary>
    public static string BuildHostPage(AwardArtifact award)
    {
        var toolNameJson = JsonSerializer.Serialize("reveal_challenge_three");
        var structuredJson = JsonSerializer.Serialize(new { code = award.Code, message = award.Message });

        // Host context: a dark theme with the award's gold accent, mapped to the --mcp-ui-color-* CSS vars
        // the component reads (the MCP Apps host context).
        var hostContextJson = JsonSerializer.Serialize(new
        {
            theme = "dark",
            displayMode = "inline",
            styles = new
            {
                variables = new Dictionary<string, string>
                {
                    ["--mcp-ui-color-background"] = "#141430",
                    ["--mcp-ui-color-foreground"] = "#f4f4f8",
                    ["--mcp-ui-color-muted-foreground"] = "#b7b9c9",
                    ["--mcp-ui-color-card"] = "#ffffff12",
                    ["--mcp-ui-color-border"] = "#ffffff26",
                    ["--mcp-ui-color-accent"] = "#ffd24a",
                }
            }
        });

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>ARD Award</title>
              <style>
                html, body { margin: 0; height: 100%; background: #0f1020; overflow: hidden; }
                #app { border: 0; width: 100vw; height: 100vh; display: block; background: transparent; }
              </style>
            </head>
            <body>
              <iframe id="app" src="award.html"
                      sandbox="allow-scripts allow-popups allow-forms"
                      allow="clipboard-write"></iframe>
              <script>
                const HOST_CONTEXT = {{hostContextJson}};
                const TOOL_NAME = {{toolNameJson}};
                const STRUCTURED = {{structuredJson}};
                const frame = document.getElementById('app');

                let ready = false;
                function signalReady() {
                  if (ready) return;
                  ready = true;
                  try { window.chrome.webview.postMessage(JSON.stringify({ type: 'ard-ready' })); } catch (e) {}
                }
                function send(win, msg) { try { win.postMessage(msg, '*'); } catch (e) {} }

                function pushToolResult(win) {
                  send(win, { jsonrpc: '2.0', method: 'ui/notifications/tool-input', params: { toolName: TOOL_NAME, input: {} } });
                  send(win, { jsonrpc: '2.0', method: 'ui/notifications/tool-result', params: {
                    toolName: TOOL_NAME,
                    result: { content: [{ type: 'text', text: STRUCTURED.message }], structuredContent: STRUCTURED },
                    isError: false
                  } });
                }

                window.addEventListener('message', function (e) {
                  let m = e.data;
                  if (typeof m === 'string') { try { m = JSON.parse(m); } catch (_) { return; } }
                  if (!m || typeof m !== 'object') return;
                  const src = e.source || frame.contentWindow;

                  if (m.type === 'ard-card-rect') {
                    try { window.chrome.webview.postMessage(JSON.stringify({
                      type: 'card-rect', left: m.left, top: m.top, width: m.width, height: m.height })); } catch (e) {}
                    return;
                  }

                  if (m.method === 'ui/initialize') {
                    send(src, { jsonrpc: '2.0', id: m.id, result: {
                      protocolVersion: '2026-01-26',
                      hostInfo: { name: 'ARD Award App (.NET WebView2)', version: '1.0.0' },
                      hostCapabilities: { openLinks: {}, logging: {}, downloadFile: {} },
                      hostContext: HOST_CONTEXT
                    } });
                    pushToolResult(src);
                    setTimeout(function () { signalReady(); }, 350);
                  } else if (m.method === 'ui/notifications/initialized') {
                    pushToolResult(src);
                    setTimeout(function () { signalReady(); }, 250);
                  } else if (m.method === 'ui/notifications/size-changed') {
                    var h = m.params && (m.params.height || (m.params.size && m.params.size.height));
                    if (h) { frame.style.height = h + 'px'; }
                    try { window.chrome.webview.postMessage(JSON.stringify({ type: 'size', height: h })); } catch (e) {}
                  } else if (m.id !== undefined && typeof m.method === 'string' && m.method.indexOf('tools/') === 0) {
                    // App-initiated tool call — reply with the known award result (no real round-trip needed here).
                    send(src, { jsonrpc: '2.0', id: m.id, result: {
                      content: [{ type: 'text', text: STRUCTURED.message }], structuredContent: STRUCTURED
                    } });
                  }
                });

                // Fallback: even if the component never handshakes (renders from its CSS fallbacks), proceed.
                setTimeout(signalReady, 2500);
              </script>
            </body>
            </html>
            """;
    }
}
