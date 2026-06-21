using System.Text.Json;
using Ard.Core;
using Moq;
using Moq.Protected;

namespace Ard.Core.Tests;

/// <summary>Moq-mocked HTTP tests for <see cref="McpHttpClient"/> (handshake, tool call, the dual-Accept rule, JSON-RPC errors).</summary>
public class McpHttpClientTests
{
    private static string MethodOf(string body)
    {
        if (string.IsNullOrEmpty(body)) return "";
        using var d = JsonDocument.Parse(body);
        return d.RootElement.TryGetProperty("method", out var m) ? m.GetString() ?? "" : "";
    }

    [Fact]
    public async Task InitializeAsync_ParsesProtocolAndServer_AndSendsBothAcceptMediaTypes()
    {
        // Arrange
        var handler = MockHttp.Handler((req, body) => MethodOf(body) switch
        {
            "initialize" => MockHttp.Sse("""{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","serverInfo":{"name":"challenge-1"}}}"""),
            "notifications/initialized" => MockHttp.Accepted(),
            _ => MockHttp.Sse("""{"jsonrpc":"2.0","id":1,"result":{}}"""),
        });
        var client = new McpHttpClient(handler.Client(), "https://server.example.com/mcp");

        // Act
        await client.InitializeAsync();

        // Assert — negotiated values are captured, and the critical dual-Accept header was sent.
        Assert.Equal("2025-06-18", client.ProtocolVersion);
        Assert.Equal("challenge-1", client.ServerName);
        handler.Protected().Verify("SendAsync", Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Headers.Accept.Any(a => a.MediaType == "application/json") &&
                r.Headers.Accept.Any(a => a.MediaType == "text/event-stream")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CallToolAsync_ReturnsResultContentFromSse()
    {
        // Arrange
        var handler = MockHttp.Handler((req, body) => MethodOf(body) == "tools/call"
            ? MockHttp.Sse("""{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"hello"}]}}""")
            : MockHttp.Sse("""{"jsonrpc":"2.0","id":1,"result":{}}"""));
        var client = new McpHttpClient(handler.Client(), "https://server.example.com/mcp");

        // Act
        var result = await client.CallToolAsync("reveal_challenge_one");

        // Assert
        Assert.Equal("hello", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task JsonRpcError_IsSurfacedAsArdException()
    {
        // Arrange — the server replies with a JSON-RPC error envelope.
        var handler = MockHttp.Handler((req, body) =>
            MockHttp.Sse("""{"jsonrpc":"2.0","id":1,"error":{"code":-32000,"message":"Not Acceptable"}}"""));
        var client = new McpHttpClient(handler.Client(), "https://server.example.com/mcp");

        // Act + Assert
        var ex = await Assert.ThrowsAsync<ArdException>(() => client.CallToolAsync("reveal_challenge_one"));
        Assert.Contains("Not Acceptable", ex.Message);
    }

    [Fact]
    public async Task ReadResourceTextAsync_ReturnsFirstContentText()
    {
        // Arrange
        var handler = MockHttp.Handler((req, body) =>
            MockHttp.Sse("""{"jsonrpc":"2.0","id":1,"result":{"contents":[{"uri":"ui://x/award.html","text":"<html>AWARD</html>"}]}}"""));
        var client = new McpHttpClient(handler.Client(), "https://server.example.com/mcp");

        // Act
        var html = await client.ReadResourceTextAsync("ui://x/award.html");

        // Assert
        Assert.Equal("<html>AWARD</html>", html);
    }
}
