namespace ConsoleScout.Agent;

/// <summary>The agent cannot be reached or started.</summary>
internal sealed class AgentUnavailableException(string message) : Exception(message);
