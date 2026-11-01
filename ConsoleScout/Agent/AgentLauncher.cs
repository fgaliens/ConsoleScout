using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using ConsoleScout.Configuration;
using ConsoleScout.Contracts;

namespace ConsoleScout.Agent;

/// <summary>Finds the running agent through agent.json, or starts one and waits until it answers.
/// The started process is detached from the CLI: it outlives this run, so the next question finds a
/// browser that is already up, and it stops itself once idle.</summary>
internal sealed class AgentLauncher(AgentSettings settings) : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(2);

    private readonly HttpClient _health = new() { Timeout = HealthTimeout };
    private readonly Lock _lock = new();
    private Task<AgentEndpoint>? _endpoint;

    private static string LogPath => Path.Combine(AgentDiscovery.DataDirectory, "agent.log");

    /// <summary>Begins locating or starting the agent without waiting, so the first tool call that
    /// needs it does not pay the browser's start-up time on its own.</summary>
    public void StartInBackground() =>
        GetEndpointAsync().ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);

    /// <param name="startupTimeout">How long to wait if the agent has to be started; Agent:StartupTimeoutSeconds
    /// by default. Only the first call of a run starts it, so only its value counts.</param>
    public Task<AgentEndpoint> GetEndpointAsync(TimeSpan? startupTimeout = null)
    {
        lock (_lock)
        {
            return _endpoint ??= LocateAsync(startupTimeout ?? TimeSpan.FromSeconds(settings.StartupTimeoutSeconds));
        }
    }

    /// <summary>Forgets the endpoint after a connection failure, e.g. when the agent went idle and
    /// stopped between two calls. The next <see cref="GetEndpointAsync"/> locates it afresh.</summary>
    public void Invalidate(AgentEndpoint stale)
    {
        lock (_lock)
        {
            if (_endpoint is { IsCompletedSuccessfully: true } current && current.Result == stale)
            {
                _endpoint = null;
            }
        }
    }

    public void Dispose() => _health.Dispose();

    private async Task<AgentEndpoint> LocateAsync(TimeSpan startupTimeout)
    {
        // A failure stays cached for the rest of the run: one question should not wait out the start-up
        // timeout once per tool call. Only Invalidate, after a working endpoint stopped answering, retries.
        if (await FindHealthyAsync() is { } running)
        {
            return running;
        }

        if (!settings.AutoStart)
        {
            throw new AgentUnavailableException(
                "The agent is not running and Agent:AutoStart is off. Start ConsoleScout.Agent first.");
        }

        var executable = ResolveExecutable();
        Start(executable);
        return await WaitUntilHealthyAsync(startupTimeout);
    }

    private async Task<AgentEndpoint> WaitUntilHealthyAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval);

            if (await FindHealthyAsync() is { } endpoint)
            {
                return endpoint;
            }
        }

        throw new AgentUnavailableException(
            $"The agent did not start within {timeout.TotalSeconds:0} s. See {LogPath}.");
    }

    /// <summary>The agent that is running and answering, if any. Never starts one.</summary>
    public Task<AgentEndpoint?> FindRunningAsync() => FindHealthyAsync();

    private async Task<AgentEndpoint?> FindHealthyAsync()
    {
        var endpoint = ReadEndpoint();
        if (endpoint is null)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Url + "/health");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.Token);

            using var response = await _health.SendAsync(request);
            return response.IsSuccessStatusCode ? endpoint : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null; // stale file: the process it names is gone or not answering
        }
    }

    private static AgentEndpoint? ReadEndpoint()
    {
        try
        {
            var path = AgentDiscovery.EndpointFilePath;
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, ContractsJsonContext.Default.AgentEndpoint);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private string ResolveExecutable()
    {
        var name = OperatingSystem.IsWindows() ? "ConsoleScout.Agent.exe" : "ConsoleScout.Agent";

        if (settings.ExecutablePath is { Length: > 0 } configured)
        {
            return File.Exists(configured)
                ? configured
                : throw new ConfigurationException($"Agent:ExecutablePath points to {configured}, which does not exist.");
        }

        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "agent", name), // the dotnet publish layout
            Path.Combine(AppContext.BaseDirectory, name),
            .. DevelopmentOutput() is { } dev ? [Path.Combine(dev, name)] : Array.Empty<string>(),
        ];

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new ConfigurationException(
                $"Cannot find {name}. Looked in:{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", candidates)}"
                + $"{Environment.NewLine}Set Agent:ExecutablePath (CONSOLESCOUT_AGENT__EXECUTABLEPATH).");
    }

    /// <summary>Debug builds carry the agent's build output directory, so dotnet run works without setup.</summary>
    private static string? DevelopmentOutput() =>
        typeof(AgentLauncher).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "AgentDevelopmentOutput")
            ?.Value;

    private static void Start(string executable)
    {
        Directory.CreateDirectory(AgentDiscovery.DataDirectory);

        ProcessStartInfo start;
        if (OperatingSystem.IsWindows())
        {
            start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        }
        else
        {
            // Started through sh as a background job, so it leaves this process group: Ctrl+C in the
            // terminal stops the CLI but not the agent. Output goes to a log instead of the terminal.
            start = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("\"$0\" >\"$1\" 2>&1 </dev/null &");
            start.ArgumentList.Add(executable);
            start.ArgumentList.Add(LogPath);
        }

        using var process = Process.Start(start)
            ?? throw new AgentUnavailableException($"Could not start {executable}.");
    }
}
