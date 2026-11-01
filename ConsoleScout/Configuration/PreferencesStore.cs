namespace ConsoleScout.Configuration;

/// <summary>Writes the user's preferences into preferences.json, kept apart from the keys in secrets.json.
/// Same shape as appsettings.json, so <see cref="ConfigurationLoader"/> layers it like any other source.</summary>
internal static class PreferencesStore
{
    public static void SetShowDonateHint(bool show)
    {
        string[] keys = ["preferences", "showDonateHint"];
        UserSettingsFile.Set(ConfigurationLoader.PreferencesPath, keys, show, ownerOnly: false);
    }
}
