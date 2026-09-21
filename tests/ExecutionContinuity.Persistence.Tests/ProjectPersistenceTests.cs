using System.Text.Json.Nodes;
using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ExecutionContinuity.Persistence.Tests;

public sealed class ProjectPersistenceTests
{
    [Fact]
    public async Task Projects_and_route_ownership_survive_store_recreation()
    {
        var path = NewDatabasePath();
        try
        {
            var route = Route.Create("Prepare the session", Step.Create("Check input", "Input checked", "Do not record yet"));
            var state = StateTransitions.SetRouteProject(AppState.Create(route), route.Id, "Graduation recording");

            await new SqliteStateStore(path).SaveAsync(state);
            var recovered = await new SqliteStateStore(path).LoadAsync();

            var project = Assert.Single(recovered.Projects);
            Assert.Equal("Graduation recording", project.Name);
            Assert.Equal(project.Id, recovered.Route(route.Id).ProjectId);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task A_payload_written_before_projects_existed_still_loads_as_unassigned()
    {
        var path = NewDatabasePath();
        try
        {
            var route = Route.Create("Legacy route", Step.Create("Action", "Done", "Boundary"));
            await new SqliteStateStore(path).SaveAsync(AppState.Create(route));
            await StripProjectsFromPayloadAsync(path);

            var recovered = await new SqliteStateStore(path).LoadAsync();

            Assert.Empty(recovered.Projects);
            Assert.Null(recovered.Route(route.Id).ProjectId);
            Assert.Equal("Legacy route", recovered.Route(route.Id).Title);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task StripProjectsFromPayloadAsync(string path)
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
        document.Remove("Projects");

        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE app_state SET payload = $payload WHERE id = 1;";
        update.Parameters.AddWithValue("$payload", document.ToJsonString());
        await update.ExecuteNonQueryAsync();
    }

    private static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"execution-continuity-project-{Guid.NewGuid():N}.db");

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
