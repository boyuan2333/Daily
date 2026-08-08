param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot "..\src\ExecutionContinuity.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\ExecutionContinuity.App.exe"),
    [int]$TimeoutSeconds = 10,
    [string]$ExpectedWindowTitle = "Daily",
    [string]$DatabasePath = (Join-Path $PSScriptRoot "..\.ui-review\ui004-fixture\execution-continuity.db"),
    [string]$StartupTracePath = (Join-Path $PSScriptRoot "..\.ui-review\ui004-fixture\startup-trace.log"),
    [switch]$KeepRunning
)

Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class ReleaseWindowProbe
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    public static bool TryFindWindow(uint processId, string expectedTitle, out IntPtr handle, out string title)
    {
        var foundHandle = IntPtr.Zero;
        var foundTitle = string.Empty;
        var found = false;
        EnumWindows(delegate(IntPtr hWnd, IntPtr unused)
        {
            uint ownerProcessId;
            GetWindowThreadProcessId(hWnd, out ownerProcessId);
            if (ownerProcessId != processId || !IsWindowVisible(hWnd))
            {
                return true;
            }

            var buffer = new StringBuilder(512);
            GetWindowText(hWnd, buffer, buffer.Capacity);
            var candidateTitle = buffer.ToString();
            if (foundHandle == IntPtr.Zero)
            {
                foundHandle = hWnd;
                foundTitle = candidateTitle;
            }

            if (string.Equals(candidateTitle, expectedTitle, StringComparison.Ordinal))
            {
                foundHandle = hWnd;
                foundTitle = candidateTitle;
                found = true;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        handle = foundHandle;
        title = foundTitle;
        return found;
    }
}
"@

$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
$resolvedDatabasePath = (Resolve-Path -LiteralPath $DatabasePath -ErrorAction Stop).Path
$resolvedStartupTracePath = [IO.Path]::GetFullPath($StartupTracePath)
$startupTraceDirectory = Split-Path -Parent $resolvedStartupTracePath
if (-not [string]::IsNullOrWhiteSpace($startupTraceDirectory)) {
    [IO.Directory]::CreateDirectory($startupTraceDirectory) | Out-Null
}
[IO.File]::WriteAllText($resolvedStartupTracePath, [string]::Empty)
$applicationPri = [IO.Path]::ChangeExtension($resolvedExecutable, ".pri")
if (-not (Test-Path -LiteralPath $applicationPri -PathType Leaf)) {
    throw "Published WinUI resource index is missing: $applicationPri"
}

$previousDatabasePath = $env:EXECUTION_CONTINUITY_DATABASE
$previousStartupTracePath = $env:EXECUTION_CONTINUITY_STARTUP_TRACE
$env:EXECUTION_CONTINUITY_DATABASE = $resolvedDatabasePath
$env:EXECUTION_CONTINUITY_STARTUP_TRACE = $resolvedStartupTracePath
$process = $null

try {
    $process = Start-Process -FilePath $resolvedExecutable -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()

        if ($process.HasExited) {
            throw "Release application exited before showing a window. Exit code: $($process.ExitCode)."
        }

        $windowHandle = [IntPtr]::Zero
        $windowTitle = ""
        $found = [ReleaseWindowProbe]::TryFindWindow(
            [uint32]$process.Id,
            $ExpectedWindowTitle,
            [ref]$windowHandle,
            [ref]$windowTitle)
        if ($found -and $windowHandle -ne [IntPtr]::Zero) {
            Write-Output "PASS ProcessId=$($process.Id) MainWindowHandle=$windowHandle Title='$windowTitle' DatabasePath='$resolvedDatabasePath' StartupTracePath='$resolvedStartupTracePath'"
            if ($KeepRunning) {
                Write-Output "KEEP_RUNNING ProcessId=$($process.Id) MainWindowHandle=$windowHandle DatabasePath='$resolvedDatabasePath' StartupTracePath='$resolvedStartupTracePath'"
                $process.WaitForExit()
                return
            }

            exit 0
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Release application stayed alive but did not expose a '$ExpectedWindowTitle' window within $TimeoutSeconds seconds. Last handle: $windowHandle. Last title: '$windowTitle'."
}
finally {
    if (-not $KeepRunning -and $null -ne $process -and -not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }

    if ($null -eq $previousDatabasePath) {
        Remove-Item Env:\EXECUTION_CONTINUITY_DATABASE -ErrorAction SilentlyContinue
    }
    else {
        $env:EXECUTION_CONTINUITY_DATABASE = $previousDatabasePath
    }

    if ($null -eq $previousStartupTracePath) {
        Remove-Item Env:\EXECUTION_CONTINUITY_STARTUP_TRACE -ErrorAction SilentlyContinue
    }
    else {
        $env:EXECUTION_CONTINUITY_STARTUP_TRACE = $previousStartupTracePath
    }
}
