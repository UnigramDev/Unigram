# Animation cache — rewrite design note

> **Status, 2026-08-29: built and running.** The shared cache layer, both backends on top of it,
> and the lottie move into `Telegram.Native` are all done and exercised in the app. What follows is
> the reasoning, kept because the decisions are the part worth preserving; the **Outcome** section
> below records what was actually built, what changed along the way, and what is still unverified.

Written 2026-08-29, from a session that started as an outline/shimmer measurement and ended in
the sticker cache. Nothing here is implemented. The code references were all read rather than
recalled, and the three reference clients are the local checkouts at
`C:\Source\{Android,Telegram-iOS,tdesktop}`.

## Why touch it

Not for speed. The per-item costs we measured are small: the placeholder path is 24-47 µs of
geometry plus 23-68 µs of Composition objects per realized sticker, a tenth of a frame for a
30-item burst. Two things are actually wrong:

- **Memory that never comes back.** Deleting the app cache and scrolling the sticker panel takes
  the process from ~40 MB to ~150 MB and none of it is reclaimed. The same scroll against an
  existing cache does not reproduce it.
- **No cancellation.** Scrolling past a sticker still pays for its entire cache build — every
  frame rendered, compressed and written — for a sticker the user saw for a few frames.

And a structural reason, which is the real argument for a rewrite rather than more patches:
**four owners share an animation's lifetime and none of them can enforce it.**

## What is actually broken

All verified in this session.

1. **`AnimatedImageTask` has no disposal.** It is abstract with `NextFrame` and `Seek` and
   nothing else (`AnimatedImage.cs:1842`). `AnimatedImagePresenter.Dispose()` (`:1476`) does
   `Volatile.Write(ref _task, null)` — it drops the reference and never closes the
   `LottieAnimation` / `CachedVideoAnimation` behind it, so the native animation, its cache file
   handle and its buffers survive until the RCW is finalized. This is why the cost appears only
   while caching: an animation mid-cache holds the rlottie tree and render buffers, one replaying
   an existing file holds a fraction of that.

2. **A strong capture defeats a deliberate weak one** (`AnimatedImage.cs:1982`):

   ```csharp
   _delegates[correlationId] = new WeakReference<AnimatedImagePresenter>(sender);
   _workQueue.Run(() => Work(new WorkItem(correlationId, sender.Presentation)));
   ```

   Someone thought about not rooting the presenter and then captured `sender` in the lambda.
   Every queued item roots its presenter until it runs, and the queue is longest exactly during
   a cache-building scroll. Hoisting `var presentation = sender.Presentation;` above the lambda
   fixes this one in isolation.

3. **Disposal is deferred to a tick that may not come.** `UnloadImpl` (`:1071`) sets
   `_disposing = true; _ticking = false` and relies on one further `RenderNextFrame` (`:1409`)
   to call `Dispose()`. It should fire — `Skip` does not clear `_ticking` — but the invariant is
   "someone will tick me again", which nothing enforces.

4. **Cancellation is fully built and has never fired.** `Cache()` pushes
   `WorkItem(get_weak(), ...)`, and both compress workers resolve it —
   `if (auto item{ work->animation.get() })` (`CachedVideoAnimation.cpp:402`,
   `LottieAnimation.cpp:735`) — silently skipping the item when the animation is gone. It never
   is, because of (1): nothing releases the animation, so the weak reference never dies. The
   memory bug and the cancellation gap are one bug, and fixing the lifetime turns cancellation
   on by itself.

   No work is lost by this. A whole cache build is a single work item, so an item dropped from
   the queue has rendered nothing. Mid-build cancellation — the only kind that could waste
   frames already rendered, and what Android does with its `cancelled` flag — is deliberately
   **not** wanted here: if a build has started, finishing it is cheaper than redoing it when the
   user scrolls back a few seconds later. No partial files, no resume, no format change.

5. **Two copies of the same machinery.** `s_compressQueue`, `s_compressWorker`, `s_compressLock`
   and the LZ4 frame format exist independently in `Telegram.Native/CachedVideoAnimation.cpp:22`
   and `RLottie.UWP/Telegram.Lottie/LottieAnimation.cpp:685`, in two repositories.

