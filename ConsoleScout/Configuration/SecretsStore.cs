namespace ConsoleScout.Configuration;

/// <summary>Writes API keys into secrets.json, the file <see cref="ConfigurationLoader"/> reads them from.
/// The file is kept owner-only.</summary>
internal static class SecretsStore
{
    /// <summary>The models a key can be set for: the sections of <see cref="ModelsSettings"/>.</summary>
    public static IReadOnlyList<string> ModelNames { get; } = ["main", "summarizer"];

    /// <summary>Sets models:&lt;model&gt;:apiKey, creating the file and the sections it needs.</summary>
    public static void SetModelKey(string model, string key)
    {
        string[] keys = ["models", model, "apiKey"];
        UserSettingsFile.Set(ConfigurationLoader.SecretsPath, keys, key, ownerOnly: true);
    }

    /// <summary>The environment variable that would override the stored key, when it is set.</summary>
    public static string? OverridingVariable(string model)
    {
        var variable = ConfigurationLoader.EnvironmentVariableFor($"Models:{model}:ApiKey");
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrEmpty(value) ? null : variable;
    }
}
