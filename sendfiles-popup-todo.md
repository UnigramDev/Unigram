# SendFilesPopup — improvement plan

Findings from a read-through of `Telegram/Views/Popups/SendFilesPopup.xaml{,.cs}`, the five
`Telegram/Entities/Storage*.cs` entities, both album-grouping implementations, and the entry
points that reach the popup (`ComposeViewModel.SendFileExecute`, `DialogViewModel.HandlePackageAsync`,
`SendMessagesView`, `Extensions.PickSingleMediaAsync`).

Tasks are ordered so each one can land and be reviewed on its own. Check an item off in the
same commit as its fix.

---

## Task 1 — Move the thumbnail lifecycle to the ListView — **done**

- [x] **1.1** `StorageMedia._preview` is assigned in four places and released in none. Nothing
  ever sets it back to null, so a decoded thumbnail lives as long as the model does — and the
  model outlives the popup, because `ComposeViewModel.SendFileExecute` holds the `items` list
  across the whole send loop.

  Per item, never reclaimed:
  - photo → `BitmapImage` at `DecodePixelWidth = 300` **logical**, so ~450px at 150% scale ≈ 1 MB
  - video → `WriteableBitmap` at 600 min-side ≈ 2 MB, held as a live pixel buffer
  - cropped → `SoftwareBitmapSource` at 600

  40 dropped videos ≈ 75 MB retained past the popup.

  The only consumer is the `ImageBrush` in `MediaItemTemplate` (`SendFilesPopup.xaml`), reached
  only through `StorageAlbumPanel` — so the move is self-contained.

- [x] **1.2** Thundering herd: `Refresh()` is `async void` fired from the `Preview` getter with no
  in-flight guard. `_preview` stays null across the await, so every getter hit before it completes
  starts another decode of the same file. `StorageAlbumPanel.UpdateMessage` clears and rebuilds
  every button on each `UpdatePanel()`, re-evaluating every `x:Bind Preview`.

- [x] **1.3** No cancellation. Closing the popup mid-decode leaves every outstanding decode running.

**How it landed.** `Preview`/`Refresh`/`RefreshAsync` are gone from `StorageMedia`, along with the
`StorageVideo.Refresh` override — the `LoadPreview()` it re-ran after a crop only feeds the
compression fields that Task 5 shows are dead, and the constructor still runs it. The decode moved
verbatim into `StorageThumbnailCache` at the foot of `SendFilesPopup.xaml.cs`, which keys two
dictionaries off the model: the decoded source, and the in-flight `Task` that coalesces concurrent
requests (1.2). The `ImageBrush` in `MediaItemTemplate` no longer binds; `UpdateTemplate` pushes into
it, and `LoadThumbnail` re-checks `root.DataContext` before applying a late result.

Eviction has two triggers: `OnContainerContentChanging` with `InRecycleQueue` nulls the brushes and
drops the album's entries, and `OnUnloaded` clears everything. A decode that completes after either
one is dropped rather than cached — `DecodeAsync` only writes to the cache if its own `_inflight`
entry is still there, which is also what stops a pre-crop image from landing after `Invalidate`.

Caveat on 1.3: no decode already under way is stopped, so only the retention is fixed, not the CPU.
See the cancellation survey below for what it would take — the first estimate here was too
pessimistic.

### Cancellation survey (1.3 follow-up)

Two levers already exist and neither is used.

**`.AsTask(token)`** covers every WinRT stage: `StorageFile.OpenReadAsync`,
`BitmapImage.SetSourceAsync`, `BitmapDecoder.CreateAsync`, `GetSoftwareBitmapAsync`,
`SoftwareBitmapSource.SetBitmapAsync`. That is the whole photo path and the whole cropped-photo
path. The repo calls `AsTask()` in three places and never with a token.

**`VideoAnimation.Stop()`** is projected to C# (`VideoAnimation.idl`) and the `stopped` flag it sets
is already checked in all three hot spots — `readCallback`, `seekCallback`, and the
`while (!stopped && triesCount > 0)` loop in `RenderSync` (`VideoAnimation.cpp`). Nothing in the app
has ever called it. `ImageHelper`'s two video branches build the animation inside a `Task.Run` and
never expose it, so reaching `Stop()` is a C#-side restructure, not a native one.

