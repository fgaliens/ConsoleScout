using Microsoft.Extensions.Options;

namespace ConsoleScout.Scraper.Hosting;

/// <summary>Stops the service after a quiet spell. The CLI starts it on demand, so nobody else would.
/// A request still in progress counts as activity however long it runs.</summary>
internal sealed class IdleShutdown(
    IOptions<ScraperSettings> options,
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<IdleShutdown> logger) : BackgroundService, IMiddleware
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly int _idleMinutes = Math.Max(1, options.Value.IdleMinutes);

    private long _lastActivity = time.GetTimestamp();
    private int _inFlight;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        Interlocked.Increment(ref _inFlight);
        try
        {
            await next(context);
        }
        finally
        {
            var now = time.GetTimestamp();
            Interlocked.Exchange(ref _lastActivity, now);
            Interlocked.Decrement(ref _inFlight);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(CheckInterval, time);

        while (await timer.WaitForNextTickAsync(ct))
        {
            if (Volatile.Read(ref _inFlight) > 0)
            {
                continue;
            }

            var lastActivity = Interlocked.Read(ref _lastActivity);
            var idle = time.GetElapsedTime(lastActivity);
            if (idle < TimeSpan.FromMinutes(_idleMinutes))
            {
                continue;
            }

            logger.LogInformation("No requests for {Minutes} minutes, shutting down", _idleMinutes);
            lifetime.StopApplication();
            return;
        }
    }
}