6. **The build holds a lock that readers need.** Two presenters can land on the same cache
   identity while differing in playback policy — the same sticker at the same pixel size in a
   second window, scrolled back to, or in the panel and a bubble that happen to match. Both
   writers take `GetLockForKey(m_cacheKey)` and hold it for the *entire* build
   (`CachedVideoAnimation.cpp:432`, `LottieAnimation.cpp:765`), and the readers take the same
   lock — `LoadFromFile` (`:153`, `:240`, `:336`) and, worse, the per-frame cache read
   (`CachedVideoAnimation.cpp:264`, `LottieAnimation.cpp:597`). A second presenter arriving
   mid-build therefore blocks for seconds, and not alone: `LoadFromFile` runs on
   `AnimatedImageLoader._workQueue` and the frame read on the shared `FifoActionWorker`, both
   FIFO, so one blocked call stalls every animation behind it. Most likely on a cold cache,
   which is when builds are in flight.

   What is already right: the writer re-checks the header under the lock and skips a redundant
   build, and `dwShareMode = 0` keeps two builds from interleaving. Only the waiting is wrong.

7. **Per-animation scratch buffers.** `m_decompressBuffer = new uint8_t[m_maxFrameSize]` per
   `CachedVideoAnimation` (`:274`), plus a frame buffer set per task. Nothing is pooled across
   the dozens of animations a panel has in flight.

Not broken, contrary to a first reading: **the first frame is always displayed before caching
starts.** `LottieAnimation.cpp:646-664` renders into the caller's buffer and only then sets
`m_readyToCache`, so the sticker shows a static first frame while the cache builds. Unigram
already has what Android exposes as `CacheOptions.firstFrame` and iOS as
`getFirstFrameSynchronously`; it just falls out of the render loop instead of being named.

## What the other three do

**Android** — `RLottieDrawable.java`, `messenger/utils/BitmapsCache.java`.
Cancellation is explicit and two-layered: `checkCacheCancel()` (`:643`) drops the task when
`parentViews.isEmpty() && getCallback() == null`, `cancelRunnable` removes it if it has not
started, and `createCache()` re-checks an `AtomicBoolean cancelled` **inside the frame loop**
(`BitmapsCache.java:261`), recycling bitmaps and closing the file mid-build. Generation runs on
one dedicated `DispatchQueue`. Scratch bitmaps and byte buffers come from a static
`CacheGeneratorSharedTools` shared by every concurrent generation and released when a global
task counter reaches zero (`:158-177`). Compression is pipelined onto a separate executor
through N slots with a `CountDownLatch` each, so memory is bounded by N frames rather than by
frame count or by how many stickers are building at once. Release is explicit — `recycle()`,
`recycleNativePtr()`, `bitmapsCache.recycle()` — deferred only while tasks are in flight.

**iOS** — `submodules/TelegramUI/Components/AnimationCache/`.
`AnimationCache` is a protocol over *both* animation kinds: `get(sourceId:size:fetch:)`,
`getFirstFrame(queue:sourceId:size:...)`, `getFirstFrameSynchronously(sourceId:size:)`. Frames
are appended through an `AnimationCacheItemWriter` (`add(with drawingBlock:...)` / `finish()`),
so writing is streaming rather than a batch. Frames are stored as DCT/YUV planes
(`DCTAnimationCacheImpl`), and `DCTMultiAnimationRendererImpl` drives many animations from one
renderer — their answer to the emoji grid.

**tdesktop** — `SourceFiles/chat_helpers/stickers_lottie.cpp`.
The cache key is `LottieCacheKeyShift(replacementsTag, sizeTag)` where `sizeTag` is a
`StickerLottieSize` **enum**, not a pixel count: a closed set of size classes, stored in the
shared cache database rather than as per-file blobs. Above
`kDontCacheLottieAfterArea = 512 * 512` (`:32`) lottie is not cached at all.


## Outcome

Shipped, in `Telegram.Native/Cache/`: `FrameCacheFormat.h`, `FrameCodec.h`, `FrameCacheReader`,
`FrameCacheWriter`, `FrameProducer.h`, `FrameCacheService`. `CachedVideoAnimation` and
`LottieAnimation` are each a reader, a producer and a frame index. rlottie and gzip-hpp are
submodules under `Libraries/`, built by `Libraries/rlottie-build/rlottie.vcxproj` as a static
library; tlottie is a prebuilt Rust staticlib. The vendored `RLottie.*` binaries and every csproj
reference to them are gone, and the C# side moved from `using RLottie;` to `Telegram.Native`.

### Where the plan changed

