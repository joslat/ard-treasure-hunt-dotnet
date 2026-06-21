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
}
