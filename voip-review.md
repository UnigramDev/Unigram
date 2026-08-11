# VoipManager / VoipGroupManager review — to-do

Line-by-line review of `Telegram.Native.Calls/VoipManager.{h,cpp}` and
`Telegram.Native.Calls/VoipGroupManager.{h,cpp}`, cross-checked against the C# call
sites in `Telegram/Services/Calls/` and against tgcalls' own expectations.

Line numbers are as of `c533f04c0`, i.e. **before** the P0 commits below; anything still
open has shifted by a few lines since.

Legend: **[live]** = confirmed reachable from current app code · **[latent]** = correct
today only by convention or because no caller exercises it.

**Build:** `msbuild Telegram.Native.Calls\Telegram.Native.Calls.vcxproj /p:Configuration=Debug
/p:Platform=x64 /m` with VS 18 Community. Takes a few minutes and links clean; the C4715
and C4702 warnings it prints are pre-existing.

---

## P0 — memory safety and security — **done**

All five landed on `develop` as `4eda2af98..c4bbb77f6`, one fix per commit, built x64 Debug.

- [x] **Unchecked `get_self` on a `VoipCaptureBase`** — `VoipManager.cpp:215-219` **[live]**
      → `4eda2af98`

  `Start` does `get_self<implementation::VoipVideoCapture>(descriptor.VideoCapture())`
  with no `try_as`. `get_self` reinterprets the ABI pointer, so a `VoipScreenCapture`
  reads `m_impl` at the wrong offset. Reachable because `VoipCall._camera` holds either
  kind (`VoipCall.cs:832`) and is passed straight into the descriptor
  (`VoipCall.cs:680`) — start screen sharing before the call reaches `Ready`.

  Fixed: the `try_as` chain became `GetVideoCaptureImpl` in `VoipScreenCapture.h`, used by
  all four call sites (both managers, `Start` + `SetVideoCapture`).

- [x] **E2E: a null delegate silently transmits plaintext** — `VoipGroupManager.cpp:409-435` **[live]**
      → `d200580d0`

  `OnE2EEncryptDecrypt` returns the input unchanged when `m_encryptData` is null, and
  hands raw ciphertext to the media stack when `m_decryptData` is null. Two windows:
  `SetEncryptDecrypt` runs *after* construction (`VoipGroupCall.cs:297`, `:748`), and
  `SetEncryptDecrypt(null, null)` runs before `Stop()` (`VoipGroupCall.cs:787`).

  Fixed: returns an empty vector, which is what `FrameTransformer::Transform`
  (`GroupInstanceCustomImpl.cpp:1454`) treats as "drop this frame" — and what the managed
  `EncryptData`/`DecryptData` already return when TDLib fails (`VoipGroupCall.cs:414`).

  The window is now closed too, see P2 below — and it was **teardown-only**, not
  construction. Every construction site installs the delegates synchronously before
  anything can move media, so the "delegates are set after construction" half of this
  finding was wrong.

- [x] **`persistentState` out-of-bounds write** — `VoipManager.cpp:72-79` **[latent]**
      → `fef656e87`

  Empty vector indexed by `[i]`. Nothing in C# sets `PersistentState` today, so it never
  fires — but it is heap corruption the moment someone wires it up. Also re-fetches
  `descriptor.PersistentState()` twice per byte.

  Fixed with `vector_to_unmanaged`. Note nothing reads the state back —
  `getPersistentState` is still commented out at `VoipManager.cpp:363` — so the property
  could just as well be deleted.

- [x] **`CLoopbackCapture` held by value** — `VoipGroupManager.h:81` → `c4bbb77f6`

  It is a ref-counted WRL `RuntimeClass`, and its `METHODASYNCCALLBACK` sub-objects
  forward `AddRef`/`Release` to it while MF work items hold those refs. Storage died with
  `VoipGroupManager` regardless of the refcount.

  Fixed: `ComPtr<CLoopbackCapture>` via `Make<>`, created only on the screencast path.
  That also removed the `StopCaptureAsync` every non-screencast manager was making.