- **One `.tgfc` file for both renderers.** The old `.cache`/`.tcache` split existed to let the two
  caches coexist; Fela's call was that the frames are interchangeable, and they are — both emit
  premultiplied BGRA. That is only true because tlottie is constructed with
  `TLOTTIE_CHANNEL_BGRA`; its default is RGBA, and losing the flag would persist channel-swapped
  frames to disk and serve them to the other renderer.
- **Submodules, not vendored source.** rlottie/gzip-hpp are pinned to the commits RLottie.UWP used,
  with our build files outside the submodule so nothing shows up as a modification in it.
- **Win2D stayed out of Telegram.Native.** Keeping `RenderSync(CanvasBitmap, …)` would have pulled
  a second Win2D into the payload for one caller; `DiceView` moved to the `IBuffer` overload
  instead. `RenderSync(String, Int32)` had no callers and went too.
- **The queue is LIFO.** Written FIFO first, which regressed "visible stickers start playing after
  a long time" — the ones scrolled past sat ahead of the ones on screen. The original
  `s_compressQueue` was a `std::stack` for exactly this reason.
- **Both backends adopt their own cache once the build lands.** Missing this meant the instance
  that did the caching rendered every remaining frame by hand and re-raised `IsReadyToCache`
  forever, costing a skipped tick per frame. Video adopts only at a loop boundary, because its
  reader restarts at frame 0.
- **`VideoAnimatedImageTask` no longer compares `TotalFrame` against its own `_index`.**
  `TotalFrame` is 0 until a cache exists, so the two disagreed the moment one was adopted
  mid-playback. `completed` is authoritative.

### The C# lifetime work, also done

`AnimatedImageTask` has a `Dispose` that closes the native animation; `AnimatedImagePresenter`
guards it with a borrow count so a close cannot land inside a render; the strong `sender` capture
that defeated the `WeakReference` at the loader is hoisted; and the UI-affine half of teardown is
posted to the dispatcher rather than skipped, which had been dropping two `WriteableBitmap`s on
every teardown that ran on the scheduler thread. The bitmaps are pooled by
`AnimatedImageLoader.BitmapRecyclePool`, one per `XamlRoot`.

### Still open

- **The measurement that started this has not been repeated.** 40 → 150 MB on a cache-deleted
  scroll, never re-checked since. Several independent causes were addressed and nobody knows which
  mattered.
- **Only Debug|x64 has been built.** ARM64 and Release/.NET Native are untested, and Release turns
  on `WholeProgramOptimization` for a static library that has never been through it.
- `AnimatedImageLoader._closed` is never assigned, so the loader is never removed from `_loaders`
  and `Bitmaps.Clear()` is dead code.
- The `_disposing`/`_ticking` deferral can collapse now that disposal is borrow-safe; deliberately
  left alone so a crash would be attributable.
- A build is not re-prioritised when a sticker scrolls back into view, and off-screen realized
  items still build. `AnimatedListHandler` already knows which items those are.

### Deliberately not done: one cache per sticker, shared across accounts

Considered on 2026-08-29 and parked, not rejected. The cache key is the sticker's **local file
path**, which contains the session id, so the same sticker downloaded under two accounts is cached
twice at every size. `remoteFile.unique_id` is documented as "the same for the same file even for
different users and is persistent over time" and is exactly the right key.

The two halves are one change: sharing across accounts means leaving the per-session directory the
files currently sit in, so it comes with a move out of TDLib's tree - `TemporaryState` being the
natural home, since these are pure derived data and the reader treats a missing file as no cache.
The `LoadFromData` caches already moved there for that reason.

What it costs, and the reason it is worth a second look rather than a quick change:

- **TDLib's storage accounting.** Today the `.tgfc` files are counted as sticker storage because
  they happen to live in TDLib's directory. That is inheritance rather than design, and it may be
  wrong in the other direction too: `optimizeStorage` works from TDLib's own file database, so
  foreign files in that directory are plausibly invisible to its cleanup - which would mean "clear
  stickers" frees less than it reports, and `FlushStickerCache` is the only thing that ever removes
  them. Worth confirming before treating the current numbers as a feature. If the user-visible
  story matters, reporting the cache folder's own size is a few lines.
- **The sweep has to stay.** TemporaryState is cleared by Storage Sense and Disk Cleanup under
  pressure, not on a schedule. Sharing makes the sweep simpler - one folder instead of one per
  session - not unnecessary.
- **`unique_id` may be empty**, per the schema, and local assets built by
  `TdExtensions.GetLocalFile` have no remote at all. Needs the path as a fallback.
