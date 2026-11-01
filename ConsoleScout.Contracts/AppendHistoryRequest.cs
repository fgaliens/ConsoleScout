namespace ConsoleScout.Contracts;

/// <param name="Session">The terminal session the question was asked in.</param>
/// <param name="ParentId">The entry this question follows up on, if any.</param>
public sealed record AppendHistoryRequest(string Session, long? ParentId, string Question, string Answer);
