// Ard.Artifacts — serves the static ARD discovery files for the local hunt: the well-known catalog,
// the challenge-2 manifest, and the three MCP server cards. Every absolute URL is generated from the
// endpoints the AppHost resolved at runtime (the MCP server bases come in as env vars; the card URLs
// are built from the incoming request), so there are no hard-coded ports. In Azure these same files
// are served by THIS service running as a container app, with your custom domain bound to it
// (see src/Ard.AppHost/AppHost.cs and docs/SELFHOST.md).
//
// Config (injected by the AppHost): C1_BASE, C2_BASE, C3_BASE (the three MCP server base URLs).

using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Behind Azure Container Apps ingress (TLS terminated at the proxy), honor X-Forwarded-Proto so the scheme
// reads https rather than http. ACA does not send X-Forwarded-Host — it passes the original client Host
// header through unmodified, so Request.Host already equals the bound custom domain. Harmless locally.
var fwd = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

static string Mcp(string envName)
{
    var b = Environment.GetEnvironmentVariable(envName);
    if (string.IsNullOrWhiteSpace(b))
        throw new InvalidOperationException(
            $"{envName} is not set — Ard.Artifacts cannot build the MCP card endpoint. The AppHost injects C1_BASE/C2_BASE/C3_BASE; when running the container standalone, set it to the server's public ingress base URL.");
    return b.TrimEnd('/') + "/mcp";
}

static string Self(HttpRequest r) => $"{r.Scheme}://{r.Host}";

// Mechanism 1 — the well-known catalog at the domain root.
app.MapGet("/.well-known/ai-catalog.json", (HttpRequest r) => Results.Json(new
{
    specVersion = "1.0",
    host = new { displayName = "Self-hosted ARD Treasure Hunt", identifier = $"did:web:{r.Host}" },
    entries = new[]
    {
        new
        {
            identifier = "urn:ai:local:server:challenge-one",
            displayName = "Challenge 1 — Well-Known Catalog",
            type = "application/mcp-server+json",
            url = $"{Self(r)}/cards/challenge1.mcp.json",
            capabilities = new[] { "reveal_challenge_one" },
        },
    },
}));

// Mechanism 2 — the challenge-2 manifest the _catalog TXT record points at.
app.MapGet("/c2/ai-catalog.json", (HttpRequest r) => Results.Json(new
{
    specVersion = "1.0",
    entries = new[]
    {
        new
        {
            identifier = "urn:ai:local:server:challenge-two",
            displayName = "Challenge 2 — DNS TXT manifest",
            type = "application/mcp-server+json",
            url = $"{Self(r)}/cards/challenge2.mcp.json",
            capabilities = new[] { "reveal_challenge_two" },
        },
    },
}));

// The three MCP server cards (endpoint URLs come from the resolved server bases).
app.MapGet("/cards/challenge1.mcp.json", () => Card("challenge-one", Mcp("C1_BASE"), "reveal_challenge_one"));
app.MapGet("/cards/challenge2.mcp.json", () => Card("challenge-two", Mcp("C2_BASE"), "reveal_challenge_two"));
app.MapGet("/cards/challenge3.mcp.json", () => Card("challenge-three", Mcp("C3_BASE"), "reveal_challenge_three"));

app.MapGet("/healthz", () => "ok");
app.Run();

static IResult Card(string id, string mcpUrl, string tool) => Results.Json(new
{
    specVersion = "1.0",
    type = "application/mcp-server+json",
    identifier = $"urn:ai:local:server:{id}",
    endpoint = new { url = mcpUrl, transport = "streamable-http" },
    tools = new[] { new { name = tool, description = $"Returns the {id} solution." } },
});
