using System.CommandLine;
using ConsoleScout.AI;
using ConsoleScout.App;

var questionArgument = new Argument<string[]>("question")
{
    Description = "The question to answer, unquoted. Omitted: asks for one, or, while a key is missing, "
        + "shows the usage guide and setup status",
    Arity = ArgumentArity.ZeroOrMore,
};

var longAnswerOption = new Option<bool>(name: "--long", aliases: ["-l"])
{
    Description = "Show detailed answer",
};

var shortAnswerOption = new Option<bool>(name: "--short", aliases: ["-s"])
{
    Description = "Show the shortest answer",
};

var setKeyOption = new Option<string[]>(name: "--set-key", aliases: ["-k"])
{
    Description = "Save a model's API key to ~/.consolescout/secrets.json: -k main asks for it without echoing, "
        + "-k main=<key> takes it as given. Models: main, summarizer. Repeatable",
    HelpName = "model[=key]",
};

var runAgentOption = new Option<bool>(name: "--run-agent", aliases: ["-r"])
{
    Description = "Start the agent and wait until its browser is ready (downloads Chromium on first use if needed)",
};

var donateOption = new Option<string?>(name: "--donate")
{
    Description = "Support the developer: lists the networks; --donate <network> shows the address and QR code",
    HelpName = "network",
    Arity = ArgumentArity.ZeroOrOne,
};

var noDonateOption = new Option<bool>(name: "--no-donate")
{
    Description = "Stop showing the donation hint while the model thinks",
};

var statusOption = new Option<bool>(name: "--status")
{
    Description = "Show the setup state: configuration, model keys and the agent",
};

var readmeOption = new Option<bool>(name: "--readme")
{
    Description = "Show the full documentation",
};

// -c is a plain flag; a reference after it (m42, d7, b3) is moved to the hidden option by ContextArguments.
var contextOption = new Option<bool>(name: "--with-context", aliases: ["-c", "--continue"])
{
    Description = "Follow up on an earlier answer: -c the last one in this terminal; -c b<N> the one N back; "
        + "-c m<id> message <id>, -c d<id> the latest message of dialog <id>, from any terminal",
};

var dialogOption = new Option<bool>(name: "--dialog", aliases: ["-d"])
{
    Description = "Dialog mode in this terminal: each question continues the conversation. Alone it turns the mode "
        + "on or off; with -c it turns it on for that conversation; with a question it turns it on and asks",
};

var newDialogOption = new Option<bool>(name: "--new", aliases: ["-n"])
{
    Description = "Start a new dialog with this question, even in dialog mode",
};

var contextReferenceOption = new Option<string?>(name: ContextArguments.ReferenceOption)
{
    Hidden = true,
};

var rootCommand = new RootCommand("Console Scout")
{
    questionArgument,
    longAnswerOption,
    shortAnswerOption,
    setKeyOption,
    runAgentOption,
    donateOption,
    noDonateOption,
    statusOption,
    readmeOption,
    contextOption,
    contextReferenceOption,
    dialogOption,
    newDialogOption,
};

HelpLayout.Apply(rootCommand);

// Reported by System.CommandLine like any other usage error, before anything runs.
rootCommand.Validators.Add(result =>
{
    if (result.GetValue(longAnswerOption) && result.GetValue(shortAnswerOption))
    {
        result.AddError("--long and --short cannot be used together.");
    }

    var actions = new[]
    {
        (result.GetValue(questionArgument) ?? []).Length > 0,
        (result.GetValue(setKeyOption) ?? []).Length > 0,
        result.GetValue(runAgentOption),
        result.GetResult(donateOption) is not null,
        result.GetValue(noDonateOption),
        result.GetValue(statusOption),
        result.GetValue(readmeOption),
    };
    if (actions.Count(given => given) > 1)
    {
        result.AddError("A question and the options --set-key, --run-agent, --donate, --no-donate, --status and --readme "
            + "cannot be combined; give one of them.");
    }

    // -c, -d and -n go with a question, or alone: the question is then asked for (-c, -n) or the mode changed (-d).
    var otherActions = actions.Skip(1).Count(given => given);
    var conversation = result.GetValue(contextOption) || result.GetValue(dialogOption) || result.GetValue(newDialogOption);
    if (conversation && otherActions > 0)
    {
        result.AddError("-c, -d and -n go with a question only.");
    }

    if (result.GetValue(newDialogOption) && (result.GetValue(contextOption) || result.GetValue(dialogOption)))
    {
        result.AddError("-n starts a new dialog, so it cannot be combined with -c or -d.");
    }
});

rootCommand.SetAction((parseResult, ct) =>
{
    var words = parseResult.GetValue(questionArgument) ?? [];

    var reference = parseResult.GetValue(contextReferenceOption);
    var context = !parseResult.GetValue(contextOption)
        ? null
        : reference is null ? ContextReference.Latest : ContextReference.TryParse(reference);

    AnswerLength? length = parseResult.GetValue(longAnswerOption) ? AnswerLength.Long
        : parseResult.GetValue(shortAnswerOption) ? AnswerLength.Short
        : null;

    return ConsoleScoutApp.RunAsync(
        new CommandLineOptions(
            Question: words.Length > 0 ? string.Join(' ', words) : null,
            Length: length,
            SetKeys: parseResult.GetValue(setKeyOption) ?? [],
            RunAgent: parseResult.GetValue(runAgentOption),
            Donate: parseResult.GetResult(donateOption) is not null,
            DonateNetwork: parseResult.GetValue(donateOption),
            NoDonate: parseResult.GetValue(noDonateOption),
            Status: parseResult.GetValue(statusOption),
            Readme: parseResult.GetValue(readmeOption),
            Context: context,
            Dialog: parseResult.GetValue(dialogOption),
            NewDialog: parseResult.GetValue(newDialogOption)),
        ct);
});

var normalized = ContextArguments.Normalize(args);
return await rootCommand
    .Parse(normalized)
    .InvokeAsync();
