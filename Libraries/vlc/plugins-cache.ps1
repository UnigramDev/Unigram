<#
.SYNOPSIS
    Generates the LibVLC plugin cache (plugins.dat) for the plugins shipped by the nuspec.

.DESCRIPTION
    libvlc scans every plugin DLL on startup unless it finds a matching cache. The cache can
    only be produced by running vlc-cache-gen, which loads each plugin and records what it
    registers -- there is no way to derive that statically.

    That is a problem for ARM64, which cannot execute on an x64 host. It is worked around by
    exploiting the fact that a cache entry is almost entirely architecture-independent: the
    module descriptors come from the same sources, and only two fields differ per build. The
    per-plugin trailer is (src/modules/cache.c):

        uint16 len + textdomain
        uint16 len + path            e.g. "access\libhttps_plugin.dll\0"
        char   unloadable
        int64  mtime
        uint64 size

    all fixed-width little-endian on both architectures. So the cache is generated once for
    x64 and then re-stamped with the ARM64 files' mtime and size.

    src/modules/bank.c discards any entry whose mtime or size does not match the file on disk,
    so this must run on every build, and the timestamps have to survive packaging.
#>
param(
    [string]$Root = $PSScriptRoot,
    [string]$Nuspec = "VideoLAN.LibVLC.UWP.nuspec"
)

$ErrorActionPreference = "Stop"

function Get-ArchDirectory {
    param([string]$Root, [string]$Arch)

    $parent = Join-Path $Root "vlc\$Arch"
    if (-not (Test-Path $parent)) {
        throw "Build output not found: $parent"
    }

    # The install directory carries the VLC version (vlc-3.0.23), which moves between releases.
    $dir = Get-ChildItem -Path $parent -Directory -Filter "vlc-*" | Select-Object -First 1
    if ($null -eq $dir) {
        throw "No vlc-* install directory under $parent -- was the build run?"
    }

    return $dir.FullName
}

function Get-ShippedPlugins {
    param([string]$NuspecPath)

    # Only the plugins the package actually ships belong in the cache; libvlc would otherwise
    # record entries for files that are not there.
    [xml]$doc = Get-Content -LiteralPath $NuspecPath -Raw

    $paths = foreach ($file in $doc.package.files.file) {
        $target = $file.target
        if ($target -match '^build/win10-x64/plugins/(.+\.dll)$') {
            $Matches[1] -replace '/', '\'
        }
    }

    $paths = @($paths | Sort-Object -Unique)
    if ($paths.Count -eq 0) {
        throw "No plugin entries found in $NuspecPath"
    }

    return $paths
}

$AppxManifestTemplate = @'
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
  <Identity Name="VlcCacheGen" Publisher="CN=VlcCacheGen" Version="1.0.0.0" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>VlcCacheGen</DisplayName>
    <PublisherDisplayName>VlcCacheGen</PublisherDisplayName>
    <Logo>logo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  <Resources><Resource Language="en-us" /></Resources>
  <Applications>
    <Application Id="App" Executable="vlc-cache-gen.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="VlcCacheGen" Description="VLC plugin cache generator"
                          BackgroundColor="transparent" Square150x150Logo="logo.png" Square44x44Logo="logo.png" />
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
'@

