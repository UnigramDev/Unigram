# Win32 host + UWP XAML islands — the third path

Written 2026-08-21. Companion to `net10-benefits-and-winui3.md`, which assessed .NET 10 and
UWP → WinUI 3 as the only two options. This note assesses a third: **keep `Windows.UI.Xaml`,
but move the process out of the UWP container**, hosting the existing XAML in a
`DesktopWindowXamlSource` inside a Win32 `.exe`.

Everything stated as fact was read out of this repository or out of installed packages, and the
source is named. Anything not verified locally is marked **unverified**.

---

## The answer up front

The objection that killed this before — "islands mean losing .NET Native" — is gone: .NET 10
NativeAOT compiles a Win32 desktop app fine, and `Telegram.Stub` already *is* a
`net10.0-windows10.0.18362.0` process. What is left is a genuinely different trade from the
WinUI 3 one, and on the evidence below it is the better one:

- **The Win32 process is the prize, not the XAML host.** Every item on the wish list — app
  lifecycle, tray, file paths, device access, stock native deps, a title bar that maximizes —
  comes from leaving the AppContainer. Both this path and WinUI 3 deliver all of them.
- **The app-model rewrite is common to both paths.** `ApplicationView`, `CoreApplication`,
  `Window.Current` have to go either way. That cost does not distinguish the options and should
  be struck from the comparison.
- **What *does* distinguish them is the XAML layer.** Islands preserve it entirely — including
  `XamlDirect`, which `net10-benefits-and-winui3.md` identifies as the one *verified hard
  blocker* for WinUI 3 (72 references, still, after the `MessageTextBlock` split). On this path
  that blocker does not exist.
- **And islands are the staging ground WinUI 3 doesn't otherwise have.** A Win32 host can host
  `Microsoft.UI.Xaml` islands *and* `Windows.UI.Xaml` islands. So the migration that cannot be
  done incrementally from UWP can be done incrementally from Win32, one surface at a time,
  whenever WinUI 3 is worth having. Or never.

The gate that decides the whole thing — **does C# + NativeAOT + UWP XAML islands actually
work?** — has been spiked, and it passes outright. Compiled markup, custom types resolved from
markup, merged resource dictionaries, visual states, `XamlDirect`, Composition, input, popups
that escape the window, and **many windows on one thread** — all of it, in a single ~4.8 MB
native exe. Every gate the spike defines passes except 1.7, which was never a capability
question.

Three things that had been listed as risks turned out not to be:

- **`VoipCallCoordinator`** — a UWP workaround the app already degrades without.
- **Popup clipping** — `ShouldConstrainToRootBounds = false` escapes the island fine.
- **Thread-per-view** — came from the app model, not from XAML, so it *goes away* rather than
  being ported. That was previously counted as a WinUI-3-only benefit.

**The remaining cost of this path is app-model work, not XAML work** — and that is work the
WinUI 3 path charges for too. Phase 0 is where it lives, and Phase 0 is worth starting now
regardless of which destination is eventually chosen.

---

## The goal, restated

**Not "migrate to Win32". Add a Win32 host *beside* the UWP one, from one source tree**, and
decide later — possibly never — whether UWP retires. Everything below is written against that.

This is far more reasonable here than on any other path, for one reason: **the XAML layer is
100% shared.** UWP and an island host both run `Windows.UI.Xaml`. Same 475 `.xaml` files, same
2381 usings, same `XamlDirect`, same Composition, same custom controls, same C++/WinRT
components. Nothing forks.

Compare the alternative. Dual-targeting UWP + WinUI 3 would fork **931 of 1259 files** — every
namespace, `XamlDirect` vs no `XamlDirect`, two control sets. Nobody would attempt it. So
"keep both alive" is only an option *because* the XAML host is unchanged.

What actually has to exist twice is the app model, and that is exactly the Phase 0 surface
already inventoried: ~50 `ApplicationView` sites, 23 `GetForCurrentView` singletons, ~45
lifecycle handlers, the pickers and file access, `InputListener`, and `ViewService`. Call it
150 sites. Behind interfaces they mostly already have (`IViewService`, `WindowContext`), a
second implementation is additive, not a fork.

Four things make it cheaper than it sounds:

- **The entry point is already a single switch.** `DISABLE_XAML_GENERATED_MAIN` selects between
  the XAML compiler's `Application.Start` Main and a hand-written one. One define, not an `#if`
  scattered through the app.
- **Precedent exists.** `Telegram.csproj` and `Telegram.Modern.csproj` already build the same
  1259 files two ways.
- **`UseUwp=true` does not imply the UWP app model** (spike, finding 2). The same property that
  gives the UWP project its projections gives the Win32 project its XAML toolchain.
- **The native components may not need rebuilding at all.** `WINAPI_FAMILY_APP` restricts which
  APIs `Telegram.Native` may *call*; it does not stop the resulting DLL loading in a desktop
  process. So the UWP-safe build likely serves both, and Phase 2.3 becomes deferrable until
  something actually wants a desktop-only API. **Unverified** — worth an hour before planning
  around it.

The costs, honestly:

- **A third build configuration** over one source tree. That is one too many, so the sequencing
  is: retire the .NET Native project first, then add the Win32 one. Two at a time, never three.
- **Divergence rot** — the risk `net10-benefits-and-winui3.md` already names about `#if`
  guards. The mitigation is that this seam is interface-shaped rather than `#if`-shaped, and
  that item 0.9's analyzer is what keeps new code from reaching past it. That makes 0.9 more
  important under this goal, not less.

