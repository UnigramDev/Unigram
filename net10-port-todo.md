# Porting the app to .NET 10, keeping .NET Native alive

Written 2026-08-14. Plan and resume point for building the app a second way — UWP XAML on
.NET 10 with CsWinRT and NativeAOT — while `Telegram.csproj` keeps shipping on .NET Native.

The online documentation for this is stale. Everything below that is stated as fact was read
out of the installed toolchain or out of this repository, and the source is named so it can be
re-checked when the toolchain moves.

## The shape

A second project file, `Telegram/Telegram.Modern.csproj`, beside the existing one, in the same
directory so every relative path, `Assets\`, XAML and `Strings\` reference resolves unchanged.

There is no multi-targeting option. `UseDotNetNativeToolchain` exists only in the legacy UWP
project system and `UseUwp` only in SDK-style, and neither project format can produce the other's
output — the same wall `Telegram.Benchmarks.NetNative` hit, and for the same reason, recorded in
its header comment.

Ground truth for the new file is the template Visual Studio 18 installs at
`Common7\IDE\ProjectTemplates\CSharp\Windows UAP\1033\Windows_UAP_NET_WAP_BlankXamlApplication\`,
which is the packaged-by-a-wapproj variant — the layout this repository already has:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.18362.0</TargetPlatformMinVersion>
    <UseUwp>true</UseUwp>
    <Platforms>x64;arm64</Platforms>
    <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
    <PublishAot>true</PublishAot>
    <PublishProfile>win-$(Platform).pubxml</PublishProfile>
    <DisableRuntimeMarshalling>true</DisableRuntimeMarshalling>
  </PropertyGroup>
</Project>
```

`EnableMsixTooling` is the single-project-MSIX variant and is deliberately absent here: packaging
stays in `Telegram.Msix`, which also carries `Telegram.Stub`.

What the toolchain does with those properties, from
`dotnet\sdk\10.0.302\Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.Windows.targets`:

- `UseUwp` adds the `Microsoft.Windows.SDK.NET.Ref.Xaml` framework reference — the
  `Windows.UI.Xaml.*` projections — and turns on `CsWinRTUseWindowsUIXamlProjections`.
- `UseUwpTools` follows `UseUwp` by default and brings in the XAML compiler and MSIX tooling.
- **The Windows SDK revision digit picks the CsWinRT major version.** A `.0` revision
  (`10.0.26100.0`) selects CsWinRT 2.x; a `.1` revision selects the 3.0 preview. Stay on `.0`:
  CsWinRT 3.0 could not be made to build a UWP XAML app, which is written up under
  "CsWinRT 3.0 preview does not build a UWP XAML app yet" in `Telegram.Benchmarks/README.md`.

Item globbing comes from
`MSBuild\Microsoft\WindowsXaml\v18.0\8.21\Microsoft.Windows.UI.Xaml.CSharp.ModernNET.DefaultItems.Props`:
`App.xaml` becomes `ApplicationDefinition`, every other `**/*.xaml` becomes a `Page`, and `.cs`
files come from the ordinary SDK glob. That matters because `Telegram.csproj` lists **1250
`Compile` and 473 `Page` entries by hand**; a second hand-written copy would be stale within a
week. The new project globs, and only needs to subtract.

Measured against disk, the legacy project omits exactly eight files, seven of them not built at
all and one built differently:

| file | why |
|---|---|
| `Common\HeapSizeCalculator.cs` | not built |
| `Common\HttpServer.cs` | not built |
| `Common\MediaHttpServer.cs` | not built |
| `Controls\SettingsCheckBox.cs` | not built |
| `Entities\SourceGenerationContext.cs` | not built |
| `Streams\DiceFileSource.cs` | not built |
| `Td\Api\ChatProjection.cs` | not built |
| `Common\CommonStyles.xaml` | `Content`, not `Page` — loaded at runtime through `ms-appx:///` from `App.xaml` |

Nothing listed in the project is missing from disk.

## Why this is a smaller job than it looks

