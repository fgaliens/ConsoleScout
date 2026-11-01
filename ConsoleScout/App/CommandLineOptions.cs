using ConsoleScout.AI;

namespace ConsoleScout.App;

/// <summary>What the command line asked for, already parsed and validated: at most one action is given -
/// a question, <see cref="SetKeys"/>, <see cref="RunAgent"/>, <see cref="Donate"/>, <see cref="NoDonate"/>,
/// <see cref="Status"/> or <see cref="Readme"/>.</summary>
/// <param name="Length">Null when neither --long nor --short was given: the configured preference applies.</param>
/// <param name="SetKeys">--set-key values, each "model" or "model=key".</param>
/// <param name="DonateNetwork">The network given to --donate, if any; null lists the networks.</param>
/// <param name="Context">The earlier answer -c follows up on; null without -c. Goes with a question, or with
/// none, in which case the question is asked for.</param>
/// <param name="Dialog">-d: without a question it turns dialog mode on or off (on for <see cref="Context"/>'s
/// conversation, given -c); with a question it turns the mode on and asks.</param>
/// <param name="NewDialog">-n: the question starts a new dialog, even in dialog mode.</param>
internal sealed record CommandLineOptions(
    string? Question,
    AnswerLength? Length,
    IReadOnlyList<string> SetKeys,
    bool RunAgent,
    bool Donate,
    string? DonateNetwork,
    bool NoDonate,
    bool Status,
    bool Readme,
    ContextReference? Context,
    bool Dialog,
    bool NewDialog)
{
    /// <summary>No question and no other action: the usage guide is shown, and a question asked for.</summary>
    public bool ShowsGuide =>
        Question is null && SetKeys.Count == 0 && !RunAgent && !Donate && !NoDonate && !Status && !Readme && !Dialog;

    /// <summary>-d without a question: only the mode changes.</summary>
    public bool ChangesDialogMode => Dialog && Question is null;

    /// <summary>Actions that work without the configuration, so they keep working when it is broken -
    /// and, for the guide, on a fresh machine with nothing set up yet.</summary>
    public bool NeedsNoConfiguration => SetKeys.Count > 0 || Donate || NoDonate || Status || Readme || ShowsGuide;
}
