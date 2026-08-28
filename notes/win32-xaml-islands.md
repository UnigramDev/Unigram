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

`ApplicationData.Current` (49 sites) keeps working in a packaged desktop app. So do toasts,
share target, file type associations, protocol activation, startup task and the Store listing —
MSIX-packaged desktop apps get all of it. Distribution is not at risk on either path.

**Settings are now the exception that no longer needs the app container at all.** As of
2026-08-22 they sit behind `ISettingsStore`
(`Telegram/Services/Settings/SettingsStore.cs`), and `ApplicationData.Current.LocalSettings` is
referenced from exactly **one live line** in the whole app —
`ApplicationDataSettingsStore.Local`. Everything else goes through the two entry points,
`AppSettings` and `ISettingsService`. An unpackaged host therefore needs one new `ISettingsStore`
implementation and one line changed, rather than touching ~200 accessors. The remaining 45
`ApplicationData.Current` sites are `LocalFolder` (35) and `TemporaryFolder` (10) — file access,
which 0.7 covers and which is fine on desktop regardless.

The seam is not free of work: the interface has a `Flush()` that `ApplicationDataSettingsStore`
implements as a no-op, because `ApplicationData` persists as it goes. Any file-backed store has
to make that real, and needs a call from suspend and close. See
`notes/settings-service-refactor.md` §4.1 and step 6.

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

Five further findings worth carrying forward:

0. **The island host still has a `CoreWindow`.** `WindowsXamlManager.InitializeForCurrentThread()`
   creates a hidden one on the thread, so `CoreWindow.GetForCurrentThread()` is non-null and
   `CoreWindow`-bound WinRT APIs keep working without HWND interop. Item 0.1 below is the
   corroboration already in this note: `FrameworkElement.Dispatcher` returns a live
   `CoreDispatcher` inside an island, and a `CoreDispatcher` comes from a `CoreWindow`. This is
   Phase 2 only — Phase 3 (WinUI 3) has no `CoreWindow`, which is where
   `SystemMediaTransportControls.GetForCurrentView`, `UIViewSettings.GetForCurrentView` and the
   rest of the `GetForCurrentView` family start needing interop. Sealing `CoreWindow` behind
   `WindowContext` (item 0.2) is still right — it is what makes Phase 3 a change to one file — but
   nothing on this path is *blocked* on it.

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

**Every gate now passes, 1.7 included** — the spike has no open questions left. The custom
non-client caption works: the window has no system caption at all, XAML reaches the top edge,
and dragging, double-click maximize, the system menu, the top resize border, the three caption
buttons and the Windows 11 snap layouts flyout all behave.

The one part that is not free is **snap layouts**, and Terminal marks it `// BODGY`: the island's
core input site covers the whole window and steals `WM_NCHITTEST`, so returning `HTMAXBUTTON`
where the maximize button is requires a separate `WS_EX_LAYERED | WS_EX_NOREDIRECTIONBITMAP`
drag-bar HWND laid over the buttons, which then forwards hover and press back to the XAML ones by
hand (`_InputSinkMessageHandler`, plus `TRACKMOUSEEVENT` bookkeeping).

And there is no downside case: UWP with a custom caption cannot maximize at all — measured, see
the custom title bar spike — so the Win32 host is strictly better than what ships today even if
snap layouts were skipped entirely.

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

**Where it stands, 2026-08-23.** `Window.Current` 73 -> 22 mentions, of which 5 are comments and
most of the rest are the seam itself (`WindowContext`, `BootStrapper`) or 0.4's rewrite.
`WindowContext.Current` 124 -> 42, and the distribution is now lopsided in a useful way:
`BootStrapper` (14) and `App.xaml.cs` (6) are bootstrap code that forks anyway, `ViewService` (3)
is 0.4, and the remaining ~15 are ordinary UI sites in four property families — `ActualTheme`,
`RasterizationScale`, `Bounds`/`PointerPosition`, and `NavigationServices`/`Content`/`Title`.

**How to reach the window, in order of preference** (Fela's rule, and it corrects an earlier
draft of this paragraph that reached for `ForXamlRoot` first):

1. **`ViewModel.Window`**, or `NavigationService.Window` — whenever a view model or a navigation
   service is in reach. It does not depend on the element being in the tree, and it says where
   the window came from.
2. **A cached `WindowContext` or `Theme`**, resolved once at a hook where the element is attached
   and held in a field — what `MessageBubble` does at `Loading`.
3. **`WindowContext.ForXamlRoot(...)`** — last resort, for a leaf control with neither.

The reason is not taste. `ForXamlRoot` resolves through `XamlRoot`, which is null until the
element is attached (0.17), so it is a trap in `OnNavigatedTo`, in constructors and anywhere
before `Loading` — three of the theme update sites were converted to it and two had to be
changed again. It also costs a lookup per call and hides which window is meant. `Theme.Current` 50 -> 25, of which 15 are the `MonospaceFontFamily`
/ `XamlAutoFontFamily` pair still waiting on 0.19a.

UI `[ThreadStatic]` is down to `Theme` (9), `WatchDog` (5, diagnostics), `ProfileCell` (4),
`MessageBubbleBrush` (1, dead code) and `Direct2D` (1); `PlaceholderHelper`'s is gone. The
per-`XamlRoot` `ConditionalWeakTable` pattern is now applied across `OverlayWindow`,
`AnimatedImage`, `ProfilePicture`, `ContentPopup` and `RelativeDateService`, and
`DispatcherContext.Current` replaced the `WindowContext.Current.Dispatcher` hops.

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

  - [x] **`Navigation/InputListener.cs` — done 2026-08-24, and the advice here was wrong.**
    It said to rebuild on the island root's `KeyDown`/`PreviewKeyDown` *or* the host's message
    loop. Routed events are not an option, for two reasons that only turned up on contact:

    - `AcceleratorKeyActivated` distinguishes `SystemKeyDown`, and the handler depends on it —
      `Alt+Left` and `Alt+Back` go through `VirtualKeyModifiers.Menu`.
    - It fires *before* the tree, so it still sees keys a focused `RichEditBox` swallows, and
      keys pressed while focus is in a WebView2, which is not in the XAML tree at all.

    Fela had already tried the routed-event route once, without success, which is very likely
    the same wall. So: **the message loop, and a fork of the class.**

    The shape matters as much as the choice. The first attempt raised app-owned events off
    `WindowContext` and fed them per host — which reads well, but moved the `CoreWindow`
    subscriptions in the *shipping* app, changed `IShortcutsService.Process`, and put an args
    allocation on every keystroke. Fela: *"I would prefer to keep UWP as it is as it ships to
    millions"*, and *"if you have to fire a new event try to cache the args instead of recreating
    per key stroke/mouse click"*. Forking removes the question entirely — no event, no args:

    - `InputListener.cs` became `InputListener.Uwp.cs`, **content byte-identical** (git reports a
      pure rename); only its `Telegram.csproj` entry changed.
    - `InputListener.Win32.cs` implements `IMessageFilter`, which `IslandWindow` consults ahead
      of its own accelerator handling. `WM_KEYDOWN`/`WM_SYSKEYDOWN` carry the virtual key in
      `wParam`; `WM_XBUTTONDOWN` carries the button chord in its low word, so the back/forward
      gesture reads it directly with no `PointerPoint`.
    - `ShortcutsService` gained a `Process(VirtualKey, out modifiers)` **overload** rather than a
      changed signature, so the UWP call site is untouched.

    The decision logic is a deliberate copy, not a shared base: the two halves differ only in
    where the key comes from, and the UWP one ships. About forty lines, and the rule that bought
    it — **an edit to a shipping path needs a better reason than symmetry** — is worth keeping
    for the rest of the fork.
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

- [ ] **0.4 Split `ViewService`, and leave `ViewLifetimeControl` alone.** Fela's design, and it
  is far smaller than this item has looked all along, because **`ViewLifetimeControl` does not
  actually leak** — measured 2026-08-24:

  - Both callers discard the return: `VoipCall.cs:756` and `VoipGroupCall.cs:2051` are
    `_ = service.OpenAsync(options);`. So does every `await OpenAsync(...)` in
    `TLNavigationService`. **No caller anywhere reads the returned control.**
  - Every `Content` callback ignores the parameter: the two `CreatePresentation` methods and the
    seven `(control, window) =>` lambdas in `TLNavigationService` all read only `window`.

  So the whole leak was three signatures.

  - [x] **Step 1 — behaviour-preserving, done 2026-08-24.**

        Func<ViewLifetimeControl, WindowContext, UIElement> Content  ->  Func<WindowContext, UIElement>
        Task<ViewLifetimeControl> OpenAsync(...)                     ->  Task<WindowContext>

    plus the two `NavigationService` forwarders, the seven lambdas and the two
    `CreatePresentation` bodies. **`IViewService` now names no UWP type**, and
    `ViewLifetimeControl` is confined to its own folder — the only mentions left outside it are
    in `SecondaryViewSynchronizationContextDecorator`, which is UWP-only by nature.

    Three things fell out that were not in the plan:

    - `WindowContext.Id` **is** the view id (`GetApplicationViewIdForWindow`, same as
      `ViewLifetimeControl.Id`), so `TryShowAsViewModeAsync`/`TryShowAsStandaloneAsync`/
      `SwitchAsync` take it unchanged.
    - `OpenAsyncInternal` called `ViewLifetimeControl.GetForCurrentView()` purely to have
      something to return. Dropping it is safe: `GetOrAdd` means the control is already created
      by `ViewService.OnWindowCreated`, which must have run for `WindowContext.Current` to be
      valid in the same callback.
    - The chat-already-open search did `oldControl = ViewLifetimeControl.GetForCurrentView()`
      inside a `ForEachAsync` body — correct only because the body is dispatched to that window's
      thread. It is now just `oldWindow = window`, which is what it always meant.
    - `ViewLifetimeControl.Facade()` was left with no callers (`FacadeAsync` returns
      `WindowContext.Current` now), so it went, and with it the ctor's `newWindow == null`
      Xbox branch and the unused parameter.

  - [x] **Step 2 — done 2026-08-24, and it is 100 lines.** `ViewService.cs` keeps the interface,
    the enum, the options and `_mainWindowCreated`; `ViewService.Uwp.cs` takes every path that
    names `CoreApplication.CreateNewView`, `ApplicationViewSwitcher` or `ViewLifetimeControl`;
    `ViewService.Win32.cs` makes another `IslandWindow` **on the same thread** and wraps it in a
    `WindowContext`. Gate 1.8a is what makes that legitimate, and item 0.18 is why no secondary
    window needs a thread of its own.

    `ViewLifetimeControl` never moved, exactly as planned - it and
    `SecondaryViewSynchronizationContextDecorator` were simply renamed `.Uwp.cs` so the Win32
    project's `**\*.Uwp.cs` exclusion drops them. **All 21 `ApplicationView` sites went with it and
    were never ported.**

    Two things the Win32 half does differently, both deliberate:

    - **`ViewMode` is ignored.** `CompactOverlay` is a UWP app model feature with no Win32
      equivalent; picture-in-picture becomes a small topmost window the app positions itself,
      which is Tier 2 of the fork list rather than this item.
    - **Content is assigned through `WindowContext.Content`** rather than passed to
      `IslandWindow.Create`, so a secondary window takes the same path as the main one - building
      the `WindowControl`, merging the chat theme, publishing the XamlRoot.

    It also needed `IsInMainView` on the Win32 side, which nothing had set: UWP asks
    `CoreApplication` which view it is in, and here the answer is whichever window came first.
    The chat-already-open search depends on it.

  This was described here as "the multi-month piece in every path". That was wrong: the mass is
  all inside the class that isn't moving.

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
  - [x] **`CharacterReceived` — 10 sites, done.** Replaced with the routed
    `UIElement.CharacterReceived`, which works in an island because it is XAML input rather than
    CoreWindow. Three shapes, because the sites were doing three different things:
    - `FormattedTextBox` (its own emoticon replacement) subscribes to **itself**, via
      `AddHandler(..., handledEventsToo: true)` — a plain `+=` never fires, because RichEditBox
      consumes the character and marks the event handled. Killed the `_characterReceived`
      double-subscription guard and both `Loaded`/`Unloaded` handlers with it: they existed only
      because `CoreWindow` outlives the control.
    - `MainPage` / `ChatView` (type-to-search, type-to-compose) go through a new
      `WindowContext.CharacterReceived`, raised from the window's root element with a plain `+=`.
    - `ChooseChatsPopup` / `SendFilesPopup` subscribe to **themselves** — a popup is its own
      subtree, so anything focused above it never bubbles down.

    What made it worth doing beyond the port: **every `FocusManagerEx.TryGetFocusedElement` and
    `GetOpenPopupsForXamlRoot` check was hand-rolling what routed events already encode.** A
    focused `TextBox`/`RichEditBox` marks the character handled, and a popup only swallows it
    when it took focus — which is exactly what `ChatView`'s hardcoded allow-list
    (`ToolTip`, `TeachingTipRootGrid`, `ReactionAnimation`, empty `Grid`) was enumerating: the
    popups that do *not* take focus. All of it deleted. Also gone: two allocations per keystroke
    (`Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode))`) in each of four handlers
    that were **all** subscribed to the same CoreWindow, so every keystroke anywhere paid it 4x.

    **The trap, found by testing.** Two shortcuts used to ride in on the character stream,
    because `CoreWindow.CharacterReceived` sat *below* XAML's key handling. The routed event sits
    *above* it, so they behave differently:
    - **Ctrl+V still arrives** as U+0016 (SYN — Ctrl+letter is letter minus 0x40). The paste
      branches survive untouched.
    - **Enter does not.** XAML consumes it as a key before any character is produced, so the
      `'\r'` branch in both send popups went dead and Enter stopped closing them. Restored as an
      explicit `PreviewKeyDown` handler calling `Accept()`, guarded by the same four focused
      types. Preview rather than KeyDown because a focused `ListViewItem` sits in a `ListView`
      that may consume Enter for item invocation, so bubbling never reaches the popup.

    Worth knowing that `ContentPopup.OnProcessKeyboardAccelerators` *also* maps Enter to
    `Hide(ContentDialogResult.Primary)` with an identical focus check, and appears never to fire
    for these two — the `'\r'` branch was the live path. Whatever suppresses it is unexplained;
    do not assume the base class covers Enter for a `ContentPopup`.

    Verified by hand: type-to-search, type-to-compose, emoticon replacement, Ctrl+V paste, and
    Enter from a focused list item closing the popup with the caption intact.
  - [x] **`ActivationMode` — 6 sites, done.** No app-owned enum was needed: five of the six
    asked `!= Deactivated`, so they became `WindowContext.IsActive`. Only
    `ChatView.Window_Activated` wants the three-way split, and it ignores the middle state, so
    `IsForeground` plus `!IsActive` reproduces it exactly. That site was also reading
    `Window.Current` while handling *its own* window's `Activated` event — wrong in a secondary
    view; it now uses `NavigationService.Window`.
  - [ ] `GetAsyncKeyState` 2, `PointerPressed` 2, `FlowDirection` 2, `PointerPosition` 1,
    `ResizeStarted`/`ResizeCompleted` 2 each.

- [ ] **0.10 `WindowContext.Current` is `[ThreadStatic]`, and gate 1.8a makes that a problem.**
  92 sites. It resolves "the window on this thread", which is unambiguous only because UWP gives
  every view its own thread. The moment many windows share one thread — which 1.8a proved we can
  do, and 0.4 will do deliberately — `Current` silently returns whichever window was constructed
  last on that thread.

  This is not hypothetical for the migration; it is the same class of bug as CsWinRT #2524.
  The fix already exists in the codebase: **`WindowContext.ForXamlRoot(...)`**, used at 27 sites
  today (`MainPage.OnLoaded` among them). Anything holding a `UIElement` can resolve its own
  window instead of the thread's.

  So the work is: audit the 92, convert the ones with an element or `XamlRoot` in reach, and
  decide what `Current` should mean for the rest — probably "the active window", explicitly
  tracked, rather than a thread-static. Worth doing before 0.4 rather than after, because 0.4 is
  what makes the ambiguity real.

  Two known callers to fix while doing it: `AnimatedImage` (3 sites, converted to
  `WindowContext.Current.IsActive` in the `ActivationMode` change above — no worse than the
  `Window.Current` it replaced, but no better either), and note that `Current`'s getter logs
  `Environment.StackTrace` when null, which is not something to leave on a hot path.

  **What the replacement cannot be.** `WindowContext` already has three statics —
  `Current` (thread-static), `Active`, and `Main` — and `Active` looks like the obvious answer
  until you read how it is maintained (`OnActivated`):

      if (e.WindowActivationState != CoreWindowActivationState.Deactivated) Active = this;
      else if (Active == this) Active = null;

  **`Active` goes null whenever the app loses focus.** `Current` never does while a window
  exists on the thread. So it is not a drop-in: swapping the 124 sites onto `Active` would start
  throwing the moment you alt-tab, which is precisely when background callers like
  `AnimatedImage` ask. Any fix has to pick a defined fallback (`Active ?? Main`, or a
  never-nulled "last active"), and that is a decision, not a rename.

  **Count went up before it goes down.** The `Window.Current` migration deliberately funnelled
  60-odd scattered sites into `WindowContext.Current`, taking it from 92 to 124. That is the
  point: each `Window.Current` site needed its own judgement, whereas the 124 need one decision
  applied consistently. Sequence it with 0.4 — 0.4 is what makes the ambiguity real, and 0.4 is
  also what supplies the per-window plumbing the fix needs.

