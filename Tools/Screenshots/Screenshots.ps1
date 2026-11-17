# Captures the shots a scenario lists, from a copy of a Debug build that cannot reach a real
# account: its data root holds nothing but the scenario, and the theme and window size it changes
# are not the ones of the app in daily use.
#
#   -Flavor WinUI  the unpackaged WinUI build, in Windows Sandbox only: unpackaged, it would share
#                  its data and settings with the debug build in daily use
#   -Flavor UWP    the UWP Debug layout, registered as a package of its own, *.Screenshots
#
#   -On Sandbox    the WinUI build in Windows Sandbox, through the winapp CLI: nothing opens on
#                  this desktop, and the region is the guest's (en-US, 24-hour clock), not yours
#
# Build the Debug configuration of that flavour first. The scenario format is described on
# MockClient in Telegram\Td\MockClient.cs, the handshake with the app on ScreenshotDriver.
#
# The window opens on the desktop and takes focus once, at launch; after that nothing is typed or
# clicked, and every capture is of the app's window alone.
#
# A scenario may also put its shots in a frame, which the app never reads:
#   "frame": {
#     "size": [1366, 1024], "scale": 2,          design size, and what the output is of it
#     "background": [tl, tr, bl, br],            corner colours of the gradient, or an image path
#                                                 in the scenario folder
#     "title": "...", "subtitle": "...",
#     "heading": { "top", "titleSize", "subtitleSize", "lineHeight", "gap" }
#                                                 optional: centred above the window, else the
#                                                 title top left and the subtitle beside it
#     "app": [1194, 834],                         the window's size in effective pixels
#     "window": [x, y, width]                     where it is drawn, in design units
#   }
# Each capture is then also written to framed\: the window with its corners and shadow, on the
# gradient under the heading.
param(
    [string]$Scenario = (Join-Path $PSScriptRoot 'Sample'),
    [ValidateSet('WinUI', 'UWP')]
    [string]$Flavor = 'WinUI',
    [ValidateSet('Host', 'Sandbox')]
    [string]$On = 'Host',
    # The build folder to run instead of the newest WinUI Debug output; the sandbox run uses it.
    [string]$Source,
    [string]$Out,
    [int]$Width = 1280,
    [int]$Height = 800,
    [int]$Timeout = 120
)
$ErrorActionPreference = 'Stop'

# Before any other UI call, or the window rect and the captured bitmap disagree about what a
# pixel is on a scaled display.
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ScreenshotNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out RECT r, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out uint v, int size);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[ScreenshotNative]::SetProcessDPIAware() | Out-Null
Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
# Ignored by git: the captures by default, and the build the sandbox runs.
$work = Join-Path $PSScriptRoot 'work'
if (-not $Out) { $Out = Join-Path $work 'out' }
# What a run leaves only when something went wrong, so that an old one is not read as this one's.
foreach ($file in 'unhandled.log', 'errors.log', 'crash.json', 'failed.png') {
    Remove-Item -LiteralPath (Join-Path $Out $file) -Force -ErrorAction SilentlyContinue
}
$state = Join-Path $env:LOCALAPPDATA 'UnigramScreenshots'
$layout = Join-Path $state $Flavor

if ($Source) {
    $source = (Resolve-Path -LiteralPath $Source).Path
} elseif ($Flavor -eq 'WinUI') {
    # Whichever target framework was built last.
    $built = Get-ChildItem -Path (Join-Path $root 'Telegram\bin\winui\x64\Debug\*\win-x64\Telegram.exe') -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $built) { throw "no WinUI Debug build under Telegram\bin\winui\x64\Debug - build the Unpackaged profile first" }
    $source = $built.DirectoryName
} else {
    $source = Join-Path $root 'Telegram\bin\x64\Debug\AppX'
    if (-not (Test-Path -LiteralPath (Join-Path $source 'AppxManifest.xml'))) {
        throw "no Debug layout at $source - build it with Run.Debug.ps1 first"
    }
}

