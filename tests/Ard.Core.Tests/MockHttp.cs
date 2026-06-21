using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Moq;
using Moq.Protected;

namespace Ard.Core.Tests;

/// <summary>
/// Moq-based HTTP test doubles. Rather than hand-writing a stub <see cref="HttpMessageHandler"/>,
/// every test mocks the handler with Moq (via <c>Protected().Setup(...)</c>) so we can both stub
/// responses <i>and</i> <c>Verify(...)</c> how the client called out (URLs, headers, body).
/// </summary>
internal static class MockHttp
{
    /// <summary>A JSON (<c>application/json</c>) response.</summary>
    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>An SSE (<c>text/event-stream</c>) response wrapping one JSON-RPC payload as an <c>event:</c>/<c>data:</c> frame.</summary>
    public static HttpResponseMessage Sse(string jsonRpc, HttpStatusCode status = HttpStatusCode.OK)
    {
        var content = new StringContent($"event: message\ndata: {jsonRpc}\n\n", Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return new HttpResponseMessage(status) { Content = content };
    }

    /// <summary>A 202 Accepted with an empty body (what a JSON-RPC notification gets).</summary>
    public static HttpResponseMessage Accepted()
        => new(HttpStatusCode.Accepted) { Content = new StringContent("") };

    /// <summary>
    /// A mocked <see cref="HttpMessageHandler"/> that routes every request through <paramref name="router"/>,
    /// which receives the request and its (already-read) body text and returns the response to send back.
    /// </summary>
    public static Mock<HttpMessageHandler> Handler(Func<HttpRequestMessage, string, HttpResponseMessage> router)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                var body = req.Content is null ? "" : await req.Content.ReadAsStringAsync(ct);
                return router(req, body);
            });
        return mock;
    }

    /// <summary>An <see cref="HttpClient"/> backed by the mocked handler.</summary>
    public static HttpClient Client(this Mock<HttpMessageHandler> handler)
        => new(handler.Object) { Timeout = TimeSpan.FromSeconds(30) };
}