**What it changes in the plan below:** Phase 0 stops being preparation and becomes the
deliverable — it is the abstraction layer that lets both hosts exist. Phase 2 items become "add
a second implementation" rather than "replace", and 2.4 (dropping manifest capabilities) applies
only to the Win32 flavour. Phase 3 is unaffected.

---

## What changed since the last assessment

| | then | now |
|---|---|---|
| AOT | islands ⇒ no .NET Native ⇒ no AOT | NativeAOT works in a plain Win32 exe |
| Precedent | none in C# | still none in C# — Terminal is C++/WinRT (see below) |
| Projection | `Windows.UI.Xaml` stripped from desktop projections | `Microsoft.Windows.UI.Xaml.dll` ships the hosting API — verified, below |
| Win32 half | none | `Telegram.Stub` already exists, already .NET 10, already owns tray + loopback + passkeys |

### Windows Terminal is precedent for the host, not for us

Terminal is a Win32 app hosting `Windows.UI.Xaml` through `WindowsXamlManager` /
`DesktopWindowXamlSource`, shipping to millions. That settles "is the host alive" — it is, and
Terminal is why it stays maintained.

It settles nothing about a C# app. Terminal is C++/WinRT and supplies its own
`IXamlMetadataProvider`; the `Microsoft.Toolkit.Win32.UI.XamlHost` wrappers that made this work
from .NET were archived.

### What Terminal actually does for the title bar — read from its source

`NonClientIslandWindow.cpp`. Worth copying, because it is a bounded recipe rather than
open-ended work, and it is the thing UWP cannot do at all:

- **One island for everything**, not a separate title-bar island. `Initialize` builds a
  `_rootGrid` with two rows — `TitlebarControl` in row 0, client content in row 1.
- **`_OnNcCalcSize`** applies the default frame via `DefWindowProc`, then restores the original
  `top` so the standard caption is gone while the resize borders survive. When maximized (not
  fullscreen) it adds the resize-handle height back: `newSize.top += _GetResizeHandleHeight();`.
- **`_OnNcHitTest`** delegates the frame to `DefWindowProc` and handles only the top border and
  the drag bar. `_GetDragAreaRect` transforms the XAML drag bar's bounds to client pixels.