function New-PluginCache {
    <#
        The UWP build of libvlc loads plugins with LoadPackagedLibrary (src/win32/plugin.c),
        which only resolves DLLs inside the calling process's package. vlc-cache-gen therefore
        finds nothing when run from a plain directory, however the plugins are laid out.

        So the staged tree is registered as a loose package and cache-gen is run inside it.
        The command goes through cmd.exe with an explicit `cd`, because
        Invoke-CommandInDesktopPackage does not set a working directory and the relative
        plugin path would otherwise resolve somewhere else entirely -- which looks exactly
        like "no plugins found".
    #>
    param([string]$InstallDir, [string[]]$Plugins, [string]$CacheGen, [string]$OutFile)

    $stage = Join-Path ([System.IO.Path]::GetTempPath()) ("vlccache-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $stage -Force | Out-Null

    $registered = $null
    try {
        # vlc-cache-gen needs libvlc beside it, and the plugin tree laid out as it ships.
        foreach ($dll in @("libvlc.dll", "libvlccore.dll")) {
            Copy-Item (Join-Path $InstallDir $dll) $stage -Force
        }
        Copy-Item $CacheGen $stage -Force

        foreach ($rel in $Plugins) {
            $src = Join-Path (Join-Path $InstallDir "plugins") $rel
            if (-not (Test-Path $src)) {
                throw "Plugin referenced by the nuspec was not built: $rel"
            }

            $dst = Join-Path (Join-Path $stage "plugins") $rel
            New-Item -ItemType Directory -Path (Split-Path $dst -Parent) -Force | Out-Null
            # Copy-Item preserves LastWriteTime, which the cache is keyed on.
            Copy-Item $src $dst -Force
        }

        Set-Content -LiteralPath (Join-Path $stage "AppxManifest.xml") -Value $AppxManifestTemplate -Encoding UTF8

        # A 1x1 PNG, only so the manifest validates.
        [System.IO.File]::WriteAllBytes((Join-Path $stage "logo.png"), [Convert]::FromBase64String(
            'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=='))

        # The cache is written next to the plugins, so the packaged process needs write access
        # to the staged tree.
        & icacls $stage /grant "*S-1-15-2-1:(OI)(CI)(M)" /T | Out-Null

        Get-AppxPackage -Name VlcCacheGen -ErrorAction SilentlyContinue |
            ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue }

        Add-AppxPackage -Register (Join-Path $stage "AppxManifest.xml")
        $registered = Get-AppxPackage -Name VlcCacheGen

        $log = Join-Path $stage "cache-gen.log"
        Invoke-CommandInDesktopPackage -PackageFamilyName $registered.PackageFamilyName -AppId "App" `
            -Command "$env:SystemRoot\System32\cmd.exe" `
            -Args "/c cd /d `"$stage`" && vlc-cache-gen.exe plugins > `"$log`" 2>&1" `
            -PreventBreakaway

        $dat = Join-Path (Join-Path $stage "plugins") "plugins.dat"
        for ($i = 0; $i -lt 60 -and -not (Test-Path $dat); $i++) {
            Start-Sleep -Milliseconds 500
        }

        if (-not (Test-Path $dat)) {
            $detail = if (Test-Path $log) { Get-Content $log -Raw } else { "(no output captured)" }
            throw "vlc-cache-gen produced no plugins.dat. Output: $detail"
        }

        # A cache with no entries is only a header; treat that as failure rather than shipping it.
        if ((Get-Item $dat).Length -lt 1024) {
            throw "vlc-cache-gen produced an empty cache ($((Get-Item $dat).Length) bytes) -- libvlc could not load the plugins"
        }

        Copy-Item $dat $OutFile -Force
        Write-Host "Generated $OutFile ($((Get-Item $OutFile).Length) bytes, $($Plugins.Count) plugins)"
    }
    finally {
        if ($null -ne $registered) {
            Remove-AppxPackage -Package $registered.PackageFullName -ErrorAction SilentlyContinue
        }
        Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Convert-PluginCache {
    param([string]$SourceCache, [string]$InstallDir, [string[]]$Plugins, [string]$OutFile)

    $bytes = [System.IO.File]::ReadAllBytes($SourceCache)
    $patched = 0

    foreach ($rel in $Plugins) {
        $file = Get-Item (Join-Path (Join-Path $InstallDir "plugins") $rel)

        # Locate the record by its path string: uint16 length prefix, then the NUL-terminated
        # path. Searching for the length prefix as well avoids matching the same text elsewhere.
        $pathBytes = [System.Text.Encoding]::ASCII.GetBytes($rel)
        $needle = New-Object byte[] ($pathBytes.Length + 3)
        [BitConverter]::GetBytes([uint16]($pathBytes.Length + 1)).CopyTo($needle, 0)
        $pathBytes.CopyTo($needle, 2)
        $needle[$needle.Length - 1] = 0

        $at = -1
        for ($i = 0; $i -le $bytes.Length - $needle.Length; $i++) {
            $match = $true
            for ($j = 0; $j -lt $needle.Length; $j++) {
                if ($bytes[$i + $j] -ne $needle[$j]) { $match = $false; break }
            }
            if ($match) { $at = $i; break }
        }

        if ($at -lt 0) {
            throw "Could not locate '$rel' in $SourceCache"
        }

        # path, then: char unloadable, int64 mtime, uint64 size
        $trailer = $at + $needle.Length + 1

        # Sanity check before writing: the size recorded here must be the x64 file's size. If it
        # is not, the offset is wrong and patching would corrupt the cache.
        $x64Size = [BitConverter]::ToUInt64($bytes, $trailer + 8)
        $expected = (Get-Item (Join-Path (Join-Path (Get-ArchDirectory -Root $Root -Arch "win64-uwp") "plugins") $rel)).Length
        if ($x64Size -ne $expected) {
            throw "Offset check failed for '$rel': cache records $x64Size bytes, x64 file is $expected"
        }

        $mtime = [DateTimeOffset]::new($file.LastWriteTimeUtc).ToUnixTimeSeconds()
        [BitConverter]::GetBytes([int64]$mtime).CopyTo($bytes, $trailer)
        [BitConverter]::GetBytes([uint64]$file.Length).CopyTo($bytes, $trailer + 8)
        $patched++
    }

    [System.IO.File]::WriteAllBytes($OutFile, $bytes)
    Write-Host "Wrote $OutFile ($patched plugins re-stamped)"
}

$nuspecPath = Join-Path $Root $Nuspec
$plugins = Get-ShippedPlugins -NuspecPath $nuspecPath
Write-Host "Nuspec ships $($plugins.Count) plugins"

$x64Dir = Get-ArchDirectory -Root $Root -Arch "win64-uwp"
$cacheGen = Join-Path $Root "vlc\win64-uwp\bin\.libs\vlc-cache-gen.exe"
if (-not (Test-Path $cacheGen)) {
    throw "vlc-cache-gen.exe not found at $cacheGen"
}

New-PluginCache -InstallDir $x64Dir -Plugins $plugins -CacheGen $cacheGen -OutFile (Join-Path $Root "plugins-x64.dat")

$armDir = Get-ArchDirectory -Root $Root -Arch "winarm64-uwp"
Convert-PluginCache -SourceCache (Join-Path $Root "plugins-x64.dat") -InstallDir $armDir -Plugins $plugins -OutFile (Join-Path $Root "plugins-arm64.dat")
