using ConsoleScout.AI;
using ConsoleScout.Configuration;
using ConsoleScout.Output;
using ConsoleScout.Scraper;
using ConsoleScout.Tools;
using ConsoleScout.Tools.WebFetch;
using ConsoleScout.Tools.WebSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleScout.App;

/// <summary>The composition root, one method per area. A disabled feature is simply not registered,
/// so nothing it would need (a key, the scraper) is ever resolved.</summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>Engines the scraper can read. A provider with its own API gets its own registration
    /// in <see cref="AddWebSearch"/> instead.</summary>
    private static readonly string[] ScraperEngines = ["google", "duckduckgo"];

    private const string MainModel = "main";
    private const string SummarizerModel = "summarizer";

    public static IServiceCollection AddConsoleScout(
        this IServiceCollection services,
        IConfiguration configuration,
        AppSettings settings)
    {
        services.AddSingleton(configuration);
        services.AddSingleton(settings);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<QuestionContext>();

        return services
            .AddOutput(settings)
            .AddTools(settings)
            .AddAssistant(settings)
            .AddSingleton<AskCommand>()
            .AddSingleton<SetKeyCommand>()
            .AddSingleton<RunScraperCommand>()
            .AddSingleton<GuideCommand>()
            .AddSingleton<DonateCommand>()
            .AddSingleton<NoDonateCommand>();
    }

    /// <summary>True when some registered tool goes through the scraper, so it is worth starting early.</summary>
    public static bool UsesScraper(this AppSettings settings) =>
        settings.Tools.WebSearch.Enabled || settings.Tools.WebFetch.Enabled;

    private static IServiceCollection AddOutput(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton(ConsoleTheme.Default);
        services.AddSingleton<MarkdownRenderer>();
        services.AddSingleton<IAnswerWriter>(provider =>
        {
            if (settings.Output.Format == AnswerFormat.Plain)
            {
                return new PlainAnswerWriter();
            }

            var renderer = provider.GetRequiredService<MarkdownRenderer>();
            return new MarkdownAnswerWriter(renderer);
        });

        services.AddSingleton<WalletWriter>();
        services.AddSingleton<ConsoleUi>();
        services.AddSingleton<IConsoleUi>(provider => provider.GetRequiredService<ConsoleUi>());
        services.AddSingleton<IActivityReporter>(provider => provider.GetRequiredService<ConsoleUi>());

        return services;
    }

    private static IServiceCollection AddTools(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton(provider =>
        {
            var tools = provider.GetServices<IAssistantTool>();
            return new ToolInvoker(tools);
        });

        // Always registered, for --run-scraper; nothing is started until something asks for it.
        services.AddSingleton(settings.Scraper);
        services.AddSingleton<ScraperLauncher>();
        services.AddSingleton<ScraperClient>();

        if (settings.Tools.WebSearch.Enabled)
        {
            services.AddWebSearch(settings.Tools.WebSearch);
        }

        if (settings.Tools.WebFetch.Enabled)
        {
            var condense = settings.Tools.WebFetch.Condense && settings.Models.Summarizer is not null;
            if (condense)
            {
                services.AddSummarizer(settings);
            }

            services.AddSingleton<IAssistantTool>(provider =>
            {
                var scraper = provider.GetRequiredService<ScraperClient>();
                var activity = provider.GetRequiredService<IActivityReporter>();
                var condenser = provider.GetService<PageCondenser>();
                return new WebFetchTool(scraper, settings.Tools.WebFetch, activity, condenser);
            });
        }

        return services;
    }

    /// <summary>The summarizer model and the condenser that uses it. Its key is resolved together with the
    /// tools, before the question is asked, so a missing one fails the run up front.</summary>
    private static void AddSummarizer(this IServiceCollection services, AppSettings settings)
    {
        var summarizer = settings.Models.Summarizer!;

        services.AddKeyedSingleton(SummarizerModel, (_, _) =>
        {
            var apiKey = ConfigurationLoader.RequireKey(summarizer.ApiKey, "Models:Summarizer:ApiKey", "the summarizer model");
            return new ModelClient(summarizer, apiKey);
        });

        services.AddSingleton(provider =>
        {
            var client = provider.GetRequiredKeyedService<ModelClient>(SummarizerModel);
            var question = provider.GetRequiredService<QuestionContext>();
            return new PageCondenser(client, question, settings.Tools.WebFetch.CondensedMaxWords);
        });
    }

    private static void AddWebSearch(this IServiceCollection services, WebSearchSettings settings)
    {
        foreach (var engine in ScraperEngines)
        {
            services.AddKeyedSingleton<ISearchProvider>(engine, (provider, _) =>
            {
                var scraper = provider.GetRequiredService<ScraperClient>();
                return new ScraperSearchProvider(scraper, engine);
            });
        }

        // Validated here rather than on first search, so a typo fails before the question is asked.
        var configured = settings.GetProviders();
        var unknown = configured.Where(name => !ScraperEngines.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();
        if (unknown.Count > 0)
        {
            throw new ConfigurationException(
                $"Unknown search provider(s) in Tools:WebSearch:Providers: {string.Join(", ", unknown)}. "
                + $"Available: {string.Join(", ", ScraperEngines)}.");
        }

        services.AddSingleton<IAssistantTool>(provider =>
        {
            var providers = configured
                .Select(name => provider.GetRequiredKeyedService<ISearchProvider>(name.ToLowerInvariant()))
                .ToList();
            var activity = provider.GetRequiredService<IActivityReporter>();
            return new WebSearchTool(providers, settings, activity);
        });
    }

    private static IServiceCollection AddAssistant(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton<EnvironmentInfo>();
        services.AddKeyedSingleton(MainModel, (_, _) =>
        {
            var model = settings.Models.Main;
            var apiKey = ConfigurationLoader.RequireKey(model.ApiKey, "Models:Main:ApiKey", "the main model");
            return new ModelClient(model, apiKey);
        });

        services.AddSingleton(provider =>
        {
            var client = provider.GetRequiredKeyedService<ModelClient>(MainModel);
            var tools = provider.GetRequiredService<ToolInvoker>();
            var environment = provider.GetRequiredService<EnvironmentInfo>();
            return new Assistant(client, settings.Output.Format, tools, environment);
        });

        return services;
    }
}
