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
$PatchFiles = Get-ChildItem -Path $CurrentPath -Filter *.patch
if ($PatchFiles.Count -gt 0) {
    Push-Location "$CurrentPath\vlc"
    foreach ($Patch in $PatchFiles) {
        Write-Host "Applying patch: $($Patch.Name)"
        git am --keep-cr $Patch.FullName 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Patch already applied or failed, skipping..."
            git am --abort 2>$null
        }
    }
    Pop-Location
} else {
    Write-Host "No patch files found."
}

# Step 2: Copy revision.txt
$RevisionFile = Join-Path $CurrentPath "revision.txt"
$DestFolder   = Join-Path $CurrentPath "vlc\src"
if (Test-Path $RevisionFile) {
    if (-not (Test-Path $DestFolder)) {
        New-Item -ItemType Directory -Path $DestFolder -Force | Out-Null
    }
    Copy-Item $RevisionFile -Destination $DestFolder -Force
    Write-Host "Copied revision.txt to vlc/src/"
} else {
    Write-Host "revision.txt not found, skipping..."
}

# Step 3: Build inside Docker with live logs
$SourceFolder = Join-Path $CurrentPath "vlc"
$SourceFolder = $SourceFolder -replace '\\','/'
$SourceFolder = "`"$SourceFolder`""

$DockerCommand = "cd ../vlc`n"

foreach ($a in $NormalizedArch) {
    $DockerCommand += "extras/package/win32/build.sh -a $a -z -r -u -w -D=$SourceFolder`n"
}

$DockerCommand += "exit`n"

Write-Host "Launching Docker..."
docker run -it -v "${CurrentPath}\vlc:/vlc" registry.videolan.org/vlc-debian-llvm-uwp:20211020111246 bash -c "$DockerCommand"

# Step 4: Pack NuGet
Write-Host "Packing NuGet..."
nuget pack VideoLAN.LibVLC.UWP.nuspec -OutputDirectory "${CurrentPath}\.."
