using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Ard.Core;

/// <summary>
/// Turns each of the three ARD discovery mechanisms (ARD §6.1) into catalog entries / cards.
/// Nothing here hard-codes a blob or azurewebsites URL: every endpoint flows from the well-known
/// catalog, the DNS records, and the registry search.
/// </summary>
public sealed class ArdResolver
{
    private readonly HttpClient _http;
    private readonly DnsOverHttps _dns;
    private readonly string _scheme;

    /// <summary>
    /// <paramref name="scheme"/> ("https" default, "http" for a local self-hosted hunt) governs the
    /// well-known URL and the SRV-derived registry base; <paramref name="dohResolvers"/> overrides the DoH providers.
    /// </summary>
    public ArdResolver(HttpClient http, string scheme = "https", IEnumerable<string>? dohResolvers = null)
    {
        _http = http;
        _dns = new DnsOverHttps(http, dohResolvers);
        _scheme = scheme;
    }

    // --- Mechanism 1: Well-Known URI -------------------------------------------------

    /// <summary><c>GET https://{domain}/.well-known/ai-catalog.json</c>.</summary>
    public async Task<AiCatalog> ResolveWellKnownAsync(string domain, CancellationToken ct = default)
    {
        var url = $"{_scheme}://{domain}/.well-known/ai-catalog.json";
        return await FetchCatalogAsync(url, ct);
    }

    // --- Mechanism 4a: DNS TXT → static manifest pointer -----------------------------

    /// <summary>
    /// TXT lookup on <c>_catalog._agents.{domain}</c>, parse the <c>url=</c> value, fetch that catalog.
    /// Returns the parsed catalog plus the TXT value and resolved URL for reporting.
    /// </summary>
    public async Task<(AiCatalog Catalog, string TxtValue, string ManifestUrl)> ResolveDnsCatalogAsync(
        string domain, CancellationToken ct = default)
    {
        var name = $"_catalog._agents.{domain}";
        var txts = await _dns.ResolveTxtAsync(name, ct);
        var txt = txts.FirstOrDefault(t => t.StartsWith("url=", StringComparison.OrdinalIgnoreCase))
                  ?? throw new ArdException($"No 'url=' TXT record found at {name}.");
        var url = txt.Substring("url=".Length).Trim();
        var catalog = await FetchCatalogAsync(url, ct);
        return (catalog, txt, url);
    }

    // --- Mechanism 4b: DNS SRV → registry POST /search -------------------------------

    /// <summary>
    /// SRV lookup on <c>_search._agents.{domain}</c>, build the registry base URL, then <c>POST /search</c>
    /// per ARD §7.2. Returns the search response plus the SRV record and registry base for reporting.
    /// </summary>
    public async Task<(SearchResponse Response, SrvRecord Srv, string RegistryBase)> ResolveDnsRegistryAsync(
        string domain, string queryText, int pageSize = 10, CancellationToken ct = default)
    {
        var name = $"_search._agents.{domain}";
        var srvs = await _dns.ResolveSrvAsync(name, ct);
        var srv = srvs.FirstOrDefault() ?? throw new ArdException($"No SRV record found at {name}.");
        var registryBase = srv.ToBaseUrl(_scheme);
        var response = await SearchAsync(registryBase, queryText, pageSize, ct);
        return (response, srv, registryBase);
    }

    /// <summary><c>POST {registryBase}/search</c> with an ARD query envelope.</summary>
    public async Task<SearchResponse> SearchAsync(string registryBase, string queryText, int pageSize = 10, CancellationToken ct = default)
    {
        var url = $"{registryBase.TrimEnd('/')}/search";
        var body = new SearchRequest { Query = new SearchQuery { Text = queryText }, PageSize = pageSize };
        using var resp = await _http.PostAsJsonAsync(url, body, Json.Default, ct);
        resp.EnsureSuccessStatusCode();
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var result = JsonSerializer.Deserialize<SearchResponse>(Encoding.UTF8.GetString(bytes), Json.Default);
        return result ?? throw new ArdException($"Registry search at {url} returned no parseable body.");
    }

    // --- shared fetchers -------------------------------------------------------------

    /// <summary>Fetch and parse an <c>ai-catalog.json</c> document.</summary>
    public async Task<AiCatalog> FetchCatalogAsync(string url, CancellationToken ct = default)
    {
        var json = await GetStringUtf8Async(url, ct);
        return JsonSerializer.Deserialize<AiCatalog>(json, Json.Default)
               ?? throw new ArdException($"Catalog at {url} did not parse.");
    }

    /// <summary>Fetch and parse an <c>application/mcp-server+json</c> card.</summary>
    public async Task<McpServerCard> FetchCardAsync(string url, CancellationToken ct = default)
    {
        var json = await GetStringUtf8Async(url, ct);
        return JsonSerializer.Deserialize<McpServerCard>(json, Json.Default)
               ?? throw new ArdException($"MCP card at {url} did not parse.");
    }

    /// <summary>Raw GET returning the body decoded as UTF-8 (also used by the console <c>fetch</c> command).</summary>
    public async Task<string> GetStringUtf8Async(string url, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        return Encoding.UTF8.GetString(bytes);
    }
}
