namespace ConsoleScout.Configuration;

internal sealed class ModelSettings
{
    public string Endpoint { get; set; } = "https://api.mistral.ai/v1";

    public string Name { get; set; } = "ministral-14b-latest";

    /// <summary>Secret: belongs in secrets.json or an environment variable, not appsettings.json.</summary>
    public string? ApiKey { get; set; }

    /// <summary>A search followed by reading a page or two takes three rounds.</summary>
    public int MaxToolRounds { get; set; } = 6;

    /// <summary>Wraps the transport in ContentFlatteningHandler. Turn it off for providers
    /// that never chunk message.content, so unknown chunk types cannot be dropped silently.</summary>
    public bool FlattenContentArrays { get; set; } = false;
}
