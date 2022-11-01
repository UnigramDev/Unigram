@echo off

echo.
echo Downloading the depot_tools...
curl https://storage.googleapis.com/chrome-infra/depot_tools.zip --output depot_tools.zip
if errorlevel 1 goto :error

echo.
echo Opening the zip file...
c:
mkdir c:\depot_tools
if errorlevel 1 goto :error

tar -xf depot_tools.zip -C /../depot_tools/
if errorlevel 1 goto :error

echo.
echo Deleting the depot_tools.zip file
del depot_tools.zip

echo.
echo Setting environment variables
set PATH=c:\depot_tools;%PATH%
set DEPOT_TOOLS_WIN_TOOLCHAIN=0
set GYP_MSVS_VERSION=2022

echo.
echo Creating the folder where the code base will be placed...
c:
mkdir c:\webrtc
if errorlevel 1 goto :error

cd c:\webrtc
if errorlevel 1 goto :error

REM Downloading the bits
echo.
echo Telling the gclient tool to initialize your local copy of the repos...
call gclient
if errorlevel 1 goto :error

echo.
echo Requesting the tools to fetch the WebRTC code base...
call fetch --nohooks webrtc
if errorlevel 1 goto :error

echo.
echo Changing to the branch-heads/5304 branch...
cd src
if errorlevel 1 goto :error

call git checkout branch-heads/5304
if errorlevel 1 goto :error

echo.
echo Instructing the tools to bring the bits from all the sub repositories to your dev box...
call gclient sync -D -r branch-heads/5304
if errorlevel 1 goto :error

echo.
echo Adding forked Telegram+UWP upstream
call git remote add upstream https://github.com/FrayxRulez/webrtc-uwp.git
call git checkout m107
call git apply "build/M107.patch" --directory="C:\webrtc\src\build"
call git apply "third_party/M107.patch" --directory="C:\webrtc\src\third_party"
call git apply "third_party/libyuv/M107.patch" --directory="C:\webrtc\src\third_party\libyuv"

goto :exit

:error
echo Last command failed with erro code: %errorlevel%

:exit
exit /b %errorlevel%
