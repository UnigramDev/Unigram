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
      `.pri` beside it. Moot now: the prebuilt binaries are gone and `rlottie` is a submodule
      built in-tree.

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
        <CsWinRTIncludes>Telegram.Native</CsWinRTIncludes>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="Microsoft.Windows.CsWinRT" Version="2.2.0" />
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
| `Common\Direct2D.cs` | `new Direct2DDevice((Window)null)` | **CsWinRT gives every projected runtime class an `IObjectReference` constructor**, so a bare `null` is ambiguous. Expect this wherever the app passes `null` to a projected constructor. |
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
- **And they must stay projections - a `ProjectReference` was tried and taken back out,
  2026-08-24.** Visual Studio offers it, the csproj accepts it, and it compiles: `NETSDK1130` is
  about a bare `<Reference>` to a winmd, and says nothing about referencing the project that
  produces one. Fela published `Telegram.Modern` that way successfully. It still does not work,
  for two reasons that only show up outside Visual Studio:

  - **A referenced project is built.** A worktree would then have to build `Telegram.Native` and
    `Telegram.Native.Calls` - vcpkg, tgcalls, webrtc - which is exactly what copying `x64\` in
    from the main checkout exists to avoid. `ReferenceOutputAssembly="false"` does not help: it
    stops the reference contributing files, not the project being built.
  - **The vcxproj has no `OutDir`**, so C++ defaults to `$(SolutionDir)$(Platform)\$(Configuration)\`.
    A bare `MSBuild Telegram.Win32.csproj` has no solution, `$(SolutionDir)` falls back to the
    project directory, and the reference's copy lands in `Telegram.Native\x64\Debug\...` while
    `ReferenceCopyLocalPaths` still reads `x64\Debug\Telegram.Native\...` - two publish items with
    one relative path, `NETSDK1152`. It works in Visual Studio because the IDE defines
    `SolutionDir`, which is why the same change can pass there and fail on the command line.

  **What it was meant to prevent, and how to recognise it instead.** Nothing sequences the native
  projects before the app, so a package can end up carrying a `Telegram.Native.dll` older than the
  winmd its projection was generated from - a class the app calls that the binary does not have.
  The symptom is unhelpful: an unhandled managed exception at startup (WER exception code
  `e0434352`), no line in `tdlib_log.txt` because it dies before TDLib initialises, and no app
  error report. The check that settles it in seconds is whether the packaged binary contains the
  class name at all:

      python -c "print('TextHost' in open(r'...\publish\Telegram.Native.dll','rb').read().decode('latin-1'))"

  Build the native projects first, then the app - and re-publish after touching either.
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
      `Direct2D.Current` is `[ThreadStatic]` and constructs
      `new Direct2DDevice(Window.Current)` per view, whose native constructor takes
      `window.Compositor()` — the first thing to check is whether that construction is what
      throws on the secondary view.
- [x] `{Binding}` **audited and fixed — see `binding-audit.md`.** 108 occurrences across 26 of 475
      files, against 2038 `x:Bind`, plus three bindings built in code and the by-name properties
      (`DisplayMemberPath`, `ItemsPath`). Two mechanisms satisfy a classic binding and they are
      disjoint here: `XamlTypeInfo.g.cs` emits accessors for any managed member reached from markup,
      and `[GeneratedBindableCustomProperty]` covers the types the compiler cannot see because they
      are only ever a runtime `DataContext`. Three live defects, all fixed.

## Phase 5 — AOT (compiles, links, runs; three features still under repair)

**It builds and links**: a 57 MB native `Telegram.exe`, **zero errors, zero trim warnings, zero
`CsWinRT1028`**, and it launches and runs. The publish is a 50-file layout against 250 for CoreCLR,
with every native dependency intact.

Build and deploy exactly as in Phase 4, minus `-p:PublishAot` — the property lives in the project
now. Three traps that cost real time, none of which announce themselves:

- **`-p:PublishAot=true` on the command line is a global property**, so it flows into the
  `ProjectReference` and `Telegram.Generators` (netstandard2.0) fails `NETSDK1207`.
- **Without `-restore` the AOT step silently no-ops.** `Microsoft.DotNet.ILCompiler` is an implicit
  package reference resolved at restore; with it missing the build is green, the layout is ordinary
  managed output, and `Telegram.exe` is a 162 KB apphost. A CI job doing this would ship a non-AOT
  package looking like success. Check for `native\` and an exe of tens of MB.
- **`[Conditional("DEBUG")]` still binds the symbol**, so a type with a call site cannot be put
  behind `#if DEBUG` — the call site has to go with it.

