using ConsoleScout.Configuration;
using ConsoleScout.Output;

namespace ConsoleScout.App;

/// <summary>--no-donate: turns off the donation hint under the spinner for good, via preferences.json.</summary>
internal sealed class NoDonateCommand(IConsoleUi ui)
{
    public void Run()
    {
        PreferencesStore.SetShowDonateHint(false);

        ui.WriteInfo($"The donation hint is off (saved to {ConfigurationLoader.PreferencesPath}).");
        ui.WriteInfo("To turn it back on, remove preferences.showDonateHint from that file.");
    }
}
