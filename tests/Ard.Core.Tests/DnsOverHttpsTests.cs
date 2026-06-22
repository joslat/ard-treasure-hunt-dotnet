using System.Net;
using Ard.Core;
using Moq;
using Moq.Protected;

namespace Ard.Core.Tests;

/// <summary>Moq-mocked HTTP tests for <see cref="DnsOverHttps"/> (DoH TXT/SRV resolution + resolver fallback).</summary>
public class DnsOverHttpsTests
{
    [Fact]
    public async Task ResolveTxtAsync_ParsesUrlRecord_AndQueriesType16WithDnsJsonAccept()
    {
        // Arrange
        var handler = MockHttp.Handler((req, _) =>
            MockHttp.Json("""{"Status":0,"Answer":[{"type":16,"data":"url=https://x/ai-catalog.json"}]}"""));
        var dns = new DnsOverHttps(handler.Client());

        // Act
        var txts = await dns.ResolveTxtAsync("_catalog._agents.example.com");

        // Assert
        Assert.Equal("url=https://x/ai-catalog.json", Assert.Single(txts));
        handler.Protected().Verify("SendAsync", Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.RequestUri!.Query.Contains("type=16") &&
                r.Headers.Accept.Any(a => a.MediaType == "application/dns-json")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ResolveSrvAsync_ParsesRecord_AndDerivesBaseUrl()
    {
        // Arrange
        var handler = MockHttp.Handler((req, _) =>
            MockHttp.Json("""{"Status":0,"Answer":[{"type":33,"data":"0 0 443 registry.example.net."}]}"""));
        var dns = new DnsOverHttps(handler.Client());

        // Act
        var srvs = await dns.ResolveSrvAsync("_search._agents.example.com");

        // Assert
        var srv = Assert.Single(srvs);
        Assert.Equal(443, srv.Port);
        Assert.Equal("https://registry.example.net", srv.ToBaseUrl());
    }

    [Fact]
    public async Task QueryAsync_FallsBackToSecondResolver_WhenFirstFails()
    {
        // Arrange — dns.google returns 500; the Cloudflare fallback returns a valid answer.
        var handler = MockHttp.Handler((req, _) =>
            req.RequestUri!.Host.Contains("dns.google")
                ? MockHttp.Json("upstream error", HttpStatusCode.InternalServerError)
                : MockHttp.Json("""{"Status":0,"Answer":[{"type":16,"data":"url=https://ok"}]}"""));
        var dns = new DnsOverHttps(handler.Client());

        // Act
        var txts = await dns.ResolveTxtAsync("_catalog._agents.example.com");

        // Assert — resolution still succeeds, having tried both providers.
        Assert.Equal("url=https://ok", Assert.Single(txts));
        handler.Protected().Verify("SendAsync", Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_UsesCustomResolver_NotPublicDefaults()
    {
        // Arrange — point at a local mock-DoH endpoint via the resolvers override (the self-host path).
        var handler = MockHttp.Handler((req, _) =>
            req.RequestUri!.Host == "mock-doh.local"
                ? MockHttp.Json("""{"Status":0,"Answer":[{"type":16,"data":"url=https://ok"}]}""")
                : MockHttp.Json("should not be hit", HttpStatusCode.InternalServerError));
        var dns = new DnsOverHttps(handler.Client(), new[] { "https://mock-doh.local/resolve" });

        // Act
        var txts = await dns.ResolveTxtAsync("_catalog._agents.example.com");

        // Assert — resolution used the custom resolver and never touched the public defaults.
        Assert.Equal("url=https://ok", Assert.Single(txts));
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.Host == "mock-doh.local"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ResolveSrvAsync_OrdersByPriorityThenWeight()
    {
        // Arrange — mixed priority/weight; expect lower priority first, then higher weight within a priority.
        var handler = MockHttp.Handler((req, _) => MockHttp.Json(
            """{"Status":0,"Answer":[{"type":33,"data":"10 5 443 c.example.net."},{"type":33,"data":"0 1 443 a.example.net."},{"type":33,"data":"0 9 443 b.example.net."}]}"""));
        var dns = new DnsOverHttps(handler.Client());

        // Act
        var srvs = await dns.ResolveSrvAsync("_search._agents.example.com");

        // Assert — priority 0 before 10; within priority 0, higher weight (9) first.
        Assert.Equal(new[] { "b.example.net.", "a.example.net.", "c.example.net." }, srvs.Select(s => s.Target).ToArray());
    }
}
