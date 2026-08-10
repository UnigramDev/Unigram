# webrtc-uwp fork — review of the Microsoft-added files

Line-by-line review of the 22 files **added** by the tip commit `33bed7021d`
("UWP + tgcalls patches") in `c:\webrtc\src`, i.e. the UWP capture module and the
Media Foundation H.264 codec that are not in upstream WebRTC.

Severity: **A** = user-visible failure, **B** = latent correctness/robustness, **C** = hygiene.

Status: `open`, or the commit that fixed it on branch `m123-uwp-review-fixes` in the fork.

---

## modules/video_capture/windows/video_capture_winrt.cc (763)

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 1 | ~509 | A | `get_VideoFormat`'s HRESULT was discarded; a non-video source (depth/IR) returns success with a null format and every read below dereferenced it. Now captured plus an explicit null check. | fixed 685d0ac7 |
| 2 | ~488 | B | Every local in `FrameArrived` was uninitialised. With a plane count other than 2 the UV descriptor was never filled, so `size_y`/`stride_uv` were read from uninitialised stack and `size_y` became the UV plane offset. All locals now zero-initialised, so a 1-plane frame yields offset 0 (a valid pointer the fourcc-driven converter never reads) instead of garbage. | fixed cfbe5cdc |
| 3 | ~160 | B | `StartCapture` overwrote `media_frame_reader_` and `media_source_frame_arrived_token` without stopping a previous reader. Reachable because `is_capturing` is only set at the *end* of the internal `StartCapture`, so an attempt that fails after `add_FrameArrived` leaves a live handler that the public wrapper's `CaptureStarted()` check will not clean up. Now stops the previous reader first. | fixed 5b2b422d |

Remaining lines of this file not yet reviewed in full — only the paths reachable from
the reported crash stacks were read closely.

---

## modules/video_coding/codecs/h264/win/utils/sample_attribute_queue.h (69)

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 4 | 38–40 | B | `pop`'s `entry.first > id` branch reports success, returns **another frame's** attributes, and pops nothing. Combined with the encoder's `size() <= 2` gate (`h264_encoder_mf_impl.cc:455`, which is what admits new frames), a persistent run of output timestamps below the queue front would wedge the queue at 3 entries: `FromVideoFrame` is never called again and every subsequent frame is dropped, i.e. outgoing video freezes for the rest of the call. `OnH264Encoded` carries the comment "This must be done even if the frame is discarded later, or the queue will clog"; this is the branch that clogs it. **Trigger not proven:** timestamps are monotonic from `startTime_`, and `ReleaseWriter` resets the base and clears the queue together, so the plausible trigger is stale in-flight outputs after a reconfigure — which is transient and self-heals once they drain. Worth fixing anyway because the wedge is unrecoverable and a cap costs nothing. Suggested: return `false` for `>` and bound `push`. | open |
| 5 | 66 | C | `std::queue<std::pair<uint64_t, const T>>` — the `const` element makes the pair non-assignable and blocks moves. | open |

---

## modules/video_coding/codecs/h264/win/utils/crit_sec.h (57)

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 6 | 21 | B | `InitializeCriticalSectionEx` return value ignored. It fails under low memory, leaving the section uninitialised; every later `EnterCriticalSection` is then undefined behaviour. Relevant because the crashes being chased happen precisely under memory exhaustion. | open |
| 7 | 16–32, 42–55 | B | Neither `CritSec` nor `AutoLock` suppresses copy. A copied `AutoLock` unlocks twice; a copied `CritSec` double-deletes the section. | fixed fa8232a7 |
| 8 | 18 | C | `m_criticalSection` is public. | open |

---

## modules/video_coding/codecs/h264/win/utils/utils.h (33)

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 9 | 15–30 | B | `ON_SUCCEEDED` expands to a bare `if` with no `do { } while (0)` wrapper, so `if (c) ON_SUCCEEDED(x); else y;` silently binds `else` to the macro's inner `if`. Used pervasively across these files. | fixed 323ae74d |

---

