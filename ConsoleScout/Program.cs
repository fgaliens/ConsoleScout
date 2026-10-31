using System.CommandLine;
using System.Text;
using ConsoleScout.AI;
using ConsoleScout.AI.ToolProviders;
using ConsoleScout.AssistantTools.Search;
using ConsoleScout.Configuration;
using ConsoleScout.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

var questionArgument = new Argument<string[]>("question")
{
    Description = "The question to answer, unquoted. Omitted, it is asked interactively",
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

var rootCommand = new RootCommand("Console Scout")
{
    questionArgument,
    longAnswerOption,
    shortAnswerOption,
};

rootCommand.SetAction(async (parseResult, ct) =>
{
    var wantsLong = parseResult.GetValue(longAnswerOption);
    var wantsShort = parseResult.GetValue(shortAnswerOption);

    if (wantsLong && wantsShort)
    {
        Console.Error.WriteLine("--long and --short cannot be used together.");
        return 1;
    }

    try
    {
        var configuration = ConfigurationLoader.Build();
        var settings = ConfigurationLoader.Bind(configuration);

        var length = wantsLong ? AnswerLength.Long
            : wantsShort ? AnswerLength.Short
            : settings.Preferences.AnswerLength;

        var words = parseResult.GetValue(questionArgument) ?? [];
        var question = words.Length > 0 ? string.Join(' ', words) : AnsiConsole.Prompt(Ask());

        if (string.IsNullOrWhiteSpace(question))
        {
            return 0;
        }

        // Assigned once the spinner exists; the search callback reads it through the closure.
        StatusContext? status = null;

        await using var provider = BuildServices(
            configuration,
            settings,
            query => status?.Status($"[green]Searching[/]: {Markup.Escape(query)}"));

        var assistant = provider.GetRequiredService<Assistant>();
        var tool = provider.GetService<WebSearchTool>();

        var answer = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .StartAsync("[green]Thinking[/]...", async ctx =>
            {
                status = ctx;
                return await assistant.AskAsync(question, length, ct);
            });

        provider.GetRequiredService<IAnswerWriter>().Write(answer, tool?.Sources ?? []);
        return 0;
    }
    catch (ConfigurationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
    catch (AiException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.ToString());
        return 1;
    }
});

return await rootCommand
    .Parse(args)
    .InvokeAsync();

// Search is registered only when enabled, so a disabled search never resolves its API key.
static ServiceProvider BuildServices(IConfiguration configuration, AppSettings settings, Action<string> onSearching)
{
    var services = new ServiceCollection();

    services.AddSingleton(configuration);
    services.AddSingleton(settings);

    if (settings.Search.Enabled)
    {
        services.AddSingleton<IWebSearch>(provider =>
        {
            var search = provider.GetRequiredService<AppSettings>().Search;

            return new TavilySearch(
                ConfigurationLoader.RequireKey(search.ApiKey, "Search:ApiKey", "web search"),
                search.MaxResults,
                TimeSpan.FromSeconds(search.TimeoutSeconds));
        });

        services.AddSingleton(provider =>
            new WebSearchTool(provider.GetRequiredService<IWebSearch>(), onSearching));
    }

    services.AddSingleton(provider =>
    {
        var appSettings = provider.GetRequiredService<AppSettings>();
        var model = appSettings.Models.Main;

        return new Assistant(
            model,
            appSettings.Output.Format,
            ConfigurationLoader.RequireKey(model.ApiKey, "Models:Main:ApiKey", "the main model"),
            provider.GetService<WebSearchTool>());
    });

    services.AddSingleton(ConsoleTheme.Default);

    services.AddSingleton<IAnswerWriter>(provider =>
    {
        var theme = provider.GetRequiredService<ConsoleTheme>();

        return provider.GetRequiredService<AppSettings>().Output.Format switch
        {
            AnswerFormat.Plain => new PlainAnswerWriter(theme),
            _ => new MarkdownAnswerWriter(new MarkdownRenderer(theme), theme),
        };
    });

    return services.BuildServiceProvider();
}

static TextPrompt<string> Ask()
{
    return new TextPrompt<string>("What is your question?")
        .DefaultValue("What is the weather for today?")
        .ShowDefaultValue();
}
