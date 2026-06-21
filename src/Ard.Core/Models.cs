using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ard.Core;

// ---------------------------------------------------------------------------
// ARD — ai-catalog.json (the "phone book" entry; ARD §6 / ai-catalog standard)
// ---------------------------------------------------------------------------

/// <summary>An ARD catalog document (<c>ai-catalog.json</c>): the list of agentic resources a host publishes.</summary>
public sealed class AiCatalog
{
    public string? SpecVersion { get; set; }
    public CatalogHost? Host { get; set; }
    public List<CatalogEntry> Entries { get; set; } = new();
}

public sealed class CatalogHost
{
    public string? DisplayName { get; set; }
    public string? Identifier { get; set; }
}

/// <summary>
/// A single ARD catalog entry. Carries exactly one of <see cref="Url"/> (a pointer to the artifact
/// document, e.g. an MCP card) or <see cref="Data"/> (an inline artifact).
/// </summary>
public sealed class CatalogEntry
{
    public string? Identifier { get; set; }
    public string? DisplayName { get; set; }
    public string? Type { get; set; }
    public string? Url { get; set; }
    public JsonElement? Data { get; set; }
    public string? Description { get; set; }
    public List<string>? Capabilities { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? RepresentativeQueries { get; set; }
}

// ---------------------------------------------------------------------------
// MCP server card (application/mcp-server+json)
// ---------------------------------------------------------------------------

/// <summary>The MCP "card" an ARD entry points at: how to actually connect to the server.</summary>
public sealed class McpServerCard
{
    public string? SpecVersion { get; set; }
    public string? Type { get; set; }
    public string? Identifier { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public McpEndpoint? Endpoint { get; set; }
    public List<McpCardTool> Tools { get; set; } = new();
}

public sealed class McpEndpoint
{
    public string? Url { get; set; }
    public string? Transport { get; set; }
}

public sealed class McpCardTool
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

// ---------------------------------------------------------------------------
// ARD registry REST API (ARD §7) — POST /search
// ---------------------------------------------------------------------------

public sealed class SearchRequest
{
    public SearchQuery Query { get; set; } = new();
    public int PageSize { get; set; } = 10;
}

public sealed class SearchQuery
{
    public string Text { get; set; } = "";
    public JsonElement? Filter { get; set; }
}

public sealed class SearchResponse
{
    public List<SearchResult> Results { get; set; } = new();
    public List<JsonElement>? Referrals { get; set; }
    public string? PageToken { get; set; }
}

public sealed class SearchResult
{
    public string? Identifier { get; set; }
    public string? DisplayName { get; set; }
    public string? Type { get; set; }
    public string? Url { get; set; }
    public double Score { get; set; }
}

// ---------------------------------------------------------------------------
// DNS-over-HTTPS (RFC 8484 JSON form, as served by dns.google / cloudflare-dns.com)
// ---------------------------------------------------------------------------

public sealed class DohResponse
{
    public int Status { get; set; }
    public List<DohAnswer>? Answer { get; set; }
}

public sealed class DohAnswer
{
    public string? Name { get; set; }
    public int Type { get; set; }
    public int Ttl { get; set; }
    public string? Data { get; set; }
}

/// <summary>A parsed SRV record: <c>priority weight port target</c>.</summary>
public sealed record SrvRecord(int Priority, int Weight, int Port, string Target)
{
    /// <summary>Base URL implied by the SRV target+port (drops the scheme's default port). Pass <c>scheme: "http"</c> for a local self-hosted hunt.</summary>
    public string ToBaseUrl(string scheme = "https")
    {
        var host = Target.TrimEnd('.');
        var defaultPort = scheme == "http" ? 80 : 443;
        return Port == defaultPort ? $"{scheme}://{host}" : $"{scheme}://{host}:{Port}";
    }

    public override string ToString() => $"{Priority} {Weight} {Port} {Target}";
}
