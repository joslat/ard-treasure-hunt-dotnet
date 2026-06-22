using System.Text;
using System.Text.Json;
using Ard.Core;

// ARD Treasure Hunt — headless walker + reproduction toolkit.
//
//   walk   [--domain d] [--out dir] [--name n]   Walk the whole trail from the seed domain (default).
//   dns    <domain>                              Show the ARD TXT + SRV records for a domain.
//   fetch  <url>                                 GET a static artifact and pretty-print it.
//   mcp    <endpoint-url>                        Minimal MCP client: initialize, list, call tools.
//   servers [--config mcp.json]                  Connect to every server in an mcp.json.
//   award  [--domain d] [--endpoint u] [--out dir]   Fetch + save the challenge-3 MCP App award HTML.

const string DefaultDomain = "nullpointer.se";
const string DefaultName = "your name?";

Console.OutputEncoding = Encoding.UTF8;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("ard-dotnet-toolkit/1.0");

var argList = args.ToList();
var command = argList.FirstOrDefault()?.ToLowerInvariant() ?? "walk";

try
{
    switch (command)
    {
        case "walk": await WalkAsync(); break;
        case "dns": await DnsAsync(Positional(1) ?? DefaultDomain); break;
        case "fetch": await FetchAsync(Positional(1) ?? throw new ArgumentException("usage: fetch <url>")); break;
        case "mcp": await McpAsync(Positional(1) ?? throw new ArgumentException("usage: mcp <endpoint-url>")); break;
        case "servers": await ServersAsync(); break;
        case "award": await AwardAsync(); break;
        case "-h" or "--help" or "help": PrintHelp(); break;
        default:
            // Allow `walk` flags without the verb, e.g. `Ard.Walker --name "..."`.
            if (command.StartsWith('-')) { await WalkAsync(); break; }
            Console.Error.WriteLine($"Unknown command '{command}'.");
            PrintHelp();
            return 1;
    }
    return 0;
}
catch (Exception ex)
{
    WriteColor(ConsoleColor.Red, $"\n✖ {ex.Message}");
    if (ex.InnerException is not null) Console.Error.WriteLine($"   ↳ {ex.InnerException.Message}");
    return 2;
}

// ----------------------------------------------------------------------------- walk

async Task WalkAsync()
{
    var domain = Option("--domain") ?? Environment.GetEnvironmentVariable("ARD_SEED_DOMAIN") ?? DefaultDomain;
    var outDir = ResolveOutDir(Option("--out") ?? "ard-output");
    var name = Option("--name") ?? DefaultName;

    // Local self-hosted mode (set by the Aspire AppHost): http scheme + a local mock-DoH resolver.
    var scheme = Environment.GetEnvironmentVariable("ARD_SCHEME") ?? "https";
    var dohEnv = Environment.GetEnvironmentVariable("ARD_DOH_RESOLVER");
    var dohResolvers = string.IsNullOrWhiteSpace(dohEnv) ? null : new[] { dohEnv };

    Banner($"ARD Treasure Hunt — walking the trail from  {scheme}://{domain}");

    var runner = new HuntRunner(http, Console.WriteLine, scheme, dohResolvers);
    var report = await runner.RunAsync(domain);

    // Persist the proof + artifacts.
    Directory.CreateDirectory(outDir);
    var reportPath = Path.Combine(outDir, "hunt-report.json");
    await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, Json.Pretty), Encoding.UTF8);

    string? awardPath = null;
    var award = report.Challenges.FirstOrDefault(c => c.AwardHtml is not null);
    if (award?.AwardHtml is not null)
    {
        awardPath = Path.Combine(outDir, "award.html");
        await File.WriteAllTextAsync(awardPath, award.AwardHtml, new UTF8Encoding(false));
    }

    // Summary.
    Console.WriteLine();
    Banner("🏆  Treasure reached — completion codes");
    foreach (var c in report.Challenges)
    {
        WriteColor(ConsoleColor.Cyan, $"  Challenge {c.Number}  ·  {c.Mechanism}");
        Console.WriteLine($"            tool   : {c.Tool}");
        Console.WriteLine($"            endpoint: {c.Endpoint}");
        Console.Write("            code   : ");
        WriteColor(ConsoleColor.Yellow, $"{c.Code}");
    }
    Console.WriteLine();
    Console.WriteLine($"  Report saved : {reportPath}");
    if (awardPath is not null)
    {
        Console.WriteLine($"  Award HTML   : {awardPath}");
        WriteColor(ConsoleColor.DarkGray,
            $"  Render it as a PNG with:  dotnet run --project src/Ard.AwardApp -- --screenshot \"{Path.Combine(outDir, "award.png")}\" --name \"{name}\"");
    }
}

