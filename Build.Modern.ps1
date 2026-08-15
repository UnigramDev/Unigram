<#
.SYNOPSIS
    Builds a signed NativeAOT msixbundle from Telegram.Modern.csproj.

.DESCRIPTION
    The .NET 10 / CsWinRT / NativeAOT build, packaged by Telegram.Msix.Modern. Installs beside the
    shipping app and beside the loose development layout: all three have different identities,
    because Windows will not move an installed app between a registered layout and a bundle.

    Takes tens of minutes - most of it the ILC link.

    Deliberately does not call UpdateManifest.ps1, so the version does not move and each build
    replaces the last at the same path. Run it by hand first if you need a bundle that supersedes
    an installed one rather than overwriting it.

.EXAMPLE
    .\Build.Modern.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string] $Platform = 'x64',

    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    # Alternative installs beside the shipping bundle. Original carries Telegram.Msix's own
    # identity, which is what a beta for testers wants - and which means it replaces the shipping
    # app rather than sitting next to it.
    [ValidateSet('Alternative', 'Original')]
    [string] $Identity = 'Alternative'
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

# Through the solution, never the project file. $(SolutionDir) is empty otherwise, and the native
# projects then write their .winmd to a path Telegram.Modern.csproj does not read - which fails as
# a stale projection rather than as a missing file.
$solution = Join-Path $PSScriptRoot 'Telegram.Modern.slnx'

$common = @("-p:Configuration=$Configuration", "-p:Platform=$Platform", '-nologo', '-verbosity:minimal', '-m')

function Invoke-MSBuild {
    param([string[]] $Arguments, [string] $Description)

    Write-Host "==> $Description" -ForegroundColor Cyan
    & $msbuild @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

# The native components first. The app consumes their .winmd and .dll by path rather than through a
# ProjectReference, so building the package does not build them.
Invoke-MSBuild -Description 'Telegram.Native, Telegram.Native.Calls' -Arguments (
    @($solution, '-target:Telegram_Native;Telegram_Native_Calls', '-restore') + $common)

# AppxBundle=Always and AppxBundlePlatforms are what produce a bundle rather than loose .msix files,
# and the Store will not accept switching between the two once an app has shipped as one of them.
Invoke-MSBuild -Description "Telegram.Msix.Modern ($Configuration|$Platform, $Identity identity)" -Arguments (
    @($solution, '-target:Telegram_Msix_Modern', '-restore',
      '-p:UapAppxPackageBuildMode=SideloadOnly',
      "-p:AppxBundlePlatforms=$Platform",
      '-p:AppxBundle=Always',
      '-p:AppxPackageSigningEnabled=True',
      "-p:ModernIdentity=$Identity") + $common)

$packages = Join-Path $PSScriptRoot 'Telegram.Msix.Modern\AppPackages'
$bundle = Get-ChildItem $packages -Recurse -Filter *.msixbundle -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1

if (-not $bundle) {
    throw "The build reported success but no .msixbundle was found under $packages."
}

Write-Host ''
Write-Host ("{0}  ({1:N0} bytes)" -f $bundle.FullName, $bundle.Length) -ForegroundColor Green
Write-Host 'Install with Add-AppDevPackage.ps1 beside it, which also trusts the test certificate.'
