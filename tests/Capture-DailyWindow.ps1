# Capture-DailyWindow.ps1
#
# Launches the unpackaged Daily app against an isolated database, waits for the real window,
# drives optional scripted input, and writes PNG screenshots. It exists because an agent or a
# reviewer without screen access can still read the captured PNGs and inspect the rendered UI.
#
# Usage:
#   & .\tests\Capture-DailyWindow.ps1 `
#       -ExecutablePath .\src\ExecutionContinuity.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\ExecutionContinuity.App.exe `
#       -DatabasePath .\.ui-review\ui004-fixture\execution-continuity.db `
#       -OutputDir .\.ui-review\captures
#
# Optional scripted actions (window-relative pixels, joined with '|'):
#   click:544,63        click at window coordinate
#   scroll:390,450,-120 mouse wheel at window coordinate
#   wait:800            sleep in milliseconds
#   capture:name        write <OutputDir>\name.png
#
# Example:
#   -Actions "click:544,63|wait:900|capture:planning|click:105,269|wait:900|capture:routes"
#
# Notes:
#   - Coordinates are relative to the window's client origin captured at run time, so the tool
#     keeps working if the window moves. Re-derive coordinates from a fresh screenshot after any
#     layout change.
#   - The mouse wheel is not reliably delivered to the app from this launcher on every system;
#     prefer fixtures whose content fits the first screen over scrolling.
#   - Requires an interactive desktop session. A locked or disconnected session produces a black
#     frame, which the script reports through the capture method.

