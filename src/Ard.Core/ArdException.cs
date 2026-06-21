namespace Ard.Core;

/// <summary>Raised when an ARD discovery step or MCP call fails.</summary>
public sealed class ArdException : Exception
{
    public ArdException(string message, Exception? inner = null) : base(message, inner) { }
}
