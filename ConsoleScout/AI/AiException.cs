namespace ConsoleScout.AI;

/// <summary>The model endpoint failed for a reason worth showing the user as is.</summary>
internal sealed class AiException(string message) : Exception(message);