- [x] **Loopback start failure crashed on stop** — `LoopbackCapture.cpp` → `d4f761270`

  Originally filed as "`Stop()` calls `MFShutdown()` process-wide". **That part was wrong**
  — `MFStartup` is called from `InitializeLoopbackCapture` (`LoopbackCapture.cpp:65`) and
  the two are paired 1:1, which is the contract MF expects.

  What was real underneath it: `OnStopCapture` dereferenced `m_AudioClient` unconditionally,
  while `StopCaptureAsync` accepts the error state that a failed activation leaves behind
  and where no client was ever created — a null deref on exactly the machines where process
  loopback is unsupported. And the `MFStartup` was left dangling whenever the start failed,
  since `StopCaptureAsync` is the only thing that paired it; the destructor now does.

  Still open, deliberately not touched: `StopCaptureAsync` blocks the calling thread on
  `m_hCaptureStopped.wait()` with no timeout, and `Stop()` runs on the UI thread. → P2.

## P1 — user-visible bugs

- [x] **Remote video sink registered over and over** — `VoipManager.cpp:271-278` **[live]**

  Originally filed as "the null passed by `VoipCall.SetRemoteVideoOutput` is ignored, so
  the sink is never detached". **Wrong on both halves.** tgcalls' `setIncomingVideoOutput`
  takes a `weak_ptr` and `VideoSinkImpl::addSink` *appends*, pruning entries only as they
  expire (`InstanceV2Impl.cpp:710-741`) — so a null sink was never how you detach, and
  passing one through would just append a dead entry. Detaching already works, via
  `VoipVideoOutputSink::Stop` dropping the `shared_ptr` (`VoipPage.xaml.cs:943`).

  The real bug is the reverse: `OnRemoteMediaStateChanged` re-sets the *same* output on
  every state change where video is active (`VoipPage.xaml.cs:210-215`), and each call
  appends the same sink again, so one frame is rendered N times — at 30fps, N times the
  D2D and Composition work.

  Fixed by remembering the last sink and skipping a repeat. The field is a `weak_ptr` on
  purpose: it exists only to recognise a repeat, and a strong one would keep the sink
  alive and break detach-by-expiry.

  **Corrected on a second pass** — the first version early-returned on a null sink, which
  was wrong twice over. It left `m_incomingVideoOutput` pointing at a sink the caller had
  just asked to detach, and it swallowed the one thing a null *is* good for:
  `setIncomingVideoOutput` also assigns `_currentSink` (`InstanceV2Impl.cpp:2036`), which
  a newly negotiated video channel gets handed on creation (`:1512`). Never clearing it
  means a channel that appears after the detach re-attaches the old sink. Null now flows
  through as an empty `shared_ptr`, and the dedupe check handles it for free — two nulls
  in a row compare equal and do nothing.

  Not an issue for `VoipGroupManager::AddIncomingVideoOutput` — every caller there builds
  a fresh sink (`GroupCallPage.xaml.cs:2125`, `StoryContent.xaml.cs:844`).

- [x] **`done()` never latches `_done`, so a double deferral re-enters tgcalls** —
  `VoipGroupManager.h:128-148`, `:177-205`, `:234-242` **[live]**

  `OnMediaChannelDescriptionsRequested` called `args.Deferral(...)` on the null-participants
  path and fell through without returning (`VoipGroupCall.cs:913-918`) — so in practice it
  NREd on `participants.ToDictionary()` inside an `async void` before it ever got as far as
  deferring twice. Both halves fixed: `_done = nullptr` after firing in all three task
  impls, and the missing `return`.

