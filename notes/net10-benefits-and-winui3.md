# What .NET 10 is worth, and whether WinUI 3 follows

Written 2026-08-17. Two investigations that were asked together because they interact:
what the .NET 10 port unlocks beyond "it compiles", and whether a UWP → WinUI 3 migration
is the sensible next step after it.

Companion to `net10-port-todo.md`, which is the port's own plan and resume point.

Everything stated as fact below was read out of this repository or out of installed
metadata, and the source is named. Anything I could not verify locally is marked
**unverified** rather than smoothed over.

---

## The two answers, up front

**.NET 10.** The compile-time language features are mostly *already available* — the legacy
project is on `LangVersion 14.0` too — so the C# 14 wins cost nothing and are the cheap half.
The valuable half is the BCL and the runtime, and every item of it costs an `#if` for as long
as `Telegram.csproj` ships. That makes "when does .NET Native retire?" the question that
governs the whole list, not a footnote to it. Before spending on any of it: **nothing about
the AOT build has been measured yet** — Phase 5's own "compare startup and working set"
item is still unchecked, and `Telegram.Benchmarks` already has both hosts wired up.

**WinUI 3.** Not now, and possibly not ever on the current design. There is one verified hard
blocker: **`XamlDirect` does not exist in Windows App SDK** — no `XamlDirect`,
no `IXamlDirectObject`, no `Microsoft.UI.Xaml.Core.Direct` anywhere in 1.8's metadata — and
`FormattedTextBlock.cs` has 98 references to it, plus a native fast path in
`NativeUtils::AddRunToCollection`. That is the app's hottest surface, rebuilt on an API the
target framework does not have. A second likely blocker, unverified, is the `windows.voipCall`
UWP extension the call stack is built on. Neither is discovered by starting the migration;
both are answerable in a day of spikes, and those spikes should happen before anything else.

The good news for sequencing: **the WinUI 3 path invalidates none of the .NET 10 work.**
CsWinRT discipline, the AOT manifest, the `TG100x` analyzer, `List<T>` on the TDLib API,
`LibraryImport` — all of it carries over unchanged. Finishing .NET 10 first is not a detour.

---

# Part 1 — What .NET 10 actually buys

## The governing constraint

The two projects compile the same 1259 `.cs` files (450k lines) and 475 `.xaml` files (86k
lines). `Telegram.csproj` is .NET Native, whose BCL is netstandard2.0 plus `System.Memory`
and `PolySharp`; `Telegram.Modern.csproj` is `net10.0-windows10.0.26100.0`.

Divergence today is small: **50 `#if NET9_0_OR_GREATER` guards across 19 files.** That is the
budget every proposal below draws against. A hot-path optimisation that needs a .NET 9+ type
is not one edit, it is two code paths kept in step by hand, forever, in the highest-churn part
of the app.

Two consequences worth stating plainly:

- **Don't scatter `#if` through hot code.** The precedent that already works is aliasing:
  `CsWinRT.cs` aliases `CompositionTarget` to `CompositionTargetImpl` on .NET 9+ and to the real
  type otherwise, and not a single call site knows. A small `Telegram/Compat/` layer of aliases
  and one-line shims can absorb most of the list below the same way.
- **The list is worth roughly 2–3× more if there is an end date for .NET Native.** Most items
  are cheap as a one-way change and expensive as a permanently forked one.

## Tier 0 — already banked, and unmeasured

| what | state |
|---|---|
| NativeAOT instead of .NET Native ILC | links, runs, packages; 57 MB exe |
| `LibraryImport` + `DisableRuntimeMarshalling` | done, every `DllImport` converted |
| `System.Text.Json` source generation | was already reflection-free before the port |
| Reflection eliminated | `TypeCrosserGenerator` deleted, `TypeContainerGenerator` behind `#if DEBUG` |

**The first action item in this whole document is a measurement, not a change.** There is no
number for startup, working set, or binary size against the shipping .NET Native build. Modern
RyuJIT codegen through ILC, the regions GC, and .NET 9/10 escape analysis (non-escaping objects
and small fixed-size arrays get stack-allocated) are all free and may already be worth more than
everything in Tier 2. Optimising before that number exists is guessing.

## Tier 1 — language features, free in both builds

`Telegram.csproj` sets `<LangVersion>14.0</LangVersion>` and `Telegram.Modern.csproj` sets
`preview`. So C# 14 is available to *both*, and anything purely compile-time can be adopted with
no conditional compilation at all. Worth doing on touch, not as a sweep.

