using System.Security.Cryptography;
using System.Text.Json.Serialization;
using ConsoleScout.Contracts;
using ConsoleScout.Scraper;
using ConsoleScout.Scraper.Browser;
using ConsoleScout.Scraper.Fetching;
using ConsoleScout.Scraper.Hosting;
using ConsoleScout.Scraper.Search;
using Microsoft.Extensions.Options;

// Started by the CLI on demand, possibly twice at once: the later instance leaves the field to the first.
if (EndpointAnnouncer.FindRunning() is { } running)
{
    Console.WriteLine($"Another scraper is already running (pid {running.Pid}) at {running.Url}."
        + (args.Contains("--setup") ? " Stop it first: the browser profile can be open in one process only." : ""));
    return;
}

// --setup [url]: a visible browser on the scraper's profile, to accept cookies or pass a CAPTCHA by hand.
// It needs the profile to itself, hence the check above runs first.
var setup = args.Length > 0 && args[0] == "--setup";
if (setup)
{
    args = args[1..];
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // The CLI starts this process from whatever directory it was itself run in.
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Configuration.AddEnvironmentVariables("CONSOLESCOUT_SCRAPER_");

// Output ends up in ~/.consolescout/scraper.log; timestamps show where start-up time goes.
builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss.fff ");

// Loopback only, on a port the OS picks; scraper.json tells the CLI which one.
builder.WebHost.UseUrls("http://127.0.0.1:0");

var scraperSection = builder.Configuration.GetSection("Scraper");
builder.Services
    .AddOptions<ScraperSettings>()
    .Bind(scraperSection, options => options.ErrorOnUnknownConfiguration = true);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ContractsJsonContext.Default);
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton(TimeProvider.System);
var tokenBytes = RandomNumberGenerator.GetBytes(32);
var token = Convert.ToHexString(tokenBytes);
builder.Services.AddSingleton(new AccessToken(token));
builder.Services.AddSingleton<EndpointAnnouncer>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<EndpointAnnouncer>());
builder.Services.AddSingleton<AccessGuard>();
builder.Services.AddSingleton<IdleShutdown>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<IdleShutdown>());

builder.Services.AddSingleton<NavigationGuard>();
builder.Services.AddSingleton<BrowserHost>();
builder.Services.AddSingleton<PageFetcher>();
builder.Services
    .AddHttpClient(PageFetcher.HttpClientName, (provider, http) =>
    {
        var settings = provider.GetRequiredService<IOptions<ScraperSettings>>().Value;
        http.Timeout = TimeSpan.FromSeconds(settings.Fetch.HttpTimeoutSeconds);
    })
    .ConfigurePrimaryHttpMessageHandler(provider =>
    {
        var guard = provider.GetRequiredService<NavigationGuard>();
        return PageFetcher.CreateHandler(guard);
    });

// A new engine is one class and one line here; the CLI selects it by Name.
builder.Services.AddSingleton<ISearchEngine, GoogleEngine>();
builder.Services.AddSingleton<ISearchEngine, DuckDuckGoEngine>();

var app = builder.Build();

if (setup)
{
    var url = args.FirstOrDefault() ?? "https://www.google.com/search?q=ConsoleScout";
    Console.WriteLine($"Opening {url}. Accept cookies or pass the check, then close the browser window.");
    var setupBrowser = app.Services.GetRequiredService<BrowserHost>();
    await setupBrowser.RunSetupAsync(url);
    return;
}

app.UseMiddleware<IdleShutdown>();
app.UseMiddleware<AccessGuard>();

// Expected failures become a ScraperError body; anything else is a bug and surfaces as a 500.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ScraperFailure failure)
    {
        context.Response.StatusCode = failure.StatusCode;
        var error = new ScraperError(failure.Code, failure.Message);
        await context.Response.WriteAsJsonAsync(error, ContractsJsonContext.Default.ScraperError);
    }
});

app.MapGet("/health", () => Results.Ok());

// Waits until the browser is up, installing Chromium if needed: what `ConsoleScout --run-scraper` waits for.
app.MapPost("/warmup", async (BrowserHost browser, CancellationToken ct) =>
{
    await browser.EnsureStartedAsync(ct);
    return Results.Ok();
});

app.MapPost("/fetch", async (FetchRequest request, PageFetcher fetcher, CancellationToken ct) =>
{
    var page = await fetcher.FetchAsync(request, ct);
    return Results.Ok(page);
});

// Several pages in parallel, each with its own outcome, so the model can read them in one tool call.
app.MapPost("/fetch/batch", async (FetchBatchRequest request, PageFetcher fetcher, CancellationToken ct) =>
{
    var pages = await fetcher.FetchBatchAsync(request, ct);
    return Results.Ok(pages);
});

app.MapPost("/search", async (SearchRequest request, IEnumerable<ISearchEngine> engines, CancellationToken ct) =>
{
    var engine = engines.FirstOrDefault(e => string.Equals(e.Name, request.Engine, StringComparison.OrdinalIgnoreCase))
        ?? throw new ScraperFailure(
            ScraperErrorCodes.UnsupportedEngine,
            $"Unknown engine '{request.Engine}'. Available: {string.Join(", ", engines.Select(e => e.Name))}.");

    if (string.IsNullOrWhiteSpace(request.Query))
    {
        throw new ScraperFailure(ScraperErrorCodes.BadRequest, "The query is empty.");
    }

    var maxResults = Math.Clamp(request.MaxResults, 1, 20);
    var hits = await engine.SearchAsync(request.Query, maxResults, ct);
    return Results.Ok(new SearchResponse(hits));
});

// Launching the browser takes seconds; doing it now spares the first request.
var startupBrowser = app.Services.GetRequiredService<BrowserHost>();
app.Lifetime.ApplicationStarted.Register(() => _ = startupBrowser.WarmUpAsync(app.Lifetime.ApplicationStopping));

app.Run();