## modules/video_coding/codecs/h264/win/utils/async.h (66)

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 10 | 62–63 | C | `m_pParent` / `m_pInvokeFn` are public. | open |
| 11 | 56 | C | The `m_pParent != nullptr` guard in `Invoke` is dead — nothing ever nulls it, so a destroyed parent dangles rather than being caught. Lifetime is actually held by the AddRef delegation; the check reads as protection it does not provide. | open |

---

## modules/video_coding/codecs/h264/win/encoder/h264_encoder_mf_impl.{h,cc} (119 + 705)

Reviewed so far: header, ctor/dtor, `HeightToEncode`, `InitEncode` entry, `FromVideoFrame`,
`ReleaseWriter`, the `_sampleAttributeQueue` call sites. Rate control, `InitWriter`,
`ReconfigureSinkWriter` body and `OnH264Encoded`'s tail still to do.

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 12 | .cc 83–92 | B | `HeightToEncode` has no `default` and no return after the switch, so a `frame_height_round_mode` outside 0..2 falls off the end of a non-void function. The global is explicitly documented as settable by user code ("User code should extern-declare and set this"). Unreachable today — nothing in tgcalls or the app assigns it, so it stays `kFrameHeightNoChange`. | fixed 3567da9b |
| 13 | .cc 388–393 | B | `FromVideoFrame` returns `sample` unconditionally, even when `hr` failed partway through building it. `Encode` then writes that partially built sample to the sink writer instead of dropping the frame. | open |
| 14 | .cc 366–376 | B | The attribute push is inside `if (SUCCEEDED(hr))` while the sample is returned regardless, so a failure at `SetSampleDuration` produces a sample with no queue entry — its later `pop` misses and `OnH264Encoded` discards the encoded frame. Same root as #13. | open |
| 15 | .h 79–98 | B | `max_bitrate_`, `width_`, `height_`, `frame_rate_`, `target_bps_`, `max_qp_`, `mode_`, `frame_dropping_on_`, `key_frame_interval_`, `next_frame_rate_`, `next_target_bps_` have no initialiser, unlike their neighbours which use `{}`. Anything reading them before `InitEncode` completes reads indeterminate values. | fixed aa109157 |
| 16 | .h 22 | C | `#include "H264_media_sink.h"` — the file is `h264_media_sink.h`. Compiles only because Windows filesystems are case-insensitive. | open |
| 17 | .cc 70–73 | C | The constructor captures `MFStartup`'s HRESULT into `hr` and then discards it; the encoder proceeds as if Media Foundation had started. | open |
| 18 | .cc 539 | **A** | `for (uint32_t i = 0; i < sendBuffer.size() - 5; ++i)` — `size()` is unsigned, so any sample of 1..4 bytes makes `size() - 5` wrap to ~2^64 and the NAL scan walks off the end of the heap buffer. `curLength == 0` is handled immediately above (with a "Got empty sample." warning, so degenerate samples do occur in practice), but 1..4 is not. This is an out-of-bounds read in the encoder output path and a candidate for the `VoipException: ACCESS_VIOLATION` groups. Suggested: `if (sendBuffer.size() > 5)` around the scan, or iterate with `size_t` and compare `i + 5 < size()`. | fixed d8725a66 |
| 19 | .cc 521–524 | C | The comment "sendBuffer is not copied here" is wrong — `EncodedImageBuffer::Create(data, size)` allocates and copies. Harmless today, but the comment describes a contract under which `sendBuffer` going out of scope would leave `encodedImage` dangling. | open |
| 20 | .cc 251 / 478 / 590 | B | `framePendingCount_` is incremented under `crit_` (478), decremented under `callbackCrit_` (590) and reset under `crit_` (251). Two different mutexes guarding one non-atomic int is a data race. | fixed c345d2a0 |
| 21 | .cc 537–574 | C | The NAL scan is almost entirely dead: every `fragmentationHeader` statement is commented out since that type left the WebRTC API. What survives is the key-frame detection, which `MFSampleExtension_CleanPoint` (checked just above) normally already provides — so every encoded frame pays a byte-by-byte scan of its whole buffer for a fallback boolean. This is also what makes #18 sting, since the loop barely earns its place. | open |
| 22 | .cc 572 | C | `i += 5` inside a loop whose header already does `++i` advances by 6, and `prefixLengthFound` is 3 or 4 — so the scan can step over a following start code and miss an IDR NAL, weakening the very fallback the loop exists for. | open |

