<#
.SYNOPSIS
    Builds a signed NativeAOT msixbundle from Telegram.Win32.csproj.

.DESCRIPTION
    The XAML Islands flavour, packaged from the app project itself rather than from a wapproj -
    it already carries AppxPackage and EnableMsixTooling, so there is nothing for a packaging
    project to add.

    Installs beside everything else: the shipping app, the Modern bundle, and the loose Win32
    publish folder that Add-AppxPackage -Register points at. That last one matters - Windows will
    not move an installed app between a registered layout and a bundle, so the bundle carries its
    own identity and the two can coexist.

    Takes tens of minutes - most of it the ILC link.

.EXAMPLE
    .\Build.Win32.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string] $Platform = 'x64',

    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    # Registered is the identity the development layout uses. Bundle is what makes this install
    # beside it rather than fighting it.
    [ValidateSet('Bundle', 'Registered')]
    [string] $Identity = 'Bundle'
)

$ErrorActionPreference = 'Stop'

if ($Platform -eq 'ARM64') {
    Write-Warning 'ARM64 has never been built for this stack - neither the app nor the native projects.'
}

# vswhere has to be on PATH, not merely findable: ILC shells out to it while looking for link.exe,
# and without it the AOT step dies with MSB3073 and exit code 123, naming link.exe rather than the
# thing that actually went missing.
$installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
$vswhere = Join-Path $installer 'vswhere.exe'

if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found at $vswhere"
}

$env:PATH = "$installer;$env:PATH"

$vs = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vs) {
    throw 'No Visual Studio installation with MSBuild was found.'
}

# VS's MSBuild, not the SDK's: modern UWP XAML support is imported from ImportBefore/ImportAfter
# hooks that only VS's copy has.
$msbuild = Join-Path $vs 'MSBuild\Current\Bin\amd64\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    throw "MSBuild.exe not found at $msbuild"
}

# The two native projects still restore through packages.config, which -restore does not cover on
# its own: it drives NuGet's PackageReference restore, and a packages.config project is skipped
# silently. The build then fails on the CppWinRT .props it imports by path, telling you to run a
# restore you just ran. Restoring both kinds costs nothing once the packages are there.
$common = @("-p:Configuration=$Configuration", "-p:Platform=$Platform",
            '-p:RestorePackagesConfig=true', '-nologo', '-verbosity:minimal', '-m')

function Invoke-MSBuild {
    param([string[]] $Arguments, [string] $Description)

    Write-Host "==> $Description" -ForegroundColor Cyan
    & $msbuild @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

# Through the solution, never the project file. Building the vcxprojs without one leaves
# $(SolutionDir) empty - they then write their .winmd and .dll under their own directories rather
# than x64\$Configuration\, which is where this project reads them from. The result is a stale
# projection, not an error.
$solution = Join-Path $PSScriptRoot 'Telegram.Win32.slnx'

Invoke-MSBuild -Description 'Telegram.Native, Telegram.Native.Calls' -Arguments (
    @($solution, '-target:Telegram_Native;Telegram_Native_Calls', '-restore') + $common)

# RuntimeIdentifier and SelfContained are not optional here: without a RID this project resolves no
# package assets at all and the compiler reports thousands of errors that look like a broken
# projection.
$project = Join-Path $PSScriptRoot 'Telegram\Telegram.Win32.csproj'

Invoke-MSBuild -Description "Telegram.Win32 ($Configuration|$Platform, $Identity identity)" -Arguments (
    @($project, '-target:Publish', '-restore',
      "-p:RuntimeIdentifier=win-$($Platform.ToLowerInvariant())",
      '-p:SelfContained=true',
      '-p:UapAppxPackageBuildMode=SideloadOnly',
      "-p:AppxBundlePlatforms=$Platform",
      '-p:AppxBundle=Always',
      '-p:GenerateAppxPackageOnBuild=true',
      '-p:AppxPackageSigningEnabled=True',
      "-p:Win32Identity=$Identity") + $common)

$packages = Join-Path $PSScriptRoot 'Telegram\AppPackages'
$bundle = Get-ChildItem $packages -Recurse -Include *.msixbundle, *.msix -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1

if (-not $bundle) {
    throw "The build reported success but no package was found under $packages."
}

Write-Host ''
Write-Host ("{0}  ({1:N0} bytes)" -f $bundle.FullName, $bundle.Length) -ForegroundColor Green
Write-Host 'Install with Add-AppDevPackage.ps1 beside it, which also trusts the test certificate.'
