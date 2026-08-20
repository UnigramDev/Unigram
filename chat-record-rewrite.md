# Voice/video message recording — rewrite plan

Findings from a read-through of `Telegram/Controls/Chats/ChatRecordButton.cs`,
`Telegram/Controls/Chats/ChatRecordBar.xaml{,.cs}`, both hosts (`Views/ChatView.xaml{,.cs}`,
`Controls/Stories/StoriesWindow.xaml{,.cs}`), the send path
(`ComposeViewModel.SendVoiceNoteAsync`/`SendVideoNoteAsync`, `GenerationService.TranscodeOpusAsync`),
the native encoder (`Telegram.Native/Opus/OpusOutput.{h,cpp,idl}`) and the TDLib file-generation
and upload machinery in `Libraries/tdlib/td/telegram/files/`.

Tasks are ordered so each one can land and be reviewed on its own. Check an item off in the
same commit as its fix.

---

## How it works today

One file, `ChatRecordButton.cs` (1355 lines), holds four things that never should have shared a
type:

1. **The button** — pointer capture, click modes, the press-and-hold vs tap-to-switch-mode timer,
   restriction toasts, accessibility.
2. **The state machine** — `_recordingAudioVideo`, `_recordingLocked`, `_recordingStopped`,
   `_recordingPaused`, `_calledRecordRunnable`, `_recordAudioVideoRunnableStarted`,
   `_enqueuedLocking`, `recordInterfaceState`, folded into `UpdateRecordingInterface()` (:324).
3. **The capture engine** — `Recorder` (:712) and `OpusRecorder` (:1235): `MediaCapture`,
   `LowLagMediaRecording`, `MediaFrameReader`, the amplitude meter and the waveform compressor.
4. **The send policy** — `Send()` (:1172) builds `VideoGeneration` and calls into the view model.

`ChatRecordBar` then drives the whole visual side off four `EventHandler`s from the button, and
reaches back into it (`ControlledButton.StopRecording`, `.LockRecording`, `.PauseRecording`,
`.IsViewOnce`) — so the two are mutually dependent and neither can be tested or reused alone.

The **voice** pipeline is: `MediaCapture` → `PrepareLowLagRecordToStorageFileAsync` with
`MediaEncodingProfile.CreateWav` (:1292) → a `.oga`-named file that actually holds **WAV** →
`InputVoiceNote` with `ConversionType.Opus` → at send time `GenerationService.TranscodeOpusAsync`
(:332) → `OpusOutput.Transcode` reads the WAV back off disk and encodes to Ogg/Opus.

The **video** pipeline is: `MediaCapture` → `MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto)`
→ full-resolution mp4 → `VideoGeneration` crops/scales/flips to
`Options.SuggestedVideoNoteLength` (384) at send time.

In parallel, a second reader (`MediaFrameReader`, :848) pulls float32 audio frames off the same
`MediaCapture` purely to drive the blob amplitude and accumulate the waveform.

Nothing is uploaded until the user releases the button, and for voice nothing is even *encoded*
until then.

---

## What's wrong

### The engine

- **The mic is recorded twice and encoded three times.** The frame reader already has every
  float32 sample in memory (:898) while `LowLagMediaRecording` writes the same audio to a WAV on
  disk, which is then read back and re-encoded at send time. `OpusOutput` already exposes
  `WriteFrame(AudioFrame)` (`OpusOutput.h:100`) — a streaming encode entry point that **nothing in
  C# calls**. The whole WAV round-trip is avoidable.
- **The waveform we compute is thrown away.** `GetWaveform()` produces a proper 5-bit/100-sample
  waveform, and `SendVoiceNoteAsync` sends `Array.Empty<byte>()` (`ComposeViewModel.cs:737`). Only
  the pause preview ever uses it.
- **…and it isn't computed at all under power saving.** `InitializeQuantumAsync` is gated on
  `PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths` (:812) — an *animation*
  setting deciding whether the *waveform data* exists.
