param(
    [ValidateSet('x64', 'ARM64', IgnoreCase = $false)]
    [string[]]$arch = @( "x64", "ARM64" )
)

$ArchMap = @{
    "x64"   = "x86_64"
    "arm64" = "aarch64"
}

$NormalizedArch = @()
foreach ($a in $arch) {
    $key = $a.ToLower()
    if ($ArchMap.ContainsKey($key)) {
        $NormalizedArch += $ArchMap[$key]
    } else {
        Write-Host "⚠️ Unknown arch '$a', passing as-is."
        $NormalizedArch += $a
    }
}

$CurrentPath = (Get-Location).ProviderPath
Write-Host "==> Current Path: $CurrentPath"

# Step 1: Apply patches
#$PatchFiles = Get-ChildItem -Path $CurrentPath -Filter *.patch
#if ($PatchFiles.Count -gt 0) {
#    Push-Location "$CurrentPath\vlc"
#    foreach ($Patch in $PatchFiles) {
#        Write-Host "Applying patch: $($Patch.Name)"
#        git am --keep-cr $Patch.FullName 2>$null
#        if ($LASTEXITCODE -ne 0) {
#            Write-Host "Patch already applied or failed, skipping..."
#            git am --abort 2>$null
#        }
#    }
#    Pop-Location
#} else {
#    Write-Host "No patch files found."
#}

# Step 2: Copy revision.txt
$RevisionFile = Join-Path $CurrentPath "revision.txt"
$DestFolder   = Join-Path $CurrentPath "vlc\src"
if (Test-Path $RevisionFile) {
    if (-not (Test-Path $DestFolder)) {
        New-Item -ItemType Directory -Path $DestFolder -Force | Out-Null
    }
    # Normalise rather than copy: the Makefile inside the container cats this file straight
    # into a C string literal, so a CRLF here puts a bare CR inside the literal and
    # src/revision.c fails to compile ("expected expression").
    $Revision = (Get-Content $RevisionFile -Raw).Trim()
    [System.IO.File]::WriteAllText((Join-Path $DestFolder "revision.txt"), $Revision + "`n")
    Write-Host "Copied revision.txt to vlc/src/ ($Revision)"
} else {
    Write-Host "revision.txt not found, skipping..."
}

# Step 3: Build inside Docker with live logs
$SourceFolder = Join-Path $CurrentPath "vlc"
$SourceFolder = $SourceFolder -replace '\\','/'
$SourceFolder = "`"$SourceFolder`""

$DockerCommand = "cd ../vlc`n"

foreach ($a in $NormalizedArch) {
    # -D takes its value as a separate argument. Passing it as -D=<path> makes getopts
    # capture the '=' as part of OPTARG, which ends up in the -fdebug-prefix-map
    # replacement and puts a literal '=' in front of every source path recorded in the
    # PDBs, e.g. "=C:\...\win64-uwp\modules\=C:\...\modules\audio_output\wasapi.c".
    $DockerCommand += "extras/package/win32/build.sh -a $a -z -r -u -w -D $SourceFolder`n"
}

$DockerCommand += "exit`n"

Write-Host "Launching Docker..."

# No -it: it needs a terminal on stdin, so the script cannot run from CI or any
# non-interactive shell ("cannot attach stdin to a TTY-enabled container").
docker run --rm -v "${CurrentPath}\vlc:/vlc" registry.videolan.org/vlc-debian-llvm-uwp:20211020111246 bash -c "$DockerCommand"

if ($LASTEXITCODE -ne 0) {
    # Without this the script used to carry on and pack whatever happened to be lying around
    # from an earlier build, turning a failed build into a stale package.
    throw "Docker build failed with exit code $LASTEXITCODE"
}

# Step 4: Generate the plugin cache
# Cross-compiled builds skip it (Makefile.am only runs vlc-cache-gen when build == host), so
# without this libvlc rescans every plugin on each startup.
Write-Host "Generating plugin cache..."
& (Join-Path $CurrentPath "plugins-cache.ps1") -Root $CurrentPath

# Step 5: Pack NuGet
# The nuspec refers to the install directory by version (vlc-3.0.23), so the version is
# substituted rather than hardcoded 284 times. A wildcard would be simpler but NuGet then
# treats every target as a directory, producing build/win10-x64/libvlc.dll/libvlc.dll.
$VlcVersion = (Get-ChildItem -Path (Join-Path $CurrentPath "vlc\win64-uwp") -Directory -Filter "vlc-*" |
    Select-Object -First 1).Name -replace '^vlc-', ''
if (-not $VlcVersion) {
    throw "Could not determine the built VLC version from vlc\win64-uwp"
}

Write-Host "Packing NuGet (VLC $VlcVersion)..."
nuget pack VideoLAN.LibVLC.UWP.nuspec -Properties "vlcver=$VlcVersion" -OutputDirectory "${CurrentPath}\.."
