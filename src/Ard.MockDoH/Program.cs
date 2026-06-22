// Ard.MockDoH — a tiny local stand-in for DNS-over-HTTPS, used ONLY by the local Aspire stack.
// It mimics the dns.google JSON API (GET /resolve?name=&type=) and answers the two ARD records the
// hunt needs: the _catalog._agents TXT (points at the challenge-2 manifest) and the _search._agents
// SRV (points at the search service). In Azure these are real Azure DNS records instead.
//
// Config (injected by the AppHost): CATALOG2_URL, SEARCH_URL.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const int TypeTxt = 16;
const int TypeSrv = 33;

app.MapGet("/resolve", (string? name, int type) =>
{
    if (type == TypeTxt)
    {
        var catalogUrl = Environment.GetEnvironmentVariable("CATALOG2_URL");
        return string.IsNullOrEmpty(catalogUrl) ? Empty() : Doh(name, TypeTxt, $"url={catalogUrl}");
    }
    if (type == TypeSrv)
    {
        var searchUrl = Environment.GetEnvironmentVariable("SEARCH_URL");
        if (string.IsNullOrEmpty(searchUrl)) return Empty();   // no bogus localhost:80 fallback that masks a missing wiring
        var search = new Uri(searchUrl);
        return Doh(name, TypeSrv, $"0 0 {search.Port} {search.Host}.");
    }
    return Empty();
});

app.MapGet("/healthz", () => "ok");
app.Run();

static IResult Doh(string? name, int type, string data) => Results.Json(new
{
    Status = 0,
    Answer = new[] { new { name = name ?? "", type, TTL = 60, data } },
});

static IResult Empty() => Results.Json(new { Status = 0, Answer = Array.Empty<object>() });
