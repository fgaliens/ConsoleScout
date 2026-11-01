using ConsoleScout.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace ConsoleScout.Agent.History;

/// <summary>The questions answered so far, in ~/.consolescout/history.db. One row per answer, linked to the
/// answer it followed up on; root_id is its dialog, the id of the dialog's first answer. The sessions table keeps
/// each terminal's dialog mode and the answer its next question continues. A connection per call (pooled), and
/// WAL, so two terminals can ask at once.</summary>
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
        CREATE TABLE IF NOT EXISTS sessions (
            session     TEXT    PRIMARY KEY,
            dialog_mode INTEGER NOT NULL,
            current_id  INTEGER NULL,
            updated_at  TEXT    NOT NULL
        );
        """;

    private static string DatabasePath => Path.Combine(AgentDiscovery.DataDirectory, "history.db");

    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath,
        Pooling = true,
    }.ToString();

    /// <summary>Creates or upgrades the database, keeps it owner-only and drops what is past KeepDays.</summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(AgentDiscovery.DataDirectory);
        RestrictToOwner();

        await using var connection = await OpenAsync(ct);
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", ct);
        await ExecuteAsync(connection, Schema, ct);
        await AddDialogsAsync(connection, ct);

        var keepDays = Math.Max(1, options.Value.History.KeepDays);
        var cutoff = time.GetUtcNow().AddDays(-keepDays).ToString("O");

        await using var prune = connection.CreateCommand();
        prune.CommandText =
            """
            DELETE FROM entries WHERE created_at < $cutoff;
            DELETE FROM sessions WHERE updated_at < $cutoff;
            """;
        prune.Parameters.AddWithValue("$cutoff", cutoff);
        var removed = await prune.ExecuteNonQueryAsync(ct);
        if (removed > 0)
        {
            logger.LogInformation("Removed {Count} history rows older than {Days} days", removed, keepDays);
        }
    }

    public async Task<AppendHistoryResponse> AppendAsync(AppendHistoryRequest request, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        long? root = request.ParentId is { } parent ? await RootOfAsync(connection, parent, ct) : null;

        await using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO entries (session, parent_id, root_id, question, answer, created_at)
            VALUES ($session, $parent, $root, $question, $answer, $created);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$session", request.Session);
        insert.Parameters.AddWithValue("$parent", (object?)request.ParentId ?? DBNull.Value);
        insert.Parameters.AddWithValue("$root", (object?)root ?? DBNull.Value);
        insert.Parameters.AddWithValue("$question", request.Question);
        insert.Parameters.AddWithValue("$answer", request.Answer);
        var created = time.GetUtcNow().ToString("O");
        insert.Parameters.AddWithValue("$created", created);

        var inserted = await insert.ExecuteScalarAsync(ct);
        var id = Convert.ToInt64(inserted);

        // An answer without a (surviving) parent starts a dialog of its own.
        var dialog = root ?? id;
        if (root is null)
        {
            await using var own = connection.CreateCommand();
            own.CommandText = "UPDATE entries SET root_id = $id WHERE id = $id;";
            own.Parameters.AddWithValue("$id", id);
            await own.ExecuteNonQueryAsync(ct);
        }

        // In dialog mode the next question continues this answer, whether it branched off (-c x5) or began anew (-n).
        await using var advance = connection.CreateCommand();
        advance.CommandText = "UPDATE sessions SET current_id = $id, updated_at = $now WHERE session = $session AND dialog_mode = 1;";
        advance.Parameters.AddWithValue("$id", id);
        advance.Parameters.AddWithValue("$now", created);
        advance.Parameters.AddWithValue("$session", request.Session);
        await advance.ExecuteNonQueryAsync(ct);

        await transaction.CommitAsync(ct);
        return new AppendHistoryResponse(id, dialog);
    }

    /// <summary>What the next question follows up on: an explicit reference, a new dialog, the session's dialog
    /// mode, or nothing - in that order.</summary>
    /// <exception cref="AgentFailure">With <see cref="AgentErrorCodes.NotFound"/> for a reference that matches nothing.</exception>
    public async Task<HistoryContextResponse> ResolveContextAsync(HistoryContextRequest request, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var state = await ReadSessionAsync(connection, request.Session, ct);

        long? anchor = null;
        if (request.Reference is { } reference)
        {
            anchor = await ResolveAsync(connection, request.Session, reference, ct);
        }
        else if (!request.NewDialog && state.DialogMode && state.CurrentId is { } current)
        {
            // The answer may have expired since; then the question simply starts a new dialog.
            anchor = await ExistsAsync(connection, current, ct) ? current : null;
        }

        if (anchor is not { } id)
        {
            return new HistoryContextResponse([], null, state.DialogMode);
        }

        var maxTurns = Math.Clamp(request.MaxTurns, 1, 50);
        var thread = await ReadThreadAsync(connection, id, maxTurns, ct);
        var dialog = await RootOfAsync(connection, id, ct);
        return new HistoryContextResponse(thread, dialog, state.DialogMode);
    }

    /// <summary>Toggles the mode, turns it on (Enable), or turns it on to continue a referenced conversation.</summary>
    /// <exception cref="AgentFailure">With <see cref="AgentErrorCodes.NotFound"/> for a reference that matches nothing;
    /// the mode is then left as it was.</exception>
    public async Task<DialogModeResponse> SetDialogModeAsync(DialogModeRequest request, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var state = await ReadSessionAsync(connection, request.Session, ct);

        bool on;
        long? current;
        if (request.Reference is { } reference)
        {
            on = true;
            current = await ResolveAsync(connection, request.Session, reference, ct);
        }
        else if (request.Enable == true)
        {
            on = true;
            current = state.DialogMode ? state.CurrentId : null;
        }
        else
        {
            // Turning it on starts a new dialog with the next question; turning it off forgets the pointer.
            on = !state.DialogMode;
            current = null;
        }

        await using var upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            INSERT INTO sessions (session, dialog_mode, current_id, updated_at) VALUES ($session, $mode, $current, $now)
            ON CONFLICT (session) DO UPDATE SET dialog_mode = $mode, current_id = $current, updated_at = $now;
            """;
        upsert.Parameters.AddWithValue("$session", request.Session);
        upsert.Parameters.AddWithValue("$mode", on ? 1 : 0);
        upsert.Parameters.AddWithValue("$current", (object?)current ?? DBNull.Value);
        var now = time.GetUtcNow().ToString("O");
        upsert.Parameters.AddWithValue("$now", now);
        await upsert.ExecuteNonQueryAsync(ct);

        long? dialog = current is { } id ? await RootOfAsync(connection, id, ct) : null;
        return new DialogModeResponse(on, state.DialogMode, current, dialog);
    }

    /// <summary>Gives every entry its dialog. Databases from before dialogs existed lack the column; the root of an
    /// entry without a parent is itself, and every other entry inherits its parent's.</summary>
    private static async Task AddDialogsAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var columns = connection.CreateCommand();
        columns.CommandText = "SELECT COUNT(*) FROM pragma_table_info('entries') WHERE name = 'root_id';";
        var count = await columns.ExecuteScalarAsync(ct);
        if (Convert.ToInt64(count) == 0)
        {
            await ExecuteAsync(connection, "ALTER TABLE entries ADD COLUMN root_id INTEGER NULL;", ct);
        }

        await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS entries_root ON entries (root_id, id);", ct);
        await ExecuteAsync(connection, "UPDATE entries SET root_id = id WHERE root_id IS NULL AND parent_id IS NULL;", ct);

        // Entries whose parent has expired start dialogs of their own.
        await ExecuteAsync(connection,
            "UPDATE entries SET root_id = id WHERE root_id IS NULL AND parent_id NOT IN (SELECT id FROM entries);", ct);

        // One generation per pass; the chains are short.
        while (true)
        {
            await using var inherit = connection.CreateCommand();
            inherit.CommandText =
                """
                UPDATE entries SET root_id = (SELECT p.root_id FROM entries p WHERE p.id = entries.parent_id)
                WHERE root_id IS NULL AND (SELECT p.root_id FROM entries p WHERE p.id = entries.parent_id) IS NOT NULL;
                """;
            var updated = await inherit.ExecuteNonQueryAsync(ct);
            if (updated == 0)
            {
                break;
            }
        }
    }

    /// <summary>The entry a reference points at.</summary>
    /// <exception cref="AgentFailure">With <see cref="AgentErrorCodes.NotFound"/> when there is none.</exception>
    private static async Task<long> ResolveAsync(SqliteConnection connection, string session, HistoryReference reference, CancellationToken ct)
    {
        if (reference.Id is { } id)
        {
            if (!await ExistsAsync(connection, id, ct))
            {
                throw new AgentFailure(AgentErrorCodes.NotFound, $"There is no answer x{id}.");
            }

            return id;
        }

        if (reference.DialogId is { } dialog)
        {
            return await FindLatestOfDialogAsync(connection, dialog, ct);
        }

        return await FindRecentAsync(connection, session, reference.Back, ct);
    }

    private static async Task<long> FindLatestOfDialogAsync(SqliteConnection connection, long dialog, CancellationToken ct)
    {
        await using var find = connection.CreateCommand();
        find.CommandText = "SELECT id FROM entries WHERE root_id = $dialog ORDER BY id DESC LIMIT 1;";
        find.Parameters.AddWithValue("$dialog", dialog);
        var latest = await find.ExecuteScalarAsync(ct);
        if (latest is not null)
        {
            return Convert.ToInt64(latest);
        }

        // A common mix-up: the number of an answer in the middle of a dialog, given as a dialog.
        if (await ExistsAsync(connection, dialog, ct))
        {
            var root = await RootOfAsync(connection, dialog, ct);
            throw new AgentFailure(AgentErrorCodes.NotFound,
                $"x{dialog} belongs to dialog d{root}: use -c d{root} (its latest answer) or -c x{dialog} (this answer).");
        }

        throw new AgentFailure(AgentErrorCodes.NotFound, $"There is no dialog d{dialog}.");
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

    /// <summary>The entry and the ones it followed up on, oldest first, at most <paramref name="maxTurns"/>.</summary>
    private static async Task<IReadOnlyList<HistoryEntry>> ReadThreadAsync(SqliteConnection connection, long id, int maxTurns, CancellationToken ct)
    {
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

        thread.Reverse();
        return thread;
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

    /// <returns>The entry's dialog, or null when the entry does not exist.</returns>
    private static async Task<long?> RootOfAsync(SqliteConnection connection, long id, CancellationToken ct)
    {
        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT root_id FROM entries WHERE id = $id;";
        read.Parameters.AddWithValue("$id", id);
        var root = await read.ExecuteScalarAsync(ct);
        return root is null or DBNull ? null : Convert.ToInt64(root);
    }

    private static async Task<bool> ExistsAsync(SqliteConnection connection, long id, CancellationToken ct)
    {
        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT 1 FROM entries WHERE id = $id;";
        read.Parameters.AddWithValue("$id", id);
        var found = await read.ExecuteScalarAsync(ct);
        return found is not null;
    }

    private static async Task<SessionState> ReadSessionAsync(SqliteConnection connection, string session, CancellationToken ct)
    {
        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT dialog_mode, current_id FROM sessions WHERE session = $session;";
        read.Parameters.AddWithValue("$session", session);

        await using var reader = await read.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return SessionState.Off;
        }

        var on = reader.GetInt64(0) == 1;
        long? current = reader.IsDBNull(1) ? null : reader.GetInt64(1);
        return new SessionState(on, current);
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
