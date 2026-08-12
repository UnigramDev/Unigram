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
- [x] **2.6** `Add_Click` and the popup's own `HandlePackageAsync` still call the blocking
  `CreateAsync`, so dropping onto an already-open popup stalls exactly the way the initial drop used
  to. They need the same pipeline, but `Probe` is single-shot and owns `_probeCount`, so it has to
  be generalised for a second batch first.

  Generalised by splitting the two jobs the one counter was doing. `_allocated` is the next free
  slot in the index space and only grows, so a batch appended later lands behind everything already
  picked and the contiguous-run logic keeps working across batches. `_expected` is what the title
  claims while items are in flight, and drops to zero when nothing is. Batches can overlap — files
  dropped while the first batch is still typing — so `_isLoading` became a count.

  `LoadAsync` takes one `initial` flag rather than a pile of behaviour switches: only the batch the
  popup opened for runs the caller's guard and only that one closes the popup when it comes back
  empty. An appended batch is deliberately no more checked than it was before it streamed, because
  rejecting one late arrival would otherwise close a popup with a composed caption in it — see 6.6.

  The editing branch of `HandlePackageAsync` still types its one file inline: it replaces a single
  message, so there is nothing to stream.

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

### Follow-up: one input shape (Fela's review)

The first cut left the popup with a three-step construction protocol — build with an empty list, set
`Validating`, call `Probe` from the caller's `Loaded` — driven by a `SendFilesAsync(items, files, …)`
null-pair. Miss a step and you get a silently empty popup or an unguarded send, with nothing
enforcing it. The two construction sites differ enough that this was a real trap: the edit path
passes exactly one already-typed item, has no guard at all, and reads `popup.Items[0]` back the
moment the popup closes.

- [x] **2.7** `StorageMediaSource` (in `StorageMedia.cs`, so no `.csproj` entry) is now the single
  thing the popup is given. `FromMedia` exposes everything through `Ready`; `FromFiles` leaves
  `Ready` empty and delivers through `LoadAsync`. `Count` is known up front either way.

The constructor seeds `Items` from `source.Ready` — so the edit path still has its item before the
popup opens and `Items[0]` cannot throw — and the popup calls `LoadAsync` from its own `OnLoaded`,
which is a no-op for a complete source. Callers can no longer forget it. `_expectedCount` comes from
`source.Count` at construction rather than being patched in later, so the title is right from the
first frame.

The guard is a constructor argument instead of a settable property, and the up-front loop in
`SendFilesAsync` runs over `source.Ready` — empty for files, so the last flavour check disappeared
rather than moving. The edit path passes `null`, which is honest: that item's permissions were
checked when it was first sent.

## Task 2b — Albums that grow instead of being rebuilt

Pulled forward from Task 3, because incremental arrival made it much worse: a drop of 20 photos
fills album 0 a batch at a time, then album 1, and each batch rebuilt the album from nothing.

- [x] **2b.1** `CompareItems` compared album *contents*, so an album that gained a photo was a
  different item and the diff removed and re-inserted it. `ChatDiffHandler` shows the intended
  contract — `CompareItems` is identity, `UpdateItem` carries content over. `StorageAlbum` now has
  an `Ordinal` (its position among the albums of a view) and that is what is compared.
- [x] **2b.2** The remove/insert recycled the container, and the Task 1 recycle handler released the
  album's thumbnails on the way out — so **every batch re-decoded every thumbnail in the album**.
  This was the expensive half, not the remeasure. Fixed by 2b.1: the container survives.
- [x] **2b.3** `UpdateItem` now moves the new media onto the retained album instance and refreshes
  the realized panel, rather than only invalidating layout — which had left the panel showing the
  old contents whenever it did fire.
- [x] **2b.4** `StorageAlbumPanel.UpdateMessage` reuses its children instead of `Children.Clear()`
  plus a fresh `Button` per item. Growth now only appends, and a surviving item keeps the thumbnail
  it already had. (This was Task 3.4.)
- [x] **2b.5** `Remove_Click` invalidates the removed item's thumbnail, since a container recycling
  underneath it no longer does.

Also restored: the constructor's `Logger.Info` line names each item's width and height, which is
what album layout bugs get diagnosed from. On the drop path nothing is typed yet, so it logs the
pending count there and logs the dimensions again once everything has landed.