- [ ] **`OnMediaChannelDescriptionsRequested` can double-add a description** —
  `VoipGroupCall.cs:952-962`

  When any ssrc was unknown, the second pass walks *all* of `AudioSourceIds` again and
  re-adds the ones the first pass already added. Harmless today only because tgcalls
  requests a single ssrc at a time, which the comment above it relies on.

  Note while here: tgcalls never calls `cancel()` on these tasks (no `cancel` call exists
  in `GroupInstanceCustomImpl.cpp`), so the `cancel()` overrides are dead code. What keeps
  a post-`Stop` deferral safe is that tgcalls' own completion lambdas capture a `weak_ptr`
  — worth a comment so nobody "simplifies" the null checks away.

- [x] **`GetDebugInfo` leaks and can return garbage** — `VoipManager.cpp:338-351`

  `malloc`'d `wlog` never freed, and the `MultiByteToWideChar` return went unchecked, so a
  failure returned an uninitialized buffer. The correct implementation was already sitting
  right below it as dead code — that is the C4702 the build used to warn about. Now the
  only line.

- [x] **`IsMuted` lies for screencast** — `VoipGroupManager.h:83`, `.cpp:102-129`

  Field defaults to `true` and the screencast path called `m_impl->setIsMuted(false)`
  directly, leaving it stale. Now routed through the `IsMuted(bool)` setter.

  Left alone: `AudioProcessId == 0` still returns early and stays muted, as does a loopback
  that fails to start. That is the right meaning — a screencast with no audio to send *is*
  muted — and it now reports itself honestly.

- [x] **Uninitialized `qualityImpl`** — `VoipGroupManager.cpp:353-365`

  Switch had no `default:`; now initialised to `Thumbnail`. The `scale` switches at
  `:344-351` and `:378-385` also have no default, but 0 is the 1000ms case and tgcalls only
  ever passes one of the four listed periods, so those are left as they are.

- [x] **`AddIncomingVideoOutput` has no null guard** — `VoipGroupManager.cpp:181-188`

  `get_self` on a null sink then `implementation->Sink()` is a null deref. `VoipManager`'s
  equivalent already checked.

- [x] **TURN entry with an empty host** — `VoipManager.cpp:118-131`

  `pushStun` skipped empty hosts, `pushTurn` didn't, so a server with no IPv6 contributed
  a TURN entry pointing at nothing. Still open, deliberately: the reflector loop
  (`:141-158`) ignores `Ipv6Address` entirely, and `RtcServer::id` is `uint8_t` while
  `reflectorId` is a `ptrdiff_t`. → P4.

- [x] **`EncryptionKey` read with no null/size check** — `VoipManager.cpp:81-88`

  Was 256 property calls plus 256 `GetAt` calls, and a short vector would have thrown
  `hresult_out_of_bounds` out of `Start`. One `GetMany` into the shared array, which clamps
  to what is there, and the redundant copy through a stack `std::array` is gone.

- [ ] **`RequestMediaChannelDescriptionTaskImpl::done` hardcodes `type = Audio`** —
  `VoipGroupManager.h:140` — **not a bug; decide whether to keep it that way**

  `VoipMediaChannelDescription` carries only `AudioSource` and `UserId`
  (`Telegram.Native.Calls.idl:67-71`), so audio-only is the shape of the whole API rather
  than a slip in this one line. It also looks correct: the path exists to resolve unknown
  *audio* ssrcs (`maybeRequestUnknownSsrc`), while video endpoints arrive through
  `SetRequestedVideoChannels`. Supporting video would mean extending the IDL and the
  managed side — a feature, not a fix.

- [x] **Empty-but-non-null broadcast part reported as `Success`** — `VoipGroupManager.h:185-197`

  Now `NotReady`, so tgcalls retries instead of decoding a zero-byte part. Took the double
  copy with it (the P3 item below) since it was the same five lines: `GetMany` straight
  into the destination instead of an iterator copy followed by a `memcpy`.

