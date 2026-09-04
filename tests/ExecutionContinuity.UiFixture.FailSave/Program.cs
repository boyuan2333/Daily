using System.Diagnostics;

var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var databasePath = Path.Combine(repositoryRoot, ".ui-review", "ui004-fixture", "execution-continuity.db");
var seedExecutablePath = Path.Combine(
    repositoryRoot,
    "tests",
    "ExecutionContinuity.UiFixture",
    "bin",
    "Debug",
    "net10.0",
    "ExecutionContinuity.UiFixture.exe");
if (!File.Exists(seedExecutablePath))
{
    Console.Error.WriteLine($"Build the regular fixture before launching the fail-save fixture: {seedExecutablePath}");
    return 1;
}
var seedStartInfo = new ProcessStartInfo
{
    FileName = seedExecutablePath,
    UseShellExecute = false,
    WorkingDirectory = Path.GetDirectoryName(seedExecutablePath)!
};
seedStartInfo.ArgumentList.Add(databasePath);
using (var seed = Process.Start(seedStartInfo) ?? throw new InvalidOperationException("Could not start fixture seeder."))
{
    await seed.WaitForExitAsync();
    if (seed.ExitCode != 0)
    {
        Console.Error.WriteLine($"Fixture seeder failed with exit code {seed.ExitCode}.");
        return seed.ExitCode;
    }
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
startInfo.Environment["EXECUTION_CONTINUITY_FAIL_SAVE"] = "1";
using var child = Process.Start(startInfo);
if (child is null)
{
    Console.Error.WriteLine("Interactive fail-save fixture application did not start.");
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
    "failSave=True"
]);
Console.WriteLine($"Interactive fail-save fixture launched: PID={child.Id}, SessionId={child.SessionId}");
await child.WaitForExitAsync();
return child.ExitCode;

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