- **The clock starts before the mic does.** `_start = DateTime.Now` is set from `RecordingStarting`
  (:370), which fires before `MediaCapture.InitializeAsync` and `PrepareLowLagRecordToStorageFileAsync`
  — several hundred ms of device init. The elapsed label counts time that was never recorded, and
  the sent duration comes from `MediaCaptureStopResult.RecordDuration` instead, so the two disagree.
  Then `(int)duration.TotalSeconds` (:1161) truncates, so a 5.9 s note ships as 5 s.
- **Video records at whatever the camera gives.** `CreateMp4(VideoEncodingQuality.Auto)` on a 1080p
  webcam writes a 1080p file for a 60-second clip, then re-encodes it to 384×384. Recording near
  the target size makes the send-time transcode nearly free.
- **The camera is picked by panel, not by the user.** `FindCameraDeviceByPanelAsync(Panel.Front)`
  (:785) ignores `MediaDeviceTracker`/`MediaDeviceList`, which calls already use for selection and
  hot-plug. There is no mic selection at all.
- **Mid-recording failure is a dead end.** `MediaCapture.Failed` only logs (:843). Unplug the mic
  and the UI stays "recording" forever; `RecordingFailed` is raised only from `Start`'s catch.
- **`RecordingTooShort` has no subscribers.** Release under 700 ms silently discards the recording
  with no feedback (:1152).
- **Video notes are not length-limited.** The bar draws a 60 s `SelfDestructTimer` ring
  (`ChatRecordBar.xaml.cs:182`) but nothing stops the recording when it fills.

### The lifetime

- **`Recorder.Current` is a `[ThreadStatic]` singleton** (:722) that every loaded `ChatRecordButton`
  on the thread subscribes to in `OnLoaded` (:162). ChatView and StoriesWindow both host one. A
  recording started in one drives `_recordingAudioVideo = true` in *all* of them, and
  `QuantumProcessed` is a single `Action` field (:169) — last one loaded wins, and the first one to
  unload nulls it out from under the other. `IsViewOnce` is likewise shared global state.
- **`SetControlledButton` never unsubscribes** (`ChatRecordBar.xaml.cs:141`) — five handlers,
  including `ManipulationDelta`, wired for the life of the bar with no teardown path.
- **`_recordingPaused` is never reset** when a recording ends (:401 resets the other two flags), so
  a pause in one recording leaks into the elapsed maths of the next.
- **`StopRecording(false)` is a trap.** It maps to `Stop(null, null)` (:633), and `Stop` neither
  deletes nor sends on a null `cancel` (:1140) — the temp file leaks; had it sent, `viewModel` is
  null and it would throw. No caller passes `false` today, which is the only reason it's invisible.

### The input model

- **Permissions are done the old way.** `CheckDeviceAccessAsync` (:562) uses
  `DeviceAccessInformation` plus a throwaway `MediaCapture` to trigger the consent prompt, and then
  **returns false unconditionally** (:590) — the first press after granting never records, it just
  primes the prompt. `MediaDevicePermissions.CheckAccessAsync` (`Common/MediaDevicePermissions.cs`)
  already does this properly with `AppCapability` and is what calls use.
- **Press-and-hold vs mode switch is a 300 ms race.** `_timer` (:128) plus `_calledRecordRunnable`
  and `_recordAudioVideoRunnableStarted` encode "did the hold outlive the tick" across three
  handlers (`OnClick`, `OnRelease`, `OnPointerCaptureLost`). It also means every recording is
  delayed 300 ms *before* device init even starts.
- **`Elapsed` uses `DateTime.Now`** — wall clock, so a DST or NTP step during a recording moves the
  timer.
- The `_timer.Tick` handler is an unremovable lambda (:130), against the project rule.

---

## Upload while recording — worth designing for, not worth blocking on

Not a must-have on desktop, so it sits at the end of the plan as its own task. It still belongs
*here*, in the architecture section, because it decides where the bytes go: if the sink writes to a
path we choose and the session owns the hand-off to the view model, streaming upload later is an
added sink, not a reshape. Design for it, ship without it.

**TDLib supports it, through file generation — verified in the vendored source.** The chain is:

1. `InputFileGenerated(originalPath, conversion, expectedSize)` → TDLib raises
   `UpdateFileGenerationStart` with a `DestinationPath` (under
   `LocalFolder\<sessionId>`, per `ClientService.cs:629` — so a `StorageFile` opens there fine).
2. The app writes bytes into that path and reports progress with
   `SetFileGenerationProgress(generationId, expectedSize, localPrefixSize)`.
3. `FileManager::on_partial_generate` (`files/FileManager.cpp:4956`) turns each report into a
   `PartialLocalFileLocation`, calls `run_upload` on the first one, and pushes every later one into
   the uploader via `FileUploadManager::update_local_file_location` (:4985).
4. `FileUploader::on_update_local_location` (`files/FileUploader.cpp:119`) accepts a *partial*
   location, takes the ready prefix, and inits its parts manager with an approximate size
   (`local_is_ready_ = false`) — i.e. it uploads the growing prefix.
5. `FinishFileGeneration` closes it out; the same `InputFile` then goes into `sendMessage`.

`run_upload` only does something if an upload is already pending, so the recorder has to kick one
with `PreliminaryUploadFile` at record start. Cancel is `CancelPreliminaryUploadFile` +
`FinishFileGeneration(error)`.

The app has **never** called `SetFileGenerationProgress` — every conversion in `GenerationService`
runs to completion and then reports. This is new plumbing, and it is the one genuinely new
subsystem in this plan.

**Ogg/Opus is safe to stream; mp4 needs proving first.** `OpusOutput` writes ogg pages
sequentially and never seeks backwards (`writeOggPage`, `OpusOutput.cpp:142`) — an uploaded prefix
can never be invalidated. Media Foundation's mp4 sink is a different matter: if it patches earlier
bytes when finalizing, every uploaded part before that point is garbage. So voice gets streaming
upload; video gets it only after a measurement says it's safe (Task 5).

---

## The shape to move to

One engine, two sinks — no AudioGraph, and voice and video differ only in what consumes the frames:

```
ChatRecordSession            state machine + policy. No XAML, no MediaCapture.
  ├─ ChatRecordEngine        MediaCapture: device init, permission, start/pause/resume/stop,
  │                          MediaFrameReader for audio (level + waveform + samples)
  │    ├─ VoiceSink          audio frames in, a file and a waveform out
  │    └─ VideoSink          audio+video in, a file out
  └─ hosts                   ChatRecordButton (input only), ChatRecordBar (render only)
```

Ogg/Opus and mp4 are each a sink's own business — the session, the engine and the hosts never name
a container or a codec.

`ChatRecordSession` is owned by the host (one per ChatView / StoriesWindow), not by a thread-static
singleton, and exposes a single observable state (`Idle | Starting | Recording | Paused | Stopping`)
plus `Elapsed`, `Level` and `Waveform`. The button raises intents (`RequestStart(mode)`,
`RequestLock`, `RequestStop(cancel)`); the bar renders state. Neither reaches into the other.

The session hands the finished value object (`inputFile`, exact `duration`, `waveform`, `mirrored`)
to the view model — the capture layer stops knowing about `ComposeViewModel`, `VideoGeneration` and
`MessageSelfDestructType`.

**Why not AudioGraph.** It would have given voice a cheaper, faster-starting engine, but it means a
second capture stack to keep alive, and the same `MediaFrameReader` we already run for the level
meter can hand us exactly the frames `OpusOutput.WriteFrame` wants. The WAV round-trip dies either
way, which was the actual win. (`SoundEffects` still uses AudioGraph; out of scope here, but if it
gets rewritten too, nothing in recording will depend on it.)

Two things must change about that reader for it to become the audio *source* rather than a meter:

- **`MediaFrameReaderAcquisitionMode.Realtime` (:869) drops frames under load.** Fine for a blob,
  catastrophic for a recording — it has to be `Buffered`, and the encode has to move off the
  callback thread.
