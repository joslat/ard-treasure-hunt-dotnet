using System.Net;
using System.Text.Json;
using Ard.Core;

namespace Ard.Core.Tests;

/// <summary>
/// End-to-end test of <see cref="HuntRunner.RunAsync"/> with the ENTIRE hunt mocked via a single Moq
/// <c>HttpMessageHandler</c>: the well-known catalog, DNS-over-HTTPS (TXT + SRV), the registry
/// <c>POST /search</c>, and three MCP servers (incl. the challenge-3 MCP App). No live network — it
/// proves the orchestration walks all three discovery mechanisms and collects every code offline.
/// </summary>
public class HuntRunnerEndToEndTests
{
    private static string MethodOf(string body)
    {
        if (string.IsNullOrEmpty(body)) return "";
        using var d = JsonDocument.Parse(body);
        return d.RootElement.TryGetProperty("method", out var m) ? m.GetString() ?? "" : "";
    }

    private static string IdOf(string body)
    {
        if (string.IsNullOrEmpty(body)) return "0";
        using var d = JsonDocument.Parse(body);
        return d.RootElement.TryGetProperty("id", out var i) ? i.GetRawText() : "0";
    }

    [Fact]
    public async Task RunAsync_WalksAllThreeMechanisms_AndCollectsEveryCodeAndTheAward()
    {
        // Arrange — one routing mock handler stands in for the whole hunt infrastructure.
        var handler = MockHttp.Handler((req, body) =>
        {
            var uri = req.RequestUri!;
            var host = uri.Host;
            var path = uri.GetLeftPart(UriPartial.Path); // scheme+host+path, no query

            // 1) DNS-over-HTTPS (either provider routes here) — TXT (type 16) and SRV (type 33).
            if (host.Contains("dns.google") || host.Contains("cloudflare"))
            {
                if (uri.Query.Contains("type=16"))
                    return MockHttp.Json("""{"Status":0,"Answer":[{"type":16,"data":"url=https://cards.example.com/catalog2.json"}]}""");
                if (uri.Query.Contains("type=33"))
                    return MockHttp.Json("""{"Status":0,"Answer":[{"type":33,"data":"0 0 443 registry.example.com."}]}""");
            }

            // 2) Static catalogs, cards, and the registry search endpoint.
            switch (path)
            {
                case "https://example.com/.well-known/ai-catalog.json":
                    return MockHttp.Json("""{"entries":[{"type":"application/mcp-server+json","url":"https://cards.example.com/card1.json"}]}""");
                case "https://cards.example.com/card1.json":
                    return MockHttp.Json("""{"endpoint":{"url":"https://mcp1.example.com/mcp","transport":"streamable-http"},"tools":[{"name":"reveal_challenge_one"}]}""");
                case "https://cards.example.com/catalog2.json":
                    return MockHttp.Json("""{"entries":[{"type":"application/mcp-server+json","url":"https://cards.example.com/card2.json"}]}""");
                case "https://cards.example.com/card2.json":
                    return MockHttp.Json("""{"endpoint":{"url":"https://mcp2.example.com/mcp"},"tools":[{"name":"reveal_challenge_two"}]}""");
                case "https://cards.example.com/card3.json":
                    return MockHttp.Json("""{"endpoint":{"url":"https://mcp3.example.com/mcp"},"tools":[{"name":"reveal_challenge_three"}]}""");
                case "https://registry.example.com/search":
                    return MockHttp.Json("""{"results":[{"identifier":"urn:ai:c3","url":"https://cards.example.com/card3.json","score":100}],"pageToken":null}""");
            }

            // 3) MCP servers (POST streamable-HTTP), routed by JSON-RPC method and which host.
            if (uri.AbsolutePath == "/mcp")
            {
                var method = MethodOf(body);
                if (method == "notifications/initialized") return MockHttp.Accepted();
                var result = method switch
                {
                    "initialize" => """{"protocolVersion":"2025-06-18","serverInfo":{"name":"mock"}}""",
                    "tools/list" => """{"tools":[{"name":"reveal"}]}""",
                    "resources/list" => """{"resources":[{"uri":"ui://challenge-three/award.html","mimeType":"text/html;profile=mcp-app"}]}""",
                    "resources/read" => """{"contents":[{"uri":"ui://challenge-three/award.html","text":"<html>AWARD</html>"}]}""",
                    "tools/call" => host switch
                    {
                        "mcp1.example.com" => """{"content":[{"type":"text","text":"Well done. Completion code: \"Rip and tear!\" Hint: DNS."}]}""",
                        "mcp2.example.com" => """{"content":[{"type":"text","text":"Nice. Completion code: \"Sean Astrakhan\" Hint: an SRV record."}]}""",
                        _ => """{"content":[{"type":"text","text":"For MCP Apps."}],"structuredContent":{"code":"1337 h4x0r","message":"Congrats!"}}""",
                    },
                    _ => "{}",
                };
                return MockHttp.Sse($$"""{"jsonrpc":"2.0","id":{{IdOf(body)}},"result":{{result}}}""");
            }

            return MockHttp.Json("{}", HttpStatusCode.NotFound);
        });

        var runner = new HuntRunner(handler.Client());

        // Act
        var report = await runner.RunAsync("example.com");

        // Assert — every mechanism solved, every code collected, the award captured.
        Assert.Equal(3, report.Challenges.Count);
        Assert.Equal("Rip and tear!", report.Challenges[0].Code);
        Assert.Equal("Sean Astrakhan", report.Challenges[1].Code);
        Assert.Equal("1337 h4x0r", report.Challenges[2].Code);
        Assert.Equal("Congrats!", report.Challenges[2].Message);
        Assert.Contains("AWARD", report.Challenges[2].AwardHtml!);
    }
}
