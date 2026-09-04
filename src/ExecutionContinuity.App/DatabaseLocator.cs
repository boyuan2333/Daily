namespace ExecutionContinuity.App;

public static class DatabaseLocator
{
    public const string OverrideEnvironmentVariable = "EXECUTION_CONTINUITY_DATABASE";
    public const string FailSaveEnvironmentVariable = "EXECUTION_CONTINUITY_FAIL_SAVE";

    public static string Resolve(string? explicitPath = null)
    {
        StartupDiagnostics.Trace("DatabaseLocator.Resolve entered");
        var configuredPath = explicitPath ?? Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var resolvedPath = Path.GetFullPath(configuredPath);
            var directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            StartupDiagnostics.Trace($"DatabaseLocator.Resolve configured path='{resolvedPath}'");
            return resolvedPath;
        }

        var defaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ExecutionContinuity");
        Directory.CreateDirectory(defaultDirectory);
        var defaultPath = Path.Combine(defaultDirectory, "execution-continuity.db");
        StartupDiagnostics.Trace($"DatabaseLocator.Resolve default path='{defaultPath}'");
        return defaultPath;
    }
}