- **The float32 requirement is a silent bail-out today.** `InitializeQuantumAsync` gives up if the
  source format isn't `MediaEncodingSubtypes.Float` (:860), which currently costs only the
  waveform. As the audio source it costs everything, so it needs `SetFormatAsync` (which
  `SharingMode.SharedReadOnly` forbids — see Task 5.1) or an int16 path through the sink.
- **The 64 ms throttle drops whole frames, and it drops them first.** `OnAudioFrameArrived` returns
  before touching the buffer (:906). Harmless for a meter; it silently discards audio the moment
  the same callback feeds the encoder. The throttle has to move off frame *acquisition* and onto
  level *reporting*.

### The blob keeps working — it gets better

It survives untouched, because nothing about how it's driven changes. `UpdateLevel`
(`CompositionBlobVisual.cs:141`) only writes two floats and a `MathF.Max`; every Composition call
happens in `OnRendering`, ticked by `CompositionVSync(30)` off `CompositionTarget.Rendering` on the
UI thread. So the capture thread posting levels is safe today and stays safe — the audio side and
the render side are already decoupled by that vsync, which is exactly why the reader can take on a
second job without the blob noticing.

What improves is the signal. Today the level and the waveform are both accumulated *only* from
frames that survive the 64 ms gate, so both are built from a decimated slice of the audio: the
`_micLevelPeakCount >= 1200` counter (:961) means "25 ms of audio" in principle, but those 25 ms
are drawn from a stream with most frames thrown away, so updates arrive in bursts several times
further apart than the 33 ms blob tick. Process every frame and the peak is a true peak over a
continuous window, landing at roughly one update per rendered frame.

Two knock-on moves:

- The reader currently only exists when `PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths`
  (:812). As the audio source it must always run — so that gate moves to the bar, which already
  tests the same condition to choose `StartAnimating()` vs `Clear()`
  (`ChatRecordBar.xaml.cs:226`). Skip the level *notification* when the blob isn't animating; keep
  accumulating the waveform regardless.
- Keep `QuantumProcessed?.Invoke(0)` on stop (:1132) — that's what settles the blob back down.

---

## Task 1 — Extract the state machine, no behaviour change — **done, unbuilt**

- [x] **1.1** Move the flag soup and `UpdateRecordingInterface` into `ChatRecordSession` with an
  explicit enum state and one transition method. Keep the existing engine (`Recorder`) behind it
  verbatim so the diff is mechanical and testable against the current behaviour.
- [x] **1.2** ~~One session per host, created by ChatView/StoriesWindow and passed to both the
  button and the bar.~~ The button owns one and exposes it — a button is already one per host, and
  this way no XAML or host code changes. Dropped `[ThreadStatic] Recorder.Current` and
  `Recorder.Release()` (`Navigation/WindowContext.cs:297`).
- [x] **1.3** Pair every `+=` in `SetControlledButton` with a `-=`, and replace the `_timer.Tick`
  lambda with a named method.
- [x] **1.4** Delete `StopRecording(bool)`'s null-`cancel` path: `Cancel()` and `Complete()` as two
  methods, no tri-state.

**How it landed.** `Telegram/Common/Recording/` gains `ChatRecordEngine` (the old nested
`Recorder`, moved out whole and no longer thread-static) and `ChatRecordSession`, which owns the
state, the clock and the engine. `ChatRecordButton` is down from 1355 lines to ~440 and holds the
gesture and its own visuals; `ChatRecordBar` is unchanged apart from the renamed calls and the
detach.

`ChatRecordState` has three values, not four. The old interface state 3 — locked, then stopped —
was only reachable through `StopRecording(false)`, which no caller ever passed. It went out with
the tri-state rather than being carried over.

**What the extraction fixed, beyond shape.**

- Two chats no longer share one recording. The engine was `[ThreadStatic]`, and every loaded button
  on the thread subscribed to it: a recording in a chat drove the state of the story composer too,
  `QuantumProcessed` was a single field so whichever loaded last owned the level meter, and the
  first to unload set it to null for the other.
- `Elapsed` is off `Environment.TickCount64` rather than `DateTime.Now`, so a clock change during a
  recording no longer moves it, and it stops while paused. It restarts when the device is actually
  open, so it no longer counts the few hundred ms of `InitializeAsync` — the residue between it and
  the sent duration is now the gap between the device opening and the first frame.
