namespace ConsoleScout.Configuration;

/// <summary>Configuration is missing or unusable, with a message meant for the user.</summary>
internal sealed class ConfigurationException(string message) : Exception(message);