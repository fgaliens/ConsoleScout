using ConsoleScout.Configuration;
using ConsoleScout.Output;

namespace ConsoleScout.App;

/// <summary>--set-key: stores model API keys in secrets.json. "main=sk-..." takes the key from the
/// argument; a bare "main" asks for it without echoing, which also keeps it out of the shell history.</summary>
internal sealed class SetKeyCommand(IConsoleUi ui)
{
    public void Run(IReadOnlyList<string> assignments)
    {
        foreach (var assignment in assignments)
        {
            var separator = assignment.IndexOf('=');
            var model = (separator < 0 ? assignment : assignment[..separator]).Trim().ToLowerInvariant();

            if (!SecretsStore.ModelNames.Contains(model))
            {
                throw new ConfigurationException(
                    $"Unknown model '{model}' in --set-key. Available: {string.Join(", ", SecretsStore.ModelNames)}.");
            }

            var key = separator < 0 ? Ask(model) : assignment[(separator + 1)..];
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ConfigurationException($"The API key for the {model} model is empty; nothing was saved.");
            }

            var trimmed = key.Trim();
            SecretsStore.SetModelKey(model, trimmed);
            ui.WriteInfo($"Saved the {model} model key to {ConfigurationLoader.SecretsPath}.");

            if (SecretsStore.OverridingVariable(model) is { } variable)
            {
                ui.WriteInfo($"Note: {variable} is set in this environment and takes precedence over the saved key.");
            }
        }
    }

    private string Ask(string model)
    {
        try
        {
            return ui.AskSecret($"API key for the {model} model:");
        }
        catch (InputUnavailableException ex)
        {
            throw new InputUnavailableException($"{ex.Message} Use --set-key {model}=<key> instead.");
        }
    }
}
