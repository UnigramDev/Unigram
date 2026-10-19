# Wallet card: gift stickers

Gift NFTs from the wallet stuck on the TON wallet card. Resume doc.

## Sources

- Source 10-12: `GramWallet-20261012-1237-source.zip` (Downloads). `GiftStickers.kt` is the sticker
  board: physics, placement, gestures, easter eggs, persistence. `drawCurled` is the roll-on curl.
- APK 10-16: `GramWallet-20261016-0320-3dcard.apk`, not minified. Two specs were read from its dex,
  both in `notes/wallet-card/`:
  - `spec-stickers-diff.md` - what changed in the sticker board since the source.
  - `spec-card3d.md` - the GL card. Parked, see below.
- TL (Telegram, 2026-10-16): `areaPosition`, `tonNft.gift`, `tonWalletCardSticker`,
  `tonWalletCard`, `inputTonWalletCardSticker`, `userFullInfo.ton_wallet_card`,
  `setTonWalletCardStickers`, `upgradedGift.can_be_ton_wallet_card_background`. In td_api.tl since
  2026-10-19; the mocks are gone.

## Decisions (Fela's unless noted)

- **The grid is the wallet's own NFTs** (`WalletViewModel.Collectibles`, `getTonWalletNfts`), in a
  `GridView`. `TonNft.Gift` is optional: an NFT without it is not a gift, cannot go on the card, and
  gets a plain cell: its picture over the whole cell, or its name without one.
- **`WalletService.LoadCardAsync`/`SaveCard`** read the card from the current user's
  `UserFullInfo.TonWalletCard` and save with `setTonWalletCardStickers`, which can take 20 seconds: every finished edit queues a
  save, one in flight at a time, latest wins. A failed save is only logged.
- **Apply Color** is offered only for gifts with `CanBeTonWalletCardBackground`.
- **Layout like ProfilePage:** one `ScrollViewer` holds the header (card, buttons, sticky tabs) and
  both lists. The lists' templates have no scroll viewer, both sit at 0,0 under an empty `Border`
  header as tall as the real header, and `ScrollViewerIncrementalLoader` follows the visible one.
  The card is raised over the buttons and tabs while pinned and while its layer `IsActive` (a
  sticker in hand or moving), since a sticker beyond its edge is still drawn as part of it.
- **System drag and drop** started by each gift's container (`CanDrag`, `DragStarting`,
  `DropCompleted`), not the list's `CanDragItems`, which draws its own visual of the item before
  anything can replace it. The window's root grid is the drop target, because the card may be
  translated where layout does not know. The system visual is an empty bitmap, then hidden while
  over the window; a read-only `WalletStickerLayer` over the window, `DragLayer`, scaled to the
  card, carries the gift from the moment it peels out of its cell. On a drop the card's own
  sticker, made hidden when the drag started so its art is loaded, takes over in place and rolls
  on; a cancelled drag flies back into the cell. `WalletNftGridView` and its items skip
  `ListViewBase`'s own drag-target handling, which otherwise parts the items under the pointer as
  if making room for a drop.
- **The card comes to the gift:** while a gift is on its way (drag, menu), the real card is
  translated by an expression on the scroll viewer's manipulation property set, down to just under
  the title bar, and goes back after the landing. Hit testing does not see the translation, so the
  window maps points onto the face itself (`ToFace`): into the card's parent, which is not
  translated, then by the card's layout offset and `PinOffset`.
- **A sticker lands where it is dropped.** No easter eggs, no placement search, no shrinking.
  Added from the menu, it goes to the first free one of ten fixed spots.
- **Stickers sit under the print** (address, QR key, diamond, balance, name), whose text is made
  non-hit-testable so stickers under it can be taken. The white texts (balance, USD, name) cast a
  drop shadow, faded in, while a sticker's round lies under them where it is going
  (`WalletStickerLayer.IsUnder`, re-asked on `Arranged` and on the text's size). The engraved
  address does not: it is dark, at 0.3. The QR button and the diamond stop taking the pointer while a
  sticker is under them, so the press reaches the sticker.
- **Stickers are cut by the card's rounded edge once applied** (the prototype does not clip; its
  edge areas kept stickers in). Each sticker's sprite has its own clip, the face moved into its
  space every frame, rather than one on the layer: a sticker flying to or from a cell is unclipped
  meanwhile, and so is the selected one, whose overhang can still be seen and pressed. The part of
  an unselected sticker hanging over the edge takes no press.
- **A cell says "on card" only once the gift is on the card.** Its art gives way to the outline as
  soon as it is carried; the ribbon keeps the number until the drop.
