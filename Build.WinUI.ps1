<#
.SYNOPSIS
    Builds a signed NativeAOT msixbundle from Telegram.WinUI.csproj.

.DESCRIPTION
    The WinUI 3 flavour, packaged by the single-project MSIX tooling that is already on in the
    project - there is no wapproj, unlike the .NET Native and Modern flavours.

    The bundle carries its own identity, 38833FF26BA1D.UnigramWinUIBundle, so it installs beside
    the loose layout Add-AppxPackage -Register puts on during development: Windows will not move
    an installed app between a registered layout and a bundle, so the two cannot share a name. The
    protocol, alias and file type claims are suffixed to match, so neither steals the other's.

    One architecture per run. The result is a real msixbundle either way; carrying several would
    mean building the native components for each, and only x64 has ever been built here.

    Takes tens of minutes - most of it the ILC link, which this flavour has never been through.

.EXAMPLE
    .\Build.WinUI.ps1

.EXAMPLE
    .\Build.WinUI.ps1 -SkipNative
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string] $Platform = 'x64',

    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    # Bundle installs beside the development layout; Layout builds the bundle under the identity
    # the layout registers, which can then only replace it.
    [ValidateSet('Bundle', 'Layout')]
    [string] $Identity = 'Bundle',

    # The two C++/WinRT components, which the app's ProjectReferences would otherwise build again.
    [switch] $SkipNative
)

$ErrorActionPreference = 'Stop'

$installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
$vswhere = Join-Path $installer 'vswhere.exe'

if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found at $vswhere"
}

# On PATH, not merely found: ILC shells out to vswhere looking for link.exe, and without it the
# AOT step dies with MSB3073 naming link.exe rather than the thing that actually went missing.
$env:PATH = "$installer;$env:PATH"

$vs = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vs) {
    throw 'No Visual Studio installation with MSBuild was found.'
}

$msbuild = Join-Path $vs 'MSBuild\Current\Bin\amd64\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    throw "MSBuild.exe not found at $msbuild"
}

# Through the solution, never the project file, as Build.Modern.ps1 does: $(SolutionDir) is empty
# otherwise, and the native projects then write their .winmd somewhere other than where a solution
# build of the app looks.
$solution = Join-Path $PSScriptRoot 'Telegram.WinUI.slnx'

# The natives restore through packages.config, which -restore skips without this.
$common = @("-p:Configuration=$Configuration", '-p:RestorePackagesConfig=true', '-nologo', '-verbosity:minimal')

function Invoke-MSBuild {
    param([string[]] $Arguments, [string] $Description)

    Write-Host "==> $Description" -ForegroundColor Cyan
    & $msbuild @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipNative) {
    # Node reuse off: a leftover node holds pch.pch open and the next build dies with MSB6003.
    Invoke-MSBuild -Description "Telegram.Native, Telegram.Native.Calls ($Configuration|$Platform)" -Arguments (
        @($solution, '-target:Telegram_Native;Telegram_Native_Calls', '-restore', "-p:Platform=$Platform", '-nodeReuse:false') + $common)
}

$packages = Join-Path $PSScriptRoot 'Telegram\AppPackages'

# Publish, not Build: the payload has to be the AOT output, and the packaging targets take it from
# the profile's PublishDir. GenerateAppxPackageOnBuild is what produces an msix at all outside
# Visual Studio, and AppxBundle=Always what makes it a bundle rather than a loose one.
Invoke-MSBuild -Description "Telegram.WinUI ($Configuration|$Platform, $Identity identity)" -Arguments (
    @($solution, '-target:Telegram_WinUI:Publish', '-restore',
      "-p:Platform=$Platform",
      "-p:PublishProfile=Properties\PublishProfiles\winui-$($Platform.ToLowerInvariant()).pubxml",
      "-p:WinUIIdentity=$Identity",
      '-p:GenerateAppxPackageOnBuild=true',
      '-p:AppxBundle=Always',
      "-p:AppxBundlePlatforms=$Platform",
      '-p:UapAppxPackageBuildMode=SideloadOnly',
      '-p:AppxPackageSigningEnabled=true',
      "-p:AppxPackageDir=$packages\",
      '-nodeReuse:false') + $common)

$bundle = Get-ChildItem $packages -Recurse -Filter '*.msixbundle' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1

if (-not $bundle) {
    throw "The build reported success but no .msixbundle was found under $packages."
}

# The desktop CRT framework the manifest names - see Package.Win32.Final.xslt. The packaging
# targets only lay down the Windows App Runtime here, and Add-AppDevPackage.ps1 installs whatever
# it finds, so a machine without the Visual C++ redistributable gets the framework with the app.
if ($Configuration -ne 'Debug') {
    $arch = $Platform.ToLowerInvariant()
    $vclibs = Join-Path ${env:ProgramFiles(x86)} "Microsoft SDKs\Windows Kits\10\ExtensionSDKs\Microsoft.VCLibs.Desktop\14.0\Appx\Retail\$arch\Microsoft.VCLibs.$arch.14.00.Desktop.appx"
    if (-not (Test-Path $vclibs)) {
        throw "Microsoft.VCLibs.140.00.UWPDesktop not found at $vclibs. Install the Windows SDK's Desktop C++ runtime package."
    }

    $dependencies = Join-Path $bundle.DirectoryName "Dependencies\$arch"
    New-Item -ItemType Directory -Force $dependencies | Out-Null
    Copy-Item $vclibs $dependencies -Force
}

Write-Host ''
Write-Host ("{0}  ({1:N0} bytes)" -f $bundle.FullName, $bundle.Length) -ForegroundColor Green
Write-Host 'Install with Add-AppDevPackage.ps1 beside the bundle, which also trusts the test certificate.'
