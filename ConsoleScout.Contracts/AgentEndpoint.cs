namespace ConsoleScout.Contracts;

/// <summary>Contents of agent.json: written by a running agent, read by the CLI to find it.</summary>
public sealed record AgentEndpoint(string Url, string Token, int Pid);