- **Stickers are drawn natively, flat or curled** (2026-10-18; the composition die-cut looked
  bad: the effect brush is clipped to the sticker square, which the art fills, so the edge had no
  room). `StickerCurlSurface` draws a frame with the die-cut edge (the prototype's recipe: blur at
  1.44% of the frame, then alpha 10a - 150) and the curl (`StickerCurlEffect`,
  `Shaders/StickerCurl.hlsl`, the prototype's cylinder inverted per pixel). At rest it is the first
  frame, drawn once, and the scratch bitmaps are trimmed; on click a Lottie gift plays once, each
  frame rendered with `LottieAnimation.RenderSync` on the frame loop. The shadow is a composition
  `DropShadow` masked by the same surface. A plain `AnimatedImage` stands in only where the surface
  never drew (no shader model 4). `AnimatedImageEffect.DieCut` is gone.
- **Rotate and scale with a selection frame** modelled on `PaintEntityLayer`: click selects; the
  two handles turn and size (snapping to quarter turns); two fingers pinch; arrows, +/-, [ ],
  Delete and Escape from the keyboard. The wheel is left to scroll the window.
- **Removing:** drop it off the card, the menu, or Delete. It peels up and flies back into its cell,
  or shrinks away when the cell is not on screen.
- **A drop the window does not take arrives after a `DragLeave`**, which fades the carried gift, so
  `ReturnPreview` brings it back to full presence before flying it home.
- **Colours:** Apply/Remove Color on the cell and sticker menus. The reveal wave, the symbol pattern
  and `WalletCardMaterials` are kept from the spike. The colours go when their sticker leaves.
- **Never `DownloadFileAsync`.** `AnimatedImage` downloads stickers; the curl's still is decoded only
  once the file is there.
- **Tap a cell opens the gift** (`MessageHelper.NavigateToUpgradedGift`, by name: the NFT's copy
  has no owner); menus open only through `ContextRequested`.
- **2D only; 3D is parked** (2026-10-16). The 3D work is `notes/wallet-card/3d-card.patch`, written
  against the spike's board and never built; it no longer applies to this design.

## Layout

C# (`Telegram/Controls/Wallet/`):
- `WalletStickerLayer` - a `Canvas` in the card's face (360 by 220 face units): the stickers, input,
  selection, keyboard, the frame loop (CompositionTarget.Rendering, only while something moves),
  drop and preview, add, remove, `ToInput`.
- `WalletStickerView` - one sticker: its `StickerCurlSurface` drawing and `DropShadow`,
  springs, the home flight, playback.
- `WalletStickerFrame` (XAML) - the selection ring and handles, over the card's print.
- `StickerSpring`, `WalletCardMaterials` - kept from the spike.

Elsewhere:
- `Controls/Cells/WalletCollectibleCell` - gift cell (pattern, model, ribbon) or the plain placeholder.
  A gift out of its cell, on the card or carried, leaves its outline (`getStickerOutlineSvgPath`,
  as a composition shape) in the backdrop's symbol colour, with the art collapsed, and "on card" on
  the ribbon. Cells are found by address, never by instance.
- `WalletCard` - `Stickers`, the frame, `SetStyle` (origin in face units), the reveal and pattern.
  Its material is `Telegram.Native`'s `WalletCardSurface` (`WalletCard.hlsl`): face, thickness
  plates and lift shadow, drawn at the card's shown size on the shared graphics device. The
  colours are a `WalletCardPalette` on the style, the stock one for the blue card and each tone
  tinted by `WalletCardMaterials` for a gift; the reveal is the shader mixing two palettes inside
  a circle. Hover tilts it (mouse and pen only), and it draws only while something moves:
  `TwinkleWhileIdle` is off. `Eject(slot)` plays it coming out of an element on its side and
  sprung to rest, with the motion measured from the Android beta: 35 degrees about the slot line
  through `HingeRoot`, the eye placed for a 1.6 times spread, 1 s rather than 650 ms.
- `WalletBalancePopup` - a profile's card out of its balance: a `ModalPopup` with the card as its
  `Overlay`, its chrome hidden until the card is on its way to rest. A fixed `CardSpace` height,
  because a resize slides the popup and the card with it off the slot.
- `WalletWindow` - the layout, tabs, drag and drop, menus, pin, saving.
- `ProfileHeader` - the spike's read-only card on the profile, kept compiling with
  `Stickers.IsReadOnly`; out of scope otherwise.
- Strings: the `NOT FINAL` block at the end of `Resources.xml`.

The spike's files as they were before the rewrite are in the session scratchpad only
(`spike-backup`); the board, editor, overlay, float and grid are gone.

## Unverified

- Whether `DragUIOverride.IsContentVisible = false` hides the in-app drag visual of a `GridView`
  item drag.
- Whether a press on a sticker inside the scroll viewer reaches the layer before touch panning
  (`ManipulationMode` is set off System on the sticker).
- The curl's scene-position assumption: the effect is drawn untransformed into its own bitmap, so
  scene and input coordinates should coincide.
- The card material: how tinted palettes look on real backdrops, and the hover strength (0.7 of
  the full tilt).
- The eject: the tilt's sign is Skia's convention as read, not the bytecode's, and the slot's
  edge is hard where the beta fades it over 28 dp.
