namespace ConsoleScout.Contracts;

/// <param name="Id">The answer's number, x&lt;id&gt;.</param>
/// <param name="DialogId">Its dialog's number, d&lt;id&gt;: the id of the dialog's first answer.</param>
public sealed record AppendHistoryResponse(long Id, long DialogId);
