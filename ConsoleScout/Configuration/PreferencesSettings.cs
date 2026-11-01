using ConsoleScout.AI;

namespace ConsoleScout.Configuration;

internal sealed class PreferencesSettings
{
    /// <summary>Used when neither --long nor --short is given.</summary>
    public AnswerLength AnswerLength { get; set; } = AnswerLength.Normal;

    /// <summary>The line about --donate under the spinner. Turned off by --no-donate, which stores it in preferences.json.</summary>
    public bool ShowDonateHint { get; set; } = true;
}