- [x] **`responseTimestamp` was only set on the `Success` path** — `VoipGroupManager.h:189-207`

  Found by re-reviewing the commit above, which is also what made it matter: `NotReady` is
  *precisely* the status tgcalls reads `responseTimestamp` on, to decide where to restart a
  stream that has not begun yet (`StreamingMediaContext.cpp:857-860`). It arrived as 0. The
  caller had been supplying it all along (`VoipGroupCall.cs:886`) and we dropped it.

  Two bugs in one line, because the field is also in **seconds** while every other timestamp
  in this API is milliseconds — tgcalls does `responseTimestamp * 1000.0` to get back to ms.
  We passed `UnixTimeMilliseconds` straight through, 1000x too large. Converted at the
  tgcalls boundary rather than changing the delegate, so the WinRT surface stays
  consistently milliseconds: `requestCurrentTime` genuinely is ms
  (`StreamingMediaContext.cpp:643`), so this one field was the odd one out, not the caller.

  **Verified on a live stream** by Fela, 2026-08-11 — so the seconds reading is right, and
  the `NotReady` + `segmentTimestamp == 0` startup path really does get exercised.

## P2 — lifetime and threading

- [x] **Close the E2E delegate window** — carried over from P0 → `5356ebabb`

  **There was no window at construction.** Checked every site: `297`, `352` and `748` all
  call `SetEncryptDecrypt` synchronously, on the same thread, before the `SetConnectionMode`
  / `EmitJoinPayload` that starts media (`:564`, `:765`) — and `:145`, the one construction
  that doesn't set them, is the non-conference call, where `IsConference` means the
  `e2eEncryptDecrypt` callback is never wired at all (`VoipGroupManager.cpp:69-84`).

  The real window was teardown: `SetEncryptDecrypt(null, null)` ran *before* `Stop()` in
  both paths (`:787` and `:1116`), so tgcalls was still live with no delegate installed.
  Fixed by reordering — the clear only exists to break the native to managed cycle, which
  works just as well after the instance is gone. Not compiled; C# side, statement reorder.

- [ ] **`DecryptData` ignores the data channel** — `VoipGroupCall.cs:421`

  It always passes `new GroupCallDataChannelMain()`, while `EncryptData` distinguishes
  screen sharing from main. Work out whether decrypting a remote screencast needs the
  other channel, or whether the receiving side genuinely only ever sees main.

- [x] **`StopCaptureAsync` blocks the caller with no timeout** — `LoopbackCapture.cpp:230`

  `m_hCaptureStopped.wait()` was unbounded and `VoipGroupManager::Stop()` runs on the UI
  thread, so a work queue that never drained hung the app. Gone with the rewrite below:
  `Stop` now takes the lock the sample callback holds, so it waits for one packet read at
  most and never for a work item that may never run.

- [x] **Rewrite `CLoopbackCapture` without WIL, C++/WinRT friendly** *(Fela)*

  Now `VoipLoopbackCapture` (`VoipLoopbackCapture.{h,cpp}`), matching how the rest of the
  project names things. What changed:

  - WRL `RuntimeClass` → `winrt::implements`, which brings agility with it, so `FtmBase`
    is gone too. `wil::com_ptr_nothrow` → `com_ptr`, `wil::unique_event` → `handle`,
    `wil::critical_section` → `slim_mutex`, and the `RETURN_IF_FAILED` macros → plain
    `if (auto result = …; FAILED(result))`.
  - The `METHODASYNCCALLBACK` macro and its four `offsetof`-derived callback objects are
    gone. Only the sample pump genuinely needs the MMCSS work queue, so start, stop and
    finish are now direct calls and there is one `IMFAsyncCallback` left.
  - No more `m_hCaptureStopped` handshake: a `slim_mutex` held across the sample callback
    is what `Stop` synchronises against.
  - The handler is a constructor argument rather than a settable sink, so it cannot be
    missing when the first packet lands.

  Three real bugs fell out of the rewrite, all in the packet loop:

  - The byte count came from `GetNextPacketSize` but the copy ran after `GetBuffer`, which
    is allowed to hand back a different frame count — an over-read whenever they differ.
  - `AUDCLNT_BUFFERFLAGS_SILENT` was ignored. A silent buffer's contents are undefined, so
    silence was being sent as whatever happened to be in memory. Now zeroed.
  - `m_samples` was invoked unguarded.

