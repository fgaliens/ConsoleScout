using ConsoleScout.Contracts;
using Microsoft.Extensions.Configuration;

namespace ConsoleScout.Configuration;

/// <summary>Builds IConfiguration from these sources, each overriding the one before:
/// appsettings.json next to the executable, appsettings.Debug.json (Debug builds only), preferences.json and
/// secrets.json in the user profile, and environment variables prefixed CONSOLESCOUT_ (sections joined by a double
/// underscore). API keys live in the same tree as everything else, under the section that uses them.</summary>
internal static class ConfigurationLoader
{
    private const string EnvironmentPrefix = "CONSOLESCOUT_";

    /// <summary>The scraper's own variables. The scraper inherits this process's environment when started
    /// from here, so they are set alongside the CLI's - and would otherwise be read as unknown CLI settings.</summary>
    private const string ScraperEnvironmentPrefix = "CONSOLESCOUT_SCRAPER_";

    /// <summary>secrets.json in the user profile, alongside how git keeps its own credentials.
    /// Same shape as appsettings.json, holding only the keys.</summary>
    public static string SecretsPath { get; } = Path.Combine(ScraperDiscovery.DataDirectory, "secrets.json");

    /// <summary>preferences.json next to secrets.json: choices made from the command line, such as --no-donate.
    /// Same shape as appsettings.json.</summary>
    public static string PreferencesPath { get; } = Path.Combine(ScraperDiscovery.DataDirectory, "preferences.json");

    // AppContext.BaseDirectory resolves next to the executable under both dotnet run and a published build.
    private static string SettingsPath { get; } = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>Debug builds only: local overrides such as real model endpoints. Not tracked in git.</summary>
    private static string DebugSettingsPath { get; } = Path.Combine(AppContext.BaseDirectory, "appsettings.Debug.json");

    public static IConfiguration Build()
    {
        try
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile(SettingsPath, optional: true, reloadOnChange: false);
#if DEBUG
            builder.AddJsonFile(DebugSettingsPath, optional: true, reloadOnChange: false);
#endif
            var environment = EnvironmentSettings();
            return builder
                .AddJsonFile(PreferencesPath, optional: true, reloadOnChange: false)
                .AddJsonFile(SecretsPath, optional: true, reloadOnChange: false)
                .AddInMemoryCollection(environment)
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

    /// <summary>What AddEnvironmentVariables(prefix) would produce, minus the scraper's variables.
    /// CONSOLESCOUT_SCRAPER__X (double underscore) is still the CLI's own Scraper:X.</summary>
    private static IEnumerable<KeyValuePair<string, string?>> EnvironmentSettings()
    {
        foreach (System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            var name = (string)variable.Key;
            if (!name.StartsWith(EnvironmentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.StartsWith(ScraperEnvironmentPrefix, StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith(ScraperEnvironmentPrefix + "_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new(name[EnvironmentPrefix.Length..].Replace("__", ConfigurationPath.KeyDelimiter), (string?)variable.Value);
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
        {
            return key;
        }

        var segments = path.Split(':');
        var command = segments is ["Models", var model, "ApiKey"]
            ? $"Run ConsoleScout --set-key {model.ToLowerInvariant()}, set {EnvironmentVariableFor(path)}"
            : $"Set {EnvironmentVariableFor(path)}";

        throw new ConfigurationException(
            $"""
             Missing API key for {purpose} ({path}).
             {command}, or add it to {SecretsPath}:
               {Nest(path)}
             """);
    }

    /// <summary>The environment variable that sets <paramref name="path"/>, e.g. "Models:Main:ApiKey"
    /// -> CONSOLESCOUT_MODELS__MAIN__APIKEY.</summary>
    public static string EnvironmentVariableFor(string path) =>
        EnvironmentPrefix + path.Replace(":", "__").ToUpperInvariant();

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
