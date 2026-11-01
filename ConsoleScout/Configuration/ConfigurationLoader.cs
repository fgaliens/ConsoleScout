using ConsoleScout.Contracts;
using ConsoleScout.Session;
using Microsoft.Extensions.Configuration;

namespace ConsoleScout.Configuration;

/// <summary>Builds IConfiguration from these sources, each overriding the one before:
/// appsettings.json next to the executable (the shipped defaults), appsettings.Debug.json (Debug builds only),
/// then in ~/.consolescout: settings.json (the user's settings), preferences.json and secrets.json, and finally
/// environment variables prefixed CONSOLESCOUT_ (sections joined by a double underscore). API keys live in the
/// same tree as everything else, under the section that uses them.</summary>
internal static class ConfigurationLoader
{
    private const string EnvironmentPrefix = "CONSOLESCOUT_";

    /// <summary>The agent's own variables. The agent inherits this process's environment when started
    /// from here, so they are set alongside the CLI's - and would otherwise be read as unknown CLI settings.</summary>
    private const string AgentEnvironmentPrefix = "CONSOLESCOUT_AGENT_";

    /// <summary>secrets.json in the user profile, alongside how git keeps its own credentials.
    /// Same shape as appsettings.json, holding only the keys.</summary>
    public static string SecretsPath { get; } = Path.Combine(AgentDiscovery.DataDirectory, "secrets.json");

    /// <summary>preferences.json next to secrets.json: choices made from the command line, such as --no-donate.
    /// Same shape as appsettings.json.</summary>
    public static string PreferencesPath { get; } = Path.Combine(AgentDiscovery.DataDirectory, "preferences.json");

    /// <summary>settings.json next to secrets.json: the user's own settings, such as models and search
    /// providers. Kept out of the install directory, so updates replace appsettings.json without losing them.</summary>
    public static string UserSettingsPath { get; } = Path.Combine(AgentDiscovery.DataDirectory, "settings.json");

    // AppContext.BaseDirectory resolves next to the executable under both dotnet run and a published build.
    /// <summary>The shipped defaults; replaced by every update.</summary>
    private static string DefaultsPath { get; } = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>Debug builds only: local overrides such as real model endpoints. Not tracked in git.</summary>
    private static string DebugSettingsPath { get; } = Path.Combine(AppContext.BaseDirectory, "appsettings.Debug.json");

    public static IConfiguration Build()
    {
        try
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile(DefaultsPath, optional: true, reloadOnChange: false);
#if DEBUG
            builder.AddJsonFile(DebugSettingsPath, optional: true, reloadOnChange: false);
#endif
            var environment = EnvironmentSettings();
            return builder
                .AddJsonFile(UserSettingsPath, optional: true, reloadOnChange: false)
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

    /// <summary>What AddEnvironmentVariables(prefix) would produce, minus the agent's variables.
    /// CONSOLESCOUT_AGENT__X (double underscore) is still the CLI's own Agent:X.</summary>
    private static IEnumerable<KeyValuePair<string, string?>> EnvironmentSettings()
    {
        foreach (System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            var name = (string)variable.Key;
            if (!name.StartsWith(EnvironmentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.StartsWith(AgentEnvironmentPrefix, StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith(AgentEnvironmentPrefix + "_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Shares the prefix but selects the terminal session rather than configuring anything.
            if (string.Equals(name, TerminalSession.Variable, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = name[EnvironmentPrefix.Length..].Replace("__", ConfigurationPath.KeyDelimiter);
            yield return new(key, (string?)variable.Value);
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
            // The binder cannot tell which file a key came from, so the files in use are listed instead.
            var files = SettingsFiles().Where(File.Exists);
            var list = string.Join(Environment.NewLine + "  ", files);
            throw new ConfigurationException(
                $"Invalid settings.{Environment.NewLine}{ex.Message}{Environment.NewLine}"
                + $"Check these files (and CONSOLESCOUT_* variables):{Environment.NewLine}  {list}");
        }
    }

    private static IEnumerable<string> SettingsFiles()
    {
        yield return DefaultsPath;
#if DEBUG
        yield return DebugSettingsPath;
#endif
        yield return UserSettingsPath;
        yield return PreferencesPath;
        yield return SecretsPath;
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
