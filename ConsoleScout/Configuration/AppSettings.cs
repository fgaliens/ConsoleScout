using System.Text.Json.Serialization;

namespace ConsoleScout.Configuration;

/// <summary>Non-secret settings, loaded from appsettings.json next to the executable.</summary>
internal sealed record AppSettings
{
    public ModelSettings Model { get; init; } = new();

    public SearchSettings Search { get; init; } = new();

    public OutputSettings Output { get; init; } = new();
}

internal sealed record ModelSettings
{
    public string Endpoint { get; init; } = "https://api.mistral.ai/v1";

    public string Name { get; init; } = "ministral-14b-latest";

    public int MaxToolRounds { get; init; } = 4;

    /// <summary>Wraps the transport in ContentFlatteningHandler. Turn it off for providers
    /// that never chunk message.content, so unknown chunk types cannot be dropped silently.</summary>
    public bool FlattenContentArrays { get; init; } = true;
}

internal sealed record SearchSettings
{
    /// <summary>When false the model is never offered the search tool, and no search key is required.</summary>
    public bool Enabled { get; init; } = true;

    public int MaxResults { get; init; } = 5;

    public int TimeoutSeconds { get; init; } = 30;
}

internal sealed record OutputSettings
{
    /// <summary>Picks the answer writer, and is also told to the model so that what it writes
    /// matches what the console can display.</summary>
    public AnswerFormat Format { get; init; } = AnswerFormat.Markdown;
}

[JsonConverter(typeof(JsonStringEnumConverter<AnswerFormat>))]
internal enum AnswerFormat
{
    Markdown,
    Plain,
}