| what | state |
|---|---|
| Reflection in app code | none — every `GetProperty`/`Activator` hit in the tree is inside `bin\*\ilc\PInvoke.g.cs`, which is .NET Native's own output |
| `System.Text.Json` | every call site goes through a `JsonSerializerContext`; no reflection-based serializer anywhere |
| CsWinRT preparation | `Telegram/CsWinRT.cs` already carries ~40 `[assembly: GeneratedWinRTExposedExternalType]` entries under `#if NET9_0_OR_GREATER`, and `Common/Interop.cs` already has CsWinRT-era `[CustomMarshaller]` code |
| `Default.rd.xml` | its one directive, `MessageSelector.UpdateSelection`, is obsolete — the method is only ever called from C# |
| WinUI 2 | `Microsoft.UI.Xaml` 2.8.7 ships a `net8.0-windows10.0.22621.0` asset |
| Win2D | `Win2D.uwp` 1.28.3 ships a `net8.0-windows10.0.19041.0` asset |
| Publish profiles | `Telegram/Properties/PublishProfiles/win-*.pubxml` already exist and match the template |
| Modern toolchain in-repo | `Telegram.Stub` already builds `net10.0-windows10.0.18362.0` with `PublishAot` |

The usual hard part of an AOT port — hunting reflection out of a large app — is already done here.

## Phase 0 — drop x86 (done, unbuilt)

Unrelated to the port but a prerequisite for not carrying dead configurations into a new project
file. x64 and ARM64 are the only supported architectures.

