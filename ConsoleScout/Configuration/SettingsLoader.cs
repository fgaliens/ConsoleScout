using System.Text.Json;

namespace ConsoleScout.Configuration;

internal static class SettingsLoader
{
    private const string FileName = "appsettings.json";

    public static AppSettings Load()
    {
        // AppContext.BaseDirectory resolves next to the executable under both dotnet run and a published build.
        var path = Path.Combine(AppContext.BaseDirectory, FileName);
        if (!File.Exists(path))
            return new AppSettings();

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, AppJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"{path} could not be read as settings.{Environment.NewLine}{ex.Message}");
        }
        catch (IOException ex)
        {
            throw new ConfigurationException($"Cannot read {path}.{Environment.NewLine}{ex.Message}");
        }
    }
}
