# Moving off ContentPopup

`ModalPopup` exists to replace `ContentPopup`. It was born as the fix for a crash — a managed
subclass of a WinUI 2 control dies in two halves, and `TeachingTip` touches itself from a
destructor and from revokers it never releases — and `ContentPopup : ContentDialogEx : ContentDialog`
is the same shape of hazard, one `muxc` release away from the same fault.

`ModalPopup` derives from `ContentControl` and hosts itself in a `Popup` it owns. Nothing in this
subsystem may derive from a `muxc` control again.

## Where it stands

The surface and the chrome are both done. A `ModalPopup` now carries what a `ContentPopup` carries:

- three buttons, their text, content, styles and enabled flags, `DefaultButton`, `CloseButtonResult`,
  `ButtonsLayout`, `IsPrimaryButtonSplit`, `IsPrimaryButtonPending`;
- click events that cancel and defer, `Opened`/`Closing`/`Closed`;
- `Title`/`TitleTemplate`, the dismiss button, the `Content*` sizing four;
- `ShowQueuedAsync`/`OpenAsync`/`SetResult`/`Close`/`Block`/`IsAnyPopupOpen`, `IsFinalized`, and the
  `OnCreate`/`OnNavigatedTo`/`OnNavigatedFrom` contract, with
  `INavigationService.ShowPopupAsync(ModalPopup, …)` beside the `ContentPopup` overload;
- ContentPopup's brushes, padding, title metrics, separator and command row.

`PopupQueue` is the single per-`XamlRoot` queue **both** kinds wait on, so the two interleave
correctly for as long as the migration takes.

## What is left

Around 154 types derive from `ContentPopup`. They move a batch at a time; each one is a base-class
swap plus the differences below.

- **`Closed`, `Closing` and `Opened` change signature.** The `ContentDialog*EventArgs` types are
  sealed and cannot be raised from outside the framework, so ModalPopup declares its own. A handler
  written `(ContentDialog sender, ContentDialogClosedEventArgs args)` becomes
  `(ModalPopup sender, ModalPopupClosedEventArgs args)`. `ContentDialogResult` is unchanged, so
  every comparison against `Primary` still reads the same.
- **Never set `Width`, `MinWidth` or `MaxWidth` on a ModalPopup.** The control *is* the window — it
  is stretched to the `XamlRoot` so the smoke has somewhere to live — so a width on it shrinks the
  whole layer to a band down the left edge. Size the card with `ContentMinWidth`/`ContentMaxWidth`.
- **`ContentTemplate`, `Padding`, `HorizontalContentAlignment` and `VerticalContentAlignment` mean
  what they mean on a ContentPopup**, including alignment placing the *card* rather than the content
  inside it. 29 popups set `VerticalContentAlignment="Stretch"` for a tall dialog and keep working.
- **A `ContentDialog` cannot open over another one; a ModalPopup can.** Once a caller and everything
  it can raise are both ModalPopups, the `ShowNestedAsync` / `ShowPopupNestedAsync` /
  `ShowInputNestedAsync` variants have nothing left to do and should collapse back into the plain
  ones. That is the cleanest single win in the whole migration — see `MessagePopup`, `InputPopup`
  and `ViewModelBase`.
- **`ContentPopupButtonsLayout` was moved into `ModalPopup.cs`** so it outlives `ContentPopup.cs`.
  It wants a better name once the old file is gone.

## Deliberate gaps

- **The content-resize morph is not ported.** `ContentPopup.OnSizeChanged` animates a dialog growing
  or shrinking, using `BackgroundElement`, `BorderElement` and `AnimationElement` — three layers the
  ModalPopup template does not have, because it does not need them for anything else. Port it if
  someone misses it; the template grows two Borders and a redirect visual when they do.
- **`IsFullWindow` and `FullSizeDesired` are dead in `ContentPopup`** — nothing sets either, and the
  `FullDialogSizing` visual state is unreachable. They were not carried over.
- **`ContentPopup.Win32.cs`'s `ReleaseTextInput` is unnecessary here.** It exists because
  `ContentDialog` marks every key handled from its own accelerator handler and relies on an internal
  flag that does not hold in a XAML island. Nothing in ModalPopup derives from `ContentDialog`, so
  no key is stolen and there is nothing to give back.

## Traps worth keeping

- **A `ThemeShadow` set on an element breaks the hit test of every child it has.** The card carries
  none; an unfilled `ShadowCaster` rectangle behind it does, and `CardRoot` is what the open and
  close animations scale so the shadow travels with it. Fold the shadow back into `ContentRoot` and
  the whole popup goes dead to the pointer, dismissing on any click, because the transparent
  light-dismiss rectangle underneath receives them instead. `MessageBubble` carries one for the
  same reason.
- **`DefaultButton` says nothing about which button Enter presses.** Callers set it to give the
  primary button the accent colour. Enter always presses primary, guarded by the focus check when
  `DefaultButton != Primary` — which is what `ContentDialog` and `ContentPopup` do between them.
  Worth revisiting, but only deliberately.
- **`Hide(result)` runs the matching button's handler on the way out**, so hiding with `Primary`
  gets the validation a click would have got — and a button that is present but disabled refuses to
  close at all. Both are ContentPopup's behaviour, quirk included. `Hide()` closes directly.
- **Two results, not one.** `_closingResult` is whatever closed the popup and is what `ShowAsync`
  completes with; `_result` is what `SetResult` named and is what `OpenAsync` returns.