if ($On -eq 'Sandbox') {
    if ($Flavor -ne 'WinUI') { throw '-On Sandbox runs the unpackaged WinUI build only' }
    if (-not (Get-Command winapp -ErrorAction SilentlyContinue)) { throw 'winapp is not installed: winget install Microsoft.WinAppCli' }
    $env:WINAPP_CLI_TELEMETRY_OPTOUT = '1'

    function Invoke-Winapp {
        & winapp @args
        if ($LASTEXITCODE -ne 0) { throw "winapp $($args -join ' ') failed with $LASTEXITCODE" }
    }

    # winapp is a packaged app and does not see this user's %LOCALAPPDATA%, so the layout is staged
    # beside this script instead.
    $staged = Join-Path $work 'sandbox'
    # A clean Windows has neither C runtime: the Debug build links the debug one, which only Visual
    # Studio installs, and tdjson the release one, which Build.WinUI.ps1 deploys app-local.
    $runtime = 'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140d.dll', 'vcruntime140d.dll', 'vcruntime140_1d.dll', 'ucrtbased.dll'
    robocopy $source $staged /MIR /XF @runtime /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with $LASTEXITCODE" }
    foreach ($name in $runtime) { Copy-Item -LiteralPath (Join-Path $env:WINDIR "System32\$name") -Destination $staged -Force }

    # Relative target paths land under the guest's work root. The '--' is quoted because Windows
    # PowerShell takes a bare one as its own and drops it.
    $work = 'C:\WinApp\work'
    Invoke-Winapp target push sandbox $staged app
    # A push deletes nothing, so the scenario and the output start empty.
    Invoke-Winapp target exec sandbox '--' powershell.exe -NoProfile -Command "Remove-Item $work\scenario, $work\out -Recurse -Force -ErrorAction SilentlyContinue; exit 0"
    Invoke-Winapp target push sandbox $PSCommandPath Tools\Screenshots\Screenshots.ps1
    Invoke-Winapp target push sandbox (Resolve-Path -LiteralPath $Scenario).Path scenario
    Invoke-Winapp target exec sandbox '--' powershell.exe -NoProfile -Command "Set-ItemProperty 'HKCU:\Control Panel\International' -Name sShortTime -Value 'HH:mm'; Set-ItemProperty 'HKCU:\Control Panel\International' -Name sTimeFormat -Value 'HH:mm:ss'"

    & winapp target exec sandbox --cwd $work '--' powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$work\Tools\Screenshots\Screenshots.ps1" `
        -Source "$work\app" -Scenario "$work\scenario" -Out "$work\out" -Width $Width -Height $Height -Timeout $Timeout
    $exit = $LASTEXITCODE

    New-Item -ItemType Directory -Force -Path $Out | Out-Null
    Invoke-Winapp target pull sandbox out $Out
    if ($exit -ne 0) { throw "the run in the sandbox failed with $exit; what it left is in $Out" }
    return
}

# The account every Windows Sandbox signs in with, and what MockClient checks for too.
if ($Flavor -eq 'WinUI' -and $env:USERNAME -ne 'WDAGUtilityAccount') {
    throw 'the WinUI build runs in Windows Sandbox only: pass -On Sandbox'
}

function Get-ScreenshotApp {
    Get-Process Telegram -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$layout\*" }
}

# Files in use can't be replaced. Only the copy is ever stopped: a build running from the
# output folder is somebody's session.
$running = Get-ScreenshotApp
if ($running) { $running | Stop-Process -Force; $running | ForEach-Object { $_.WaitForExit() } }

# A copy, so the build can be rebuilt while a run is going. The UWP manifest is written below
# rather than copied, so robocopy leaves it alone.
robocopy $source $layout /MIR /XF AppxManifest.xml /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with $LASTEXITCODE" }

if ($Flavor -eq 'WinUI') {
    $data = Join-Path $env:LOCALAPPDATA 'TelegramFZ-LLC.Windows.Debug'
} else {
    # Its own identity, so it installs beside the Debug package instead of over it, and without
    # the application's extensions: protocols, the share target, the startup task and the
    # execution alias all belong to the package that is really used.
    $registered = Join-Path $state 'AppxManifest.registered.xml'
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $source 'AppxManifest.xml') -Raw
    $manifest.Package.Identity.Name = $manifest.Package.Identity.Name + '.Screenshots'
    $manifest.Package.Properties.DisplayName = 'Unigram Screenshots'
    $application = $manifest.Package.Applications.Application
    $extensions = $application.Extensions
    if ($extensions) { $application.RemoveChild($extensions) | Out-Null }

    $manifestPath = Join-Path $layout 'AppxManifest.xml'
    $manifest.Save($manifestPath)

    $name = $manifest.Package.Identity.Name
    $package = Get-AppxPackage $name
    $changed = -not (Test-Path -LiteralPath $registered) -or
        (Get-FileHash -LiteralPath $manifestPath).Hash -ne (Get-FileHash -LiteralPath $registered).Hash
    if (-not $package -or $changed) {
        Add-AppxPackage -Register $manifestPath
        Copy-Item -LiteralPath $manifestPath -Destination $registered -Force
        $package = Get-AppxPackage $name
    }

    $data = Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)"
}