// ------------------------------------------------------------------------------ dns

async Task DnsAsync(string domain)
{
    Banner($"ARD DNS records for  {domain}");
    var dns = new DnsOverHttps(http);

    var catalogName = $"_catalog._agents.{domain}";
    Console.WriteLine($"TXT  {catalogName}");
    foreach (var txt in await dns.ResolveTxtAsync(catalogName))
        WriteColor(ConsoleColor.Yellow, $"   {txt}");

    var searchName = $"_search._agents.{domain}";
    Console.WriteLine($"\nSRV  {searchName}");
    foreach (var srv in await dns.ResolveSrvAsync(searchName))
        WriteColor(ConsoleColor.Yellow, $"   {srv}   →  {srv.ToBaseUrl()}");
}

// ---------------------------------------------------------------------------- fetch

async Task FetchAsync(string url)
{
    var resolver = new ArdResolver(http);
    var body = await resolver.GetStringUtf8Async(url);
    try { Console.WriteLine(Json.Stringify(JsonDocument.Parse(body).RootElement)); }
    catch (JsonException) { Console.WriteLine(body); }
}

// ------------------------------------------------------------------------------ mcp

async Task McpAsync(string endpoint)
{
    Banner($"Minimal MCP client  →  {endpoint}");
    var mcp = new McpHttpClient(http, endpoint);
    await mcp.InitializeAsync();
    WriteColor(ConsoleColor.DarkGray, $"  initialized · server '{mcp.ServerName}' · protocol {mcp.ProtocolVersion}");

    var tools = await mcp.ListToolsAsync();
    var names = new List<string>();
    if (tools.TryGetProperty("tools", out var arr))
        foreach (var t in arr.EnumerateArray())
            if (t.TryGetProperty("name", out var n) && n.GetString() is { } name)
            {
                names.Add(name);
                var desc = t.TryGetProperty("description", out var d) ? d.GetString() : "";
                Console.WriteLine($"\n  tool: {name}  —  {desc}");
            }

    foreach (var name in names)
    {
        var result = await mcp.CallToolAsync(name);
        WriteColor(ConsoleColor.Green, $"\n  {name} →");
        Console.WriteLine(Indent(Json.Stringify(result), "    "));
    }
}

// -------------------------------------------------------------------------- servers

async Task ServersAsync()
{
    var configPath = Option("--config") ?? McpConfig.Find(Directory.GetCurrentDirectory())
        ?? McpConfig.Find(AppContext.BaseDirectory)
        ?? throw new ArdException("No mcp.json found. Pass --config <path>.");
    Banner($"MCP servers from  {configPath}");

    var servers = McpConfig.Load(configPath);
    foreach (var (name, cfg) in servers)
    {
        if (!cfg.IsHttp)
        {
            WriteColor(ConsoleColor.DarkGray, $"\n  {name}: (non-http: {cfg.Command} {string.Join(' ', cfg.Args ?? new())}) — skipped");
            continue;
        }
        WriteColor(ConsoleColor.Cyan, $"\n  {name}  →  {cfg.Url}");
        try
        {
            var mcp = new McpHttpClient(http, cfg.Url!);
            await mcp.InitializeAsync();
            var tools = await mcp.ListToolsAsync();
            if (tools.TryGetProperty("tools", out var arr))
                foreach (var t in arr.EnumerateArray())
                    if (t.TryGetProperty("name", out var n) && n.GetString() is { } tool)
                    {
                        var result = await mcp.CallToolAsync(tool);
                        var sc = result.TryGetProperty("structuredContent", out var s) && s.TryGetProperty("code", out var scode)
                            ? scode.GetString() : null;
                        var text = result.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array && c.GetArrayLength() > 0
                            && c[0].TryGetProperty("text", out var tx) ? tx.GetString() : "";
                        WriteColor(ConsoleColor.Yellow, $"      {tool} → {sc ?? Trim(text)}");
                    }
        }
        catch (Exception ex) { WriteColor(ConsoleColor.Red, $"      ✖ {ex.Message}"); }
    }
}

