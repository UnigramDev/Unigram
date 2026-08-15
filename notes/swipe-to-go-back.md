# Swipe to go back

A Chrome-style back gesture for the detail pane of `MasterDetailView`: a horizontal
left-to-right swipe pulls a circular back chip in from the left edge, and past a 72px
threshold it navigates back. The content does not move.

Branch `worktree-swipe-to-go-back`, worktree `.claude/worktrees/swipe-to-go-back`.

## Why it looks like this

Chrome on the desktop animates only the chip, never the page. Copying that decides most
of the design for us:

- No master reveal, so `MasterFrame` stays collapsed and there is no measure/arrange of
  the whole chat list at the moment the gesture starts.
- No z-order inversion of `MasterDetailPanel`'s children, which paint master over detail.
- No difference between `BackStackDepth == 1` and deeper, because nothing is being
  scrubbed. Frame navigation is not interruptible, but we never ask it to be: the chip is
  a progress indicator and the commit is a plain `GoBack()`.

The chip itself is `ChatListListView`'s indicator recipe — `ArrowLeft.png` in a 30px
circle of `MessageServiceBackgroundColor`, sliding `-30 → 25`, scaling `0.8 → 1.0`,
opacity `0 → 1` over 72px. Same asset, same vocabulary as the folder-switch gesture.
Unlike that one the arrow is not mirrored: `ArrowLeft.png` already points the right way
for a left-edge back chip.

## The gesture arbitration problem

`MessageSelector` already owns a horizontal `InteractionTracker` over every message
bubble, and `PositionXChainingMode = Never` means nothing overflows to an ancestor. For a
precision touchpad, `ManipulationRedirectionMode = CapableTouchpadOnly` redirects
automatically to the innermost `VisualInteractionSource` under the cursor — there is no
`PointerPressed` to intercept and no position to gate on, so an edge zone is meaningless
for the primary input device. A `MasterDetailView`-level tracker simply cannot win over a
bubble.

So it does not try. **`MasterDetailView` owns the chip; whichever tracker owns the gesture
drives it.** An `ExpressionAnimation` can reference a tracker anywhere in the same
compositor, and every chip property is a pure function of `tracker.Position.X`, so the
binding is made once per gesture and then runs entirely on the compositor — no per-frame
UI-thread work, unlike the `OnValuesChanged` callbacks the two existing indicators use.

There are two sources, feeding one chip:

- `MessageSelector`'s existing tracker, over message bubbles.
- A new source on `DetailRoot`, for everywhere else in the detail: the chat header, the
  composer, `ProfilePage`, settings pages, an empty chat.

`MessageSelector`'s offset expression already returns 0 translation for a negative
position when `CanShare` is false, so the bubble stays still while the tracker travels —
which is exactly the Chrome behaviour, and it was already there. The only thing stopping
it was `MinPosition` being pinned to 0.

## Decisions taken

- **One new bool, `SwipeToGoBack`**, beside the existing `SwipeToShare` / `SwipeToReply`
  checkboxes under *Swipe Actions*. Not a three-way choice for the left-to-right
  direction: that reads better on paper but rebuilds that section of the settings page.
- **Precedence**: over a message bubble, left-to-right forwards if `SwipeToShare` is on
  and the message can be forwarded, otherwise goes back. Anywhere else in the detail it
  goes back. So there is no state where a setting is on but silently does nothing — a
  user who wants back everywhere unchecks *Share*.
- **Enabled in wide mode too**, not just `Minimal`. `CanGoBack` is already
  `DetailFrame.CanGoBack` with no state test (there is a comment at `MasterDetailView.cs`
  recording that wide-state back navigation was deliberately fixed), and wide is the most
  common mode.
- **RTL is ignored for now**, matching `ChatListListView` and `MessageSelector`, neither
  of which consults `FlowDirection`.
- The dead `GoBack()` branch in `MessageSelector.OnInertiaStateEntered` becomes live
  rather than being deleted — it was reaching for this, as was the commented-out
  `master?.ConfigureAnimations(_tracker)` two methods below it.

## Progress

