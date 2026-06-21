using System.Text.Json;

namespace Ard.Core;

/// <summary>One server entry from an <c>mcp.json</c> / <c>claude_desktop_config.json</c>.</summary>
public sealed class McpConfigServer
{
    public string? Type { get; set; }
    public string? Url { get; set; }
    public string? Command { get; set; }
    public List<string>? Args { get; set; }

    /// <summary>True for a remote streamable-HTTP server this toolkit can connect to directly.</summary>
    public bool IsHttp => Url is not null && (Type is null || Type.Equals("http", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Loads MCP server definitions from a config file, tolerant of both schemas:
/// VS Code / Visual Studio use <c>{"servers": {…}}</c>; Claude uses <c>{"mcpServers": {…}}</c>.
/// </summary>
public static class McpConfig
{
    public static Dictionary<string, McpConfigServer> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var node = root.TryGetProperty("servers", out var s) ? s
                 : root.TryGetProperty("mcpServers", out var m) ? m
                 : default;
        var result = new Dictionary<string, McpConfigServer>(StringComparer.OrdinalIgnoreCase);
        if (node.ValueKind != JsonValueKind.Object) return result;
        foreach (var prop in node.EnumerateObject())
        {
            var server = prop.Value.Deserialize<McpConfigServer>(Json.Default);
            if (server is not null) result[prop.Name] = server;
        }
        return result;
    }

    /// <summary>Walk up from a starting directory to find the nearest <c>mcp.json</c> (or <c>.vscode/mcp.json</c>).</summary>
    public static string? Find(string startDir, int maxDepth = 8)
    {
        var dir = new DirectoryInfo(startDir);
        for (var i = 0; i < maxDepth && dir is not null; i++, dir = dir.Parent)
        {
            foreach (var rel in new[] { "mcp.json", Path.Combine(".vscode", "mcp.json") })
            {
                var candidate = Path.Combine(dir.FullName, rel);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }
}
