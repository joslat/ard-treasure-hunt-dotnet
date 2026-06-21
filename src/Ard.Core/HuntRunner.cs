using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ard.Core;

/// <summary>
/// Walks the entire ARD trail starting from a single seed domain — the only thing a solver is given.
/// Each leg follows the same pattern: <i>discover a catalog entry → fetch the MCP card → connect →
/// call the single <c>reveal_*</c> tool → read the completion code + next hint</i>. The hint in each
/// result tells you which mechanism comes next (Well-Known → DNS TXT → DNS SRV).
/// </summary>
public sealed partial class HuntRunner
{
    private readonly HttpClient _http;
    private readonly ArdResolver _resolver;
    private readonly Action<string> _log;

    public HuntRunner(HttpClient http, Action<string>? log = null)
    {
        _http = http;
        _resolver = new ArdResolver(http);
        _log = log ?? (_ => { });
    }

    public async Task<HuntReport> RunAsync(string seedDomain = "nullpointer.se", CancellationToken ct = default)
    {
        var report = new HuntReport { SeedDomain = seedDomain };
        report.Challenges.Add(await SolveChallengeOneAsync(seedDomain, ct));
        report.Challenges.Add(await SolveChallengeTwoAsync(seedDomain, ct));
        report.Challenges.Add(await SolveChallengeThreeAsync(seedDomain, ct));
        return report;
    }

    // --- Challenge 1: Well-Known URI -------------------------------------------------

    private async Task<ChallengeResult> SolveChallengeOneAsync(string domain, CancellationToken ct)
    {
        var r = new ChallengeResult { Number = 1, Mechanism = "Well-Known URI" };
        _log($"\n=== Challenge 1 — Well-Known URI ===");

        Step(r, $"GET https://{domain}/.well-known/ai-catalog.json");
        var catalog = await _resolver.ResolveWellKnownAsync(domain, ct);
        var entry = FirstEntry(catalog, "well-known catalog");
        Step(r, $"catalog entry: {entry.Identifier}  →  card: {entry.Url}");

        await SolveFromEntryUrlAsync(r, entry.Url!, ct);
        return r;
    }

    // --- Challenge 2: DNS TXT → manifest pointer -------------------------------------

    private async Task<ChallengeResult> SolveChallengeTwoAsync(string domain, CancellationToken ct)
    {
        var r = new ChallengeResult { Number = 2, Mechanism = "DNS TXT → manifest pointer" };
        _log($"\n=== Challenge 2 — DNS TXT manifest pointer ===");

        Step(r, $"DNS TXT _catalog._agents.{domain}");
        var (catalog, txt, manifestUrl) = await _resolver.ResolveDnsCatalogAsync(domain, ct);
        Step(r, $"TXT: {txt}");
        Step(r, $"→ manifest: {manifestUrl}");
        var entry = FirstEntry(catalog, "DNS-pointed catalog");
        Step(r, $"catalog entry: {entry.Identifier}  →  card: {entry.Url}");

        await SolveFromEntryUrlAsync(r, entry.Url!, ct);
        return r;
    }

    // --- Challenge 3: DNS SRV → registry /search → MCP Apps award --------------------

    private async Task<ChallengeResult> SolveChallengeThreeAsync(string domain, CancellationToken ct)
    {
        var r = new ChallengeResult { Number = 3, Mechanism = "DNS SRV → registry /search → MCP App" };
        _log($"\n=== Challenge 3 — DNS SRV → registry search → MCP App ===");

        Step(r, $"DNS SRV _search._agents.{domain}");
        var (search, srv, registryBase) = await _resolver.ResolveDnsRegistryAsync(domain, "treasure hunt challenge", 10, ct);
        Step(r, $"SRV: {srv}  →  registry: {registryBase}");
        Step(r, $"POST {registryBase}/search  →  {search.Results.Count} result(s)");
        var top = search.Results.OrderByDescending(x => x.Score).FirstOrDefault()
                  ?? throw new ArdException("Registry /search returned no results.");
        Step(r, $"top result: {top.Identifier} (score {top.Score})  →  card: {top.Url}");

        var card = await _resolver.FetchCardAsync(top.Url!, ct);
        var endpoint = card.Endpoint?.Url ?? throw new ArdException("Challenge-3 card has no endpoint URL.");
        var toolName = card.Tools.FirstOrDefault()?.Name ?? "reveal_challenge_three";
        r.Endpoint = endpoint;
        r.Tool = toolName;
        Step(r, $"MCP endpoint: {endpoint} (transport {card.Endpoint?.Transport})");

        var mcp = new McpHttpClient(_http, endpoint);
        await mcp.InitializeAsync(ct: ct);
        await mcp.ListToolsAsync(ct);
        var result = await mcp.CallToolAsync(toolName, ct: ct);

        r.ResultText = GetResultText(result);
        var (code, message) = GetStructuredCode(result);
        r.Code = code ?? ExtractCodeFromText(r.ResultText);
        r.Message = message;
        _log($"  ▸ {toolName} → structuredContent.code = \"{r.Code}\"");

        // Read the MCP App award component.
        var resources = await mcp.ListResourcesAsync(ct);
        var uri = FindUiResourceUri(resources) ?? "ui://challenge-three/award.html";
        r.AwardUri = uri;
        Step(r, $"resources/read {uri}");
        r.AwardHtml = await mcp.ReadResourceTextAsync(uri, ct);
        _log($"  ▸ award HTML: {r.AwardHtml.Length:N0} chars");

        return r;
    }

