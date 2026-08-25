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

.EXAMPLE
    .\UpdateManifest.ps1 -path Telegram.Msix -config RELEASE -mode SideloadOnly
    .\Build.Modern.ps1 -Identity Original -Instrumented

.EXAMPLE
    .\UpdateManifest.ps1 -path Telegram.Msix -config RELEASE -mode StoreUpload
    .\Build.Modern.ps1 -Platform ARM64, x64 -Mode StoreUpload -Identity Original
#>
[CmdletBinding()]
param(
    # One entry builds a single-architecture bundle; several build one bundle carrying all of them.
    # The last is the one MSBuild is invoked for - see the note where AppxBundlePlatforms is passed
    # - so put the host architecture last unless you have a reason not to.
    #
    # A list and a separated string are both accepted, and there is no ValidateSet because of it:
    # powershell.exe -File hands every argument over as one string, so -Platform x64,ARM64 arrives
    # there as the single value "x64,ARM64" and a set would reject it before the split below.
    [string[]] $Platform = 'x64',

    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [ValidateSet('SideloadOnly', 'StoreUpload')]
    [string] $Mode = 'SideloadOnly',

    # Alternative installs beside the shipping bundle. Original carries Telegram.Msix's own
    # identity, which is what a beta for testers wants - and which means it replaces the shipping
    # app rather than sitting next to it.
    [ValidateSet('Alternative', 'Original')]
    [string] $Identity = 'Alternative',

    # Compiles in the Profiler probes and Instrumentation.Register, which holds a WeakReference per
    # registered object for the session. For measuring, never for a bundle anyone else installs.
    [switch] $Instrumented
)

$ErrorActionPreference = 'Stop'

$known = 'x64', 'ARM64'
$resolved = [System.Collections.Generic.List[string]]::new()

foreach ($name in ($Platform -split '[,|]')) {
    $trimmed = $name.Trim()
    if (-not $trimmed) {
        continue
    }

    # Matched rather than used as given, so -Platform arm64 still reaches MSBuild as the name the
    # solution and the packaging project spell out.
    $canonical = $known | Where-Object { $_ -eq $trimmed } | Select-Object -First 1
    if (-not $canonical) {
        throw "Unknown platform '$trimmed'. Expected one or more of $($known -join ', ')."
    }

    if (-not $resolved.Contains($canonical)) {
        $resolved.Add($canonical)
    }
}

$Platform = $resolved

if ($Platform -contains 'ARM64') {
    Write-Warning 'ARM64 has never been through ILC. The native components and the tdjson and rlottie binaries are all there for it; Telegram.Modern.csproj is not, so this is the first AOT compile for that architecture.'
}

if ($Mode -eq 'StoreUpload' -and $Identity -ne 'Original') {
    Write-Warning "Mode StoreUpload with the $Identity identity: the .msixupload will carry the patched package name and the Store will reject it. Pass -Identity Original for a real submission."
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

$common = @("-p:Configuration=$Configuration", '-nologo', '-verbosity:minimal', '-m')

# The last platform in AppxBundlePlatforms is the one the packaging targets treat as producing the
# bundle: every other platform is built from it, through the solution and with bundling suppressed,
# and only it runs MakeAppx bundle. Invoking MSBuild for any other one leaves a set of .msix files
# and no bundle at all, so this is the platform the packaging pass is driven with.
$bundlePlatforms = $Platform -join '|'
$producing = $Platform[-1]

# Reaches Telegram.Modern.csproj through the packaging project's ProjectReference, as a global
# property: AdditionalProperties there adds to the set rather than replacing it.
$instrumentation = if ($Instrumented) { @('-p:Instrumented=true') } else { @() }

function Invoke-MSBuild {
    param([string[]] $Arguments, [string] $Description)

    Write-Host "==> $Description" -ForegroundColor Cyan
    & $msbuild @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

# The native components first, for every architecture the bundle will carry. The app consumes their
# .winmd and .dll by path rather than through a ProjectReference, so building the package does not
# build them - and the recursion into the other platforms goes through the solution, which would
# build them there but leaves the driving platform's still missing.
foreach ($p in $Platform) {
    Invoke-MSBuild -Description "Telegram.Native, Telegram.Native.Calls ($p)" -Arguments (
        @($solution, '-target:Telegram_Native;Telegram_Native_Calls', '-restore', "-p:Platform=$p") + $common)
}

# AppxBundle=Always and AppxBundlePlatforms are what produce a bundle rather than loose .msix files,
# and the Store will not accept switching between the two once an app has shipped as one of them.
Invoke-MSBuild -Description "Telegram.Msix.Modern ($Configuration|$bundlePlatforms, $Identity identity, $Mode)" -Arguments (
    @($solution, '-target:Telegram_Msix_Modern', '-restore',
      "-p:Platform=$producing",
      "-p:ModernPackageBuildMode=$Mode",
      "-p:AppxBundlePlatforms=$bundlePlatforms",
      '-p:AppxBundle=Always',
      '-p:AppxPackageSigningEnabled=True',
      "-p:ModernIdentity=$Identity") + $instrumentation + $common)

$packages = Join-Path $PSScriptRoot 'Telegram.Msix.Modern\AppPackages'

function Get-Newest {
    param([string] $Extension)

    Get-ChildItem $packages -Recurse -Filter "*$Extension" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime | Select-Object -Last 1
}

# StoreUpload is the legacy name for store-and-sideload: it produces the signed bundle as well, so
# the bundle is the one artifact both modes always have.
$bundle = Get-Newest '.msixbundle'

if (-not $bundle) {
    throw "The build reported success but no .msixbundle was found under $packages."
}

Write-Host ''
Write-Host ("{0}  ({1:N0} bytes)" -f $bundle.FullName, $bundle.Length) -ForegroundColor Green

if ($Mode -eq 'StoreUpload') {
    $upload = Get-Newest '.msixupload'

    if (-not $upload) {
        throw "The build reported success but no .msixupload was found under $packages."
    }

    Write-Host ("{0}  ({1:N0} bytes)" -f $upload.FullName, $upload.Length) -ForegroundColor Green
    Write-Host 'The .msixupload is what Partner Center takes; it is unsigned by design.'
}

Write-Host 'Install with Add-AppDevPackage.ps1 beside the bundle, which also trusts the test certificate.'
