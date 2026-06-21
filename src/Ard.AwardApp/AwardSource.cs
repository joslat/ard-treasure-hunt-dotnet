using System.Text.RegularExpressions;
using Ard.Core;

namespace Ard.AwardApp;

/// <summary>Acquires the challenge-3 award (HTML + completion code) by the requested strategy, with offline fallbacks.</summary>
public static partial class AwardSource
{
    public static async Task<AwardArtifact> AcquireAsync(AwardOptions o, Action<string> log, CancellationToken ct = default)
    {
        // 1. Explicit local file (offline / render the walker's saved award.html).
        if (o.HtmlPath is not null)
        {
            log($"Loading local award HTML: {o.HtmlPath}");
            return FromHtmlFile(o.HtmlPath);
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ard-award-app/1.0");

        try
        {
            var runner = new HuntRunner(http, log);

            if (o.Walk)
            {
                log("Walking the entire ARD trail to discover the award…");
                var report = await runner.RunAsync(o.Domain, ct);
                var c3 = report.Challenges.First(c => c.AwardHtml is not null);
                return new AwardArtifact(c3.Code ?? "1337 h4x0r",
                    c3.Message ?? DefaultMessage, c3.AwardHtml!, c3.AwardUri ?? "ui://challenge-three/award.html", c3.Endpoint ?? "");
            }

            var endpoint = o.Endpoint;
            if (endpoint is null)
            {
                log($"Discovering the challenge-3 MCP App via ARD ({o.Domain})…");
                var resolver = new ArdResolver(http);
                var (search, _, registry) = await resolver.ResolveDnsRegistryAsync(o.Domain, "treasure hunt challenge", ct: ct);
                log($"  registry: {registry}");
                var top = search.Results.OrderByDescending(s => s.Score).First();
                var card = await resolver.FetchCardAsync(top.Url!, ct);
                endpoint = card.Endpoint?.Url ?? throw new ArdException("Card had no endpoint.");
            }

            log($"Connecting to MCP endpoint: {endpoint}");
            return await runner.FetchAwardAsync(endpoint, ct);
        }
        catch (Exception ex)
        {
            log($"⚠ Live fetch failed ({ex.Message}). Trying a captured local copy…");
            var fallback = FindCapturedAward();
            if (fallback is not null)
            {
                log($"Using captured award: {fallback}");
                return FromHtmlFile(fallback);
            }
            throw;
        }
    }

    private const string DefaultMessage = "Congrats, you solved the Agentic Resource Discovery (ARD) challenge!";

    private static AwardArtifact FromHtmlFile(string path)
    {
        var html = File.ReadAllText(path);
        var code = CompletionCodeAttr().Match(html) is { Success: true } m ? m.Groups[1].Value : "1337 h4x0r";
        return new AwardArtifact(code, DefaultMessage, html, "ui://challenge-three/award.html", $"(local file) {path}");
    }

    /// <summary>Walk up from the app's base directory to find <c>artifacts/(run|captured)/award.html</c>.</summary>
    private static string? FindCapturedAward()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            foreach (var sub in new[] { "run", "captured" })
            {
                var candidate = Path.Combine(dir.FullName, "artifacts", sub, "award.html");
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    [GeneratedRegex("id=\"completion-code\"[^>]*value=\"([^\"]*)\"")]
    private static partial Regex CompletionCodeAttr();
}
