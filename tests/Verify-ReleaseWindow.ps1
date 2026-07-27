param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot "..\src\ExecutionContinuity.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\ExecutionContinuity.App.exe"),
    [int]$TimeoutSeconds = 10
)

$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
$applicationPri = [IO.Path]::ChangeExtension($resolvedExecutable, ".pri")
if (-not (Test-Path -LiteralPath $applicationPri -PathType Leaf)) {
    throw "Published WinUI resource index is missing: $applicationPri"
}

$process = Start-Process -FilePath $resolvedExecutable -PassThru

try {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()

        if ($process.HasExited) {
            throw "Release application exited before showing a window. Exit code: $($process.ExitCode)."
        }

        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            Write-Output "PASS ProcessId=$($process.Id) MainWindowHandle=$($process.MainWindowHandle)"
            exit 0
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Release application stayed alive but did not expose a main window within $TimeoutSeconds seconds."
}
finally {
    if (-not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }
}