- [x] **0.12 `WindowContent` — done.** Fela's design: one base for everything assigned to
  `WindowContext.Content`, with the window-level events behind overridable methods.

  - `WindowEx` (which lived at the bottom of `VoipPage.xaml.cs`, below 1350 lines of call UI)
    became `Telegram.Controls.WindowContent`. Its constructor set white-on-transparent caption
    buttons — call-window chrome that every root would have inherited — so that became an opt-in
    `UseDarkCaptionButtons()`.
  - **All ten roots derive from it**, where they previously had four different bases
    (`Page`, `UserControl`, `UserControlEx`, `WindowEx`). `RootWindow`/`StandaloneWindow` were
    `Page` but used no `Page` member — every `.Frame` was `service.Frame`.
  - **`IPopupHost` is now `OnPopupOpened`/`OnPopupClosed` virtuals.** Six of the seven roots were
    doing the identical `SetTitleBar(null)` / `SetTitleBar(<element>)` pair, differing only in
    which element they named, so that collapsed to `protected override UIElement TitleBarElement`.
    Only `RootWindow` genuinely differs (it forwards to its `IRootContentPage`).
  - **`Activated`, `VisibilityChanged` and `CloseRequested` are wired once**, in
    `WindowContent.OnLoaded`/`OnUnloaded`, and surfaced as
    `OnWindowActivated(bool)` / `OnWindowVisibilityChanged(bool)` /
    `OnWindowCloseRequested(WindowCloseRequestedEventArgs)`. Every `+=`/`-=` pair for window
    events now lives in one file — the CLAUDE.md pairing rule enforced structurally.
    `WindowCloseRequestedEventArgs` wraps the UWP args and exposes only `Handled` and
    `GetDeferral()`, the two things all four callers used, so
    `SystemNavigationCloseRequestedPreviewEventArgs` no longer appears outside the base.
  - **`Window` is required at construction**, not resolved. It was briefly
    `ForXamlRoot(this)` in a property getter, but `XamlRoot` is null until the content is in a
    tree, which is later than some roots need it — Fela's call, and the right one. Every root is
    created with `new` and never reparented, so `protected WindowContent(WindowContext)` is
    honest about the dependency and impossible to get wrong.

    This also deleted six duplicate `WindowContext` fields (`_context`/`_window`, 27 references)
    that each root was keeping alongside the base's, and took the roots to **zero**
    `WindowContext.Current` — app-wide it is now 94, down from a peak of 124.

    Four roots had to gain the parameter: `RichTextWindow` and the three call windows. All four
    already had a `WindowContext` in scope at their call site, because `ViewServiceOptions.Content`
    and `CreatePresentation` were given one earlier — so nothing had to reach for a thread-static
    to supply it.

  **Renamed and regrouped**: nine `*Page` roots became `*Window` (they were never pages), and
  `PasscodeWindow`, `RichTextWindow` (was `TextEditorRichPopup`) and `WebAppWindow` moved into
  `Views/Host` beside the other four.

  **Do not create a `Views/Windows` folder** — tested, and it does not compile. Folder maps to
  namespace here, and inside `namespace Telegram.Views.Windows` the simple name `Windows` binds
  to that namespace before the global WinRT root:
  `CS0234: The type or namespace name 'Foundation' does not exist in the namespace
  'Probe.Views.Windows'`. Every `Windows.*` reference would need `global::`.

- [x] **0.13 Every `WindowContext` event carries app-owned args.** Fela's rule: *anything the
  Win32 host has to raise, it has to be able to construct* — and a UWP
  `VisibilityChangedEventArgs`, `WindowSizeChangedEventArgs` or
  `ApplicationViewConsolidatedEventArgs` cannot be `new`ed. The five events now carry
  `PopupActivatedEventArgs`, `WindowActivatedEventArgs`, `WindowVisibilityEventArgs` and
  `WindowSizeChangedEventArgs`, all declared in `Telegram.Navigation`.

  `CharacterReceived` is the deliberate exception: `CharacterReceivedRoutedEventArgs` is a XAML
  routed type, forwarded from `_content.CharacterReceived` and never constructed by us. An
  island raises it the same way, so it needs no wrapper.

  The app types intentionally shadow the UWP ones inside `Telegram.Navigation` — which is why
  `BootStrapper.OnActivated`, which really does handle the raw `Window`, has to qualify
  `Windows.UI.Core.WindowActivatedEventArgs`. Anything left on the UWP args fails loudly.

- [x] **0.3a `ApplicationView` events — into `WindowContent`.** `Consolidated` and
  `VisibleBoundsChanged` are wired by the base and surfaced as `OnWindowConsolidated()` /
  `OnWindowVisibleBoundsChanged()` — **no args**, because the only root consuming them ignored
  the `Consolidated` payload entirely and wanted `IsFullScreenMode` from the other, which
  `WindowContext` already exposes. So `ApplicationViewConsolidatedEventArgs` never reaches a root.

  `WebAppWindow.OnLoaded` became empty and was deleted; its doc comment — that `UserControlEx`
  guarantees `OnLoaded`/`OnUnloaded` alternate where raw `Loaded`/`Unloaded` re-fire on
  reparenting, and that a duplicate handler on a deferral-taking event means a duplicate
  confirmation dialog — moved to `WindowContent.OnLoaded`, where that reasoning now lives.

- [x] **0.3b `ApplicationView` methods — onto `WindowContext`. Done.** New members:
  `VisibleBounds`, `TryResizeView(Size)`, `SwitchToAsync()`, and a **static**
  `PreferredLaunchViewSize` (process-wide, not per-window). `TryEnterFullScreenMode()` now returns
  `bool` — `LiveStreamWindow` only flips its button when it succeeds, and the old `void` signature
  had silently dropped that.

  Two things fell out rather than being converted:
  - **Both `TryConsolidateAsync` calls were dead.** They were `else` branches for `Window == null`,
    which stopped being reachable once the constructor started requiring a `WindowContext`.
  - **`RootWindow.OnVisibleBoundsChanged` was dead**, referenced only from commented-out code
    above it. Deleted.

  After this, `ApplicationView` appears in the roots only in comments — plus
  `CoreApplicationViewTitleBar.LayoutMetricsChanged` in `StandaloneWindow`/`TabbedWindow`, which
  is a different API (`CoreApplication.GetCurrentView().TitleBar`, with `IsVisibleChanged`
  alongside) and is left for its own pass.

  Still excluded, still correct: `ViewService` + `ViewLifetimeControl` (21 of the 50 sites) are
  0.4's rewrite, and `OverlayWindow` / `GalleryWindow` / `ZoomableMediaPopup` are not roots.

- [ ] **0.3c `CoreApplicationViewTitleBar`** — `StandaloneWindow` and `TabbedWindow` each wire
  `IsVisibleChanged` + `LayoutMetricsChanged` off `CoreApplication.GetCurrentView().TitleBar` and
  handle both with one method. Same shape as everything else `WindowContent` absorbed, so it
  belongs there as a virtual — and `CoreApplicationViewTitleBar` is another type an island host
  has no equivalent for.

- [ ] ~~**0.12 A `WindowBase` for the window roots**~~ The types that get assigned to `WindowContext.Content` are window roots, not pages,
  and they all hand-wire the same set of window-level events:

  | wired by hand today | where |
  |---|---|
  | `SystemNavigationManagerPreview.CloseRequested` | `OverlayWindow`, `VoipPage`, `TextEditorRichPopup`, `WebAppPage` — 8 sites, always a `+=`/`-=` pair |
  | `WindowContext.Activated` | `WebAppPage`, `PasscodePage`, `TextEditorRichPopup`, `StoriesWindow`, `ChatView` |
  | `WindowContext.VisibilityChanged` | `ChatView` and others |
  | `ApplicationView.Consolidated` / `VisibleBoundsChanged` | `WindowContext`, `GalleryWindow`, `VoipPage`, … |
  | `SetTitleBar` | `TabbedPage`, `WebAppPage`, `VoipPage`, `GroupCallPage`, `LiveStreamPage` |

  A `WindowBase` that owns the `WindowContext` and exposes `protected virtual OnActivated`,
  `OnCloseRequested`, `OnVisibilityChanged`, `OnConsolidated` would:

  - **collapse every `+=`/`-=` pair into the base**, which is the pairing rule in CLAUDE.md
    enforced structurally rather than by review;
  - **remove the remaining `WindowContext.Current` from every root**, since the base holds it —
    finishing what 0.4's constructor injection started;
  - **absorb most of 0.3 and 0.5**: `ApplicationView` and the `GetForCurrentView` singletons stop
    being touched by roots at all, and are reached only through the base;
  - and, the point for this note, **put the UWP-vs-Win32 swap in exactly one place**. Whether
    `OnCloseRequested` is driven by `SystemNavigationManagerPreview` or by `WM_CLOSE` becomes an
    implementation detail of `WindowBase`, invisible to the twelve roots above it.

  The roots as they stand: `RootPage`, `StandalonePage`, `TabbedPage`, `WebAppPage`, `SharePage`,
  `VoipPage`, `GroupCallPage`, `LiveStreamPage`, `TextEditorRichPopup`, plus the overlay windows
  (`GalleryWindow`, `StoriesWindow`, `OverlayWindow`). They currently derive from four different
  bases — `Page`, `UserControl`, `UserControlEx` — which is part of why the wiring is duplicated.

  **Rename while doing it**: they are named `*Page` but are not pages. `WebAppWindow`,
  `TabbedWindow` and so on. Beware the cost — `Telegram.csproj` is not globbed (~1240 explicit
  `Compile`/`Page` entries), so a file rename means editing both csproj files and every
  `x:Class`. Worth splitting the rename from the base-class change so neither blocks the other.

- [x] **0.14 `XamlRoot` and `UIContext` are per-island — verified.** Gate 1.8d/1.8e, Fela's
  question and the right one to ask, because the whole `ForXamlRoot` design rests on it:

      1.8d  XamlRoot same: False | UIContext same: False
      1.8e  Popups: open in A: 1 | seen from B: 0   (scoped correctly)

  Two islands on one thread get **distinct** `XamlRoot`s and distinct `UIContext`s (compared by
  reference, as Fela suggested — `UIContext` is the identity token). 1.8e is the practical half:
  a `Popup` opened against window A's `XamlRoot` is invisible to `GetOpenPopupsForXamlRoot(rootB)`.

  So the rule for 0.10 is now proven rather than assumed:

  | keyed on | scope with N windows on one thread |
  |---|---|
  | `GetForCurrentView()` | **per thread — collapses** (1.8c: same `ConnectedAnimationService`) |
  | `XamlRoot` / `UIContext` | **per window — stays correct** |

  `WindowContext.ForXamlRoot`, `GetOpenPopupsForXamlRoot`, `FocusManagerEx.TryGetFocusedElement`
  and `MessagePopup.ShowAsync(XamlRoot, …)` all keep working. That is what makes 0.10 a
  mechanical conversion rather than a design problem.

- [ ] **0.15 No UI `[ThreadStatic]` may survive.**

  **State, 2026-08-22.** The pattern is settled and being applied:
  `ConditionalWeakTable<XamlRoot, T>`, plus a new `XamlRoot.TryGetContent<T>` extension that
  also retires the `try/catch` around `XamlRoot.Content` throwing on a closed window.
  Converted so far (uncommitted): `ContentPopup._currentDialogShowRequest`, `AnimatedImageLoader`,
  `ProfilePicture.Loader`, `OverlayWindow`, `PaidReactionService`.
  **Still `[ThreadStatic]`, and inconsistent with the above** — their `Release()` takes no
  `XamlRoot` where the converted ones now do: `PlaceholderHelper`, `MessageBubbleBrush`,
  `ProfileCell` (4). Note `ProfileCell.xaml.cs` is cp1252, not UTF-8; a scripted rewrite that
  assumes UTF-8 will corrupt it.

  Original entry: **0.15 No UI `[ThreadStatic]` may survive.** Fela's rule. The distinction that matters:

  **A `[ThreadStatic]` cache of XAML objects is correct and must stay.** Brushes, styles and
  `CompositionBrush`es are thread-affine, so caching them per thread is exactly right, and
  sharing them between windows *on the same thread* is fine — that is the point.
  - `Theme._light` / `_dark` / `_lightBackground` / `_darkBackground` (both theme classes)
  - `ProfileCell.t_accent` / `t_transparent` / `t_bodyStyle` / `t_secretStyle`
  - `MessageBubbleBrush._brushes`
  - `Interop` `ICompositionTargetStatics` + the two token dictionaries (CompositionTarget is
    genuinely per-thread)
  - `PlaceholderHelper._foreground`, `FormattedTextBlock.RelativeDateService._current`

  **A `[ThreadStatic]` standing in for "the current window's X" is the bug** — it only reads as
  per-window because UWP gave every view its own thread:
  - `WindowContext._current` — item 0.10, 94 call sites.
  - **`OverlayWindow.Current`** — a per-window overlay. `WindowContext.PopupOpened/Closed`,
    `NavigationService` and `TLNavigationService` all reach for it; two windows on one thread
    would share one overlay.
  - **`ContentPopup._currentDialogShowRequest`** — worse than shared state, it is a
    *serialisation gate*: `while (_currentDialogShowRequest != null) await …` queues dialogs so
    only one shows at a time. Per-thread today means per-window; with several windows on a
    thread, opening a dialog in one window would block a dialog in another.
  - `PaidReactionService._toast` and `GroupCallPaidReactionService._toast` — per-window toasts.
  - `Theme.Current` — cleared in `WindowContext` (line ~453), so it looks per-window. Needs a
    decision: is a per-window theme override a real feature, or is this incidental?

  **Not UI, correctly per-thread, leave alone**: `Profiler.t_scopes`, `WatchDog._supersede*` and
  `_reporting`, `Td/Client._writer`, `Td/ClientJson._writer` / `_buffer`.

  **The shape to migrate onto: one `ConditionalWeakTable<XamlRoot, T>` per type**, not a single
  per-window bag wrapping everything. Reasons, in order of weight:

  1. **The release list deletes itself.** `WindowContext.OnShutdownCompleted` currently hand-maintains
     eight teardown calls — `Theme.Current = null`, `ThemeIncoming.Release()`, `ThemeOutgoing.Release()`,
     `PlaceholderHelper.Release()`, `MessageBubbleBrush.Release()`, and two that *already* take a
     `XamlRoot` (`AnimatedImageLoader.Release(XamlRoot)`, `ProfilePicture.Loader.Release(XamlRoot)`).
     A weak table frees its entry with the window, so none of those calls are needed. The migration
     is already half-done and inconsistent.
  2. **That teardown is already broken for the target.** It runs on `ShutdownCompleted`, i.e. thread
     shutdown. With several windows per thread, closing one window does not end the thread, so the
     block stops firing at the right time. Per-`XamlRoot` storage is the fix, not just a tidy-up.
  3. **A central bag inverts the dependencies.** The state lives in `Common`, `Controls`, `Services`
     and `Navigation`; a type wrapping all of it must reference all of it, and becomes the file
     everyone edits to add state.

  Verified in the spike before recommending it:

  | gate | result |
  |---|---|
  | 1.8g `XamlRoot` RCW identity | same reference every read, and from any element in the tree — so CWT's reference-only keying is safe |
  | 1.8i per-window isolation | entries keyed on window A are invisible from window B |
  | 1.8j automatic release | window destroyed → `XamlRoot alive: False`, `entries left: 0` |

  Two traps the spike surfaced:
  - **`XamlRoot` is null until `Loaded`** (1.8h: `at gate time: NULL | at Loaded: set`), and
    `TryGetValue(null, …)` **throws `ArgumentNullException`** rather than returning false (1.8i).
    Every lookup needs a null guard. This is also why `WindowContent` takes its `WindowContext` in
    the constructor rather than resolving it.
  - **`GetValue`'s `createValueCallback` may run more than once** under contention — only one result
    is stored, the rest are discarded. Keep it cheap and side-effect-free, and cache the delegate,
    as `TextSelectionCoordinator` already does.

  **`ConditionalWeakTable` thread safety — measured, not cited.** A probe in
  `C:\Source\XamlIslandSpike\probe` (8 threads x 40,000 interleaved
  `Add`/`Remove`/`TryGetValue`/`GetValue` over 256 shared keys):

  | check | result |
  |---|---|
  | mixed read/write hammer | 0 unexpected exceptions, 0 corrupted reads |
  | `GetValue` callback under contention | **61 surplus runs** over 400 rounds x 8 racing threads; 0 rounds where callers disagreed on the value |
  | enumeration while another thread mutates | survived 2000 full enumerations, no throw |

  So the table is safe, and two nuances follow:

  - **`GetValue`'s `createValueCallback` genuinely runs more than once** — the table stays
    consistent (one value wins, every caller sees it) but the surplus results are discarded. The
    factory must therefore be cheap and side-effect-free: a callback that subscribed an event,
    started a timer or allocated a native handle would leak on every surplus run, silently, and
    only under contention.
  - **Enumeration held up**, contrary to my first assumption. It does not throw. Still do not rely
    on it for completeness or order, since a concurrent write can add or drop an entry mid-walk.

  And the caveat that matters most here: **a thread-safe table says nothing about the values.**
  `SolidColorBrush`, `Style` and `CompositionColorBrush` remain thread-affine. Keying per window
  satisfies that naturally, because a window belongs to one thread — but the table is not what
  guarantees it.

- [x] **0.16 Island teardown order — found by crashing it.** Destroying an island's HWND while the
  `DesktopWindowXamlSource` still holds content **takes the process down**; the XAML core is left
  pointing at a dead window. The working order is: detach content (`Content = null`), `Dispose()`
  the source, then release the native side and let `WM_DESTROY` run. Relevant to 0.4: whatever
  replaces `ViewService` has to tear down in that order.

- [x] **0.17 When `XamlRoot` exists — read out of the WinUI core.** The full XAML core is at
  `C:\Source\microsoft-ui-xaml\src\dxaml`, which settles these questions properly.

  `element.XamlRoot` is `XamlRoot::GetForElementStatic` (`XamlRoot_Partial.cpp:220`):

      VisualTree* visualTree = VisualTree::GetForElementNoRef(element->GetHandle());
      if (visualTree) xamlRootInsp = visualTree->GetOrCreateXamlRootNoRef();

  So: **the XamlRoot object is created lazily on first request**, and it is reachable exactly when
  the element is attached to a `VisualTree`. There is no "window initialised" moment that creates
  it; it appears the instant content is attached — `Window.Current.Content = x` on UWP,
  `DesktopWindowXamlSource.Content = x` in an island. That matches gate 1.8d, where the island
  root had a XamlRoot immediately after `IslandWindow.Create`, and 1.8h, where a child inside a
  ScrollViewer template did not.

  **Now used in the app**: `WindowContext.SetContent` captures `_xamlRoot` and fills `_mapping`
  immediately after `_window.Content = _content`, and the `Loading` handler it used to need is
  gone.

  Also worth knowing: `put_XamlRoot` exists and fails with `ERROR_CANNOT_SET_XAMLROOT_WHEN_NOT_NULL`
  — you *can* assign a XamlRoot to a still-detached element (that is how a `Popup` gets one before
  opening, as gate 1.8e does), but only once.

  **`Loading` fires immediately before `ApplyTemplate`**, in the same measure pass
  (`framework.cpp:1493` in `CFrameworkElement::MeasureCore`):

      RaiseLoadingEventIfNeeded();
      if (!bInLayoutTransition) { IFC_RETURN(InvokeApplyTemplate(&bTemplateApplied)); }

  That is the only hook where an element is parented (so `XamlRoot` resolves) but its template —
  and therefore its `ThemeResource` lookups — has not yet inflated. Re-verified 2026-08-23
  against `framework.cpp`, because it is surprising enough to be worth not taking on trust.

  Four properties of `Loading` follow from `RaiseLoadingEventIfNeeded` at `framework.cpp:1703`,
  and they decide what it can be used for:

  - **It is raised from `MeasureCore`**, not from tree insertion. An element that is never
    measured never gets it.
  - **Synchronously** — `Raise(..., fRaiseSync: TRUE)` — so the handler has finished before
    `InvokeApplyTemplate` runs on the next line. That is what makes it usable for merging a
    dictionary that the template's lookups must see.
  - **Once per element**, latched on `m_firedLoadingEvent`; it does not fire again when an
    element is reparented or recycled.
  - **Only if something is listening.** `ShouldRaiseEvent` gates it, so with no handler attached
    the event never fires and the latch is never set.

  The practical limit: in a `Loading` handler the template does not exist yet, so
  `GetTemplateChild` returns nothing. Assigning `Resources` is fine — that is not a template
  part — but anything reaching for template children has to wait for `OnApplyTemplate`.

  **`Loading` is not the same as the top of `OnApplyTemplate`**, which is the obvious-looking
  alternative. `InvokeApplyTemplate` inflates the template at `framework.cpp:1209` and only calls
  the override at 1233, after `RefreshTemplateBindings`. So by `OnApplyTemplate` every
  `{ThemeResource}` in the template has already resolved against whatever dictionary was in
  scope. Assigning there still works — `ThemeResource` re-evaluates when the dictionary changes,
  which is why it worked when tried at `ContainerContentChanging` — but it works by resolving
  twice. `Loading` resolves once.

  **`ContainerContentChanging` is later than it looks.** `ListViewBase_Partial_ContainerPhase.cpp`
  forces a measure *before* raising it, specifically to materialise `ContentTemplateRoot`:

      // force measure. This will be no-op since content has not been set/changed
      // but we need it for the contenttemplateroot
      IFC_RETURN(containerAsISI.Cast<SelectorItem>()->Measure(measureSize));
      ... Raise(ContainerContentChanging) ... then put_Content(item)

  So by CCC the template is inflated and `ThemeResource` has resolved; merging a dictionary there
  works (Fela tested it) but only via re-evaluation — a second pass per bubble.