- [x] **2b.6** Publish in album-sized chunks, so an album appears complete instead of reflowing as
  each photo lands.

  This also fixed an ordering bug. `Flush` sorted *within* a batch but appended batches in arrival
  order, so a slot that resolved late landed after everything behind it — the picked order was
  wrong across batches, and album membership with it. Results are now buffered by source index and
  only the longest settled run is published, which is required anyway: album membership is
  positional, so a slot that has not settled could still turn out to be a document and split the
  album behind it.

  While loading, and only when the caller asked for media mode, the run is truncated to a multiple
  of `StorageAlbum.MAX_ITEMS` — *every* complete album available, not one per pass, so a drop that
  types quickly still lands in a single flush. `Flush(final: true)` at the end publishes the tail
  including a part-filled last album.

  `ProbeAsync` now reports every index, passing null for a file it could not type. Without that a
  failed file would be a permanent hole and the run would never get past it.

  Trade-off: a slow file holds back everything behind it, where before those items appeared (out of
  order) without it. The popup and its title still appear immediately, which was the actual
  complaint; and a drop with `media: false` is not chunked at all, since file rows do not reflow.

## Task 3 — Stop rebuilding the world on every interaction — **done**

`UpdatePanel()` is called from roughly a dozen places — mute, TTL, crop, spoiler toggle, item
add/remove, even `SendFilesAlbumPanel_Loading`.

- [x] **3.1** ~~`UpdateCollection()` rebuilds the whole view list and, in files-mode, allocates a
  fresh `StorageDocument` wrapper for every item. New instances every time, so `CompareItems` falls
  back to path comparison and the diff churns.~~ **Wrong, and closed as won't-fix.** The fallback
  compares path *and* type, which does match, so the diff saw no change and there was no churn. That
  left only the allocation: a handful of small objects on a path that runs a few times per popup,
  not per frame. A cache for that would have to be invalidated in two places to stay correct, which
  is a standing bug risk bought for nothing measurable. The wrappers stay per-call.
- [x] **3.2** ~~`await ScrollingHost.UpdateLayoutAsync()` forces a synchronous layout pass per call.~~
  **Wrong.** It subscribes to `LayoutUpdated` once, completes a `TaskCompletionSource`, and
  unsubscribes — it waits for the next layout pass rather than forcing one, and the container walk
  genuinely has to happen after layout. The real cost is that concurrent calls each walk everything
  after the *same* layout pass, and there are several: each album panel raises `Loading`, and every
  arriving batch raises it again. A call that finds a walk already waiting now returns instead of
  queuing another — the walk reads live state, so it already covers whatever the callers behind it
  changed. The flag clears before the walk, so a call arriving during one still gets its own.
- [x] **3.3** The container walk builds a **new** `GaussianBlurEffect`, effect factory,
  `CompositionEffectBrush`, backdrop brush and `SpriteVisual` per media item per call. Effect
  factories are expensive and meant to be created once and shared. Same for `new ParticlesImageSource()`.
  The factory is now built once per popup (per instance, not static — the compositor belongs to the
  window), and both the particles and the backdrop are only touched when the state they represent
  actually flipped, instead of being reassigned on every pass.
- [x] **3.4** `StorageAlbumPanel.UpdateMessage` does `Children.Clear()` and a `new Button` with a
  fresh `Click` subscription per media, on every container realization. *Done as 2b.4.*

## Task 4 — One grouping algorithm, not two — **done**

- [x] **4.1** `SendFilesPopup.UpdateCollection` and `ComposeViewModel.GetItemsView` both group into
  `StorageAlbum`s and disagree. `GetItemsView` splits on muted video, splits `.webp` documents (the
  server-side workaround), and tracks `albumType` so media/audio/documents never mix; `UpdateCollection`
  does none of that. The grouping previewed is not the grouping sent — and `Send_ContextRequested`
  already calls `GetItemsView`, comparing against a different grouping than the one on screen.

`GetItemsView` is the source of truth and is now the only place a `StorageAlbum` is constructed.
`UpdateCollection` calls it and adapts the result for display rather than grouping again.

Three things the adaptation has to do, because grouping for sending and grouping for display are
not quite the same question:

- **Documents and audio albums expand into rows.** They are grouped for sending, but there is no
  mosaic to draw for them, so the grouping is invisible either way and the rows are what the popup
  always showed. Files mode wraps them in `StorageDocument` for the glyph, as before.
- **A standalone photo or video gets a one-item album.** `GetItemsView` leaves a muted video bare
  because it is sent as its own message; rendering that literally would drop it out of the mosaic
  and into a file row the moment the user hits mute. Sending cannot tell the difference — a
  one-item album and a bare item take the same path in `SendFilesAsync`.
