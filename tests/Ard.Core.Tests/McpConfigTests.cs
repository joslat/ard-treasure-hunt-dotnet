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
        // Arrange
        var path = WriteTemp("{\"servers\":{\"one\":{\"type\":\"http\",\"url\":\"https://x/mcp\"}}}");
        try
        {
            // Act
            var servers = McpConfig.Load(path);

            // Assert
            Assert.True(servers.ContainsKey("one"));
            Assert.True(servers["one"].IsHttp);
            Assert.Equal("https://x/mcp", servers["one"].Url);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_ReadsMcpServersSchema_ClaudeStyle()
    {
        // Arrange
        var path = WriteTemp("{\"mcpServers\":{\"two\":{\"type\":\"http\",\"url\":\"https://y/mcp\"}}}");
        try
        {
            // Act
            var servers = McpConfig.Load(path);

            // Assert
            Assert.True(servers.ContainsKey("two"));
            Assert.Equal("https://y/mcp", servers["two"].Url);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_StdioBridge_IsNotHttp()
    {
        // Arrange
        var path = WriteTemp("{\"mcpServers\":{\"bridge\":{\"command\":\"npx\",\"args\":[\"-y\",\"mcp-remote\",\"https://z/mcp\"]}}}");
        try
        {
            // Act
            var s = McpConfig.Load(path)["bridge"];

            // Assert
            Assert.False(s.IsHttp);
            Assert.Equal("npx", s.Command);
            Assert.Contains("mcp-remote", s.Args!);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenNeitherKeyPresent()
    {
        // Arrange
        var path = WriteTemp("{\"somethingElse\":{}}");

        // Act + Assert
        try { Assert.Empty(McpConfig.Load(path)); }
        finally { File.Delete(path); }
    }
}