param(
    [Parameter(Mandatory = $true)][string]$ExecutablePath,
    [Parameter(Mandatory = $true)][string]$DatabasePath,
    [Parameter(Mandatory = $true)][string]$OutputDir,
    [string]$Actions = "capture:default",
    [int]$WaitSeconds = 15
)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class DailyWindowDriver
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    public static IntPtr FindDailyWindow(uint processId)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hWnd, IntPtr unused)
        {
            uint owner;
            GetWindowThreadProcessId(hWnd, out owner);
            if (owner != processId || !IsWindowVisible(hWnd)) return true;

            var buffer = new StringBuilder(512);
            GetWindowText(hWnd, buffer, buffer.Capacity);
            if (string.Equals(buffer.ToString(), "Daily", StringComparison.Ordinal))
            {
                found = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void Click(int screenX, int screenY)
    {
        SetCursorPos(screenX, screenY);
        System.Threading.Thread.Sleep(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    public static void Wheel(int screenX, int screenY, int delta)
    {
        SetCursorPos(screenX, screenY);
        System.Threading.Thread.Sleep(120);
        mouse_event(0x0800, 0, 0, unchecked((uint)delta), UIntPtr.Zero);
    }
}
"@

function Test-MostlyBlack([System.Drawing.Bitmap]$Bitmap) {
    $samples = 0
    $dark = 0
    $stepX = [Math]::Max(1, [int]($Bitmap.Width / 24))
    $stepY = [Math]::Max(1, [int]($Bitmap.Height / 24))
    for ($x = 0; $x -lt $Bitmap.Width; $x += $stepX) {
        for ($y = 0; $y -lt $Bitmap.Height; $y += $stepY) {
            $pixel = $Bitmap.GetPixel($x, $y)
            $samples++
            if ($pixel.R -lt 12 -and $pixel.G -lt 12 -and $pixel.B -lt 12) { $dark++ }
        }
    }
    return ($samples -gt 0 -and ($dark * 100 / $samples) -gt 92)
}

function Save-DailyWindow([IntPtr]$Handle, [string]$Path) {
    $rect = New-Object DailyWindowDriver+RECT
    if (-not [DailyWindowDriver]::GetWindowRect($Handle, [ref]$rect)) { throw "GetWindowRect failed" }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw "empty window rectangle" }

    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    try { $printed = [DailyWindowDriver]::PrintWindow($Handle, $hdc, 2) }
    finally { $graphics.ReleaseHdc($hdc) }

    $method = "print-window"
    if (-not $printed -or (Test-MostlyBlack $bitmap)) {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
        $method = "screen-copy"
    }

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    return "size=${width}x${height} method=$method"
}

$resolvedExe = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
$resolvedDb = [IO.Path]::GetFullPath($DatabasePath)
$resolvedDir = [IO.Path]::GetFullPath($OutputDir)
if (-not (Test-Path -LiteralPath $resolvedDb -PathType Leaf)) {
    throw "Fixture database not found: $resolvedDb. Create it first with ExecutionContinuity.UiFixture.exe <path>."
}
[IO.Directory]::CreateDirectory($resolvedDir) | Out-Null
$tracePath = Join-Path $resolvedDir "capture-startup-trace.log"
[IO.File]::WriteAllText($tracePath, [string]::Empty)

$previousDatabasePath = $env:EXECUTION_CONTINUITY_DATABASE
$previousStartupTracePath = $env:EXECUTION_CONTINUITY_STARTUP_TRACE
$env:EXECUTION_CONTINUITY_DATABASE = $resolvedDb
$env:EXECUTION_CONTINUITY_STARTUP_TRACE = $tracePath

$process = $null
try {
    $process = Start-Process -FilePath $resolvedExe -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
    $handle = [IntPtr]::Zero
    while ([DateTime]::UtcNow -lt $deadline -and $handle -eq [IntPtr]::Zero) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "application exited early with code $($process.ExitCode)" }
        $handle = [DailyWindowDriver]::FindDailyWindow([uint32]$process.Id)
    }
    if ($handle -eq [IntPtr]::Zero) { throw "no visible 'Daily' window within $WaitSeconds seconds" }

    [DailyWindowDriver]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 1500

    foreach ($action in $Actions.Split('|')) {
        $trimmed = $action.Trim()
        if ($trimmed -like "click:*") {
            $parts = $trimmed.Substring(6).Split(',')
            $rect = New-Object DailyWindowDriver+RECT
            [DailyWindowDriver]::GetWindowRect($handle, [ref]$rect) | Out-Null
            [DailyWindowDriver]::Click($rect.Left + [int]$parts[0], $rect.Top + [int]$parts[1])
            Start-Sleep -Milliseconds 900
            Write-Output "clicked $($parts[0]),$($parts[1])"
        }
        elseif ($trimmed -like "scroll:*") {
            $parts = $trimmed.Substring(7).Split(',')
            $rect = New-Object DailyWindowDriver+RECT
            [DailyWindowDriver]::GetWindowRect($handle, [ref]$rect) | Out-Null
            [DailyWindowDriver]::Wheel($rect.Left + [int]$parts[0], $rect.Top + [int]$parts[1], [int]$parts[2])
            Start-Sleep -Milliseconds 700
            Write-Output "scrolled at $($parts[0]),$($parts[1]) delta=$($parts[2])"
        }
        elseif ($trimmed -like "wait:*") {
            Start-Sleep -Milliseconds ([int]$trimmed.Substring(5))
        }
        elseif ($trimmed -like "capture:*") {
            $name = $trimmed.Substring(8)
            $path = Join-Path $resolvedDir "$name.png"
            $detail = Save-DailyWindow $handle $path
            Write-Output "captured $path $detail"
        }
        else {
            Write-Output "skipped unknown action '$trimmed'"
        }

        $process.Refresh()
        if ($process.HasExited) { throw "application exited during '$trimmed'" }
    }
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }

    if ($null -eq $previousDatabasePath) { Remove-Item Env:\EXECUTION_CONTINUITY_DATABASE -ErrorAction SilentlyContinue }
    else { $env:EXECUTION_CONTINUITY_DATABASE = $previousDatabasePath }

    if ($null -eq $previousStartupTracePath) { Remove-Item Env:\EXECUTION_CONTINUITY_STARTUP_TRACE -ErrorAction SilentlyContinue }
    else { $env:EXECUTION_CONTINUITY_STARTUP_TRACE = $previousStartupTracePath }
}
