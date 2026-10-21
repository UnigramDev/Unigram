# Duplicated libraries

Measured 2026-08-14, while moving libvlc and webrtc to vcpkg; libvlc has since been removed.
Nothing here is a bug; it is a
list of things the app carries more than one copy of, with the evidence, so the cost is known
rather than rediscovered.

## ffmpeg — one copy since libvlc went, 2026-10-20

| copy | form | size |
|---|---|---|
| vcpkg `ffmpeg` 7.1.2 (overlay port) | `avcodec-61.dll` and friends, linked by both native projects | ~8 MB |
| VLC's contrib ffmpeg, **removed with libvlc** | statically inside `plugins\codec\libavcodec_plugin.dll` | 17.3 MB |

WebRTC also carries ffmpeg **headers** (`third_party/ffmpeg`, referenced by a few webrtc headers),
but nothing is compiled in: `lib /list webrtc.lib` finds zero ffmpeg objects, because the fork is
built without `rtc_use_h264`. So it is not a second copy.

Until libvlc was removed, two independent decoder stacks parsed untrusted media, each with its own
CVE surface. The media player now decodes through the vcpkg build too (`notes/ffmpeg-media-player.md`).

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

## libyuv from vcpkg had no SIMD — built with clang-cl since 2026-10-20

Stock vcpkg builds libyuv with MSVC, which prints during the build:

> You are using MSVC to compile libyuv. This build won't compile any of the acceleration codes,
> which results in a very slow library. See <https://github.com/microsoft/vcpkg/issues/28446>

Until the overlay port, every libyuv conversion in `Telegram.Native` ran scalar code, while the same
operations inside `webrtc.lib` were accelerated, because that copy is built with clang.

**Why.** libyuv's SIMD is GCC-style inline assembly: `row_gcc.cc` for x86, `row_neon64.cc` for
ARM64. MSVC on x64 gets four SSSE3 rows (I422/I444 to ARGB, plus the alpha variants) and nothing
else. MSVC on ARM64 gets no NEON at all, because it is gated on `__aarch64__`, which MSVC does not
define.

**Who calls it.** The media player's `MediaVideoRenderer` and the editor's `VideoTranscoder`.
`VideoAnimation` has a libyuv branch too, but it is dead in practice: it is taken only when no
swscale context exists, and one is created whenever the destination is 16-byte aligned, which every
`malloc`ed frame buffer is. GIFs and video stickers are converted, and resized, by swscale.

**Measured 2026-10-20, x64.** Microseconds per frame, comparing the shipped MSVC `libyuv.dll` with
the overlay port's, built from the same source (`d98915a6`):

| caller: call | 720p MSVC | 720p clang-cl | 1080p MSVC | 1080p clang-cl |
|---|---|---|---|---|
| player `Upload`, 8-bit software decode: `I420ToNV12` | 80 | 31 | 184 | 92 |
| player `Upload`, 10-bit software decode: `P010ToNV12` | 289 | 60 | 658 | 140 |
| player `DrawSoftware` fallback: `I420Scale` to half size | 157 | 14 | 361 | 51 |
| editor export: `ARGBToNV12` | 1,096 | 175 | 2,505 | 422 |

ARM64 was not measured, and should gain more, since there the MSVC build has no SIMD at all.

**Decided (Fela, 2026-10-20): build libyuv with clang-cl, and require Visual Studio's Clang
component for it.**
- **Overlay port.** `Libraries/vcpkg-ports/libyuv` builds the same libyuv commit with the clang-cl
  bundled with Visual Studio (22.1 at the time), linked by `link.exe`. On ARM64 the NEON, SVE and SME
  sources now compile, and the DLL doubles to 485 KB. Every dotprod or i8mm instruction in it sits inside a `_DotProd` or `_I8MM`
  row, which libyuv picks only after `IsProcessorFeaturePresent`; on Windows that reports NEON and
  DotProd only, so SVE, SME and I8MM rows are never selected. ARM64 is built but not yet run.
- **`VideoAnimation`** was meant to get a one-pass conversion, dropping its trailing `ARGBToBGRA`.
  That is moot while the branch is dead. Whether libyuv should take the frames that need no resizing,
  instead of swscale, is still undecided.
- **libjpeg-turbo.** The stock port depends on it only for MJPEG decoding, which the app never calls.
  That dependency was the sole reason `jpeg62.dll` and `turbojpeg.dll` shipped, not ffmpeg as listed
  below. The overlay port drops it.

## Worth deciding later

- Whether `avdevice`, `avfilter`, `dav1d` and `vpx` need to ship at all — they are installed by
  vcpkg as dependencies of ffmpeg, and nothing observed imports them directly. `avfilter` is linked since the media
  player's phase 1. `avfilter` and `avdevice` in particular are 0.1 MB and 0.0 MB, so this is tidiness
  rather than size.
- Whether `Telegram.Native.Calls` still needs `swscale`: it is on the link line but the built
  binary does not import it.
