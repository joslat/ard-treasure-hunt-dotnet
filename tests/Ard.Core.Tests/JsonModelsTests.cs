using System.Text.Json;
using Ard.Core;

namespace Ard.Core.Tests;

/// <summary>
/// Covers the shared <see cref="Json"/> options: camelCase ARD/MCP payloads and PascalCase DNS-over-HTTPS
/// payloads must both round-trip through the case-insensitive options.
/// </summary>
public class JsonModelsTests
{
    [Fact]
    public void DohResponse_DeserializesPascalCaseKeys()
    {
        var json = "{\"Status\":0,\"Answer\":[{\"name\":\"_catalog._agents.x.\",\"type\":16,\"TTL\":3600,\"data\":\"url=https://x/ai-catalog.json\"}]}";
        var doh = JsonSerializer.Deserialize<DohResponse>(json, Json.Default)!;
        Assert.Equal(0, doh.Status);
        Assert.Single(doh.Answer!);
        Assert.Equal(16, doh.Answer![0].Type);
        Assert.Equal("url=https://x/ai-catalog.json", doh.Answer![0].Data);
    }

    [Fact]
    public void AiCatalog_DeserializesCamelCaseKeys()
    {
        var json = "{\"specVersion\":\"0.9\",\"entries\":[{\"identifier\":\"urn:ai:x\",\"type\":\"application/mcp-server+json\",\"url\":\"https://x/card.json\"}]}";
        var cat = JsonSerializer.Deserialize<AiCatalog>(json, Json.Default)!;
        Assert.Single(cat.Entries);
        Assert.Equal("https://x/card.json", cat.Entries[0].Url);
        Assert.Equal("application/mcp-server+json", cat.Entries[0].Type);
    }

    [Fact]
    public void McpServerCard_DeserializesEndpointAndTools()
    {
        var json = "{\"type\":\"application/mcp-server+json\",\"endpoint\":{\"url\":\"https://x/mcp\",\"transport\":\"streamable-http\"},\"tools\":[{\"name\":\"reveal_challenge_one\"}]}";
        var card = JsonSerializer.Deserialize<McpServerCard>(json, Json.Default)!;
        Assert.Equal("https://x/mcp", card.Endpoint!.Url);
        Assert.Equal("streamable-http", card.Endpoint!.Transport);
        Assert.Single(card.Tools);
        Assert.Equal("reveal_challenge_one", card.Tools[0].Name);
    }

    [Fact]
    public void SearchRequest_SerializesToCamelCase()
    {
        var json = JsonSerializer.Serialize(
            new SearchRequest { Query = new SearchQuery { Text = "treasure" }, PageSize = 5 }, Json.Default);
        Assert.Contains("\"query\"", json);
        Assert.Contains("\"text\"", json);
        Assert.Contains("\"pageSize\"", json);
    }

    [Fact]
    public void SearchResponse_DeserializesResultsWithScore()
    {
        var json = "{\"results\":[{\"identifier\":\"urn:ai:x\",\"type\":\"application/mcp-server+json\",\"url\":\"https://x.json\",\"score\":100}],\"referrals\":[],\"pageToken\":null}";
        var resp = JsonSerializer.Deserialize<SearchResponse>(json, Json.Default)!;
        Assert.Single(resp.Results);
        Assert.Equal(100, resp.Results[0].Score);
        Assert.Equal("https://x.json", resp.Results[0].Url);
    }
}