The one genuinely uncancellable stage is the probe. `VideoAnimation::LoadFromFile` constructs the
instance and only then runs `avformat_open_input` and `avformat_find_stream_info`, so the caller has
no handle while the expensive part runs. The fix is `fmt_ctx->interrupt_callback` pointed at the same
`stopped` flag before `avformat_open_input`; ffmpeg polls it through open, probe, read and seek.
That one needs a `Telegram.Native` rebuild.

Two latent bugs in the existing `Stop()` path, never exercised because nothing calls it:

- [ ] **1.4** `stopped` is a plain `bool`, written by `Stop()` without taking `m_lock` and read from
  the decode thread. Should be `std::atomic<bool>`.
- [ ] **1.5** `readCallback` returns `0` when stopped rather than `AVERROR_EOF`. ffmpeg reads `0` as
  "no bytes this call" and can spin instead of aborting.

Cost × rate says this does not pay for thumbnails alone — closing the popup mid-decode is rare. It
pays as part of Task 2, whose cancel affordance needs the same plumbing, and whose probe pipeline is
what justifies the native `interrupt_callback` work.

## Task 2 — Show the popup first, probe files after — **done**

- [x] **2.1** `StorageMedia.CreateAsync(IEnumerable)` is a serial `foreach`, and every iteration is
  expensive: a `GetBasicPropertiesAsync` RPC per file, plus `BitmapDecoder.CreateAsync` for photos
  and a full `VideoAnimation.LoadFromFile` (ffmpeg open + probe) for video and audio. All of it is
  awaited before `new SendFilesPopup` is reached, with no feedback and no cancel.
- [x] **2.2** The whole loop sits in **one** try/catch, so a single throwing file silently discards
  every remaining file in the drop. Needs to be per-file.
- [ ] **2.3** Each media item is opened and probed twice: `StoragePhoto.CreateAsync` decodes for
  dimensions and the preview opens the file again; `StorageVideo.CreateAsync` builds a
  `VideoAnimation` for dimensions and `GetPreviewBitmapAsync` builds a second one for one frame.
  *Left open — a separate change to the `Storage*` factories, not to the popup flow.*
- [x] **2.4** Pipeline the probes with bounded concurrency.
- [x] **2.5** Open the popup first and append items as they resolve. **No placeholder rows** — Fela's
  call: in the vast majority of cases probing is instant, so a row that exists only to be replaced
  buys nothing and costs the popup having to tolerate a not-yet-typed item everywhere.
- [ ] **2.6** `Add_Click` and the popup's own `HandlePackageAsync` still call the blocking
  `CreateAsync`, so dropping onto an already-open popup stalls exactly the way the initial drop used
  to. They need the same pipeline, but `Probe` is single-shot and owns `_probeCount`, so it has to
  be generalised for a second batch first.

**How it landed.** `StorageMedia.ProbeAsync` types files concurrently — capped at
`Math.Clamp(Environment.ProcessorCount, 2, 8)` so a large drop cannot open hundreds of decoders —
and reports each result through a callback as it lands, with the file's original index. Callbacks
arrive on the UI thread, since every await in the chain captures the caller's context.

`ComposeViewModel.SendFileExecute` no longer probes. Both overloads now funnel into `SendFilesAsync`,
which takes either already-typed items or raw files; with files it builds the popup empty and starts
`Probe` from the `Loaded` handler. That hook matters: `OpenAsync` queues behind any other dialog and
only creates its closing task once it reaches the front, so a `Hide` from a probe result that
resolved earlier would have had nothing to close.

Results are buffered and flushed on a low-priority dispatch, so everything resolving within one UI
turn is appended by a single `AddRange` — one `CollectionChanged`, one `UpdateView`, one
`UpdatePanel`. Each batch is sorted by original index, so the picked order survives whenever
probing is fast enough to land in one flush; across batches items appear as they resolve.

The guard moved rather than disappeared. `SendFilesAsync` keeps one `Validating` function holding
the original messages: run as a loop up front for already-typed items, handed to the popup as a
callback for probed ones. The first failure cancels probing and closes the popup, and the error is
raised after `OpenAsync` returns — which is also where the caption is already restored.

Three smaller consequences:

