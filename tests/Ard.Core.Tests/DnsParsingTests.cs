using Ard.Core;

namespace Ard.Core.Tests;

/// <summary>Covers TXT unquoting and SRV → base-URL derivation in <see cref="DnsOverHttps"/> / <see cref="SrvRecord"/>.</summary>
public class DnsParsingTests
{
    [Theory]
    [InlineData("\"hello\"", "hello")]                            // strip surrounding quotes
    [InlineData("\"a\" \"b\"", "ab")]                             // multi-chunk TXT concatenation
    [InlineData("url=https://x/y.json", "url=https://x/y.json")]  // no quotes → returned unchanged
    [InlineData("  url=https://x  ", "url=https://x")]            // surrounding whitespace trimmed
    [InlineData("\"a\" \"b", "ab")]                               // unterminated trailing quote is flushed, not dropped
    public void Unquote_HandlesQuotingVariants(string input, string expected)
        => Assert.Equal(expected, DnsOverHttps.Unquote(input));

    [Theory]
    [InlineData(443, "host.example.net.", "https://host.example.net")]        // default https port dropped + trailing dot trimmed
    [InlineData(8443, "host.example.net.", "https://host.example.net:8443")]  // non-standard port preserved
    public void SrvRecord_ToBaseUrl_DropsDefaultPortAndTrailingDot(int port, string target, string expected)
        => Assert.Equal(expected, new SrvRecord(0, 0, port, target).ToBaseUrl());

    [Fact]
    public void SrvRecord_ToString_IsWireFormat()
        => Assert.Equal("0 5 443 ard-host.azurewebsites.net.",
            new SrvRecord(0, 5, 443, "ard-host.azurewebsites.net.").ToString());
}
