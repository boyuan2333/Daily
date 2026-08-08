namespace ExecutionContinuity.App;

internal static class StartupDiagnostics
{
    private const string DirectoryName = "ExecutionContinuity";
    private const string FileName = "startup-error.txt";
    internal const string StartupTraceEnvironmentVariable = "EXECUTION_CONTINUITY_STARTUP_TRACE";

    internal static void Record(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DirectoryName);
            Directory.CreateDirectory(directory);
            Write(exception, DateTimeOffset.UtcNow, Path.Combine(directory, FileName));
        }
        catch
        {
            // Diagnostics must never hide the original startup exception.
        }
    }

    internal static string Format(Exception exception, DateTimeOffset occurredAtUtc) =>
        $"OccurredAtUtc: {occurredAtUtc:O}{Environment.NewLine}{exception}";

    internal static void Write(Exception exception, DateTimeOffset occurredAtUtc, string path) =>
        File.WriteAllText(path, Format(exception, occurredAtUtc));

    internal static void Trace(string stage, string? explicitPath = null)
    {
        try
        {
            var path = explicitPath ?? Environment.GetEnvironmentVariable(StartupTraceEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var resolvedPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(resolvedPath, $"OccurredAtUtc={DateTimeOffset.UtcNow:O} Stage={stage}{Environment.NewLine}");
        }
        catch
        {
            // Optional tracing must never affect application startup.
        }
    }
}
