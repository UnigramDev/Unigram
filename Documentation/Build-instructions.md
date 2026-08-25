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

That is the whole setup. **Do not** run `vcpkg integrate install`: the repository disables the
machine-wide integration so that it always builds against its own pinned commit.

Everything else comes from `vcpkg.json` in the repository root, which is a
[manifest](https://learn.microsoft.com/vcpkg/consume/manifest-mode): it pins the vcpkg commit
(`builtin-baseline`) and lists the libraries, and the build restores them on demand into
`vcpkg_installed\<triplet>\<triplet>` — one install root per architecture, the triplet repeated
because vcpkg creates its own `<triplet>` folder inside the root it is given.

ffmpeg has to be built with a specific set of decoders, so it is vendored as an
[overlay port](https://learn.microsoft.com/vcpkg/concepts/overlay-ports) in
`Libraries\vcpkg-ports\ffmpeg`, taken from the vcpkg registry with the `--enable-*` list applied
on top. It takes precedence over whichever ffmpeg version the pinned commit happens to carry.

TDLib is built from the same manifest and into the same roots, so the openssl and zlib it links
are the ones the app ships.

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
> powershell -ExecutionPolicy ByPass ./build.ps1
```

The script finds vcpkg exactly as the rest of the build does — it asks MSBuild to evaluate
`Directory.Build.props` — and builds against the manifest in the repository root, so openssl
and zlib are the same builds the app links. Both architectures are built by default; pass
`-arch x64` or `-arch ARM64` for one of them.

### LibVLC and WebRTC

LibVLC plays video and audio, and WebRTC backs calls and video chats. Both arrive as prebuilt
binaries through the same manifest as everything else, as overlay ports in
`Libraries\vcpkg-ports`. The build downloads an archive for the architecture it is building,
verifies it against a SHA512 recorded in the port, and caches it — so there is nothing to install
or configure for either.

They are built from [UnigramDev/vlc](https://github.com/UnigramDev/vlc) and
[UnigramDev/webrtc-uwp](https://github.com/UnigramDev/webrtc-uwp); the WebRTC fork follows
[WinRTC](https://github.com/microsoft/winrtc/tree/master/patches_for_WebRTC_org/m84). Each
release names the commit it was built from.

To change either one, see [UnigramDev/deps](https://github.com/UnigramDev/deps), which holds the
build scripts, the patches and the packaging, and documents how to publish a new archive.

### Code fails to build?

If the code fails to build make sure to create a [new issue](https://github.com/UnigramDev/Unigram/issues/new?assignees=&labels=needs-triage&template=anything-else.md&title=) or to open a [pull request](https://github.com/UnigramDev/Unigram/compare).