- **`field` keyword.** The app is full of `private X _x;` plus a property whose setter raises
  `RaisePropertyChanged`. `set { field = value; RaisePropertyChanged(); }` removes the backing
  field without changing a single generated instruction. Legibility only — it allocates nothing
  less, so it does not belong on a hot path as a justification of its own.
- **Collection expressions.** Already in use, and already policed: `TG1003` catches the one
  shape that cannot work across the WinRT ABI (a collection expression targeting a read-only
  interface synthesises a type that can never have a CCW). Keep using them; keep the analyzer.
- **Null-conditional assignment**, **unbound `nameof`**, **partial properties and events** —
  cosmetic, safe, free.
- **Extension members (extension blocks, extension properties, static extension members).**
  `Td/Api/TdExtensions.cs` is 4936 lines of `public static bool IsX(this Chat chat)`, much of
  which reads as properties. **Verify before adopting**: extension blocks emit a new metadata
  shape (a nested marker type), and whether .NET Native's ILC digests it is unknown. One
  throwaway file in the legacy project answers it.
- **`params ReadOnlySpan<T>`** — this one is *not* free. The compiler materialises the argument
  buffer through `[InlineArray]`, which is a runtime layout feature, not an attribute PolySharp
  can polyfill. Treat as **modern-only**, i.e. Tier 2.

## Tier 2 — BCL and runtime wins, each costing an `#if`

Ordered by value per unit of divergence. Every one of these is unavailable on .NET Native.

**1. `System.Threading.Lock` (.NET 9) — 389 `lock` sites.**
The cheapest large-surface change available, because it needs no call-site edits at all: a
`global using` alias resolving to `System.Threading.Lock` on modern and `System.Object` on
legacy makes `private readonly TgLock _lock = new();` compile in both, and `lock (_lock)` binds
to the fast path only where it exists. Whether 389 sites is worth anything depends on contention,
which is unmeasured — but the divergence cost here is one line in one file, which is unusual.

**2. `FrozenDictionary` / `FrozenSet` (.NET 8) — the static tables.**
Read-only lookup tables built once at startup and read forever, which is exactly what `Frozen`
is for (roughly 2× read throughput for a slower build). The candidates, all `static readonly`:

| site | shape |
|---|---|
| `Services/ThemeService.Defaults.cs:177`, `:1776` | `Dictionary<string, object>`, ~1600 entries each, read per theme-resource resolve |
| `Common/Emoticon.cs:163` | `Dictionary<string, string> Data` |
| `Entities/Country.cs:84-85` | `KeyedCountries`, `Codes` |
| `Controls/FormattedTextBlock.cs:2171`, `:2202` | syntax-highlight colour maps, read per code block rendered |
| `Charts/BaseChartView.cs:409`, `:426`; `Charts/DataView/LineViewData.cs:74`, `:88` | chart palettes |

**3. `Dictionary.GetAlternateLookup<ReadOnlySpan<char>>` (.NET 9) — the `Substring` tax.**
265 `.Substring(` call sites. The ones that exist only to produce a dictionary key are pure
waste, and an alternate lookup deletes the allocation outright. Not all 265 qualify — the
scan is worth doing once, and the URL/scheme parsing in `Common/MessageHelper.cs` is where I
would start.

Note the counter-example, so this is not oversold: `FormattedTextBlock.cs:768` does
`text.Substring(offset, length)` to feed `XamlDirect.SetStringProperty`, per run, on the hottest
path in the app. There is no span overload across the WinRT ABI. .NET 10 does not help there.

**4. `SearchValues<char>` / `SearchValues<string>` (.NET 8/9) — the scanners.**
Only **2** `IndexOfAny` sites exist in the whole app, which is the tell: the character scanning
is hand-rolled `for` loops over `char`. `SearchValues` replaces those with a vectorised,
allocation-free search. `Common/AutocompleteEntityFinder.cs:26` is the clearest case — a
`HashSet<char>` of four symbols (`: # @ /`), consulted per character per keystroke in the
composer. Entity/markdown/URL detection is the same shape.

**5. `[GeneratedRegex]` — and two sites that are wrong regardless of framework.**
Only 10 `Regex` uses, but two are defects:

- `Controls/Messages/MessageBubble.xaml.cs:2455` constructs a `new Regex(...)` **per call** on a
  rendering path (YouTube embed detection).
- `Services/SettingsSearchService.cs:62-64` builds `"\b" + Regex.Escape(query)` and runs
  `Regex.IsMatch` **per entry, per query** — a fresh parse and interpret for every settings row
  on every keystroke.

