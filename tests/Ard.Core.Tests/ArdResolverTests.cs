using Ard.Core;
using Moq;
using Moq.Protected;

namespace Ard.Core.Tests;

/// <summary>Moq-mocked HTTP tests for <see cref="ArdResolver"/> (well-known catalog, card fetch, registry search).</summary>
public class ArdResolverTests
{
    [Fact]
    public async Task ResolveWellKnownAsync_GetsTheWellKnownPath_AndParsesEntries()
    {
        // Arrange
        var handler = MockHttp.Handler((req, _) =>
            MockHttp.Json("""{"specVersion":"0.9","entries":[{"identifier":"urn:ai:x","type":"application/mcp-server+json","url":"https://x/card.json"}]}"""));
        var resolver = new ArdResolver(handler.Client());

        // Act
        var catalog = await resolver.ResolveWellKnownAsync("example.com");

        // Assert
        Assert.Equal("https://x/card.json", Assert.Single(catalog.Entries).Url);
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.RequestUri!.ToString() == "https://example.com/.well-known/ai-catalog.json"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ResolveWellKnownAsync_HonoursHttpScheme_ForSelfHostedHunt()
    {
        // Arrange
        var handler = MockHttp.Handler((req, _) =>
            MockHttp.Json("""{"specVersion":"0.9","entries":[{"identifier":"urn:ai:x","type":"application/mcp-server+json","url":"http://x/card.json"}]}"""));
        var resolver = new ArdResolver(handler.Client(), scheme: "http");

        // Act
        await resolver.ResolveWellKnownAsync("example.com");

        // Assert — the local-mode http scheme builds an http:// well-known URL.
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.RequestUri!.ToString() == "http://example.com/.well-known/ai-catalog.json"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task FetchCardAsync_ParsesEndpointAndTools()
    {
        // Arrange
        var handler = MockHttp.Handler((req, _) =>
            MockHttp.Json("""{"type":"application/mcp-server+json","endpoint":{"url":"https://x/mcp","transport":"streamable-http"},"tools":[{"name":"reveal_challenge_one"}]}"""));
        var resolver = new ArdResolver(handler.Client());

        // Act
        var card = await resolver.FetchCardAsync("https://x/card.json");

        // Assert
        Assert.Equal("https://x/mcp", card.Endpoint!.Url);
        Assert.Equal("streamable-http", card.Endpoint!.Transport);
        Assert.Equal("reveal_challenge_one", Assert.Single(card.Tools).Name);
    }

    [Fact]
    public async Task SearchAsync_PostsQueryEnvelopeToSearch_AndParsesRankedResults()
    {
        // Arrange
        string? capturedBody = null;
        var handler = MockHttp.Handler((req, body) =>
        {
            capturedBody = body;
            return MockHttp.Json("""{"results":[{"identifier":"urn:ai:x","url":"https://x.json","score":100}],"pageToken":null}""");
        });
        var resolver = new ArdResolver(handler.Client());

        // Act
        var resp = await resolver.SearchAsync("https://registry.example.net", "treasure hunt", 7);

        // Assert
        Assert.Equal(100, Assert.Single(resp.Results).Score);
        Assert.Contains("\"text\":\"treasure hunt\"", capturedBody);
        Assert.Contains("\"pageSize\":7", capturedBody);
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Method == HttpMethod.Post &&
                r.RequestUri!.ToString() == "https://registry.example.net/search"),
            ItExpr.IsAny<CancellationToken>());
    }
}
