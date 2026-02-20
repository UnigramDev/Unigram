param (
  [string]$vcpkg_root = $(throw "-vcpkg_root=<path to vcpkg> is required"),
  [ValidateSet('x86', 'x64', 'ARM', 'ARM64', IgnoreCase = $false)]
  [string[]]$arch = @( "x86", "x64", "ARM", "ARM64" ),
  [string]$mode = "all"
)
$ErrorActionPreference = "Stop"

$vcpkg_root = Resolve-Path $vcpkg_root

$vcpkg_cmake="${vcpkg_root}\scripts\buildsystems\vcpkg.cmake"
$arch_list = $arch
$td_root = Resolve-Path "../tdlib"

function CheckLastExitCode {
  if ($LastExitCode -ne 0) {
    $msg = @"
EXE RETURNED EXIT CODE $LastExitCode
CALLSTACK:$(Get-PSCallStack | Out-String)
"@
    throw $msg
  }
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
    $fixed_arch = $arch
    if ($arch -eq "x86") {
      $fixed_arch = "win32"
    }
    cmake -A $fixed_arch -DCMAKE_SYSTEM_VERSION="10.0" -DCMAKE_SYSTEM_NAME="WindowsStore" -DCMAKE_TOOLCHAIN_FILE="$vcpkg_cmake" -DTD_ENABLE_MULTI_PROCESSOR_COMPILATION=ON "$td_root"
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
