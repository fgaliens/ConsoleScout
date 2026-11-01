namespace ConsoleScout.Configuration;

internal sealed class ModelsSettings
{
    /// <summary>Writes the answer the user reads.</summary>
    public ModelSettings Main { get; set; } = new();

    /// <summary>A cheaper model that condenses fetched pages to what matters for the question, so the main
    /// model reads a digest instead of raw page text. Optional: without it, pages reach the main model as is.</summary>
    public ModelSettings? Summarizer { get; set; }
}
