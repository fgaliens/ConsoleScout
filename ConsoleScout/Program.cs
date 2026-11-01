using System.CommandLine;
using ConsoleScout.AI;
using ConsoleScout.App;

var questionArgument = new Argument<string[]>("question")
{
    Description = "The question to answer, unquoted. Omitted: shows the usage guide and setup status, then asks for one",
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

var runScraperOption = new Option<bool>(name: "--run-scraper", aliases: ["-r"])
{
    Description = "Start the scraper and wait until its browser is ready (downloads Chromium on first use if needed)",
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

var rootCommand = new RootCommand("Console Scout")
{
    questionArgument,
    longAnswerOption,
    shortAnswerOption,
    setKeyOption,
    runScraperOption,
    donateOption,
    noDonateOption,
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
        result.GetValue(runScraperOption),
        result.GetResult(donateOption) is not null,
        result.GetValue(noDonateOption),
    };
    if (actions.Count(given => given) > 1)
    {
        result.AddError("A question, --set-key, --run-scraper, --donate and --no-donate cannot be combined; give one of them.");
    }
});

rootCommand.SetAction((parseResult, ct) =>
{
    var words = parseResult.GetValue(questionArgument) ?? [];

    AnswerLength? length = parseResult.GetValue(longAnswerOption) ? AnswerLength.Long
        : parseResult.GetValue(shortAnswerOption) ? AnswerLength.Short
        : null;

    return ConsoleScoutApp.RunAsync(
        new CommandLineOptions(
            Question: words.Length > 0 ? string.Join(' ', words) : null,
            Length: length,
            SetKeys: parseResult.GetValue(setKeyOption) ?? [],
            RunScraper: parseResult.GetValue(runScraperOption),
            Donate: parseResult.GetResult(donateOption) is not null,
            DonateNetwork: parseResult.GetValue(donateOption),
            NoDonate: parseResult.GetValue(noDonateOption)),
        ct);
});

return await rootCommand
    .Parse(args)
    .InvokeAsync();
