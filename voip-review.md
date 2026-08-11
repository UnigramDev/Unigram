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

- [ ] **Remote video sink can never be detached** — `VoipManager.cpp:276-283` **[live]**

  `if (m_impl && sink)` silently ignores the null that `VoipCall.SetRemoteVideoOutput`
  passes to detach (`VoipCall.cs:290`). The old sink keeps receiving decoded frames and
  keeps its composition surface alive for the rest of the call.

  Fix: handle null like `SetVideoCapture` does. Check whether the same leak shows up in
  the leak instrumentation harness.

- [ ] **`done()` never latches `_done`, so a double deferral re-enters tgcalls** —
  `VoipGroupManager.h:128-148`, `:177-205`, `:234-242` **[live]**

  `OnMediaChannelDescriptionsRequested` calls `args.Deferral(...)` on the null-participants
  path and falls through without returning (`VoipGroupCall.cs:913-918`) — then NREs on
  `participants.ToDictionary()` inside an `async void`.

  Fix (native): set `_done = nullptr` after invoking, in all three task impls.
  Fix (managed): add the missing `return` at `VoipGroupCall.cs:916`.

  Note while here: tgcalls never calls `cancel()` on these tasks (no `cancel` call exists
  in `GroupInstanceCustomImpl.cpp`), so the `cancel()` overrides are dead code. What keeps
  a post-`Stop` deferral safe is that tgcalls' own completion lambdas capture a `weak_ptr`
  — worth a comment so nobody "simplifies" the null checks away.

- [ ] **`GetDebugInfo` leaks and can return garbage** — `VoipManager.cpp:338-351`

  `malloc`'d `wlog` never freed; `MultiByteToWideChar` return unchecked (failure leaves the
  buffer uninitialized); the correct implementation is already sitting there as dead code
  on the next line.

  Fix: `return winrt::to_hstring(m_impl->getDebugInfo());`

- [ ] **`IsMuted` lies for screencast** — `VoipGroupManager.h:83`, `.cpp:102-129`

  Field defaults to `true`; the screencast path calls `m_impl->setIsMuted(false)` without
  updating it. And `AudioProcessId == 0` returns early, skipping that call, so the mute
  state depends on whether audio sharing was requested.

  Fix: route through the `IsMuted(bool)` setter, and add a comment stating what a
  screencast manager's mute state is supposed to mean.

- [ ] **Uninitialized `qualityImpl`** — `VoipGroupManager.cpp:353-365`

  Switch has no `default:`. Same shape at `:344-351` and `:378-385`, where an unknown
  `period` silently means `scale = 0`.

- [ ] **`AddIncomingVideoOutput` has no null guard** — `VoipGroupManager.cpp:181-188`

  `get_self` on a null sink then `implementation->Sink()` is a null deref. `VoipManager`'s
  equivalent does check.

- [ ] **TURN entry with an empty host** — `VoipManager.cpp:118-131`

  `pushStun` skips empty hosts, `pushTurn` doesn't. Related: the reflector loop
  (`:141-158`) ignores `Ipv6Address` entirely, and `RtcServer::id` is `uint8_t` while
  `reflectorId` is a `ptrdiff_t`.

- [ ] **`EncryptionKey` read with no null/size check** — `VoipManager.cpp:81-88`

  256 property calls + 256 `GetAt` calls; a short vector throws `hresult_out_of_bounds`
  out of `Start`. Fix with `GetMany` into the array, plus a size check.

- [ ] **`RequestMediaChannelDescriptionTaskImpl::done` hardcodes `type = Audio`** —
  `VoipGroupManager.h:140`. Video channel descriptions can never be returned.

- [ ] **Empty-but-non-null broadcast part reported as `Success`** — `VoipGroupManager.h:185-197`.
  Should be `NotReady`.

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

- [ ] **`StopCaptureAsync` blocks the caller with no timeout** — `LoopbackCapture.cpp:230`

  `m_hCaptureStopped.wait()` is unbounded and `VoipGroupManager::Stop()` runs on the UI
  thread, so a work queue that never drains hangs the app. Carried over from P0.

- [ ] **Rewrite `CLoopbackCapture` without WIL, C++/WinRT friendly** *(Fela)*

  It is a near-verbatim copy of the Microsoft ApplicationLoopbackAudio sample: WRL
  `RuntimeClass`, `wil::com_ptr_nothrow`/`unique_event`/`critical_section`, and the
  `METHODASYNCCALLBACK` macro with its `offsetof`-based parent pointer. Everything else in
  this project is C++/WinRT, so it is the odd one out and the reason the two P0 fixes above
  had to be phrased in WRL terms.

  Worth pinning down before starting: whether the four MF async callbacks can become
  `winrt::implements` types (or one shared dispatcher), what replaces `wil::unique_event`
  (`winrt::handle` + `CreateEventW`, or an `IAsyncAction`), and whether the blocking waits
  in `ActivateAudioInterface` and `StopCaptureAsync` can become coroutines — which would
  also settle the item above.

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

- [ ] **`BroadcastPartTaskImpl::done` copies the payload twice** — `VoipGroupManager.h:187-191`

  Copies the `IVector` into `data`, then `memcpy`s into `bytes`. The first copy is waste.

- [ ] **`ReceiveSignalingData` push_backs per byte with no reserve** — `VoipManager.cpp:367-379`.
  Use `vector_to_unmanaged`.

- [ ] **`RemoveSsrcs` iterator copy** — `VoipGroupManager.cpp:173-179`. Same fix.

- [ ] **`SetRequestedVideoChannels` builds the vector even when `m_impl` is null** —
  `VoipGroupManager.cpp:273-302`. Also walks `SourceGroups()`/`SourceIds()` through COM
  iterators. Move the null check to the top.

- [ ] **`Protocol()`'s comparator takes `std::string` by value** — `VoipManager.h:34`.
  Two allocations per comparison. Also: lexicographic version sort breaks at a two-digit
  major.

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

P0 is done. Suggested order from here:

1. Remote video sink detach (P1) — likely visible in the leak harness.
2. `_done` latching + the missing `return` in `OnMediaChannelDescriptionsRequested` (P1).
3. `GetDebugInfo`, `IsMuted` for screencast, the missing `default:`, the null guard on
   `AddIncomingVideoOutput` (P1) — each a few lines, all independent.
4. The `m_lock` rework (P2) — one change that unblocks the E2E interop cost in P3.
5. Then the interop cost on the E2E and audio-levels paths (P3).

The `CLoopbackCapture` rewrite (P2) is its own piece of work and doesn't block any of these.