# The scenario replaces whatever the last run left. The settings stay - the UWP settings.dat is
# locked while the package is registered - which is why every shot that cares sets its own theme.
$mock = Join-Path $data 'LocalState\mock'
if (Test-Path -LiteralPath $mock) { Remove-Item -LiteralPath $mock -Recurse -Force }
New-Item -ItemType Directory -Force -Path $mock | Out-Null
Copy-Item -Path (Join-Path $Scenario '*') -Destination $mock -Recurse -Force
if (-not (Test-Path -LiteralPath (Join-Path $mock 'scenario.json'))) { throw "no scenario.json in $Scenario" }

# Only whole-line comments are dropped: Windows PowerShell's parser takes none, and a // inside a
# string must survive.
$config = (Get-Content -LiteralPath (Join-Path $mock 'scenario.json') -Raw -Encoding UTF8) -replace '(?m)^\s*//.*$', '' | ConvertFrom-Json
$frame = $config.frame
if ($frame) {
    Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
    $Width, $Height = [int]$frame.app[0], [int]$frame.app[1]
    New-Item -ItemType Directory -Force -Path (Join-Path $Out 'framed') | Out-Null
}

New-Item -ItemType Directory -Force -Path $Out | Out-Null

function Wait-Until([scriptblock]$condition, [string]$what) {
    $deadline = (Get-Date).AddSeconds($Timeout)
    while ((Get-Date) -lt $deadline) {
        $value = & $condition
        if ($value) { return $value }
        Start-Sleep -Milliseconds 200
    }
    throw "timed out waiting for $what"
}

if ($Flavor -eq 'WinUI') {
    $app = Start-Process -FilePath (Join-Path $layout 'Telegram.exe') -WorkingDirectory $layout -PassThru

    # A handle of 0 is still truthy to Wait-Until, so only a real one is returned.
    $hwnd = Wait-Until {
        $app.Refresh()
        if ($app.HasExited) { throw "the app exited with $($app.ExitCode)" }
        if ($app.MainWindowHandle -ne [IntPtr]::Zero) { $app.MainWindowHandle }
    } 'the app window'
} else {
    Start-Process "shell:AppsFolder\$($package.PackageFamilyName)!$($application.Id)"
    $app = Wait-Until { Get-ScreenshotApp | Select-Object -First 1 } 'the app to start'

    # A UWP window belongs to ApplicationFrameHost, not to the app: the frame is the one whose
    # child is the app's CoreWindow.
    $A = [System.Windows.Automation.AutomationElement]
    $S = [System.Windows.Automation.TreeScope]
    $T = [System.Windows.Automation.Condition]::TrueCondition
    function Find-Frame {
        foreach ($window in $A::RootElement.FindAll($S::Children, $T)) {
            if ($window.Current.ClassName -ne 'ApplicationFrameWindow') { continue }
            foreach ($child in $window.FindAll($S::Children, $T)) {
                if ($child.Current.ProcessId -eq $app.Id) { return [IntPtr]$window.Current.NativeWindowHandle }
            }
        }
    }
    $hwnd = Wait-Until { Find-Frame } 'the app window'
}

