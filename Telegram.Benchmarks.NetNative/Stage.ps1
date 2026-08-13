# Stage the .NET Native package as a loose layout and register it.
#   msbuild Telegram.Benchmarks.NetNative.csproj /p:Configuration=Release /p:Platform=x64 /restore
#   pwsh Stage.ps1
#
# The MSIX is unsigned, so it is unpacked and registered rather than installed. tdjson.dll is built
# for the store and imports VCRUNTIME140_APP and friends; a real package would declare a
# Microsoft.VCLibs.140.00 dependency, but for a dev layout carrying the DLLs is equivalent.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$msix = Get-ChildItem -Path (Join-Path $root 'AppPackages') -Recurse -Filter '*_x64.msix' |
    Where-Object { $_.FullName -notmatch 'Debug' } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $msix) { throw 'No Release msix found - build first' }

$layout = Join-Path $root 'AppPackages\layout'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($msix.FullName, $layout)

$crt = Join-Path $env:TEMP 'tdjson-storecrt-x64'
if (-not (Test-Path (Join-Path $crt 'vcruntime140_app.dll'))) {
    $appx = Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\Windows Kits\10\ExtensionSDKs\Microsoft.VCLibs\14.0\Appx\Retail\x64\Microsoft.VCLibs.x64.14.00.appx'
    New-Item -ItemType Directory -Force -Path $crt | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($appx, $crt)
}
Copy-Item -Path (Join-Path $crt '*.dll') -Destination $layout -Force

Get-Process -Name 'Telegram.Benchmarks.NetNative' -ErrorAction SilentlyContinue | Stop-Process -Force
Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml')

Write-Output "registered $layout"