- [ ] **`AUTOCONVERTPCM` is passed as the periodicity argument** —
  `VoipLoopbackCapture.cpp`, in `OnActivated`

  `IAudioClient::Initialize` takes it in `StreamFlags`, but it sits in `hnsPeriodicity`,
  where `0x80000000` means a ~214s period and shared mode wants 0 regardless. Inherited
  verbatim from the Microsoft sample and preserved through the rewrite **on purpose** —
  moving it changes what the audio engine is asked for, and screen-share audio works
  today. Wants testing on a real capture, not reasoning. Left commented in place.

- [x] **Lock held across managed callbacks, and taken again by `add`/`remove`** —
  `VoipManager.cpp:415-458` vs `:463-571`; `VoipGroupManager.cpp:306-435` vs `:447-539`

  `winrt::event` synchronises itself, so `m_lock` only served to serialise callbacks
  against (un)subscription — at the price of a lock-order inversion with the C# side: the
  UI thread takes `_managerLock` then unsubscribes (`VoipCall.cs:803-814` → wanted
  `m_lock`), while a tgcalls worker held `m_lock` inside managed code that takes C# locks
  (`_stateLock`, `VoipCall.cs:507`). No handler closed the cycle, but any handler touching
  `_managerLock` would have.

  Gone from all 41 sites. `VoipManager` has no lock at all now; `VoipGroupManager` keeps
  one for the only state that is genuinely shared — the encrypt/decrypt delegates, written
  from the UI thread and read per frame from a media thread.

  The bigger win is what came off that lock: the managed `EncryptData` blocks on a TDLib
  round trip (`VoipGroupCall.cs:407-414`), and it used to do that while holding the same
  mutex as every audio-level update and every subscribe. The delegates are now copied out
  under the lock and invoked outside it.

  A comment on the event fields records why there is no lock, so it does not come back.

- [ ] **`m_impl` is not guarded** — both managers

  Every `if (m_impl)` is TOCTOU against `Stop()`'s `reset()`. Correct today only because
  all callers are on the UI thread under `_managerLock`. Deliberately *not* folded into the
  lock rework above: taking a lock around `m_impl` would put one back on the path that
  reaches managed code, which is what that change was undoing. Wants either the contract
  written down in the header or an atomic-swap teardown, not a mutex.

- [x] **No destructor on either manager**

  Destruction without `Stop()` destroyed the tgcalls instance without calling `stop()`, and
  left the loopback capture running. Both destructors call `Stop()`, which is idempotent.

- [x] **`VoipManager::Start` has no re-entrancy guard** — calling it twice destroyed the
  previous `Instance` without `stop()`.

  Now a no-op when one is already running, rather than a stop-and-restart: the instance
  in flight is serving the live call, and the second descriptor would be for the same one.
  Completes the Start/Stop lifecycle work the destructors began.

- [ ] **Verify `requestMediaChannelDescriptions` gating on `IsConference`** —
  `VoipGroupManager.cpp:69-83`

  The C# handler is subscribed unconditionally (`VoipGroupCall.cs:151`, `:296`, `:351`).
  For non-conference calls tgcalls falls back to synthesizing a description with
  `userId = 0` (`GroupInstanceCustomImpl.cpp:3453-3458`). Confirm that's intended before
  changing anything.

## P3 — performance on hot paths

- [x] **`OnE2EEncryptDecrypt` does ~2 COM calls per byte, per packet** —
  `VoipGroupManager.cpp:409-435`

  The return path rebuilt the result with a `push_back` loop over an `IIterator`, on every
  frame of a conference call. Now one `GetMany` into a pre-sized buffer. The lock it all
  used to run under is gone too, see P2.

  Still there, and not fixable from this side: the input is copied into a
  `single_threaded_vector` because the delegate takes an `IVector<byte>`, and the managed
  side then calls `ToArray()` on it (`VoipGroupCall.cs:407`) — two copies before TDLib
  even sees the frame. Removing them means changing the delegate signature in the IDL to
  pass a buffer the managed side writes into. → below.

