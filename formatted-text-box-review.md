# FormattedTextBox / ChatTextBox review

Read-through of `Telegram/Controls/FormattedTextBox.cs` and
`Telegram/Controls/Chats/ChatTextBox.cs` after the typing/pasting performance work.
Line numbers are against the working tree at the time of writing.

Everything below is unfixed. Check an item off in the same commit as its fix.

## Bugs

- [x] **`_updateLocked` stays set if `GetFormattedText` throws** — it set the field and cleared it
      with the whole TOM walk in between and no `try`/`finally`. One exception disabled
      `UpdateCustomEmoji` for the lifetime of the control (it early-returns on `_updateLocked`),
      so custom emoji stopped rendering in the box with no other symptom and no way back. The
      same went for `BatchDisplayUpdates`, which left the editor frozen. Split into
      `GetFormattedTextImpl` the way `SetText` already was, with both undone in a `finally`.

- [x] **`_undoGroup` drifts if anything between `BeginUndoGroup` and `EndUndoGroup` throws** —
      eight pairs, none of them in a `try`/`finally`. Once the counter was stuck above zero,
      `OnTextChanging` stopped calling `UpdateFormat` permanently (it's gated on
      `_undoGroup == 0`), so blockquote font sizes silently stopped being normalized.
      `BeginUndoGroup` returns a disposable scope now, so the pairing can't be skipped.

- [ ] **`CharacterReceived` can be subscribed more than once** — `FormattedTextBox.cs:294`
      subscribes in `Loaded` and `:299` unsubscribes in `Unloaded`. `Loaded` can fire again
      without an intervening `Unloaded` (re-parenting), and `CoreWindow` lives for the whole
      session, so a second subscription both replaces the emoticon twice per keystroke and pins
      the control until the app exits. This is the case the project rules single out. Guard the
      subscription with a flag.

- [ ] **`Text` includes hidden text** — `FormattedTextBox.cs:1249` reads with
      `TextGetOptions.None`, and hidden runs are only dropped by `NoHidden`. The property
      therefore returns custom emoji metadata (`<emoji>;00000000ABCDEF01`) and hyperlink URLs
      interleaved with the visible text. Its one consumer is `ChatView.CheckMessageBoxEmpty`,
      which feeds it to `getLinkPreview`. Carrying the hyperlink URLs may well be deliberate —
      it's the only way a text-url entity gets a preview — but the emoji metadata isn't.
      Decide which, then either switch to `NoHidden` or write down why not.

## Performance

- [ ] **`DateTime.Now` twice per keystroke** — `ChatTextBox.cs:336` and `:339`, in the typing
      indicator. `Logger.cs` already documents that `Now` is expensive next to `UtcNow` (it
      resolves the time zone on every call). `_lastKeystroke` is only ever compared against
      itself, so `UtcNow` is a straight swap.

- [ ] **`LoadQuickReplyShortcuts` is sent per keystroke while typing a command** —
      `ChatTextBox.cs:669`. `GetCommands` runs for every new query that doesn't match the
      previous `AutocompleteList`, so typing `/start` can send six identical requests. The
      `// TODO: is this actually needed?` sitting on the line is the same question — answer it,
      and if it is needed, send it once per chat rather than once per character.

- [ ] **The whole draft is materialized on every selection change** — `ChatTextBox.cs:391`.
      This runs on every keystroke *and* every caret move. Only three paths need the string: the
      inline-bot search (already skipped when there's no inline bot), the sticker branch of
      `TryGetAutocomplete`, and `SearchByInlineBot` — which reads only up to the first space and
      only matters when the message starts with `@`, something a one-character range read can
      answer. Defer materializing it to those branches.

- [ ] **`UsernameCollection` re-queries `GetTopChats` for every autocomplete query** —
      `ChatTextBox.cs:719` and `:736`. Two round trips per `@` query; the top-chat lists barely
      move within a session.

- [ ] **A `CancellationTokenSource` per keystroke on the autocomplete path** —
      `ChatTextBox.cs:365`, `:429`, `:558`. Each assignment drops the previous instance without
      disposing it unless it happened to go through `CancelInlineBotToken`.

## Cleanup

- [ ] **`ChatTextBox` redeclares `ContentElement`** — `ChatTextBox.cs:45` shadows
      `FormattedTextBox.cs:74`. Two fields and two `GetTemplateChild` calls for one template
      part. Make the base field `protected` and drop the derived one.

- [ ] **`UpdatePadding` is an unfinished experiment** — `FormattedTextBox.cs:213`-`:259`, marked
      as one, reachable only from the `FooterSize` setter. The scale-factor fallback its comment
      describes doesn't exist: `:228` assigns `1.0` and the next line overwrites it inside a bare
      block that dereferences `XamlRoot` unguarded. Finish it or delete it.

- [ ] **`HandwritingView.Unloaded` is subscribed with a local function** —
      `FormattedTextBox.cs:412`-`:415`. It does unsubscribe itself, and delegate equality makes
      that work, but it's the pattern the project rules forbid, and if `TryClose()` doesn't raise
      `Unloaded` the handler stays subscribed with its captured state.

- [ ] **`ContentElement.ViewChanged` is subscribed on every `OnApplyTemplate`** —
      `FormattedTextBox.cs:271`, never removed, so a second template application doubles the
      handler. The same method null-checks `Blocks` and `CustomEmoji` but not `ContentElement`.

- [ ] **Check that `MergeParagraphs` always advances** — `FormattedTextBox.cs:1961`. The `while`
      body does nothing when `searchRange.StartPosition <= range.StartPosition`, and nothing else
      moves the range, so termination rests entirely on `FindText` continuing past its own match.

## Looked at, deliberately left alone

- `IsLongerThanMaxLength` returns `exceeding = length` when `IsReadOnly`
  (`FormattedTextBox.cs:1850`), so callers that truncate to `exceeding` insert the *whole* string
  instead of nothing — a paste into a read-only box would go through. Nothing sets `IsReadOnly`
  on a `FormattedTextBox` today, so it's unreachable; fix it if the property ever gets used.

- `IsCustomEmoji` mutates the range it is handed (`range.EndPosition -= ...`) and its callers
  depend on that. It reads as a query and isn't one, but the behaviour is load-bearing.

## Landed already

Fixed while investigating; listed so the file reads as the current state:

- The three per-keystroke document walks (`UpdateFormat`, `UpdateBlocks`, `UpdateCustomEmoji`)
  now start from one whole-document uniformity probe, and the ranges they walk with are reused
  rather than reallocated.
- `UpdateBlocks` removed stale blockquote decorations with an index loop that skipped every
  other element.
- `CustomEmojiCanvas.UpdateEntities` recreated every `CustomEmojiFileSource` on every keystroke,
  each one firing a `getCustomEmojiStickers` request from its constructor.
- `OnCharacterReceived` allocated a byte array, a string and a closure per character typed.
- `SearchInlineBotResults` split the entire message on spaces per keystroke.
- `ChatView` asked TDLib for a link preview on every keystroke, whatever the text.
- `IsValidUrl` handed the whole string to the entity parser before checking it could be a URL.
- Pasting applied entities through one COM range per entity, and inserted without batching
  display updates.
- Pasting a URL over a selection went through `SetText`, and turned an already-linked selection
  into the label of a link to the new URL.
- The composer's font family led with a packaged emoji font, which cost about a millisecond per
  word to resolve — a 17,000 character paste froze the UI for 2.7 seconds. See
  `Telegram/Common/Theme.cs`.