static string Trim(string? s) => s is null ? "" : (s.Length > 90 ? s[..90] + "…" : s);

// ---------------------------------------------------------------------------- award

async Task AwardAsync()
{
    var domain = Option("--domain") ?? DefaultDomain;
    var outDir = ResolveOutDir(Option("--out") ?? "ard-output");
    var endpoint = Option("--endpoint");

    var runner = new HuntRunner(http, Console.WriteLine);

    if (endpoint is null)
    {
        var resolver = new ArdResolver(http);
        Banner($"Discovering the challenge-3 MCP App via ARD ({domain})");
        var (search, _, registry) = await resolver.ResolveDnsRegistryAsync(domain, "treasure hunt challenge");
        Console.WriteLine($"  SRV → registry {registry}");
        var top = search.Results.OrderByDescending(s => s.Score).First();
        var card = await resolver.FetchCardAsync(top.Url!);
        endpoint = card.Endpoint?.Url ?? throw new ArdException("Card had no endpoint.");
        Console.WriteLine($"  endpoint {endpoint}");
    }

    var award = await runner.FetchAwardAsync(endpoint);
    Directory.CreateDirectory(outDir);
    var path = Path.Combine(outDir, "award.html");
    await File.WriteAllTextAsync(path, award.Html, new UTF8Encoding(false));

    WriteColor(ConsoleColor.Yellow, $"\n  code: {award.Code}");
    Console.WriteLine($"  message: {award.Message}");
    Console.WriteLine($"  award HTML ({award.Html.Length:N0} chars) saved: {path}");
}

// --------------------------------------------------------------------------- shared

string? Positional(int index)
{
    var positionals = argList.Skip(1).Where(a => !a.StartsWith('-')).ToList();
    return index - 1 >= 0 && index - 1 < positionals.Count ? positionals[index - 1] : null;
}

string? Option(string flag)
{
    var i = argList.IndexOf(flag);
    return i >= 0 && i + 1 < argList.Count ? argList[i + 1] : null;
}

static string ResolveOutDir(string given)
    => Path.IsPathRooted(given) ? given : Path.GetFullPath(given);

static void Banner(string text)
{
    var line = new string('─', Math.Max(text.Length, 10));
    WriteColor(ConsoleColor.White, $"\n{text}\n{line}");
}

static void WriteColor(ConsoleColor color, string text)
{
    var prev = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ForegroundColor = prev;
}

static string Indent(string s, string pad) =>
    string.Join('\n', s.Split('\n').Select(l => pad + l));

static void PrintHelp()
{
    Console.WriteLine("""
        ARD Treasure Hunt — .NET walker & reproduction toolkit

          walk   [--domain d] [--out dir] [--name n]   Walk the whole trail (default command)
          dns    <domain>                              Show ARD TXT + SRV records
          fetch  <url>                                 GET a static artifact, pretty-print
          mcp    <endpoint-url>                        Minimal MCP client: init/list/call
          servers [--config mcp.json]                  Connect to every server in an mcp.json
          award  [--domain d] [--endpoint u] [--out d] Fetch + save the MCP App award

        Examples
          dotnet run --project src/Ard.Walker
          dotnet run --project src/Ard.Walker -- dns nullpointer.se
          dotnet run --project src/Ard.Walker -- mcp https://<mcp-server-host>/mcp
        """);
}
