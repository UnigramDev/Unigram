## Requirements

The following tools and SDKs are mandatory for the project development:
* Visual Studio 2022, with
    * .NET desktop development
    * Desktop development with C++
    * Universal Windows Platform deveopment
	    * Windows 11 SDK (10.0.22621.0)
 
## Getting started

1. First, check that you have the [necessary tools](#requirements) installed.
2. Go to <https://my.telegram.org/apps> and register a new app.
3. Clone the repository __*recursively*__ by using `git clone --recursive https://github.com/UnigramDev/Unigram.git`.
4. Create a new file inside `Unigram/Telegram` and name it `Constants.Secret.cs`:
```csharp
namespace Telegram
{
    public static partial class Constants
    {
        static Constants()
        {
            ApiId = your_api_id;
            ApiHash = "your_api_hash";
            
            AppChannel = "Telegram channel username used for in-app updates";

            AppCenterId = "AppCenter ID used for crash data";
            BingMapsApiKey = "Bing Maps API key used for map previews";
        }
    }
}
```
5. Replace `your_api_id` and `your_api_hash` with the data obtained from step 2.

## Dependencies

Unigram uses NuGet for managed dependencies and vcpkg for unmanaged ones.
Run the following commands to clone vcpkg:
```shell
> git clone https://github.com/Microsoft/vcpkg.git
> cd vcpkg
> git checkout cff6ed45719c0162fa7065fdac90506a0add812c
> ./bootstrap-vcpkg.bat
```

Apply the patch contained in `Libraries\vcpkg.patch`.

Now that vcpkg is ready, you must customize the **ffmpeg** port to be built with all the flags needed by the app:
- Navigate to `vcpkg\ports\ffmpeg`
- Open `portfile.cmake`
- Locate `--enable-libvpx` and replace it with the following:
```
--disable-everything --enable-protocol=file --enable-libopus --enable-libvpx --enable-decoder=aac --enable-decoder=aac_at --enable-decoder=aac_fixed --enable-decoder=aac_latm --enable-decoder=aasc --enable-decoder=ac3 --enable-decoder=alac --enable-decoder=alac_at --enable-decoder=av1 --enable-decoder=eac3 --enable-decoder=flac --enable-decoder=gif --enable-decoder=h264 --enable-decoder=hevc --enable-decoder=libvpx_vp8 --enable-decoder=libvpx_vp9 --enable-decoder=mp1 --enable-decoder=mp1float --enable-decoder=mp2 --enable-decoder=mp2float --enable-decoder=mp3 --enable-decoder=mp3adu --enable-decoder=mp3adufloat --enable-decoder=mp3float --enable-decoder=mp3on4 --enable-decoder=mp3on4float --enable-decoder=mpeg4 --enable-decoder=msmpeg4v2 --enable-decoder=msmpeg4v3 --enable-decoder=opus --enable-decoder=pcm_alaw --enable-decoder=pcm_alaw_at --enable-decoder=pcm_f32be --enable-decoder=pcm_f32le --enable-decoder=pcm_f64be --enable-decoder=pcm_f64le --enable-decoder=pcm_lxf --enable-decoder=pcm_mulaw --enable-decoder=pcm_mulaw_at --enable-decoder=pcm_s16be --enable-decoder=pcm_s16be_planar --enable-decoder=pcm_s16le --enable-decoder=pcm_s16le_planar --enable-decoder=pcm_s24be --enable-decoder=pcm_s24daud --enable-decoder=pcm_s24le --enable-decoder=pcm_s24le_planar --enable-decoder=pcm_s32be --enable-decoder=pcm_s32le --enable-decoder=pcm_s32le_planar --enable-decoder=pcm_s64be --enable-decoder=pcm_s64le --enable-decoder=pcm_s8 --enable-decoder=pcm_s8_planar --enable-decoder=pcm_u16be --enable-decoder=pcm_u16le --enable-decoder=pcm_u24be --enable-decoder=pcm_u24le --enable-decoder=pcm_u32be --enable-decoder=pcm_u32le --enable-decoder=pcm_u8 --enable-decoder=vorbis --enable-decoder=vp8 --enable-decoder=wavpack --enable-decoder=wmalossless --enable-decoder=wmapro --enable-decoder=wmav1 --enable-decoder=wmav2 --enable-decoder=wmavoice --enable-encoder=aac --enable-encoder=libopus --enable-parser=aac --enable-parser=aac_latm --enable-parser=flac --enable-parser=gif --enable-parser=h264 --enable-parser=hevc --enable-parser=mpeg4video --enable-parser=mpegaudio --enable-parser=opus --enable-parser=vorbis --enable-demuxer=aac --enable-demuxer=flac --enable-demuxer=gif --enable-demuxer=h264 --enable-demuxer=hevc --enable-demuxer=matroska --enable-demuxer=m4v --enable-demuxer=mov --enable-demuxer=mp3 --enable-demuxer=ogg --enable-demuxer=w64 --enable-demuxer=wav --enable-muxer=mp4 --enable-muxer=ogg --enable-muxer=opus
```
Now that everything is properly configured go back to the terminal and enter the following:
```
> ./vcpkg.exe install ffmpeg[opus,vpx]:$arch$-uwp lz4:$arch$-uwp openssl:$arch$-uwp zlib:$arch$-uwp libogg:$arch$-uwp opus:$arch$-uwp boost-regex:$arch$-uwp
> ./vcpkg.exe integrate install
```
Make sure to replace `$arch$` with either `x64`, `x86` or `arm64` depending on your build target.

### TDLib
In order to communicate with Telegram servers, Unigram uses TDLib.
Here is complete instruction for TDLib binaries building, taken from the official [documentation](https://tdlib.github.io/td/build.html?language=C%23):

- Download and install Microsoft Visual Studio. Enable C++ and Windows 10 SDK support while installing.
- Download and install CMake; choose "Add CMake to the system PATH" option while installing.
- Download and install Git.
- Download and unpack PHP. Add the path to php.exe to the PATH environment variable.
- Download and install 7-Zip archiver. Add the path to 7z.exe to the PATH environment variable.
- Close and re-open PowerShell if the PATH environment variable was changed.
- Run these commands in PowerShell to build TDLib and to install it to td/tdlib:

```shell
> git clone https://github.com/tdlib/td.git
> cd td
> git clone https://github.com/Microsoft/vcpkg.git
> cd vcpkg
> git checkout cff6ed45719c0162fa7065fdac90506a0add812c
> ./bootstrap-vcpkg.bat
> ./vcpkg.exe install gperf:x86-windows openssl:arm-uwp openssl:arm64-uwp openssl:x64-uwp openssl:x86-uwp zlib:arm-uwp zlib:arm64-uwp zlib:x64-uwp zlib:x86-uwp
> cd ..
> cd example/uwp
> powershell -ExecutionPolicy ByPass ./build.ps1 -vcpkg_root ../../vcpkg -nupkg
```

The resulting .nupkg file must be copied into Unigram\Libraries.

### VLC
Unigram uses VLC to play videos and audio in the app. This is required because the system provided media player doesn't meet the app quality expectations.
You can freely use the pre-built NuGet packages `VideoLAN.LibVLC.UWP` and `LibVLCSharp` provided by VLC, however we ship the app with binaries built by us,
as we disable all the features that we don't need to save a bit of disk space:
1. Clone VLC [repository](https://code.videolan.org/videolan/vlc) in `C:\Source` and check out branch `3.0.x`.
2. Apply the patch located in `Unigram repository\Libraries\vlc` by running the following command :
```
git apply --3way --ignore-whitespace {Your Repository Folder}\Libraries\vlc\vlc.patch
```
3. Make sure to have docker installed on your machine 
4. Open the terminal and run the following commands:
```
docker run -it -v C:\Source\vlc:/vlc registry.videolan.org/vlc-debian-llvm-uwp:20200706065223
cd ../vlc
extras/package/win32/build.sh -a x86_64 -z -r -u -w -D=C:/Source/vlc
```
When the building is complete, the NuGet package can be [manually created](https://code.videolan.org/videolan/LibVLCSharp).

For reference, this is the list of VLC plugins currently needed by Unigram to properly work:
- access\libimem_plugin.dll
- audio_filter\libaudio_format_plugin.dll # .MP3/.M4A/.OGG/.FLAC
- audio_filter\libsamplerate_plugin.dll
- audio_filter\libscaletempo_plugin.dll
- audio_filter\libtrivial_channel_mixer_plugin.dll # .OGG
- audio_filter\libugly_resampler_plugin.dll
- audio_mixer\libfloat_mixer_plugin.dll
- audio_output\libwasapi_plugin.dll
- audio_output\libwinstore_plugin.dll
- codec\libavcodec_plugin.dll
- codec\libd3d11va_plugin.dll
- codec\libflac_plugin.dll # .FLAC
- codec\libmpg123_plugin.dll # .MP3
- codec\libopus_plugin.dll # .OGG
- demux\libes_plugin.dll # .MP3
- demux\libflacsys_plugin.dll # .FLAC
- demux\libmp4_plugin.dll
- demux\libogg_plugin.dll # .OGG
- packetizer\libpacketizer_flac_plugin.dll # .FLAC
- packetizer\libpacketizer_mpegaudio_plugin.dll # .MP3
- stream_filter\libcache_read_plugin.dll
- stream_filter\librecord_plugin.dll
- stream_filter\libskiptags_plugin.dll # .MP3
- text_renderer\libtdummy_plugin.dll
- video_chroma\libswscale_plugin.dll
- video_chroma\libyuvp_plugin.dll
- video_output\libdirect3d11_plugin.dll

⚠️ TODO: there must be a way to compile WITHOUT libd3d11va and just use libavcodec

### VLC-arm64-NuGet Package

To build the VLC-arm64 NuGet package for Unigram, please follow these steps in Git Bash shell:
```
git clone https://code.videolan.org/videolan/libvlc-nuget.git
cd libvlc-nuget
git apply <path-to-Unigram>/Libraries/vlc/0001-vlc-nuget-win-arm64.patch
bash ./package-nuget-win-arm64.bash 4.0.0
cp VideoLAN.LibVLC.UWP.4.0.0.nupkg <path-to-Unigram>/Libraries/
```

1. Clone the VLC-NuGet Repository at your desired location.
2. Navigate to the root of the cloned repository and apply the patch from `Unigram/libraries/vlc`.
3. Run the Package Creation Script (`package-nuget-win-arm64.bash`) with version (`4.0.0`) in git bash terminal from the root folder of the `libvlc-nuget` repository.
4. On Successful execution, the `VideoLAN.LibVLC.UWP.4.0.0.nupkg` file will be created in the same directory.
5. Copy the generated NuGet package to the `Unigram/Libraries` directory to build Unigram for win-arm64.

### WebRTC
Unigram uses WebRTC for calls and video chats. Since WebRTC doesn't currently support UWP, you must use our fork to build it.
1. Click on Start Menu → Visual Studio 2022 → x64 Native Tools Command Prompt for VS 2022.
2. Navigate to .\Unigram\Libraries\webrtc
3. Execute `.\acquire.cmd`. This will clone WebRTC source code to `C:\webrtc`, and it will take a while (~1.5h)
4. Execute `.\build.cmd "$arch$" "$config$"`. Replace `$arch$` with either `x64`, `win32` or `arm64` depending on your build target. `$config$` can be set to either `release` or `debug`.

⚠️ Note that WebRTC build instructions are based on [WinRTC](https://github.com/microsoft/winrtc/tree/master/patches_for_WebRTC_org/m84).

### Building without WebRTC
Since compiling WebRTC is time and resources consuming, it is possible to build the app without calls support:
- Locate Telegram > References and remove `Telegram.Native.Calls` from the list.
- From Telegram > Properties > Build, remove `ENABLE_CALLS` directive.
- Exclude from the project the following files:
  - Controls/Cells/GroupCallParticipantGridCell.xaml
  - Views/Calls/*


### Code fails to build?

If the code fails to build make sure to create a [new issue](https://github.com/UnigramDev/Unigram/issues/new?assignees=&labels=needs-triage&template=anything-else.md&title=) or to open a [pull request](https://github.com/UnigramDev/Unigram/compare).