- The pause state is reset when a recording ends, so it can't leak into the next one.
- `ChatRecordBar` detaches from the button on unload.

## Task 2 — Voice: encode straight to Opus, no WAV — **done, unbuilt**

- [x] **2.1** Drop `LowLagMediaRecording` from the voice path. The `MediaFrameReader` becomes the
  source: `Buffered` acquisition, frames into `VoiceSink` (`OpusOutput.WriteFrame(AudioFrame)`) on
  a worker.
  - **Trap:** `WriteFrame` slices the buffer into `TG_OPUS_FRAME_SIZE` (960 sample) chunks and
    silently drops the remainder (`OpusOutput.h:110`). Whatever the device quantum is, the C# side
    must accumulate to a multiple of 960 or the tail of every buffer is lost.
  - Handle a non-float source format, per the note above.
- [x] **2.2** Retire `ConversionType.Opus` for recordings (the file is already Ogg). Keep
  `TranscodeOpusAsync` only if some other caller reaches it — check.
- [x] **2.3** Duration from the sample count, not `MediaCaptureStopResult`, and rounded rather than
  truncated. ~~Start the elapsed clock when the first frame arrives, on `Environment.TickCount64`.~~
  The *sent* duration is now exact; the elapsed label still counts from the button press, because
  `_start` lives in the state machine that Task 1 extracts.
- [x] **2.4** Send the waveform: `GetWaveform()` into `InputVoiceNote` (`ComposeViewModel.cs:737`),
  and compute it unconditionally — untie it from `PowerSavingPolicy.AreMaterialsEnabled`.
- [ ] **2.5** Mic selection through `MediaDeviceTracker`, including device-change handling
  mid-recording. **Blocked, and not on effort:** `MediaDeviceTracker` carries
  `// TODO: implement storage of chosen devices`, so there is no app-wide chosen microphone to
  follow — only the system default, which `MediaCapture` already uses. Wiring the tracker in
  today would spin up three `DeviceWatcher`s per recording to arrive back at the same device.
  This wants the device-preference storage first.
- [x] **2.6** Restructure `OnAudioFrameArrived`: every frame is encoded and folded into the
  waveform; only the `QuantumProcessed` notification is rate-limited. Move the animation gate to
  the bar. The blob must look the same or smoother — check it against a recording made before the
  change, not just "it still wobbles".

**How it landed.** Two new classes under `Telegram/Common/Recording/`. `VoiceSink` owns the
`OpusOutput`, buffers samples to whole 960-sample frames and counts what it encoded — that count is
the duration. `AudioWaveform` folds the same samples into the 100-bucket waveform and the blob
level. `Recorder` picks the path after the device is initialized: `PrepareReaderAsync` reports
whether the frame source can be encoded as it stands, and only then is there a sink.

**The fallback is not optional.** The encoder is 48kHz mono and `MediaFrameSource` hands over
whatever the endpoint runs at. `SetFormatAsync` is attempted, but if the source stays at 44.1kHz —
or reports a subtype that isn't Float or PCM, or a depth that isn't 32 or 16 — the recording goes
back through `MediaCapture` and `ConversionType.Opus`, which resamples for us. Wrong-speed audio is
not a trade worth making, and I have no device survey to say how common that is. Both paths now
carry a waveform.

**A bug fixed on the way.** The old accumulator never reset `_currentPeak` after storing a bucket
(`ChatRecordButton.cs:932` before this change), so each bucket held the running maximum of the whole
recording and the waveform could only ever climb. Telegram's own implementation resets it. Nothing
showed, because the waveform was discarded at send time anyway.

**Two more, while in there.** `PauseAsync` never set its `TaskCompletionSource` when the recorder
was already gone, so the caller awaited forever; and the temporary file was created with the default
collision option, so two recordings in the same second threw.