- [x] **0.18 Per-window chat themes - done 2026-08-23, and the rule this item used to state is
  gone.** It went round three times, so the conclusion is worth keeping along with why the two
  earlier answers were wrong.

  **How bubble theming works**, which is what made it look impossible: `[ThreadStatic]` tables hold
  `(Color, SolidColorBrush)`; every `ThemeOutgoing`/`ThemeIncoming` copies **the same brush
  references** into its ThemeDictionaries; a per-chat theme mutates ~20 brush colours in place and
  every bubble in the window repaints. No walking, no re-resolution. It is very cheap and the
  cheapness is the point - which is why the fix could not be "one dictionary instance per bubble".

  **The first answer was a rule: a window that renders chat content owns its thread.** It followed
  from `App.xaml` resources being instantiated per thread - visible in `Theme`'s own constructor,
  `_isPrimary = Current == null` over a `[ThreadStatic] Current`, which only makes sense if
  `Theme()` runs once per thread. Thread == view was what made the scheme correct.

  **It was too strong** - Fela, 2026-08-23. The app theme is global and every window shows the same
  one; the only thing that varies per window is the **chat override**, and that touches exactly
  `Outgoing.Update`, `Incoming.Update` and the background. It never runs the app colour pass. The
  message brushes live in bubbles, bubbles are content, and gate 1.11 measured that content scopes
  are per island - so a per-window override was always possible.

  **What shipped**, four commits: `de38e4934` moved the message brushes onto the window's theme,
  `04319fbaf` moved the chat override onto the window, `8d2acea34` forwarded the window's brushes
  to popups and flyouts - which closes the one loose end this item had, the popups that render
  bubble keys and used to inherit the override by accident - and `efa19f6a1` reduced
  `Theme.Current` to the app-level shim it now is. `WindowContext.SetContent` merges the window's
  own `Incoming.CreateDictionary()` into the presenter's resources; `MessageBubble` takes
  `Outgoing` from the window.

  What is left is not a limit: the **app** theme stays per thread because `Application.Resources`
  is what popups resolve from, and it is meant to be global anyway. Nothing caps 1.8a any more -
  several chat windows can share a thread.

  **And on the Win32 host they all do.** Every window is created through
  `ViewService.Win32.OnUIThread`, which dispatches to `WindowContext.Main.Dispatcher`: the whole
  app is one thread and many islands. Stated plainly because the superseded rule above reads as if
  it were still live, and it keeps being repeated as one.

  Anything per thread on this host is therefore **process-wide in practice**:
  `Application.Resources`, the `GetForCurrentView()` singletons (1.8c), the `Compositor` (1.8f),
  and the XAML backstop switch Mica needs (1.10). A shared `Backdrop` acrylic brush falls into that
  same set - see 2.1c.

