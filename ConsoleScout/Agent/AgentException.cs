using ConsoleScout.Contracts;

namespace ConsoleScout.Agent;

/// <summary>The agent answered, with an error. <see cref="Code"/> is one of <see cref="AgentErrorCodes"/>.</summary>
internal sealed class AgentException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>The engine or site refused this client; another one may still answer.</summary>
    public bool IsBlocked => Code is AgentErrorCodes.Captcha or AgentErrorCodes.Blocked;
}