# The window rect includes the invisible resize border; the extended frame bounds are what is
# drawn, and that is the size asked for.
function Get-Bounds {
    $rect = New-Object ScreenshotNative+RECT
    $visible = New-Object ScreenshotNative+RECT
    [ScreenshotNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    # DWMWA_EXTENDED_FRAME_BOUNDS
    if ([ScreenshotNative]::DwmGetWindowAttribute($hwnd, 9, [ref]$visible, 16) -ne 0) { $visible = $rect }
    return $rect, $visible
}

$scale = [ScreenshotNative]::GetDpiForWindow($hwnd) / 96.0
if ($scale -le 0) { $scale = 1.0 }
$rect, $visible = Get-Bounds
$borderX = ($rect.Right - $rect.Left) - ($visible.Right - $visible.Left)
$borderY = ($rect.Bottom - $rect.Top) - ($visible.Bottom - $visible.Top)

# The extended frame bounds still include the thin border DWM draws around the window, which
# PrintWindow renders black. It is cropped off, and the window grown by it, so the content alone
# is the size asked for.
$outline = [uint32]0
# DWMWA_VISIBLE_FRAME_BORDER_THICKNESS, Windows 11 only.
if ([ScreenshotNative]::DwmGetWindowAttribute($hwnd, 37, [ref]$outline, 4) -ne 0) { $outline = 0 }
$outline = [int]$outline

# SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE
[ScreenshotNative]::SetWindowPos($hwnd, [IntPtr]::Zero, 0, 0,
    [int][Math]::Round($Width * $scale) + $borderX + 2 * $outline, [int][Math]::Round($Height * $scale) + $borderY + 2 * $outline,
    0x2 -bor 0x4 -bor 0x10) | Out-Null

function Save-Window([string]$path) {
    $rect, $visible = Get-Bounds
    $bitmap = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    # PW_RENDERFULLCONTENT: the window alone, whatever sits on top of it.
    $ok = [ScreenshotNative]::PrintWindow($hwnd, $hdc, 2)
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
    if ($ok) {
        $crop = New-Object System.Drawing.Rectangle(($visible.Left - $rect.Left + $outline), ($visible.Top - $rect.Top + $outline),
            ($visible.Right - $visible.Left - 2 * $outline), ($visible.Bottom - $visible.Top - 2 * $outline))
        $cropped = $bitmap.Clone($crop, $bitmap.PixelFormat)
        $cropped.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $cropped.Dispose()
    }
    $bitmap.Dispose()
    if (-not $ok) { throw "PrintWindow failed for $path" }
}

function Save-Framed([string]$capture, [string]$path) {
    $w, $h = [double]$frame.size[0], [double]$frame.size[1]
    $k = [double]$frame.scale
    $x, $y, $width = [double]$frame.window[0], [double]$frame.window[1], [double]$frame.window[2]

    $shot = New-Object System.Windows.Media.Imaging.BitmapImage
    $shot.BeginInit()
    $shot.CacheOption = 'OnLoad'
    $shot.UriSource = New-Object Uri($capture)
    $shot.EndInit()
    $height = $width * $shot.PixelHeight / $shot.PixelWidth

    # Windows 11 rounds a window's corners by 8 effective pixels, and PrintWindow hands them back
    # square.
    $radius = 8 * $width / [double]$frame.app[0]

    if ($frame.background -is [string]) {
        $image = New-Object System.Windows.Media.Imaging.BitmapImage
        $image.BeginInit()
        $image.CacheOption = 'OnLoad'
        $image.UriSource = New-Object Uri((Join-Path $mock $frame.background))
        $image.EndInit()
        $brush = New-Object System.Windows.Media.ImageBrush($image)
        $brush.Stretch = 'UniformToFill'
    } else {
        # The four corner colours interpolated exactly: a 2x2 bitmap stretched between its pixel
        # centres, which is what the Viewbox below selects.
        $corners = New-Object System.Windows.Media.Imaging.WriteableBitmap(2, 2, 96, 96, [System.Windows.Media.PixelFormats]::Bgra32, $null)
        $pixels = New-Object byte[] 16
        for ($i = 0; $i -lt 4; $i++) {
            $c = [System.Windows.Media.ColorConverter]::ConvertFromString($frame.background[$i])
            $pixels[$i * 4] = $c.B; $pixels[$i * 4 + 1] = $c.G; $pixels[$i * 4 + 2] = $c.R; $pixels[$i * 4 + 3] = 255
        }
        $corners.WritePixels((New-Object System.Windows.Int32Rect(0, 0, 2, 2)), $pixels, 8, 0)
        $brush = New-Object System.Windows.Media.ImageBrush($corners)
        $brush.Viewbox = New-Object System.Windows.Rect(0.25, 0.25, 0.5, 0.5)
        $brush.Stretch = 'Fill'
    }

    $heading = $frame.heading
    if ($heading) {
        $headingXaml = @"
    <StackPanel Canvas.Top="$($heading.top)" Width="$w">
        <TextBlock x:Name="Title" TextAlignment="Center" FontFamily="Segoe UI Variable Display" FontWeight="Bold" FontSize="$($heading.titleSize)" Foreground="White" />
        <TextBlock x:Name="Subtitle" Margin="0,$($heading.gap),0,0" TextAlignment="Center" FontFamily="Segoe UI Variable Display" FontWeight="SemiBold" FontSize="$($heading.subtitleSize)" LineHeight="$($heading.lineHeight)" LineStackingStrategy="BlockLineHeight" Foreground="White" />
    </StackPanel>
"@
    } else {
        $headingXaml = @"
    <TextBlock x:Name="Title" Canvas.Left="64" Canvas.Top="40" FontFamily="Segoe UI Variable Display" FontWeight="Bold" FontSize="68" Foreground="White" />
    <TextBlock x:Name="Subtitle" Canvas.Left="683" Canvas.Top="44" FontFamily="Segoe UI Variable Display" FontWeight="SemiBold" FontSize="32" LineHeight="42" LineStackingStrategy="BlockLineHeight" Foreground="White" />
"@
    }

    # In proportion to the window, so a larger design gets the same shadow.
    $blur = 0.05 * $width
    $depth = 0.0125 * $width

    $xaml = @"
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Width="$w" Height="$h" TextOptions.TextFormattingMode="Ideal" RenderOptions.BitmapScalingMode="HighQuality">
    <Rectangle x:Name="Background" Width="$w" Height="$h" RenderOptions.BitmapScalingMode="Linear" />
$headingXaml
    <Rectangle Canvas.Left="$x" Canvas.Top="$y" Width="$width" Height="$height" RadiusX="$radius" RadiusY="$radius" Fill="Black">
        <Rectangle.Effect>
            <DropShadowEffect BlurRadius="$blur" ShadowDepth="$depth" Direction="270" Opacity="0.32" RenderingBias="Quality" />
        </Rectangle.Effect>
    </Rectangle>
    <Image x:Name="Shot" Canvas.Left="$x" Canvas.Top="$y" Width="$width" Height="$height" Stretch="Fill">
        <Image.Clip>
            <RectangleGeometry Rect="0,0,$width,$height" RadiusX="$radius" RadiusY="$radius" />
        </Image.Clip>
    </Image>
    <Rectangle Canvas.Left="$x" Canvas.Top="$y" Width="$width" Height="$height" RadiusX="$radius" RadiusY="$radius" Stroke="#33000000" StrokeThickness="1" />
</Canvas>
"@
    $root = [System.Windows.Markup.XamlReader]::Parse($xaml)

    $root.FindName('Background').Fill = $brush
    $root.FindName('Title').Text = $frame.title
    $root.FindName('Subtitle').Text = $frame.subtitle
    $root.FindName('Shot').Source = $shot

    # Laid out at the design size and rendered at scale, so the text and the edges are drawn at
    # the output resolution rather than enlarged from the design's.
    $root.LayoutTransform = New-Object System.Windows.Media.ScaleTransform($k, $k)
    $size = New-Object System.Windows.Size(($w * $k), ($h * $k))
    $root.Measure($size)
    $root.Arrange((New-Object System.Windows.Rect($size)))
    $root.UpdateLayout()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap([int]($w * $k), [int]($h * $k), 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($root)

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

Set-Content -LiteralPath (Join-Path $mock 'go.txt') -Value '' -NoNewline

$shotFile = Join-Path $mock 'shot.txt'
$doneFile = Join-Path $mock 'done.txt'
$last = -1
$saved = @()

$started = Get-Date
try {
while ($true) {
    $next = Wait-Until {
        if ($app.HasExited) { throw "the app exited with $($app.ExitCode)" }
        if (Test-Path -LiteralPath $doneFile) { return 'done' }
        if (-not (Test-Path -LiteralPath $shotFile)) { return $null }
        $line = (Get-Content -LiteralPath $shotFile -Raw -ErrorAction SilentlyContinue)
        if ($line -match '^(\d+) (.+)$' -and [int]$Matches[1] -gt $last) { return $line.Trim() }
    } 'the next shot'

    if ($next -eq 'done') { break }

    $index, $shotName = $next -split ' ', 2
    $path = Join-Path $Out "$shotName.png"
    Save-Window $path
    $saved += $path
    if ($frame) {
        $framed = Join-Path $Out "framed\$shotName.png"
        Save-Framed $path $framed
        $saved += $framed
    }
    $last = [int]$index
    Set-Content -LiteralPath (Join-Path $mock 'captured.txt') -Value $index -NoNewline
}
} catch {
    # What the window showed when it went wrong, usually an error popup or a page that never
    # filled, and the crash report the app wrote for it, if it got that far.
    if (-not $app.HasExited) {
        try { Save-Window (Join-Path $Out 'failed.png'); Write-Warning "the window as it was: $(Join-Path $Out 'failed.png')" } catch { }
    }
    $report = Get-ChildItem -Path (Join-Path $data 'LocalState\ErrorReports') -Filter *.json -ErrorAction SilentlyContinue |
        Where-Object LastWriteTime -ge $started | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($report) {
        Copy-Item -LiteralPath $report.FullName -Destination (Join-Path $Out 'crash.json') -Force
        Write-Warning "crash report: $(Join-Path $Out 'crash.json')"
    }
    throw
} finally {
    Get-ScreenshotApp | Stop-Process -Force
}

foreach ($file in @('unhandled.log', 'errors.log')) {
    $log = Join-Path $mock $file
    if (Test-Path -LiteralPath $log) {
        # Beside the shots, where a run in the sandbox hands them back.
        Copy-Item -LiteralPath $log -Destination $Out -Force
        Write-Warning "$file has entries: $(Join-Path $Out $file)"
    }
}

$saved