Both should become cached statics *today*, in both builds. `[GeneratedRegex]` on top is
modern-only and is the smaller half of the win.

**6. `CollectionsMarshal.AsSpan(List<T>)` — and why the in-flight `List<T>` work is the enabler.**
The uncommitted `IList<T>` → `List<T>` change on the generated TDLib API (86 files, per
`net10-port-todo.md`) is what makes this possible: with `IList<T>` there is no span to take and
`foreach` boxes an enumerator (23 sites on `.Entities` alone, per render). With `List<T>` the
hot iterations over `.Entities`, `.Sizes` and friends can become span loops with elided bounds
checks. `GetValueRefOrAddDefault` is the same story for the counting dictionaries.
This is the highest-value item in Tier 2 and it is already half-done for other reasons.

**7. `Span<T>` that is already there.** On .NET Native `Span<T>` comes from the `System.Memory`
package — the portable implementation, not the runtime's `ref`-field one, and without the
vectorised `MemoryExtensions`. If that holds (**unverified**; a disassembly or a
`Telegram.Benchmarks` run settles it), then every existing `Span` path in the app got faster on
.NET 10 with zero edits, and the right response is to measure it, not to write more code.

**8. Minor, on touch:** `params ReadOnlySpan<T>` for varargs helpers (`Logger.Log`,
`MvxObservableCollection.AddRange`), `[InlineArray]` scratch buffers in `MeasureOverride`
(only 5 `stackalloc` in the app today), `Base64Url`, `Ascii`, `Convert.TryToHexString`.

## Tier 3 — AOT and build knobs, modern-only, all measurable

None of these are code changes; all should be A/B'd rather than reasoned about.

- **`OptimizationPreference`** — `Speed` vs `Size` against the current 57 MB exe.
- **`IlcInstructionSet`** — raising the x64 baseline (e.g. `x86-64-v3` for AVX2) is real
  throughput for the vectorised BCL paths, at the cost of excluding pre-2013 CPUs. For a
  shipping client that is a product decision, not a build one. Check what the current default
  actually is before assuming there is headroom.
- **`IlcFoldIdenticalMethodBodies`** — size.
- **Feature switches**: `EventSourceSupport=false`, `MetadataUpdaterSupport=false`,
  `NullabilityInfoContextSupport=false`, `HttpActivityPropagationSupport=false`.
  **Do not set `UseSystemResourceKeys=true`** — it replaces framework exception messages with
  bare keys, and the crash reporter and `WatchDog` are built on those messages being readable.
- **GC**: workstation concurrent is right for this app; `GCConserveMemory` is worth one
  measurement given the working-set comparison has never been made.

## Suggested order