---

## modules/video_coding/codecs/h264/win/encoder/h264_media_sink.{h,cc} (102 + 281)

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 23 | .cc 216–231 | **A** | `Shutdown()` sets `isShutdown_ = true` **only inside** `if (SUCCEEDED(hr) && outputStream_ != nullptr)`. A sink that never got a stream sink — `AddStreamSink` not called, or it failed — is therefore never marked shut down, yet `Shutdown` still returns `S_OK`. Every later call passes `CheckShutdown()` and proceeds against a sink the owner believes is dead. | fixed e4b535dd |
| 24 | .cc 241, 254, 278 | **A** | `OnClockStart`, `OnClockStop` and `RegisterEncodingCallback` dereference `outputStream_` with no null check — `CheckShutdown()` only tests the flag. Five other sites do check it (53, 74, 107, 148, 221), so the omission is inconsistent rather than deliberate. Directly reachable via #23, and `RegisterEncodingCallback` is additionally reachable on its own: the encoder calls it right after `AddStreamSink`, so if that failed this is an immediate null dereference. Another candidate for the `VoipException: ACCESS_VIOLATION` groups. | fixed 593fadf2 |
| 25 | .h 83–89 | B | `CheckShutdown()` reads `isShutdown_` without holding `critSec_`, and is called from methods that hold it and from `RegisterEncodingCallback` which does not. | open |
| 26 | .cc 216–231 | C | `Shutdown()` returns `S_OK` unconditionally, discarding the `MF_E_SHUTDOWN` that `CheckShutdown()` produces on a second call. Idempotence is reasonable, but combined with #23 it means a failed shutdown is indistinguishable from a successful one. | open |
| 27 | .h 21 | C | `#include "../Utils/crit_sec.h"` — the directory is `utils`. Same class as #16; compiles only on a case-insensitive filesystem. | open |
| 28 | .h 11 | C | Include guard is `THIRD_PARTY_H264_WINUWP_H264ENCODER_H264MEDIASINK_H_`, inconsistent with the `MODULES_VIDEO_CODING_...` convention the sibling files use. | open |
| 29 | .cc 22 | C | `~H264MediaSink` calls `OutputDebugString` on every teardown — debug tracing left in the shipping path. | open |

---

