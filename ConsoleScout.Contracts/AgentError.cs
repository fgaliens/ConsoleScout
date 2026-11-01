namespace ConsoleScout.Contracts;

/// <summary>Body of every non-2xx response. <see cref="Code"/> is one of <see cref="AgentErrorCodes"/>.</summary>
public sealed record AgentError(string Code, string Message);
