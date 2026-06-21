using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ard.Core;

/// <summary>
/// A thin, dependency-free MCP client speaking <b>streamable-HTTP</b> (JSON-RPC 2.0),
/// faithful to the MCP streamable-HTTP transport rules. Deliberately hand-rolled so the documented
/// pitfalls are handled explicitly and visibly:
/// <list type="bullet">
///   <item>the <c>Accept</c> header carries <b>both</b> <c>application/json</c> and <c>text/event-stream</c>
///         (single value yields <c>-32000 Not Acceptable</c>);</item>
///   <item>responses may be SSE-framed (<c>data:</c> lines) — parsed back to JSON;</item>
///   <item>bodies are decoded as <b>UTF-8</b> (trophy emoji / em-dashes mojibake otherwise);</item>
///   <item>no <c>Mcp-Session-Id</c> is issued — the client stays stateless but echoes one if it ever appears.</item>
/// </list>
/// </summary>
public sealed class McpHttpClient
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private int _id;
    private string? _sessionId;

    public string Endpoint => _endpoint;
    public string? ProtocolVersion { get; private set; }
    public string? ServerName { get; private set; }

    public McpHttpClient(HttpClient http, string endpoint)
    {
        _http = http;
        _endpoint = endpoint;
    }

    /// <summary>Full opening handshake: <c>initialize</c> + <c>notifications/initialized</c>.</summary>
    public async Task InitializeAsync(string clientName = "ard-dotnet-client", CancellationToken ct = default)
    {
        var result = await RequestAsync("initialize", new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = clientName, version = "1.0.0" },
        }, ct);

        if (result.TryGetProperty("protocolVersion", out var pv)) ProtocolVersion = pv.GetString();
        if (result.TryGetProperty("serverInfo", out var si) && si.TryGetProperty("name", out var sn))
            ServerName = sn.GetString();

        await NotifyAsync("notifications/initialized", null, ct);
    }

    /// <summary><c>tools/list</c> → the server's tool definitions (name, description, schemas, _meta).</summary>
    public async Task<JsonElement> ListToolsAsync(CancellationToken ct = default)
        => await RequestAsync("tools/list", new { }, ct);

    /// <summary><c>tools/call</c> → the tool result (<c>content[]</c> plus optional <c>structuredContent</c>).</summary>
    public async Task<JsonElement> CallToolAsync(string name, object? arguments = null, CancellationToken ct = default)
        => await RequestAsync("tools/call", new { name, arguments = arguments ?? new { } }, ct);

    /// <summary><c>resources/list</c> → declared resources (e.g. the MCP App <c>ui://…</c> URI).</summary>
    public async Task<JsonElement> ListResourcesAsync(CancellationToken ct = default)
        => await RequestAsync("resources/list", new { }, ct);

    /// <summary><c>resources/read</c> → resource contents; returns the first content's text (the award HTML).</summary>
    public async Task<string> ReadResourceTextAsync(string uri, CancellationToken ct = default)
    {
        var result = await RequestAsync("resources/read", new { uri }, ct);
        if (result.TryGetProperty("contents", out var contents) && contents.GetArrayLength() > 0)
        {
            var first = contents[0];
            if (first.TryGetProperty("text", out var text))
                return text.GetString() ?? "";
        }
        throw new ArdException($"resources/read for '{uri}' returned no text content.");
    }

    // ----- transport -----

    /// <summary>Send a JSON-RPC request (with id) and return its <c>result</c> element. Throws on JSON-RPC error.</summary>
    private async Task<JsonElement> RequestAsync(string method, object? @params, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _id);
        using var doc = await PostAsync(method, @params, id, ct)
            ?? throw new ArdException($"MCP '{method}' returned an empty body.");
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var error))
        {
            var code = error.TryGetProperty("code", out var c)
                ? (c.ValueKind == JsonValueKind.Number ? c.GetRawText() : c.ToString())
                : "?";
            var msg = error.TryGetProperty("message", out var m) ? m.GetString() : "(no message)";
            throw new ArdException($"MCP '{method}' error {code}: {msg}");
        }
        if (root.TryGetProperty("result", out var result))
            return result.Clone();
        throw new ArdException($"MCP '{method}' response had neither 'result' nor 'error'.");
    }

    /// <summary>Send a JSON-RPC notification (no id, no response expected).</summary>
    private async Task NotifyAsync(string method, object? @params, CancellationToken ct)
        => (await PostAsync(method, @params, id: null, ct))?.Dispose();

    private async Task<JsonDocument?> PostAsync(string method, object? @params, int? id, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["method"] = method };
        if (id is not null) payload["id"] = id;
        if (@params is not null) payload["params"] = @params;

        using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json.Default), Encoding.UTF8, "application/json");
        // BOTH media types are required, or the server replies -32000 Not Acceptable.
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (_sessionId is not null) req.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);

        // Capture a session id if one is ever issued (these servers don't, but be defensive).
        if (resp.Headers.TryGetValues("Mcp-Session-Id", out var sids))
            _sessionId = sids.FirstOrDefault() ?? _sessionId;

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length == 0) return null; // e.g. 202 Accepted for a notification

        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
        var json = ExtractJson(contentType, bytes);

        if (!resp.IsSuccessStatusCode && string.IsNullOrWhiteSpace(json))
            throw new ArdException($"MCP '{method}' HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}.");

        return string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json);
    }

    /// <summary>Decode the body as UTF-8 and, if SSE-framed, reassemble the JSON-RPC payload from <c>data:</c> lines.</summary>
    private static string ExtractJson(string contentType, byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (!contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
            return text;

        // Reassemble per SSE: a blank line ends an event; within an event, multiple
        // data: lines join with '\n'. Build one joined-data string per event so we never
        // splice together separate events (which would yield invalid JSON).
        var events = new List<string>();
        var current = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (current.Count > 0) { events.Add(string.Join("\n", current)); current.Clear(); }
                continue;
            }
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var d = line.Substring(5);
                if (d.StartsWith(" ", StringComparison.Ordinal)) d = d.Substring(1);
                current.Add(d);
            }
        }
        if (current.Count > 0) events.Add(string.Join("\n", current));
        if (events.Count == 0) return text;

        // The JSON-RPC response is the last event that parses as JSON; iterate newest-first
        // so the final message wins, and fall back to the last event if none parse.
        for (var i = events.Count - 1; i >= 0; i--)
        {
            try { using var _ = JsonDocument.Parse(events[i]); return events[i]; }
            catch (JsonException) { }
        }
        return events[^1];
    }
}