- **Snap layouts need an input-sink child HWND.** The island is a child window and would swallow
  the mouse, so Terminal puts a separate `_dragBarWindow` over the caption buttons whose
  `_dragBarNcHitTest` returns **`HTMAXBUTTON`** — which is what makes Windows 11 show the
  snap-layouts flyout on hover (GH#9443). `_InputSinkMessageHandler` then handles
  `WM_NCLBUTTONUP` for that region and calls `_titlebar.ClickButton()`.

So the answer to "can a custom caption still maximize and snap" is yes, and the mechanism is
known. What Terminal does *not* give us for free is the multi-window story: theirs is a bespoke
monarch/peasant protocol with a thread per window — but see gate 1.8a, which shows that part is
not actually forced on us.

Two ways Terminal's case does not transfer, in opposite directions: its XAML surface is small
(the terminal itself is D2D on a swapchain), and it **started as Win32**, so islands were pure
upside. We would be running the trade the other way — paying to *leave* an app model.

---

## Pros

- **Deletes `Telegram.Stub` and the `FullTrustProcessLauncher` dance.** The manifest currently
  ships a whole second full-trust process for a tray icon and a loopback exemption —
  `windows.fullTrustProcess` pointing at `Telegram.Stub.exe` with `/SystemTray` and
  `/LoopbackExempt` parameter groups. Tray, passkeys and the app service between them all
  collapse into the main process.
- **Lifecycle stops being adversarial.** 29 `Suspending` + 16 `Resuming` handlers and an
  `ExtendedExecutionSession` that has to be requested and can be revoked, all replaced by "the
  process runs until it exits". `rescap:confirmAppClose` and `EnablePrelaunch` go with them.
- **Real file paths.** 40 `FutureAccessList` sites become paths. `picturesLibrary` /
  `removableStorage` brokering goes away, and so does the per-call broker IPC on media writes.
- **Device and network freedom.** No AppContainer loopback block, no `oneProcessVoIP` rescap,
  ordinary camera/mic enumeration — which is where the recording rework hit the privacy-indicator
  wall.
- **Native dependencies stop being special.** No `WINAPI_FAMILY_APP` subset. ffmpeg, webrtc,
  vlc, tdlib build as stock desktop libraries — directly relevant to the build-reproducibility
  work and to `duplicated-libraries.md`.
- **The title bar ceiling lifts.** The custom-title-bar spike measured hide/minimize/close as
  possible and **maximize never** under UWP. A Win32 window has no such limit, and snap layouts
  come back.
- **`XamlDirect` survives**, and with it `FormattedTextBlock`'s recycle path and
  `NativeUtils::AddRunToCollection`. This is the single biggest difference from WinUI 3.
- **Everything else in the XAML layer survives too**: 2381 `Windows.UI.Xaml` usings, 461
  `Windows.UI.Composition`, 337 `ElementCompositionPreview`, 332 `muxc:` control uses, all 475
  `.xaml` files, both C++/WinRT components, and `RLottie.UWP` in its own repo. None of it is
  touched.
- **Many windows on one thread** — verified, gate 1.8a. This kills the per-view-thread bug class
  (CsWinRT #2524, the secondary-view RCW access violation), which `net10-benefits-and-winui3.md`
  had counted as a WinUI-3-only benefit. It is not: thread-per-view came from the app model, not
  from `Windows.UI.Xaml`.
- **Debugging and profiling become ordinary.** Real profilers, ETW, dump collection without the
  UWP dance — directly useful to the crash-triage harness.
- **It is reversible and incremental.** WinUI 3 islands can be introduced later, per surface,
  in the same process.

## Cons

- **No C# precedent, and the supported .NET wrapper is archived.** **Resolved by the spike.**
  Hosting, input, compiled markup, custom types from markup, resources, visual states,
  `XamlDirect`, Composition and NativeAOT all work from C# without the archived toolkit
  wrappers. The residual cost is build plumbing — the XBF resource layout — not capability.
- **`Windows.UI.Xaml` is frozen.** Choosing this is choosing to stay on a framework that gets no
  new controls and no fixes. Defensible — the app is styled against WinUI 2.8 already and
  controls everything above the framework — but it is a deliberate bet, and it is the same bet
  as "stay on UWP", just with a better process around it.
- **The app-model rewrite is real work** — but it is *not* a con relative to WinUI 3, which
  needs the same work. See Phase 0 for the surface.
- **`VoipCallCoordinator` — downgraded to a non-issue after reading the call sites.** It was
  listed here as the one question that could sink Win32. It is not, and the reason is that the
  integration is *itself* a UWP workaround:

  - **Its entry point is `ReserveCallResourcesAsync`** (`Extensions.cs:752`), which is the UWP
    background-execution reservation — the exact mechanism a Win32 process makes unnecessary.
    `InitializeSystemCallAsync` gates everything else on it succeeding, so the whole block
    exists to buy something a desktop process has for free.
  - **The app already runs without it, by design.** `ApiInfo.IsVoipSupported` is a runtime
    `ApiInformation.IsApiContractPresent("…CallsVoipContract", 1)` check, every call is wrapped
    in `Try…` helpers that swallow exceptions, and `InitializeSystemCallAsync`'s catch nulls
    `_coordinator` and `_systemCall` and carries on. Calls work with `_systemCall == null` —
    that is a supported, already-shipping path, and `WatchDog.TrackEvent("VoipCall", …)` is
    already measuring how often it is taken.
  - **It is currently causing a bug.** Both `RequestNewOutgoingCall` and `RequestNewIncomingCall`
    carry `// TODO: this moves the focus from the call window to the main window :(`. Dropping
    the coordinator fixes that rather than costing anything.

  What is genuinely lost is small and replaceable: `MuteStateChanged` (hardware/shell mute →
  `AudioState`), which becomes `WM_APPCOMMAND` / `APPCOMMAND_MICROPHONE_MUTE_TOGGLE` and then
  works regardless of contract; and `AnswerRequested`/`RejectRequested`, which the app's own
  call window and toast already provide. Three manifest entries go with it — the
  `windows.voipCall` extension, `uap:Capability voipCall`, and `rescap:oneProcessVoIP`, the last
  of which exists purely because UWP wanted the call in a separate background process.
- **CompactOverlay is lost.** 24 sites; `ApplicationViewSwitcher.TryShowAsViewModeAsync` has no
  desktop equivalent. Replaceable with an always-on-top sized window; the gallery PiP and the
  call window are the users.
- **You inherit some of Terminal's chores**: non-client hit-testing by hand (bounded — their
  recipe is above), per-window HWND lifetime, focus and accelerator routing across the island
  boundary, IME. Not their multi-window architecture, though — gate 1.8a.
- ~~**Flyout/popup clipping.**~~ **Tested and not a problem.** A `Popup` with
  `ShouldConstrainToRootBounds = false` escapes the island's HWND and renders over the desktop,
  so the six sites that rely on it — `ChatTextBox:719`, `CaptionTextBox:319`,
  `FormattedTextBox:102`, `ChatRecordBar.xaml:149`, `EmojiMenuFlyout:329`,
  `MessageEffectMenuFlyout:258` — keep working.

  One trap for whoever reads the spike: a default `MenuFlyout` at the bottom edge opens
  *upward*, which looks like clipping and is not. `FlyoutBase.ShouldConstrainToRootBounds`
  defaults to `true`, so that is ordinary placement and happens identically in the UWP app
  today. The spike now shows both variants side by side to keep the two apart.

## What does *not* change either way

`ApplicationData.Current` (58 sites) keeps working in a packaged desktop app. So do toasts,
share target, file type associations, protocol activation, startup task and the Store listing —
MSIX-packaged desktop apps get all of it. Distribution is not at risk on either path.

---

## The gate — **answered, and it opens**

Spiked 2026-08-21 in `C:\Source\XamlIslandSpike`. **A C# desktop process on .NET 10 hosts UWP
XAML in a `DesktopWindowXamlSource`, and `XamlDirect` works inside it.** All seven gates the
spike covers pass:

```
PASS  1.2a DispatcherQueue on thread                    -  controller 0x1da84ca0a10
PASS  1.2b WindowsXamlManager.InitializeForCurrentThread-  ok
PASS  1.2c CreateWindowEx host                          -  hwnd 0x301136
PASS  1.2d DesktopWindowXamlSource.AttachToWindow       -  island hwnd 0x19009c6 (Native2)
PASS  1.2e Stock XAML content                           -  content set
PASS  1.4  XamlDirect inside the island                 -  inline count 1
PASS  1.5  ElementCompositionPreview + animation        -  compositor Compositor
TIME  process start -> island ready                     -  84 ms
```

**That run is the NativeAOT build** — gate 1.6 passes too, and it is the same seven passes as
the JIT build, not a reduced set. Measured over four runs each, same machine, cold-ish:

| | NativeAOT | JIT (self-contained) |
|---|---|---|
| startup, process start → island ready | 75 / 78 / 79 / 82 ms | 203 / 205 / 224 / 229 ms |
| publish output | **1 file, 4.0 MB** | 196 files, 122.4 MB |
| gates passing | 7 of 7 | 7 of 7 |

So UWP XAML, `XamlDirect`, Composition and the island host all survive ILC, in a single
self-contained 4 MB exe with no CsWinRT or runtime assemblies beside it. Nothing about AOT is a
compromise on this path — it is ~2.7× the startup and 1/30th the layout.

Two build notes for whoever repeats this:

- **`BuiltInComInteropSupport` must be `false`.** NativeAOT does not support built-in COM
  interop, which is precisely why `IslandNative` does manual `QueryInterface` + vtable function
  pointers instead of a `[ComImport]` cast. The workaround forced by finding 4 below is what
  makes AOT possible — the two are the same decision.
- **ILC needs `vswhere` on `PATH`.** Without it the link step fails with a mangled command line
  (`MSB3073 ... exited with code 123`) that looks like an AOT failure and is not. Prepend
  `C:\Program Files (x86)\Microsoft Visual Studio\Installer`, or publish from a developer prompt.

### Gate 1.3 — compiled markup — **passes too**

The make-or-break. A `UserControl` with compiled markup, a `StaticResource`, a custom C# type
(`local:Badge`) named from markup, and a `VisualStateManager` — all resolved through the
generated `XamlTypeInfo.g.cs` provider, **in the island, under NativeAOT**:

```
PASS  1.3a new App() - generated IXamlMetadataProvider  -  Application + IXamlMetadataProvider
PASS  1.3b Compiled markup (InitializeComponent + StaticResource)
PASS  1.3c Custom C# type resolved from markup (local:Badge)  -  Badge.Level = 'ok'
PASS  1.3d VisualStateManager.GoToState from markup states    -  went to Highlighted
```

11 of 11 gates, zero failures across four consecutive AOT runs, 227–272 ms to island ready.
**Package identity is not required** — `ms-appx:///` resolves against the exe directory
unpackaged, so the earlier suspicion was wrong; it was purely a file-layout problem.

Five things the real migration has to know:

1. **Both `UseUwpTools=true` and `UseUwp=true` are required, and it must be built with VS
   MSBuild.** `UseUwpTools` is what imports the XAML compiler
   (`Microsoft.Windows.UI.Xaml.CSharp.ModernNET.ImportAfter.targets`), but the targets refuse to
   run without `UseUwp`, which supplies the projections. The imports live in VS's
   ImportBefore/ImportAfter hooks, so the `dotnet` CLI's MSBuild cannot build this — same
   constraint `Telegram.Modern.csproj` already documents.
2. **`UseUwp=true` does not force the UWP app model.** This is the important one: the output is
   still an ordinary Win32 `Exe` with its own `Main` and message loop. `UseUwp` buys the
   projections and the XAML toolchain, not AppContainer.
3. **`DISABLE_XAML_GENERATED_MAIN` is mandatory.** The XAML compiler emits its own
   `Program.Main` calling `Application.Start` — the UWP entry point. An island host owns its
   message loop, so that Main has to be suppressed. This define is precisely the seam between
   the two app models.
4. **An `ApplicationDefinition` (`App.xaml`) is required**, and is where the generated
   `IXamlMetadataProvider` lands. Without one the XAML compiler dies with an internal error
   (`WMC9999: Object reference not set to an instance of an object`), which is not a useful
   diagnostic — remember it.
5. **The XBF layout is wrong out of the box, twice over.** `InitializeComponent` loads
   `ms-appx:///XamlIslandSpike/SpikeCard.xaml` — the assembly-name-prefixed *library* form —
   while the build drops the `.xbf` at the output root, and **`Publish` omits the `.xbf` files
   entirely** (the AOT publish folder held only the exe and pdb). Both are build plumbing, not
   capability limits, but both fail at runtime as an opaque `XamlParseException`. Budget time
   for getting the resource layout right across 475 `.xaml` files.

### Gate 1.8a — many islands, one thread — **passes, and it is a win**

```
PASS  1.2c-e Win32 host + DesktopWindowXamlSource + content  -  hwnd 0x1090d90
PASS  1.8a   Second island on the SAME thread                -  islands on this thread: 2
```

Two HWNDs, two `DesktopWindowXamlSource`s, **one** `WindowsXamlManager`, both hosting compiled
markup, no `CoreApplication.CreateNewView()` and no per-view dispatcher anywhere.

This matters more than it looks. `net10-benefits-and-winui3.md` lists "WinUI 3 supports multiple
windows on one thread, which would delete the entire class of bug this repo has been fighting —
CsWinRT #2524, and the still-open access violation where a secondary view's RCWs are released
after its XAML core is gone" as one of the few genuine arguments *for* WinUI 3. **That argument
applies to this path too.** The thread-per-view model came from the UWP app model, not from
`Windows.UI.Xaml`, so leaving the app model is what removes it — the XAML framework was never
the reason.

So `ViewService`'s rewrite (Phase 0, item 0.4) is not merely a port: on either path it can
collapse to plain HWNDs on the UI thread.

**With this, nothing about the XAML layer is unproven any more.** Hosting, input, markup,
custom types, resources, visual states, `XamlDirect`, Composition and NativeAOT all work
together from C#. What remains open is app-model surface, not XAML.

Four further findings worth carrying forward:

1. **No `UseUwp`, and no VS MSBuild.** A plain `net10.0-windows10.0.26100.0` `Exe` with a direct
   `<Reference>` to `Microsoft.Windows.UI.Xaml.dll` out of the ref pack compiles and runs under
   the ordinary `dotnet build`. The projection is not gated to the UWP app model.
2. **An unpackaged island host needs `maxversiontested` in a side-by-side manifest.** Without it
   `WindowsXamlManager.InitializeForCurrentThread()` fails with `E_UNEXPECTED`
   ("Catastrophic failure") — there is no package manifest to carry the declaration. This was the
   only hard failure hit, and the error message names the fix.
3. **A `DispatcherQueueController` must exist on the thread first** (`CreateDispatcherQueueController`
   from `coremessaging.dll`, `DQTYPE_THREAD_CURRENT`). `CoreWindow` used to supply this.
4. **`[ComImport]` casts do not work on CsWinRT-projected objects.**
   `(IDesktopWindowXamlSourceNative2)(object)source` throws `InvalidCastException` — unlike the
   `(ICoreWindowInterop)(object)window` pattern in `WindowContext.cs`, which works because that
   object comes from the UWP projection. The fix is `WinRT.MarshalInspectable<T>.FromManaged`,
   a manual `QueryInterface`, and vtable calls through function pointers. That is more code but
   it is also NativeAOT-clean, which gate 1.6 wants anyway. `IslandNative.cs` in the spike is the
   reusable version.

**`XamlDirect` passing is the headline.** It is the one *verified hard blocker* for WinUI 3, and
it does not exist on this path — `FormattedTextBlock`'s recycle queues and
`NativeUtils::AddRunToCollection` would move across unchanged.

What the spike has **not** answered: only **1.7**, the custom non-client caption. And that is
now the least risky item on the list, because Terminal's `NonClientIslandWindow.cpp` is a
working implementation of exactly it — see the recipe above. It is a day of `WM_NCCALCSIZE` /
`WM_NCHITTEST` work, not an unknown.

### Background: what was known before the spike

`Microsoft.Windows.SDK.NET.Ref` 10.0.26100.57 ships
`Microsoft.Windows.UI.Xaml.dll` (7.3 MB) as a *separate* assembly from
`Microsoft.Windows.SDK.NET.dll`, and it contains the hosting API:

| type | in projection |
|---|---|
| `Windows.UI.Xaml.Hosting` namespace | yes (102 metadata hits) |
| `WindowsXamlManager` | yes |
| `DesktopWindowXamlSource` | yes |
| `XamlSourceFocusNavigationRequest` | yes |
| `Windows.UI.Xaml.Core.Direct` / `XamlDirect` | yes |

So the projection surface exists and is reachable. What is still unknown, in order of how likely
it is to end the discussion:

1. Can `Microsoft.Windows.UI.Xaml.dll` be referenced from a **desktop** TFM, or does `UseUwp`
   gate it to the UWP app model? (`UseUwp` is not in the .NET SDK — it comes from VS's
   ImportBefore/ImportAfter hooks, per the comment in `Telegram.Modern.csproj`.)
2. Does `WindowsXamlManager.InitializeForCurrentThread()` succeed in a desktop process, called
   from C#?
3. Can a **C#** `IXamlMetadataProvider` — the generated `XamlTypeInfo.g.cs` — be registered so
   custom controls and `ResourceDictionary` merges resolve? This is the archived-wrapper
   question and the one most likely to fail.
4. Does NativeAOT link and run the result?
5. Do `XamlDirect` and `ElementCompositionPreview` work *inside* an island?

`C:\Source\XamlIslandSpike` answers these. See the todo.

---

# Todo

## Phase 0 — reduce UWP coupling in the current app

Everything here is worth doing **on its own merits**, ships incrementally on `develop`, needs no
decision about the destination, and pays off identically for UWP-forever, islands, or WinUI 3.
Start here regardless of what the spike says.

The good news from the survey: `WindowContext` is already the funnel — 18 of the 50
`ApplicationView` sites are in that one file, it already exposes `Handle` via
`ICoreWindowInterop`, and it already drives a `DispatcherContext` off `DispatcherQueue` rather
than `CoreDispatcher`. Most of this phase is finishing a job that is well started.

- [ ] **0.1 Dispatchers — much narrower than it first looked.** Two things measured in the spike
  (gate 1.9) change the shape of this:

  - **`FrameworkElement.Dispatcher` still works inside an island** — it returns a live
    `CoreDispatcher`, *not* null. So the ~20 sites doing `SomeElement.Dispatcher.RunAsync(...)`
    (`ReactionButton`, `ChartCell`, `ContentPopup`, `GroupCallMessageCell`, `SendFilesPopup`,
    `Extensions`, …) keep working unchanged on this path. Do **not** sweep them.
  - **There is no 1:1 replacement to sweep them *to*.** UWP's `DependencyObject` has no
    `DispatcherQueue` property — it does not compile. That property is a WinUI 3 addition.

  So this is not a 26-site mechanical conversion. What actually needs work is only the handful
  that reach a dispatcher through types that *do* disappear:

  - **`Navigation/InputListener.cs` — the real one.** Built entirely on `Window`:
    `_window.Dispatcher.AcceleratorKeyActivated` and `_window.CoreWindow.PointerPressed`. This is
    the app's global keyboard entry point (Escape, back/forward, gamepad shoulders), so it has to
    be rebuilt on the island root's `KeyDown`/`PreviewKeyDown` or on the host's message loop —
    which already runs `PreTranslateMessage` for exactly this reason. Small class, real rewrite.
  - `Views/InstantPage.xaml.cs:91` — a second `AcceleratorKeyActivated` handler, same treatment.
  - `Services/ViewService/ViewService.cs:118` — `CoreApplication.MainView.Dispatcher.RunAsync`;
    goes with `CoreApplication` in 0.4.
  - `Services/ViewService/ViewLifetimeControl.cs:232` — `Window.Dispatcher.RunAsync`; same.

  The sweep only becomes necessary for Phase 3, where `DependencyObject.Dispatcher` returns null
  and `DispatcherQueue` replaces it. Worth knowing, not worth pre-paying.

- [ ] **0.2 Seal `WindowContext` — no `Window`, no `CoreWindow` leaking out.** Three members
  publish the UWP types to the rest of the app:
  - `public static implicit operator Window(WindowContext)` — the widest hole; find its users
    and give them named members instead.
  - `public CoreWindow CoreWindow => _window.CoreWindow`
  - `public CoreWindowActivationMode ActivationMode` — return an app-owned enum.

  After this, `Navigation/WindowContext.cs` is the only file in the app that names either type.

- [ ] **0.3 Move the `ApplicationView` surface onto `WindowContext` members.** The set actually
  used is small and every one has a Win32 answer, so the shim is thin:

  | used today | sites | desktop equivalent |
  |---|---|---|
  | `Title` | 3 | `SetWindowText` |
  | `TitleBar` (via `CoreApplication.GetCurrentView()`) | 3 | custom caption + `WM_NCHITTEST` |
  | `Consolidated` | 5 | `WM_CLOSE` / window destroy |
  | `VisibleBoundsChanged` | 3 | `WM_SIZE` / `WM_DPICHANGED` |
  | `TryEnterFullScreenMode` / `ExitFullScreenMode` / `IsFullScreenMode` | 7 | style swap + `SetWindowPos` |
  | `IsScreenCaptureEnabled` | 2 | `SetWindowDisplayAffinity` |
  | `TryResizeView` / `SetPreferredMinSize` | 2 | `SetWindowPos` / `WM_GETMINMAXINFO` |
  | `PersistedStateId` | 1 | settings |
  | `TryConsolidateAsync` / `Id` | 4 | window close / HWND |
  | `IsViewModeSupported` | 1 | drop with CompactOverlay |

- [ ] **0.4 Put `IViewService` in front of the view model.** `ViewService.cs` +
  `ViewLifetimeControl.cs` are 21 `ApplicationView` sites over `CoreApplication.CreateNewView`,
  `ApplicationViewSwitcher.SwitchAsync` (11) and `TryShowAsStandaloneAsync` (4). The *interface*
  is already the right shape — keep it, and make the UWP implementation one of two.
  This is the multi-month piece in every path; getting the seam right now is what makes it
  survivable later.

- [ ] **0.5 Wrap the remaining `GetForCurrentView` singletons.** The complete surveyed surface,
  generated code excluded. `ApplicationView` is item 0.3; `ViewLifetimeControl` is our own type.

  | type | sites | where | what is used | desktop replacement |
  |---|---|---|---|---|
  | `ConnectedAnimationService` | 12 | Gallery, `ProfileHeader`, `VoipPage`, `ChatView`, `EditMediaPopup`, `SendFilesPopup` | `PrepareToAnimate`, `GetAnimation`, `DefaultEasingFunction` | survives; needs a per-window owner (0.5b) — same in WinUI 3 |
  | `SystemNavigationManagerPreview` | 8 | `OverlayWindow`, `VoipPage`, `TextEditorRichPopup`, `WebAppPage` | `CloseRequested` only | **deleted** — per-HWND `WM_CLOSE` |
  | `ResourceContext` | 6 (4 live, 2 commented) | `MessageHelper`, `FrameFacade`, `SettingsLanguageViewModel` | `.Reset()` after a language change | MRT Core `ResourceManager` |
  | `SystemNavigationManager` | 3 | `BootStrapper`, `WindowContext` | `BackRequested`, `AppViewBackButtonVisibility` | **deleted** — Xbox/tablet-mode legacy; keep only mouse-back via `WM_APPCOMMAND` |
  | `CompositionCapabilities` | 3 | `PowerSavingPolicy`, `DiceView`, `DiagnosticsViewModel` | `AreEffectsFast()` | works on desktop; GPU capability, not really per-view |
  | `UIViewSettings` | 1 | `GalleryWindow:1079` | `UserInteractionMode` | `IUIViewSettingsInterop::GetForWindow(hwnd)` |
  | `SystemMediaTransportControls` | 1 | `PlaybackService:230` | the whole transport | `ISystemMediaTransportControlsInterop::GetForWindow(hwnd)` |
  | `DisplayInformation` | 1 | `MasterDetailView:843` | — | per-window display info / monitor APIs |

  **The shape of the work is one pattern, not eight.** Most of these have a documented
  `…Interop::GetForWindow(HWND)` sibling — `IUIViewSettingsInterop`,
  `ISystemMediaTransportControlsInterop`, `IDataTransferManagerInterop`, `IInputPaneInterop`,
  `IPrintManagerInterop`, plus `IInitializeWithWindow` for the pickers in 0.7. So 0.5 and 0.7
  are the same job: **get an HWND to the call site.** `WindowContext.Handle` already exposes one
  (via `ICoreWindowInterop`), which is why 0.2 should land first.

  Not affected, and worth knowing so nobody "fixes" them:
  - `DispatcherQueue.GetForCurrentThread()` — 22 sites, works unchanged on desktop.
  - `UISettings` — 11 references but constructed with `new UISettings()`, not per-view. Fine.
  - `DataTransferManager` — only `IsSupported()` is used (`ChooseChatsViewModel:509`). If share
    UI is ever invoked, that needs `IDataTransferManagerInterop::ShowShareUIForWindow`.

- [ ] **0.5b `ConnectedAnimationService` needs a per-window owner — the one real catch behind
  1.8a.** Gate 1.8c: with two islands on one thread,
  `ConnectedAnimationService.GetForCurrentView()` returns **the same instance** for both, while
  their `XamlRoot`s differ. "Current view" in an island host means current *thread*.

  With 12 sites keyed on plain strings — `"FullScreenPicture"` in the gallery,
  `"EditMediaPopup"` in `SendFilesPopup`/`EditMediaPopup` — two windows running the same
  animation key at once would collide. Today each view has its own thread, so it cannot happen.

  **This is not a cost of *this* path.** `ConnectedAnimationService.GetForCurrentView()` behaves
  the same way in WinUI 3 (**unverified locally** — WinAppSDK is no longer in the package cache;
  asserted by Fela and consistent with WinUI 3's one-thread-many-windows model). It is a
  property of putting many windows on one thread, which is the *goal* on both paths. Handle it
  when 0.4 lands; it is 12 sites and two string keys.

  ~~The other two singletons listed here originally.~~ Both were me over-applying 1.8c to APIs
  that do not survive the move at all:

  - **`SystemNavigationManagerPreview.CloseRequested`** (8 sites) is not re-keyed, it is
    *deleted*. It exists because UWP will not let an app intercept window close — that is what
    `rescap:confirmAppClose` is for. In Win32 every HWND gets its own `WM_CLOSE`, which is
    inherently per-window, so the sharing problem cannot arise.
  - **`SystemNavigationManager`** (3 sites) is deletion too, and the code already says so:
    `BootStrapper.cs:83` is commented `// WARNING: this is used by Xbox (and some Windows
    users)`, and `WindowContext.cs:178` sets `AppViewBackButtonVisibility = Collapsed` from
    inside a `#region Legacy code`. It was for Xbox and Windows 10 tablet mode; Xbox support is
    gone and Windows 11 has no tablet mode. The only piece worth keeping is the mouse back
    button, which becomes `WM_APPCOMMAND` / `APPCOMMAND_BROWSER_BACKWARD` — more reliable than
    what it replaces.

- [ ] **0.6 Narrow the `CoreWindow` uses.** 44 sites, mostly input.
  - [x] **`PointerCursor` — 24 sites, done.** `WindowContext.SetPointerCursor(PointerCursorType)`
    with an app-owned enum and one cached `CoreCursor` per type. Removed the per-call
    `new CoreCursor(...)` allocation — `FormattedTextBlock` was doing it on pointer move —
    and deleted five hand-rolled cursor caches in `ImageTextSelection` and
    `MasterDetailPanel`. Five files no longer import `Windows.UI.Core` at all. Builds.
  - [ ] `CharacterReceived` 10, `ActivationMode` 3, `GetAsyncKeyState` 2, `PointerPressed` 2,
    `FlowDirection` 2, `PointerPosition` 1, `ResizeStarted`/`ResizeCompleted` 2 each.

- [ ] **0.7 Route file access through a path-first helper.** 40 `FutureAccessList` sites, 25
  `StorageFile.` statics, 12 `FileOpenPicker`, 6 `CameraCaptureUI`, 4 `FileSavePicker`, 3
  `FolderPicker`, 2 `KnownFolders`, 28 `Launcher.`. Adding an HWND parameter at this seam is
  what makes `IInitializeWithWindow` a one-line change later instead of 90.
  Leave `ApplicationData.Current` (58) alone — it works on desktop.

- [ ] **0.8 Collect the lifecycle handlers.** 29 `Suspending` + 16 `Resuming` behind one
  app-owned event pair, so the desktop implementation can raise them on power/session
  transitions — or never.

- [ ] **0.9 Keep it from getting worse.** Extend the `TG100x` analyzer set with a rule that
  flags `ApplicationView`, `CoreApplication`, `Window.Current` and `CoreWindow` outside
  `Navigation/` and `Services/ViewService/`. Cheap, and it is the only thing that stops 0.2–0.6
  from silently regrowing.

## Phase 1 — the spike (`C:\Source\XamlIslandSpike`)

Answers the gate. Throwaway, in the pattern of `CustomTitleBarSpike` / `DustSpike`. Ordered so
the most likely failure comes first.

- [x] **1.0 `VoipCallCoordinator`** — closed by reading the call sites rather than by spiking
  it. The integration is gated on `ReserveCallResourcesAsync`, i.e. the UWP background-execution
  reservation a Win32 process does not need; the app already degrades to `_systemCall == null`
  on a supported path; and it is currently the cause of the call-window focus bug. Whether it
  works on packaged desktop stopped mattering. See the Cons entry.
- [x] **1.1** Desktop `net10.0-windows10.0.26100.0` exe that references
  `Microsoft.Windows.UI.Xaml.dll` and compiles against `Windows.UI.Xaml.Hosting`. **Passes** — no
  `UseUwp`, plain `dotnet build`.
- [x] **1.2** `WindowsXamlManager.InitializeForCurrentThread()` + a `DesktopWindowXamlSource`
  attached to a plain Win32 HWND, showing a stock `Button`. **Passes**, given a
  `DispatcherQueueController` on the thread and `maxversiontested` in the app manifest.
- [x] **1.3** A **C# custom control** with its own `ResourceDictionary` and a custom
  `VisualStateManager`, resolved through the generated `XamlTypeInfo` metadata provider.
  **Passes**, incl. under AOT. Needs `UseUwpTools`+`UseUwp`, VS MSBuild,
  `DISABLE_XAML_GENERATED_MAIN`, an `App.xaml`, and a corrected XBF layout.
- [x] **1.4** `XamlDirect.GetDefault()` inside the island — create and mutate a `Run` the way
  `FormattedTextBlock` does. **Passes.** This is the WinUI 3 blocker, and it is not one here.
- [x] **1.5** `ElementCompositionPreview.GetElementVisual` + a running `Compositor` animation.
  **Passes.** `SurfaceImageSource` fed from D2D is still to do.
- [x] **1.6** `PublishAot=true`. **Passes** — 4.0 MB single-file exe, 79 ms median startup
  against 215 ms for the JIT layout, all seven gates unchanged. Needs
  `BuiltInComInteropSupport=false` and `vswhere` on `PATH`.
- [ ] **1.7** Non-client: custom caption that **maximizes and snaps** — the thing UWP cannot do.
- [x] **1.8a** Two islands on the **same thread**. **Passes** — two HWNDs, two
  `DesktopWindowXamlSource`s, one `WindowsXamlManager`, both hosting compiled markup. UWP's
  one-thread-per-view came from `CoreApplication.CreateNewView()`, not from XAML, so islands are
  not bound by it. See below — this is a *benefit*, not a parity item.
- [x] **1.8b** Popups and flyouts near the island edge. **Passes** — a `Popup` with
  `ShouldConstrainToRootBounds = false` opens *outside* the window, over the desktop, from
  inside the island. The six composer sites survive.
- [x] **1.8c** What `GetForCurrentView()` resolves to with many islands on one thread.
  **Answered, and it is a caveat, not a pass**: `ConnectedAnimationService` is the *same*
  instance for both islands while their `XamlRoot`s differ. Per-view singletons are really
  per-thread. Feeds Phase 0 item 0.5b.
- [x] **1.9** What `DependencyObject.Dispatcher` returns inside an island. **Present, not
  null** — unlike WinUI 3. And `element.DispatcherQueue` does not compile on UWP at all.
  Together these shrink Phase 0 item 0.1 to a handful of sites; see it for the detail.

Record results back in this file. If 1.3 fails and cannot be worked around, close the path and
say so here.

## Phase 2 — the second host, added beside the first

- [ ] **2.0** Retire the .NET Native project first. Three build configurations over one source
  tree is one too many; two is the standing arrangement.
- [ ] **2.1** Grow `Telegram.Stub` into the host process rather than creating a new one — it is
  already .NET 10, already Win32, already owns the tray and passkeys.
- [ ] **2.2** Desktop `IViewService` / `WindowContext` implementations behind the Phase 0 seams.
- [ ] **2.3** Check whether `Telegram.Native` / `Telegram.Native.Calls` need rebuilding at all —
  a UWP-safe DLL should load fine in a desktop process. If so, this is deferred indefinitely;
  if not, it is 37 + 21 of 250 files.
- [ ] **2.4** Manifest for the **Win32 flavour only**: drop `runFullTrust`,
  `windows.fullTrustProcess`, `confirmAppClose`, `oneProcessVoIP`, `packageManagement`,
  `picturesLibrary`, `removableStorage`; keep the extensions. The UWP manifest is untouched.
- [ ] **2.5** Retire `Telegram.Stub`'s IPC, app service and loopback exemption **on the Win32
  flavour**. They stay for as long as the UWP flavour ships.
- [ ] **2.6** Re-point the native dependency builds off the `WINAPI_FAMILY_APP` subset.

## Phase 3 — optionality, not a commitment

- [ ] **3.1** Prove a `Microsoft.UI.Xaml` island alongside a `Windows.UI.Xaml` one in the same
  process. That is what makes a per-surface WinUI 3 migration possible at all.
- [ ] **3.2** If and only if it is ever worth it, port surfaces one at a time — starting with the
  ones that do *not* use `XamlDirect`.

---

## Open questions

- ~~Can a packaged desktop app use `VoipCallCoordinator`?~~ **Closed** — it does not matter.
  See Cons. The remaining VoIP question is much smaller: route hardware mute through
  `WM_APPCOMMAND` instead.
- ~~Does `UseUwp` gate the XAML projection to the UWP app model?~~ **Closed** — no. `UseUwp`
  supplies projections and the XAML toolchain; the output is still an ordinary Win32 exe.
- ~~Can C#-generated XAML metadata be registered in an island?~~ **Closed** — yes, gate 1.3,
  including under NativeAOT. What is left is the XBF resource layout, which is build plumbing.
- **How long is `Windows.UI.Xaml` supported?** The same roadmap question
  `net10-benefits-and-winui3.md` ends on, and it still outweighs every technical item here —
  though this path is strictly less exposed to it than staying on UWP, because the process is
  no longer tied to the framework.
