# Stage the AOT publish output as a loose-file UWP package layout.
$publish = 'C:\Source\Telegram\Telegram.Benchmarks\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish'
$source  = 'C:\Source\Telegram\Telegram.Benchmarks\Uwp'

# Assets at the layout root, so the manifest paths need no rewriting.
$assets = Join-Path $publish 'Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
Copy-Item -Path (Join-Path $source 'Assets\*') -Destination $assets -Force

$manifest = Get-Content (Join-Path $source 'Package.appxmanifest') -Raw
$manifest = $manifest.Replace('$targetnametoken$.exe', 'Telegram.Benchmarks.exe')
$manifest = $manifest.Replace('$targetentrypoint$', 'Telegram.Benchmarks.BenchmarkApp')
Set-Content -Path (Join-Path $publish 'AppxManifest.xml') -Value $manifest -Encoding UTF8

Write-Output "staged $publish"
Get-ChildItem $publish | Select-Object -ExpandProperty Name

# tdjson.dll is built for the store and imports VCRUNTIME140_APP and friends. A real package would
# declare a Microsoft.VCLibs.140.00 framework dependency; for a dev layout, carrying the three DLLs
# is simpler and has the same effect.
$crt = Join-Path $env:TEMP 'tdjson-storecrt-x64'
if (Test-Path $crt) {
    Copy-Item -Path (Join-Path $crt '*.dll') -Destination $publish -Force
    Write-Output "copied store CRT from $crt"
}
