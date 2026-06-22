// Ard.AppHost — .NET Aspire orchestration. Runs the WHOLE treasure hunt two ways:
//
//   • LOCAL  (dotnet run):  starts Andreas's 4 TS servers + 2 .NET glue services + the walker,
//                           with a local mock-DoH standing in for DNS. Press Start on "walker".
//   • AZURE  (azd up):      deploys the 4 servers + Ard.Artifacts to Azure Container Apps. DNS is
//                           real (Azure DNS, provisioned by infra/dns.bicep), so the local-only
//                           mock-DoH and walker are excluded from the published app.
//
// Local prereq (once): build the TS servers — see scripts/setup-local.ps1.

using Azure.Provisioning.AppContainers;

var builder = DistributedApplication.CreateBuilder(args);

// Scale every published container app to zero when idle (cheap idle; cold-start on first request — fine for a hunt).
static void ScaleToZero(Azure.Provisioning.AppContainers.ContainerApp app)
{
    app.Template.Scale = new ContainerAppScale { MinReplicas = 0, MaxReplicas = 3 };
}

// Azure publish only: model the Azure Container Apps environment so `azd up` has an ACA environment
// to attach the container apps to (Aspire 9.4+ no longer creates one implicitly).
// WithAzdResourceNaming() keeps azd's existing resource-naming scheme.
if (builder.ExecutionContext.IsPublishMode)
    builder.AddAzureContainerAppEnvironment("aca-env").WithAzdResourceNaming();

// --- Andreas's vendored TS servers (npm locally; containerized for Azure) ---
// NOTE: deliberately Aspire.Hosting.NodeJs / AddNpmApp (default script "start"). The newer
// Aspire.Hosting.JavaScript / AddJavaScriptApp defaults to "dev" (tsc --watch — never starts a listener)
// AND rejects a runScriptName when an existing Dockerfile is present, breaking the azd publish path with
// our custom multi-stage Dockerfiles. The NodeJs package is pinned and works for both run and publish.
var c1 = builder.AddNpmApp("challenge1-mcp", "../../servers/challenge1-mcp", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/healthz")
    .WithExternalHttpEndpoints()
    .WithEnvironment("NODE_ENV", builder.ExecutionContext.IsPublishMode ? "production" : "development")
    .PublishAsDockerFile()
    .PublishAsAzureContainerApp((infra, app) => ScaleToZero(app));

var c2 = builder.AddNpmApp("challenge2-mcp", "../../servers/challenge2-mcp", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/healthz")
    .WithExternalHttpEndpoints()
    .WithEnvironment("NODE_ENV", builder.ExecutionContext.IsPublishMode ? "production" : "development")
    .PublishAsDockerFile()
    .PublishAsAzureContainerApp((infra, app) => ScaleToZero(app));

var c3 = builder.AddNpmApp("challenge3-mcp", "../../servers/challenge3-mcp", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/healthz")
    .WithExternalHttpEndpoints()
    .WithEnvironment("NODE_ENV", builder.ExecutionContext.IsPublishMode ? "production" : "development")
    .PublishAsDockerFile()
    .PublishAsAzureContainerApp((infra, app) => ScaleToZero(app));

var search = builder.AddNpmApp("challenge3-search", "../../servers/challenge3-search", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/healthz")
    .WithExternalHttpEndpoints()
    .WithEnvironment("NODE_ENV", builder.ExecutionContext.IsPublishMode ? "production" : "development")
    .PublishAsDockerFile()
    .PublishAsAzureContainerApp((infra, app) => ScaleToZero(app));

// --- glue: the well-known catalog + MCP cards, generated from the resolved MCP endpoints ---
// In Azure this is the public entry point: bind your custom domain to THIS container app so
// https://{domain}/.well-known/ai-catalog.json is served here (see docs/SELFHOST.md).
var artifacts = builder.AddProject<Projects.Ard_Artifacts>("artifacts")
    .WithEnvironment("C1_BASE", c1.GetEndpoint("http"))
    .WithEnvironment("C2_BASE", c2.GetEndpoint("http"))
    .WithEnvironment("C3_BASE", c3.GetEndpoint("http"))
    .WithHttpHealthCheck("/healthz")
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((infra, app) => ScaleToZero(app));

// the search service returns OUR challenge-3 card URL (its one env-overridable value)
search.WithEnvironment("CARD3_URL", ReferenceExpression.Create($"{artifacts.GetEndpoint("http")}/cards/challenge3.mcp.json"));

// --- LOCAL ONLY: the mock DoH resolver + the walker (Azure uses real DNS + an external client) ---
if (builder.ExecutionContext.IsRunMode)
{
    var mockDoh = builder.AddProject<Projects.Ard_MockDoH>("mockdoh")
        .WithEnvironment("CATALOG2_URL", ReferenceExpression.Create($"{artifacts.GetEndpoint("http")}/c2/ai-catalog.json"))
        .WithEnvironment("SEARCH_URL", search.GetEndpoint("http"))
        .WithHttpHealthCheck("/healthz");

    builder.AddProject<Projects.Ard_Walker>("walker")
        .WithEnvironment("ARD_SCHEME", "http")
        .WithEnvironment("ARD_SEED_DOMAIN", ReferenceExpression.Create(
            $"{artifacts.GetEndpoint("http").Property(EndpointProperty.Host)}:{artifacts.GetEndpoint("http").Property(EndpointProperty.Port)}"))
        .WithEnvironment("ARD_DOH_RESOLVER", ReferenceExpression.Create($"{mockDoh.GetEndpoint("http")}/resolve"))
        .WaitFor(c1).WaitFor(c2).WaitFor(c3).WaitFor(search).WaitFor(artifacts).WaitFor(mockDoh)
        .WithExplicitStart();
}

builder.Build().Run();
