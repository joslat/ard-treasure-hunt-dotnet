using System.Text;
using System.Text.Json;
using Ard.Core;

namespace Ard.Core.Tests;

/// <summary>
/// Covers <see cref="McpHttpClient.ExtractJson"/> — the streamable-HTTP SSE reassembly that turns
/// <c>event:</c>/<c>data:</c> frames back into a single JSON-RPC message.
/// </summary>
public class SseReassemblyTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void PlainJson_IsReturnedVerbatim()
    {
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}";
        Assert.Equal(json, McpHttpClient.ExtractJson("application/json", B(json)));
    }

    [Fact]
    public void Sse_SingleEvent_IsUnwrapped()
    {
        var body = "event: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":7,\"result\":{}}\n\n";
        var json = McpHttpClient.ExtractJson("text/event-stream", B(body));
        Assert.Equal(7, JsonDocument.Parse(json).RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Sse_DataPrefixWithoutSpace_IsHandled()
    {
        var json = McpHttpClient.ExtractJson("text/event-stream", B("data:{\"id\":3}\n\n"));
        Assert.Equal(3, JsonDocument.Parse(json).RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Sse_MultipleDataLinesInOneEvent_AreJoinedWithNewline()
    {
        var body = "event: message\ndata: {\"x\":\ndata: 1}\n\n";
        var json = McpHttpClient.ExtractJson("text/event-stream", B(body));
        Assert.Equal(1, JsonDocument.Parse(json).RootElement.GetProperty("x").GetInt32());
    }

    [Fact]
    public void Sse_MultipleEvents_ReturnsLastParseableEvent()
    {
        var body = "event: message\ndata: {\"id\":1}\n\nevent: message\ndata: {\"id\":2}\n\n";
        var json = McpHttpClient.ExtractJson("text/event-stream", B(body));
        Assert.Equal(2, JsonDocument.Parse(json).RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Sse_DoesNotSpliceSeparateEventsIntoInvalidJson()
    {
        // Two complete objects in separate events. A naive flat join of all data: lines would yield
        // "{...}\n{...}" — NOT valid JSON. The event-boundary parser must return one valid object.
        var body = "data: {\"id\":1}\n\ndata: {\"id\":2}\n\n";
        var json = McpHttpClient.ExtractJson("text/event-stream", B(body));
        using var doc = JsonDocument.Parse(json); // must not throw
        Assert.Equal(2, doc.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Sse_CrlfLineEndings_AreHandled()
    {
        var body = "event: message\r\ndata: {\"id\":9}\r\n\r\n";
        var json = McpHttpClient.ExtractJson("text/event-stream", B(body));
        Assert.Equal(9, JsonDocument.Parse(json).RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Sse_Utf8Payload_IsDecodedCorrectly()
    {
        // The trophy emoji is the classic mojibake trap — confirm bytes decode as UTF-8.
        var json = McpHttpClient.ExtractJson("text/event-stream", B("data: {\"emoji\":\"🏆\"}\n\n"));
        Assert.Equal("🏆", JsonDocument.Parse(json).RootElement.GetProperty("emoji").GetString());
    }

    [Fact]
    public void Sse_NoDataLines_FallsBackToRawText()
    {
        var body = "event: ping\n\n";
        Assert.Equal(body, McpHttpClient.ExtractJson("text/event-stream", B(body)));
    }
}
