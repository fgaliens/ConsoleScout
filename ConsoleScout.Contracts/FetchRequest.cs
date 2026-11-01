namespace ConsoleScout.Contracts;

public sealed record FetchRequest(string Url, FetchMode Mode = FetchMode.Auto, int MaxChars = 8000);
