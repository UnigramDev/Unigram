1. First, check that you have the [necessary tools](#requirements) installed.
2. Go to <https://my.telegram.org/apps> and register a new app.
3. Clone the repository __*recursively*__ by using `git clone --recursive https://github.com/UnigramDev/Unigram.git`.
4. Create a new file inside `Unigram/Unigram/Unigram` and name it `Constants.Secret.cs`:
```csharp
namespace Unigram
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
> git checkout 1b1ae50e1a69f7c659bd7d731e80b358d21c86ad
> ./bootstrap-vcpkg.bat
```

Now that vcpkg is ready, you must customize the **ffmpeg** port to be built with all the flags needed by the app:
- Navigate to `vcpkg\ports\ffmpeg`
- Open `portfile.cmake`
- Locate `--enable-libvpx` and replace it with the following:
```
--disable-everything --enable-hwaccel=h264_d3d11va --enable-hwaccel=h264_d3d11va2 --enable-hwaccel=h264_dxva2 --enable-hwaccel=hevc_d3d11va --enable-hwaccel=hevc_d3d11va2 --enable-hwaccel=hevc_dxva2 --enable-hwaccel=mpeg2_d3d11va --enable-hwaccel=mpeg2_d3d11va2 --enable-hwaccel=mpeg2_dxva2 --enable-protocol=file --enable-libopus --enable-libvpx --enable-decoder=aac --enable-decoder=aac_fixed --enable-decoder=aac_latm --enable-decoder=aasc --enable-decoder=alac --enable-decoder=flac --enable-decoder=gif --enable-decoder=h264 --enable-decoder=hevc --enable-decoder=libvpx_vp8 --enable-decoder=libvpx_vp9 --enable-decoder=mp1 --enable-decoder=mp1float --enable-decoder=mp2 --enable-decoder=mp2float --enable-decoder=mp3 --enable-decoder=mp3adu --enable-decoder=mp3adufloat --enable-decoder=mp3float --enable-decoder=mp3on4 --enable-decoder=mp3on4float --enable-decoder=mpeg4 --enable-decoder=msmpeg4v2 --enable-decoder=msmpeg4v3 --enable-decoder=opus --enable-decoder=pcm_alaw --enable-decoder=pcm_f32be --enable-decoder=pcm_f32le --enable-decoder=pcm_f64be --enable-decoder=pcm_f64le --enable-decoder=pcm_lxf --enable-decoder=pcm_mulaw --enable-decoder=pcm_s16be --enable-decoder=pcm_s16be_planar --enable-decoder=pcm_s16le --enable-decoder=pcm_s16le_planar --enable-decoder=pcm_s24be --enable-decoder=pcm_s24daud --enable-decoder=pcm_s24le --enable-decoder=pcm_s24le_planar --enable-decoder=pcm_s32be --enable-decoder=pcm_s32le --enable-decoder=pcm_s32le_planar --enable-decoder=pcm_s64be --enable-decoder=pcm_s64le --enable-decoder=pcm_s8 --enable-decoder=pcm_s8_planar --enable-decoder=pcm_u16be --enable-decoder=pcm_u16le --enable-decoder=pcm_u24be --enable-decoder=pcm_u24le --enable-decoder=pcm_u32be --enable-decoder=pcm_u32le --enable-decoder=pcm_u8 --enable-decoder=vorbis --enable-decoder=wavpack --enable-decoder=wmalossless --enable-decoder=wmapro --enable-decoder=wmav1 --enable-decoder=wmav2 --enable-decoder=wmavoice --enable-encoder=libopus --enable-parser=aac --enable-parser=aac_latm --enable-parser=flac --enable-parser=h264 --enable-parser=hevc --enable-parser=mpeg4video --enable-parser=mpegaudio --enable-parser=opus --enable-parser=vorbis --enable-demuxer=aac --enable-demuxer=flac --enable-demuxer=gif --enable-demuxer=h264 --enable-demuxer=hevc --enable-demuxer=matroska --enable-demuxer=m4v --enable-demuxer=mov --enable-demuxer=mp3 --enable-demuxer=ogg --enable-demuxer=wav --enable-muxer=ogg --enable-muxer=opus
```
Now that everything is properly configured go back to the terminal and enter the following:
```
> ./vcpkg.exe install ffmpeg[opus,vpx]:$arch$-uwp lz4:$arch$-uwp openssl:$arch$-uwp zlib:$arch$-uwp libwebp:$arch$-uwp libogg:$arch$-uwp opus:$arch$-uwp
> ./vcpkg.exe integrate install
```
Make sure to replace `$arch$` with either `x64`, `x86` or `arm64` depending on your build target.

### WebRTC
Unigram uses WebRTC for calls and video chats. Since WebRTC doesn't currently support UWP, you must use our fork to build it.
1. Click on Start Menu → Visual Studio 2019 → x64 Native Tools Command Prompt for VS 2022.
2. Navigate to .\Unigram\Libraries\webrtc
3. Execute `.\acquire-m108.cmd`. This will clone WebRTC source code to `C:\webrtc`, and it will take a while (~1.5h)
4. Execute `.\build-m108.cmd "$arch$" "$config$"`. Replace `$arch$` with either `x64`, `win32` or `arm64` depending on your build target. `$config$` can be set to either `release` or `debug`.

⚠️ Note that WebRTC build instructions are based on [WinRTC](https://github.com/microsoft/winrtc/tree/master/patches_for_WebRTC_org/m84).

### Building without WebRTC
Since compiling WebRTC is time and resources consuming, it is possible to build the app without calls support:
- Locate Unigram > References and remove `Unigram.Native.Calls` from the list.
- From Unigram > Properties > Build, remove `ENABLE_CALLS` directive.
- Exclude from the project the following files:
  - Controls/Cells/GroupCallParticipantGridCell.xaml
  - Views/GroupCallPage.xaml
  - Views/LiveStreamPage.xaml
  - Views/VoIPPage.xaml

## Requirements

The following tools and SDKs are mandatory for the project development:
* Visual Studio 2022, with
    * .NET desktop development
    * Desktop development with C++
    * Universal Windows Platform deveopment
	    * Windows 11 SDK (10.0.22621.0)
    * [TDLib for Universal Windows Platform](https://tdlib.github.io/td/build.html?language=C%23)

### Code fails to build?

If the code fails to build make sure to create a [new issue](https://github.com/UnigramDev/Unigram/issues/new?assignees=&labels=needs-triage&template=anything-else.md&title=) or to open a [pull request](https://github.com/UnigramDev/Unigram/compare).
