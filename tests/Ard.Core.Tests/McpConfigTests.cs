using Ard.Core;

namespace Ard.Core.Tests;

/// <summary>Covers <see cref="McpConfig.Load"/> tolerating both the VS Code (<c>servers</c>) and Claude (<c>mcpServers</c>) schemas.</summary>
public class McpConfigTests
{
    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), "ard-cfg-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_ReadsServersSchema_VsCodeStyle()
    {
        var path = WriteTemp("{\"servers\":{\"one\":{\"type\":\"http\",\"url\":\"https://x/mcp\"}}}");
        try
        {
            var servers = McpConfig.Load(path);
            Assert.True(servers.ContainsKey("one"));
            Assert.True(servers["one"].IsHttp);
            Assert.Equal("https://x/mcp", servers["one"].Url);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_ReadsMcpServersSchema_ClaudeStyle()
    {
        var path = WriteTemp("{\"mcpServers\":{\"two\":{\"type\":\"http\",\"url\":\"https://y/mcp\"}}}");
        try
        {
            var servers = McpConfig.Load(path);
            Assert.True(servers.ContainsKey("two"));
            Assert.Equal("https://y/mcp", servers["two"].Url);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_StdioBridge_IsNotHttp()
    {
        var path = WriteTemp("{\"mcpServers\":{\"bridge\":{\"command\":\"npx\",\"args\":[\"-y\",\"mcp-remote\",\"https://z/mcp\"]}}}");
        try
        {
            var s = McpConfig.Load(path)["bridge"];
            Assert.False(s.IsHttp);
            Assert.Equal("npx", s.Command);
            Assert.Contains("mcp-remote", s.Args!);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenNeitherKeyPresent()
    {
        var path = WriteTemp("{\"somethingElse\":{}}");
        try { Assert.Empty(McpConfig.Load(path)); }
        finally { File.Delete(path); }
    }
}
