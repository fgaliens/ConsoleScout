namespace ConsoleScout.Contracts;

public static class ScraperDiscovery
{
    /// <summary>Where a running scraper announces itself. Removed again when it stops.</summary>
    public static string EndpointFilePath { get; } = Path.Combine(DataDirectory, "scraper.json");

    /// <summary>~/.consolescout. Shared with the CLI, which keeps secrets.json here too.</summary>
    public static string DataDirectory
    {
        get
        {
            var profile = UserProfile();
            return Path.Combine(profile, ".consolescout");
        }
    }

    /// <summary>DoNotVerify: by default a missing profile directory yields "", and every path built on it
    /// would quietly become relative to the current directory - secrets.json included.</summary>
    private static string UserProfile()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        return Path.IsPathRooted(profile)
            ? profile
            : throw new InvalidOperationException("The user profile directory cannot be determined (is HOME set?).");
    }
}
