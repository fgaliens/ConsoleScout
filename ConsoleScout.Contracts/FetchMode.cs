namespace ConsoleScout.Contracts;

/// <summary>How the scraper loads a page.</summary>
public enum FetchMode
{
    /// <summary>Plain HTTP first; the browser only when the page turns out to need JavaScript.</summary>
    Auto,
    Http,
    Browser,
}