### What the app needed

- `TypeCrosserGenerator` deleted (dead), `TypeContainerGenerator` behind `#if DEBUG`: between them
  they were the whole trim-warning surface, five warnings, all in code Release cannot reach.
- Fourteen ABI-crossing classes marked `partial` (`CsWinRT1028`), two of them LottieGen output that
  will lose the keyword when regenerated.

### The failures that only appear at runtime

None of these warned at build time. All were found by running a feature, and all but one are a
missing entry in `CsWinRT.cs`, which is effectively the app's AOT manifest. `TG1001`/`TG1002`/`TG1003` now
catch the whole class at compile time — see [the analyzer](#the-analyzer) below.

| symptom | cause | fix |
|---|---|---|
| sticker panel empty, headers only | `CollectionViewSource.ItemsPath` resolves reflectively; the generated lookup existed, but `MvxObservableCollection<StickerViewModel>` had no marshalling support for that instantiation — the emoji drawer works because its group exposes `MvxObservableCollection<object>` | registered both sticker instantiations |
| call window: no video, no lottie | `CompositionTarget.Rendering` subscribed from a secondary view is delivered to the **first** view's thread — [CsWinRT #2524](https://github.com/microsoft/CsWinRT/issues/2524) | `CompositionTargetImpl`, registered through the ABI per view |
| hard crash on hovering a forward header | `TextStyleRun.GetParts` returned `Array.Empty<TextStylePart>()`; **a managed array cannot cross as `IVector<T>` when `T` is a value type that is not a WinRT fundamental** — it boxes through `IReferenceArray`, which AOT cannot synthesise. `GeneratedWinRTExposedExternalType` does *not* help: it emits CCWs for managed types. An array of a *runtimeclass* is fine, marshalling as an array of pointers: `MessageSelector` hands `ConfigurePositionXInertiaModifiers` one and it works | return a shared empty `List` |
| freeform gradient not drawn | same rule: `GetColors()` returns `Color[]` handed to `CreateFreeformGradient(IVector<Color>)`, and the throw was swallowed inside XAML brush creation | convert to `List<Color>` at the call site |
| `CompositionVSync` NRE, killing calls | self-inflicted: through the ABI the rendering args arrive as a bare `IInspectable`, so `e as RenderingEventArgs` is null. Casting per frame would be a QueryInterface per frame | both consumers now read `Stopwatch.GetTimestamp()`; `VisualUtilities.Tilt` was silently reading a stale timestamp for the same reason |
| chat background: gradient but no pattern | the **setter** `_freeform.Colors = GetColors()` still handed a `Color[]`; only the constructor call had been converted | build the `List<Color>` once, use it on both branches |
| …then still no pattern | `CreateEffectFactory(effect, ["Intensity.Opacity"])` — a **collection expression targeting a read-only interface** (here `IIterable<String>` → `IEnumerable<string>`) synthesises a type that can never have a CCW. **Two** of them, picked by element count: `<>z__ReadOnlySingleElementList<T>` for one, `<>z__ReadOnlyArray<T>` for more — so a site that works with two elements can break when one is removed. `List<T>`, `IList<T>` and `ICollection<T>` are safe, being mutable: those give a real `List<T>` | `new[] { … }`, as every other call site already used |
| hard crash joining a group call | `MessagesHost.ItemsSource = new ObservableCollection<GroupCallMessage>(…)`. **External** generic instantiations get no CCW vtable — the generator only emits those for the app's own partial types — so XAML's QI for `IBindableIterable` fails and `set_ItemsSource` returns `E_INVALIDARG`. On a `DispatcherQueueHandler` that is a fail-fast | registered the instantiation in `CsWinRT.cs` |
| leaving a call from the call window did nothing | `ContentPopup` completes the task `ShowQueuedAsync` awaits from a `CompositionTarget.Rendered` callback. Subscribing threw `NotSupportedException: Cannot provide IReference support for delegate type 'EventHandler<RenderedEventArgs>'` — nothing roots that marshaller, because `CompositionTargetImpl` bypasses the projection that would have — and `QueueCallbackForCompositionRendered` swallowed it. Every dialog on that view was dead | `CompositionTargetImpl.Rendered` marshals `EventHandler<object>`, like `Rendering`; no caller reads the args |

### The analyzer

`Telegram.Generators\WinRTExposedTypeAnalyzer.cs`. Two rules, both warnings, both silent on .NET
Native — `GeneratedWinRTExposedExternalTypeAttribute` does not exist there, and neither does the
problem, so the analyzer resolves the attribute and switches itself off when it is missing.

- **TG1001** — a concrete array or constructed generic boxed into a WinRT `object`, or into
  `IEnumerable`/`IList` without an element type: `ItemsSource`, `Tag`, `Content`, `SetValue`. The
  runtime has only the concrete type to go on and needs a vtable for it. The message names the
  attribute to paste.
- **TG1002** — an array of a value type that is not a WinRT fundamental, passed to a typed
  collection. `IReferenceArray<T>`, which AOT cannot synthesise. The message names the `List<T>`.
- **TG1003** — a collection expression targeting a read-only interface. The synthesised type has no
  CCW and cannot be given one, so it is the one case a registration does not fix. TG1001's reasoning
  does not reach it: the target is typed, but the marshaller the generator emits still needs a CCW
  for whatever concrete type turns up.

The dividing line between the first two is whether the compiler can see the conversion. A parameter typed
`IEnumerable<T>` is a conversion in source, so CsWinRT generates the marshaller for that
instantiation and any concrete type reaches it. A parameter typed `object` is not. Elements are the
same question one level down, at a point no call site corresponds to — which is how
`List<IList<Rect>>` marshals and then throws when the native side calls `GetAt` — so both rules
follow initializers into a collection when the signature only names an interface.

Two things it taught immediately:

- **Nine of thirteen** entries in the ItemsSource block of `CsWinRT.cs` were mine from a grep sweep;
  two named the wrong type entirely, because what those popups assign is the
  `DiffObservableCollection` and the `List` beside it is only the backing store. That block is now
  the analyzer's output. **Rerun it rather than adding entries by hand.**
- The forward-header crash was not fixed, only moved: `MessageForwardHeader` builds its own
  `TextStylePart[]` rather than going through `GetParts`.

Detecting the WinRT boundary is by CsWinRT's own `[WindowsRuntimeType]`/`[ProjectedRuntimeClass]`
attributes, not by namespace: the projection for a referenced C++/WinRT component is generated
**into** the consuming assembly, so `Telegram.Native.Direct2DDevice` is a type of this
compilation and looks managed by every other measure.

Blind spot: a binding assigns through the property's **declared** type, so where that is an
interface the concrete type is unknowable at compile time and no rule can fire. That is how
`SettingsStoragePage` bound `Statistics.ByChat` and threw `E_INVALIDARG` with nothing to warn on.
For TDLib it is closed from the other end - `CsWinRT.Vectors.cs` registers every vector
instantiation in the schema, both parsers materialising a vector as `List<T>` - but it stays open
for any other property typed as an interface. That file is **real source, not generated**: CsWinRT
reads these attributes in its own generator, and a generator cannot see what another generator
wrote. `TDAPI003` compares the schema against the attributes actually present and reports what a
TDLib update has added. Classic `{Binding}` is a second blind spot, having no
C# anywhere; `x:Bind` is covered, which is why the analyzer opts into analysing generated code.

### Still open

- [x] **Group calls crash on join** — fixed; see the table above. Diagnosed from a crash dump rather
      than the log, with the recipe below.
- [x] **Background pattern** — fixed; it was two further instances of the same rules.
- [x] **Leaving a call from the call window did nothing** — fixed; the delegate marshaller row above.
- [ ] **A secondary view's RCWs are released after its XAML core is gone.** Closing the call window
      access-violates a while later, inside `RoUninitialize` on that view's thread. Fully diagnosed:
      the .NET finalizer thread finalizes an `ObjectReferenceWithContext`, whose `Release` marshals
      back to the creating apartment through `IContextCallback`; that apartment is mid teardown but
      still pumping in `WaitForPendingGitRegistrations`, so it services the call, and XAML unparents
      a `CRichTextBlock` whose `CCoreServices` is already null — `GetMainRootVisual` reads
      `[rbx+0xD0]` with `rbx = 0`. The object is a `FormattedTextBlock`: it is built on a
      RichTextBlock, and its paragraphs, spans and runs are XamlDirect objects, created through
      XamlDirect whether or not a recycle pool is attached.

      **Not specific to calls** — any secondary window hosting one is exposed, a chat opened in its
      own window included. It only surfaced now because both preconditions are recent fixes: group
      call messages started rendering (so that window had a FormattedTextBlock at all), and Leave
      started working (so the window could close).

      Nothing in the app holds these wrongly: `RelativeDateService._current` and the
      `Direct2DDevice` caches are already `[ThreadStatic]` and released from
      `WindowContext.OnShutdownCompleted`, and `FormattedTextBlockRecyclePool` is an instance field.
      A fix has to make the release happen on the owning thread while XAML is still alive, or not
      happen at all. `GC.WaitForPendingFinalizers` from the view thread is the obvious lever and
      also the obvious deadlock, since the finalizer needs that same apartment to pump. Worth an
      upstream report as well: CsWinRT dispatches a Release into an apartment that is uninitializing.
- [x] **`CsWinRT1034` and `CsWinRT1035`** from the 2.3.1 analyzers: casting to a WinRT type consults
      its metadata, so trimming can drop a type that is only ever cast to and the cast then throws
      at runtime. 1798 of them, but they collapse to **131 distinct types**, and a type is only at
      risk if *nothing else* roots it. Cross-referencing the cast targets against every `new`,
      declaration and XAML usage left **three**: `AppServiceTriggerDetails`,
      `Windows.Data.Xml.Dom.XmlElement` (the badge updater, which had also been throwing a
      first-chance AV) and `ApplicationDataCompositeValue`. All three now carry
      `[DynamicWindowsRuntimeCast]`, with a dummy in `CsWinRT.cs` for .NET Native beside the
      existing `GeneratedBindableCustomProperty` one.

      The remaining ~1795 are noise: `TextBlock`, `Style`, `Grid`, `FrameworkElement` and the like,
      each rooted a hundred times over. **Re-run the cross-reference rather than reading the count**
      — it immediately caught a fourth, `Windows.Foundation.Deferral`, introduced by the teardown
      fix an hour earlier. The script is `castonly.sh` in the session scratchpad; it wants checking
      in somewhere if this is to stay honest.
- [ ] Diagnostics left in the working tree: none. The `VoipGroupCall` callback logging and the
      `ChatBackgroundBrush` probes are gone. The catches in `ChatBackgroundBrush`,
      `DispatcherContext.Dispatch` and `QueueCallbackForCompositionRendered` now log rather than
      swallow, which is worth keeping — each of them hid a crash for at least one session.

### Reading a fail-fast out of a crash dump

Worth more than any amount of logging here, because **the TDLib log is buffered and a fail-fast takes
its tail with it** — the entries that would explain the crash are exactly the ones lost. The dump
keeps everything. `cdb.exe` ships with the Windows Kits under
`%ProgramFiles(x86)%\Windows Kits\10\Debuggers\x64`, and dumps land in `%LOCALAPPDATA%\CrashDumps`.
Pass commands in a script file rather than `-c`, or `.sympath+` swallows the rest of the line:

    cdb -z <dump> -y "srv*;<publish dir>" -cf script.txt

`.ecxr` gives the exception record. For `0xC000027B` the first parameter is a pointer to an array of
stowed exceptions and the second is the count. `dq` it to reach the `SE02` record: `ResultCode` at
+0x08 is the HRESULT (`!error` it), and the stack-trace pointer at +0x20 holds return addresses, so
`dps <ptr> L<count>` against `Telegram.pdb` prints the **managed** stack and `ln <addr>` turns a
frame into a source line. That is how `set_ItemsSource` was found after two sessions of guessing.
`ln poi(<obj>)` on a suspect pointer resolves its C++ vftable, which names the XAML type.

### Two things worth knowing about the harness

- **Crash reports are uploaded and deleted on the next launch.** `HandleReportAsync` POSTs to
  `integrations.telegram.org/ugram_crash_logs/`, so every AOT crash this evening went to the
  production endpoint tagged 12.7.0.12195, and the local JSON was gone before it could be read.
  Worth suppressing for this identity.
- **Verbosity has to be exactly Warning (2) to see managed logging.** Two gates, not one:
  `Logger.Log` passes if `level <= VerbosityLevel`, but it then calls `AddLogMessage(**2**, …)`,
  and `Logging::add_message` clamps that hardcoded 2 into `VLOG(client)` — so below 2 TDLib
  discards every managed entry, `WatchDog` included. Above it, at 4, the log rotates every couple
  of minutes and takes them with it. Note the default is 4 here: `SettingsService` picks
  `IsPackagedRelease ? 4 : 2`, and a sideloaded `-Register` package is Developer-signed, not Store.
- **F5 needs `Properties\launchSettings.json`.** It is the one piece of the VS template this
  project never copied, and without it VS starts the build output's `Telegram.exe` as a bare
  process outside the package container, where a UWP XAML app fails fast in `Windows.UI.Xaml.dll`
  (`0xC0000409`, subcode 7) with no window, no log and no report. The tell is in the WER event:
  the faulting path is the loose exe and the package full name is empty. The profile is one line,
  `"commandName": "MsixPackage"`, which is what makes VS deploy the layout and activate the app
  by identity rather than run the exe.
- **Copy-local paths have to be absolute.** `ReferenceCopyLocalPaths` reaches the
  `.appxrecipe` verbatim and Visual Studio resolves the recipe against its own working
  directory, so the relative entries for the two C++/WinRT components and the tdjson set
  built and packaged fine but failed the F5 layout copy with `DEP1000 ... 0x80070003`. They
  are `$(TelegramRoot)`-rooted now.
- **Never `Add-AppxPackage -Register` the build output directory itself.** VS deploys into
  `net10.0-...\AppX` and registers that. Registering the directory the build writes to leaves
  the deployment stack holding it - `microsoft.system.package.metadata` appears inside it, the
  directory cannot even be renamed, and the next build fails in MakePri with
  `PRI210: 0x800704c8` (ERROR_USER_MAPPED_FILE) moving its staging directory in. Unregistering
  and deleting that folder clears it.
- **To debug a published AOT build instead**, Debug → Other Debug Targets → **Debug Installed App
  Package**, pick `38833FF26BA1D.UnigramNet10`, tick "do not launch, but debug my code when it
  starts", Native Only; `Telegram.pdb` sits beside the exe in `publish\` so symbols resolve on
  their own.
- **Publish needs `-p:SelfContained=true` and `vswhere.exe` on `PATH`.** Without the first,
  `PublishAot` does not imply self-contained here and ILLink fails `NETSDK1102`; without the
  second, ILC's linker lookup substitutes the shell's "not recognized" text into the `link.exe`
  command line and fails `MSB3073` with exit code 123. `vswhere` lives in
  `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer`, which a plain shell does not have.

## Phase 5 — AOT, original plan

- [ ] Turn on `PublishAot`, resolve trim and AOT warnings until clean.
- [ ] `DisableRuntimeMarshalling` is in the template but leave it **off** at first. There are 43
      `DllImport`s, mostly `kernel32` with `CharSet.Unicode`, plus tdjson's cdecl entry points; it
      changes their marshalling. Turn it on afterwards, as a measured change.
- [ ] Compare startup and working set against the .NET Native build. `Telegram.Benchmarks` already
      has both hosts wired up if a narrower measurement is wanted.

## Phase 6 — packaging (x64 builds a signed bundle)

`Telegram.Msix.Modern` packages `Telegram.Modern.csproj` the way `Telegram.Msix` packages
`Telegram.csproj`. A second project rather than a switch on the first, because the two have to be
installable at once, which means different identities, which means different manifests — the one
thing a single wapproj cannot condition cleanly. Nothing is duplicated but the project file: the
manifest is generated from `Telegram.Msix\Package.appxmanifest` with the name and display name
poked, the same trick `Telegram.Modern.csproj` uses for the loose layout.

```powershell
MSBuild.exe Telegram.slnx /target:Telegram_Msix_Modern `
  /p:Configuration=Release /p:Platform=x64 /p:UapAppxPackageBuildMode=SideloadOnly `
  /p:AppxBundlePlatforms=x64 /p:AppxBundle=Always /p:AppxPackageSigningEnabled=True /m
```

Output: `Telegram.Msix.Modern\AppPackages\Telegram.Msix.Modern_<version>_Test\*.msixbundle`, 85 MB,
signed, carrying `Telegram.Modern\Telegram.exe` at 58 MB — the real NativeAOT binary — and
`Telegram.Stub\Telegram.Stub.exe`.

**Three identities, one per deployment mechanism.** Windows cannot move an installed app between a
registered loose layout and a bundle, any more than between an msix and an msixbundle, so any two
sharing a name would leave whichever was installed first blocking the other:

| identity | mechanism |
|---|---|
| `TelegramFZ-LLC.Windows` | the shipping bundle, .NET Native |
| `38833FF26BA1D.UnigramNet10` | the loose layout, `Add-AppxPackage -Register`, for development |
| `38833FF26BA1D.UnigramNet10Bundle` | the AOT bundle |

**What the template's four bullets did not say**, all of it found by building:

- **`PublishProfile` as `ProjectReference` metadata is inert outside Visual Studio.** Nothing in
  DesktopBridge, AppxPackage or the .NET SDK reads it; only `$(PublishProfile)` as a property of
  the project being built. Without passing it through `AdditionalProperties` as well, packaging
  resolves the app at its no-RuntimeIdentifier output path and PRI generation fails looking for a
  `resources.pri` that was never built there.
- **The app does not land at the package root.** That exemption belongs to the legacy UWP project
  system; an SDK-style reference is payload like any other and goes in a folder named after the
  project. `Application` copes on its own — it says `$targetnametoken$.exe` and the packaging
  rewrites it — but `startupTask` and `appExecutionAlias` spell `Telegram.exe` out, and MakeAppx
  rejects the package because that path does not exist. The generated manifest rewrites both to
  `Telegram.Modern\Telegram.exe`, derived from the project name rather than written out.
- **Only one certificate in the repository is still valid.** `Telegram.Msix_TemporaryKey.pfx` runs
  to 2027-04; the three beside it expired in March 2025, and signing with one fails `APPX0108`.
- **Do not poke the publisher.** The packaging targets substitute the certificate's subject into
  the generated manifest at package time, so the source manifest's `CN=Telegram FZ-LLC` is fine.
- **Build through `Telegram.slnx`**, never the wapproj directly — `$(SolutionDir)` is empty
  otherwise, the same trap the native `.vcxproj` files have.
- From Git Bash use `-p:` switches, not `/p:` — MSYS strips the slash and turns `/nologo` into a
  path, which surfaces as the unhelpful `MSB1008: Only one project can be specified`.

The publish profiles are tracked now: `.gitignore` excludes `*.pubxml`, so they only existed on one
machine, and the wapproj references one per platform by path. Their `PublishDir` had to move under
`bin\modern\` — the template's default is `bin\$(Configuration)\`, which is where `Telegram.csproj`
puts its .NET Native output.

### Still to do

- [x] **ARM64 builds.** Done 2026-08-25 from a fresh clone: the native projects, ILC, and a
      bundle carrying both architectures. `Build.Modern.ps1 -Platform x64,ARM64` drives it.
- [ ] `UpdateManifest.ps1` before any bundle meant to supersede a previous one; it stamps the
      version from `git rev-list --count HEAD`, and without it the new bundle overwrites the old
      one at the same path.
- [ ] The bundle has been verified by unpacking it, but never installed. Doing so needs the test
      certificate trusted, which `Add-AppDevPackage.ps1` beside it handles.
- [ ] Decide whether `Telegram.Msix.Modern` belongs in the default solution build. It is in
      `Telegram.slnx` with `<Deploy />`, so a plain solution build now pays for a NativeAOT
      compile.

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

## Diagnosing RPC_E_SERVERCALL_RETRYLATER

Not a port item — it outlives this work — but it is written down here because deleting
`StackCapture` is what leaves the gap.

`0x8001010A` is an ASTA refusing an incoming call because the target thread is not in a state to
take one. `WatchDog` treats it specially: it is the only condition that ever passed
`captureAllThreads: true`, which walked every thread's stack in-process through `dbghelp` and put
the result in the crash report. Fela's assessment is that it never produced anything trustworthy,
so it goes.

What replaces it is the open question. Three things learned on 2026-08-15 bear on it:

- **A dump answers this question completely and immediately.** The ASTA deadlock that day was
  diagnosed from `~*k` over all 57 threads in about a minute: the UI thread parked in
  `RhWaitForPendingFinalizers` inside `CoWaitForMultipleHandles`, the finalizer thread blocked in
  `MTAThreadWaitForCall` on a context callback into that same apartment. That is exactly the
  picture `StackCapture` was trying to build, and the debugger builds it correctly.
- **In-process is the hard part.** Walking *other* threads' stacks from inside the process, while
  they are running, is what was unreliable — not the idea. `MiniDumpWriteDump` from a watchdog
  thread would get the same fidelity as the debugger, at the cost of a large artifact to store or
  upload.
- **The log is the wrong instrument, and not only here.** TDLib's file log is buffered, so a
  fail-fast takes the tail with it. `Logger` already keeps its own ring buffer of the last N
  entries with `Logger.Dump()`, which survives in memory and is already in the report.

Worth investigating, roughly in order of cost:

- [ ] Whether the ring buffer alone is enough. `0x8001010A` is raised on the *caller*; what is
      wanted is what the *callee* was doing. A per-thread breadcrumb, or simply dumping the
      existing buffer with thread ids attached, may answer it without walking anything.
- [ ] `MiniDumpWriteDump` with `MiniDumpWithThreadInfo` on the watchdog thread, written to
      `LocalState` and uploaded like the JSON reports are. Truthful and complete; the questions are
      size and whether the endpoint will take it.
- [ ] Whether the AOT build changes the shape of the problem at all. ASTA reentrancy rules are the
      root cause of both the deadlock and this error, and they are a Windows behaviour, not a
      runtime one - but CsWinRT marshals releases back into apartments in a way .NET Native did
      not, which is what turned it into something reproducible.

## Open questions

- Project name. `Telegram.Modern.csproj` is a placeholder.
- How long both are kept. Everything above assumes indefinitely; if the modern build is only ever
  a development-time convenience the packaging phase can be skipped entirely.
- Store submission on the modern stack has not been looked at at all.
