using System.Text.Json;

namespace Ard.Core;

/// <summary>Shared System.Text.Json options used across the toolkit.</summary>
public static class Json
{
    /// <summary>
    /// camelCase out, case-insensitive in. ARD/MCP payloads are camelCase, while
    /// DNS-over-HTTPS uses PascalCase keys (Status/Answer/TTL) — case-insensitive matching covers both.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static readonly JsonSerializerOptions Pretty = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>Pretty-print any JsonElement (used by the console <c>fetch</c> command).</summary>
    public static string Stringify(JsonElement element) =>
        JsonSerializer.Serialize(element, Pretty);
}