    /// <summary>
    /// Connect straight to an MCP Apps endpoint, call its tool, and fetch the award HTML — the path the
    /// AwardApp uses when it isn't walking the whole trail.
    /// </summary>
    public async Task<AwardArtifact> FetchAwardAsync(string endpoint, CancellationToken ct = default)
    {
        var mcp = new McpHttpClient(_http, endpoint);
        await mcp.InitializeAsync(ct: ct);
        var tools = await mcp.ListToolsAsync(ct);
        var toolName = FirstToolName(tools) ?? "reveal_challenge_three";

        var result = await mcp.CallToolAsync(toolName, ct: ct);
        var (code, message) = GetStructuredCode(result);
        code ??= ExtractCodeFromText(GetResultText(result)) ?? "1337 h4x0r";
        message ??= "Congrats, you solved the Agentic Resource Discovery (ARD) challenge!";

        var resources = await mcp.ListResourcesAsync(ct);
        var uri = FindUiResourceUri(resources) ?? "ui://challenge-three/award.html";
        var html = await mcp.ReadResourceTextAsync(uri, ct);

        return new AwardArtifact(code, message, html, uri, endpoint);
    }

    // --- shared per-leg logic --------------------------------------------------------

    private async Task SolveFromEntryUrlAsync(ChallengeResult r, string cardUrl, CancellationToken ct)
    {
        var card = await _resolver.FetchCardAsync(cardUrl, ct);
        var endpoint = card.Endpoint?.Url ?? throw new ArdException("Card has no endpoint URL.");
        var toolName = card.Tools.FirstOrDefault()?.Name
                       ?? throw new ArdException("Card declares no tools.");
        r.Endpoint = endpoint;
        r.Tool = toolName;
        Step(r, $"MCP endpoint: {endpoint} (transport {card.Endpoint?.Transport})");

        var mcp = new McpHttpClient(_http, endpoint);
        await mcp.InitializeAsync(ct: ct);
        await mcp.ListToolsAsync(ct);
        var result = await mcp.CallToolAsync(toolName, ct: ct);

        r.ResultText = GetResultText(result);
        var (code, message) = GetStructuredCode(result);
        r.Code = code ?? ExtractCodeFromText(r.ResultText);
        r.Message = message;
        _log($"  ▸ {toolName} → code = \"{r.Code}\"");
    }

    // --- helpers ---------------------------------------------------------------------

    private void Step(ChallengeResult r, string s)
    {
        r.DiscoverySteps.Add(s);
        _log($"  • {s}");
    }

    private static CatalogEntry FirstEntry(AiCatalog catalog, string what)
    {
        var entry = catalog.Entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Url))
                    ?? throw new ArdException($"No usable entry with a 'url' in the {what}.");
        return entry;
    }

    private static string GetResultText(JsonElement result)
    {
        var sb = new StringBuilder();
        if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in content.EnumerateArray())
                if (item.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && item.TryGetProperty("text", out var txt))
                    sb.Append(txt.GetString());
        }
        return sb.ToString();
    }

    private static (string? Code, string? Message) GetStructuredCode(JsonElement result)
    {
        if (result.TryGetProperty("structuredContent", out var sc) && sc.ValueKind == JsonValueKind.Object)
        {
            var code = sc.TryGetProperty("code", out var c) ? c.GetString() : null;
            var msg = sc.TryGetProperty("message", out var m) ? m.GetString() : null;
            return (code, msg);
        }
        return (null, null);
    }

    private static string? ExtractCodeFromText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var m = CompletionCodeRegex().Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? FindUiResourceUri(JsonElement resources)
    {
        if (resources.TryGetProperty("resources", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var res in arr.EnumerateArray())
                if (res.TryGetProperty("uri", out var u) && u.GetString() is { } uri
                    && uri.StartsWith("ui://", StringComparison.OrdinalIgnoreCase))
                    return uri;
        return null;
    }

    private static string? FirstToolName(JsonElement tools)
    {
        if (tools.TryGetProperty("tools", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var t in arr.EnumerateArray())
                if (t.TryGetProperty("name", out var n)) return n.GetString();
        return null;
    }

    [GeneratedRegex("Completion code:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex CompletionCodeRegex();
}
