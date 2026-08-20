# Build, deploy and launch the demo.
#
# The loose bin\x64\Debug folder is NOT a registrable app layout: a UWP .NET
# Core app needs the CoreCLR host at the root and the managed exe under
# entrypoint\, and only the packaging step arranges that. Registering the build
# output directly gets the app launched under the desktop CLR, which fails
# before any managed code of ours runs. So: package, unpack, register that.

param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$name = "6c1a0e52-9b3d-4c77-a0f2-51b9e2c4a311"
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
$makeappx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"

# Rebuild, not Build. The CoreCLR entrypoint\ indirection that makes the package
# launchable is generated as part of a full pass; an incremental build leaves it
# out of the payload, and the app then starts under the desktop CLR and dies
# before any managed code runs. The project is small enough that this is cheap.
& $msbuild "$root\WalletCardDemo.csproj" /t:Rebuild /p:Configuration=$Configuration /p:Platform=$Platform /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$msix = Get-ChildItem "$root\AppPackages" -Recurse -Filter "*_$($Platform)_$Configuration.msix" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) { throw "no package produced" }

# A running instance holds entrypoint\WalletCardDemo.exe open, and the layout
# cannot be replaced underneath it.
Get-Process -Name WalletCardDemo -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$layout = "$root\bin\$Platform\$Configuration\Layout"
if (Test-Path $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
& $makeappx unpack /p $msix.FullName /d $layout /o /nv | Out-Null
if ($LASTEXITCODE -ne 0) { throw "unpack failed" }

# Re-registering the same version over a development-mode install is refused,
# so drop the old registration first. The app has no state worth keeping.
Get-AppxPackage -Name $name | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Register "$layout\AppxManifest.xml"

$family = (Get-AppxPackage -Name $name).PackageFamilyName
$state = "$env:LOCALAPPDATA\Packages\$family\LocalState"
Remove-Item "$state\*.txt" -ErrorAction SilentlyContinue

if (-not $NoLaunch) {
    Start-Process "shell:AppsFolder\$family!App"
    Start-Sleep -Seconds 6

    # crash.txt / resources.txt are the only view into a render-thread throw;
    # see the logging in App.xaml.cs and WalletCardView.
    Get-ChildItem $state -Filter "*.txt" -ErrorAction SilentlyContinue | ForEach-Object {
        "=== $($_.Name) ==="
        Get-Content $_.FullName
    }

    $process = Get-Process -Name WalletCardDemo -ErrorAction SilentlyContinue
    if ($process) { "running: pid $($process.Id)" } else { "NOT RUNNING" }
}
