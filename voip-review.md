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
  purpose: a strong one would keep the sink alive and break detach-by-expiry.

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

- [ ] **Lock held across managed callbacks, and taken again by `add`/`remove`** —
  `VoipManager.cpp:415-458` vs `:463-571`; `VoipGroupManager.cpp:306-435` vs `:447-539`

  `winrt::event` is already thread-safe, so `m_lock` only serialises callbacks against
  (un)subscription — at the price of a lock-order inversion with the C# side: the UI
  thread takes `_managerLock` then unsubscribes (`VoipCall.cs:803-814` → wants `m_lock`),
  while a tgcalls worker holds `m_lock` inside managed code that takes C# locks
  (`_stateLock`, `VoipCall.cs:507`). No current handler closes the cycle; any handler
  touching `_managerLock` would.

  Fix: drop `m_lock` from the add/remove overloads, and snapshot the event before invoking
  it outside the lock. In `VoipGroupManager` this also gets the per-packet E2E path off the
  same mutex as event subscription (see P3).

- [ ] **`m_impl` is not guarded by `m_lock`** — both managers

  Every `if (m_impl)` is TOCTOU against `Stop()`'s `reset()`. Correct today only because
  all callers are on the UI thread under `_managerLock`. Decide: either document that
  contract in the header, or guard `m_impl` properly (a separate mutex from the event one).

- [ ] **No destructor on either manager**

  Destruction without `Stop()` destroys the tgcalls instance without calling `stop()`, and
  leaves the loopback capture running. Add a destructor that calls `Stop()`.

- [ ] **`VoipManager::Start` has no re-entrancy guard** — calling it twice destroys the
  previous `Instance` without `stop()`.

- [ ] **Verify `requestMediaChannelDescriptions` gating on `IsConference`** —
  `VoipGroupManager.cpp:69-83`

  The C# handler is subscribed unconditionally (`VoipGroupCall.cs:151`, `:296`, `:351`).
  For non-conference calls tgcalls falls back to synthesizing a description with
  `userId = 0` (`GroupInstanceCustomImpl.cpp:3453-3458`). Confirm that's intended before
  changing anything.

## P3 — performance on hot paths

- [ ] **`OnE2EEncryptDecrypt` does ~2 COM calls per byte, per packet** —
  `VoipGroupManager.cpp:409-435`

  `single_threaded_vector(std::vector(message))` copies the input, the C# delegate returns
  an `IVector`, and the result is rebuilt with a `push_back` loop over a COM iterator.
  Three copies plus per-element interop on every media packet of a conference call, all
  under `m_lock`.

  Fix: `GetMany` into a pre-sized buffer. Ideally change the delegate signature so the
  managed side writes into a buffer we own rather than returning an `IVector<byte>`.

- [ ] **`OnAudioLevelsUpdated` allocates per update** — `VoipGroupManager.cpp:313-329`

  A fresh `IVector` plus one boxed element per participant, ~10x/s for the whole call, each
  `Append` a COM call. The `/*std::move(levels)*/` comment says this was already spotted:
  build a `std::vector` and hand ownership over once via
  `single_threaded_vector(std::move(vec))`.

- [x] **`BroadcastPartTaskImpl::done` copies the payload twice** — `VoipGroupManager.h:187-191`.
  Done alongside the `NotReady` fix in P1.

- [ ] **`ReceiveSignalingData` push_backs per byte with no reserve** — `VoipManager.cpp:367-379`.
  Use `vector_to_unmanaged`.

- [ ] **`RemoveSsrcs` iterator copy** — `VoipGroupManager.cpp:173-179`. Same fix.

- [ ] **`SetRequestedVideoChannels` builds the vector even when `m_impl` is null** —
  `VoipGroupManager.cpp:273-302`. Also walks `SourceGroups()`/`SourceIds()` through COM
  iterators. Move the null check to the top.

- [ ] **`Protocol()`'s comparator takes `std::string` by value** — `VoipManager.h:34`.
  Two allocations per comparison. Also: lexicographic version sort breaks at a two-digit
  major.

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

- [ ] `EmitJoinPayload` (`VoipGroupManager.cpp:151-163`) invokes `completion` synchronously
      on the null-impl path and asynchronously otherwise — inconsistent threading for the
      caller.
- [ ] No *remove* counterpart to `AddIncomingVideoOutput`, so sinks accumulate for the
      call's lifetime as participants come and go.
- [ ] Fixed log file names (`VoipManager.cpp:35-36`, `VoipGroupManager.cpp:21-27`) —
      unbounded growth, and the main + screencast group managers write the same
      `tgcalls_group.txt` concurrently.
- [ ] `const auto RegisterTag = tgcalls::Register<...>()` at namespace scope in a header
      (`VoipManager.h:22-24`) — internal linkage, one registration per including TU. Only
      one TU includes it today; a second would silently double-register every version.
- [ ] Dead `#ifndef _WIN32` branch inside the designated-initializer list —
      `VoipManager.cpp:51-57`.
- [ ] `enableAEC`/`enableNS`/`enableAGC` hard-coded `true` in 1:1 calls
      (`VoipManager.cpp:46-48`) while group calls honour the `IsNoiseSuppressionEnabled`
      setting. Decide whether 1:1 should honour it too.

---

## Next up

P0 and P1 are done, apart from two items left open on purpose: the `type = Audio`
question above (a feature, not a fix) and the duplicate-description pass in
`OnMediaChannelDescriptionsRequested`.

Suggested order from here:

1. The `m_lock` rework (P2) — the one structural change, and it unblocks 3.
2. No destructor on either manager (P2) — small, and it pairs with the `m_lock` work since
   both are about what happens around `Stop`.
3. The interop cost on the E2E and audio-levels paths (P3), which is where the real
   per-frame waste is.
4. P4 whenever, though the header-scope `RegisterTag` is worth doing before anyone adds a
   second TU that includes `VoipManager.h`.

The `CLoopbackCapture` rewrite (P2) is its own piece of work and blocks none of these.