- [x] `Telegram.slnx`: the `x86` platform and its two project mappings
- [x] `Telegram/Telegram.csproj`: default platform to x64, the `Debug|x86` and `Release|x86`
      property groups, and the dead `TelegramTdPlatform`/`Win32` conditions (`$(Platform)` in a
      C# project is never `Win32`, so those branches never evaluated)
- [x] `Telegram.Msix/Telegram.Msix.wapproj`: the two x86 project configurations and their
      property groups
- [x] `Telegram.Native.vcxproj`, `Telegram.Native.Calls.vcxproj`: the `Win32` configurations
- [x] `Directory.Build.props`: the `x86-uwp` vcpkg triplet
- [x] `Build.ps1`: `$arch` default
- [x] `Telegram/Properties/PublishProfiles/win-x86.pubxml`
- [x] `Libraries/rlottie/x86/RLottie.winmd` — the only x86 file left there, with no `.dll` or
      `.pri` beside it

Left alone deliberately: `Libraries/tdjson/build.ps1` still knows how to build x86 tdlib. That is
a capability of a dependency build script, not a stale reference. Also noted while passing:
`Build.ps1` still invokes `Telegram.sln`, which no longer exists.

Turned up while doing it, and relevant to Phase 6: **`.gitignore` ignores `*.pubxml`**, so the
publish profiles under `Telegram/Properties/PublishProfiles` exist only on this machine. The
modern wapproj references one per platform by path, so they have to be tracked before that build
works anywhere else.

## Phase 1 — spikes (done, all three pass)

Each of these could have invalidated the plan, so each got its own throwaway project rather than
being discovered halfway through a 1250-file compile. They live in the scratchpad, not the repo.

**Build them with Visual Studio's MSBuild, not the dotnet CLI.** Modern UWP XAML support is
imported from VS's own `ImportBefore`/`ImportAfter` hooks, which the SDK's MSBuild does not have:

```
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" ^
  Spike.csproj -restore -p:Configuration=Release -p:Platform=x64 ^
  -p:RuntimeIdentifier=win-x64 -p:SelfContained=true -p:PublishAot=true
```

The AOT link step also needs `vswhere.exe` on PATH, the same trap the benchmark README records.

- [x] **Baseline.** A minimal UWP XAML app (`App.xaml` + a page) on `net10.0-windows10.0.26100.0`
      with `UseUwp` builds, with nothing beyond the template properties.
- [x] **WebView2 — supported outright, no workarounds.** The package itself branches on
      `UseUwpTools`: `build\Common.targets` sets `WebView2EnableCsWinRTProjection`, references the
      prebuilt `lib_manual\net8.0-windows10.0.17763.0\Microsoft.Web.WebView2.Core.Projection.dll`,
      copies `runtimes\win-x64\native_uap\Microsoft.Web.WebView2.Core.dll`, and feeds the winmd to
      `CsWinRTInputs`. A page using `muxc:WebView2` and a representative slice of the app's
      `CoreWebView2` surface — `SetVirtualHostNameToFolderMapping`, `AddWebResourceRequestedFilter`,
      `Profile.PreferredColorScheme`, `CallDevToolsProtocolMethodAsync`, the seven event handlers —
      compiles, and AOT-publishes with **zero warnings** to a 5.6 MB native exe.
- [x] **WinUI 2 — supported, and it came for free with the above.** `Microsoft.UI.Xaml` 2.8.7
      resolves a prebuilt `lib\net8.0-windows10.0.22621.0\Microsoft.UI.Xaml.Projection.dll`.
      `XamlControlsResources` merged in `App.xaml` and an `InfoBar` both compile and AOT.
- [x] **Local WinRT components — need a projection, and generating it in-project works.**
      A bare `<Reference>` to a winmd, which is what `Telegram.csproj` does today for RLottie, is
      rejected outright: `NETSDK1130: Referencing a Windows Metadata component directly when
      targeting .NET 5 or higher is not supported`. The replacement:

      ```xml
      <PropertyGroup>
        <CsWinRTIncludes>RLottie;Telegram.Native</CsWinRTIncludes>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="Microsoft.Windows.CsWinRT" Version="2.2.0" />
        <CsWinRTInputs Include="...\rlottie\x64\RLottie.winmd" />
        <CsWinRTInputs Include="...\x64\Release\Telegram.Native\Telegram.Native.winmd" />
        <CsWinRTInputs Include="...\win2d.uwp\1.28.3\lib\uap10.0\Microsoft.Graphics.Canvas.winmd" />
      </ItemGroup>
      ```

      Everything in `CsWinRTInputs` is passed to `cswinrt.exe` as `-input` and read for metadata;
      only the namespaces named in `CsWinRTIncludes` are emitted. That distinction is what the
      third line is for — both components have Win2D types in their signatures
      (`RLottie.ILottieAnimation` takes a `CanvasBitmap`), and without its winmd cswinrt fails
      with `Type 'Microsoft.Graphics.Canvas.CanvasBitmap' could not be found`; with it, Win2D
      still comes from its own package projection rather than being projected twice.
      This generated ~22k lines across RLottie and seven `Telegram.Native.*` namespaces, compiled,
      and AOT-published with **zero warnings** to a 4.4 MB native exe.
- [x] **Windows Desktop Extensions — no SDKReference needed.** `FullTrustProcessLauncher`,
      `StartupTask`, `StartupTaskState` and `ApiInformation` all compile with no reference beyond
      `UseUwp`. The `<SDKReference Include="WindowsDesktop" />` in `Telegram.csproj` has no
      equivalent in the new project because the modern projections already carry those types.

Spiked with the app's real API shapes but not with the app's real code, and nothing was *run* —
these are compile and AOT-link results, not runtime ones.

## Phase 2 — the project file (done)

`Telegram/Telegram.Modern.csproj`. Two traps cost a build each and are worth keeping:

**`Sdk.props` is imported by hand rather than through the `Sdk` attribute.** Three properties have
to be set before it, and setting them in the body is too late: `BaseIntermediateOutputPath` and
`BaseOutputPath`, so the two projects do not share `obj\` and `bin\`, and `DefaultItemExcludes`,
because the SDK's `**/*.cs` glob otherwise compiles **Telegram.csproj's own generated output** —
its `XamlTypeInfo.g.cs`, every page's `.g.cs`, and the .NET Native ilc sources under `bin\` — as
app source. That produced 417 errors that looked like toolchain incompatibility and were not.

**The `TdParsers` switch has to be mirrored.** The generator reads the property, the hand-written
code reads `TD_READER_PARSER`/`TD_POINTER_PARSER`, and if the two projects disagree the generated
parser calls helpers that were compiled out — 3055 errors, all in one generated file. It is a
by-hand duplication and the first thing to check whenever the generated code stops compiling.

- [x] `Telegram/Telegram.Modern.csproj` from the WAP template, plus the output paths,
      `AllowUnsafeBlocks` and `LangVersion`.
- [x] Mirror `DefineConstants`, now without `ENABLE_CALLS`. `NET9_0_OR_GREATER` arrives for free
      and is already the switch the source uses.
- [x] Subtract the seven unbuilt files; `<Page Remove>` `Common\CommonStyles.xaml` and add it back
      as `Content`. Compiling it as a `Page` instead would be a real change (XBF rather than
      runtime-parsed XAML) and should be a separate, deliberate one.
- [x] Same source generator wiring: `Telegram.Generators` as an analyzer, `td_api.tl` as an
      `AdditionalFiles`.
- [x] Packages. Drop `Microsoft.NETCore.UniversalWindowsPlatform`, `System.ValueTuple`,
      `System.Memory`, `Microsoft.Bcl.HashCode`, `PolySharp`, and `System.Reflection.Metadata`
      (nothing in the app references it). Keep the rest. `Rg.DiffUtils` is `netstandard1.0` and
      will at least warn.
- [ ] Delete `Properties/Default.rd.xml` from the new project's world; the legacy project can
      drop it too, since its only directive is obsolete.
- [x] Project `Telegram.Native.Calls` the same way as the other two. The spike only wired
      `Telegram.Native` and `RLottie`; the third component is the same shape, and its winmd may
      pull in further metadata the way Win2D did.
- [ ] Expect `CsWinRT1028` — *"implements WinRT interfaces but it or a parent type isn't marked
      partial"* — across the app. XAML classes are already `partial`; anything else that crosses
      the ABI is not. The spike hit it on a bare `Application` subclass, so the count in a codebase
      this size will not be small, and it is a trimming/AOT correctness warning rather than style.

## Phase 3 — compile (done)

**The app builds.** `Telegram.Modern.csproj` produces a 14.9 MB `Telegram.dll` from all 1250
sources and all 473 XAML pages, zero errors, 474 `.xbf` — with `PublishAot` off. The warnings
that remain are the app's own (`CS0162` unreachable, `CS0649` unassigned), not port damage.

Build it with:

```
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" ^
  Telegram\Telegram.Modern.csproj -restore -p:Configuration=Release -p:Platform=x64
```

One thing to watch, because it cost a detour: a run failed in the XAML pass with
`WMC9999: Specified argument was out of the range of valid values`, an internal error naming no
file, and it did not reproduce on the next pass over the same sources. It looks like incremental
state rather than a real defect — a full `obj\modern` delete cured a similar mismatch earlier —
but if it returns, that is the first thing to try.
- [x] Whatever the modern stack rejects that .NET Native accepted goes behind
      `#if NET9_0_OR_GREATER`, so the shipping build stays green the whole way.

### What the app source needed, and why

Five sites, all of which also still compile under .NET Native. Three were in `#if
NET9_0_OR_GREATER` blocks written for this port and never compiled until now.

| where | what | why |
|---|---|---|
| `Common\Locale.cs` | `GetUserDefaultLocaleName` hoisted out of four popups, and its `Span<char>` parameter became `char*` behind a `fixed` | `SYSLIB1051`: the P/Invoke source generator only marshals `Span<T>` when runtime marshalling is disabled, and `char` is not blittable while it is enabled. Runtime marshalling stays at its UWP default, so the buffer crosses as a pointer. `ref char` is not enough — `char` is the problem, not the indirection. |
| `StakeDicePopup` | added `using System;` | its `NET9_0_OR_GREATER` block used `Span<char>` with no `using System;` |
| `Common\Extensions.cs` | `using WinRT;`, guarded | `IBuffer.As<IBufferByteAccess>()` is `WinRT.CastExtensions`; the namespace does not exist under .NET Native, so the using has to be inside the `#if` |
| `Common\PlaceholderHelper.cs` | `new PlaceholderImageHelper((Window)null)` | **CsWinRT gives every projected runtime class an `IObjectReference` constructor**, so a bare `null` is ambiguous. Expect this wherever the app passes `null` to a projected constructor. |
| `Properties\AssemblyInfo.cs` | excluded from the modern project, its values carried over as `AssemblyTitle`/`Product`/`Copyright` | the SDK generates those attributes itself, and two sets is `CS0579`. Excluded rather than turning off `GenerateAssemblyInfo`, so the legacy project keeps the file it has always had |
| `Common\Extensions.cs` | hoisted `CancellationTokenRegistration registration = default;` out of its initializer | the local function captures `registration` and is converted to a delegate inside the very expression that assigns it. Definite assignment rejects that at a modern `LangVersion`; the legacy project's `LangVersion 14.0` does not. |

That is a small list for a codebase this size, and it is consistent with the reflection audit:
nothing here was a design problem, only interop and language-version detail.

## Identity

The modern build installs as **`38833FF26BA1D.UnigramNet10`, "Unigram .NET 10"**, beside the two
identities that already exist — `UnigramExperimental` (what F5 on `Telegram.csproj` deploys) and
`TelegramFZ-LLC.Windows` (the store package `Telegram.Msix` builds). A different package family
also means a separate `LocalState`, so the two builds do not share an account and the port cannot
corrupt a real profile.

It is not a second manifest. `Telegram.Modern.csproj` copies `Package.appxmanifest` into `obj\` at
build time and `XmlPoke`s three values — `Identity/@Name`, `Properties/DisplayName` and
`VisualElements/@DisplayName`. The manifest is 108 lines of capabilities, extensions and file
associations, and a copy would drift the first time one of them changes.

**The destination path must come from `BaseIntermediateOutputPath`, not `IntermediateOutputPath`.**
The latter is still empty where the property is evaluated, which collapses the destination onto
`Package.appxmanifest` itself — so the first run rewrote the real manifest in place, reformatting
it and giving `Telegram.csproj` the modern identity. There is now an `Error` in the target that
fires if the two paths ever resolve to the same file.

Two more things the packaging path needed:

- **`EnableMsixTooling`, not a bare `AppxPackage`.** Without it the PRI targets are half
  configured and fail on a missing `IntermediateExtension`. With it, `Strings\**\*.resw` are
  globbed as `PRIResource` automatically — listing them as well is `NETSDK1022`.
- **The C++/WinRT binaries have to be copied by hand.** They are consumed as projections rather
  than `ProjectReference`s, so nothing brings `Telegram.Native.dll` and friends along. They come
  out of the same `x64\Release\...` folders the winmds are read from, which also means the
  solution has to have built them first. `zlib1.dll` and `Microsoft.Graphics.Canvas.dll` are
  excluded there because they also arrive with tdjson and from the Win2D package: the legacy
  build copies both over each other, publish calls it `NETSDK1152`.
- **The downloaded tdjson binaries carry the mark of the web**, which the PRI step refuses
  (`MSB3821`). `Unblock-File` on `Libraries\tdjson\x64\tdjson.dll` and `.pdb` clears it.
- **`UnigramUsesVcpkg` matches on project name**, so `Telegram.Modern` was invisible to it and got
  neither the vcpkg runtime DLLs nor libvlc's plugin tree — video died natively, with no managed
  error report to show for it, because libvlc loads and then finds no plugins. The project is now
  in that list in `Directory.Build.props`, which also means the plugins keep their
  `plugins\<category>\<name>.dll` shape, as `plugins.dat` records those paths.
  Consequence: the vcpkg runtime DLLs then collide with the same sixteen ffmpeg/libvlc files if
  they are also copied out of `x64\Release\Telegram.Native\`. Only the components themselves come
  from there now; the shared set comes from vcpkg, exactly as for `Telegram.csproj`.
- **`Content` needs `CopyToOutputDirectory`.** The legacy UWP project system deployed `Content`
  implicitly; SDK-style does not, and nothing says so at build time. The app started, initialised
  TDLib, wrote its databases — and then died on
  `XamlParseException: Cannot locate resource from 'ms-appx:///Common/CommonStyles.xaml'`, because
  neither that file nor anything under `Assets\` had been laid down. `Assets\**` is auto-included
  by the MSIX tooling, so it takes `Content Update` rather than a second `Include`.

## Phase 4 — run it

How to get it onto the machine — a loose layout, registered, no signing:

```
msbuild Telegram\Telegram.Modern.csproj -t:Publish -restore -p:Configuration=Release ^
  -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:SelfContained=true -p:PublishAot=false
copy ...\win-x64\AppxManifest.xml ...\win-x64\publish\
Add-AppxPackage -Register ...\win-x64\publish\AppxManifest.xml
Start-Process "shell:appsFolder\38833FF26BA1D.UnigramNet10_g9c9v27vpyspw!App"
```

`AppxManifest.xml` is generated one directory above `publish\` and has to be copied in. The
package declares no `Microsoft.VCLibs.140.00` dependency, so the store CRT (`vcruntime140_app.dll`
and friends, out of the VCLibs extension SDK appx) is copied into the layout the same way
`Telegram.Benchmarks.NetNative\Stage.ps1` does it. Uninstall with
`Get-AppxPackage 38833FF26BA1D.UnigramNet10 | Remove-AppxPackage`.

When it crashes, the app's own reporter beats the event log: read the newest file under
`%LOCALAPPDATA%\Packages\38833FF26BA1D.UnigramNet10_*\LocalState\ErrorReports\*.json`, which
carries the exception type, message and a stack. The Application event log only gives
`0xc000027b` in `Windows.UI.Xaml.dll`, which says "a managed exception" and nothing else.

- [x] Launch under CoreCLR. It runs. Verified by Fela: stickers, animations, WebView2, VLC video
      and secondary windows all work.
- [ ] **Calls: `CompositionTarget.Rendering` is delivered to the wrong view.** This is a defect in
      the Windows SDK XAML projection, not in the app.

      Measured in a throwaway app: a handler subscribed on a secondary view's thread runs on the
      **main** view's thread (`Rendering handlers ran on: main 4, secondary 4`, where the secondary
      view is thread 5). The app agrees — its own log pairs
      `resources thread 23, drawing on 4, dispatcher access False` for the presenter in the call
      window, against `resources thread 4, drawing on 4` for the ones in the main window.

      Why: `Microsoft.Windows.UI.Xaml.dll` caches the statics object in a plain static field,
      `__objRef_global__Windows_UI_Xaml_Media_ICompositionTargetStatics`, and the whole assembly
      contains **no** `ThreadStaticAttribute` or `ThreadLocal` at all. The first view to touch
      `CompositionTarget` wins it for the process, so `add_Rendering` from any other view registers
      against the first view's core. `Window.Current` is unaffected because `get_Current` is itself
      thread-aware; it is registration that binds to the wrong thread. There is no CsWinRT switch
      for it — the configuration knobs in `WinRT.Runtime.dll` cover dynamic objects, `IReference`,
      `IDynamicInterfaceCastable`, custom type mappings and the XAML projection choice, nothing
      about statics caching.

      Consequences: everything frame-driven in a secondary window runs on the main thread. The
      call's blob waves survive it because `Windows.UI.Composition` objects are agile. Lottie and
      call video do not, because `WriteableBitmap` is thread-affine, so `Invalidate()` is
      `RPC_E_WRONG_THREAD` — which the XAML handler swallows, leaving a frozen window and no
      report.

      **Fixed** by registering through the ABI instead: `CompositionTargetRendering` in
      `Common/Interop.cs`, used by `AnimatedImageLoader`. Reported upstream as
      [CsWinRT #2524](https://github.com/microsoft/CsWinRT/issues/2524).

      What the measurements ruled out on the way, all of it in the throwaway app:

      - `RoGetActivationFactory` returns the **same** statics object on both views, and it answers
        `IAgileObject`, so there is no per-view statics to fetch and no marshalling on the call.
        The ABI would happily register per view; it is the projection that does not.
      - The tick counts gave it away in hindsight: both views' handlers reported *exactly* the same
        count on every run (256/256, 258/258, 262/262). Two view render loops would drift; one
        registration driving two delegates does not.
      - Registering with `add_Rendering` through the ABI on the secondary view's thread puts the
        callback on that view's thread — measured, on the same thread and in the same run where the
        projected subscription still landed on the main view's.

      Every call site is unchanged: `CsWinRT.cs` aliases `CompositionTarget` to the stand-in on
      .NET 9+ and to the real type otherwise, so `CompositionVSync` (the call blobs, DiceView),
      `VisualUtilities`, both Premium controls and `GiftCraftPopup` are fixed too, without a line
      of conditional compilation between them. Aliasing in both directions also settles the clash
      with `Windows.UI.Composition.CompositionTarget`, which is the only reason those sites spelled
      the namespace out — so the diff is shorter than what it replaced.

      `Rendered` registers the same way, through `ICompositionTargetStatics3`
      (`bc0a7cd9-6750-4708-994c-2028e0312ac8`).

      Options considered, for the record:

      1. **Marshal the frame work.** In `AnimatedImageLoader.OnRendering`, enqueue onto the
         presenter's dispatcher when `!HasThreadAccess`. One bool test on the main view, which is
         where the hundreds of animations are; a per-frame enqueue only for secondary views.
         Smallest change, and it leaves the pacing on a real vsync tick.
      2. **Ask for the statics per thread.** `RoGetActivationFactory` for
         `Windows.UI.Xaml.Media.CompositionTarget` with
         `IID_ICompositionTargetStatics = 2b1af03d-1ed2-4b59-bd00-7594ee92832b` (taken from the
         .NET Native interop this repository already generates), then add and remove the handler
         through that. Registers against the right core, and fits the hand-written COM interop
         already in `Common/Interop.cs`. Unproven — worth a spike before committing to it.
      3. Report it to microsoft/CsWinRT either way. Any per-view static WinRT **event** is affected,
         so this is bigger than animations.

      What the evidence rules out: the frame driver. The call's blob waves animate, and they run
      off `CompositionVSync` → `CompositionTarget.Rendering`, the same per-view static event
      `AnimatedImage` uses. So `Rendering` does fire on that thread.

      What is left is the surface path, which is what lottie and video share and the blobs do not:
      the blobs are pure Composition geometry, while both of the broken ones draw through a
      `CompositionDrawingSurface` off `Telegram.Native`'s `CompositionGraphicsDevice`.
      `PlaceholderHelper.Foreground` is `[ThreadStatic]` and constructs
      `new PlaceholderImageHelper(Window.Current)` per view, whose native constructor takes
      `window.Compositor()` — the first thing to check is whether that construction is what
      throws on the secondary view.
- [ ] `{Binding}` is where the runtime differences will show. 180 occurrences across 32 of 481
      XAML files, against 1927 `x:Bind` which are compiled and free. Each managed binding source
      type needs `[GeneratedBindableCustomProperty]` and to be `partial`. Bindings whose source is
      a XAML/WinRT type need nothing.

## Phase 5 — AOT

- [ ] Turn on `PublishAot`, resolve trim and AOT warnings until clean.
- [ ] `DisableRuntimeMarshalling` is in the template but leave it **off** at first. There are 43
      `DllImport`s, mostly `kernel32` with `CharSet.Unicode`, plus tdjson's cdecl entry points; it
      changes their marshalling. Turn it on afterwards, as a measured change.
- [ ] Compare startup and working set against the .NET Native build. `Telegram.Benchmarks` already
      has both hosts wired up if a narrower measurement is wanted.

## Phase 6 — packaging

The template's wapproj differs from `Telegram.Msix` in ways that are all mechanical, but all
required:

- [ ] `<DebuggerType>CoreClr</DebuggerType>`
- [ ] The project reference carries `UseLowTrustEntryPoint`, `SkipGetTargetFrameworkProperties`
      and `PublishProfile=Properties\PublishProfiles\win-$(Platform).pubxml`
- [ ] `Microsoft.Windows.SDK.BuildTools` package reference
- [ ] `EntryPointProjectUniqueName` points at the new project

Whether that is a second wapproj or a conditioned property in the existing one is open. A second
one duplicates the manifest and the signing setup; a conditioned one risks the packaging path that
`ShouldUnsetParentConfigurationAndPlatform` already made delicate.

## Keeping both green

The two projects must build the same set of files, and nothing enforces that. A parity check —
compare the glob against the legacy project's item lists, fail on divergence — is worth writing
early, and it also catches the existing trap of adding a file and forgetting its `Compile` entry.

- [ ] Parity script, run manually at first
- [ ] Decide whether it becomes a build target or stays a script

## Cleanups this turned up

- [ ] Rewrite `TypeContainerGenerator` as a source generator, beside the one in
      `Telegram.Generators`. It builds the dependency container — the `_globals`, `_singletons`,
      `_lazySingletons` and `_instances` tables and the constructor calls for them — by reflecting
      over the app's own types (`GetConstructors`, `GetProperties`) and returning C# as a string,
      from a `[Conditional("DEBUG")]` method that is run by hand and whose output is pasted back
      into source. A generator would emit it at build time, keep it in step with the types by
      construction, and take the last reflection out of the app: those three calls were the whole
      of NativeAOT's trim warnings once `TypeCrosserGenerator` was deleted, and the only reason
      the file now needs `#if DEBUG`.

- [ ] Rename the `Unigram*` MSBuild properties and targets to `Telegram*`. They follow the product
      name while everything else in the build — repo, projects, assembly, namespaces — says
      Telegram. Fifteen names (`UnigramRoot`, `UnigramUsesVcpkg`, `UnigramVcpkg*`, `UnigramAdd*`,
      `UnigramCheckVcpkg*`, `UnigramVlcPlugin`) live in `Directory.Build.props` and
      `Directory.Build.targets`, and nothing else in the repository references them, so it is one
      mechanical commit.

## Open questions

- Project name. `Telegram.Modern.csproj` is a placeholder.
- How long both are kept. Everything above assumes indefinitely; if the modern build is only ever
  a development-time convenience the packaging phase can be skipped entirely.
- Store submission on the modern stack has not been looked at at all.