- [ ] **0.19 `Theme.Current` -> `WindowContext.Theme`, in three steps.** Fela's proposal, and the
  right destination — but a straight rename is churn, because the 50 `Theme.Current` sites do
  not all want a window, and because moving the handle does not move the state.

  **a. Split the global settings off first.** 30 of the 50 sites are `MessageFontSize` (9),
  `CaptionFontSize` (6), `MonospaceFontFamily` (11) and `XamlAutoFontFamily` (4). They come out
  of `_isolatedStore` and are identical in every window; per-window they become N copies of one
  setting, and a read that is currently a `[ThreadStatic]` field — free — turns into a
  `_mapping` lookup plus two derefs, on `FormattedTextBlock`'s path. Split value from
  materialisation: the size and the font *name* are global statics; the `FontFamily` instances
  stay per-window, since `FontFamily` is a `DependencyObject` and therefore thread-affine, which
  is why they are per-thread today. Doing this first shrinks the problem to 13 sites.

  **b. Move the dictionaries to the window root.** `Theme` is a `ResourceDictionary` merged at
  `App.xaml:515`, so XAML constructs it — one per thread (see 0.18). `WindowContext.Theme` can
  only *point at* that instance; a per-window handle onto per-thread brushes is worse than an
  honest `[ThreadStatic]`, because it reads as solved. So `<common:Theme/>` and
  `<common:ThemeIncoming/>` move out of `App.xaml` into the window root's `Resources`
  (`WindowContent`), which is per-window by construction. `WindowContext` then *owns* the
  instance and merges it rather than discovering it, and `ThemeResource` resolves one level
  earlier than App rather than one later. `ThemeOutgoing` cannot join them at the root — it
  redefines the same keys as `ThemeIncoming` — which is what `ThemeOutgoing2.ForXamlRoot` plus
  the `Loading` hook from 0.17 is for.

  **c. Then the handle, for the 13 that want it.** `Parameters` (8 sites) is already
  `_parameters[WindowContext.Current.ActualTheme]` — a per-window lookup wearing a static's
  clothes; `window.Theme.Parameters` deletes the hop. And the broadcast in
  `AppearanceSettings.cs:202` is the case that makes it worth doing:

      await WindowContext.ForEachAsync(window =>
      {
          Theme.Current.Update(theme);   // "current" = whichever thread this landed on

  correct today only because `ForEachAsync` dispatches to each window's own thread.
  `window.Theme.Update(theme)` is right by construction, and stays right the day two windows
  share one. Same for `ChatBackground`, `ChatTheme`, `DarkSettings`, `UpdateScrolls`,
  `UpdateEmojiSet`.

  Rule for the call sites: anchor the `WindowContext` on the element and cache it at `Loading`
  or `Loaded`. Never a lookup per read — that is the one way this change can cost more than it
  buys.

  **State, 2026-08-22.** Fela has done most of (a) and (c); (b) is untouched.
  `Theme.Current` is down from 50 sites to 25.
  - (a) `MessageFontSize`/`CaptionFontSize` moved to `SettingsService` — 72 lines out of
    `Theme.cs`. **`MonospaceFontFamily` and `XamlAutoFontFamily` have not moved**, and they are
    15 of the 25 remaining sites. They are the `FontFamily`-is-a-`DependencyObject` case: the
    font *name* is global, the instance has to stay per-window.
  - (c) `WindowContext.Theme` exists as `=> Theme.Current`, a forwarder, with `ViewModelBase`
    now exposing `Window` so view models can reach it. Call sites move first, the
    `[ThreadStatic]` behind it swaps later. The broadcast is already
    `window.Theme.Update(theme)` — the case that motivated the change.
  - What still reads `Theme.Current` and genuinely wants a window: `ChatBackgroundControl` (3),
    `PaymentFormViewModel.Parameters` (1), and the four `Update(...)` entry points in
    `SettingsThemeViewModel`, `BlankPage`, `ChatView` and `SettingsAppearancePage`.
  - (b) `App.xaml:515` still merges `<common:Theme/>` and `<common:ThemeIncoming/>`, and the
    per-bubble dictionaries in `ChatView.xaml` are back to the original shape.
  - `ThemeOutgoing2` (the `ConditionalWeakTable<XamlRoot, …>` experiment) is in `Theme.cs` with
    **no consumers**. Decide whether it becomes (b) or gets deleted; leaving it is the worst of
    the three.

- [ ] **0.20 Untangle `Theme` / `ThemeIncoming` / `ThemeOutgoing`.** Supersedes 0.19b, which
  described the destination without saying how to get there. Fela's read: they are tangled for
  two reasons, one historical — per-chat appearances did not exist when this was written — and
  one structural, that every UWP window needs a `Theme` anyway, so it may as well be the thing
  that resolves the other two.

  **The domain, which the code does not currently express.** Three layers over two independent
  axes, each layer overriding the one above it, per base, with the base flippable at any time:

  | | colours | background |
  | --- | --- | --- |
  | base | Light / Dark | — |
  | app | `AppSettings.Appearance[base]` — Classic / Day / Tinted / Custom(file) / Accent | `settings.Background` |
  | chat | `ChatTheme.LightSettings` / `.DarkSettings` | `ChatBackground` |

  **What the code does instead.** There is one resolution — `(base, appearance, chatTheme) ->
  values` — and it is written twice: the four-case decision (chat theme? custom file? accent?
  plain) appears in `Update(ApplicationTheme)` at 285-300 and again in the `else` branch of the
  Local `Update` at 240-260, and the tint mapping is duplicated verbatim at 205 and 314. There
  are five `ThemeOutgoing.Update` call sites for what is one operation. There is no Local path
  and a Global path; there is one function split in half by which caller arrived.

  Three defects follow directly, all present today:

  - **Double application.** `Update(ApplicationTheme)` resolves and updates the bubble
    dictionaries at 343, then calls the Local update at 305, which resolves again and updates
    them again at 225. Every app theme switch with a chat theme active pays twice.
  - **The change detector is too narrow.** `_lastAccent` is an `int?` holding only the accent,
    yet it guards the assignment of `_lastLightSettings`, `_lastDarkSettings` and
    `_lastChatTheme`. A chat theme that changes while keeping the same accent skips the update
    and leaves all three stale.
  - **The app layer leaks into the chat layer.** Local reads `AppSettings.Appearance[base].Type`
    at 204 to pick the tint for a *chat* theme. Probably intended — chat themes tinting to the
    user's chosen style — but nothing says so, which is why the two halves cannot be told apart
    by reading them.

  Also worth knowing before moving anything: **`Parameters` is a byproduct**. `_parameters[…]` is
  assigned only at line 466, inside the 400-line colour pass, so the bot-API payload cannot be
  obtained without running brush computation.

  **The constraint that fixes the shape.** The bubble keys are referenced **44 times across ~15
  XAML files** — `MessageBubble.xaml` and the content controls, but also `ChatPinnedMessage`,
  `SharedLinkCell`, `SendFilesPopup`, `Generic.xaml`. The `ThemeResource` dictionary-scoping
  mechanism is therefore load-bearing and cannot be replaced by assigning brushes in code. The
  incoming set is the app-wide default, which is why `App.xaml` merges it; outgoing is a scoped
  override.

  **Target shape.**

  - `Theme`, one per window, owns the resolved values and the brush objects. The `[ThreadStatic]`
    tables become instance fields — and no `ConditionalWeakTable` is needed either, because
    ownership does the scoping that `ThemeOutgoing2` was reaching for with one. That is why that
    experiment felt like it was fighting the grain; **delete it**, it has no callers.
  - The **incoming** set merges into `Theme` itself, so `App.xaml` carries one entry instead of
    two and every non-bubble consumer resolves exactly as now.
  - `Theme.Outgoing` is **one dictionary per window**, merged into an outgoing bubble's
    `Resources` in code at `Loading` instead of being constructed by the `DataTemplate`. Cheaper
    than today, where every realised bubble builds its own `ResourceDictionary`, and `Loading` is
    the correct hook because it runs immediately before `ApplyTemplate` (0.17), so the lookups
    happen after the merge without the second pass that `ContainerContentChanging` would cost.
  - One resolver, one application point. Local and Global stop being separate concepts: the only
    difference is whether a `ChatTheme` was supplied. Change detection then compares the resolved
    inputs, which fixes the stale-field bug for free, and the background axis resolves alongside
    as `chatBackground ?? settings.Background` in one place rather than only in Local.

  **`ThemeOutgoing` is merged in six places, not one** — a belief worth correcting before the
  code moves. Besides `ChatView.xaml:413` it appears in `BackgroundPopup.xaml:629`,
  `ThemePreviewPopup.xaml` (114, 120, 130), `SettingsAppearancePage.xaml:42` and
  `SupergroupProfileColorPage.xaml:152`. All five extra sites are **previews** that render fake
  outgoing bubbles, none of them inside a `MessageBubble`, so "the bubble merges it at Loading"
  does not cover them.

  Settled: **all of them show the current theme except `ThemePreviewPopup`**, which by definition
  shows one that has not been applied. So the shared per-window `Theme.Outgoing` serves five of
  the six, and `ThemePreviewPopup` keeps building a throwaway dictionary of its own — an
  exception stated in the design rather than an accident of it. `ChatThemeCell` already works
  that way from the other direction, computing `MessageBackgroundBrush` through
  `ThemeAccentInfo.Colorize` rather than reading any dictionary.

  **Merge from code-behind at each site, not from XAML.** Six places is not many, and it inverts
  the hard part: rather than a `ResourceDictionary` having to discover which window owns it, the
  merging control already has a `XamlRoot` and simply asks for `Theme.Outgoing`.

  **The dictionary stays per bubble — a shared instance is not possible.** Assigning one
  `ResourceDictionary` to two elements throws `ArgumentException`, and the core says why:
  `FrameworkElement`'s `Resources` setter calls `resources->SetResourceOwner(this)`
  (`framework.cpp:343`) on a dictionary that holds a **single** owner pointer, later used to find
  the visual owner for theme resolution (`Resources.cpp:1344`) — and that owner is propagated
  into the merged and theme dictionaries beneath it (`Resources.cpp:133`, `141`). So merging a
  shared instance is no better than assigning one: even where it does not throw, the owner would
  flap between bubbles and break `{ThemeResource}` re-evaluation for all but the last.

  That removes the allocation saving that first motivated the change — the constructor's two
  child dictionaries and ~40 insertions stay, per realised bubble — but not the point of it. Only
  the **wrapper** has to be per bubble; the brushes inside are the window's, shared and
  recoloured in place, which is what makes one theme change repaint every bubble without touching
  any of them. So `Theme` exposes `CreateOutgoing()`, not an `Outgoing` property, and what moves
  off `[ThreadStatic]` is the brush state rather than the dictionary. A rewrite has to keep
  `ThemeDictionaries["Light"]` and `ThemeDictionaries["Default"]` — dark is stored under
  `Default`, not `Dark`.

  The hook only matters in `MessageBubble`, and `Loading` is the right one. `IsOutgoing` is a
  **plain C# property, not a DependencyProperty** — nothing binds to it, animates it or styles
  it, and a DP would box the bool and cost a property-system lookup on a per-bubble path for
  nothing. XAML can still set it from the template, because a literal attribute needs only a
  settable public property; it is bindings and animations that require a DP. The setter cannot
  do the assignment itself, because at parse time the
  element is not parented and there is no `XamlRoot` to resolve the window's `Theme` from;
  `Loading` is the first moment there is one, and it runs immediately before `ApplyTemplate`
  (0.17), so the lookups resolve once against the dictionary that is already in place. Firing
  once per element is sufficient here precisely because `Resources` persists on the instance —
  a recycled bubble keeps the assignment and needs no second pass. The five previews can use
  whatever hook each type already has with a `XamlRoot` in hand.

  Worth checking while in there: the **incoming** template merges `<common:ThemeIncoming />` per
  bubble as well (`ChatView.xaml:424`) even though the same dictionary is already merged in
  `App.xaml:516`. Sibling elements do not inherit each other's resources, so the per-bubble copy
  looks redundant today — if it is, incoming bubbles need no dictionary at all once `Theme` owns
  the default set, and only outgoing ones get an assignment.

  **The scoping, settled 2026-08-23.** Fela's scheme, and gate 1.11 measured that each level
  behaves:

  | dictionary | merged at | reaches |
  | --- | --- | --- |
  | `Theme`, including the **default** incoming brushes | `App.xaml` | everything, `PopupRoot` included - the fallback for anything not forwarded |
  | the **window's** incoming | `WindowContext.Content` | all window content - bubbles, cells, headers, pages |
  | the **window's** outgoing | `MessageBubble` when `IsOutgoing` | that bubble |
  | the **window's** incoming again | `ContentPopup.ShowQueuedAsync`, plus three flyouts | popup content that should follow the chat override |

  **Two instances, not one.** The app-level set and the per-window set have to be different
  `MessageBrushes`: today `UpdateMessages` recolours the very brushes that are folded into the
  app dictionary, so a chat override would repaint the fallback and every window with it. So
  `Theme.Incoming`/`Theme.Outgoing` become the defaults, recoloured only by the app theme pass,
  and `WindowContext` owns a second pair that the chat override writes to.

  That relocates the override path: `Theme.Update(ElementTheme, ChatTheme, …)` acts on the
  window's pair rather than `Theme`'s own, so it becomes a `WindowContext` operation and its
  three callers - `ChatView`, `BlankPage`, `SettingsAppearancePage` - follow it there.

  The last row is the price, and it is a fair one. The message keys are referenced by ~75 XAML
  files; nearly all are window content, but **seven are popups** - `SendFilesPopup`,
  `BackgroundPopup`, `BackgroundsPopup`, `SendLocationPopup`, `SendGiftPopup`,
  `ReceivedGiftPopup`, `BusinessChatLinkPopup` - and all seven derive from `ContentPopup`, so
  `ShowQueuedAsync` covers them in one place; it already takes the `XamlRoot`, which is the
  legitimate use of `ForXamlRoot`.

  **Three flyouts build their own `Popup` and are not covered by it**: `EmojiMenuFlyout`,
  `MessageEffectMenuFlyout` and `ReactionsMenuFlyout` each `new Popup()` and host an
  `EmojiDrawer`, whose markup uses the message keys. They are `UserControl`s, so they need the
  same forwarding where they create the popup - three sites, each with an anchor element in hand.

  Ten explicit call sites in place of a global that currently works by accident, and
  `SendFilesPopup` keeps showing the chat's theme because someone decided it should.

  **Order.** Delete `ThemeOutgoing2` first — 184 lines, no callers, and a parked half-alternative
  beside the real one is most of what makes the file read as a mess. Then the single resolver,
  since it is what the rest depends on. Then `Theme` taking ownership of both directions. The
  `Outgoing`/`Incoming` de-duplication falls out of that last step rather than being its own job.

- [ ] **0.11 Find a better hook for type-to-search / type-to-compose in `ChatView` and
  `MainPage`.** Both now subscribe to `WindowContext.CharacterReceived` and then disambiguate by
  state — `MainPage` bails unless `MasterDetail.NavigationService?.Frame.Content is BlankPage`,
  `ChatView` assumes it wins otherwise. That is still a broadcast-plus-guard design; it is only
  less bad than the `CoreWindow` version, not actually right.

  Both are in the same tree, so subscribing at the window root was the expedient answer: a
  character typed with focus in the chat list has to reach the *open chat's* composer, which a
  subscription on `ChatView` itself would never see. Better shapes to consider:
  - one owner — `MainPage` subscribes and routes to whatever is currently open, since it is the
    one that knows;
  - or a small registration on `WindowContext`: whoever is the active text target registers
    itself, and the window forwards to exactly one subscriber rather than broadcasting.

  The second also answers what happens with several windows on one thread, so it pairs with
  0.10.

- [ ] **0.7 Route file access through a path-first helper.** 40 `FutureAccessList` sites, 25
  `StorageFile.` statics, 12 `FileOpenPicker`, 6 `CameraCaptureUI`, 4 `FileSavePicker`, 3
  `FolderPicker`, 2 `KnownFolders`, 28 `Launcher.`. Adding an HWND parameter at this seam is
  what makes `IInitializeWithWindow` a one-line change later instead of 90.
  Leave `ApplicationData.Current` alone — it works on desktop, and the 45 remaining sites are
  all `LocalFolder`/`TemporaryFolder`. Its settings half is already done: one line, behind
  `ISettingsStore`.

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
- [x] **1.7 Non-client: a custom caption that maximizes and snaps.** **Passes.** The thing UWP
  cannot do — a UWP app extending into the title bar can hide, minimize and close, but never
  maximize, and loses snap layouts with the system caption. Three pieces, in
  `IslandWindow.NonClient.cs`:

  1. **`WM_NCCALCSIZE`** — let the default proc compute the frame, then restore the original
     `top`, so the frame survives on three sides and the client area reaches the top edge. When
     maximized the top must come back in by `SM_CYSIZEFRAME + SM_CXPADDEDBORDER`, because a
     maximized window is deliberately larger than the monitor by the frame width; without that
     the caption sits off-screen.
  2. **`WM_NCHITTEST`** — removing the top of the frame takes the top resize border with it, so
     it has to be answered by hand. The other three sides still come from the default proc.
  3. **A drag-bar child HWND** over the caption strip, `WS_EX_LAYERED | WS_EX_NOREDIRECTIONBITMAP`.

  Two things about that third piece that cost time and are not in any article:

  - **The island swallows `WM_NCHITTEST`.** It is a child window, so the top-level window never
    sees the caption and dragging does not work at all. The drag bar answers instead.
  - **A child window answering `HTCAPTION` drags nothing.** The hit test alone is not enough; the
    drag bar must forward `WM_NCMOUSEMOVE`, `WM_NCLBUTTONDOWN`, `WM_NCLBUTTONDBLCLK`,
    `WM_NCLBUTTONUP` and the right-button pair to the **parent** with `SendMessage` whenever the
    hit is `HTCAPTION` or `HTTOP`. That is what produces dragging, double-click maximize, the
    system menu and the top resize.

  And the split that matters for expectations: returning `HTMAXBUTTON` earns the **snap layouts
  flyout** for free, but **not the click** — Terminal's own words, "the buttons won't work as
  you'd expect". A release over a button is turned into `WM_SYSCOMMAND` with `SC_MINIMIZE`,
  `SC_MAXIMIZE`/`SC_RESTORE` or `SC_CLOSE` by hand.

  ~~**Left undone**: hover feedback on the caption buttons.~~ **Done, 2026-08-24 - see 1.7a.**
  It went exactly the way this paragraph guessed: `TrackMouseEvent` with `TME_NONCLIENT`, armed
  once per visit, and the hover pushed into XAML by hand.

  One incidental lesson from the same session: the spike merges no `XamlControlsResources`, so
  there are no theme brushes to inherit and every colour in it is a literal — which made it easy
  to hardcode dark and get it wrong on a light system. It now derives its palette from
  `Application.Current.RequestedTheme`, which is also what `DWMWA_USE_IMMERSIVE_DARK_MODE` should
  be given rather than a constant `true`.
- [x] **1.7a One caption-button model for both hosts, 2026-08-24.** Fela's design, and the piece
  that makes the Win32 caption real rather than a hit-test with nothing drawn in it.

  `WindowPresenter` - the `WindowContext`'s own root, which every window already has - grows a
  template holding minimize, maximize and close, and a `CaptionButtons` flags property saying
  which of them the window wants. Buttons only: no caption text, no icon, because no window in
  this app wanted either.

  **The rule that makes one setting serve two hosts.** `All` means "an ordinary window", and an
  ordinary UWP window already has all three drawn by the shell - better than anything we could
  draw, since it minimizes, maximizes and has the system menu. So on UWP `All` hides ours and
  anything less than `All` takes the shell's caption away and draws our own; on Win32, where
  there is no shell caption, the setting is always honoured as given. One boolean per host half,
  `HasSystemCaptionButtons`, is the whole of the difference.

  **What UWP can actually draw is Close and only Close** - it has no way to minimize or maximize
  a view, which is gate 1.7's whole reason for existing. So the values that mean anything on both
  hosts are `None`, `Close` and `All`; the other combinations are Win32 only. Both call sites
  that wanted this - `WebAppWindow` and `PaymentFormPage` - wanted `Close`.

  **Three copies of the same hand-drawn X** (`TabbedWindow.xaml`, `WebAppWindow.xaml`,
  `PaymentFormPage.xaml` - `Path` data `M0.5 0.5 L9.5 9.5...`, `#C42B1C` on hover) collapse into
  the one template. The other two glyphs follow its convention: 10x10 box, 1px stroke, and a
  restore glyph swapped in by a visual state when the window is zoomed.

  Four things that were not obvious going in:

  - **Not a parameter on `SetTitleBar`.** That was the shape it replaced - `SetTitleBar(element,
    collapsed: true)` - and it cannot stay one: `WindowContent.OnPopupOpened` calls
    `SetTitleBar(null)` and `OnPopupClosed` calls `SetTitleBar(element)`, both with the default
    argument, so every popup opening over a web app would have quietly handed the caption back to
    the shell. The buttons are a window-level setting, kept on `WindowContext` because a root sets
    it from its constructor - before there is a presenter to set it on.
  - **The buttons take no input on Win32, deliberately.** The drag bar of 1.7 is the window under
    the pointer and claims the rightmost slots as `HTCLOSE`/`HTMAXBUTTON`/`HTMINBUTTON`; that
    claim is what earns the snap layouts flyout, and it already turns the click into a
    `WM_SYSCOMMAND`. So XAML draws them and Win32 answers them, and only the *appearance* of
    hover and pressed has to cross over - `SetCaptionButtonState`, pushed in from the drag bar's
    `WM_NCMOUSEMOVE`. It is a pointer-rate path, so it early-outs on an unchanged state on both
    sides of the boundary.
  - **The strip and the hit test have to agree, so they share their numbers.** `CaptionHeight`
    went 32 -> 40, which is what every title bar in this app is, and `DragBarHitTest` now walks
    the same flags the template lays out rather than assuming three buttons.
  - **The close button routes through `OnWindowCloseRequested`** - Fela, rather than the separate
    hook I had written, and the build proved him right twice over. `WebAppWindow` *already*
    overrode it, so my hook would have been a second copy of the same confirmation; and it does so
    with a **deferral**, which a synchronous "did the root handle it?" would have walked straight
    past - the override is `async void`, so it returns at its first await with `Handled` still
    false. `WindowCloseRequestedEventArgs` now carries an app-side deferral
    (`TaskCompletionSource`, allocated only if a handler asks for one) and `RequestCloseAsync`
    waits on it. The lesson is the ordinary one: the type already told me it could defer.

  And one thing that followed from Fela's own reading: **no maximize button means no resizing**.
  The two are one affordance, so `Close` alone drops `WS_THICKFRAME` and `WS_MAXIMIZEBOX` and the
  top resize border with them.

  Not done: the presenter is where a window's *insets* should come from now that the app draws
  the buttons - roots still pad themselves from `SystemOverlayMetrics.RightInset`, which is the
  shell's number and is 0 on Win32. `WebAppWindow` and `PaymentFormPage` keep a 46px spacer where
  their button was.

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

- [x] **1.10 Mica / acrylic backdrop.** **Passes, but only through an undocumented API.**
  Measured on build 26200.

  Mica is the one item on this list UWP cannot have at all, because the backdrop is a DWM
  attribute on an HWND and a UWP app does not own one. Terminal's entire implementation is a
  single call (`IslandWindow.cpp:1847`):

      const int attribute = newValue ? DWMSBT_MAINWINDOW : DWMSBT_NONE;
      DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, &attribute, sizeof(attribute));

  Documented from 22621; on 22000 it fails silently and `DWMWA_MICA_EFFECT` (1029) is the only
  route, which Terminal never bothered to implement.

  **That call alone is not enough, and the failure is confusing.** With the attribute accepted
  (`hr == 0`) the backdrop appears in the **non-client area only** — acrylic shows in the title
  bar and the client area stays an opaque sheet. XAML islands paint what Terminal calls the
  "emergency backstop" behind the island content, and nothing in the public surface turns it off:
  root `Background = null`, `WS_EX_NOREDIRECTIONBITMAP`, suppressing the class brush on
  `WM_ERASEBKGND` and `DwmExtendFrameIntoClientArea(-1,-1,-1,-1)` together do not.

  Terminal removes it with `TerminalTrySetTransparentBackground` (GH#603), which comes from
  `Microsoft.Internal.Windows.Terminal.ThemeHelpers` — **an internal package, not on nuget.org**.
  What it does, read out of the shipped `TerminalThemeHelpers.dll`: activate
  `Windows.UI.Xaml.Window`, call `IWindowStatics::get_Current`, QI the result for
  `{06636C29-5A17-458D-8EA2-2422D997A922}` (`IWindowPrivate`), and call vtable slot 7 — a boolean
  `put_TransparentBackground`. Reproduced in the spike as `WindowPrivate.cs`; the property reads
  back `False -> True` and the backdrop then fills the whole window, client area included,
  confirmed by A/B'ing `DWMSBT_NONE` against `DWMSBT_TRANSIENTWINDOW` on the live HWND.

  Two things fall out that matter beyond the effect itself:
  - **`Window.Current` is non-null inside an island.** Terminal depends on it, and the spike
    confirms it. It is a per-thread stub with no `CoreWindow`, which also means
    `TransparentBackground` is a **per-thread** switch, not per-window — one more entry on the
    list of things that are thread-scoped when we would want them window-scoped.
  - The dependency is on an **undocumented interface**. It is the same bet Terminal has been
    shipping for years, so the risk is low in practice, but it is a bet, and it should be
    isolated behind one call so the feature can be dropped rather than the host.

  Terminal's other Mica lesson, for when 1.7 lands: with a custom caption, set
  `margins.cyTopHeight = 0` rather than `-frame.top` (`NonClientIslandWindow.cpp:936`). Any
  non-empty top margin lets the DWM title bar — and the accent strip, when "show accent colour on
  title bars" is set — draw over the backdrop. Their comment calls the empty rect LOAD-BEARING:
  it is what makes DWM use `NCHITTEST` for the snap flyout.

- [x] **1.13 Text input inside a `ContentDialog` does not work in an island. Measured
  2026-08-24, and it is the largest constraint found so far.**

      ContentDialog TextBox  -  KeyDown 15, CharacterReceived 0, TextChanged 0

  Three runs, same answer. Keys reach the control - its own `OnKeyDown` fires, it takes focus by
  Tab, the caret blinks - and **`CharacterReceived` never fires**, so no character is ever produced
  and nothing is inserted. `RichEditBox` and `TextBox` both.

  **It is `ContentDialog` specifically.** A `Popup` in the same island, same window, same thread
  types perfectly - both `ShouldConstrainToRootBounds` true *and* false. So it is not focus, not
  the message loop, not popup hosting, and not the app's `ContentPopup`. Four theories died on the
  way to that, three of them mine: `WM_CHAR`/`TranslateMessage` (disproved by the main window's
  composer working), windowed popups (disproved by `ContentPopup` not setting the property), and
  focus (disproved by the caret).

  **The reach:** `ContentPopup` derives from `ContentDialogEx` derives from `ContentDialog`, and
  **153 files derive from `ContentPopup`**. Every dialog in the app that takes text is inert on this
  host - send-files captions, forward comments, folder names, link editing, polls.

  Also seen, and relevant because the app queues dialogs: `ShowAsync` threw
  `COMException: An async operation was not properly started` when a second dialog was shown while
  one was already open. Worth confirming against `ShowQueuedAsync` before designing anything.

  **And it is not the only thing wrong with `ContentDialog` here.**
  microsoft/microsoft-ui-xaml#3577 reports the dialog's backdrop staying at its initial size when
  the island window is resized while it is open - Fela hit that independently before finding the
  issue. No root cause, no workaround, and nothing has come of it. Two unrelated defects in the
  same control, neither being fixed, is the argument against waiting for this one to improve.

  **The CoreWindow route was tried, and it half works.** A thread hosting islands still gets a
  `CoreWindow` - a 1x1 invisible stub, a child of the top-level window - and #3577's workaround is
  to forward `WM_SIZE` to it by hand. Measured 2026-08-24:

  - **`WM_SIZE`: works.** The smoke layer resizes with the window. `Telegram\Host\CoreWindowBridge.cs`
    finds the stub by class name - it is a child, so no `ICoreWindowInterop` is needed - and posts
    to it from `WM_SIZE`. **Keep.**
  - **The keyboard: crashes.** Forwarding `WM_KEYDOWN`/`WM_CHAR` and friends to the same stub takes
    the process down. Removed.

  So the dialog's *layout* listens to that CoreWindow and its *input* does not, and gate 1.13's
  missing `CharacterReceived` cannot be reached from there. That was the cheap hope; it is gone.

  **Terminal never hit this, and that is why nobody has fixed it.** It is the flagship islands app
  and it uses `ContentDialog` heavily - `ConfirmCloseDialog`, `CloseReadOnlyDialog`,
  `MultiLinePasteDialog`, `LargePasteDialog`, `ControlNoticeDialog`, `UriErrorDialog` - and **not
  one of them contains an input control**. Its only `TextBox` is `WindowRenamerTextBox`, and that
  lives in a `TeachingTip`. So Terminal hit the resize bug hard enough to file it eighteen times
  (see microsoft/terminal a4cf4e276, which is the same `WM_SIZE` workaround we landed on
  independently) and never once typed into a dialog.

  Two things follow. **No upstream fix is coming**, because the problem is not reported - a minimal
  repro is about fifteen lines and the spike already has it, so filing it is cheap if we want to.
  And as a data point only - **not as a candidate** - a `TeachingTip` holds a working TextBox. With
  both `Popup` flavours typing fine too, the pattern is that everything except `ContentDialog`
  works, which makes rebuilding the popup host on a `Popup` the known-good path rather than a
  gamble. Fela on `TeachingTip` itself, 2026-08-24: *"terrible, definitely not a replacement for
  ContentDialog"* - and if anything in that family were to be reimplemented in the app today, it
  would be `TeachingTip` that needed it.

  **The options, none of them small:**

  1. **Reimplement the popup host on `Popup`, for this host only.** Popups demonstrably work, and
     `ContentPopup` is already the app's own wrapper - so if its public surface is preserved
     (`Title`, `PrimaryButtonText`, `ShowAsync`, `Hide`, the queueing), the 153 subclasses need not
     change. It means rebuilding what `ContentDialog` supplies: layout, buttons, modality, the
     smoke layer, focus and dismiss.
  2. **Phase 3** - a `Microsoft.UI.Xaml` island, where `ContentDialog` is WinUI's own implementation
     rather than the system one. Bigger, but this is the second thing pointing that way after
     acrylic.

- [ ] **1.13a Reimplementing `ContentPopup` on `Popup` - the point, made 2026-08-27.** Gate 1.13
  is the only remaining blocker, and the reason it looks frightening is that "rebuild `ContentDialog`"
  sounds like rebuilding a control. It is not, and the difference is worth writing down before
  anyone estimates it.

  **Most of that control is already ours.** `ContentPopup` sets `DefaultStyleKey` and ships its own
  `ControlTemplate` - Generic.xaml:4498, ~390 lines - with its own named parts (`LayoutRoot`,
  `BackgroundElement`, `BorderElement`, `ContentElement`, `CommandSpace`, `PrimaryRoot`,
  `PrimaryButton`, `DismissButton`, an explicit `LightDismiss` rectangle, `AnimationElement`) and
  its own visual states for showing, sizing and every button arrangement. The layout, the chrome,
  the smoke layer and the animations are app code today.

  **What `ContentDialog` still supplies is four things**, and only these:

  1. Hosting itself - it puts its template into a `Popup` on the `XamlRoot` and makes it modal.
  2. `ShowAsync` / `Hide` and the `ContentDialogResult` that comes back.
  3. Focus: trapping it inside the dialog and restoring it after.
  4. `Opened` / `Closing` / `Closed`, which `ContentPopup` and its subclasses hang behaviour on.

  A `Popup` gives (1) directly, and gate 1.13 measured that a `Popup` types correctly in an island
  while a `ContentDialog` does not. (2) is a `TaskCompletionSource` - `ContentPopup` already owns
  the queueing around it in `ShowQueuedAsync`. (4) is four events to raise at the right moments.

  **(3) is free, measured 2026-08-27.** I claimed focus containment was the real work and the thing
  to judge the estimate by; Fela's instinct was that a `Popup` already contains focus, and he is
  right - a modal `Popup` (`IsLightDismissEnabled = false`) with two `TextBox`es and a button keeps
  Tab inside itself, confirmed by hand in the spike. Three synthetic probes of mine said otherwise
  and all three were wrong for the same reason: they ran as startup gates, before the window was
  shown, and **nothing takes focus in an unactivated window** - so `Focus()` returned false and
  `FindNextElement` had no starting point. The spike now carries a button that opens the popup once
  the window is live, which is the only way to ask this.

  What is left of (3) is small and worth naming so it is not forgotten: Escape and Enter, and
  restoring focus to whatever had it when the dialog closes. Neither is a wall.

  **So the shape of the work is: keep the class, keep the template, replace the base.** If
  `ContentPopup`'s public surface is preserved - `Title`, `PrimaryButtonText`, `ShowAsync`,
  `ShowQueuedAsync`, `Hide`, `IsLightDismissEnabled`, the sizing properties, the events - none of
  the 153 subclasses change. That is the whole reason this is tractable: the blast radius is one
  class and one template, not 153 files.

  **Which host gets it.** Fela, repeatedly and rightly: *"I'd prefer to keep UWP as it is as it
  ships to millions."* So build it, ship it on Win32 first, and leave UWP on `ContentDialog` -
  which the fork already supports without a second implementation of anything above the base class.
  If it proves out, UWP can follow later and the app is rid of `ContentDialog`'s other bug at the
  same time (microsoft-ui-xaml#3577, the backdrop that keeps its opening size). If it does not, the
  UWP flavour never knew.

  **The blast radius, counted rather than feared.** "Keep the surface and nothing changes" is true
  of the 153 subclasses' *bodies*, but not of their *signatures*: 92 files name `ContentDialog` and
  its event-arg types in handler signatures, 118 occurrences, all of the shape
  `(ContentDialog sender, ContentDialogButtonClickEventArgs args)`. Those types are sealed with no
  public constructor, so a new base cannot raise them and every one of those signatures has to
  resolve to something else - a name alias per file or globally, which leaves the bodies alone but
  is still 92 files touched. Worth knowing before starting; also worth knowing what is *not* there:
  no `new ContentDialog()` anywhere in the app, and no subclass overrides `OnPrimaryButtonClick`
  and friends. In XAML it is 7 files, using `<ContentDialog.Resources>` property-element syntax,
  which only needs the base type's name.

- [x] **1.13 SOLVED 2026-08-27: `ContentDialog` marks every key handled, and in an island that
  kills text input.** The blocker that was going to cost a reimplementation is four lines.

  `ContentDialog` attaches an accelerator handler to its `LayoutRoot` template part in
  `OnApplyTemplate` - unconditionally, `ContentDialog_Partial.cpp:240` - and marks **every key but
  Escape and Enter** handled, so the dialog behaves like a modal. It then sets an internal
  `HandledShouldNotImpedeTextInput` flag so the InputManager skips `SetKeyDownHandled` and the
  character still reaches the focused text box:

  ```cpp
  get_Handled(&bAlreadyHandled);
  if (!bAlreadyHandled) { put_HandledShouldNotImpedeTextInput(TRUE); put_Handled(TRUE); }
  ```

  **In an island that exemption does not hold.** The key is handled, the character is dropped, and
  everything that does not depend on characters keeps working - which is exactly the symptom that
  made this so hard to place: Tab navigates inside the dialog, the context menu pastes, and
  `KeyDown` arrives *unhandled* at the text box because the marking happens after it.

  **The fix**: `ContentPopup` already subscribes to that same event on that same element
  (`ContentPopup.cs:306`), and subscribing later means running later - so it can put `Handled` back.
  Only for unmodified keystrokes, so a real accelerator stays handled and still cannot reach the
  window behind. `ContentPopup.Win32.cs` implements a `partial void ReleaseTextInput`; the UWP
  flavours have no implementation and the call is elided, so nothing about the shipping app changes.

  **How it was found, because the route matters more than the answer.** Building variants up from a
  minimal template and guessing which stock feature was poison failed nine times - part names,
  `LayoutRoot`, the `ScrollViewer`, `TabFocusNavigation="Cycle"`, stretching, collapse-then-reveal,
  the named parts, buttons, a real `DialogShowingStates` group, `ScaleTransform`,
  `TranslateTransform`, `SetIsTranslationEnabled`. What worked was instrumenting the actual failing
  dialog: subscribe `ProcessKeyboardAccelerators` on every element from the text box up to the
  dialog and print `Handled` at each hop. The two logs then differ in one place.

  | | route |
  | --- | --- |
  | minimal template | `TextBox -> StackPanel -> ContentPresenter -> LayoutRoot -> dialog`, all `handled=False` |
  | stock template | bubble, then a `TryInvokeKeyboardAccelerator` walk down and back, then `LayoutRoot: handled=True`, and the event never reaches the dialog |

  Two traps met on the way, both worth remembering. Touching the tree from inside a
  `ProcessKeyboardAccelerators` handler takes the process down with no managed exception - collect
  the lines and flush them from a timer. And the spike installs no `SynchronizationContext`, so
  `await Task.Delay` resumes on the thread pool and the next XAML call throws `RPC_E_WRONG_THREAD`;
  tick a `DispatcherTimer` instead. The same absence is already recorded for the share target.

  **What this retires.** 1.13a is dead: no `Popup` of our own, no `ArrangeOverride`, no
  `ShowAsync`/`Hide` replacement, no new event plumbing, and none of the 92 files carrying
  `(ContentDialog sender, ContentDialogButtonClickEventArgs args)` signatures are touched. Also
  recorded, since it cost real time to establish and might be wanted later: a `ContentDialog` *can*
  be hosted in a `Popup` by hand, as long as the subclass overrides `ArrangeOverride` to arrange the
  template child at `finalSize` - its own arrange returns nothing unless `ShowAsync` set up its
  hosting.

- [x] **1.11 Resource scope across islands.** Measured, and it decides 0.18. Three probes, each a
  `Border` whose `Background` is `{ThemeResource ScopeBrush}` parsed at runtime, with red defined
  in `Application.Resources` and green scoped to the first island's content:

  | probe | resolved |
  | --- | --- |
  | island A content | **green** - the island's own scope |
  | popup opened on island A | **red** - `Application.Resources` |
  | island B, same thread | **red** - scopes do not leak between islands |

  **Islands do not rescue popups.** A `PopupRoot` is a sibling of the content under the XamlRoot,
  so its lookup reaches `Application` without passing through anything the app scoped - exactly as
  in UWP. But **content scopes are genuinely per island**: a dictionary on one island's content is
  invisible to another on the same thread.

Record results back in this file. If 1.3 fails and cannot be worked around, close the path and
say so here.

### The .NET 10 teardown AV — fixed, and not by islands

Recorded here because it briefly looked like an argument for hurrying this project, and it was
not. Closing a secondary view access-violated inside `RoUninitialize`: a XamlDirect handle's RCW,
finalized after that view's XAML core was released, marshalled its `Release` back into the
uninitializing apartment through `IContextCallback` and unparented a core-less
`CDependencyObject`. Both ends were confirmed from a dump —
`WinRT.ObjectReferenceWithContext<T>.Release()` on the finalizer thread, and
`interface_forwarder<IXamlDirectObject,CDependencyObject>::Release` on the dying ASTA. Filed as
[CsWinRT #2532](https://github.com/microsoft/CsWinRT/issues/2532); the mechanism and the fix live
in `notes/net10-port-todo.md`.

Two things it settled that bear on this plan:

- **It was not a reason to go to islands.** Islands would dissolve it — one thread means no
  apartment is ever uninitialized until process exit — but the production path is .NET Native ->
  .NET 10 UWP, which has thread-per-view, so it had to be fixed on its own terms. It was, by
  releasing the handles deterministically while the core is still up.
- **0.19b is therefore not on a critical path**, which an earlier draft of 0.18 implied. Letting
  every window share a thread is still the destination, but it buys tidiness now, not a fix.

## Phase 2 — the second host, added beside the first

### Which classes fork — ranked by the code, 2026-08-23

Counting app-model API hits per file (`CoreApplication`, `ApplicationView*`, `CoreWindow`,
`SystemNavigationManager`, `CoreDispatcher`, `Window.Current`; `WindowContext.Current` excluded)
sorts the app into three tiers. The long tail is Phase 0 work, not forking — only the top tier
needs two implementations.

**Tier 1 — two implementations, one per host.**

| Class | Hits | UWP does | Win32 does |
| --- | --- | --- | --- |
| `WindowContext` | 54 | `Window` + `CoreWindow` + `ApplicationView` | HWND + `DesktopWindowXamlSource` + `IslandNative` |
| `ViewService` + `ViewLifetimeControl` | 34 | `CreateNewView` + `ApplicationViewSwitcher` | create an HWND, on this thread or a new one per 0.18 |
| `BootStrapper` + `App.xaml.cs` | 10 | `Application`, `OnLaunched`, activation, suspend | `WinMain`, `WindowsXamlManager`, own message loop |
| `InputListener` | 6 | `CoreWindow.GetAsyncKeyState` | unchanged — the stub answers key state (2.1g) |
| title bar — no class yet | — | `ExtendViewIntoTitleBar` + `CoreApplicationViewTitleBar` | `WM_NCCALCSIZE` / `WM_NCHITTEST` + drag-bar HWND (1.7) |
| tray icon | — | `Telegram.Stub`, a second process | `Shell_NotifyIcon` in-process (2.5) |

**Tier 2 — one implementation, but a behaviour difference to design.**

- `GalleryCompactOverlay` (4) — `ApplicationViewMode.CompactOverlay` is a UWP feature with no
  Win32 equivalent; picture-in-picture becomes a small topmost window the app positions itself.
  The only item here where the *feature*, not the API, differs.
- `WindowContent` (9), `CorePage` (4), `StandaloneWindow` / `TabbedWindow` / `VoipWindow` (3/2/3)
  — almost entirely title-bar plumbing, and 0.3c already turns it shared: `SystemOverlayMetrics`
  in `CorePage.cs` is an app-owned value object built from `CoreApplicationViewTitleBar`, which
  is exactly the right shape. After 0.3c only the *source* of the metrics forks, not the roots.
- `OverlayWindow` (3) — check whether its `SystemNavigationManager` use survives 0.5.

**Tier 3 — no fork, only Phase 0 call sites.** 1-3 hits each, all mechanical: `WatchDog`,
`TLNavigationService`, `MessageHelper`, `SettingsLanguageViewModel`, `LegacyIncrementalCollection`,
`Extensions`, `Interop`, `InstantPage`, `DiagnosticsViewModel`, `ChatView`, `GalleryWindow`,
`GalleryTransportControls`, `ChooseChatsPopup`, `SendFilesPopup`, `SendLocationPopup`,
`EditMediaPopup`, `RichTextWindow`.

**How to fork: by file inclusion, not `#if`.** The repo already builds two flavours from one tree,
and `Telegram.csproj` lists every file explicitly (~1240 entries) rather than globbing — so a third
project for the Win32 host selects its own files for free. Each Tier 1 class becomes a shared
`partial` plus a per-host partial: `WindowContext.cs` keeps `XamlRoot`, `_mapping`, `Theme`,
`NavigationServices` and the per-window services, while `WindowContext.Uwp.cs` /
`WindowContext.Win32.cs` supply activation, bounds and title bar. No
preprocessor branches in shared code, and the shared half stays readable. Agreed naming is
`WindowContext.Uwp.cs` / `WindowContext.Win32.cs` — `.Modern.cs` would collide with the .NET 10
project's meaning of the word.

Partials also give the contract for free: shared code calling `Activate()` will not compile
unless every host partial defines it, so no interface is needed, and with it no virtual dispatch,
no DI registration and nothing the AOT compiler cannot see through. Four of the five Tier 1
classes are already `partial`.

**`BootStrapper` is the exception** and wants treating as its own item: it is
`abstract class BootStrapper : Application`, and the Win32 host has no `Application` at all, so
the base type differs rather than the members. It needs a host-agnostic core with
`BootStrapper : Application` on one side and a plain class on the other — a bigger rework than
the partial split the others take.

**Also host-specific, and not a class today:** installing a `DispatcherQueueSynchronizationContext`
on every UI thread. UWP does it per view for free; without it every `await` on a TDLib result
resumes on a TDLib thread, and the failures look like anything but a missing sync context.

**Confirmed 2026-08-24, and the source is exact**: the Main the XAML compiler generates - the one
`DISABLE_XAML_GENERATED_MAIN` suppresses - is where it was being set:

    Application.Start((p) => {
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
        new App();
    });

Taking over the entry point means carrying that line across, and `Host\Program.cs` now does, before
constructing `App`. Anything that gives a thread an island will have to do the same - the spike
never noticed because it awaited nothing.



**The Win32 flavour ships as a *packaged* Win32 app** — Fela, 2026-08-24: "the codebase
definitely needs an identity to run". That is a decision, not a preference, and it settles more
than it looks:

- **Package identity is what the app is built on.** `ApplicationData.Current` (settings,
  `LocalState`, the TDLib database path), notifications, background tasks, protocol and
  file-type activation, the share target — all of it needs identity, and all of it keeps
  working unchanged. None of it joins the fork.
- **Registration-free WinRT never comes up.** The package manifest declares the activatable
  classes, exactly as it does today, so `Telegram.Native` / `Telegram.Native.Calls` activate the
  way they always have. An unpackaged host would have had to fall back to
  `DllGetActivationFactory` probing.
- **Full trust, not AppContainer.** A packaged Win32 app is an `Application` entry with
  `EntryPoint="Windows.FullTrustApplication"`, which *requires* `runFullTrust` — see 2.4, which
  said the opposite.

So the surface that actually forks stays what the file count measures: the view and app-model
APIs, and nothing else.

That is a scope limit, and the right one: identity is how the host gets running soonest.
**What it would cost to drop later is measured in `notes/unpackaged-win32.md`**, written the same
day — the surface item by item, so the question can be picked up on evidence. Short version: the
mechanical coupling is about three days, because settings are one live line and the access cache
is two files; what actually holds the app to identity is the share target, the app URI handler and
the MSIX update channel, which are product calls rather than porting ones.

One correction to the second bullet below, measured off the generated manifests on disk. The
source `Package.appxmanifest` declares no activatable classes at all — the packaging targets
generate them — and **what they generate differs by flavour**:

| Flavour | `InProcessServer` paths | `Telegram.Native*` classes |
|---|---|---|
| `Telegram.csproj` (UWP, ARM64 Release) | `Telegram.Native.dll`, `Telegram.Native.Calls.dll`, `RLottie.dll`, Win2D, WebView2 | **60** |
| `Telegram.Modern.csproj` | Win2D, WebView2 | **0** |
| `Telegram.Win32.csproj` | Win2D, WebView2 | **0** |

Win2D and WebView2 are declared because their own NuGet targets do it. The legacy project
declares the native components; **neither SDK-style project does**, and `RLottie.dll` drops off
with them, even though all three DLLs sit in the payload.

So "the package manifest declares the activatable classes, exactly as it does today" is not what
happens here — but the conclusion drawn from it is wrong in the *other* direction, and in a way
that helps. **Registration-free WinRT is already what these two flavours run on**: the Modern
build works today with zero declarations, so activation is resolving through
`DllGetActivationFactory` probing rather than through the manifest. C++/WinRT has that fallback
in `base.h` (line 6131) and CsWinRT has the managed equivalent.

That removes the risk rather than adding one: the Win32 host does not need those entries, because
the mechanism it already depends on never consulted them. It also means the *unpackaged* case
changes nothing about activation, which is the single biggest de-risking in
`unpackaged-win32.md`.

- [ ] **2.0** Retire the .NET Native project **when .NET 10 has earned it**, not before, and not
  as a precondition for anything here. It is what ships to users; "three flavours is one too
  many" is a tidiness preference and cannot outrank not breaking the app. Three configurations
  is the accepted cost of the transition.

  This corrects an earlier reading of this item that treated it as a gate on Phase 2. It is not:
  the islands host ships to nobody and carries no user risk, so it needs time rather than
  permission.
- [x] **2.1a The Win32 flavour builds. 2026-08-24, `Telegram.exe` out of `bin\win32\`.**
  This is the headline of the whole phase, and it is much further along than any estimate here.
  The whole app - every page, control, view model and service - compiles and links into a plain
  desktop process. It has not been *run*; that needs package identity, which is the next item.

  It took four fixes past the first build, and two of them were corrections to this plan rather
  than new work:

  - **`InputListener`** - forked. See item 0.1, where the detail lives.
  - **The `#region Legacy code` activation methods were never UWP-specific.** They went into
    `WindowContext.Uwp.cs` during the split because they name `IActivatedEventArgs`, but that is a
    projection both hosts have - a packaged Win32 app is activated with the same args - and the
    three `Activate(...)` overloads are app logic: switch on the authorization state, navigate.
    They moved back to shared, which makes the fork *smaller*. Only `UpdateTitleBar` /
    `ClearTitleBar` were ever per host, and that region is now called `#region Title bar`.
  - **`BootStrapper` does not fork as a class.** The note said its base type differs and called it
    "a bigger rework than the partial split the others take". Wrong twice over: `App : Application`
    works inside an island (gate 1.3a), and of its 827 lines exactly one method needed moving.
    `BootStrapper.Uwp.cs` is 127 lines - `OnWindowCreated`, the window `Activated`/`Closed`
    handlers, `OnWindowClosed`, `CreateWindowWrapper` - and `BootStrapper.Win32.cs` is 43. All the
    rest, activation included, compiles against either host: they are overrides the framework
    simply never raises when there is no `Application.Start`.
  - **One seam, `private partial WindowContext EnsureWindowContext(IActivatedEventArgs e)`.**
    `InitializeFrame` needed a context and used to build one from `Window.Current`; now each host
    supplies it - UWP reuses what `OnWindowCreated` made, Win32 creates an `IslandWindow`. A
    partial method with a return type has to be implemented, so the contract is a compile error.
  - **`OnWindowActivated(Window, bool)` became `OnWindowActivated(WindowContext, bool)`** - one
    virtual, one override in `App.xaml.cs`, no behaviour change, and the last `Window` in a
    signature the app overrides. Item 0.2. The UWP handler passes `WindowContext.Current`, which
    is legitimate *there*: it runs on the window's own thread and a UWP view has one window.

  `Telegram/Telegram.Win32.csproj` is `Telegram.Modern.csproj` with four differences: its own
  `obj\win32\` / `bin\win32\`, `Exe` + our own `Program.Main` instead of `WinExe`,
  `<Compile Remove="**\*.Uwp.cs" />`, and no MSIX packaging yet. `Telegram.Modern.csproj` gained
  the mirror image — `Remove **\*.Win32.cs` and `Remove Host\**` — and still builds clean.

  The host came straight out of the spike into `Telegram\Host\`: `IslandWindow` (+ its
  non-client half), `IslandNative`, the `Win32` P/Invokes, and a `Program.Main` that sets DPI
  awareness, makes a `DispatcherQueue`, constructs `App` *before* `WindowsXamlManager` (gate
  1.3a) and pumps its own loop. `WindowContext.Win32.cs` implements the contract: real where the
  spike answered it (activation, foreground, key state, `Content`, `Detach`), `NotImplemented`
  where it did not, and **absent** where the member is UWP-only, so its callers are errors.

  **The first build stopped at eight errors in three files** - listed here because the shape of
  the list is the result, not its length:

  - `InputListener.cs:22,23,28,29` — `context.CoreWindow` four times. Already item 0.1's
    "small class, real rewrite": accelerator keys and pointer-pressed off `CoreWindow`.
  - `BootStrapper.cs:134` — `context.CoreWindow == window.CoreWindow`, and `:144`
    `CreateWindowWrapper(Window)`. The `BootStrapper` fork, and both are in window creation.
  - `App.xaml.cs:85` — `GetNavigationService(Window)`, and `:178` —
    `Activate(IActivatedEventArgs, INavigationService, AuthorizationState)`.

  Nothing on that list was a surprise; every one was already catalogued, and all eight are now
  fixed. What the build proved even before that is the part worth keeping: **the rest of the app
  needs no change at all** to compile against a desktop host. The Phase 0 items that remain are
  real work, but they are not in the way of a Win32 build any more.

  Two things learned standing it up:

  - **`DISABLE_XAML_GENERATED_MAIN` is not enough here.** The constant is set, and the guard is in
    the generated `App.g.i.cs`, but the UWP XAML targets rewrite `DefineConstants` after the
    project body runs, so `Application.Start` survives and collides with ours (CS0017).
    `<StartupObject>Telegram.Host.Program</StartupObject>` settles it. The spike never hit this
    because its own `App` is trivial and its TFM has no UWP app model attached.
  - `WindowContext.Id` is an `int` and an HWND is not. Nothing outside the app reads it now that
    `ViewService` returns `WindowContext` (item 0.4), so the Win32 half hands out an incrementing
    counter — which is also the only thing that means anything on a host with no view ids.

- [x] **2.1b Packaged, registered, and it starts. 2026-08-24.** It does not get a window yet -
  it dies in `App`'s constructor - but everything before that works, and the failures on the way
  were all worth having.

  **Startup.** `Program.Main` calls `app.Start(AppInstance.GetActivatedEventArgs())`;
  `BootStrapper.Win32.cs` dispatches to `OnLaunched` or `CallInternalActivated`. Real activation
  arguments rather than an invented launch, so protocol links, share targets and file activation
  all reach the existing paths for free.

  **Identity.** `Telegram.Win32.csproj` patches the one real manifest, as the Modern project does:
  a third identity (`38833FF26BA1D.UnigramWin32`), `EntryPoint="Windows.FullTrustApplication"` on
  the application and on the two `uap5:Extension` entries that name the class, and
  `TargetDeviceFamily` to `Windows.Desktop`. Register with `Add-AppxPackage -Register` against
  `publish\AppxManifest.xml`, never the build output directory - see net10-port-todo Phase 4.
  Remove with `Get-AppxPackage 38833FF26BA1D.UnigramWin32 | Remove-AppxPackage`.

  Four things had to be true before it would even register, and none were obvious:

  - **`AppxPackage` and `EnableMsixTooling` are inert without
    `Microsoft.Windows.SDK.BuildTools[.MSIX]`.** Telegram.Modern.csproj gets them implicitly;
    this project did not, and the symptom is silence - the build succeeds and simply produces no
    `AppxManifest.xml` and no `resources.pri`. `-getProperty:AppxPackageDir,ProjectPriFullPath`
    coming back empty is the tell. They are explicit `PackageReference`s here.
  - **A full trust application cannot host an in-process app service.** `windows.appService` fails
    registration with 0x80080204 wanting an EntryPoint it cannot name. Telegram.Stub's bridge is
    on the list to retire here anyway (2.5), so it is removed rather than ported.
  - **`XmlPoke` cannot delete nodes or add them**, which is what the capability trimming of item
    2.4 needs. `Package.Win32.xslt` does the deletions and the one insertion; the csproj keeps
    XmlPoke for the renames. One manifest is still the source of truth.
  - **`Capabilities` is an ordered element** - every `Capability` before the first
    `DeviceCapability` - so `runFullTrust` has to be inserted between the two groups, not
    appended. Appending is 0xC00CE014.

  Also worth doing before installing anything: the package claims `tg:`, `tonsite:`, a
  `Telegram.exe` execution alias and `.unigram-theme`, on a machine where Telegram is actually
  used. They are renamed out of the way (`tg-win32`, `TelegramWin32.exe`, ...) so a real link or
  file still reaches the shipping app.

  **Then two runtime failures, in order:**

  - **`DisableRuntimeMarshalling` is on, and `Telegram.Host` was written with `DllImport`.**
    `MarshalDirectiveException` on the first P/Invoke. The app converted everything to
    `LibraryImport` for exactly this reason, and the new host had to follow: the class is
    `partial`, `CharSet` is gone (every string here is already an `IntPtr` from
    `StringToHGlobalUni`), and each `bool` needs an explicit
    `[return: MarshalAs(UnmanagedType.Bool)]` because a `bool` is not blittable.
  - **`REGDB_E_CLASSNOTREG` from CsWinRT is a lie**, and cost two wrong theories before the
    right one. `RLottie.LottieAnimation` threw it out of `ActivationFactory.ManifestFreeGet`, which
    reads as a registration problem. It is not: **nothing registers these components in either
    flavour** - both manifests carry 107 `ActivatableClassId` entries and every one is Win2D or
    WebView2, out of their own NuGet snippets. CsWinRT resolves ours by LoadLibrary +
    `DllGetActivationFactory`, so the error only ever means *the DLL did not load*. Two reasons it
    did not, found by diffing the layouts against Modern's rather than by reasoning:

    - **The store CRT.** Modern's generated manifest declares `Microsoft.VCLibs.140.00.Debug` and
      this one did not, so nothing satisfied what the C++/WinRT components link against.
      `Package.Win32.Final.xslt` adds it - and it has to run on the *generated* manifest, after
      `_GenerateAppxManifest`, because the packaging targets append their own `TargetDeviceFamily`
      and `PackageDependency` entries afterwards and `Dependencies` is an ordered element.
    - **The vcpkg runtime.** `Directory.Build.props` gates `TelegramUsesVcpkg` on the project
      *name*, so `Telegram.Win32` got no ffmpeg, libvlc, dav1d, opus or lz4 and
      `Telegram.Native.dll` could not load either. One name added to that condition.

    The lesson worth keeping: **when a packaged app cannot activate an in-package component,
    diff its layout and its generated manifest against one that works** - the error text points
    at registration and the fault is almost always a load failure.

  - **Two UWP app model assumptions in shared code**, both fixed with the same partial seam as
    `SetHostContent`, both behaviour-identical on UWP:

    - **`Application.RequestedTheme`** fails fast in an island. Fela's rule is that it must be set
      before `InitializeComponent` and cannot change after - and an island host never runs the app
      model startup during which it is settable at all, so it is not settable. It is not needed
      either: every island root already takes its theme from
      `NightModeService.GetCalculatedElementTheme` in `WindowContext.SetContent`. Now
      `protected partial void SetApplicationTheme(ApplicationTheme)`. **Open question** - popups
      resolve against `Application.Resources`, so whether the app theme still reaches them on this
      host is untested.
    - **`LaunchActivatedEventArgs.PrelaunchActivated`** is an `InvalidCastException`, not a false:
      the desktop activation arguments do not implement `IPrelaunchActivatedEventArgs`. Now
      `protected partial bool IsPrelaunch(LaunchActivatedEventArgs)`. `InternalLaunch` read it
      twice; the second read goes through the cached `PrelaunchActivated` property instead.

  - [x] **IT RUNS. 2026-08-24.** The real app, as a Win32 process, with a window titled
    "Telegram": 1024x720, responding, 50 threads, 420 MB. And it is genuinely alive rather than
    merely up - **TDLib initialised**, creating `local.db`, `langpack` and session folder `0`
    under its own `LocalState`, with no error reports.

    The host is doing its job: enumerating the top-level window shows
    `Windows.UI.Composition.DesktopWindowContentBridge` at 1008x712 and visible, with the gate
    1.7 drag bar above it at 1008x32.

  - [ ] **But the window is blank white, and that is where this stops.** The island exists and is
    sized, the app is running, nothing renders. The island being the right size means the failure
    is above it, in content rather than in the host.

    **`InternalLaunch` swallows everything.** `try { InitializeFrame(e); } catch (Exception) { }`
    with no log and no rethrow - which is why the window came up empty with no trace at all.
    Commenting it out is what found each of the following, and **it is currently commented out in
    the working tree, marked TEMPORARY, do not commit**.

    Two more of the same family as `PrelaunchActivated`, both fixed:

    - **`Toast.GetSession` reads `LaunchActivatedEventArgs.TileActivatedInfo`**, which lives on
      `ILaunchActivatedEventArgs2` and is not implemented by desktop activation arguments. The
      trap: **a pattern match on the interface passes anyway**, because the projection declares it
      on the class, so the QI only fails inside the getter. Catching `InvalidCastException` is the
      only way to ask.
    - **`WindowContext.Compositor`** - implemented as `Window.Current.Compositor`, and then
      `BootStrapper.Compositor` went the same way (Fela's call): the compositor is per *thread*,
      not per window, so it needs neither `WindowContext.Current` nor a fork. One more `Current`
      use gone.

    **Still empty, and the shape of it is now known.** With a debugger attached the Live Visual
    Tree shows `RootWindow` and its `Frame` - so content *is* set and the tree *is* live - but the
    Frame has no visual child, while the logs say it navigated to the auth page. Re-running the
    island `Layout()` when content is assigned changed nothing (the captured window is identical
    byte for byte), which weakens the zero-size theory. The open question is why a Frame that
    reports a successful navigation has no child: a page that threw while materialising would look
    exactly like this and, like everything else today, leave no trace.

    **`BackdropMaterial` was the first guess and it is not the cause** - Fela's, and worth testing
    because WinUI 2 applies it to the root page background. Putting it behind
    `partial void SetBackdropMaterial(WindowControl)` and doing nothing on Win32 changed the
    captured window not at all, byte for byte. The seam is kept anyway on its own merits: the
    Win32 host asks DWM for its backdrop (gate 1.10), so the two are alternatives, not layers.

    Still to probe, in order: whether `WindowContext.SetContent` runs at all on this path, whether
    `SetHostContent` reaches `DesktopWindowXamlSource.Content`, and whether the tree measures. A
    UI Automation dump of the window would separate "no content" from "content that renders
    invisibly" in one step and is the cheapest thing to try first.

- [x] **2.1d Building and running it - and from Visual Studio, 2026-08-25.** `Telegram.Win32.slnx`
  now exists, so this flavour is a project Fela can open and work on rather than one that only
  builds through a command line someone else knows:

      MSBuild Telegram.Win32.slnx -t:Build -restore -p:Configuration=Debug -p:Platform=x64

  Two things had to change for that to be true.

  - **The RID is defaulted from the platform** in the csproj. A RID-less build of this project
    resolves no package assets at all - `Telegram.Native`, `Rg.DiffUtils`, everything vanishes and
    the compiler reports thousands of errors that look like a broken projection rather than a
    missing property. `Telegram.Modern.csproj` never had to care because the packaging project that
    references it supplies one; this project has none, so it supplies its own.
  - **The native projects are `BuildDependency` entries in the solution**, not `ProjectReference`s
    in the project. Ordering is the only thing wanted - their outputs are consumed as projections,
    see net10-port-todo - and a reference would also make every worktree build vcpkg, tgcalls and
    webrtc rather than copying `x64\` in. Solution-level ordering is free of both problems, and it
    is also what gives the vcxprojs a `$(SolutionDir)` to write to: without one they write beside
    themselves and the app reads a stale projection.

  Publishing still wants the properties a package needs:

      MSBuild Telegram\Telegram.Win32.csproj -t:Publish -restore -p:Configuration=Release ^
        -p:Platform=x64 [-p:PublishAot=false]

  or `Build.Win32.ps1`, which does the whole msixbundle.
  - **`vswhere.exe` must be on `PATH`** for `PublishAot`, or ILC compiles and the native link step
    dies with MSB3073 `'vswhere.exe' is not recognized`, looking for `link.exe`. Prepend
    `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer`. Already recorded for the msixbundle
    recipe and still missed here.
  - **Check the payload before believing an AOT publish**: a real one is a ~50 MB `Telegram.exe`
    with no `Telegram.dll` and no `System.Private.CoreLib.dll` beside it. A failed link leaves the
    previous non-AOT layout in place, which runs fine and is not what was asked for.
  - **Registering is a manifest-only operation.** Publishing into the folder the package already
    points at needs no registration - just relaunch. See
    [[never-uninstall-to-update-a-dev-package]]: `Remove-AppxPackage` deletes `LocalState`, which
    is the TDLib database and the login.

- [ ] **2.1c First impressions of the running app, 2026-08-24.** What running it actually found,
  none of it visible from a build:

  - **An exception storm, not a slow build.** `SetPointerCursor` threw `NotImplementedException`
    from `FormattedTextBlock.OnPointerMoved` - once per pointer move over message text - and each
    one unwound through the WinRT ABI into `WatchDog`, which wrote a crash report **to disk**.
    Three reports in forty seconds of hovering. **The lesson: a stub that throws on a
    pointer-rate path is not a stub, it is a performance bug.** The implementation that replaced
    it - `LoadCursorW`/`SetCursor` - was itself wrong and is gone; see 2.1g.

  - **And then the rest of it was the JIT. This flavour needs NativeAOT to feel right.** Measured
    across three builds of the same sources: Debug laggy, **Release without AOT still laggy**, and
    Release with `PublishAot` smooth - Fela: *"The AOT build no longer lags, this is great."* So the
    exception storm above was real and separate, and optimised IL on CoreCLR was not enough on its
    own; what closed the gap was the native image. Worth carrying beyond this flavour, because it
    says the same about the .NET 10 port generally: CoreCLR JIT does not carry this app's UI.
  - **The drag bar was only laid out from `WM_SIZE`**, so the caption did nothing until the window
    was minimized and restored once. `Create` lays it out now - though that alone did not fix it,
    so the cause is not only this.
  - **Minimize sized the island to 0x0**, because `WM_SIZE` reports a 0x0 client rect and `Layout`
    applied it faithfully; restoring then animated the whole tree back. `SIZE_MINIMIZED` is
    skipped now.
  - **The drag bar was registered in the same map as its parent**, so `IslandWindow.All` held every
    window twice: the message loop pre-translated each message once per entry, an interop call
    doubled on the hottest path in the process, and `Windows.Count` could never reach zero, so
    closing the last window would never have quit. They have their own map now.
  - **Acrylic does not work, and it is the platform rather than us.** Flyouts and context menus
    fall back to opaque. Fela recalled it as an XAML Islands limitation and it is:
    CommunityToolkit/Microsoft.Toolkit.Win32#160 reports acrylic rendering opaque - black, there -
    inside `WindowsXamlHost` no matter how transparent the host window is made
    (`WindowStyle=None`, `AllowsTransparency=True`). No resolution was ever posted and the repo was
    archived in August 2023. It fits the mechanism: in-app acrylic consults
    `CompositionCapabilities.GetForCurrentView()`, which is per *view*, and host backdrop acrylic
    wants a real `CoreWindow`.

    **And then it got better.** microsoft/microsoft-ui-xaml#2355 draws the distinction that
    matters: **`Windows.UI.Xaml.Media.AcrylicBrush` works in islands, `Microsoft.UI.Xaml.Media`'s
    does not** - every `XamlCompositionBrushBase` brush from WinUI 2 falls back to its
    `FallbackColor` there. Closed as not planned, so it will not be fixed upstream.

    That fits this app precisely: the flyout and menu styles come from WinUI 2's
    `XamlControlsResources`, so every acrylic in them is the broken one. It also suggests a
    workaround rather than a redesign - **override the WinUI 2 acrylic resource keys with system
    `Windows.UI.Xaml.Media.AcrylicBrush` instances, in the Win32 flavour only.**

    **And then Fela measured the catch, 2026-08-27.** The system brush works, but with
    `BackgroundSource="Backdrop"` it drops to its `FallbackColor` **as soon as two islands share
    the same brush instance**. Not two islands - two islands *sharing one brush*. So the override
    cannot be a single app-level dictionary, which is where the paragraph above was heading:
    `Application.Resources` is exactly the scope that guarantees sharing.

    It has to be **one instance per window**, and the app already has both halves of that:
    `WindowContext.SetContent` merges a per-window dictionary into the presenter's resources, and
    `Extensions.cs:1139` forwards the window's brushes to popups and flyouts - which is what gate
    1.11 says is needed, since a popup resolves against `Application.Resources` rather than the
    tree it was opened from.

    Worth knowing before it is written: the two brushes this bites today are declared in
    **App.xaml** - `PageSubHeaderBackgroundBrush2` and `AcrylicToastFillColorBaseBrush`, both
    `BackgroundSource="Backdrop"` - so on this host they are already shared across every window by
    construction, and already in fallback the moment a second window opens.

    Not the DWM-private route of selastingeorge/Win32-Acrylic-Effect, which Fela raised: that is
    window *backdrop* acrylic with no XAML involvement, so it cannot reach a flyout inside the
    island - and it says of itself that it redraws only on activation, which a chat window would
    show as a stale or flat menu. Gate 1.10's Mica is already the supported answer for the backdrop.

    **Gate 1.12 passes, 2026-08-24.** The spike puts a `Windows.UI.Xaml.Media.AcrylicBrush` on a
    Border with a deliberately hideous magenta `FallbackColor`, and it renders as tinted
    translucent acrylic - so the system brush works inside an island. The probe beside it reports
    `CompositionCapabilities.GetForCurrentView()` returning **AreEffectsSupported True,
    AreEffectsFast True**, which also kills the theory that a failing capability check drives the
    fallback: capabilities are fine, and it is `Microsoft.UI.Xaml`'s own implementation that
    refuses, exactly as #2355 says.

    **So the workaround is real.** What remains is app work rather than research: find which
    resource keys the flyout and menu styles actually consume - `XamlControlsResources` defines
    them, so the list is finite - and merge a Win32-only dictionary that redefines those keys as
    system `Windows.UI.Xaml.Media.AcrylicBrush` instances. Popups resolve against
    `Application.Resources` (gate 1.11), so that is where it goes. Untested risk: WinUI 2 styles
    may reference the brushes with `{ThemeResource}` by key, in which case an override lands
    cleanly, or construct them inline, in which case the style has to be replaced too.

- [x] **2.1f Two host differences that only show at runtime, 2026-08-24.**

  - **The current directory is not the install folder.** UWP guaranteed it was; the shell launches
    a packaged Win32 app with whatever directory the launcher had, `system32` as a rule. So every
    relative path in the app and in its native libraries resolves against the wrong place.
    **MicroTeX is what surfaced it** - `RES_BASE` is the literal `"res"`, so `RichMathSurface`
    looked its fonts up as `resonts\...` and found nothing, while the files sat correctly in
    the layout. `Program.Main` now does `Directory.SetCurrentDirectory(AppContext.BaseDirectory)`
    before anything else. Worth remembering as a class of bug rather than one bug: anything
    relative is suspect on this host.
  - **Pointer input never reaches the message loop.** An island feeds it through its own
    `InputSite` - the `Windows.UI.Input.InputSite.WindowClass` child - so `WM_XBUTTONDOWN` is not
    in the queue to filter and `InputListener`'s back/forward gesture never fired. It now comes off
    a routed `PointerPressed` with `handledEventsToo` on the window root, attached from
    `SetHostContent`. That is the opposite choice to the keyboard, and deliberately: the tree
    handles neither XButton, so a routed handler still sees them, and none of the keyboard's
    reasons for needing the message loop - `SystemKeyDown`, a focused control swallowing the key,
    focus inside a WebView2 - apply to a mouse button nothing consumes.

- [x] **2.1g The stub `CoreWindow` is a real API surface inside an island, not just a handle.**
  Established 2026-08-24, after `SetCursor` failed and Terminal's source explained why.

  The starting assumption was that `CoreWindow` is the UWP app model and therefore absent here,
  so anything reading it had to be rewritten against Win32. That is wrong twice over: the stub
  exists, and for input state it is the *only* thing that answers correctly, because XAML's input
  site inside the island talks to it rather than to the top-level window.

  - **The cursor cannot be done with `SetCursor`.** The island - strictly its `InputSite` child -
    is the window under the pointer, and it answers `WM_SETCURSOR` itself, so a cursor set on the
    top-level window is overwritten on the next mouse move. What works is exactly what the UWP
    half does: `CoreWindow.GetForCurrentThread().PointerCursor`. `SetPointerCursor` is therefore
    **shared code again**, not forked - it took a fork and a fold-back to learn that.
  - **Terminal reads key state the same way, and it is island-only code.** Verified against
    `microsoft/terminal` `main`, 2026-08-24 - `CoreWindow::GetForCurrentThread().GetKeyState(...)`
    in `TermControl.cpp:2969` (`_GetPressedModifierKeys`), `TerminalPage.cpp:1449,1711,1925,3834`,
    `CommandPalette.cpp:258,421`, `SearchBoxControl.cpp:261`, `KeyChordListener.cpp`,
    `SuggestionsControl.cpp`. Terminal has no UWP flavour left - `OnLaunched` is empty and says so -
    so every one of those runs in an island.
  - **And pointer position** - `TerminalPage.cpp:6143`, `CoreWindow::GetForCurrentThread().PointerPosition()`,
    reading where a tab was dropped outside the tab view.

  **What this closes:** item 0.6's four `CoreWindow` shapes do not all need Win32 twins.
  `GetKeyState`, `PointerPosition` and `PointerCursor` can stay shared and keep reading the
  `CoreWindow`; the table above listing `GetAsyncKeyState` as `InputListener`'s replacement is a
  fallback, not a requirement. What genuinely does not survive is the *event* surface -
  `CharacterReceived`, `KeyDown`, `SizeChanged`, `Activated` on the `CoreWindow` - because nothing
  posts to the stub. That is the same split gate 1.13 found in `ContentDialog`: state reads
  through, events do not.

  **A lifetime trap that comes with it, from `WindowEmperor.cpp:275-295`.** XAML creates the
  `CoreWindow` implicitly, parented to the **first island on the thread**. Destroying that island
  destroys the `CoreWindow`, and *it cannot be recreated* - Terminal's comment calls it a WinUI
  bug. Terminal works around it by reparenting the `CoreWindow` to its own hidden initial window
  as soon as the first island exists. We do not, and our windows come and go: on a thread hosting
  more than one, closing the first would take the cursor and key state down with it for every
  window left. Terminal's second reason applies to us as well - on Windows 10 the `CoreWindow`
  shows on the taskbar as a visible window until it is reparented.

  Not a bug we have hit, because chat windows own their thread and the main window is the first on
  its own. Worth doing before that stops being true. `CoreWindowBridge.Resolve` already finds the
  stub by class name, so the reparent is three lines - though it caches, and would need to stop
  caching or be told, if the stub ever moves.

- [x] **2.1i Closing the last window crashed instead of exiting, 2026-08-24.** Found by Fela the
  first time the main window had a close button to press - until 1.7a there was none, so this path
  had never been taken.

  The log named it without a dump: the last app-side line was
  `[FormattedTextBlock.cs][ReleaseNative] released 11 block(s)`, then nothing, and no crash report -
  a native fault after managed teardown. That is the crash `WindowContext`'s shutdown drain was
  written for: this view's XamlDirect RCWs are context bound, and releasing one from the finalizer
  thread once the XAML core is gone faults on a null `CCoreServices`. The drain hangs off
  `DispatcherQueue.ShutdownStarting` - **and nothing on this host ever shuts the queue down**, so
  it never runs.

  Fixed the way Terminal fixes it: `TerminateProcess` after the message loop rather than unwinding.
  Its comment is the justification - *"There's a mysterious crash in XAML on Windows 10 if you just
  let _app get destroyed (GH#15410)"*, plus every UI thread having to be gone before the main
  thread returns, and `std::exit` being no good because `ExitProcess` still runs the teardown that
  faults. **The flagship islands app cannot exit cleanly either**, which is worth knowing before
  anyone spends a day trying to.

  Left open: running the drain properly on this host - shut the `DispatcherQueue` down after the
  loop, pump until it completes, then terminate. That would make the release ordering right rather
  than merely unreachable. Bounded pumping is the fiddly part: after `WM_QUIT` a plain `GetMessage`
  blocks, so it needs `PeekMessage` with a deadline.

- [x] **2.1j The caption has no system menu. Done 2026-08-25.** Fela, 2026-08-24. Right-clicking the draggable area
  does nothing, where a real caption opens Restore / Move / Size / Minimize / Maximize / Close.
  Alt+Space is the same menu and is presumably just as dead.

  **Why it is missing rather than broken.** The drag bar does forward `WM_NCRBUTTONUP` to the
  parent for `HTCAPTION`, and the parent's `DefWindowProc` answers it by sending `WM_CONTEXTMENU` -
  which it then hit-tests against the parent itself. After `WM_NCCALCSIZE` the parent's non-client
  area is only the borders, so the point comes back `HTCLIENT` and no menu is shown. Forwarding
  more messages will not fix it; the menu has to be raised by hand.

  **Terminal's recipe**, `IslandWindow::OpenSystemMenu`, called from its own `WM_NCRBUTTONUP`:

  - `GetSystemMenu(hwnd, FALSE)` for the menu.
  - Enable or disable each item from `GetWindowPlacement().showCmd == SW_SHOWMAXIMIZED`:
    `SC_RESTORE` only when maximized, `SC_MOVE`/`SC_SIZE`/`SC_MAXIMIZE` only when not,
    `SC_MINIMIZE` and `SC_CLOSE` always.
  - `SetMenuDefaultItem(menu, UINT_MAX, FALSE)` so nothing is bold.
  - `TrackPopupMenu(menu, TPM_RETURNCMD, x, y, 0, hwnd, nullptr)`, then
    `PostMessage(hwnd, WM_SYSCOMMAND, ret, 0)` - return the command and post it rather than letting
    the menu dispatch it.

  Written as `IslandWindow.OpenSystemMenu`, raised from the drag bar's `WM_NCRBUTTONUP` for
  `HTCAPTION` and from `WM_SYSCOMMAND`/`SC_KEYMENU` for Alt+Space - which `DefWindowProc` cannot
  serve either, for the same reason. Tied to 1.7a as planned: a window that asked for
  `CaptionButtons.Close` cannot minimize or maximize, so those items are greyed for it and not only
  while maximized, and full screen greys Move and Size as well.

  The command is **posted, not sent**: `TrackPopupMenu` is still unwinding when it returns, and the
  commands it hands back resize or destroy the window.

- [x] **2.1k The window events the Win32 half never raised, 2026-08-25.** Fela, asking rather than
  hitting it: *"Does WindowContext.Win32 raise any of the events that are being raised by the UWP
  counterpart?"* It raised **one** - `VisibleBoundsChanged`, and only from the full-screen path
  added the same afternoon. The UWP half raises four.

  Nothing errored, which is what made it worth finding by audit rather than by use. What was
  silently not happening:

  - **`Activated`** - `App.OnWindowActivated` drives `NightModeService.UpdateTimer()` and publishes
    `UpdateWindowActivated`, so automatic night mode never re-evaluated on focus; `PasscodeWindow`
    hangs its lock on it; `WebAppWindow` posts `visibility_changed` to the bot, so a bot was told
    the window was visible and never told otherwise; `ChatView`, `StoriesWindow`, `AnimatedImage`
    and `SettingsPowerSavingViewModel` all subscribe.
  - **`VisibilityChanged`**, **`SizeChanged`** - fewer consumers, same shape of failure.
  - **`WindowContext.Active`**, the static the UWP half maintains from its own `Activated` handler,
    was never set - so `NotificationsService` always fell back to `Main` when choosing which window
    a toast belongs over.

  **What it is now.** `IIslandOwner`, one interface the `WindowContext` implements, in place of the
  single `CloseRequested` delegate: they are only correct together, because activation, visibility
  and size all move in the same handful of messages.

  - `WM_ACTIVATE` -> activation, **edge-triggered**: Windows sends it for every focus change in the
    process and the UWP event does not repeat itself.
  - Visibility is **recomputed, not read off a message**. No single message means "visible": a
    minimize arrives as `WM_SIZE`, a hide as `WM_SHOWWINDOW`, a restore as either. All three ask
    `IsWindowVisible && !IsIconic` and publish only changes.
  - `WM_SIZE` -> size, minimize excepted, since a 0x0 client rect is a visibility change and UWP
    reports it as one.
  - `WM_DPICHANGED` -> the suggested rect is applied and size is raised: the scale it is measured
    in changed even when the client area did not.
  - Closing clears `Active` if it was this window, because a window destroyed while active never
    sees a deactivation.

  **Units, and the second half of this item.** The size event carries **logical** pixels, and this
  host's `Bounds` was returning **physical** ones. My first answer was to compute the logical size
  separately and leave `Bounds` alone - Fela's correction, and the right rule: *the properties
  should mirror the UWP implementation*. A half that answers the same question in different units
  is not an implementation of the same contract, and the mismatch is invisible at 100%.

  So `Bounds`, `VisibleBounds` and `PointerPosition` are now logical, and `TryResizeView` takes
  logical and converts. `Bounds` is also the **client** rect rather than the window rect, because a
  CoreWindow has no non-client area to exclude and everything comparing against it is in XAML
  coordinates. The one that proves it: `Extensions.TransformToPointerPosition` subtracts `Bounds`
  from `PointerPosition` and then subtracts a XAML transform - three values that have to be in one
  space, and were not.

  Window creation had it too: `DefaultWidth`, and `ViewServiceOptions.Width`, are logical the way a
  UWP view's size is, and were going to `CreateWindowEx` as physical - a 384x640 payments window
  came out two thirds that size at 150%. `Create` now converts, and asks for that much *client*
  area, using the system DPI because there is no window yet to ask; if it opens on another monitor
  `WM_DPICHANGED` arrives at once with a rect that preserves the logical size.

  The lesson is the item, not the fix: **two of these were found by Fela asking what the halves
  share, not by anything failing.** The rest of the contract deserves the same pass - every member
  the two `WindowContext` halves are supposed to answer alike, checked against what each actually
  does.

- [ ] **2.1e Re-activation is unhandled, and it is the first real design gap.** Found 2026-08-24
  by the call window crashing. Chats, IV, the text editor and web apps all open in their own
  windows correctly; a call fail-fasts, and the app's own log says why:

      [150,694] VoipGroupCall InitializeSystemCallAsync  ResourcesNotAvailable
      [150,694] VoipGroupCall CreateWindow  Waiting for window creation
      [150,734] BootStrapper OnLaunched  Launch            <- a SECOND launch, same process
      [150,734] InternalLaunch  Previous: NotRunning
      [150,734] BootStrapper.Win32 EnsureWindowContext  Creating the window context
      [150,775] NavigationService Navigate  Page: MainPage <- a whole second app UI
      [150,833] GroupCallWindow ..ctor

  **`OnLaunched` ran a second time, 55 seconds into the process.** `App` is a real `Application`
  the system can still call, and nothing on this host expects that: `Program.Main` reads
  `AppInstance.GetActivatedEventArgs()` exactly once and drives `Start` itself.

  Two faults, in order of severity:

  - **The activation does not arrive on the UI thread.** `EnsureWindowContext` logged "Creating
    the window context", so `WindowContext.Current` - `[ThreadStatic]` - was null, and it built an
    island on a thread with no `WindowsXamlManager` and no `DispatcherQueue`. That is a fail-fast,
    and frame 0 in `Telegram.Native.dll` fits. **This is exactly the shape item 0.10 warns about**,
    and the first time `Current` being thread-static has actually cost something.
  - **Nothing suppresses or routes the framework's activation.** UWP guaranteed one process per
    package and owned the delivery; here the callback simply arrives.

  **And the real cause, found 2026-08-24: the window was being built on the wrong thread.**
  Fela's observation is what cracked it - *"the same OpenAsync to open a secondary chat window
  doesn't trigger the same"*. Same path, same `DesktopWindowXamlSource`, different **caller
  thread**: a chat window is opened from a click, and a call window from a tgcalls or TDLib
  callback. UWP hid the difference, because `OpenAsyncInternal` enqueued everything onto the new
  view's dispatcher no matter who called; the Win32 half built the island inline, on whatever
  thread arrived. An island on a thread with no `WindowsXamlManager` and no `DispatcherQueue`
  explains the spurious launch, the wrong thread, and the fail-fast in `Telegram.Native` together.

  `ViewService.Win32` now runs both creation paths through `OnUIThread`, which dispatches to
  `WindowContext.Main.Dispatcher` when the caller has no thread access, and runs inline when it
  does. The chat-reuse path dispatches its `Activate` onto that window's own dispatcher too.

  Two guards remain, and they are guards rather than the cure:

  - `ResolveWindowContext` answers with `Main` rather than `Current`, so nothing builds a window
    off-thread even if something else calls in.
  - `ShouldHandleLaunch` drops framework-delivered launches on this host. Needed regardless:
    **`WindowsXamlManager.InitializeForCurrentThread()` raises `OnLaunched` by itself**, which
    `Program.Main` calls on the line before it drives its own - so every startup saw two.

  **How Terminal does it, checked 2026-08-24 before deciding.** `TerminalApp::App::OnLaunched` is
  **empty**, with the comment *"We used to support a pure UWP version of the Terminal. This method
  was only ever used to do UWP-specific setup"*. The framework raises it and Terminal ignores it;
  startup lives in `WindowEmperor`/`AppHost`. So the principle is: **do not put startup in
  `OnLaunched` on this host.** Ours *is* in `OnLaunched` - it is BootStrapper's - so the equivalent
  is to call it from `Program.Main` and suppress the framework's, which is what
  `ShouldHandleLaunch` does.

  Two divergences from Terminal worth knowing:

  - **They initialise XAML inside the `App` constructor** (`App::Initialize`). Doing that here
    would raise `OnLaunched` *during* construction, which is worse than our order, where `App`
    already exists by the time it fires.
  - **They never pre-create a `DispatcherQueue`, and fail fast if one exists** - a pre-existing
    queue is how they detect running under the UWP app model, and
    `WindowsXamlManager::InitializeForCurrentThread` makes one itself. `Program.Main` here creates
    a `DispatcherQueueController` first, which gate 1.2a concluded was required. Both work; the
    contract differs, and it may bear on when the launch fires.

  **Resolved by share target, 2026-08-24 - Fela's example, and it decides it.** Ignoring
  framework activations is not an option: when the app is *not* running `Program.Main` reads the
  args from `AppInstance` and everything works, but when it *is* running - which is most of the
  time - the activation only ever arrives through the framework callback. Suppressing that would
  make share, `tg:` links and toast taps silently do nothing whenever Telegram is open. So:
  **marshal onto the UI thread's dispatcher**, do not suppress.

  **And share target exposes a second problem, which is worse than the crash because it looks like
  it works.** On UWP the system creates a *separate view* for a share: `WindowContext.Current`
  there is a new context, `CreateRootElement` returns a `ShareWindow` into it, and the main window
  is never touched. `App.xaml.cs` depends on exactly that shape -
  `WindowContext.Current.Content is ShareWindow sharePage`. Nothing creates that view on Win32, so
  the current `Current ?? Main` fallback would hand the share to the **main window** and replace
  the user's chat list with the share UI.

  So `EnsureWindowContext` is asking the wrong question. Not "is the app already up?" but **"which
  window does this activation belong to?"**:

  - **`ShareTargetActivatedEventArgs`** -> an open share window if there is one, else a **new**
    island window. That is what UWP's separate view gave for free.
  - **Launch, protocol, toast, file** -> the main window, navigating in place.

  Reachable today: item 2.4 kept the `windows.shareTarget` extension in the Win32 manifest, so a
  share into this flavour will find it.

- [x] **2.5d The passkey consent prompt follows package identity, not the container - measured
  2026-08-28.** Passkeys work on this host now, in process, and Fela noticed they still raise the
  system permission modal that the UWP build raises and that other Win32 software does not.

  Two candidates, and the token settles the first one. Queried with `TokenIsAppContainer` on three
  live processes:

  | process | AppContainer |
  | --- | --- |
  | the Store build | yes |
  | the Modern debug build | yes |
  | `bin\win32\...\AppX\Telegram.exe` | **no** |

  So `runFullTrust` does what it says and the Win32 flavour is genuinely outside the container. The
  prompt is not that.

  The spike answers the rest, because it is the same code with no package at all: it links
  `WebAuthn.Win32.cs` verbatim - the file the app and the stub already share - and calls
  `MakeCredential` from an unpackaged process. **No prompt.**

  **So it is package identity.** Windows lets a browser assert an origin and trusts it to have
  validated the relying party; every other *identified* app gets the consent UI first. Nothing
  about the host changes it, which means:

  - packaged, however it is distributed: the prompt stays, on both flavours;
  - unpackaged: it goes away.

  That is now a real input to the distribution question rather than a detail - the one user-visible
  behaviour found so far that packaging costs. Worth weighing against 0.18 and the sideloading
  problems, not decisive on its own.

  (The spike's csproj now compiles a file out of this repo. Deliberate - a copy would have answered
  a different question - but it means moving `WebAuthn.Win32.cs` breaks the spike build.)

- [x] **2.1l The CoreWindow has to be adopted, or closing the main window kills XAML - fixed
  2026-08-28.** Fela spotted the comment in Terminal's `WindowEmperor` and it is the most valuable
  thing in that file:

  > The first CoreWindow is created implicitly by XAML and parented to the first XAML island. We
  > parent it to our initial window for 2 reasons: on Windows 10 the CoreWindow will show up as a
  > visible window on the taskbar due to a WinUI bug, and this will hide it, because our initial
  > window is hidden. When we DestroyWindow() the island it will destroy the CoreWindow, and it's
  > not possible to recreate it. That's also a WinUI bug.

  The second one is not a corner case for us. Our first island **is** the main window, and the app
  is built around outliving it: a chat window open when the main one closes, and - once 2.5c lands -
  closing to tray and opening a window again later. Either would have destroyed the thread's
  CoreWindow with no way back, and the failure would have looked like anything but this.

  `CoreWindowBridge` already found the stub by class name for the WM_SIZE forwarding (#3577), so
  the fix reuses it: a hidden `WS_EX_TOOLWINDOW` window that is never shown and never destroyed,
  and one `SetParent` after the first island is created. No COM needed - Terminal goes through
  `ICoreWindowInterop` because it has no parent HWND to search under; we do.

  Terminal also strips `SWP_SHOWWINDOW` in that window's WndProc. Deliberately not copied: the
  window they use has other duties and something does try to show it, while ours exists for nothing
  else. If a phantom window ever appears on Windows 10, that is the first thing to add.

  Same file's doc comment corrected while there: it still carried the theory that gate 1.13's
  input failure came from this CoreWindow. It did not - see 1.13 and `ContentPopup.Win32.cs`.

- [x] **2.1m Connected animations are fatal on the island host - found and fixed 2026-08-28.**
  Crash dump `Telegram.exe.79384`, opening the gallery. Not our code: nothing of ours is on the
  stack, it dies inside the render tick.

  ```
  DCompTreeHost + 0x258 = NULL              <- only populated for a CoreWindow-hosted tree
    ThemeShadowScene::SetupLights           <- reads [null + 0x18]
    ThemeShadowScene::EnsureInitialized
    ProjectedShadowManager::EnsureScene
    ProjectedShadowManager::UpdateCasterStatus
    CConnectedAnimation::StartSpriteAnimations
    CConnectedAnimationService::PreCommit -> CCoreServices::NWDrawTree -> OnTick
  ```

  A connected animation over an element that casts a `ThemeShadow` makes the projected shadow
  manager build its scene, and the scene wants a visual an island's `DCompTreeHost` does not have.
  Two features that only work together under a `CoreWindow`.

  **Fixed by turning connected animations off on this host**, not the shadows: every caller already
  handles a null animation, so the gallery open/close and the profile header morph simply do not
  play, while dropping `ThemeShadow` would flatten flyouts, autocomplete, the record bar and every
  bubble. `ConnectedAnimationServiceEx.Win32.cs` sets the gate through the same `static partial
  void` seam as the `ContentPopup` fix, so UWP compiles the call away.

  If shadows later prove to crash in an island on their own - this only rules out the connected
  animation path - the gate moves to `ApiInfo.CanCreateThemeShadow` instead.

- [ ] **2.1h Take what Terminal already paid for - caption buttons and the message loop.** Fela,
  2026-08-27. Terminal has shipped this host shape for years and its code is full of workarounds
  with issue numbers attached; read against ours, these are the differences worth copying. Sources:
  `src/cascadia/TerminalApp/MinMaxCloseControl.xaml` and `.cpp`, `src/cascadia/WindowsTerminal/
  WindowEmperor.cpp`.

  **The caption buttons.** Keep our 40px height and skip the tooltips - the system ones are fine,
  and Terminal only hand-rolls tooltips (a throttled func on `SPI_GETMOUSEHOVERTIME`) because their
  buttons never see a real pointer either. What we are missing:

  - **A `HighContrast` theme dictionary.** Terminal maps every brush to `SystemColorButtonFace/
    ButtonText/Highlight/HighlightText` and swaps the glyphs for the contrast set (`EF2D` `EF2E`
    `EF2F` `EF2C` instead of `E921` `E922` `E923` `E8BB`). We have no high-contrast path at all and
    a hard-coded `#C42B1C`.
  - **An `Unfocused` state.** System caption buttons dim when the window is inactive; ours do not
    change. Terminal keeps a `Focused` flag and a `_normalState()` that returns `Unfocused` instead
    of `Normal`, and every release path goes through it - so a button that was hot when the window
    lost focus lands in the right state.
  - **`VisualTransition` out of `PointerOver`.** Ours snap; the system fades. Terminal animates the
    background over 0.15s and the glyph over 0.1s, with explicit transitions for both
    `PointerOver -> Normal` and `PointerOver -> Unfocused`.
  - **The close button's resting colour is `#00C42B1C`, not `Transparent`.** Transparent black and
    transparent red interpolate differently, so a fade from `Transparent` washes through grey. This
    is the kind of detail that makes it look wrong without anyone being able to say why.
  - **`AutomationProperties.AccessibilityView="Raw"`** on the buttons, which we do not set.

  Their glyphs are a `FontIcon` in a 10x10 `Viewbox`; ours are vector strokes, which is fine and
  probably crisper - but the font route is what makes the high-contrast swap a one-line resource
  change, so consider it when doing the point above.

  **The message loop.** `WindowEmperor` filters messages before XAML sees them and handles several
  the framework does not. Ours handles none of these:

  - **`WM_SETTINGCHANGE` / `ImmersiveColorSet`** for OS theme changes, with two traps attached:
    only act if the theme *actually* flipped, because it fires on lock and on UAC too (GH#15732);
    and do the work on the next tick, because it arrives via `SendMessage` and calling into
    anything `[input_sync]` from there fails with `RPC_E_CANTCALLOUT_ININPUTSYNCCALL` (GH#19505).
  - **`WM_QUERYENDSESSION` / `WM_ENDSESSION`** - `RegisterApplicationRestart`, persist, then
    `PostQuitMessage`. We do nothing today, so a reboot or sign-out loses whatever the app had.
  - **`WM_TASKBARCREATED`** (a registered message, so it cannot be a `case` label) to re-add the
    notification icon when explorer restarts. That belongs with 2.5c rather than after it.
  - **Keys XAML mishandles, caught in the loop before `PreTranslateMessage`:** F7, or system XAML
    shows the caret browsing dialog (GH#638); `VK_MENU` key-up, which system XAML never delivers
    (GH#6421); and Alt+Space, where system XAML shows its own system menu that cannot be suppressed
    (GH#7125) - which matters to us now that we draw our own.
  - **`WM_WINDOWPOSCHANGING` clearing `SWP_SHOWWINDOW`** on their own hidden window, with the
    comment that it "hides the buggy CoreWindow that XAML creates". Worth checking whether the
    CoreWindow system XAML makes for our thread can surface in alt-tab.
  - **`SetCurrentDirectory` to system32 at startup**, so the app does not hold a lock on the
    directory it was launched from. Free, and it only matters unpackaged - which is us.

  Already done, and it is reassuring to find it independently: `TerminateProcess` on the way out
  rather than a clean return, for the same Windows 10 XAML crash we hit (GH#15410, 0.18).

- [ ] **2.1n Two input gaps found while testing the `ContentPopup` fix.** Fela, 2026-08-27, both on
  Win32 and both about input the UWP host gets from the framework for free.

  - **The mouse back button does nothing in a `ContentPopup`.** On UWP that button arrives as a
    system back request; the island host has no such plumbing, so it has to come from
    `WM_APPCOMMAND` (`APPCOMMAND_BROWSER_BACKWARD`) or `WM_XBUTTONUP` in `IslandWindow`, and be
    routed to whatever the app treats as back - which for an open dialog means dismissing it, not
    navigating the frame behind it.
  - **Tab does not wrap in the main window root.** Reaching the last focusable stops there instead
    of returning to the first, and Shift+Tab likewise at the top. That is the island's focus scope
    having no cycle: `DesktopWindowXamlSource` raises `TakeFocusRequested` when navigation runs off
    the end, and the host is expected to hand focus back in - so today it falls out and nothing
    catches it. Note that inside a dialog Tab *does* cycle, because `ContentDialog`'s
    `DialogShowing` state sets `BackgroundElement.TabFocusNavigation="Cycle"` - which is the
    behaviour the root is missing.

- [ ] **2.1** Grow `Telegram.Stub` into the host process rather than creating a new one — it is
  already .NET 10, already Win32, already owns the tray and passkeys.
- [ ] **2.2** Desktop `IViewService` / `WindowContext` implementations behind the Phase 0 seams.

  - [x] **`WindowContext` split into shared + `WindowContext.Uwp.cs`, 2026-08-24.** 1376 lines
    became 705 shared and 753 UWP. Pure code motion apart from three extracted seams; builds
    clean. `Telegram.csproj` needed the new file added by hand — it lists every file.

    **What the file name means, because it is easy to read wrong:** `.Uwp.cs` is *this host's
    implementation*, not "UWP-only". `IsActive`, `IsForeground`, `SetPointerCursor`, `Bounds`,
    `Title`, `Compositor`, `PointerPosition`, `TryResizeView`, the full-screen calls, `SetTitleBar`
    and the four static key-state methods all keep their signatures and all get a Win32 twin. No
    call site changes, and no member is marked `partial`: only one host file is ever in a build,
    so each simply defines the member itself. That is the contract — shared code reading
    `IsActive` will not compile unless the host half supplies it.

    **`partial` is only for the other direction** — the two places shared code has to call *into*
    the host. Declared in `WindowContext.cs`, implemented in the host half:

        partial void SetHostContent(UIElement content);      // SetContent's _window.Content =
        partial void SetScreenCaptureEnabled(bool enabled);  // the two ApplicationView lines

    plus `private void Detach()`, extracted the other way: the host-agnostic half of what the UWP
    `Closed` handler did inline — drop the XamlRoot mapping, suspend and clear the navigation
    services, null the content. Each host calls it from its own close path.

    **`WindowContext.Current` is the one member with no Win32 twin** — Fela, 2026-08-24. It went
    to the UWP half for that reason rather than because it names a UWP type: a thread-static
    "the window on this thread" answers something only while a thread hosts exactly one window,
    which gate 1.8a already showed islands do not guarantee. This is item 0.10, and it is now a
    hard requirement rather than a preference. `OnShutdownCompleted` went with it, being the
    per-thread teardown that only means per-window under the same assumption; the shutdown
    *drain* above it stays shared, since `DispatcherQueue` exists in both hosts.
- [x] **2.3 The native components need no rebuilding — closed 2026-08-24 without spiking it.**
  Fela has already run `Telegram.Native` in a WinUI 3 app. `WINAPI_FAMILY_APP` restricts which
  Win32 APIs the C++ may *call*, and every one of those exists on desktop too, so the subset
  never was the risk; activation was, and package identity answers it. 2.6 stays open as
  tidiness rather than as work anything waits on.
- [ ] **2.4** Manifest for the **Win32 flavour only**. `runFullTrust` **stays** — a packaged
  Win32 app is declared with `EntryPoint="Windows.FullTrustApplication"` and cannot run without
  it. What goes is the UWP app model around it: `windows.fullTrustProcess` (the extension a UWP
  app uses to launch a full-trust companion — that companion becomes the host), `confirmAppClose`,
  `oneProcessVoIP`, `packageManagement`, `picturesLibrary`, `removableStorage`. Keep the
  extensions. The UWP manifest is untouched.
- [ ] **2.5** Retire `Telegram.Stub`'s IPC, app service and loopback exemption **on the Win32
  flavour**. They stay for as long as the UWP flavour ships.
- [ ] **2.5b `CameraCaptureUI` is not supported on this host.** Fela, 2026-08-24. It is a UWP app
  model API and there is no desktop equivalent in the box.

  There is a port to steal from if it turns out to matter:
  `microsoft/WindowsAppSDK` `dev/Interop/CameraCaptureUI/CameraCaptureUI/CameraCaptureUI.cpp` is
  WinAppSDK's own reimplementation for desktop apps. Fela's own read is that it is unclear anyone
  uses the feature - so the first question is whether to reimplement it or drop the entry point on
  this flavour, and that is worth answering before writing anything.

- [ ] **2.5a Toast actions fork — `Toast.RegisterBackgroundTasks` throws on Win32.** Found
  2026-08-24. `Toast.Register` builds an **in-process** background task (a `BackgroundTaskBuilder`
  with no entry point, triggered by `ToastNotificationActionTrigger`), and hosting one is a UWP
  app model feature: a full trust packaged application has no in-process background task host, the
  same reason `windows.appService` had to go from the manifest in 2.4.

  It does not block startup - `RegisterBackgroundTasks` is wrapped in `catch { }` and `Register`
  has its own - so the only symptom is silent: **tapping a notification action does not reach the
  app**. Worth knowing before someone spends an afternoon on why replies from a toast do nothing.

  The desktop replacement is the COM activator: a `windows.toastNotificationActivation` extension
  naming a CLSID, and the activation arriving as `ToastNotificationActivatedEventArgs` - which
  `Toast.GetSession` already handles, and which `Program.Main` already feeds in from
  `AppInstance.GetActivatedEventArgs()`. So the parsing side is done; what forks is only the
  registration.
- [ ] **2.7 Close TDLib on the way out - on both hosts.** Fela, 2026-08-24, on finding that the
  app has never done it. Not an islands problem, and the fix is not islands-specific either; it
  is here because this is where it surfaced and where the exit path now exists.

  **What is true today.** Nothing sends `close` to TDLib, on either flavour. The one place UWP
  could have is commented out - `App.OnSuspendingAsync`:

      //await Task.WhenAll(LifetimeService.Current.ResolveAll<IClientService>().Select(x => x.CloseAsync()));

  as is `Session.cs`'s `//ClientService.Close(true);`. The only live callers of
  `ClientService.Close(bool)` terminate *other* sessions from the sessions settings page. So the
  process simply dies with the database open, and TDLib recovers from its binlog on next start.
  That is what the binlog is for and no data is lost by it, but the sqlite database is never
  closed cleanly and every start pays a replay it did not have to.

  2.1i's `TerminateProcess` did not introduce this - it inherited it - but it does make the Win32
  flavour the first host with a real "we are exiting" moment to hang a proper close on.

  **The two hosts need different answers, which is the interesting part.**

  - **Win32** is the easy one: after the message loop, `Close()` every `LifetimeService.Current.Items`
    session, wait for each to reach `AuthorizationStateClosed`, bounded at a couple of seconds,
    then terminate regardless. It needs **no message pumping** - unlike 2.1i's drain, TDLib's
    updates arrive on `Client.Run`'s own thread, so a plain `WaitHandle` will do. What is missing
    is something on `ClientService` that signals the close completed; today nothing observes it.
  - **UWP has no exit.** Suspend is the only hook, and it is followed by a resume as often as by
    death, so closing there means reopening on `Resuming` - and paying that reopen every time the
    app comes back. The deferral budget is about five seconds, which is enough for a `close`, but
    whether the trade is worth making is a product call rather than a technical one.

  Open question worth settling first: how much does a clean close actually save at startup? If a
  binlog replay of a normal session costs single-digit milliseconds, the UWP half is not worth its
  risk and only Win32 should do it.
- [ ] **2.5c The tray icon, on both hosts, without a second copy of it.** Design agreed with Fela
  2026-08-27; not written yet.

  **What the tray actually is today.** `Telegram.Stub` exists because a packaged UWP app cannot own
  a tray icon in its own process: it is launched through `windows.fullTrustProcess` and talks over
  `AppServiceConnection`. Measured, the tray half of that channel is four verbs out and two in:

  | Direction | Message | Where |
  |---|---|---|
  | app -> tray | launch | `App.xaml.cs:268`, gated on `AppSettings.IsTrayVisible` |
  | app -> tray | `UnreadCount` + `UnreadUnmutedCount` | `NotificationsService.cs:370`, from two update handlers |
  | app -> tray | `OpenText`, `ExitText` | the Stub has no resources, so the app ships it the strings |
  | app -> tray | `Exit` | `CloudUpdateService.cs:111`, before an update installs |
  | tray -> app | open | Stub finds the window by `ProcessId` and activates it |
  | tray -> app | `CloseRequested` | the Exit menu item |

  Everything else on that connection - passkeys, the loopback exemption - is not the tray and is
  item 2.5's business.

  **The shape.** The repo's fork idiom, not an interface: `Services/TrayService.cs` shared, with
  `TrayService.Uwp.cs` and `TrayService.Win32.cs` beside it, only one ever in a build.

  - **Shared** owns everything that is a decision: whether there is a tray at all
    (`AppSettings.IsTrayVisible`), the menu labels from `Strings`, which of the three icons an
    unread count maps to, and what Open and Exit *mean* - activate `WindowContext.Active ??
    WindowContext.Main`, and close the app. Call sites become `TrayService.Start()`,
    `TrayService.SetUnreadCount(...)`, `TrayService.Stop()`; three files change.
  - **`.Uwp.cs`** forwards those to the existing bridge. Behaviour is unchanged by construction,
    because it is the same three calls it makes today.
  - **`.Win32.cs`** owns `Shell_NotifyIcon` in process. It is not new code: `Telegram.Stub/NotifyIcon.cs`
    is already exactly this, 456 lines of it, and moves across minus the IPC.

  What that leaves genuinely shared is the part that would otherwise drift - the labels, the icon
  choice, and the two commands. What stays forked is only the plumbing that has to differ.

  **The icons are the one asset question.** The Stub loads `Default.ico`, `Muted.ico`,
  `Unmuted.ico` from its own native resources by ordinal, through `LoadImage` on its module. A
  .NET exe has no such resource table without a `.res`, so the Win32 half should load the same
  three files from `AppContext.BaseDirectory` with `LR_LOADFROMFILE`. Same files in the repo, two
  ways of embedding them - asset sharing rather than a second copy.

  **The real design change is lifetime, and it is not code motion.** Today the tray survives the
  app's windows because it is a different process. On this host it is not:
  `IslandWindow`'s `WM_DESTROY` posts a quit when `Windows.Count` reaches zero, and `Program.Main`
  then calls `TerminateProcess` (2.1i). With a tray icon that has to become conditional - a window
  count of zero is not the end of the app when the tray is showing - and closing the last window
  has to leave a live message loop with no windows in it.

  Two consequences to settle before writing it:

  - **Reopening from the tray means creating a window when `WindowContext.Main` is null.**
    `BootStrapper.Win32.ResolveWindowContext` and `ViewService.Win32.OnUIThread` both read `Main`,
    and both need an answer for "the app is running and has no windows". The XAML side is fine -
    the thread and `WindowsXamlManager` outlive the windows - but `Main` has to become
    re-creatable rather than first-one-wins.
  - **What quits the app then?** Only the tray's Exit, `CloudUpdateService`, and the system. That
    makes `TrayService.Stop()` the single exit path, and it should be the one that posts the quit
    rather than `WM_DESTROY` guessing.

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
