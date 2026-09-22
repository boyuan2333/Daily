using System.Diagnostics;
using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;

var reuseExisting = args.Length == 1 && string.Equals(args[0], "--reuse", StringComparison.OrdinalIgnoreCase);

if (args.Length > 1)
{
    Console.Error.WriteLine("Usage: ExecutionContinuity.UiFixture [database-path | --reuse]");
    return 2;
}

try
{
    var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
    var databasePath = Path.GetFullPath(reuseExisting
        ? Path.Combine(repositoryRoot, ".ui-review", "ui004-fixture", "execution-continuity.db")
        : args.Length == 1
        ? args[0]
        : Path.Combine(repositoryRoot, ".ui-review", "ui004-fixture", "execution-continuity.db"));

    if (reuseExisting)
    {
        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException("Cannot reuse a fixture database that does not exist.", databasePath);
        }

        Console.WriteLine($"Reusing fixture: {databasePath}");
    }
    else
    {
        await CreateFixtureAsync(databasePath);
        Console.WriteLine($"Fixture created: {databasePath}");
    }

    if (args.Length == 1 && !reuseExisting)
    {
        return 0;
    }

    var executablePath = Path.Combine(
        repositoryRoot,
        "src",
        "ExecutionContinuity.App",
        "bin",
        "Debug",
        "net10.0-windows10.0.19041.0",
        "win-x64",
        "ExecutionContinuity.App.exe");
    if (!File.Exists(executablePath))
    {
        throw new FileNotFoundException("Build the Debug application before interactive fixture launch.", executablePath);
    }

    var fixtureDirectory = Path.GetDirectoryName(databasePath)!;
    var startupTracePath = Path.Combine(fixtureDirectory, "interactive-startup-trace.log");
    var launcherEvidencePath = Path.Combine(fixtureDirectory, "interactive-launcher-evidence.txt");
    File.WriteAllText(startupTracePath, string.Empty);

    var startInfo = new ProcessStartInfo
    {
        FileName = executablePath,
        UseShellExecute = false,
        WorkingDirectory = Path.GetDirectoryName(executablePath)!
    };
    startInfo.Environment["EXECUTION_CONTINUITY_DATABASE"] = databasePath;
    startInfo.Environment["EXECUTION_CONTINUITY_STARTUP_TRACE"] = startupTracePath;

    using var child = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Interactive fixture application did not start.");
    var parent = Process.GetCurrentProcess();
    File.WriteAllLines(launcherEvidencePath,
    [
        $"launchedAtUtc={DateTimeOffset.UtcNow:O}",
        $"launcherPid={parent.Id}",
        $"launcherSessionId={parent.SessionId}",
        $"childPid={child.Id}",
        $"childSessionId={child.SessionId}",
        $"executable={executablePath}",
        $"database={databasePath}",
        $"startupTrace={startupTracePath}"
    ]);

    Console.WriteLine($"Interactive fixture launched: PID={child.Id}, SessionId={child.SessionId}");
    Console.WriteLine($"Evidence: {launcherEvidencePath}");
    child.WaitForExit();
    return child.ExitCode;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"UI fixture failed: {exception}");
    return 1;
}

static async Task CreateFixtureAsync(string databasePath)
{
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    var currentRoute = Route.Create(
        "UI-004 current route",
        Step.Create(
            "Open the current route detail",
            "The route detail is visible",
            "Do not switch routes while checking the detail"));
    var pausedRoute = Route.Create(
        "UI-004 paused route",
        Step.Create(
            "Review the paused route context",
            "The retained context is understood",
            "Do not change the route while reviewing it"));
    var archivedRoute = Route.Create(
        "UI-002 archived route",
        Step.Create(
            "Restore this archived route from Archive",
            "The route appears in the Routes list again",
            "Do not activate the route while restoring it"));

    var routes = new List<Route> { currentRoute, pausedRoute, archivedRoute };
    for (var index = 1; index <= 8; index++)
    {
        routes.Add(Route.Create(
            $"UI-004 draft route {index:00}",
            Step.Create(
                $"Review fixture route {index:00}",
                "The fixture route detail is visible",
                "Do not activate the fixture route")));
    }

    var state = AppState.Create(routes.ToArray());
    state = StateTransitions.SelectActiveRoute(state, currentRoute.Id);
    state = StateTransitions.SelectActiveRoute(
        state,
        pausedRoute.Id,
        new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.FromHours(8)),
        "Fixture pause");
    state = StateTransitions.SelectActiveRoute(
        state,
        currentRoute.Id,
        new DateTimeOffset(2026, 8, 5, 1, 1, 0, TimeSpan.FromHours(8)));
    state = StateTransitions.ArchiveRoute(state, archivedRoute.Id);
    state = StateTransitions.Capture(
        state,
        "UI-004 Inbox detail fixture",
        new DateTimeOffset(2026, 8, 5, 1, 2, 0, TimeSpan.FromHours(8)));
    for (var index = 1; index <= 12; index++)
    {
        state = StateTransitions.Capture(
            state,
            $"UI-004 Inbox fixture entry {index:00}",
            new DateTimeOffset(2026, 8, 5, 1, 2 + index, 0, TimeSpan.FromHours(8)));
    }
    state = StateTransitions.Capture(
        state,
        "UI-002 archived Inbox fixture entry",
        new DateTimeOffset(2026, 8, 5, 1, 30, 0, TimeSpan.FromHours(8)));
    state = StateTransitions.ArchiveCapture(state, state.Captures.Last().Id);

    await new SqliteStateStore(databasePath).SaveAsync(state);
}

static string FindRepositoryRoot(string startDirectory)
{
    for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "ExecutionContinuity.slnx")))
        {
            return directory.FullName;
        }
    }

    throw new DirectoryNotFoundException($"Could not find ExecutionContinuity.slnx above '{startDirectory}'.");
}