- [x] `SwipeToGoBack` in `SettingsService` + `SettingsAppearanceViewModel` + the checkbox
- [x] `SwipeGoBack` string in `Resources.xml`. `{CustomResource}` is a runtime lookup
      through `XamlResourceLoader`, so this does not hold up the build — the checkbox is
      simply unlabelled until the generator runs.
- [x] Chip + `AttachBackGesture` / `DetachBackGesture` / `CommitBackGesture` on `MasterDetailView`
- [x] `MasterDetailView`'s own `VisualInteractionSource` on `DetailRoot`
- [x] `MessageSelector`: open `MinPosition`, attach on interacting, commit on inertia
- [x] Built and run by Fela. Works in the chat history and on other screens; vertical
      scrolling is not starved, so the shared-source worry was unfounded.
- [x] Chip made more emphatic on his feedback: 40px rather than 30, travels the full
      `-40 → 36`, swings upright from -30°, and latches into an *armed* state at the
      threshold — scale pops to 1.15 and opacity goes to full — so releasing feels
      committed. All still pure expression, no per-frame work.
- [ ] **Fails completely on the power saving settings page.** See below.

## Touch is deliberately only half-covered

`CapableTouchpadOnly` means touch reaches a source *only* through an explicit
`TryRedirectForManipulation` on `PointerPressed`. `MessageSelector` already does that, so
touch gets the gesture over message bubbles for free. `MasterDetailView` deliberately does
**not** add its own handler: it would have to sit on the whole detail with
`handledEventsToo`, and redirecting every touch press there would take the contact away
from text boxes in the composer and from vertical `ScrollViewer`s on profile and settings
pages. Not worth the regression for an input device that is a small minority here.

So on a touchscreen the gesture works over the chat history and nowhere else. On a
trackpad — the actual target — it works everywhere, because touchpad panning is redirected
automatically by hit-test with no pointer handling involved.

## The power saving page

Fails there and only there. Static comparison rules out the obvious: it is a plain
`HostedPage` with `NavigationMode="Root"`, a vertical `ScrollingHost` and a
`SettingsPanel`, navigated by `Navigate(typeof(SettingsPowerSavingPage))` exactly like
`SettingsDataAndStoragePage`. No `SwipeControl`, no `ManipulationMode`, no interaction
tracker of its own — `SettingsExpander` is pure keyframe animation and does not touch
input.

The one structural thing that page has and the working ones do not:

```xml
<controls:HeaderedControl Header="{CustomResource LiteOptionsTitle}"
                          IsEnabled="{x:Bind ViewModel.IsAutoDisabled, Mode=OneWay}">
```

`IsAutoDisabled` is `PowerSavingPolicy.Status == PowerSavingStatus.Off`, so with power
saving **on** that `HeaderedControl` — which is nearly the whole page — is disabled. A
disabled subtree still hit-tests, but input over it may be dropped outright rather than
walking up to an ancestor `VisualInteractionSource`, which would leave the `DetailRoot`
tracker never seeing the manipulation.

Unverified. The test that settles it: with power saving on, swipe over the top *Auto*
checkbox panel, which stays enabled, and then over the disabled options below. If only the
top strip works, it is the disabled subtree; if neither works, it is something else and
the `IsEnabled` lead is a red herring.

If it is confirmed, the source cannot simply be moved — the fix is either a transparent
input overlay above the page content, or accepting that disabled regions do not carry the
gesture.

## Open

- Default value of `SwipeToGoBack`. Currently `true`, which changes what a left-to-right
  swipe does for existing users who had *Share* switched off. `false` is the conservative
  choice but leaves the feature undiscovered.
- Turning the setting **off** takes effect at once (re-read on every navigation, and again
  in `AttachBackGesture`). Turning it **on** only reaches `MessageSelector` containers
  prepared afterwards, since `OnLoaded` decides there whether to build a tracker at all —
  the same limitation `SwipeToShare` and `SwipeToReply` already have.
- The chip is vertically centred on `DetailRoot`, which spans the header row. If it reads
  as too low next to Chrome's, offset it by the 48px header.

## Also noticed, not touched

`MasterDetailPanel.ActualMasterWidth` reads `Children[2]` (the grip) and
`ActualDetailWidth` reads `Children[1]` (the master) — the indices are crossed against the
order `ArrangeOverride` uses. Unrelated to this branch, left alone.
