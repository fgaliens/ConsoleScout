using ConsoleScout.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace ConsoleScout.Agent.History;

/// <summary>The questions answered so far, in ~/.consolescout/history.db: one row per answer, linked to the
/// answer it followed up on. A connection per call (pooled), and WAL, so two terminals can ask at once.</summary>
internal sealed class HistoryStore(IOptions<AgentSettings> options, TimeProvider time, ILogger<HistoryStore> logger)
{
    private const string Schema =
        """
        CREATE TABLE IF NOT EXISTS entries (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            session    TEXT    NOT NULL,
            parent_id  INTEGER NULL,
            question   TEXT    NOT NULL,
            answer     TEXT    NOT NULL,
            created_at TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS entries_session ON entries (session, id);
        """;

    private static string DatabasePath => Path.Combine(AgentDiscovery.DataDirectory, "history.db");

    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath,
        Pooling = true,
    }.ToString();

    /// <summary>Creates the database if needed, keeps it owner-only and drops entries past KeepDays.</summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(AgentDiscovery.DataDirectory);
        RestrictToOwner();

        await using var connection = await OpenAsync(ct);
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", ct);
        await ExecuteAsync(connection, Schema, ct);

        var keepDays = Math.Max(1, options.Value.History.KeepDays);
        var cutoff = time.GetUtcNow().AddDays(-keepDays).ToString("O");

        await using var prune = connection.CreateCommand();
        prune.CommandText = "DELETE FROM entries WHERE created_at < $cutoff;";
        prune.Parameters.AddWithValue("$cutoff", cutoff);
        var removed = await prune.ExecuteNonQueryAsync(ct);
        if (removed > 0)
        {
            logger.LogInformation("Removed {Count} history entries older than {Days} days", removed, keepDays);
        }
    }

    public async Task<long> AppendAsync(AppendHistoryRequest request, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO entries (session, parent_id, question, answer, created_at)
            VALUES ($session, $parent, $question, $answer, $created);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$session", request.Session);
        insert.Parameters.AddWithValue("$parent", (object?)request.ParentId ?? DBNull.Value);
        insert.Parameters.AddWithValue("$question", request.Question);
        insert.Parameters.AddWithValue("$answer", request.Answer);
        var created = time.GetUtcNow().ToString("O");
        insert.Parameters.AddWithValue("$created", created);

        var id = await insert.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    /// <exception cref="AgentFailure">With <see cref="AgentErrorCodes.NotFound"/> when the entry does not exist.</exception>
    public async Task<IReadOnlyList<HistoryEntry>> GetThreadAsync(HistoryThreadRequest request, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);

        var id = request.Id ?? await FindRecentAsync(connection, request.Session, request.Back, ct);
        var maxTurns = Math.Clamp(request.MaxTurns, 1, 50);

        var thread = new List<HistoryEntry>();
        long? next = id;
        while (next is { } current && thread.Count < maxTurns)
        {
            var (entry, parent) = await ReadAsync(connection, current, ct);
            if (entry is null)
            {
                break;
            }

            thread.Add(entry);
            next = parent;
        }

        if (thread.Count == 0)
        {
            throw new AgentFailure(AgentErrorCodes.NotFound, $"There is no answer x{id}.");
        }

        thread.Reverse();
        return thread;
    }

    private static async Task<long> FindRecentAsync(SqliteConnection connection, string session, int back, CancellationToken ct)
    {
        await using var find = connection.CreateCommand();
        find.CommandText = "SELECT id FROM entries WHERE session = $session ORDER BY id DESC LIMIT 1 OFFSET $back;";
        find.Parameters.AddWithValue("$session", session);
        var offset = Math.Max(0, back);
        find.Parameters.AddWithValue("$back", offset);

        var id = await find.ExecuteScalarAsync(ct);
        if (id is null)
        {
            var message = offset == 0
                ? "No question has been asked in this terminal yet."
                : $"This terminal has no question {offset} back.";
            throw new AgentFailure(AgentErrorCodes.NotFound, message);
        }

        return Convert.ToInt64(id);
    }

    private static async Task<(HistoryEntry? Entry, long? Parent)> ReadAsync(SqliteConnection connection, long id, CancellationToken ct)
    {
        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT question, answer, parent_id FROM entries WHERE id = $id;";
        read.Parameters.AddWithValue("$id", id);

        await using var reader = await read.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return (null, null);
        }

        var question = reader.GetString(0);
        var answer = reader.GetString(1);
        long? parent = reader.IsDBNull(2) ? null : reader.GetInt64(2);
        return (new HistoryEntry(id, question, answer), parent);
    }

    /// <summary>The history holds every question and answer, so it is owner-only. The file is created here,
    /// before SQLite opens it, because SQLite gives its -wal and -shm files the mode of the database file.</summary>
    private static void RestrictToOwner()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        if (!File.Exists(DatabasePath))
        {
            using var created = File.Create(DatabasePath);
        }

        const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        string[] files = [DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm"];
        foreach (var file in files.Where(File.Exists))
        {
            File.SetUnixFileMode(file, ownerOnly);
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
