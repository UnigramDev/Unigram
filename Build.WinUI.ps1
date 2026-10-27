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
    [switch] $SkipNative,

    # No identity: a self-contained publish folder instead of a bundle.
    [switch] $Unpackaged,

    # The unpackaged build's version, which has no manifest of its own to take it from. Defaults to
    # the release package's.
    [version] $Version
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

if ($Unpackaged) {
    $arch = $Platform.ToLowerInvariant()
    $publish = Join-Path $PSScriptRoot "Telegram\bin\winui-unpackaged\$Platform\$Configuration\publish"

    if (-not $Version) {
        [xml] $release = Get-Content (Join-Path $PSScriptRoot 'Telegram.Msix\Package.appxmanifest') -Raw
        $Version = $release.Package.Identity.Version
    }

    if ($Version.Revision -lt 0) {
        throw "-Version needs four parts, got $Version."
    }

    # Publish only adds to the folder, and whatever an earlier one left there would ship.
    if (Test-Path $publish) {
        Remove-Item $publish -Recurse -Force
    }

    Invoke-MSBuild -Description "Telegram.Updater ($Configuration|$Platform)" -Arguments (
        @($solution, '-target:Telegram_Updater', "-p:Platform=$Platform", '-nodeReuse:false') + $common)

    Invoke-MSBuild -Description "Telegram.WinUI ($Configuration|$Platform, unpackaged $Version)" -Arguments (
        @($solution, '-target:Telegram_WinUI:Publish', '-restore',
          "-p:Platform=$Platform",
          "-p:PublishProfile=Properties\PublishProfiles\winui-$arch.pubxml",
          '-p:Unpackaged=true',
          "-p:WinUIPackageVersion=$Version",
          "-p:PublishDir=$publish\",
          '-nodeReuse:false') + $common)

    # The natives, the vcpkg libraries and tdjson import the desktop CRT, which the package gets from
    # the UWPDesktop framework and nothing provides without one, so it goes beside the app: the
    # redistributable's own folder, deployed app-local. Taken from the default toolset's version, so
    # it is never older than the code that links it.
    $vc = Join-Path $vs 'VC'
    $redist = (Get-Content (Join-Path $vc 'Auxiliary\Build\Microsoft.VCRedistVersion.default.txt') -TotalCount 1).Trim()
    $crt = Get-ChildItem (Join-Path $vc "Redist\MSVC\$redist\$arch") -Directory -Filter 'Microsoft.VC*.CRT' -ErrorAction SilentlyContinue
    if (-not $crt) {
        throw "No C++ runtime redistributable for $arch under $vc\Redist\MSVC\$redist."
    }

    Copy-Item (Join-Path $crt[0].FullName '*.dll') $publish -Force

    # In app\ with everything else; the app copies it out to the root before running it.
    Copy-Item (Join-Path $PSScriptRoot "$Platform\$Configuration\Updater.exe") $publish -Force

    $output = Split-Path $publish -Parent
    $files = Get-ChildItem $publish -Recurse -File | Where-Object Extension -ne '.pdb'

    # What the app checks a downloaded update against before staging it. Paths use forward slashes,
    # as zip entry names do.
    $manifest = [ordered]@{
        version = "$Version"
        arch    = $arch
        files   = @($files | ForEach-Object {
            [ordered]@{
                path   = $_.FullName.Substring($publish.Length + 1).Replace('\', '/')
                size   = $_.Length
                sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        })
    }

    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

    $package = Join-Path $output "Telegram-$Version-$arch.zip"
    if (Test-Path $package) {
        Remove-Item $package -Force
    }

    Write-Host "==> $package" -ForegroundColor Cyan
    $zip = [System.IO.Compression.ZipFile]::Open($package, 'Create')
    try {
        $entry = $zip.CreateEntry('manifest.json')
        $writer = New-Object System.IO.StreamWriter($entry.Open(), (New-Object System.Text.UTF8Encoding($false)))
        try {
            $writer.Write(($manifest | ConvertTo-Json -Depth 4))
        }
        finally {
            $writer.Dispose()
        }

        foreach ($file in $files) {
            $name = 'payload/' + $file.FullName.Substring($publish.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $name, 'Optimal') | Out-Null
        }
    }
    finally {
        $zip.Dispose()
    }

    # Per-user installs of Inno Setup land under LOCALAPPDATA, machine-wide ones under Program Files.
    $iscc = @(
        (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue).Source
        Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
        Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
    ) | Where-Object { $_ -and (Test-Path $_) }

    if ($iscc) {
        Write-Host "==> Installer ($Version, $arch)" -ForegroundColor Cyan
        & @($iscc)[0] '/Q' "/DAppVersion=$Version" "/DArch=$arch" "/DPublishDir=$publish" "/DOutputDir=$output" (Join-Path $PSScriptRoot 'Telegram.Installer\Telegram.iss')

        if ($LASTEXITCODE -ne 0) {
            throw "ISCC failed with exit code $LASTEXITCODE."
        }
    }
    else {
        Write-Warning 'Inno Setup 6 was not found, so there is no installer: winget install JRSoftware.InnoSetup'
    }

    Write-Host ''
    Write-Host $output -ForegroundColor Green
    return
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
