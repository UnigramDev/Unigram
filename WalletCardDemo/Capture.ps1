# Screenshot just the demo's window.
#
# Must run in a process that is DPI-aware BEFORE any UI call, or GetWindowRect
# and CopyFromScreen disagree about what a pixel is on a scaled display - hence
# SetProcessDPIAware as the very first thing here.

param([string]$Out = "$env:TEMP\walletcard.png")

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Native {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[Native]::SetProcessDPIAware() | Out-Null

$window = Get-Process | Where-Object { $_.MainWindowTitle -eq "Wallet Card Demo" } | Select-Object -First 1
if (-not $window) { throw "demo window not found" }

[Native]::SetForegroundWindow($window.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 800

$rect = New-Object Native+RECT
[Native]::GetWindowRect($window.MainWindowHandle, [ref]$rect) | Out-Null

Add-Type -AssemblyName System.Drawing
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
$graphics.Dispose()
$bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

"$Out ($width x $height)"
