using ConsoleScout.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace ConsoleScout.Scraper.Browser;

/// <summary>One long-lived browser with a persistent profile, so cookies and consent choices survive
/// between requests and between runs. Pages are handed out one per request and closed afterwards.</summary>
internal sealed class BrowserHost(
    IOptions<ScraperSettings> options,
    NavigationGuard guard,
    ILogger<BrowserHost> logger) : IAsyncDisposable
{
    private readonly BrowserSettings _settings = options.Value.Browser;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _pages = new(options.Value.Browser.MaxPages, options.Value.Browser.MaxPages);

    private IPlaywright? _playwright;
    private IBrowserContext? _context;

    private static string ProfileDirectory => Path.Combine(ScraperDiscovery.DataDirectory, "browser-profile");

    /// <summary>Runs <paramref name="work"/> on a fresh page that is closed afterwards, whatever happens.</summary>
    public async Task<T> WithPageAsync<T>(Func<IPage, Task<T>> work, CancellationToken ct)
    {
        var context = await GetContextAsync(ct);

        await _pages.WaitAsync(ct);
        try
        {
            var page = await context.NewPageAsync();
            try
            {
                page.SetDefaultNavigationTimeout(_settings.NavigationTimeoutSeconds * 1000);
                page.SetDefaultTimeout(_settings.NavigationTimeoutSeconds * 1000);
                await guard.AttachAsync(page);

                return await work(page);
            }
            catch (TimeoutException ex)
            {
                throw new ScraperFailure(ScraperErrorCodes.Timeout, $"The page did not load in time: {ex.Message}");
            }
            catch (PlaywrightException ex)
            {
                throw new ScraperFailure(ScraperErrorCodes.FetchFailed, ex.Message);
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        finally
        {
            _pages.Release();
        }
    }

    /// <summary>Starts the browser ahead of the first request, so that request does not pay for it.
    /// Failures are only logged: the first real request retries and reports them.</summary>
    public async Task WarmUpAsync(CancellationToken ct)
    {
        try
        {
            await EnsureStartedAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The first real request retries and reports the failure to its caller.
            logger.LogWarning(ex, "Browser warm-up failed");
        }
    }

    /// <summary>Opens a visible window on the same profile and waits for the user to close it: the way to
    /// accept a cookie banner or pass a CAPTCHA by hand, after which headless runs reuse the result.</summary>
    public async Task RunSetupAsync(string url)
    {
        _playwright ??= await Playwright.CreateAsync();
        Directory.CreateDirectory(ProfileDirectory);

        var context = await LaunchAsync(_playwright, headless: false);
        var closed = new TaskCompletionSource();
        context.Close += (_, _) => closed.TrySetResult();

        var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();
        await page.GotoAsync(url);
        await closed.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.CloseAsync();
        }

        _playwright?.Dispose();
    }

    /// <summary>Starts the browser if it is not running yet, downloading Chromium first when no
    /// installed browser can be used. Throws a <see cref="ScraperFailure"/> when that fails.</summary>
    public async Task EnsureStartedAsync(CancellationToken ct)
    {
        try
        {
            await GetContextAsync(ct);
        }
        catch (PlaywrightException ex)
        {
            throw new ScraperFailure(ScraperErrorCodes.FetchFailed, $"The browser could not be started: {ex.Message}");
        }
    }

    private async Task<IBrowserContext> GetContextAsync(CancellationToken ct)
    {
        if (_context is not null)
        {
            return _context;
        }

        await _startLock.WaitAsync(ct);
        try
        {
            if (_context is not null)
            {
                return _context;
            }

            _playwright ??= await Playwright.CreateAsync();
            _context = await LaunchAsync(_playwright, _settings.Headless);
            return _context;
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task<IBrowserContext> LaunchAsync(IPlaywright playwright, bool headless)
    {
        Directory.CreateDirectory(ProfileDirectory);

        if (_settings.Channel is { Length: > 0 } channel)
        {
            try
            {
                var options = Options(channel, headless);
                var context = await playwright.Chromium.LaunchPersistentContextAsync(ProfileDirectory, options);
                logger.LogInformation("Browser started from channel {Channel}", channel);
                return context;
            }
            catch (PlaywrightException ex)
            {
                logger.LogWarning("Channel {Channel} is unavailable, falling back to bundled Chromium: {Reason}",
                    channel, ex.Message);
            }
        }

        InstallChromium();
        var bundledOptions = Options(null, headless);
        var bundled = await playwright.Chromium.LaunchPersistentContextAsync(ProfileDirectory, bundledOptions);
        logger.LogInformation("Browser started from bundled Chromium");
        return bundled;
    }

    private static BrowserTypeLaunchPersistentContextOptions Options(string? channel, bool headless) => new()
    {
        Channel = channel,
        Headless = headless,
    };

    /// <summary>A no-op when Chromium is already installed; otherwise a one-time download.</summary>
    private void InstallChromium()
    {
        logger.LogInformation("Ensuring Playwright Chromium is installed");

        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new ScraperFailure(ScraperErrorCodes.FetchFailed,
                $"Installing Chromium failed (exit code {exitCode}); the reason is in scraper.log. Installing Google Chrome "
                + "works as well: the scraper uses it when present (Scraper:Browser:Channel = \"chrome\").");
        }
    }
}
