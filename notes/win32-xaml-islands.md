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

What the spike has **not** answered: only **1.7**, the custom non-client caption. It is not a
gate on anything, and calling it one earlier was wrong. Terminal's caption buttons are ordinary
XAML — its own comment says they "work reasonably well with just XAML" — so the caption is
`WM_NCCALCSIZE` work against a published reference.

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

- [ ] **0.18 Bubble theming forces one chat window per thread.** The conclusion after going round
  this twice; recorded so it is not relitigated.

  How it works today: `[ThreadStatic]` tables hold `(Color, SolidColorBrush)`; every
  `ThemeOutgoing`/`ThemeIncoming` copies **the same brush references** into its ThemeDictionaries;
  a per-chat theme mutates ~20 brush colours in place and every bubble in the window repaints. No
  walking, no re-resolution. It is very cheap and the cheapness is the point.

  **`App.xaml` resources are instantiated per thread in UWP** — Fela's correction, and the proof is
  in `Theme`'s own constructor: `_isPrimary = Current == null` with `[ThreadStatic] Current`, gating
  the initial `Update(Light)`/`Update(Dark)`. That only makes sense if `Theme()` runs once per
  thread. So thread == view is what makes the whole scheme correct today.

  Why it cannot be made per-window:
  - The app-level `<common:ThemeIncoming/>` is constructed at **thread initialisation, before any
    window exists**. There is no XamlRoot to resolve against, at any hook.
  - `Theme.Current` is not just brushes. Its own comment: *"Current is the only handle the app has
    on the theme of this view, and it is dereferenced unguarded all over the message tree."*
  - Making brushes per dictionary instance instead would allocate ~20 `SolidColorBrush` per bubble
    realisation and force `Update` to walk every live dictionary — a regression on the app's
    hottest surface.

  **Superseded 2026-08-23 - the rule was too strong.** Fela's correction: the app theme is global
  and every window shows the same one; the only thing that varies per window is the **chat
  override**, because different windows can show different chats. And that override touches
  exactly two things - `Outgoing.Update` and `Incoming.Update` on the message brushes, plus the
  background. It never runs the app colour pass.

  The message brushes live in bubbles, bubbles are content, and gate 1.11 measured that content
  scopes are per island. So per-window chat themes work in both hosts, and what is left of the
  problem is only the popups that also render bubble keys - `SendFilesPopup` and friends - which
  today inherit the override by accident. `ContentPopup.ShowQueuedAsync` already takes the
  `XamlRoot` and is the one place to forward the window's incoming set deliberately.

  What remains true is narrower: the **app** theme is per thread, because `Application.Resources`
  is what popups resolve from. That is fine, since it is meant to be global anyway.

  The original, too-strong rule follows.

  **The rule: a window that renders chat content owns its thread.** Calls, web apps, passcode,
  gallery and stories render no bubbles and can share one freely. This caps the 1.8a benefit rather
  than removing it, and costs nothing today, since the app is already one window per thread.

  If that rule is ever relaxed, the per-bubble dictionaries have a correct hook — `Loading`, per
  0.17 — and moving them to one shared per-window instance merged there would be **cheaper than
  today**, since each bubble currently constructs its own `ResourceDictionary`. It just does not
  rescue the app-level case.

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
| `InputListener` | 6 | `CoreWindow.GetAsyncKeyState` | `GetAsyncKeyState`, or XAML routed events |
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
`WindowContext.Win32.cs` supply activation, bounds, title bar, cursor and key state. No
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



- [ ] **2.0** Retire the .NET Native project **when .NET 10 has earned it**, not before, and not
  as a precondition for anything here. It is what ships to users; "three flavours is one too
  many" is a tidiness preference and cannot outrank not breaking the app. Three configurations
  is the accepted cost of the transition.

  This corrects an earlier reading of this item that treated it as a gate on Phase 2. It is not:
  the islands host ships to nobody and carries no user risk, so it needs time rather than
  permission.
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
