namespace ConsoleScout.Agent.Hosting;

/// <summary>The secret every request must carry, generated anew on each start.</summary>
internal sealed record AccessToken(string Value);
