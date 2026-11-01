using System.Text;
using ConsoleScout.AI;
using ConsoleScout.Configuration;
using ConsoleScout.Output;
using ConsoleScout.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleScout.App;

/// <summary>Runs one invocation: configuration, services, the command, and the mapping of failures to
/// messages and exit codes. Program.cs only parses arguments and calls this.</summary>
internal static class ConsoleScoutApp
{
    public static async Task<int> RunAsync(CommandLineOptions options, CancellationToken ct)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            // --set-key, --donate, --no-donate, --status, --readme and the guide need nothing from the configuration, and must keep working
            // when it is broken: saving a key is one way to repair it. Defaults stand in for the settings.
            var configuration = options.NeedsNoConfiguration
                ? new ConfigurationBuilder().Build()
                : ConfigurationLoader.Build();
            var settings = options.NeedsNoConfiguration
                ? new AppSettings()
                : ConfigurationLoader.Bind(configuration);

            await using var provider = new ServiceCollection()
                .AddConsoleScout(configuration, settings)
                .BuildServiceProvider();

            if (options.SetKeys.Count > 0)
            {
                provider.GetRequiredService<SetKeyCommand>().Run(options.SetKeys);
                return 0;
            }

            if (options.ShowsGuide)
            {
                var asked = await provider.GetRequiredService<GuideCommand>().RunAsync();
                if (asked is null)
                {
                    return 0;
                }

                // A fresh run for the typed question: it needs the configuration the guide did without.
                var withQuestion = options with { Question = asked };
                return await RunAsync(withQuestion, ct);
            }

            if (options.Status)
            {
                return await provider.GetRequiredService<StatusCommand>().RunAsync();
            }

            if (options.Readme)
            {
                provider.GetRequiredService<ReadmeCommand>().Run();
                return 0;
            }

            if (options.Donate)
            {
                return provider.GetRequiredService<DonateCommand>().Run(options.DonateNetwork);
            }

            if (options.NoDonate)
            {
                provider.GetRequiredService<NoDonateCommand>().Run();
                return 0;
            }

            if (options.RunAgent)
            {
                await provider.GetRequiredService<RunAgentCommand>().RunAsync(ct);
                return 0;
            }

            // Every other action has returned above, and without one ShowsGuide holds.
            if (options.Question is not { } question)
            {
                return 0;
            }

            // The agent (and its browser) takes seconds to start; it can do so while the model thinks, so the
            // history is ready by the time the answer is saved.
            if (settings.UsesAgent())
            {
                provider.GetRequiredService<AgentLauncher>().StartInBackground();
            }

            var length = options.Length ?? settings.Preferences.AnswerLength;
            await provider.GetRequiredService<AskCommand>().RunAsync(question, length, options.Context, ct);
            return 0;
        }
        catch (Exception ex) when (ex is ConfigurationException or AiException or AgentUnavailableException
            or InputUnavailableException or ContextUnavailableException)
        {
            ErrorConsole.Write(ex.Message);
            return 1;
        }
        catch (AgentException ex)
        {
            ErrorConsole.Write($"The agent failed: {ex.Message}");
            return 1;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return 130;
        }
        catch (Exception ex)
        {
            var details = ex.ToString();
            ErrorConsole.Write(details);
            return 1;
        }
    }
}
