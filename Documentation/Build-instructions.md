## Requirements

The following tools and SDKs are mandatory for the project development:
* Visual Studio 2026, with
    * .NET desktop development
    * Desktop development with C++
    * Universal Windows Platform deveopment
	    * Windows 11 SDK (10.0.26100.0)
 
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

If the **vcpkg package manager** component is selected in the Visual Studio installer, there is
nothing to do — the build finds the copy that ships with Visual Studio. Otherwise clone vcpkg
next to this repository, so that `vcpkg` and `Unigram` are siblings:
```shell
> git clone https://github.com/Microsoft/vcpkg.git
> cd vcpkg
> ./bootstrap-vcpkg.bat
```

The build looks for vcpkg in that order, and `VCPKG_ROOT` overrides both if you keep it
elsewhere.

Two things about that checkout matter:

- It must be **complete**. Manifest mode checks each port out of the vcpkg git history, so a
  `--depth` or `--filter` clone fails with a confusing git error.
- It must be **no older than the commit pinned in `vcpkg.json`**. vcpkg reads its version
  database from the working tree rather than from the pinned commit, so an older checkout fails
  with `no version database entry for <port> at <date>`. If you already have a vcpkg you have
  used for something else, update it and re-bootstrap:
  ```shell
  > git fetch
  > git checkout <the builtin-baseline commit from vcpkg.json>
  > ./bootstrap-vcpkg.bat
  ```
  The build checks this before doing anything and tells you the exact commands if it is behind.

That is the whole setup. There is no port to edit by hand, no patch to apply, and
**do not** run `vcpkg integrate install` — the repository disables the machine-wide
integration so that it always builds against its own pinned commit.

Everything else comes from `vcpkg.json` in the repository root, which is a
[manifest](https://learn.microsoft.com/vcpkg/consume/manifest-mode): it pins the vcpkg commit
(`builtin-baseline`) and lists the libraries, and the build restores them on demand into
`vcpkg_installed\<triplet>`.

ffmpeg has to be built with a specific set of decoders, so it is vendored as an
[overlay port](https://learn.microsoft.com/vcpkg/concepts/overlay-ports) in
`Libraries\vcpkg-ports\ffmpeg`, taken from the vcpkg registry with the `--enable-*` list applied
on top. It takes precedence over whichever ffmpeg version the pinned commit happens to carry.

TDLib is built from the same manifest and the same installed tree, so the openssl and zlib it
links are the ones the app ships.

### TDLib
In order to communicate with Telegram servers, Unigram uses TDLib. It comes as a submodule and is
built by `Libraries\tdjson\build.ps1`, which exports `tdjson.dll`, its dependencies and
`td_api.tl` into `Libraries\tdjson\<arch>` — the paths the app copies from.

Two extra tools are needed for the code generation step:

- **CMake** 4.4 or later, on PATH. Earlier versions have no Visual Studio 18 generator and will
  silently fall back to an older toolset.
- **PHP**, with `php.exe` on PATH.

Then, from `Libraries\tdjson`:

```shell
> powershell -ExecutionPolicy ByPass ./build.ps1 -arch x64,ARM64
```

The script picks up `VCPKG_ROOT` and builds against the manifest in the repository root, so
openssl and zlib are the same builds the app links. You can choose to build both `x64` and
`arm64` or just the architecture you need.

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
