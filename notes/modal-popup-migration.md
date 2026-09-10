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
- ContentPopup's brushes, padding, title metrics, separator and command row;
- ContentDialog's accessibility contract and its resize animation, both described below.

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

## Accessibility

Read from `ContentDialog_Partial.cpp` rather than guessed, and each piece is cited in the code:

- **`AutomationProperties.IsDialog` is set on the popup**, not on the control — ContentDialog does
  the same in `EnsurePopupAndSmokeLayer`. This is the whole ballgame: it is what makes Narrator
  announce a dialog, read its name and contents on entry, and stop walking what is behind it.
- **The name comes from the title**, and falls back to the subtitle cut at the first newline or
  after twenty words — `Popup::TruncateAutomationName`, which is what `ContentDialog::GetPlainText`
  applies to its content. It is set on both the popup and the control.
- **The focused element is saved and handed back when the popup closes**, weakly, as
  `ContentDialog::SetInitialFocusElement` does. A dialog that swallows the caret is the loudest
  accessibility failure there is. Two pieces of timing carry the whole thing, and both are copied
  from ContentDialog rather than reasoned out:
  - **Save at the first layout pass, not when the popup opens.** ContentDialog saves from
    `OnLayoutRootLoaded`, once its content has loaded inside the popup. Save any earlier and a
    popup raised from a context menu records the `MenuFlyoutItem` that opened it — an element the
    flyout destroys moments later, so focus can never go back to it. By first layout the flyout has
    closed and focus has settled on whatever it handed back to.
  - **Restore before the popup is closed, never after.** `ContentDialog::HideInternal` is emphatic
    about it, and rightly: as the popup goes away the FocusManager moves focus to the first
    focusable element of the page, and anything done afterwards has already been undone.

  Focus is not always on a `Control` either — a `Hyperlink` is a `TextElement`, and ContentDialog
  carries its own special case for that.
- **Escape and back run ContentDialog's close action**: the close button if it has text and is
  enabled, a plain `None` close otherwise — always closing, always reporting the key handled.
  `IsLightDismissEnabled` has no say in it; it governs a click outside the card and nothing else.
  A popup that must survive Escape cancels `Closing`, the way `Block` does.
- Decorative template parts are `AutomationProperties.AccessibilityView="Raw"`, and focus is
  trapped by `TabFocusNavigation="Cycle"` on the card.

The smoke is **one** layer, where ContentPopup has two. Its second exists only to retheme the smoke
rectangle the ContentDialog host owns, which does not see the app's theme override;
`SmokeElement` lives inside the popup and resolves `SystemControlPageBackgroundMediumAltMediumBrush`
against the theme the popup was given, so there is nothing to mix.

No automation peer of our own: ContentDialog does not override `OnCreateAutomationPeer` either.
(`ToastPopup` has one, but only to report `Pane` for a `ContentControl` sitting in a popup — it no
longer derives from TeachingTip, so there is no "press F6" announcement left to suppress.)

## The open and close animation

**ContentDialog's real animation is not the one in its template.** `DialogShowingStates` carries
storyboards that never run; what actually plays is `ContentDialogOpenCloseThemeTransition`, set on
the popup's `ChildTransitions` and built in code in
`ContentDialogOpenCloseThemeTransition::CreateStoryboardImpl` (`LayoutTransition_partial.cpp`).
`PlayOpenAnimation` and `PlayCloseAnimation` are transcribed from it:

- the card **settles inward** from `1.05` to `1.0` on open, and grows back out to `1.05` on close —
  not the other way round, and nowhere near the 0.85 the TeachingTip-era conversion used;
- scale runs **250 ms** opening, **167 ms** closing, on `cubic-bezier(0, 0, 0, 1)`, about the card's
  centre;
- opacity crosses **linearly** in **83 ms**, a third of the time — pass the linear easing
  explicitly, because a composition key frame defaults to an ease-in-out;
- ContentDialog fades the dialog and its smoke layer separately with the same curve; here both sit
  under the control's own visual, so one opacity animation covers them.

## The resize animation

`PlayResizeAnimation` carries the card between heights when its content resizes.

**The card is drawn, not laid out and not scaled.** A `CompositionSpriteShape` over a
`CompositionRoundedRectangleGeometry`, hosted on `BackgroundElement`, paints the fill and the
stroke from the control's `Background`, `BorderBrush` and `BorderThickness`; animating the
geometry's `Size` is exact at every frame. Scaling a `Border` instead — which is what ContentPopup
does — turns its corners into ellipses and its stroke into an uneven one, and it still needs an
offscreen surface for the command row it redirects.

Drawing also solves the direction problem. **A clip can only ever hide what is already laid out**,
so a clip-only version can reveal a card that grew and does nothing at all for one that shrank; the
box is already small and the clip is left cutting empty space. A shape can paint outside the
element's bounds, so the old, larger card is still there to shrink away.

Two rectangles animate together, in one batch:

- `_cardGeometry` — what is painted. A composition stroke straddles its path, so the path is inset
  by a whole stroke width and drawn at twice that, with the fill laid over the inner half; what
  survives is a stroke of the right width sitting on the card's edge, as a `Border` would draw it.
- `_clipGeometry` — the outer rounded rect, a geometric clip on `ClipRoot`. It hides the content a
  growing card has not reached yet, and it is what rounds the card off **at rest** too, which is
  why both outlive the animation and `OnContentRootSizeChanged` keeps them current.

Plus: `ShadowCaster` scaled — **a clip does not reach a cast shadow**, which is how the shadow kept
the new size when this was clip-only; `CardRoot` translated `delta / 2 → 0`, because the card is
centred; and `CommandSpace` translated `-delta → 0`, a real element that stays interactive.

The colours are bound through `CompositionColorSource`, which mirrors a XAML `SolidColorBrush` into
a `CompositionColorBrush` and follows its colour. `Background` and `BorderBrush` are also watched
with `RegisterPropertyChangedCallback`, because a theme change swaps the brush instance rather than
its colour, and both are unregistered when the popup goes — the theme brushes outlive it.
`ContentDialogTopOverlaySolid` is not a framework key: it comes from the app's own
`ThemeService.Defaults.cs`, so it is a `SolidColorBrush` and this holds.

**`ContentRoot` carries no `CornerRadius`, and must not.** A non-zero one on an element that has
children clips *those children* to it — `CFrameworkElement::UpdateRequiresCompNodeForRoundedCorners`
says so in as many words, and the hit-test path repeats it. The animation walks `CommandSpace`
below `ContentRoot`'s bottom edge when the card shrinks, and a corner radius there swallows it: the
grow direction looks perfect and the shrink direction loses its button row. That is why the
rounding lives on `ClipRoot`'s clip instead. `ContentRoot` keeps a transparent border of the card's
thickness, so the command row stops short of the stroke rather than covering it.

Guards, all of them ContentPopup's: skip when either height is zero (opening or closing) or
unchanged, and when `VerticalContentAlignment` is `Stretch`, since a card told to fill the window
does not change height by itself.

## Deliberate gaps

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
