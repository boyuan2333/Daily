using System.Diagnostics;

var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var databasePath = Path.Combine(repositoryRoot, ".ui-review", "ui004-fixture", "execution-continuity.db");
if (!File.Exists(databasePath))
{
    Console.Error.WriteLine($"Fixture database does not exist: {databasePath}");
    return 1;
}

return await LaunchAsync(repositoryRoot, databasePath, failSave: false);

static async Task<int> LaunchAsync(string repositoryRoot, string databasePath, bool failSave)
{
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
        Console.Error.WriteLine($"Build the Debug application before interactive fixture launch: {executablePath}");
        return 1;
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
    if (failSave)
    {
        startInfo.Environment["EXECUTION_CONTINUITY_FAIL_SAVE"] = "1";
    }

    using var child = Process.Start(startInfo);
    if (child is null)
    {
        Console.Error.WriteLine("Interactive fixture application did not start.");
        return 1;
    }

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
        $"startupTrace={startupTracePath}",
        $"failSave={failSave}"
    ]);
    Console.WriteLine($"Interactive fixture launched: PID={child.Id}, SessionId={child.SessionId}");
    await child.WaitForExitAsync();
    return child.ExitCode;
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