**Not verified on a device.** The pure logic — framing, ordering, padding, the waveform, the level
cadence — is covered by a throwaway test project and passes. Everything touching `MediaCapture` is
read-and-reasoned only. The two things most worth watching on first run: whether `Buffered`
acquisition really delivers every frame through `TryAcquireLatestFrame`, and which path the log
says it took.

## Task 3 — Permissions and start-up

- [x] **3.1** Replace `CheckAccessAsync`/`CheckDeviceAccessAsync` (:534–613) with
  `MediaDevicePermissions.CheckAccessAsync`, so the first press after granting actually records.
  Keep the Xbox special case if it's still needed — verify. *Kept, and still unverified: the note
  it came from was about `DeviceAccessInformation`, not `AppCapability`, so it may well be dead
  weight. Cheaper to keep than to be wrong about, and it is three lines with a comment saying so.*
  `MediaDevicePermissions` grew a `MediaDevicePurpose`, so the denial keeps the recording wording
  (`PermissionNoAudio`) instead of borrowing the call one.
- [x] **3.2** ~~Start the engine on press and discard on an early release, instead of waiting 300 ms
  to learn whether it was a hold.~~ **Built, tested, and rejected — see below. Don't try it again.**
- [x] **3.3** `MediaCapture.Failed` must fail the session and reset the UI (:843), not just log.
  It now cancels the recording and raises `RecordingFailed`, so unplugging the microphone ends the
  recording instead of leaving the bar up forever.
- [x] **3.4** Give `RecordingTooShort` a consumer — a toast, matching the Android/desktop wording.
  *No new string: it shows `HoldToAudio`/`HoldToVideo`, the same hint the mode-switch tap shows,
  which is what Android says when a press is too short to be a recording.*

**Why 3.2 can't be had.** The session grew a second phase — a `Start(commit: false)` that opened
the device silently, a `Commit()` that made the recording exist, a `Cancel()` that threw the warm-up
away — so that device init ran underneath the 300 ms timer instead of after it. It worked, and it
is not worth having: every tap that switches mode opens a device the user never asked to use. That
is not a detail the app gets to decide is harmless. Windows puts the app in the camera's and the
microphone's recently-accessed list on the open, and hardware with a privacy LED lights it — so
switching from video to voice turns the webcam on for a moment.

Nor is it a flicker that could be tuned away. `Cancel` goes onto the same single-slot queue as
`Start`, so a release at 120 ms interrupts nothing: `InitializeAsync`, the frame reader and the
file all finish first, and only then does the teardown run. The device is fully open and streaming
before the cancel is even looked at.

Warming only the microphone was considered and turned down for the same reason: an indicator that
says a device is in use when it isn't is wrong whichever device it is. What survives the revert are
the two things that never needed the warm-up — the flags the timer already answered for, and the
press that carried on recording after being let go mid-check.

## Task 4 — Video

- [x] **4.1** Record near the target: ~~pick the camera format closest to~~
  `Options.SuggestedVideoNoteLength` and set the encoding profile to it, so the send-time
  `VideoGeneration` is a crop, not a full re-encode. This is what makes release-to-sent fast for
  video, streaming upload or not.

  **The camera's format is never touched.** `SharedReadOnly` forbids that, but it doesn't forbid
  *reading* it, and the encoding profile decides what lands in the file regardless. So the profile
  is derived from the camera's own aspect — `VideoDeviceController.GetMediaStreamProperties`,
  scaled so the short side is the video-note length, rounded to even. A 1080p camera records
  682×384 instead of 1920×1080, and there is no letterbox or stretch to get wrong because the
  aspect is the one the camera gave us. Never upscales, and falls back to `Auto` if the encoder
  refuses the size. Decision 1 turned out not to gate this after all.
- [ ] **4.2** Camera selection through `MediaDeviceTracker` instead of `Panel.Front` (:785).
  Blocked with 2.5, and worse: Windows has no default-camera concept to fall back on, so without
  a stored preference there is nothing to select *by*.
- [x] **4.3** Enforce the 60 s limit the `SelfDestructTimer` ring already promises — stop and send
  when it fills. `ChatRecordSession.MaximumVideoDuration` is the one definition; the ring in the
  bar reads it too, so the drawing and the cut-off can't drift apart. The check runs off `Elapsed`
  rather than a deadline, so pausing pauses the limit.