- `TitleText` counts `Math.Max(Items.Count, _probeCount)` while probing, so it shows the drop's real
  size instead of ticking up, and stays on the Files declension until types are known.
- The requested media/files mode cannot be resolved against an empty list, so `UpdateView` settles it
  as the first items arrive — until the user picks a mode themselves, which wins from then on.
- Send is disabled while probing, and `Accept` returns early, since sending half a drop would
  silently discard the rest.

Known edge: if every file fails to probe, the popup appears briefly and then closes itself, where
before it never appeared. The alternative is waiting for the first result, which is the stall this
task removed.

## Task 3 — Stop rebuilding the world on every interaction

`UpdatePanel()` is called from roughly a dozen places — mute, TTL, crop, spoiler toggle, item
add/remove, even `SendFilesAlbumPanel_Loading`.

- [ ] **3.1** `UpdateCollection()` rebuilds the whole view list and, in files-mode, allocates a fresh
  `StorageDocument` wrapper for every item. New instances every time, so `CompareItems` falls back
  to path comparison and the diff churns.
- [ ] **3.2** `await ScrollingHost.UpdateLayoutAsync()` forces a synchronous layout pass per call.
- [ ] **3.3** The container walk builds a **new** `GaussianBlurEffect`, effect factory,
  `CompositionEffectBrush`, backdrop brush and `SpriteVisual` per media item per call. Effect
  factories are expensive and meant to be created once and shared. Same for `new ParticlesImageSource()`.
- [ ] **3.4** `StorageAlbumPanel.UpdateMessage` does `Children.Clear()` and a `new Button` with a
  fresh `Click` subscription per media, on every container realization.

## Task 4 — One grouping algorithm, not two

- [ ] **4.1** `SendFilesPopup.UpdateCollection` and `ComposeViewModel.GetItemsView` both group into
  `StorageAlbum`s and disagree. `GetItemsView` splits on muted video, splits `.webp` documents (the
  server-side workaround), and tracks `albumType` so media/audio/documents never mix; `UpdateCollection`
  does none of that. The grouping previewed is not the grouping sent — and `Send_ContextRequested`
  already calls `GetItemsView`, comparing against a different grouping than the one on screen.

## Task 5 — Dead code in StorageVideo

- [ ] **5.1** `Compression`, `MaxCompression`, `CanCompress`, `GetEncodingAsync`, `ToString()`,
  `UpdateWidthHeightBitrateForCompression` and the `original*`/`videoDuration`/`rotationValue` fields
  have no consumer outside the file — only `GetGeneration()` is live. They cannot work either: every
  `original*` field is assigned only in commented-out lines, so they are permanently 0,
  `MaxCompression` is always 1, `CanCompress` always false, and `ToString()` divides by zero.

## Task 6 — Smaller items

- [ ] **6.1** `FileItem_PointerEntered`: `storage` is assigned and never used, and `content` is the
  template root rather than the container, so `ItemFromContainer` returns null anyway.
- [ ] **6.2** `OnContainerContentChanging`: the `root is AspectView` branch is unreachable. The
  selector only returns `FileItemTemplate` or `AlbumTemplate`, both `Grid`-rooted; `MediaItemTemplate`
  is only ever a `Button.ContentTemplate` inside `StorageAlbumPanel`.
- [ ] **6.3** Six `root.FindName(...)` namescope walks per container realization, plus two `Substring`
  allocations to split the filename extension.
- [ ] **6.4** The constructor builds a log string with a `string.Format` per item and repeated
  `StringBuilder.Prepend` (quadratic in characters) before the popup shows. `Logger.Info` is
  unconditional, so this always runs.
- [ ] **6.5** `HandlePackageAsync`, `Add_Click` and `StorageMedia.CreateAsync` all `catch { }` silently.
- [ ] **6.6** `Add_Click` and drop-into-popup bypass the permission and file-size checks that
  `SendFileExecute` applies to the initial set, and do not dedupe against files already listed.
- [ ] **6.7** `StorageMedia.CreateAsync` uses `OfType<StorageFile>()`, so dropping a folder does
  nothing with no feedback.
- [ ] **6.8** `IsMediaAllowed` runs up to three LINQ passes and `TitleText` up to three more over
  `Items`, on every `UpdateView()`.
