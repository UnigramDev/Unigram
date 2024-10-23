@echo off
setlocal EnableDelayedExpansion

echo.
echo Setting environment variables
set PATH=c:\depot_tools;%PATH%
set DEPOT_TOOLS_WIN_TOOLCHAIN=0
set GYP_MSVS_VERSION=2022
cd c:\webrtc\src
if errorlevel 1 goto :error

echo.
echo Opening the developer command prompt

echo Checking the Architecture type
for /f "skip=1" %%a in ('wmic cpu get architecture') do (
    set "cpu_arch=%%a"
)
if "%cpu_arch%"=="12" (
    call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=arm64
) else (
    call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=amd64
)
if errorlevel 1 goto :error

for %%a in (%~1) do (
    for %%c in (%~2) do (
        if /I %%c==release (set is_debug=false) else (set is_debug=true)

        echo.
        echo Preparing to build the drop for UWP %%a is_debug=!is_debug!
        call gn gen --ide=vs2022 out\msvc\uwp\%%c\%%a --filters=//:webrtc "--args=is_debug=!is_debug! use_lld=false is_clang=false rtc_include_tests=false rtc_build_tools=false rtc_win_video_capture_winrt=true target_os=\"winuwp\" rtc_build_examples=false rtc_win_use_mf_h264=true rtc_enable_protobuf=false rtc_disable_metrics=true rtc_include_dav1d_in_internal_decoder_factory=false treat_warnings_as_errors=false use_custom_libcxx=false fatal_linker_warnings=false target_cpu=\"%%a\""
        if errorlevel 1 goto :error

        REM Building for UWP x64
        echo.
        echo Building the patched WebRTC for UWP %%a is_debug=!is_debug!
        call ninja -C out\msvc\uwp\%%c\%%a
        if errorlevel 1 goto :error
    )
)
