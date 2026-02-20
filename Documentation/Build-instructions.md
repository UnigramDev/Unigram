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
--disable-everything --enable-protocol=file --enable-libopus --enable-libdav1d --enable-libvpx --enable-decoder=aac --enable-decoder=aac_at --enable-decoder=aac_fixed --enable-decoder=aac_latm --enable-decoder=aasc --enable-decoder=ac3 --enable-decoder=alac --enable-decoder=alac_at --enable-decoder=av1 --enable-decoder=eac3 --enable-decoder=flac --enable-decoder=gif --enable-decoder=h264 --enable-decoder=hevc --enable-decoder=libdav1d --enable-decoder=libvpx_vp8 --enable-decoder=libvpx_vp9 --enable-decoder=mp1 --enable-decoder=mp1float --enable-decoder=mp2 --enable-decoder=mp2float --enable-decoder=mp3 --enable-decoder=mp3adu --enable-decoder=mp3adufloat --enable-decoder=mp3float --enable-decoder=mp3on4 --enable-decoder=mp3on4float --enable-decoder=mpeg4 --enable-decoder=msmpeg4v2 --enable-decoder=msmpeg4v3 --enable-decoder=opus --enable-decoder=pcm_alaw --enable-decoder=pcm_alaw_at --enable-decoder=pcm_f32be --enable-decoder=pcm_f32le --enable-decoder=pcm_f64be --enable-decoder=pcm_f64le --enable-decoder=pcm_lxf --enable-decoder=pcm_mulaw --enable-decoder=pcm_mulaw_at --enable-decoder=pcm_s16be --enable-decoder=pcm_s16be_planar --enable-decoder=pcm_s16le --enable-decoder=pcm_s16le_planar --enable-decoder=pcm_s24be --enable-decoder=pcm_s24daud --enable-decoder=pcm_s24le --enable-decoder=pcm_s24le_planar --enable-decoder=pcm_s32be --enable-decoder=pcm_s32le --enable-decoder=pcm_s32le_planar --enable-decoder=pcm_s64be --enable-decoder=pcm_s64le --enable-decoder=pcm_s8 --enable-decoder=pcm_s8_planar --enable-decoder=pcm_u16be --enable-decoder=pcm_u16le --enable-decoder=pcm_u24be --enable-decoder=pcm_u24le --enable-decoder=pcm_u32be --enable-decoder=pcm_u32le --enable-decoder=pcm_u8 --enable-decoder=vorbis --enable-decoder=vp8 --enable-decoder=wavpack --enable-decoder=wmalossless --enable-decoder=wmapro --enable-decoder=wmav1 --enable-decoder=wmav2 --enable-decoder=wmavoice --enable-encoder=aac --enable-encoder=libopus --enable-parser=aac --enable-parser=aac_latm --enable-parser=flac --enable-parser=gif --enable-parser=h264 --enable-parser=hevc --enable-parser=mpeg4video --enable-parser=mpegaudio --enable-parser=opus --enable-parser=vorbis --enable-demuxer=aac --enable-demuxer=flac --enable-demuxer=gif --enable-demuxer=h264 --enable-demuxer=hevc --enable-demuxer=matroska --enable-demuxer=m4v --enable-demuxer=mov --enable-demuxer=mp3 --enable-demuxer=ogg --enable-demuxer=w64 --enable-demuxer=wav --enable-muxer=mp4 --enable-muxer=ogg --enable-muxer=opus
```
Now that everything is properly configured go back to the terminal and enter the following:
```
> ./vcpkg.exe install ffmpeg[dav1d,opus,vpx]:x64-uwp lz4:x64-uwp openssl:x64-uwp zlib:x64-uwp libogg:x64-uwp opus:x64-uwp boost-regex:x64-uwp
> ./vcpkg.exe install ffmpeg[dav1d,opus,vpx]:arm64-uwp lz4:arm64-uwp openssl:arm64-uwp zlib:arm64-uwp libogg:arm64-uwp opus:arm64-uwp boost-regex:arm64-uwp
> ./vcpkg.exe integrate install
```

You can choose to install both `x64` and `arm64` or just opt for the architecture you need.

### TDLib
In order to communicate with Telegram servers, Unigram uses TDLib.
Here is complete instruction for TDLib binaries building:

- Download and install Microsoft Visual Studio. Enable C++ and Windows 10 SDK support while installing.
- Download and install CMake; choose "Add CMake to the system PATH" option while installing.
- Download and install Git.
- Download and unpack PHP. Add the path to php.exe to the PATH environment variable.
- Close and re-open PowerShell if the PATH environment variable was changed.
- Run these commands in PowerShell to build TDLib and to install it to td/tdlib:

```shell
> git clone https://github.com/tdlib/td.git
> cd td
> git clone https://github.com/Microsoft/vcpkg.git
> cd vcpkg
> git checkout cff6ed45719c0162fa7065fdac90506a0add812c
> ./bootstrap-vcpkg.bat
> ./vcpkg.exe install gperf:x86-windows openssl:x64-uwp zlib:x64-uwp
> ./vcpkg.exe install gperf:x86-windows openssl:arm64-uwp zlib:x64-uwp
> cd ..
> cd example/uwp
> powershell -ExecutionPolicy ByPass ./build.ps1 -vcpkg_root ../../vcpkg -nupkg
```

You can choose to install both `x64` and `arm64` or just opt for the architecture you need.
Note that `gperf:x86-windows` is needed regardless of the architecture you choose.
The resulting .nupkg file must be copied into Unigram\Libraries.

### LibVLC
Unigram uses LibVLC to play videos and audio in the app. We can't use the system provided media player doesn't meet the app quality expectations.
The app is currently using version `3.0.22-rc1` with some patches applied on top, and can be built by running the script `build.ps1` located in `Unigram repository\Libraries\vlc`.

Building LibVLC requires [Docker](https://docs.docker.com/desktop/setup/install/windows-install/) to be installed and running.

```shell
powershell -ExecutionPolicy ByPass ./build.ps1 -arch x64,ARM64
```

The script will automatically apply the needed patches to libvlc (that comes as a submodule when you clone the repository) and create a NuGet package inside the `Libraries` folder.

For reference, this is the list of VLC plugins currently needed by Unigram to properly work:
- access\libhttps_plugin.dll
- access\libhttp_plugin.dll
- access\libimem_plugin.dll
- audio_filter\libaudio_format_plugin.dll
- audio_filter\libsamplerate_plugin.dll
- audio_filter\libscaletempo_plugin.dll
- audio_filter\libtrivial_channel_mixer_plugin.dll
- audio_filter\libugly_resampler_plugin.dll
- audio_mixer\libfloat_mixer_plugin.dll
- audio_output\libwasapi_plugin.dll
- audio_output\libwinstore_plugin.dll
- codec\libavcodec_plugin.dll
- codec\libd3d11va_plugin.dll
- codec\libdav1d_plugin.dll
- codec\libflac_plugin.dll
- codec\libfaad_plugin.dll
- codec\libmpg123_plugin.dll
- codec\libopus_plugin.dll
- demux\libes_plugin.dll
- demux\libflacsys_plugin.dll
- demux\libmp4_plugin.dll
- demux\libogg_plugin.dll
- demux\libps_plugin.dll
- packetizer\libpacketizer_flac_plugin.dll
- packetizer\libpacketizer_h264_plugin.dll
- packetizer\libpacketizer_mpegaudio_plugin.dll
- packetizer\libpacketizer_mpegvideo_plugin.dll
- stream_filter\libcache_block_plugin.dll
- stream_filter\libcache_read_plugin.dll
- stream_filter\librecord_plugin.dll
- stream_filter\libskiptags_plugin.dll
- text_renderer\libtdummy_plugin.dll
- video_chroma\libswscale_plugin.dll
- video_chroma\libyuvp_plugin.dll
- video_output\libdirect3d11_plugin.dll

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
