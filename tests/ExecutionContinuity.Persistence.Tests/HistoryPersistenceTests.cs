using System.Text.Json.Nodes;
using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ExecutionContinuity.Persistence.Tests;

public sealed class HistoryPersistenceTests
{
    [Fact]
    public async Task History_survives_store_recreation()
    {
        var path = NewDatabasePath();
        try
        {
            var route = Route.Create("Prepare the session", Step.Create("Check input", "Done", "Boundary"));
            var at = new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);
            var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id, at);

            await new SqliteStateStore(path).SaveAsync(state);
            var recovered = await new SqliteStateStore(path).LoadAsync();

            var recorded = Assert.Single(recovered.History);
            Assert.Equal(HistoryEventKind.RouteStarted, recorded.Kind);
            Assert.Equal(route.Id, recorded.RouteId);
            Assert.Equal("Prepare the session", recorded.Summary);
            Assert.Equal(at, recorded.OccurredAt);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task A_payload_written_before_history_existed_still_loads_without_events()
    {
        var path = NewDatabasePath();
        try
        {
            var route = Route.Create("Legacy route", Step.Create("Action", "Done", "Boundary"));
            var state = StateTransitions.SelectActiveRoute(
                AppState.Create(route),
                route.Id,
                new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero));
            await new SqliteStateStore(path).SaveAsync(state);
            await StripPropertyFromPayloadAsync(path, "History");

            var recovered = await new SqliteStateStore(path).LoadAsync();

            Assert.Empty(recovered.History);
            Assert.Equal("Legacy route", recovered.Route(route.Id).Title);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task StripPropertyFromPayloadAsync(string path, string propertyName)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();

        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT payload FROM app_state WHERE id = 1;";
        var payload = (string)(await read.ExecuteScalarAsync())!;
        var document = JsonNode.Parse(payload)!.AsObject();
        document.Remove(propertyName);

        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE app_state SET payload = $payload WHERE id = 1;";
        update.Parameters.AddWithValue("$payload", document.ToJsonString());
        await update.ExecuteNonQueryAsync();
    }

    private static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"execution-continuity-history-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string path)
    {
        foreach (var candidate in new[] { path, $"{path}-wal", $"{path}-shm" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
