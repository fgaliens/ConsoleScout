using ConsoleScout.AI;

namespace ConsoleScout.Configuration;

// Classes rather than records: a record's generated ToString would print the API keys.
// Settable rather than init-only: the AOT binding generator silently skips init-only properties.

/// <summary>Bound from IConfiguration; see <see cref="ConfigurationLoader"/> for the sources.</summary>
internal sealed class AppSettings
{
    public ModelsSettings Models { get; set; } = new();

    public SearchSettings Search { get; set; } = new();

    public OutputSettings Output { get; set; } = new();

    public PreferencesSettings Preferences { get; set; } = new();
}

internal sealed class ModelsSettings
{
    /// <summary>Writes the answer the user reads.</summary>
    public ModelSettings Main { get; set; } = new();

    /// <summary>A cheaper model for simple analysis that never reaches the user directly.
    /// Optional: when omitted, that work goes to <see cref="Main"/>.</summary>
    public ModelSettings? Fast { get; set; }

    // A method, not a property: the binder would treat a property as one more section to fill.
    public ModelSettings GetFastOrMain() => Fast ?? Main;
}

internal sealed class ModelSettings
{
    public string Endpoint { get; set; } = "https://api.mistral.ai/v1";

    public string Name { get; set; } = "ministral-14b-latest";

    /// <summary>Secret: belongs in secrets.json or an environment variable, not appsettings.json.</summary>
    public string? ApiKey { get; set; }

    public int MaxToolRounds { get; set; } = 4;

    /// <summary>Wraps the transport in ContentFlatteningHandler. Turn it off for providers
    /// that never chunk message.content, so unknown chunk types cannot be dropped silently.</summary>
    public bool FlattenContentArrays { get; set; } = false;
}

internal sealed class SearchSettings
{
    /// <summary>When false the model is never offered the search tool, and no search key is required.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Secret: belongs in secrets.json or an environment variable, not appsettings.json.</summary>
    public string? ApiKey { get; set; }

    public int MaxResults { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 30;
}

internal sealed class OutputSettings
{
    /// <summary>Picks the answer writer, and is also told to the model so that what it writes
    /// matches what the console can display.</summary>
    public AnswerFormat Format { get; set; } = AnswerFormat.Markdown;
}

internal sealed class PreferencesSettings
{
    /// <summary>Used when neither --long nor --short is given.</summary>
    public AnswerLength AnswerLength { get; set; } = AnswerLength.Normal;
}

internal enum AnswerFormat
{
    Markdown,
    Plain,
}
