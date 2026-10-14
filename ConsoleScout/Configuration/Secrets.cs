namespace ConsoleScout.Configuration;

/// <summary>Shape of secrets.json in the user profile. Both keys are optional there:
/// an environment variable can supply either one instead. Named after the roles from
/// appsettings.json rather than after providers, because both are swappable.</summary>
internal sealed record SecretsFile
{
    public string? Model { get; init; }

    public string? Search { get; init; }
}

/// <summary>Configuration is missing or unusable, with a message meant for the user.</summary>
internal sealed class ConfigurationException(string message) : Exception(message);
