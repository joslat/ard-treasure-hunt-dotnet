// Ard.AppHost — .NET Aspire orchestration that runs the WHOLE treasure hunt locally with one command.
//
//   dotnet run --project src/Ard.AppHost
//
// It starts Andreas Adner's four vendored TypeScript servers (untouched), plus two tiny .NET glue
// services — Ard.Artifacts (the well-known catalog + MCP cards) and Ard.MockDoH (a local DNS-over-HTTPS
// stand-in for the _catalog TXT / _search SRV records) — and wires everything together by service
// discovery, so there are no hard-coded URLs or ports. Then it points the existing Ard.Walker solver
// at the local stack; press Start on "walker" in the dashboard to walk the trail and print the codes.
//
// Prereq (once): build the TS servers — see scripts/setup-local.ps1 (npm install + npm run build).

var builder = DistributedApplication.CreateBuilder(args);

// --- Andreas's vendored TS servers (run via npm; Aspire assigns each its PORT) ---
var c1 = builder.AddNpmApp("challenge1-mcp", "../../servers/challenge1-mcp")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/healthz");

var c2 = builder.AddNpmApp("challenge2-mcp", "../../servers/challenge2-mcp")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/healthz");

var c3 = builder.AddNpmApp("challenge3-mcp", "../../servers/challenge3-mcp")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/healthz");

var search = builder.AddNpmApp("challenge3-search", "../../servers/challenge3-search")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/healthz");

// --- glue: the static well-known catalog + MCP cards, generated from the resolved MCP endpoints ---
var artifacts = builder.AddProject<Projects.Ard_Artifacts>("artifacts")
    .WithEnvironment("C1_BASE", c1.GetEndpoint("http"))
    .WithEnvironment("C2_BASE", c2.GetEndpoint("http"))
    .WithEnvironment("C3_BASE", c3.GetEndpoint("http"))
    .WithHttpHealthCheck("/healthz");

// the search service returns OUR local challenge-3 card URL (its one env-overridable value)
search.WithEnvironment("CARD3_URL", ReferenceExpression.Create($"{artifacts.GetEndpoint("http")}/cards/challenge3.mcp.json"));

// --- glue: the mock DoH resolver serving the _catalog TXT + _search SRV records ---
var mockDoh = builder.AddProject<Projects.Ard_MockDoH>("mockdoh")
    .WithEnvironment("CATALOG2_URL", ReferenceExpression.Create($"{artifacts.GetEndpoint("http")}/c2/ai-catalog.json"))
    .WithEnvironment("SEARCH_URL", search.GetEndpoint("http"))
    .WithHttpHealthCheck("/healthz");

// --- the existing .NET solver, pointed at the local stack (press Start in the dashboard to run) ---
builder.AddProject<Projects.Ard_Walker>("walker")
    .WithEnvironment("ARD_SCHEME", "http")
    .WithEnvironment("ARD_SEED_DOMAIN", ReferenceExpression.Create(
        $"{artifacts.GetEndpoint("http").Property(EndpointProperty.Host)}:{artifacts.GetEndpoint("http").Property(EndpointProperty.Port)}"))
    .WithEnvironment("ARD_DOH_RESOLVER", ReferenceExpression.Create($"{mockDoh.GetEndpoint("http")}/resolve"))
    .WaitFor(c1).WaitFor(c2).WaitFor(c3).WaitFor(search).WaitFor(artifacts).WaitFor(mockDoh)
    .WithExplicitStart();

builder.Build().Run();
