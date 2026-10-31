using Microsoft.Extensions.Configuration;

namespace ConsoleScout.Configuration;

/// <summary>Builds IConfiguration from three sources, each overriding the one before:
/// appsettings.json next to the executable, secrets.json in the user profile, and
/// environment variables prefixed CONSOLESCOUT_ (sections joined by a double underscore).
/// API keys live in the same tree as everything else, under the section that uses them.</summary>
internal static class ConfigurationLoader
{
    private const string EnvironmentPrefix = "CONSOLESCOUT_";

    /// <summary>secrets.json in the user profile, alongside how git keeps its own credentials.
    /// Same shape as appsettings.json, holding only the keys.</summary>
    public static string SecretsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".consolescout",
        "secrets.json");

    // AppContext.BaseDirectory resolves next to the executable under both dotnet run and a published build.
    private static string SettingsPath { get; } = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static IConfiguration Build()
    {
        try
        {
            return new ConfigurationBuilder()
                .AddJsonFile(SettingsPath, optional: true, reloadOnChange: false)
                .AddJsonFile(SecretsPath, optional: true, reloadOnChange: false)
                .AddEnvironmentVariables(EnvironmentPrefix)
                .Build();
        }
        catch (InvalidDataException ex)
        {
            // The JSON provider names the file in the message.
            throw new ConfigurationException($"{ex.Message}{Environment.NewLine}{ex.InnerException?.Message}");
        }
        catch (IOException ex)
        {
            throw new ConfigurationException(ex.Message);
        }
    }

    /// <summary>Unknown keys are rejected, so a misspelled or renamed setting fails loudly
    /// instead of silently falling back to its default.</summary>
    public static AppSettings Bind(IConfiguration configuration)
    {
        try
        {
            return configuration.Get<AppSettings>(options => options.ErrorOnUnknownConfiguration = true)
                ?? new AppSettings();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConfigurationException($"Invalid settings.{Environment.NewLine}{ex.Message}");
        }
    }

    /// <summary>Called when the key is about to be used, so a disabled feature never fails
    /// over a key it does not need.</summary>
    /// <param name="key">The bound value.</param>
    /// <param name="path">Its configuration path, e.g. "Models:Main:ApiKey".</param>
    /// <param name="purpose">What the key is for, in words.</param>
    public static string RequireKey(string? key, string path, string purpose)
    {
        if (!string.IsNullOrWhiteSpace(key))
            return key;

        var variable = EnvironmentPrefix + path.Replace(":", "__").ToUpperInvariant();

        throw new ConfigurationException(
            $"""
             Missing API key for {purpose} ({path}).
             Set {variable}, or add it to {SecretsPath}:
               {Nest(path)}
             """);
    }

    // "Models:Main:ApiKey" -> { "models": { "main": { "apiKey": "..." } } }
    private static string Nest(string path)
    {
        var segments = path.Split(':');
        var json = "\"...\"";

        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var name = char.ToLowerInvariant(segments[i][0]) + segments[i][1..];
            json = $"{{ \"{name}\": {json} }}";
        }

        return json;
    }
}
