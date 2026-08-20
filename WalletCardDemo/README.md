# Wallet Card Demo

A standalone UWP app that reproduces the iOS 27 SceneKit wallet-card sample
(`WalletCard.zip`) — the Figma angular gradient, the soft-light stars, the
brushed-metal grain and the tilt parallax — as a test bed before any of it goes
near Unigram.

Classic UAP (`TargetPlatformVersion` 10.0.26100, min 10.0.18362) with the same
Win2D 1.28.3 that `Telegram.csproj` already references, so anything that works
here can move over as-is.

## Run it

```powershell
.\BuildAndRun.ps1          # rebuild, package, register, launch
.\Capture.ps1              # screenshot just the demo window
```

## What maps to what

| iOS sample | Here |
|---|---|
| `WalletCardModel.swift` | `WalletCardModel.cs` — 1:1, metrics unchanged |
| `CardTextures.swift` | `CardTextures.cs` — CoreGraphics → Win2D drawing sessions |
| `CardShaders.swift` (Metal) | `Shaders/CardFront.hlsl` — D2D custom effect via `PixelShaderEffect` |
| `WalletCardSceneView.swift` (SceneKit) | `WalletCardView.cs` — `CanvasAnimatedControl` + `Transform3DEffect` |
| `ContentView.swift` | `MainPage.xaml` + `WalletCardView.DrawBackdrop` |

The render loop is a faithful port: same tilt limits, same
`1 - exp(-dt * 8)` follow, same idle float, same gradient-spin accumulator that
the card's angular velocity feeds.

### What deliberately changed

- **No SceneKit.** The card is a flat image through `Transform3DEffect` (D2D's
  4×4 transform). Its thickness is one extra pass — the same silhouette drawn
  one card-depth behind, so a tilt uncovers a sliver of edge where the extrusion
  would be. At the sample's own limits (8°/12°) that sliver is about a pixel,
  which is exactly what the 6pt-on-370pt extrusion gives on iOS too.
- **No gyro** (by request; desktops have none). The pointer drives the same
  inputs the device attitude did, so everything downstream is untouched. This
  demo tilts on *hover*; Unigram's `VisualUtilities.AttachTilt` uses press,
  deliberately, because hover is feedback only mouse users get.
- **No PBR.** `_surface.emission` / `metalness` / `lightingEnvironment` have no
  D2D equivalent, so the environment reflection is two explicit specular lobes
  in the shader, placed to match the studio image the sample builds procedurally.
- **Stops are sRGB, not linear.** SceneKit lights in linear and outputs sRGB, so
  the sample's literals are linearized; D2D composites in the target's own
  space, so `#0079FF` / `#169AF9` go in directly. Same two colors.
- **Noise is procedural**, not a texture: a wrapped texture read needs
  `SamplerCoordinateMapping.Unknown`, which makes D2D request an unbounded
  source rect per tile. Same construction (hash → horizontal box blur → contrast
  → fine grain), no sampling hazard.

## Things that cost time, so they are written down

- **fxc needs `/D D2D_ENTRY=main /D D2D_FULL_SHADER`.** Without them
  `d2d1effecthelpers.hlsli` never emits the `SV_TARGET` wrapper around
  `D2D_PS_ENTRY` and fxc reports "entrypoint not found". See the
  `CompileCardShaders` target.
- **Shader inputs must share the output's DPI.** A mismatch makes Win2D insert a
  DPI-compensation pass, which renders into a *padded* intermediate — and
  `D2DGetInputCoordinate` normalizes over the padded texture, not the image. The
  symptom is memorable: `uv` reaches 0 at the top-left but stops short of 1 at
  the bottom-right, so a UV-derived rounded rect rounds one corner and squares
  the other three.
- **`Transform3DEffect.InterpolationMode` rejects `HighQualityCubic`** — D2D's
  3D transform only takes the modes its own sampler knows. `Anisotropic` is the
  right pick anyway for a foreshortened quad.
- **`PixelShaderEffect.IsSupported` is an instance method**: it answers for this
  shader on this device, so construct the effect first, then ask.
- **Build with `/t:Rebuild`, not `/t:Build`.** The CoreCLR `entrypoint\` payload
  is produced by a full pass; an incremental build silently leaves it out, and
  the app then launches under the *desktop* CLR and dies before any managed code
  runs — no stack, no log, just `e0434352` in the event log.
- **`bin\x64\Debug` is not a registrable layout.** Only the packaging step
  arranges the CoreCLR host at the root with the managed exe under
  `entrypoint\`, so `BuildAndRun.ps1` packs, unpacks and registers *that*.

## Known gaps

- Text positions are the sample's Figma numbers taken literally. Cascadia Mono
  and Nunito have different metrics from SF Mono and SF Pro Rounded, so a point
  or two of vertical nudging is the tuning surface if pixel parity matters.
- `PixelShaderEffect.Properties` is `IDictionary<string, object>`, so the
  per-frame constants box — four small allocations a frame, the only ones on the
  draw path. Worth a native D2D effect if this moves into Unigram, where
  `Telegram.Native` already owns the device.
- The card redraws continuously while the window is open. Before shipping,
  `CanvasAnimatedControl.Paused` should follow visibility and window activation.
