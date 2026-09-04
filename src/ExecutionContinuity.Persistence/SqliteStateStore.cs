using System.Text.Json;
using ExecutionContinuity.Domain;
using Microsoft.Data.Sqlite;

namespace ExecutionContinuity.Persistence;

public sealed class SqliteStateStore : IStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _connectionString;
    private readonly Func<Task>? _beforeCommit;

    public SqliteStateStore(string databasePath, Func<Task>? beforeCommit = null)
    {
        SQLitePCL.Batteries_V2.Init();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        _beforeCommit = beforeCommit;
    }

    public async Task SaveAsync(AppState state, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO app_state (id, payload)
            VALUES (1, $payload)
            ON CONFLICT(id) DO UPDATE SET payload = excluded.payload;
            """;
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(StateDocument.From(state), JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);

        if (_beforeCommit is not null)
        {
            await _beforeCommit();
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AppState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM app_state WHERE id = 1;";
        var payload = await command.ExecuteScalarAsync(cancellationToken);
        if (payload is null or DBNull)
        {
            return AppState.Create();
        }

        var document = JsonSerializer.Deserialize<StateDocument>((string)payload, JsonOptions)
            ?? throw new InvalidDataException("The persisted application state is invalid.");
        return document.ToState().Recover();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = FULL;
            CREATE TABLE IF NOT EXISTS app_state (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                payload TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private sealed record StateDocument(
        IReadOnlyList<Route> Routes,
        ExecutionState Execution,
        IReadOnlyList<ExecutionSnapshot> Snapshots,
        IReadOnlyList<CaptureEntry> Captures)
    {
        public static StateDocument From(AppState state) =>
            new(state.Routes, state.Execution, state.Snapshots, state.Captures);

        public AppState ToState() =>
            AppState.Restore(Routes, Execution, Snapshots, Captures);
    }
}