- [ ] **4.4** Preview: `CaptureElement` (`ChatRecordBar.xaml.cs:158`) pins `MediaCapture` to the UI
  thread and forces the "last frame" to go through `RenderTargetBitmap` + a PNG on disk
  (`SaveLastFrameAsync`). With a frame reader in play for 4.1 anyway, the preview can be a
  Composition surface — mirroring, the round crop and the last-frame grab all become free.
- [ ] **4.5** `_mirroringPreview` decides both the preview transform *and* the encoded flip
  (:1210). Confirm that a non-mirrored external camera still sends the right way round — the
  preview is hard-coded to `ScaleX = -1` (`ChatRecordBar.xaml.cs:165`) regardless.
- [ ] **4.6** At sixty seconds, stop capturing but show the pause UI rather than sending. This is
  what the official apps do, and 4.3 sends instead. The bar already has the state to show — its
  public `Pause()` draws the waveform, the duration and the send glyph — so the limit becomes a
  pause the user did not ask for, and the recording waits to be sent or thrown away. A recording
  that is still held by the pointer has to lock first, or the release that follows would send it.
  **There is no resuming from it**: sixty seconds is all a video message gets, so `PauseRoot` has
  to go rather than sit there showing its checked glyph. That makes it a different end state from
  a pause the user asked for, not the same one reached another way — `Pause()` cannot simply be
  called and left alone.

## Task 5 — The bar and the leftovers

- [ ] **5.1** Decide the sharing mode. `MediaCaptureSharingMode.SharedReadOnly` (:1267) forbids
  both `SetFormatAsync` on the audio frame source (Task 2.1) and stream properties on the camera
  (Task 4.1). Exclusive mode buys both and costs failing when another app holds the device.
- [x] **5.2** Reset the pause state on stop (:401) and stop `Elapsed` advancing while paused.
  Both belong to `ChatRecordSession` now.
- [ ] **5.3** The lock/pause/view-once visuals are ~250 lines of hand-built keyframe pairs that
  duplicate each other forward and backward (`Pause_Click`, both branches). Fold into one
  parameterised helper.
- [ ] **5.4** `Visibility` toggling plus a `Popup` plus expression animations bound to
  `root.Size` — check the whole bar against `layout-cycle-audit.md` while it's open.

## Task 6 — Streaming upload — *optional, after the rest works*

Nice to have, not required. It depends on Task 2 (a sink we control byte-for-byte) and is the only
part of this plan that can fail on someone else's terms, so it goes last and can be dropped without
touching anything above it.

- [ ] **6.1** New conversion type in `GenerationService` that doesn't transcode — it registers the
  `DestinationPath` with the live session and keeps the generation open. Needs a rendezvous for the
  case where `UpdateFileGenerationStart` arrives after the mic already produced frames (buffer, or
  hold the first frames).
