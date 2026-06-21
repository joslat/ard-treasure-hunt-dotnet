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

/// <summary>
/// Shared challenge-3 fallback values — used only when a live structured result isn't available
/// (offline render, a non-conformant server). Centralized here so the code/message/URI aren't
/// duplicated as magic literals across the walker, the runner, and the award app.
/// </summary>
public static class AwardDefaults
{
    public const string Code = "1337 h4x0r";
    public const string Message = "Congrats, you solved the Agentic Resource Discovery (ARD) challenge!";
    public const string AwardUri = "ui://challenge-three/award.html";
}
