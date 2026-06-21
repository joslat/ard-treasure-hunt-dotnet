using System.Text.Json;
using Ard.Core;

namespace Ard.Core.Tests;

/// <summary>
/// Covers the two completion-code extraction paths in <see cref="HuntRunner"/>:
/// the structured <c>structuredContent.code</c> and the text-fallback regex.
/// </summary>
public class CodeExtractionTests
{
    [Theory]
    [InlineData("preamble Completion code: \"Rip and tear!\" trailing hint", "Rip and tear!")]
    [InlineData("completion CODE:   \"Sean Astrakhan\"", "Sean Astrakhan")] // case-insensitive + flexible whitespace
    public void ExtractCodeFromText_PullsQuotedCode(string text, string expected)
        => Assert.Equal(expected, HuntRunner.ExtractCodeFromText(text));

    [Theory]
    [InlineData("no completion code present here")]
    [InlineData("")]
    [InlineData(null)]
    public void ExtractCodeFromText_ReturnsNull_WhenNoMatch(string? text)
        => Assert.Null(HuntRunner.ExtractCodeFromText(text));

    [Fact]
    public void GetStructuredCode_ReadsStringCodeAndMessage()
    {
        using var doc = JsonDocument.Parse("{\"structuredContent\":{\"code\":\"1337 h4x0r\",\"message\":\"Congrats!\"}}");
        var (code, message) = HuntRunner.GetStructuredCode(doc.RootElement);
        Assert.Equal("1337 h4x0r", code);
        Assert.Equal("Congrats!", message);
    }

    [Fact]
    public void GetStructuredCode_IgnoresNonStringCode_InsteadOfThrowing()
    {
        // A numeric code must fall back to null (so the caller drops to the regex), not throw.
        using var doc = JsonDocument.Parse("{\"structuredContent\":{\"code\":1337,\"message\":\"hi\"}}");
        var (code, message) = HuntRunner.GetStructuredCode(doc.RootElement);
        Assert.Null(code);
        Assert.Equal("hi", message);
    }

    [Fact]
    public void GetStructuredCode_ReturnsNulls_WhenNoStructuredContent()
    {
        using var doc = JsonDocument.Parse("{\"content\":[{\"type\":\"text\",\"text\":\"x\"}]}");
        var (code, message) = HuntRunner.GetStructuredCode(doc.RootElement);
        Assert.Null(code);
        Assert.Null(message);
    }
}