- [ ] **The E2E delegate signature forces two copies per frame** — `VoipGroupManager.idl:15-16`

  `IVector<byte>` in and out, on a per-frame path, with a `ToArray()` on the managed side
  on top. Worth reshaping once someone is willing to touch the IDL and both call sites.

- [x] **`OnAudioLevelsUpdated` allocates per update** — `VoipGroupManager.cpp:313-329`

  One `Append` COM call per participant, ~10x/s for the whole call. Now filled into a
  reserved `std::vector` and handed over once via `single_threaded_vector(std::move(vec))`
  — which is what the `/*std::move(levels)*/` comment had been asking for.

  Correcting my own earlier note: there was no boxing per element. `VoipGroupParticipant`
  is an IDL struct, so the `IVector` holds it by value.

  The remaining `IVector` allocation per update is fixed by the event signature.

- [x] **`BroadcastPartTaskImpl::done` copies the payload twice** — `VoipGroupManager.h:187-191`.
  Done alongside the `NotReady` fix in P1.

- [x] **`ReceiveSignalingData` push_backs per byte with no reserve** — `VoipManager.cpp:367-379`.
  One `GetMany` into a pre-sized buffer.

- [x] **`SetRequestedVideoChannels` builds the vector even when `m_impl` is null** —
  `VoipGroupManager.cpp:273-302`. Early-out at the top, plus a `reserve`. It still walks
  `SourceGroups()`/`SourceIds()` through COM iterators, which is fine: it runs when the
  video layout changes, not per frame.

- [x] **`Protocol()`'s comparator takes `std::string` by value** — `VoipManager.h:34`.
  Now `const&`. The lexicographic sort it performed was a live bug, not a latent one —
  see P4.

- [ ] ~~**`RemoveSsrcs` iterator copy**~~ — `VoipGroupManager.cpp:173-179`. **Won't fix.**
  `GetMany` needs matching element types and this converts `int32_t` to `uint32_t`, so it
  would trade the COM calls for a second allocation. It runs when a participant leaves,
  with a handful of ssrcs — not a hot path, and the current form is the clearer one.

## How the loopback audio reaches WebRTC

Checked while rewriting the capture, since "we just inject bytes" sounded like the wrong
shape. It is not — the screencast path is already on the intended hook:

- A screencast instance **never opens the microphone**. `createAudioDeviceModule` branches
  on `VideoContentType::Screencast` and builds a `FakeAudioDeviceModule` over an
  `ExternalAudioRecorder` (`GroupInstanceCustomImpl.cpp:4392-4397`). The loopback capture
  *is* that instance's audio device.
- `addExternalAudioSamples` converts int16 → float and appends to `_externalAudioSamples`
  (`:3828`), and `ExternalAudioRecorder::Record` (`:958`) pulls 480 samples — 10ms of
  48kHz mono — off the front each time WebRTC asks. That is why the capture format is
  fixed at 1×48000×16.
- The `AudioCapturePostProcessor` that *mixes* external samples into the microphone
  (`:842-859`) is the **other** consumer, for the main instance. We never feed it.

So there is no better hook to move to; a custom `AudioDeviceModule` via
`createAudioDeviceModule` would just be reimplementing `FakeAudioDeviceModule`. What is
worth attention is the coupling, not the mechanism:

- [ ] **The buffer between the two clocks is unmanaged** — `GroupInstanceCustomImpl.cpp:3838`

  WASAPI pushes on its capture clock, WebRTC pulls 10ms at a time on its own. Nothing
  reconciles them: the buffer grows to 2 seconds and then drops from the *front*, so a
  consumer that falls behind gets a permanent 2s lag plus a discontinuity, not a
  resynchronisation. It is also the reason screen audio can drift out of sync with video
  over a long share. Worth measuring the steady-state depth before deciding anything.

