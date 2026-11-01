using ConsoleScout.Contracts;

namespace ConsoleScout.Agent;

/// <summary>An expected failure, returned to the caller as a <see cref="AgentError"/>.
/// <see cref="Code"/> is one of <see cref="AgentErrorCodes"/>.</summary>
internal sealed class AgentFailure(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode => Code switch
    {
        AgentErrorCodes.BadRequest => StatusCodes.Status400BadRequest,
        AgentErrorCodes.UnsupportedEngine => StatusCodes.Status404NotFound,
        AgentErrorCodes.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status502BadGateway,
    };
}
