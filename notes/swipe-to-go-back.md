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

The indicator is `ChatListListView`'s, on its numbers: `ArrowLeft.png` in a 30px circle,
in from the edge over 55px, growing `0.8 → 1`, fading the whole way, and mirrored on X —
that asset is drawn for the right-hand edge, and `ChatListListView` flips it the same way
for the indicator it brings in from the left. Every property is one expression on the
tracker, so nothing runs per frame on the UI thread.

Two attempts to make it more interesting were rejected, and the second is worth recording
so it is not tried again: an elastic pill drawn out of the edge that stretched as it was
pulled and snapped to a circle at the threshold. It read as nasty in motion. **This gesture
wants the app's existing vocabulary, not a new one.**

What it does have over the original is the fill. The circle is a `SpriteVisual` painted
with `SolidGaussianBrush`, so it is frosted over whatever is behind it rather than a flat
40%-alpha grey — applied here and to the `ChatListListView` and `MessageSelector`
indicators, which are the same 30px circle.

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
- [x] Indicator: two redesigns tried and both rejected, ending back on
      `ChatListListView`'s, mirrored, with a `SolidGaussianBrush` fill. Unverified.
- [x] `SolidGaussianBrush` grew a composition-side factory, so the same frosting is on the
      `ChatListListView` and `MessageSelector` indicators too. A shape fill cannot carry an
      effect brush — it takes colour and gradient brushes only — so the circle became a
      mask over the effect, drawn through a `CompositionVisualSurface` to keep the
      antialiasing a geometric clip would have thrown away.
- [x] Fixed *most* settings pages, where the gesture only worked in the title strip, by
      adding a source inside the scrolling host.
- [x] Fixed `SettingsAdvancedPage` and `SettingsPowerSavingPage`, which failed whenever
      their content did not overflow: `ScrollMode.Auto` turns the manipulation off when
      there is nothing to scroll. **Confirmed working by Fela.**
- [x] Commit navigates with `SlideNavigationTransitionInfo`. The effect is `FromRight` —
      the frame inverts it on a back navigation, so that is the value that slides the
      uncovered page in from the left, and it is the same one `TLNavigationService` passes
      going forward. Gated on `PowerSavingPolicy.AreSmoothTransitionsEnabled`, since
      `FrameFacade` applies that policy to `Navigate` but not to `GoBack`.

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

## The scrolling host takes the pan

Reported first as "fails on the power saving page", which sent me looking for something
peculiar to that page — it has the only large `IsEnabled`-bound region in the settings, and
that was the wrong lead. Power saving was off, so nothing on it was disabled. What settled
it was the rest of the report: it failed on **Advanced** too, and on both pages it worked in
the *title strip* but nowhere in the page itself.

That is the whole diagnosis. Every settings page puts its content in
`ScrollViewer x:Name="ScrollingHost"`, and a vertical `ScrollViewer` takes the touchpad pan
before any ancestor sees it. `DetailRoot`'s source is an ancestor of the page, so it only
ever got the gesture where no scroller sat in the way — the header. The chat history was
never evidence to the contrary: what works there is `MessageSelector`'s source, which is a
*descendant* of the scroller and so wins the contact.

Fix is the same trick, applied deliberately: on navigation, `ConfigureBackGestureContent`
puts a second `VisualInteractionSource` on the scrolling host's own content element and adds
it to the same tracker. Rails and an X-only source mode leave vertical panning to the
`ScrollViewer`, exactly as they already do in the chat history.

That fixed the long pages and nothing else, and everything after it was wrong. Recorded
here so nobody spends the same rounds again.

**Cause, finally.** Fela settled it in one observation: the gesture works on Advanced and
Power Saving *if the window is made small enough for the scroller to be scrollable*.

`ScrollMode.Auto` turns the manipulation off entirely when there is nothing to scroll, and
a touchpad pan over a non-manipulable element is never classified as a manipulation — so no
`VisualInteractionSource` is consulted anywhere up the tree and the gesture is simply
absent, which is why no chip ever appeared. When the content does overflow, the
manipulation is live, the vertical rails reject the horizontal component, and it falls
through to the source.

Every one of these pages declares `VerticalScrollMode="Auto"`. The only difference between
Privacy and Advanced was whether the content happened to overflow — the markup was never
the variable, the window size was. `ConfigureBackGestureContent` now forces
`ScrollMode.Enabled` on the host and restores the declared value on the way out. It costs
nothing: there is still no extent, so nothing becomes scrollable that was not.

Three earlier theories, all wrong, all disproved by evidence Fela supplied. What they ruled
out, so nobody spends the rounds again:

- *The page's XAML.* Advanced, Power Saving, Privacy and Data & Storage declare
  byte-identical scrollers — `ScrollViewer x:Name="ScrollingHost"` with
  `VerticalScrollBarVisibility="Auto"` and `VerticalScrollMode="Auto"` — wrapping the same
  `SettingsPanel`. Two of those work and two do not. Whatever the difference is, it is not
  in the markup.
- *A scroller always eating the pan.* `SettingsStickersPage` works and its host is a
  `TableListView`, which `ConfigureBackGestureContent` skips entirely. An ancestor source is
  evidently enough there.
- *Short content, and hit-testing over empty areas.* Both were tried — `MinHeight` from
  `ViewportHeight`, and `Background = Transparent` on the bare `SettingsPanel`, which really
  does not hit-test its own area otherwise. Neither changed anything, and Fela reports the
  gesture failing over text and buttons too, not only empty space. Both reverted; do not
  re-add them without new evidence.
- *`x:Load` and `NavigationMode`.* Privacy has four `x:Load` and works, Advanced has one and
  fails; every page here is `NavigationMode="Root"` bar Stickers.

`_backContentSource` is kept, still unproven. It was added under the scroller theory, and
Stickers — which works with no source of its own — casts doubt on whether it earns anything
now that the scroll mode is the known cause. Worth deleting to see if anything changes; do
not treat it as load-bearing.

A `ListViewBase` host still gets neither the source nor the scroll-mode change. Those are
scrollable in practice, so it may never matter, but it is the remaining known gap.

Only `ScrollViewer` hosts are covered. A `ListViewBase` host would need its
`ItemsPanelRoot`, which does not exist until the list realises, and the one that matters —
the chat history — is already served by `MessageSelector`. So a list-backed page that is
not the chat history is still expected to fail; none has been reported yet.

Worth knowing while working here: `MasterDetailView.OnNavigating` exists but is **never
subscribed**. Anything that needs to run as a page is left has to go in `OnNavigated` for
the next one instead, which is why `ConfigureBackGestureContent` drops the old source itself
rather than relying on a teardown hook.

## State

Working, on Fela's build, across the chat history, profile and settings — including the
pages that used to fail — with the `FromRight` slide on commit. Nothing below is a defect.

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