- [ ] **Screencast audio is mono-only by construction** — `options.num_channels = 1`

  Fine for speech, lossy for music or a game. Widening it means the descriptor, the
  capture format and `ExternalAudioRecorder` all have to agree, so it is a tgcalls change
  as much as ours.

## P4 — hygiene, worth doing while we're in here

- [x] `EmitJoinPayload` (`VoipGroupManager.cpp:151-163`) invoked `completion`
      synchronously on the null-impl path and asynchronously otherwise. It no longer
      completes at all when there is no instance: an empty payload only got the caller as
      far as a join the server would reject.
- [x] `const auto RegisterTag = tgcalls::Register<...>()` at namespace scope in a header
      (`VoipManager.h:22-24`) — internal linkage, one registration per including TU. Moved
      into `VoipManager.cpp`, which is where a thing with internal linkage belongs.
- [x] The screencast group manager writes `tgcalls_screencast.txt` rather than sharing
      `tgcalls_group.txt` with the main one. Both still grow without bound; that needs a
      rotation policy, not a rename.
- [x] Dead `#ifndef _WIN32` branch inside the designated-initializer list —
      `VoipManager.cpp:51-57`. It could never have compiled; deleted.
- [ ] ~~No *remove* counterpart to `AddIncomingVideoOutput`~~ — **not a leak.** Same
      mistake as the P1 video-sink item: the group `VideoSinkImpl::OnFrame` prunes expired
      weak_ptrs exactly like the 1:1 one (`GroupInstanceCustomImpl.cpp:662-665`), so
      releasing the sink *is* the removal. Left open only as a reminder of the shape.
- [ ] `enableAEC`/`enableNS`/`enableAGC` hard-coded `true` in 1:1 calls
      (`VoipManager.cpp:46-48`) while group calls honour the `IsNoiseSuppressionEnabled`
      setting. **Needs your call** — it is a product decision, not a defect.
- [x] `Protocol()` sorted versions lexicographically. **Filed as latent; it was live.**
      I had assumed tgcalls' majors were single-digit. They are not — the registered set is
      `2.7.7 5.0.0 7.0.0 8.0.0 9.0.0 10.0.0 11.0.0`, and a string sort orders that
      `9 8 7 5 2.7.7 11 10`, putting the two newest protocols at the end of a list the
      comment says the server reads newest first. Now compared component-wise as numbers,
      checked against the real set plus the strict-weak-ordering properties.

---

## What is left

Every defect found in this review is fixed. What remains is decisions, one measurement,
and one thing that needs a device — nothing that can be closed by reading more code.

**Needs your call:**

- `enableAEC`/`enableNS`/`enableAGC` hard-coded in 1:1 calls while group calls honour the
  setting (P4).
- Whether `RequestMediaChannelDescriptionTaskImpl` should ever carry video, which means
  extending the IDL (P1).
- Whether to reshape the E2E delegate signature to stop copying every frame twice (P3).
- The duplicate-description pass in `OnMediaChannelDescriptionsRequested`, which is only
  safe today because tgcalls asks for one ssrc at a time (P1).
- Whether `DecryptData` should distinguish the screen-sharing channel the way `EncryptData`
  does (P2).

**Needs a device, not reasoning:**

- `AUTOCONVERTPCM` sitting in the periodicity argument (P2). Screen-share audio works
  today, so this is a "verify, then change" not a "change, then hope".

**Needs measuring first:**

- The unmanaged buffer between the WASAPI and WebRTC clocks — worth logging the
  steady-state depth over a long share before designing anything.

**Left deliberately, with the reasoning recorded above:** `m_impl` being unguarded (P2),
`RemoveSsrcs` (P3), unbounded log growth (P4).

Every item that was a defect and did not need a decision, a device or a measurement is
now fixed. Two of them were only found by re-reading commits already made — the
`responseTimestamp` gap and this version sort — so the pass over one's own work earned
its keep.