- **Permissions go in as allowed, not as the chat's.** `GetItemsView` silently drops an item whose
  type is not permitted, which is right when sending and wrong when displaying. Everything in
  `Items` already cleared the guard in `SendFilesAsync`, and the edit path has no guard at all, so
  filtering here could only blank out an item the popup exists to show.

`StorageAlbum` carries its `StorageAlbumType` now, so the popup can tell a mosaic from a row without
re-deriving it.

- [x] **4.2** `Send_ContextRequested` predicted the send with a hardcoded `albumAllowed: true,
  forceDocuments: false`, so in files mode it decided whether to offer "Send without grouping" from
  a grouping that was neither on screen nor the one that would be sent. It passes the real
  `IsAlbum`/`IsFilesSelected` now, the same pair `SendFilesAsync` uses.
- [x] **4.3** `Mute_Click` never refreshed anything. That was harmless while the popup did its own
  grouping and ignored `IsMuted`; sharing the grouping makes muting move the video out of its
  album, and nothing binds `IsMuted`, so it has to ask. Everything else `GetItemsView` reads is
  either immutable (type, extension, `IsAnimated`) or already refreshes: `Items` through
  `OnCollectionChanged`, `IsFilesSelected` through `ToggleIsFilesSelected` and `MakeContentPaid`,
  `IsAlbum` only from `SendWithoutGrouping`, which hides the popup.

## Task 5 — Dead code in StorageVideo — **done**

- [x] **5.1** `Compression`, `MaxCompression`, `CanCompress`, `GetEncodingAsync`, `ToString()`,
  `UpdateWidthHeightBitrateForCompression` and the `original*`/`videoDuration`/`rotationValue` fields
  have no consumer outside the file — only `GetGeneration()` is live. They cannot work either: every
  `original*` field is assigned only in commented-out lines, so they are permanently 0,
  `MaxCompression` is always 1, `CanCompress` always false, and `ToString()` divides by zero.

299 lines out, one in. What is left is `Width`/`Height`, `TotalSeconds`, `Duration`, `IsMuted` and
`GetGeneration`, which is the whole of what the app ever asked a `StorageVideo` for.

Two knock-ons worth knowing:

- `IsMuted`'s setter was resetting `Compression` and raising `CanCompress`; it is now just the set.
- `LoadPreview()` went with it. It was called from the constructor and did nothing but compute the
  dead compression values — which is why removing the `Refresh` override back in Task 1 was safe.

The commented-out blocks in the constructor went too: they assign the `original*` fields, so
leaving them would have described members that no longer exist. Anyone reviving video compression
starts from the Android implementation rather than from this, which never worked.

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
  Still open, and now deliberate: 2.6 routes appended files through the same pipeline, so `_validating`
  *could* be applied to them, but the guard is all-or-nothing and would close a popup that already
  has a caption in it. Doing this properly means dropping the offending item and saying why, which
  is a behaviour decision rather than a wiring one.
- [ ] **6.7** `StorageMedia.CreateAsync` uses `OfType<StorageFile>()`, so dropping a folder does
  nothing with no feedback. `StorageMedia.GetFilesAsync` now carries the same limitation for the
  package paths, and is the one place to fix it.
- [ ] **6.8** `IsMediaAllowed` runs up to three LINQ passes and `TitleText` up to three more over
  `Items`, on every `UpdateView()`.

## Task 7 — Reorder media inside an album (feature)

- [ ] **7.1** The `ListView` already has `CanReorderItems`/`CanDragItems`, so the *rows* can be
  dragged — an album as a whole, or a file row. There is no way to reorder the media **within** an
  album, or to move one from one album to another, and order is what decides both the mosaic layout
  and which ten photos end up in which message.

Notes for whoever picks this up:

- The media inside an album are `Button`s parented by `StorageAlbumPanel`, a bare `Grid` that
  arranges them from `StorageAlbum.GetPositionsForWidth`. There is no items control involved, so
  none of the ListView drag machinery applies — it needs its own pointer handling, hit-tested
  against the mosaic rectangles the panel already computes in `MeasureOverride`.
- Writing the result back is the easy half: `ItemsView_CollectionChanged` already rebuilds `Items`
  by walking `ItemsView` and flattening `album.Media`, so mutating `Media` in place and re-running
  that path keeps `Items` — the collection that actually gets sent — in step.
- Dragging across an album boundary changes album membership, which changes every following album's
  contents. `StorageAlbum.Ordinal` identity handles that: the albums stay the same items and
  `UpdateItem` carries the new contents over, so nothing gets torn down.
- Worth deciding first whether reorder should also be able to *split* an album, which is currently
  only decided by `UpdateCollection` counting to `StorageAlbum.MAX_ITEMS`.
