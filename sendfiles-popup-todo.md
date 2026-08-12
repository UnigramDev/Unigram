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

Caveat on 1.3: neither `BitmapImage.SetSourceAsync` nor the video path accepts a cancellation token,
so a decode already running still runs to completion. Only the retention is fixed, not the CPU.
Truly cancelling it would need a token threaded through `ImageHelper` and `VideoAnimation`.

## Task 2 — Show the popup first, probe files after

- [ ] **2.1** `StorageMedia.CreateAsync(IEnumerable)` is a serial `foreach`, and every iteration is
  expensive: a `GetBasicPropertiesAsync` RPC per file, plus `BitmapDecoder.CreateAsync` for photos
  and a full `VideoAnimation.LoadFromFile` (ffmpeg open + probe) for video and audio. All of it is
  awaited before `new SendFilesPopup` is reached, with no feedback and no cancel.
- [ ] **2.2** The whole loop sits in **one** try/catch, so a single throwing file silently discards
  every remaining file in the drop. Needs to be per-file.
- [ ] **2.3** Each media item is opened and probed twice: `StoragePhoto.CreateAsync` decodes for
  dimensions and the preview opens the file again; `StorageVideo.CreateAsync` builds a
  `VideoAnimation` for dimensions and `GetPreviewBitmapAsync` builds a second one for one frame.
- [ ] **2.4** (2a) Pipeline the probes with bounded concurrency and add a busy/cancel affordance,
  keeping the current "resolve, then show" shape.
- [ ] **2.5** (2b) Open the popup immediately with placeholder rows (name + size) and upgrade them
  in place as probing completes. Needs the popup to tolerate a not-yet-typed item, which touches
  `IsMediaAllowed`, `TitleText`, the media/files toggle, album layout, and the permission/size
  checks currently done up front in `SendFileExecute`.

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
