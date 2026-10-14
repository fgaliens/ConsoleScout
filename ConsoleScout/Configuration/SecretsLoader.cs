using System.Text.Json;

namespace ConsoleScout.Configuration;

internal static class SecretsLoader
{
    private const string ModelVariable = "CONSOLESCOUT_MODEL_KEY";
    private const string SearchVariable = "CONSOLESCOUT_SEARCH_KEY";

    // Read once, on the first key actually asked for. A disabled feature never asks, and so never
    // fails over a key it does not need.
    private static readonly Lazy<SecretsFile> Stored = new(ReadFile);

    /// <summary>secrets.json in the user profile, alongside how git keeps its own credentials.</summary>
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".consolescout",
        "secrets.json");

    public static string GetModelKey() => Resolve("the model endpoint", ModelVariable, Stored.Value.Model);

    public static string GetSearchKey() => Resolve("web search", SearchVariable, Stored.Value.Search);

    private static SecretsFile ReadFile()
    {
        if (!File.Exists(FilePath))
        {
            return new SecretsFile();
        }

        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize(stream, AppJsonContext.Default.SecretsFile) ?? new SecretsFile();
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"{FilePath} is not valid JSON.{Environment.NewLine}{ex.Message}");
        }
        catch (IOException ex)
        {
            throw new ConfigurationException($"Cannot read {FilePath}.{Environment.NewLine}{ex.Message}");
        }
    }

    private static string Resolve(string purpose, string variable, string? stored)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        if (!string.IsNullOrWhiteSpace(stored))
            return stored;

        throw new ConfigurationException(
            $$"""
              Missing API key for {{purpose}}.
              Set {{variable}}, or create {{FilePath}}:
                { "model": "...", "search": "..." }
              """);
    }
}
