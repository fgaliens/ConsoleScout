namespace ConsoleScout.Output;

/// <summary>Lets a tool say what it is doing ("Searching", "Reading") without knowing how it is shown.</summary>
internal interface IActivityReporter
{
    /// <param name="activity">A verb, e.g. "Searching".</param>
    /// <param name="detail">Plain text, e.g. the query; never interpreted as markup.</param>
    void Report(string activity, string detail);
}