- **The plumbing is the risk, not the key.** The native change is one line; threading the unique id
  through `AnimatedImageSource` -> `AnimatedImagePresentation` -> the loader's `Work` is not.
  `AnimatedImagePresentation` is also the presenter pool key, so adding a field changes what counts
  as the same presenter: two accounts showing one sticker at one size would start sharing a
  presentation, which is a bonus or a surprise depending on whether anything downstream assumes
  per-account identity. That is the part to think about first.

Racing builds are already safe - first writer wins through the rename, so two accounts hitting the
same sticker at once costs one wasted build rather than a corrupt file.

### Settled elsewhere

Moving the SVG outline parser into C++ was considered and **is not worth doing**: the geometry
build is 24-47 µs per shimmer, ~190 ms across a 5,000-shimmer session, and `C:\Source\SurfaceSpike`
found the surface strategies indistinguishable once the synthetic scroll load was removed. That
also weakens the case for `getStickerOutlineSvgPath` to just its own merits — ~7× less JSON and one
string rather than ~50 objects per outline — since there is no native builder for an `hstring` to
cross into.

## Decision

Fela, 2026-08-29: `LottieAnimation` and `CachedVideoAnimation` are re-architected from the
ground up rather than patched, and lottie moves into `Telegram.Native` so the two share
everything they can. The rendering backends are untouched — `VideoAnimation` for ffmpeg,
rlottie/tlottie for vector.

That is the right call because what the two classes duplicate is nearly everything *except*
frame production: the cache key, the file format (header, length-prefixed LZ4 frames, trailing
offset/timing table, `maxFrameSize`), the compress queue and worker, the scratch buffers, the
key locking, the read path, the `precache → readyToCache → caching → read` state machine, and
the handle lifetime. `BufferSurface.cpp` is duplicated outright — 18 lines, same name, one copy
per repository.

### Requirements the new design has to meet

1. Many concurrent animations (100+ in the emoji grid), so decode cost per frame dominates —
   LZ4 over raw BGRA stays, against what iOS and Android chose. **Behind a codec seam**, so the
   choice can be revisited without touching the cache layer: compress/decompress a frame and
   report a worst-case bound is the whole interface.
2. Deterministic lifetime; nothing waits on finalization.
3. Queued builds droppable when nobody is watching. Started builds always finish.
4. No reader ever blocks on a build.
5. Scratch memory bounded by concurrency, not by how many builds are queued.
6. The first frame is displayed before caching begins, as today.
7. One cache format, one generation queue, one buffer pool for both backends.
8. Cache identity stays per-size: lottie must render at the rasterization scale to stay sharp.
9. The file format is **not** carried over. A clean format with a new extension is fine: version
   negotiation is unnecessary because stale cache files are wiped regularly anyway, so the old
   files simply age out rather than needing a migration path.

### The backend interface

A **sequential producer**, not a random-access renderer: `Prepare()` / `NextFrame(buffer)` /
`Release()`, plus `Reset()`. `RenderFrame(index, buffer)` fits lottie and lies about ffmpeg,
which cannot seek cheaply. Android's `BitmapsCache.Cacheable`
(`prepareForGenerateCache`/`getNextFrame`/`releaseForGenerateCache`) is the same shape for the
same reason. Random access is a property of the cache file's offset table, not of the backend.

The asymmetry that survives is the uncached fallback: lottie can render an arbitrary frame on
demand while the cache builds, video cannot. So a backend declares whether it supports random
access, and the fallback branches on that rather than pretending the two are alike.

Colour replacement and the Fitz modifier belong to the producer, not the cache. They are applied
post-render inside `LottieAnimation` today (the `m_color.A != 0x00` loop) and they are part of
the cache *key*; keeping them in the cache layer is what makes that confusing.

### Migration cost