- [ ] **6.2** At record start: `PreliminaryUploadFile(InputFileGenerated(...), FileTypeVoiceNote)`
  with an estimated `expectedSize` (~6 KB/s at the encoder's 48 kbps), then
  `SetFileGenerationProgress` on a cadence — every ogg page flush is too chatty, once or twice a
  second is enough.
- [ ] **6.3** On release: `FinishFileGeneration`, then send with the same `InputFile`. On cancel:
  `CancelPreliminaryUploadFile` + `FinishFileGeneration(error)` + delete.
- [ ] **6.4** The cases that make this fiddly, all of which need deciding before coding:
  offline/failed upload at release; the user paused (the file stops growing but the upload is
  live); recording discarded under 700 ms; app suspended mid-recording; a second recording started
  before the first upload finished. Every one of them has to degrade to today's
  upload-on-release behaviour rather than lose a recording.
- [ ] **6.5** Verify against a known answer: record a fixed 10 s tone, confirm the uploaded voice
  note plays back identically to the same file sent the old way. A prefix-upload bug produces a
  file that is *almost* right, which is exactly the failure mode that survives a casual test.
- [ ] **6.6** Video, only if 6.1–6.5 land: record an mp4 with `LowLagMediaRecording`, hash the
  first N bytes at intervals during the recording, hash the same ranges of the finished file, and
  see whether MF's mp4 sink ever rewrites what it already wrote. If it doesn't, video reuses the
  same path. If it does, video keeps upload-on-release — owning the mp4 muxing to fix that is a
  separate project, not this one.

## Task 7 — Voice and video message drafts

Other clients keep an unsent recording as a draft: lock a recording, leave the chat, come back and
it is still there, waiting to be sent. Unigram has no such thing — leaving throws the recording
away. Nothing above depends on this, but 4.6 makes it the obvious next step, because after the
sixty-second stop the app is already holding a finished recording that has not been sent.

- [ ] **7.1** Establish what a draft is on the wire: whether the file lives in the temporary folder
  or somewhere durable, and what survives a restart of the app.
- [ ] **7.2** Persist the paused recording per chat — the file, the duration and the waveform —
  and restore the bar into its paused state when the chat is opened again.
- [ ] **7.3** Decide when a draft is discarded: sending it, the delete button, and whatever the
  official apps do when a second recording is started in the same chat.

## Task 8 — Playing a paused recording back

Pause a recording in the official apps and you can listen to it before deciding whether to send it.
Unigram pauses and draws the waveform, but there is nothing to press — the only way to hear a
recording is to send it and then play the message. 4.6 and Task 7 both end with the app holding a
recording nobody has heard, which is what makes this worth having.

- [ ] **8.1** Settle what actually gets played, which is the whole of the problem. Neither file is
  finished at the pause: on the streaming path `ChatRecordEngine.PauseAsync` only stops feeding the
  sink — frames keep arriving and are dropped, `VoiceSink` keeps `OpusOutput` open, and the ogg
  stream on disk has no final page. On the fallback path the mp4 or wav is not a file until
  `FinalizeAsync`. So either the encoded audio is kept as it is written and played from memory —
  sixty seconds of Opus at 48 kbps is about 360 KB, which for voice is nothing — or the recording is
  finalised at the pause and resuming opens a second segment to be joined at send. The first is
  cheap for voice and hopeless for video; the second is the other way round.
- [ ] **8.2** Do the recording that cannot be resumed first. 4.6 creates exactly that case: at sixty
  seconds capture is over, so the file can simply be finalised and played from disk with none of
  8.1's difficulty. A pause the user can resume from is the harder problem and deserves to be
  treated as one, not folded in.
- [ ] **8.3** Decide who plays it. `PlaybackService` is the shared player, owns the playlist and the
  system transport controls, and has just been paused by the session in order to record — handing it
  something that is not a message yet fits badly. A private `MediaPlayer` living as long as the bar
  avoids all of it. The progress and scrub surface is a waveform that is already drawn, and the sent
  voice note already has a control that does this; check whether it can be reused before rebuilding
  it.
- [ ] **8.4** Video: the preview has to show the clip instead of the camera. Cheap once 4.4 owns the
  preview surface, awkward while it is a `CaptureElement` bound to `MediaCapture`.

---

## Decisions for you

1. **Exclusive capture mode (5.1)?** Only the voice format fix wants it now — 4.1 landed without
   it. It buys `SetFormatAsync` on the audio frame source, which is what would let a 44.1kHz
   microphone take the streaming encode path instead of falling back to WAV-and-transcode. The
   cost is failing when another app holds the device, where today we'd share. Worth knowing how
   many microphones actually fall back before paying that.
2. **How far does the preview rework go (4.4)?** Composition-surface preview is the better end
   state and makes 4.1 cheap, but it's the largest single piece here.
3. **Does Task 6 get built at all?** Tasks 1–5 stand on their own: the encode win, the waveform,
   the permission fix, the latency fix, video at target size. Streaming upload adds real
   complexity — five degradation paths in 6.4 — for an improvement most desktop users will read as
   "sends fast", which 4.1 and 2.1 already deliver.