1. Measure the AOT build against .NET Native: startup, working set, binary size. (Tier 0)
2. Fix the two regex defects — no framework dependency, no `#if`. (Tier 2 #5)
3. Land the `List<T>` change that is already sitting in the working tree, then take
   `CollectionsMarshal.AsSpan` on the message-render loops. (Tier 2 #6)
4. Decide the .NET Native end date. Everything after this point is priced by that answer.
5. If the answer is "it retires": `Lock` alias, `Frozen*` tables, `SearchValues` in the
   composer scanner, alternate lookups. If the answer is "both indefinitely": take only the
   items that fit behind an alias, and skip the rest.

---

# Part 2 — UWP → WinUI 3

## Framing

These are two different changes and it is worth not conflating them. UWP-on-.NET-10 keeps the
app model (AppContainer, `Windows.Universal`) and the XAML framework (`Windows.UI.Xaml`), and
swaps the runtime underneath. WinUI 3 changes both: packaged **desktop** app model, and
`Microsoft.UI.Xaml`. The .NET 10 port is done and does not require the second step.

## The size of the surface

Numbers from the tree, generated code excluded:

| | count |
|---|---|
| C# files touching `Windows.UI.*` | 931 of 1259 |
| `using Windows.UI.Xaml*` | 2381 |
| `using Windows.UI.Composition` | 461 |
| `ElementCompositionPreview` uses | 337 |
| `.xaml` files naming `Windows.UI.Xaml` explicitly | 8 of 475 |
| `muxc:` (WinUI 2) control uses in XAML | 332 |
| `ApplicationView.GetForCurrentView` | 49 |
| `CoreWindow` references | 101 |
| Hand-written C++/WinRT files | 250 (`Telegram.Native` + `.Calls`) |
| …touching `Windows::UI::Xaml` | 37 |
| …touching `Windows::UI::Composition` | 21 |

## What is mechanical

- **The namespace rewrite.** `Windows.UI.Xaml` → `Microsoft.UI.Xaml`, `Windows.UI.Composition`
  → `Microsoft.UI.Composition`, `Windows.UI.Text` → `Microsoft.UI.Text`. Touches ~75% of the
  app's files and is nearly all `sed`. XAML barely notices — only 8 files name the namespace.
- **WinUI 2 collapses into the framework.** `muxc:` becomes the default namespace and
  `XamlControlsResources` becomes redundant. `App.xaml:513` already merges it, which means the
  app is *already* styled against WinUI 2.8 Fluent — the best single signal in this whole
  assessment that visual parity would not be a catastrophe.
- **Win2D**: `Win2D.uwp` → `Microsoft.Graphics.Win2D`, same `Microsoft.Graphics.Canvas` API.
- **WebView2**: already `muxc:WebView2`; native in WinUI 3, verified present in the metadata.
- Verified present in Windows App SDK 1.8 metadata, so *not* problems: `SurfaceImageSource`,
  `VirtualSurfaceImageSource`, `ConnectedAnimationService`, `CompositionCapabilities`
  (in `Microsoft.UI.winmd`), `RichEditBox`, `ThemeShadow`, `SwapChainPanel`, `CompositionTarget`,
  `ItemsRepeater`, `AnimatedIcon`, `TabView`, `PipsPager`. Background tasks, app notifications,
  storage pickers, media capture and package deployment all have WinAppSDK equivalents shipping
  in their own winmds.

## What is real, bounded work

- **The two native components.** `Telegram.Native.vcxproj` is `AppContainerApplication=true`
  UWP C++/WinRT. Both must be rebuilt as desktop C++/WinRT against WinAppSDK projections. The
  affected surface is bounded — 37 + 21 of 250 files — and includes `SurfaceImage`,
  `Composition.CompositionDevice`, `FreeformGradientSurface`, `ChatBackgroundPattern`,
  `MessageBubbleNineGrid`, `PlaceholderImageHelper`.
- ~~**RLottie is in another repo.**~~ No longer true: lottie moved into `Telegram.Native` and
  RLottie.UWP is retired, so there is no separate winmd and no second codebase to migrate. What
  replaced it renders into a caller-supplied buffer rather than a Win2D `CanvasBitmap`.
- **MRT → MRT Core.** `ResourceContext.GetForCurrentView` (6 sites) and the `Strings\**\*.resw`
  pipeline move to `Microsoft.Windows.ApplicationModel.Resources`.
- **`SystemNavigationManagerPreview.CloseRequested`** (8 sites) and `rescap:confirmAppClose`
  become ordinary window-closing handling — this one gets *simpler*.
- `SystemMediaTransportControls.GetForCurrentView` and `UIViewSettings.GetForCurrentView` are
  `CoreWindow`-bound and need interop or replacement.

## What is architectural

**The window and view model is the multi-month piece.** `Services/ViewService/ViewService.cs`
creates views with `CoreApplication.CreateNewView()` (two sites), and `Navigation/WindowContext.cs`
is built end to end on `ApplicationView`: `GetForCurrentView`, `ApplicationViewSwitcher`
(`SwitchAsync`, `TryShowAsStandaloneAsync`), `Consolidated`, `PersistedStateId`,
`SetPreferredMinSize`, `VisibleBoundsChanged`, `IsScreenCaptureEnabled`, fullscreen,
`CoreApplication.GetCurrentView().TitleBar`. All of it becomes `Window` + `AppWindow` +
presenters + Win32. This is where the app is most unusual — a view per thread, the gallery's
compact-overlay window, the call window — and it is the part that cannot be done incrementally.

Two things push in opposite directions here. Against: it is a rewrite of the app's spine.
For: WinUI 3 supports multiple windows on one thread, which would delete the entire class of
bug this repo has been fighting — CsWinRT #2524 (per-view static events landing on the wrong
thread), and the still-open access violation where a secondary view's RCWs are released after
its XAML core is gone.

## The blockers

**1. `XamlDirect` does not exist in Windows App SDK. (Verified.)**

Searched every `.winmd` in Windows App SDK 1.8 (`microsoft.windowsappsdk.winui`, `.foundation`,
`.interactiveexperiences`, `.base`, `.dwrite`): zero occurrences of `XamlDirect`,
`IXamlDirectObject`, or `Microsoft.UI.Xaml.Core.Direct`.

What is built on it:

| site | references |
|---|---|
| `Controls/FormattedTextBlock.cs` | 98 |
| `Controls/CustomEmojiIcon.cs` | 6 |
| `Controls/Messages/MessageBubble.xaml.cs` | 5 |
| `Navigation/WindowContext.cs` | 1 |
| `Telegram.Native/NativeUtils.{idl,h,cpp}` | `AddRunToCollection`, the native fast path |

`FormattedTextBlock` uses it for exactly what it is for: recycle queues of `Paragraph`, `Span`
and `Run` created via `CreateInstance(XamlTypeIndex.…)` and mutated via
`SetEnumProperty`/`SetDoubleProperty`/`ClearProperty(XamlPropertyIndex.…)`, bypassing the
`DependencyObject` and CCW cost per inline. That is the app's single hottest surface and the
one CLAUDE.md names first. On WinUI 3 it would have to be rebuilt on ordinary `Inline` objects,
with an unknown — and plausibly worse — cost per message.

This is also the area currently being reworked (the `MessageTextBlock` split), so the two pieces
of work would collide directly.

**2. The VoIP app-model integration. (Unverified — must be answered first.)**

`Telegram/Package.appxmanifest` declares `uap:Extension Category="windows.voipCall"`,
`uap:Capability voipCall`, and `rescap:Capability oneProcessVoIP`. `Services/Calls/VoipCall.cs`
uses `VoipCallCoordinator` for resource reservation, the system call UI, and shell-driven
answer/mute/end. My understanding is that this is UWP-app-model-only and has no packaged-desktop
equivalent, but I could not confirm it from anything installed here — and if it is true, WinUI 3
means losing system call integration, which for a messaging client is a product regression, not
a technical detail.

Also lost, but genuinely not important: `CoreApplication.EnablePrelaunch`,
`backgroundMediaPlayback` (a desktop process simply keeps running), and the `Windows.Universal`
device family, i.e. Xbox and HoloLens.

## What WinUI 3 would buy

- A supported, actively developed XAML stack. `Windows.UI.Xaml` is frozen — it gets no new
  controls and no fixes, and this port has already had to work around one projection defect
  and diagnose a second.
- The desktop app model: no AppContainer restrictions, direct Win32, tray icon, global hotkeys,
  real file paths — and `Telegram.Stub` plus the `FullTrustProcessLauncher` dance could go.
- Multiple windows on one thread, killing the per-view-thread class of bug described above.
- Mica/backdrop, modern title bar, per-monitor DPI without the `ApplicationView` contortions.

## Recommendation

**Do not start the migration. Do the spikes that decide it, now, cheaply.** Phase 1 of the
.NET 10 port is the model: each risk got its own throwaway project before anything in the repo
moved, and all three passed. The same format applies:

1. **`VoipCallCoordinator` in a packaged WinUI 3 app.** Does `GetDefault()` work at all? This
   is the single question most likely to end the discussion, and it is an afternoon.
2. **Replace `FormattedTextBlock`'s XamlDirect path with plain `Inline` objects** in the *current*
   UWP app and measure it. If ordinary inlines are within a few percent, the blocker downgrades
   to a cost; if they are not, WinUI 3 is off the table until either the app's text rendering or
   WinAppSDK changes. Either way the measurement is worth having on its own merits.
3. **One native component rebuilt as desktop C++/WinRT** against WinAppSDK — `SurfaceImage`
   plus `Composition.CompositionDevice` is the representative pair, since it covers both
   `SurfaceImageSource` D2D interop and the shared `CompositionGraphicsDevice`.
4. **RLottie against `Microsoft.Graphics.Win2D`**, in its own repo.
5. **A multi-window spike**: two chat-like windows, one thread, WinUI 3 — does the model the
   app needs actually work, and does it simplify or complicate `WindowContext`?

Until those answers exist, the right posture in day-to-day work is only to avoid making it
worse: keep `Windows.UI.Xaml` usings out of non-UI code, keep composition wrappers in
`Telegram/Composition/`, and keep the native components' XAML surface as narrow as it is today.

## Open questions

- Is there a packaged-desktop replacement for `windows.voipCall`, or is the integration simply
  lost?
- What does `FormattedTextBlock` cost without XamlDirect? Nobody has measured the thing
  XamlDirect is buying.
- Is `Windows.UI.Xaml` frozen-but-supported for long enough that "stay on UWP XAML on .NET 10"
  is a decade answer rather than a two-year one? That is a Microsoft-roadmap question and it
  outweighs every technical item above.
