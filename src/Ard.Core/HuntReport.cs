namespace Ard.Core;

/// <summary>The outcome of one leg of the hunt.</summary>
public sealed class ChallengeResult
{
    public int Number { get; set; }
    public required string Mechanism { get; set; }
    public List<string> DiscoverySteps { get; set; } = new();
    public string? Endpoint { get; set; }
    public string? Tool { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? ResultText { get; set; }
    /// <summary>Challenge 3 only: the MCP App award HTML (the <c>ui://…/award.html</c> resource).</summary>
    public string? AwardHtml { get; set; }
    public string? AwardUri { get; set; }
}

/// <summary>The full traversal report (the proof: three mechanisms, three codes).</summary>
public sealed class HuntReport
{
    public string SeedDomain { get; set; } = "";
    public List<ChallengeResult> Challenges { get; set; } = new();
}

/// <summary>The challenge-3 award payload, fetched directly from an MCP Apps endpoint.</summary>
public sealed record AwardArtifact(string Code, string Message, string Html, string Uri, string Endpoint);