`Telegram.Native` takes on rlottie/tlottie, and **RLottie.UWP goes away entirely** — with it the
vendored per-architecture `RLottie.dll`/`.winmd`/`.lib`, which were a solution in search of a
problem. That changes the build for all four configurations. The
tlottie switch comes along with it: `m_useTLottie`, the `.tcache` extension, and the property
that both caches coexist so toggling never forces a re-render. The C# side is cheap: `using
RLottie;` in `AnimatedImage.cs` and `AnimatedImageSource.cs`, and no `CsWinRT.cs` entries to
sweep. There is no test harness for any of this, on the most visible surface in the app.

## The design

**The decision everything else follows from: one owner, and disposal is not deferred.**

The owner is `AnimatedImagePresenter`, where the handle already lives. It is the ref-counted,
pooled object with a defined "last view detached" moment in `UnloadImpl`; moving the handle up
into `AnimatedImageLoader` would only create a second lifetime to reconcile against the
presenter's. Three things change, none of them the location:

1. `AnimatedImageTask` gets an explicit `Dispose` that closes the animation.
2. `UnloadImpl` disposes synchronously instead of setting `_disposing` and hoping for a tick.
3. The render worker takes a strong reference for the duration of one frame, which is what makes
   (2) safe.

An animation handle is owned by exactly one object, is `IDisposable`, and is disposed
synchronously when its last view detaches. The reason disposal is deferred today is that a
background render may be mid-frame. The fix is not to postpone the release but to make the
borrow explicit: the render worker takes a strong reference for the duration of one frame and
drops it, so the owner can release immediately and the native animation dies when the last
borrow ends. "Dispose later, when the tick notices" becomes "nobody is borrowing, so it is gone".

On top of that:

- **One generation service for both kinds**, in `Telegram.Native`, replacing the two copies of
  the compress queue. Cache identity and dedup already work — the native key is
  `path + colourReplacementHash + fitzModifier + WxH + renderer` for lottie
  (`LottieAnimation.cpp:209-237`) and `path + WxH + optional .fit` for video
  (`CachedVideoAnimation.cpp:130-149`), and `GetLockForKey` plus the header re-check already
  stops two presenters from building the same file twice. What unifying buys is one queue, one
  buffer pool and one format, not new behaviour.
- **Build to a temporary file, rename atomically on completion.** Readers then never see a
  partial file and never block; "is it complete" is answered by the file existing rather than by
  a header flag read under a mutex. The key lock shrinks to guarding a **set of in-flight keys** —
  the decision to build, not the build — which also gives dedup directly instead of deriving it
  from a header re-check, and supplies the shared state the two instances currently lack (each
  has its own `m_caching`, so neither knows the other is building). A reader that finds a build
  in flight renders directly, which is already the no-cache fallback and costs nothing new.
- **A shared scratch pool** for render and compression buffers, allocated on first use and
  released when the queue drains. Android's `CacheGeneratorSharedTools` is the model, including
  the global task counter that decides when to let go.
- **The cache item owns its file handle and closes it.** No reliance on finalization anywhere in
  the path.

Keep the frame format as LZ4 over raw BGRA. iOS and Android both trade encode/decode CPU for
smaller files (DCT, WEBP), and that is the wrong trade here: with ~100 concurrent animations in
the emoji grid the binding constraint is decode cost per frame, which is what LZ4 is best at.
A deliberate divergence, not an oversight.

## Scope beyond the cache

`AnimatedImage` and what hangs off it need work too, but as a **second phase**, not mixed into
the cache rewrite — the cache layer has no test harness and neither does the presentation layer,
and doing both at once means no known-good state to bisect against.

The open question there was `WriteableBitmap`, and the spike at `C:\Source\SurfaceSpike`
answered it: **not for speed — only for memory ownership.**

**Measured** (100 tiles at 192×192, 30 fps each, static, Debug/CoreCLR): `WriteableBitmap` 0.06 ms
per composition tick and 29% of a core; a `CompositionDrawingSurface` **per tile** 0.00 ms and 31%;
an **atlas** — one surface, one draw session per batch — 0.00 ms and 18%. Nothing dropped a frame in
any variant. `SoftwareBitmap` was eliminated: three to five times the UI-thread cost, unable to hold
the tick rate, ~9% of frames dropped waiting on `SetBitmapAsync`.

So: `WriteableBitmap` is fast, simple, reuses XAML's device, and is **not** what makes the panel
stutter. Per-tile composition is ruled out for good — it frees the UI thread and then spends the
saving on `BeginDraw`/`EndDraw` transactions at ~41 µs each. The atlas is a real lever (the gap
widens with load: 47% against 67% at 60 fps) but it is headroom for later, not a fix for anything
we have measured. Do not reopen this on performance grounds.

**The ownership case, which does stand.** `AnimatedImage.cs:80-88` already documents it: `PixelBuffer`
holds the `WriteableBitmap` as a plain COM reference from C++, which keeps it alive but **not
reachable**, so the reference tracker can collect the framework peer while the native side still
holds the object — after which `ImageBrush.ImageSource` fails with `E_FAIL`. The code works around
it by passing frame geometry as numbers rather than reading it back off the brush.

That is two owners, one of which the runtime cannot see, and no amount of tidying fixes it:

- `CleanOnSourceChanged` defaults to true, so `Unload` already nulls `LayoutRoot.Background` and
  drops the brush. The five opt-out sites (`SendMessagesView.xaml:25`, `Generic.xaml` 2154/2251/
  2334/2408) keep the last frame up while a new source loads, and they are styles with one template
  each — bounded, not the leak.
- Releasing the RCW early (`IWinRTObject.NativeObject.Dispose()`, as `FormattedTextBlock.cs:2861`
  does for handles it owns exclusively) is wrong here: XAML and the render thread hold references
  too, so it may free nothing, and the renderer holds the `PixelBuffer` while writing.

A surface the app owns outright collapses the two owners into one, which is the same rule this note
already demands of the animation handle.

**Pooling, if the surface move proves too big.** Bounded by the *concurrently visible* count rather
than the scrolled-past count, so the ceiling is knowable (~29 MB at panel size) where today's is
not. It must be per-`XamlRoot`, since `new WriteableBitmap` needs the UI thread — which is where
`AnimatedImageLoader` already lives. Trim on real events (panel closed, deactivated, suspending,
`AppMemoryUsageIncreased`) with a slow timer only as a backstop; a timer alone is a proxy for those,
and picking its interval in the abstract is guessing. Cap per size bucket and globally, or the
high-water mark becomes the sum of every bucket's peak.

**The prerequisite for both is the same, and it is already step 1 of phase one.** Returning a bitmap
to a pool while the renderer still holds its `IBuffer` is a use-after-return; disposing it early is
a use-after-free. Both need the borrow rule — the worker holds a strong reference for the duration
of one frame, and the owner cannot release until every borrow ends. Build that and pooling and
disposal both become available, on either surface type.

`AnimationScheduler` and the single `CompositionTarget.Rendering` subscription already amortize
the tick across all visible animations, so the scheduling half is in reasonable shape; it is the
per-animation GPU resources and the per-frame upload that are not.

Out of scope entirely: the outline and shimmer path, which we measured to death and which is not
the problem.

## Order of work

**Now, outside the rewrite:** the two C# lifetime fixes — `AnimatedImageTask` disposal and the
closure capture at `AnimatedImage.cs:1982`. They live in `Telegram/`, they are needed whatever
the native side becomes, they recover the memory, and the first of them turns on the
cancellation that is already written. Do them before the rewrite starts, so the rewrite is not
also carrying the memory bug.

Before those, **instrument**: register `AnimatedImagePresenter` and `AnimatedImageTask` with
`Instrumentation.Register` so the existing orphan analyzer can see them, then do a cache-deleted
scroll. That says whether the retained objects are *rooted* (defects 2 and 3) or merely
*uncollected* (defect 1), which have opposite fixes and which the RAM graph cannot tell apart.

**In the rewrite**, in dependency order: the shared cache layer and its file protocol
(temporary file plus atomic rename, the in-flight key set, one queue, one scratch pool), then
the two backends behind the sequential producer interface, then the lottie move into
`Telegram.Native` and the retirement of the duplicate `BufferSurface`.

Everything the earlier draft listed as separate in-place patches — temp file, in-flight keys,
scratch pool, unification — folds into the rewrite rather than being done twice.

## Open questions

- Why was the `SurfaceImage` / `Direct2D` path abandoned? See the scope section — it gates the
  whole second phase.
- Is `GetLockForKey`'s map thread-safe, and are its entries ever evicted? It hands out a
  reference into a map keyed by cache-key string, and both are worth confirming.
- Is `limitFps` missing from the video cache key? It is a `CachedVideoAnimation::LoadFromFile`
  parameter but does not appear in `m_cacheKey`. Lottie does not take it at all — the app steps
  the frame index at playback (`framesPerUpdate`) — so lottie is unaffected. If it changes which
  frames get written for video, two presentations differing only in that flag share one file.
- The open-ended size key stays for now. Lottie must render at the rasterization scale to stay
  sharp, so tdesktop's size classes cannot be copied directly, and narrowing the key later does
  not disturb anything else.

Settled while writing this: cancellation needs no partial-file handling (see defect 4), and the
pool key and the cache identity are already separate — the record is the presenter's key, the
native `m_cacheKey` is the file's.