## modules/video_coding/codecs/h264/win/encoder/h264_stream_sink.{h,cc} (170 + 605)

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 30 | .cc 478–481 | **A** | `OnDispatchWorkItem` does `spState.As(&pOp);` then `pOp->GetOp(&op);` with both HRESULTs discarded. A failed QI leaves `pOp` null and the very next line dereferences it. Third instance of this exact pattern (see #1, #24). | fixed ad748131 |
| 31 | .cc 480 | B | `StreamOperation op;` is uninitialised and `GetOp`'s result is unchecked, so a failure leaves the following `switch` dispatching on an indeterminate value. | fixed ad748131 |
| 32 | .cc 196–199 | B | `ProcessSample` pushes onto `sampleQueue_` and *then* calls `QueueAsyncOperation`. If queueing the work item fails, the sample is already in the list with nothing scheduled to drain it — it stays until `Stop`/`Shutdown`. The error is returned to the caller but the queue is not unwound. Since draining is strictly one sample per work item, every such failure permanently adds an entry. | open |
| 33 | .cc 445 vs 527/601 | B | `encodingCallback_` is written under `cbCritSec_` in `RegisterEncodingCallback` (601) and read under `cbCritSec_` in the dispatch (527), but cleared under `critSec_` in `Shutdown` (445). Two different locks guard one pointer, and the racing pair is exactly shutdown against an in-flight `OnH264Encoded`. `H264EncoderMFImpl::ReleaseWriter` carries a comment about avoiding lock inversion between shutdown and this callback, so the hazard was known; the callback pointer itself was missed. | fixed f37cbd57 |
| 34 | .h 155 | B | `std::list<ComPtr<IUnknown>> sampleQueue_` has no bound. In steady state it is self-limiting (one push per work item, one pop per work item), so it is not the source of the multi-gigabyte growth being chased — these are encoded frames, not raw. It is unbounded only via #32. | open |
| 35 | .h 170 | C | Include guard `THIRD_PARTY_H264_WINUWP_H264ENCODER_H264STREAMSINK_H_`, same inconsistency as #28. | open |

---

## modules/video_coding/codecs/h264/win/decoder/h264_decoder_mf_impl.{h,cc} (64 + 579)

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 36 | .cc 72 | B | `ON_SUCCEEDED(*type_found = true);` — the macro expands to `hr = (act)`, so this assigns `hr = true`, i.e. **1**, and the function returns 1 instead of `S_OK`. `SUCCEEDED(1)` is true so callers mostly survive, but any `hr == S_OK` comparison fails. A direct demonstration of #9. | fixed de01a34d |
| 37 | .cc 59–77 | B | In `ConfigureOutputMediaType`, `output_media` is dereferenced (`GetGUID`) on the iteration after `GetOutputAvailableType` succeeded, with no null check; and the `while (true)` has no iteration bound, relying entirely on the transform eventually returning `MF_E_NO_MORE_TYPES`. | open |
| 38 | .cc 46 | C | `OutputDebugString` in the destructor, same as #29. | open |

**Checked and cleared:** `buffer_pool_(false, 300)` is copied verbatim from upstream
`libvpx_vp8_decoder.cc`, same value and same trailing comment — not a Microsoft-introduced
cap, so not a finding.

---

## modules/video_capture/windows/video_capture_winrt.cc — cleanup block

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 39 | 634, 640, 646, 652 | B | The `FrameArrived` cleanup does `x.As(&closable); closable->Close();` four times with the QI result discarded. All four WinRT types do implement `IClosable`, so it works today, but it is the same unchecked-QI-then-dereference shape as #1, #24 and #30 — four more instances of it, in the per-frame path. | fixed 15b67db4 |

**Checked and cleared:** the frame lifetime itself is correct — `IClosable::Close` is called on
the memory-buffer reference, bitmap buffer, software bitmap and frame reference, and no early
return can skip it, so the classic MediaFrameReference leak is not present.

---

## Scanned and cleared

- `device_info_winrt.cc:248` `frame_rate` and `:499` `device_count` — flagged by an
  uninitialised-locals scan, but both are assigned and read inside the same `SUCCEEDED` chain.
- No other `size() - N` unsigned-underflow sites beyond #18.
- No other `ON_SUCCEEDED(x = y)` macro misuse beyond #36.

## modules/audio_device/win/audio_device_core_win.{h,cc} (236 + 2514)

Rewritten wholesale by the tip commit (+1935 / -3639), so effectively all of it is fork
code. **Missed by the first pass**, which scoped itself to files the commit *added*
rather than ones it rewrote.

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 40 | .cc 1178, 1684 | **A** | Both `InitTransport` overrides log a failed `InitMixer()` as a warning and continue, then call `_audioClient->GetMixFormat(...)`. `InitMixer` is what creates `_audioClient`, and it returns -1 from three points before assigning it (activation wait, `GetActivateResult`, `QueryInterface`), so the `ComPtr` is still null. The guards in between check `GetDevice()`, not the client. Crash telemetry reports an access violation at `CaptureDeviceInternal::InitTransport` on this exact line. | fixed 5e71eea7 |
| 41 | .h 54 | B | `WaitForAsyncOperation` blocks up to `timeout_ms = 180000` — three minutes. It logs an error when the caller is not MTA ("Deadlocks might occur") but proceeds anyway, so an STA caller can stall for three minutes. | open |
| 42 | .h 94-98 | B | The `GetLastError() == ERROR_ALREADY_EXISTS` test runs in a separate block *after* the `shared_ptr` construction and its allocation, by which point the thread's last error may have been overwritten. The event is created unnamed, so `ERROR_ALREADY_EXISTS` cannot legitimately occur either — the check is both misplaced and unreachable. | open |
| 43 | .h 57 | C | `std::shared_ptr<HANDLE> event_completed_handle_ptr = NULL;` — initialising a smart pointer from `NULL`. | open |
| 44 | .cc 302-306 | **A** | `DeviceName` copied the device name into the `guid` buffer instead of `name`, leaving `name` holding only a terminator at the copied length, and did so inside the `name != nullptr` branch without testing `guid` — which the block below does test. Asking for a name with no guid dereferenced null. Both buffers are 128 bytes so there was no overflow. | fixed 2dcc25b6 |
| 45 | .cc 379-381 | **A** | `HSTRING audio_device_id = HStringReference(...).Get();` — the reference owns the header the HSTRING points into and was a temporary, so the handle dangled before `CreateFromIdAsync` used it on the next statement. The four other `HStringReference(...).Get()` sites pass it directly as a call argument, where the temporary lives to the end of the full expression, and are fine. | fixed 52b6de80 |
| 46 | .cc 720-756 | **A** | `VolumeIsAvailable`, `SetVolume` and `Volume` dereferenced `_simpleAudioVolume` behind only `TransportIsInitialized()`. It is obtained solely in the render-side `InitTransport`, so on a capture device it is always null — and these back the microphone volume API. The three mute methods in the same class already test it. | fixed 02040023 |
| 47 | .cc 610 | B | `TransportIsAvailable` returned `true` (1, a failure in this API) and never wrote its out parameter, so `PlayoutIsAvailable`/`RecordingIsAvailable` reported failure and handed back an uninitialised bool. | fixed 52b6de80 |
| 48 | .cc 497-502 | B | The destructor closed `_hThread` and three event handles unconditionally. `_hThread` is null before a transport starts and after `StopTransport` nulls it, and the events are null if `CreateEvent` failed. `CloseHandle(nullptr)` is an invalid-handle call, fatal under strict handle checking. | fixed 22b3b39e |
| 49 | .cc 939-942 | B | `syncBuffer = new BYTE[syncBufferSize];` followed by `if (syncBuffer == nullptr)`. Throwing `new` never returns null, so the check is dead; with exceptions disabled the allocation terminates the process instead. Directly relevant to the memory-pressure crashes. | open |
| 50 | .cc 938 | B | `syncBufferSize = 2 * (bufferLength * _audioFrameSize)` is computed in 32-bit with no overflow check, and both factors come from the device. | open |
| 51 | .cc 947-949 | B | `REFERENCE_TIME latency;` is uninitialised and `_audioClient->GetStreamLatency(&latency)`'s HRESULT is discarded, so a failure feeds indeterminate latency into `extraDelayMS`. `devPeriod`/`devPeriodMin` beside it are initialised. | open |
| 52 | .cc 1129-1132 | B | The `Exit:` handler calls `_audioClient->Stop()` with no null check, while the block above it and the block below it both test the pointer first. | open |
| 53 | .cc 567-580 | B | `InitMixer` calls `punkAudioInterface->QueryInterface(...)` without checking the pointer `GetActivateResult` produced. `ComPtr::CopyTo`'s result is discarded there too, so a null interface with a success HRESULT reaches the dereference. | open |
| 54 | .cc 690-700 | B | When the start handshake times out, `StopTransport` closes `_hThread` and nulls it while the thread may still be running, leaving it free to touch members of an object that is on its way out. | open |
| 55 | .cc 317-322 | C | `CCompletionDelegate::_completedEvent` is created in a member initialiser with no failure check, then closed unconditionally in the destructor. | open |
| 56 | .cc 92 | C | `WaitForASyncWithEvent` null-checks `async_info` but dereferences `event_completed_handle_ptr` without the same treatment. | open |
| 57 | .cc 305 | C | `_TraceCOMError` passes `wchar_t` to `::isspace`, whose argument must be representable as `unsigned char` or EOF; `iswspace` is the wide-character form. | open |
| 58 | .cc 211 | C | `uint16_t _deviceIndex = -1;` — narrowing a signed sentinel into an unsigned member. | open |
| 59 | .cc 1274, 1805 | **A** | Both `InitTransport` overrides assign `_audioFrameSize`, `_sampleRate` and `_blockSize` only inside `if (hr == S_OK)` after the format search, then called `IAudioClient::Initialize` regardless — which in shared mode can succeed with a convertible format. The transport then came up with `_blockSize` at 0, and the capture thread computes `(10 * syncBufIndex) / _blockSize`, an **integer** divide by zero, and loops `while (syncBufIndex >= _blockSize)` subtracting nothing. | fixed c419936d |
| 60 | .cc 2065-2069 | **A** | `Init` creates both helpers with `new (std::nothrow)` and checked neither, while constructing the capture helper from `&_pRenderDeviceHelper->_sndCardDelay` — so a failed render allocation is dereferenced on the next statement, and `_initialized` was set either way. Choosing nothrow only helps if the null is handled. | fixed d2931560 |
| 61 | .cc 2092-2093 | **A** | `Terminate` deleted both helpers without clearing the pointers, and the public API reaches through them without consulting `_initialized`, so anything called between `Terminate` and a fresh `Init` used freed memory. The destructor calls `Terminate`, so late teardown calls were in the window too. | fixed 4a0e5b7c |
| 62 | .cc various | B | 54 public methods dereference `_pCaptureDeviceHelper` / `_pRenderDeviceHelper` with no null or `_initialized` check. #61 turns the resulting use-after-free into a deterministic null dereference, which is better but still a crash; a single accessor that returns -1 when uninitialised would close it properly. | open |
| 63 | .cc 1449 | B | The render `Transport()` calls `_audioClient->GetBufferSize` with no null check, while the capture `Transport()` explicitly tests `_audioClient == nullptr` first and logs "input state has been modified before capture loop starts." | open |
| 64 | .cc 1404-1405 | B | `_deviceSampleRate` and `_deviceBlockSize` have no initialiser and the `RenderDeviceInternal` constructor does not set them; `endpointBufferSizeMS` divides by `_deviceBlockSize`. | open |
| 65 | .cc 1661 | B | The render `Exit:` handler calls `_audioClient->Stop()` unguarded, same as the capture side (#52), while the blocks either side of it test the pointer. | open |

**Read in full.** The async helper, `DeviceHelper`, `AudioDeviceHelper`, both `Transport()`
threads, both `InitTransport` overrides, and the `AudioDeviceWindowsCore` wrapper. The wrapper's
remaining ~250 lines are one-line forwarders with no logic of their own beyond #62.

---

## modules/video_capture/windows/device_info_winrt.{h,cc} + help_functions_winrt.{h,cc} (804 + 714)

Read in full. Markedly better written than the codec files: consistent `SUCCEEDED` chaining
with every HRESULT captured, bounds-checked buffers, `unique_ptr` for scratch memory.

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 66 | help 280 | B | `ToVideoType` rejected any subtype longer than 8 characters, but four of its own table entries are 38-character GUID strings (I420, YUY2, IYUV, YV12). Those rows were unreachable, so a device reporting a GUID subtype was classified `kUnknown`. | fixed 2d3a0339 |
| 67 | dev 720 | B | `MultiByteToWideChar(..., deviceIdW, sizeof(deviceIdW))` — the last argument counts wide characters, not bytes, so the call claimed twice the real capacity. The `strnlen` bound above is what prevents an overrun today. | fixed de8f912a |
| 68 | dev 731-735 | B | `realloc`'s result was assigned straight over `_lastUsedDeviceName` and then written through: on failure that is a null `memcpy` destination, and the old block leaks. Under memory pressure. | fixed de8f912a |
| 69 | dev 385-425 | B | `FillVideoCaptureCapabilityFromDeviceWithoutProfiles` opens a `MediaCapture` and only calls `Close()` on the success path. Any earlier failure releases the ComPtr without closing, leaving the camera held open — user-visible as the in-use indicator staying on and later opens failing. | fixed acbda5e8 |
| 70 | help 322-327 | B | `GetMediaCaptureWithInitSettingsUWP` waits `INFINITE` for the UI thread to run the dispatched initialisation. No timeout at all, so a busy or stalled dispatcher hangs the caller permanently. **Note:** the dispatched lambda captures locals by reference, so a timeout cannot simply be added — an early return would leave the UI thread writing to destroyed stack objects. The two have to be fixed together. | open |
| 71 | help 222-227 | B | A second `INFINITE` wait, this one for the user to answer the camera permission prompt. A user who never answers blocks the calling thread forever. | open |
| 72 | help 400 | B | `CreateMediaCaptureInitializationSettings` always requests `StreamingCaptureMode_AudioAndVideo`, including when enumerating cameras. Camera enumeration therefore also requires microphone consent, and fails if the user granted only the camera. | fixed 33a7c67e |
| 73 | help 344 | C | `SafelyComputeMediaRatio` does not null-check its only parameter, despite the name. Both call sites pass a pointer already validated by a `SUCCEEDED` chain. | open |
| 74 | help 290-291 | C | `{L"RGB565", VideoType::kRGB565}` appears twice in the format table. | open |
| 75 | dev 254, 358 | C | `reinterpret_cast<UINT32*>(&video_capture_capability.width)` type-puns a signed member through an unsigned pointer. | open |

**Checked and cleared:** the `double frame_rate` and `uint32_t device_count` locals flagged by
the earlier scan are both assigned and read inside the same `SUCCEEDED` chain; `GetDeviceName`
bounds every conversion and handles `WideCharToMultiByte` failure.

---

## modules/video_coding/codecs/h264/win — encoder rate control, factory

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 76 | enc .cc 651-698 | B | `ReconfigureSinkWriter` is documented "must be called under crit_ lock" and both call sites hold it, yet it calls `ReleaseWriter()` and `InitWriter()`, which each take `crit_` again. **Not a deadlock on this build:** `WEBRTC_WIN` without `WEBRTC_ABSL_MUTEX` selects `mutex_critical_section.h`, and `CRITICAL_SECTION` is re-entrant. It is a latent trap all the same — `webrtc::Mutex` is documented as non-recursive, and building with `WEBRTC_ABSL_MUTEX` (absl::Mutex actively detects re-entry) would deadlock the encoder on the first rate adaptation. | open |
| 77 | enc .cc 694, 699 | B | `ReconfigureSinkWriter` discards `InitWriter()`'s return value and unconditionally returns `WEBRTC_VIDEO_CODEC_OK`, so a failed re-initialisation is invisible and leaves the encoder with no sink writer. The caller's `if (FAILED(res))` at line 432 is consequently dead code. | open |
| 78 | enc .cc 616 | B | `SetRates` reads `sinkWriter_ == nullptr` before taking `crit_`, then acts on it after. `ReleaseWriter` resets that pointer under the lock, so the check can pass and the pointer be cleared before `ReconfigureSinkWriter` runs. | open |
| 79 | enc .cc 226-233 | B | When `InitWriter` fails partway, `mediaSink_` and `sinkWriter_` keep whatever was created and `inited_` stays false. A later `InitWriter` calls `MakeAndInitialize<H264MediaSink>(&mediaSink_)` over the top without shutting the old sink down — the same shape as #3 in the capture module. | open |
| 80 | enc .cc 231 | C | On failure `InitWriter` returns a raw `HRESULT` from a function whose contract is a `WEBRTC_VIDEO_CODEC_*` code, so the specific value is meaningless to callers (it is at least non-zero). | open |
| 81 | fac .cc 63-70 | B | The factory advertises four H.264 variants including `packetization-mode=0`, but `CreateVideoEncoder` ignores `format` and always returns the same encoder, which hardcodes `NonInterleaved` (mode 1) in `OnH264Encoded`. A peer that negotiates mode 0 receives mode-1 packetisation. | open |
| 82 | fac .cc 91-92 | C | `auto test = builtin_video_decoder_factory_->CreateVideoDecoder(format); return test;` — leftover temporary. | open |

**Checked and cleared:** the members I default-initialised in `aa109157` (`width_`, `height_`,
`max_qp_`, `target_bps_`, `frame_rate_`) are all assigned by `InitEncode` at lines 117-139
before `InitWriter` reads them at 212, so that change cannot alter live behaviour.

---

## Remaining files — stream sink internals, decoder output, capture wrapper

| # | Line | Sev | Finding | Status |
|---|------|-----|---------|--------|
| 83 | ss .cc 48 | B | `spSink_ = reinterpret_cast<IMFMediaSink*>(pParent)` — `H264MediaSink` inherits `IMediaExtension`, `FtmBase`, `IMFMediaSink` and `IMFClockStateSink`, and `reinterpret_cast` skips the base offset, so the pointer addressed the wrong vtable. `AddRef`/`Release` survived because `IUnknown` occupies slots 0-2 of every COM vtable, but `GetMediaSink` hands the pointer to Media Foundation. `static_cast` needs the definition, which the TU lacked — that is why the original could not use it. | fixed d5f0b742 |
| 84 | vc .cc 714 | B | `MultiByteToWideChar(..., device_id_w, sizeof(device_id_w))` in `VideoCaptureWinRT::Init` — character count, not bytes; the twin of #67. The converted string is never read, so nothing depended on it. | fixed c4aecd94 |
| 85 | ss .h 33-39 / .cc 413-420 | B | `ValidStateMatrix` is declared `[State_Count][Op_Count]` with `Op_Count == 5`, but every initialiser row lists only four entries, so the `OpPlaceMarker` column is silently zero-filled to `FALSE`. Harmless today because `PlaceMarker` is the one operation that never calls `ValidateOperation`; adding that call would reject every marker. | open |
| 86 | dec .cc 336-355 | **A** | The NV12 to I420 conversion trusts the output buffer's size completely: it checks only `cur_length > 0`, then reads `src_data + buffer_width * buffer_height` for the UV plane and converts a full frame. A short sample is read out of bounds. Same shape as #18 in the encoder. The visible-area width/height taken from `MF_MT_MINIMUM_DISPLAY_APERTURE` are likewise not validated against the buffer dimensions. | open |
| 87 | ss .cc 371-382 | B | `GetMajorType` reads `spCurrentType_` without holding `critSec_`, while every other method on the class takes it; `SetCurrentMediaType` replaces that pointer under the lock. | open |
| 88 | vc .cc 705 | B | `SetDeviceUniqueId` uses throwing `new char[]`; with exceptions disabled an allocation failure terminates rather than returning an error. Same class as #49. | open |
| 89 | ss .cc 27-33, 48 | B | The stream sink holds a strong reference to its parent (`spSink_`) while the parent holds one to it (`outputStream_`). The cycle is broken only by `H264StreamSink::Shutdown` resetting `spSink_`, so any path that drops the sinks without shutting down leaks both objects and the serial work queue with them. Relates to #23. | open |
| 90 | ss .cc 335-343 | C | Nested `if (SUCCEEDED(hr))` inside a block already guarded by the same condition. | open |

---

## Not fully reviewed line by line

Reviewed by targeted pattern scan (unchecked HRESULT then dereference, uninitialised locals,
unsigned underflow, macro misuse, lock mismatches, missing null checks) rather than statement
by statement. A full read of these could still turn up logic errors the scan cannot see:

- `encoder/h264_encoder_mf_impl.cc` — rate control, `InitWriter`, `ReconfigureSinkWriter` body
- `encoder/h264_stream_sink.cc` — state matrix, event queue plumbing
- `decoder/h264_decoder_mf_impl.cc` — the decode loop and output handling
- remainder of `video_capture_winrt.cc`
- **`audio_device/win/audio_device_core_win.cc` — ~2,500 of 2,514 lines**
- `audio_device/win/audio_device_core_win.h` — beyond the async helper
- **`modules/audio_device/win/audio_device_core_win.cc` — 2,500 of 2,514 lines**
- `modules/audio_device/win/audio_device_core_win.h` — beyond the async helper
