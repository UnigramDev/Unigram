param (
  [string]$vcpkg_root,
  # x86 and ARM are not offered: libvlc and webrtc declare "supports": "uwp & (x64 | arm64)", so a
  # manifest install for either fails before TDLib is reached, and the app has no such platform.
  [ValidateSet('x64', 'ARM64', IgnoreCase = $false)]
  [string[]]$arch = @( "x64", "ARM64" ),
  [string]$mode = "all"
)
$ErrorActionPreference = "Stop"

$arch_list = $arch

$td_root = Resolve-Path "../tdlib"

# TDLib and the app share one manifest, so that openssl and zlib cannot drift between the
# tdjson.dll we ship and the copies the app links against.
$manifest_root = Resolve-Path "../.."

function CheckLastExitCode {
  if ($LastExitCode -ne 0) {
    $msg = @"
EXE RETURNED EXIT CODE $LastExitCode
CALLSTACK:$(Get-PSCallStack | Out-String)
"@
    throw $msg
  }
}

# Which vcpkg, where it installs and under which triplet are all decided in Directory.Build.props.
# Evaluate it per platform rather than repeating any of that here: the installed tree in particular
# is one root per triplet, and a build that guessed the layout would install beside the app's tree
# instead of into it - or, sharing a root across triplets, purge it.
function ResolveVcpkgPaths {
  param([string]$platform)

  $installer = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer"
  $vswhere = Join-Path $installer "vswhere.exe"
  if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found at $vswhere"
  }

  $vs = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -property installationPath
  if (-not $vs) {
    throw "No Visual Studio installation with MSBuild was found."
  }

  $msbuild = Join-Path $vs "MSBuild\Current\Bin\amd64\MSBuild.exe"
  if (-not (Test-Path $msbuild)) {
    throw "MSBuild.exe not found at $msbuild"
  }

  # -getProperty evaluates the project, it does not build it. The stub imports the props file on
  # its own, so none of the C++ targets are involved; TelegramUsesVcpkg is what gates the property
  # there, and an explicit -vcpkg_root arrives as the global property the chain checks first.
  $props = Join-Path $manifest_root "Directory.Build.props"
  $stub = Join-Path ([System.IO.Path]::GetTempPath()) "tdjson-vcpkg-root-$PID.proj"
  Set-Content -LiteralPath $stub -Value "<Project><Import Project=`"$props`" /></Project>"

  Try {
    $arguments = @($stub, "-nologo", "-p:TelegramUsesVcpkg=true", "-p:Platform=$platform",
                   "-getProperty:VcpkgRoot,VcpkgInstalledDir,TelegramVcpkgTriplet")
    if ($vcpkg_root) {
      $arguments += "-p:VcpkgRoot=$((Resolve-Path $vcpkg_root).Path)"
    }
    $json = (& $msbuild @arguments | Out-String)
    CheckLastExitCode
  } Finally {
    Remove-Item $stub -Force -ErrorAction SilentlyContinue
  }

  $resolved = ($json | ConvertFrom-Json).Properties

  if (-not $resolved.VcpkgRoot) {
    throw "vcpkg was not found. Clone it next to this repository, install the vcpkg component in the Visual Studio installer, or set VCPKG_ROOT - see Documentation/Build-instructions.md."
  }
  if (-not $resolved.TelegramVcpkgTriplet) {
    throw "The app defines no vcpkg triplet for platform $platform, so TDLib cannot be built against the tree it uses."
  }

  return $resolved
}

function clean {
  Remove-Item build-* -Force -Recurse -ErrorAction SilentlyContinue
}

function prepare {
  New-Item -ItemType Directory -Force -Path build-native

  cd build-native

  cmake -A Win32 -DTD_GENERATE_SOURCE_FILES=ON -DTD_ENABLE_MULTI_PROCESSOR_COMPILATION=ON "$td_root"
  CheckLastExitCode
  cmake --build .
  CheckLastExitCode

  cd ..
}

function config {
  New-Item -ItemType Directory -Force -Path build-uwp
  cd build-uwp

  ForEach ($arch in $arch_list) {
    echo "Config Arch = [$arch]"
    New-Item -ItemType Directory -Force -Path $arch
    cd $arch
    echo "${td_root}"
    $vcpkg = ResolveVcpkgPaths $arch
    $vcpkg_cmake = Join-Path $vcpkg.VcpkgRoot "scripts\buildsystems\vcpkg.cmake"
    # Trailing backslashes are trimmed: cmake.exe parses \" as an escaped quote, and MSBuild hands
    # directory properties back with one.
    $installed_root = $vcpkg.VcpkgInstalledDir.TrimEnd('\')
    cmake -A $arch -DCMAKE_SYSTEM_VERSION="10.0" -DCMAKE_SYSTEM_NAME="WindowsStore" -DCMAKE_TOOLCHAIN_FILE="$vcpkg_cmake" -DVCPKG_MANIFEST_DIR="$manifest_root" -DVCPKG_INSTALLED_DIR="$installed_root" -DVCPKG_TARGET_TRIPLET="$($vcpkg.TelegramVcpkgTriplet)" -DTD_ENABLE_MULTI_PROCESSOR_COMPILATION=ON "$td_root"
    CheckLastExitCode
    cd ..
  }
  echo "done"
  cd ..
}

function build {
  cd build-uwp
  ForEach ($arch in $arch_list) {
    echo "Build Arch = [$arch]"
    cd $arch
    cmake --build . --config RelWithDebInfo --target tdjson
    CheckLastExitCode
    cd ..
  }
  cd ..
}

function export {
  cp ../tdlib/td/generate/scheme/td_api.tl .

  ForEach ($arch in $arch_list) {
    $fixed_arch = $arch.ToLower();
    Remove-Item $arch -Force -Recurse -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $arch

    cp build-uwp/${arch}/RelWithDebInfo/* -include "SSLEAY*","LIBEAY*","libcrypto*","libssl*","zlib*","tdjson.pdb","tdjson.dll" $arch
  }
}

function run {
  Push-Location
  Try {
    if ($mode -eq "clean") {
      clean
    }
    if (($mode -eq "prepare") -or ($mode -eq "all")) {
      prepare
    }
    if (($mode -eq "config") -or ( $mode -eq "all")) {
      config
    }
    if (($mode -eq "build") -or ($mode -eq "all")) {
      build
    }
    if (($mode -eq "export") -or ($mode -eq "all")) {
      export
    }
  } Finally {
    Pop-Location
  }
}

run
