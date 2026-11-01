using System.Text.Json;
using ConsoleScout.Contracts;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ConsoleScout.Scraper.Hosting;

/// <summary>Once Kestrel has picked its port, writes scraper.json so the CLI can find this instance,
/// and removes it again on shutdown - unless another instance has replaced it meanwhile.</summary>
internal sealed class EndpointAnnouncer(
    IServer server,
    IHostApplicationLifetime lifetime,
    AccessToken token,
    ILogger<EndpointAnnouncer> logger) : IHostedService
{
    private static string FilePath => ScraperDiscovery.EndpointFilePath;

    /// <summary>The bound address, e.g. http://127.0.0.1:53017. Set once the server has started.</summary>
    public Uri? Address { get; private set; }

    public Task StartAsync(CancellationToken ct)
    {
        lifetime.ApplicationStarted.Register(Announce);
        lifetime.ApplicationStopping.Register(Withdraw);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>The endpoint another running instance announced, if that instance is still alive.</summary>
    public static ScraperEndpoint? FindRunning()
    {
        var endpoint = Read();
        if (endpoint is null || endpoint.Pid == Environment.ProcessId)
        {
            return null;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(endpoint.Pid);
            return process.HasExited ? null : endpoint;
        }
        catch (ArgumentException)
        {
            return null; // no such process: the file is stale
        }
    }

    private void Announce()
    {
        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
        if (address is null)
        {
            logger.LogError("Kestrel reported no address; the CLI will not be able to find this instance");
            return;
        }

        Address = new Uri(address);
        var url = Address.ToString().TrimEnd('/');
        var endpoint = new ScraperEndpoint(url, token.Value, Environment.ProcessId);

        Directory.CreateDirectory(ScraperDiscovery.DataDirectory);

        // Written next to the target and moved into place, so a reader never sees half a file.
        var temporary = FilePath + $".{Environment.ProcessId}.tmp";
        var json = JsonSerializer.Serialize(endpoint, ContractsJsonContext.Default.ScraperEndpoint);
        File.WriteAllText(temporary, json);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(temporary, FilePath, overwrite: true);

        logger.LogInformation("Listening on {Address}, announced in {File}", endpoint.Url, FilePath);
    }

    private void Withdraw()
    {
        if (Read()?.Pid == Environment.ProcessId)
        {
            File.Delete(FilePath);
        }
    }

    private static ScraperEndpoint? Read()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize(json, ContractsJsonContext.Default.ScraperEndpoint);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }
}
