# Duplicated libraries

Measured 2026-08-14, while moving libvlc and webrtc to vcpkg. Nothing here is a bug; it is a
list of things the app carries more than one copy of, with the evidence, so the cost is known
rather than rediscovered.

## ffmpeg — two copies ship, ~25 MB

| copy | form | size |
|---|---|---|
| vcpkg `ffmpeg` 7.1.2 (overlay port) | `avcodec-61.dll` and friends, linked by both native projects | ~8 MB |
| VLC's contrib ffmpeg | statically inside `plugins\codec\libavcodec_plugin.dll` | 17.3 MB |

WebRTC also carries ffmpeg **headers** (`third_party/ffmpeg`, referenced by a few webrtc headers),
but nothing is compiled in: `lib /list webrtc.lib` finds zero ffmpeg objects, because the fork is
built without `rtc_use_h264`. So it is not a third copy.

Two independent decoder stacks parse untrusted media, and each has its own CVE surface. Sharing
one ffmpeg between the app and libvlc is blocked by VLC's contrib build — see the earlier media
stack analysis; this is not new, and moving to vcpkg neither added nor removed a copy.

## opus and libyuv — two copies each

`Telegram.Native.dll` imports `opus.dll` and `libyuv.dll` from vcpkg. `Telegram.Native.Calls.dll`
imports neither, despite using both: it resolves them out of `webrtc.lib`, which bundles its own
static opus and libyuv. So each library exists twice in the shipped app, once as a DLL and once
inside the 339 MB static library.

This also means the two projects can be compiled against **different versions** of the same
library, which is worth remembering if behaviour ever differs between a call and a video.

## libyuv headers — the two copies are not interchangeable

**Confirmed by a build failure, 2026-08-14.** webrtc carries a *patched* libyuv whose
`ConvertToI420` takes three extra parameters:

```
webrtc's  ..., size_t sample_size, int src_stride_y, const uint8_t* src_uv,
               int src_stride_uv, uint8_t* dst_y, ...      19 arguments
vcpkg's   ..., size_t sample_size, uint8_t* dst_y, ...     16 arguments
```

`tgcalls/platform/uwp/UwpScreenCapturer.cpp:379` calls the 19-argument form. It includes plain
`<libyuv.h>`, so which library it gets is decided entirely by include order: `Telegram.Native.Calls`
must see webrtc's `third_party/libyuv/include` **before** the vcpkg include directory, which is
what `C:\webrtc\src\third_party\libyuv\include` used to do and what `Directory.Build.targets` now
does explicitly.

`Telegram.Native` deliberately keeps compiling against the stock headers, since it links
`libyuv.dll`. So the two projects compile against different, incompatible versions of the same
library by design, and any change to include order can silently move a file from one to the other.

The webrtc port therefore does **not** install `include/libyuv.h`: it conflicts with the `libyuv`
port outright, and installing it would decide this for every project at once.

## libyuv from vcpkg has no SIMD

vcpkg builds libyuv with MSVC, which prints during the build:

> You are using MSVC to compile libyuv. This build won't compile any of the acceleration codes,
> which results in a very slow library. See <https://github.com/microsoft/vcpkg/issues/28446>

This is not new — `Telegram.Native` has always linked the vcpkg build — but it means every libyuv
conversion in the app (video animation, thumbnails) runs scalar code, while the same operations
inside `webrtc.lib` are accelerated, because that copy is built with clang. Worth measuring before
deciding whether it matters.

## Worth deciding later

- Whether `avdevice`, `avfilter`, `dav1d`, `vpx`, `turbojpeg` and `jpeg` need to ship at all —
  they are installed by vcpkg as ffmpeg's dependencies, and nothing observed imports them
  directly. `avfilter` and `avdevice` in particular are 0.1 MB and 0.0 MB, so this is tidiness
  rather than size.
- Whether `Telegram.Native.Calls` still needs `swscale`: it is on the link line but the built
  binary does not import it.
