# Message text on the native editor

A plan, first steps built (2026-10-13; see "State" at the end). The goal is to retire `FormattedTextBox` and its subclasses
`ChatTextBox` and `CaptionTextBox`, which are `RichEditBox`es and the app's main source of crashes,
in favour of the native rich editor (`Controls/RichEditor/`, see `native-rich-editor.md`) running
in a restricted mode that reads and writes `FormattedText`. The two must be switchable from the
diagnostics page for as long as the migration lasts.

## Why

The crash this is aimed at, `TextServicesHost::TimerWindowProc`, is inside the RichEdit stack and
cannot be fixed from our code. Our editor takes its input through `CoreTextEditContext` and draws
its own text, so it never creates a `TextServicesHost`.

Plain `TextBox`es stay on RichEdit, and the one full dump we have shows a box with
`AcceptsReturn=false`, which a `TextBox` or a composer that sends on Enter both are. So this plan
should cut that crash, not remove it. The rollout below measures how much it cuts.

Moving off `RichEditBox` also gets rid of some data loss. Today, loading a message loses the
expandable flag on quotes and the language on code blocks. `AllowedEntities` is enforced for
`SetText` and the context menu, but not for the shortcuts, the selection flyout or Alt+X.

## What the current boxes are

The surface was mapped in full when this plan was written; the parts that shape the design are
below.

- **No model.** Formatting is stored as RichEdit character formats, used as markers:
  - bold is `Weight=SemiBold`, monospace is the font name `Consolas` and spoiler is
    `BackgroundColor=Gray`;
  - a quote is a paragraph with particular spacing and a 9pt size;
  - a link is a hidden URL, with `tg-user://` and `tg-time://` for mentions and dates;
  - a custom emoji is hidden `emoji;HEX16` text followed by a visible `` placeholder,
    drawn by a separate `CustomEmojiCanvas`. That canvas only works when the box's
    `DataContext` is a `ViewModelBase`.
- **Callers reach into `Document`.** Autocomplete (`AutocompleteEntityFinder`), the item-click
  handlers in `ChatView`, `SendFilesPopup` and `ChatTextFlyout`, mention insertion,
  `DialogViewModel.SetSelection` and the media drop all take `ITextRange`s. They write the link
  encoding and the emoji encoding themselves.
- **`ChatTextBox` is the composer's controller as well as its box.** It does sending, scheduling,
  the inline bot, the emoji/sticker flyout, the `@ # / :` suggestions, Up-to-edit-last,
  Ctrl+Up/Down to reply, rich HTML paste, media paste and the typing indicator. It reads its
  `DialogViewModel` from `DataContext`.
- **Callers:**

  | Lifetime | Where | Class |
  |---|---|---|
  | Long | `ChatView` composer | `ChatTextBox` |
  | Long | `StoriesWindow`, `StoryLiveInteractionBar`, `GroupCallWindow` | `FormattedTextBox` |
  | Short, with autocomplete | `SendFilesPopup`, `ChooseChatsPopup` | `CaptionTextBox` |
  | Short | `CreatePollPopup`, `CreateChecklistPopup`, `FactCheckPopup`, `SupergroupReactionsPopup` | `FormattedTextBox` |
  | Short | `UserEditPage`, `SendGiftPopup`, `FolderPage`, `BusinessChatLinkPopup` | `CaptionTextBox` |

## Design

### One surface for both implementations

`IFormattedTextInput`, an interface with exactly what callers use and nothing RichEdit-shaped:

- **Text:** `GetFormattedText(clear, parseMarkdown)`, `GetPreparedText`, `SetText(FormattedText)`,
  `ClearText`, `IsEmpty`, `Text` (plain);
- **Inserting:** `InsertText`, `InsertEmoji(Sticker)`, `InsertMention(User)`;
- **Selection:** `SetSelection(start, end)` and `MoveCaretToEnd` in UTF-16 offsets of the plain
  text, as TDLib counts;
- **Autocomplete:** `TryGetQuery(out trigger, out query, out start)`, `ReplaceQuery(start,
  FormattedText)` and `GetCaretRect()`, to place a flyout. The trigger is one of `@ # / :`, or a
  single emoji;
- **Settings:** `AllowedEntities`, `MaxLength`, `PlaceholderText`, `IsReadOnly`,
  `IsReplaceEmojiEnabled`, spell check and prediction;
- **Events:** `Accept`, `TextChanged` (content only), `SelectionChanged`, `PasteRequested` (data
  the box will not take itself: bitmaps, files, rich HTML), `GotFocus`/`LostFocus`;
- **Pen:** `TryCloseHandwritingAsync()`, for the callers that wait for `HandwritingView` today.

`FormattedTextBox` implements it first, over its `Document`, and every caller moves onto it while
the box underneath is still the old one. That step changes no behaviour, and it is where the
RichEdit coupling leaves the callers.

The composer logic leaves `ChatTextBox` for a `ComposerController`, which works on an
`IFormattedTextInput` and the view model. `CaptionTextBox`'s smaller version becomes the same
controller with fewer triggers on. Both boxes then only edit text.

### Message mode in the engine

A `RichSchema` given to the engine, which every path checks: typing, input rules, commands,
shortcuts, paste, drop and `SetText`. That one check is what fixes the gaps in `AllowedEntities`
enforcement.

- **Blocks:** paragraphs; block quotes, with the collapsed flag as an expandable quote; code
  blocks, with a language. Nothing else: no headings, lists, tables, media or pull quotes.
- **Marks:** bold, italic, underline, strikethrough, spoiler, monospace (code).
- **Links:** `Url` (as a text URL), `MentionName` and `DateTime`.
- **Inline objects:** `CustomEmoji` only.
- `AllowedEntities` narrows the schema further: custom emoji only for folder titles, B/I/TextUrl
  for fact checks, and so on.

### FormattedText in and out

A `RichFormattedTextConverter` beside `RichDocumentConverter`.

- **In:** split the text on `\n` into paragraphs. Map quote and code entities onto the lines they
  cover, marks onto runs, and each custom emoji onto one object character. Its alternative text
  is kept on the object.
- **Out:** join the paragraphs with `\n`. Expand each object back to its emoji, so offsets are
  UTF-16 as TDLib counts them. Merge a mark that continues across paragraphs into one entity.
- **Entities that don't sit on line boundaries.** A quote or a `Pre` can start or end mid-line in
  what the server sends. Chosen for now: the entity covers every line it touches, so the text is
  unchanged and what the user sees is what is sent; a code block widened this way takes the rest
  of its line as plain code. Splitting the line instead would add a `\n`. What the server
  accepts is still worth checking; the rule is in one place (`FromFormattedText`).
- **Detected entities** (mentions, hashtags, URLs, bot commands and similar) are dropped on the
  way in, as today, since the server detects them again.
- **Send parity:** `parseMarkdown` keeps running TDLib's `parseMarkdown` on the way out, as
  `FormattedTextBox` does.
- **Known answer.** The round trip in → out must give back the input, normalised, for every
  message in `formatted-text-block-test-messages.py`. It must also match `FormattedTextBox`'s
  output for the same input, except where the old box loses data. That comparison is the
  converter's test.

### The control

`MessageTextBox`, on `RichEditorView`, in a compact presentation:

- **Size:** it grows with its content up to `MaxHeight`, then scrolls. Today the view only
  stretches to fill its window.
- **Look:** no block spacing around a single paragraph, padding from the caller's style, the
  placeholder drawn on the document, and the message brushes. Custom emoji are drawn by the
  editor itself from an `IClientService` the control is given, so no canvas and no `DataContext`
  rule.
- **Enter:** sends when `IsSendByEnterEnabled` is on and no modifier is held, or with Ctrl when it
  is off, gated by a `CanAccept` hook (the inline bot). Shift+Enter inserts a new line.
- **Shortcuts:** the box's current set (Ctrl+B/I/U/K, Ctrl+Shift+X/M/P/N/`.`, Alt+X), with
  `ShortcutsService._reserved` updated to match, including the two chords missing from it today.
- **Paste:**
  - our own `x-tl-field-tags` and plain text are handled in the box;
  - a URL pasted over a selection labels it;
  - everything else raises `PasteRequested`, including HTML with Ctrl+Shift held, which the
    composer offers as a rich message.
- **Copy:** writes tl-tags and plain text, through `MessageHelper.CopyText` as today.
- **Emoticons:** `:)` → emoji as an input rule, behind `IsReplaceEmojiEnabled`.
- **Menus:** a context menu with cut, copy, paste, a Formatting submenu filtered by the schema,
  select all and the spelling suggestions the editor already has. A selection flyout with the
  same buttons as `FormattedTextFlyout`.
- **Input:**
  - `InputScope` and `PreventKeyboardDisplayOnProgrammaticFocus` map onto the
    `CoreTextEditContext`'s input scope and input pane policy;
  - `AutomationProperties.ControlledPeers` points at the suggestion list;
  - Narrator reads it through the existing `ITextProvider2` peer.
- **Length:** `MaxLength` counts what is sent, the UTF-16 length of `GetFormattedText().Text`.
  RichEdit counts its hidden payloads, so today's limit is fuzzier than it looks.
- **Cost:** a composer is typed into constantly, so a keystroke must allocate nothing that grows
  with the document. Creating the control matters too: a popup creates one per opening. Measure
  both against `RichEditBox` before the first switch-over.

## Switching through diagnostics

A process-wide enum in `DiagnosticsSettings`, stored as an int like `MessageDust`:

```csharp
public enum NativeTextInputMode
{
    Off,        // every surface on FormattedTextBox
    Popups,     // the short-lived surfaces
    Secondary,  // and stories, live stories and group call chat
    All,        // and the chat composer
}
```

- **Setting:** `NativeTextInput`, shown as "Native Text Input (restart)" in the Experimental group
  of `DiagnosticsPage`, through the view model's index-to-enum combo.
- **Read once.** It is read once per process into a `static readonly`, as
  `MessageTextBlock._directText` does. Switching a box under a live caret, IME composition and
  undo history is not worth supporting.
- **Choosing per surface.** Each surface declares a `FormattedTextInputHost` with its tier
  (`Tier="Popups"`). On template application the host creates either a `FormattedTextBox` (or
  `CaptionTextBox`) or a `MessageTextBox`, forwards its properties, events and style, and exposes
  the `IFormattedTextInput` the callers already use. The cost is one more element per box, on no
  hot path.
- **Defaults** follow the rollout: `Off` at first, then raised a tier per release for the Store
  build. A user, or we when triaging, can always choose a lower or higher tier on the page.
- **Optional fallback.** If `MessageTextBox` throws out of input handling, set the mode back to
  `Off` for the next launch and log it, as `WatchDog` already does for `LegacyScrollBars`. A user
  should not have to find the diagnostics page to get a working composer back.

## Order of work

1. **The interface on the old box.** Implement `IFormattedTextInput` on `FormattedTextBox`. Move
   every caller off `Document`, and pull `ComposerController` out of `ChatTextBox` and
   `CaptionTextBox`. This is shippable on its own, with no behaviour change. Fix what moving the
   code turns up:
   - `SendFilesPopup`'s query offset ignores hidden characters;
   - `CaptionTextBox.EmojiFlyout_Closed` subscribes again with `+=` instead of unsubscribing;
   - `FormattedTextFlyout` checks `Bold == On`, but bold is applied as a weight, so the button may
     never show as checked.
2. **Message mode and the converter**, with the round-trip test against the known-answer messages.
   The engine harness runs it until the repo has a test project for the editor.
3. **`MessageTextBox`**, the compact view and the box features above, tried in a spike page first.
4. **The host and the diagnostics switch**, with the tiers and `Off` as the default.
5. **`Popups`:**
   - `FactCheckPopup`, `FolderPage`, `BusinessChatLinkPopup`, `UserEditPage` and `SendGiftPopup`
     first: they have no autocomplete;
   - then `CreatePollPopup` and `CreateChecklistPopup`: several boxes in one popup, and the
     checklist uses `IsReadOnly` as a lock;
   - then `SendFilesPopup` and `ChooseChatsPopup`: autocomplete and media paste;
   - `SupergroupReactionsPopup` last, or never: it is an emoji picker shaped like a text box.
6. **`Secondary`:** `StoriesWindow`, `StoryLiveInteractionBar`, `GroupCallWindow`.
7. **`All`:** the `ChatView` composer, together with the rich-draft overlay (`DraftField`), which
   could later become the composer holding a rich message itself.
8. **Removal.** Delete `FormattedTextBox`, `ChatTextBox`, `CaptionTextBox` and the `RichEditBox`
   styles once `All` has been the default for long enough without regressions.

Between tiers, compare the `TimerWindowProc` rate per release against the share of users on each
mode. That tells us whether moving the composer is worth its risk, or whether the popups were
most of it.

## Risks to test early

- **Input:** pen handwriting (there is no `HandwritingView` on our input), dictation,
  reconversion, touch-keyboard prediction and autocorrect, and East Asian IMEs in a box that
  sends on Enter.
- **Screen readers:** Narrator in a box that grows with its text, announcing the suggestion list,
  and the placeholder.
- **The composer's keys:** typing redirected into it from the window (`ChatView.OnCharacterReceived`),
  Up on an empty box to edit the last message, and PageUp/PageDown passing through to the
  message list.

## Open questions

- The rule for entities that don't sit on line boundaries: chosen as above, to confirm against
  what the server does.
- Whether a sent message is checked against `MaxLength` before `parseMarkdown` or after, which is
  where the two boxes could disagree.
- Whether the tiers are enough, or each surface needs its own setting while it is being debugged.

## State

**2026-10-13, overnight, unbuilt.** Steps 2 to 4 for one surface, and the parts of step 1 that
need no caller moved.

- **Message mode** (`RichSchema.cs`): marks, URLs, mentions, dates, custom emoji, quotes and code,
  each allowed or not; `RichSchema.Message` is all of them. `RichEditorEngine.Schema` gates
  `ToggleMark`, `SetLink`, `InsertObject` (an object refused goes in as its text),
  `SetTextKind`, the pull quote, details, lists, tables, media, `InsertBlock` and paste
  (`InsertFragment` conforms the fragment). `Load` conforms what it is given. `Conform` keeps every
  word: a heading becomes a paragraph, a list item gets its bullet or number as text, a table
  row becomes a line with ` | ` between cells, media keeps its caption. With a schema the engine
  runs no markdown input rules: "> " and "- " are how people write in a chat, and markdown is
  parsed on send as before. A quote wrapping a quote joins it instead of nesting.
- **Quotes in a message**: `RichBlockQuote.HasCredit` (false: the credit is no stop and is not
  drawn). Collapsing is the rich editor's own (2026-10-15; until then a message's quote only
  carried a flag and stayed open): a collapsed quote is sent expandable, and an expandable one
  arrives collapsed, its lines joined into one text. One holding code cannot collapse, in either
  editor, so it arrives open and goes out an ordinary quote.
- **Converter** (`RichFormattedTextConverter.cs`): in and out as designed above, plus
  `GetLength`, the sent length without building the text. Tested in the engine harness against
  the 14 messages of `formatted-text-block-test-messages.py`, each normalised (block entities to
  whole lines, marks as maximal runs per line, nothing styled inside code) and compared after one
  round trip and after two. Also hand cases: empty lines, custom emoji with surrogate pairs and a
  skin tone, a mark across a link, code with a language in an expandable quote, adjacent quotes,
  detected entities dropped, mid-line quotes and code, the schema gates, a narrower schema, a
  rich fragment pasted in. 69 new tests; the harness passes 354 of 354. Not done: comparing with
  `FormattedTextBox`'s own output, which needs RichEdit running.
- **View**: `RichEditorView.PlaceholderText` (one hint on an empty document instead of the
  per-line hints); no side inset or bottom gap around root blocks with a schema; HTML pasted as
  its text with a schema, as the old box did; `x-tl-field-tags` pasted through the new
  converter; a quote's credit hidden when it has none.
- **`MessageTextBox`** (`Controls/RichEditor/`): `IFormattedTextInput` over the engine and the
  view, in a text box's frame (`TextControl*` brushes and states, in `RichEditor.xaml`). Enter
  sends as the user's setting has it and raises `Submitted`; otherwise it is a new paragraph, or
  nothing when `AcceptsReturn` is false. Ctrl+Shift+. quotes, Alt+X converts hex. The context
  menu is the old box's, filtered by the schema, with spelling suggestions first. Ctrl+K opens
  `CreateLinkPopup`. `MaxLength` counts what is sent, and the overflow is trimmed before the caret
  on the next dispatcher turn, never inside the input service's callback. Custom emoji are drawn
  with the `IClientService` it is given, or the window's session's. `IsReadOnly` disables the
  editor, since text arrives from the input service past any key handler. Files and bitmaps
  pasted or dropped raise `PasteRequested`. A URL pasted over a selection that is not a URL or a
  link already labels it. An emoticon typed (`:)`) becomes its emoji behind
  `IsReplaceEmojiEnabled`, outside code: only when the change was one more character in the
  same leaf, and on the next dispatcher turn, as the trim is.
- **`IFormattedTextInput`** (`Controls/`), implemented by `FormattedTextBox`, with explicit members
  where its signatures differ (`ContentChanged` is `TextChangedForRealNoCap`; `Submitted` is
  raised beside `Accept`).
- **Switch**: `DiagnosticsSettings.NativeTextInput` (`NativeTextInputMode` stored as an int,
  default `Off`), "Native Text Input (restart)" on the diagnostics page.
  `FormattedTextInputHost` reads it once per process, makes the box for its `Tier` on first use,
  and forwards the interface, so a caller keeps its code.
- **First surface**: `FactCheckPopup`, `Tier="Popups"`. It now confirms on `Submitted` instead of
  a `KeyDown` handler that the old box's own Enter handling kept from running, so Enter confirms
  on both boxes. That is a change with the switch `Off` too. The quiz explanation in
  `CreatePollPopup` is the second, `Tier="Popups"`: it is the one easy to reach, and its caller
  only reads `GetFormattedText()`.
- **Suggestions** (2026-10-14): `AutocompleteEntityFinder.TrySearch(string, caret, objectChar, …)`
  finds every kind (emoji, hashtag, username, command, sticker) in plain text; the RichEdit
  overload is untouched. `RichEditorView.Suggestions` (a `RichSuggestionContext`, set by
  `MessageTextBox` from its session, and by `RichTextWindow` with its chat) offers emoji for a ":query" and custom emoji for a trailing
  emoji, custom emoji only where the schema allows them, in `ChatTextFlyout` through
  `IAutocompleteTarget`. Hashtags, usernames and commands are found but not offered yet: that is
  the composers' work. A pick, like an emoticon, goes in through `RichEditorEngine.Substitute`: the
  run remembers what it replaced (`RichTextRun.Replaced`), never merges, and Backspace after it
  (or Delete before it) gives the typing back, at any later point, as the old box's hidden
  emoticon did.
- **Composer, step 1** (2026-10-14, on the old box only): `ComposerController`
  (`Controls/Chats/`) holds sending, scheduling and the effect, the inline bot, the `@ # /` lists
  and their picks (and the sticker list for a message that is one emoji), the commands menu, the
  typing indicator, Up to edit, Ctrl+Up/Down to reply, and pasting files, pictures and
  Ctrl+Shift+V HTML. It reads keys on `PreviewKeyDown`, which tunnels, and stands aside while the
  box's own suggestions are open (`HasOpenSuggestions`). `ChatTextBox` keeps what is RichEdit's:
  the `:` and custom emoji flyout, Up/Down to the ends, a space ending formatting, the inline
  placeholder's drawing. `IFormattedTextInput` gained the query (`TryGetQuery`/`ReplaceQuery`,
  an ordinary replacement), mentions, formatted inserts, the caret (`CaretOffset`,
  `IsCaretAtStart`/`End`, `MoveCaretToEnd`, `SplitAtSelection`), `IsSubmitEnabled`,
  `SetInlinePlaceholder`, spell check and prediction, and the `SelectionChanged` and
  `PasteRequested` events; `MessageTextBox` and the host implement all of it.
  `DialogViewModel.TextField` is the interface, and nothing outside the boxes touches `Document`.
  Fixed on the way: the typing indicator now goes out on the first character after an empty box;
  clearing the inline bot resets its loading flag; the pending inline bot query is cancelled when
  the page leaves the chat; `DialogViewModel.SetSelection`, which nothing called, is gone. Changed:
  a dropped link or text replaces the selection and moves the caret after it, as a paste does,
  where it used to go in after the selection with the caret left in front.
- **Composer, step 2**: ChatView declares `FormattedTextInputHost Kind="Composer" Tier="All"`,
  with `LegacyStyle` (ChatView's `ComposerLegacyTextBoxStyle`, the old box's attributes) and
  `NativeStyle` (`MessageTextBoxComposerStyle` in `RichEditor.xaml`: no frame, no states, the old
  composer's 48,13,0,15 inset, the inline hint over the text). The composer kind makes a
  `ChatTextBox` when the switch is below `All`, and gives the native box `AcceptsReturn` so
  Shift+Enter is a new line. ChatView gives the old box its `ControlledList`, the native one its
  client and chat id, listens for taps with `handledEventsToo`, and asks the host for the box's
  `FocusTarget` and `InputFocusState`, since the host takes no focus. `RichEditorView` now marks
  `CharacterReceived` handled, as a text box does: without that, ChatView's type-to-composer
  inserted every character a second time. The native editor counts as a text box in ChatView's
  focus checks and in `TextSelectionManager`. Not mapped yet on the native box: `InputScope` and
  `PreventKeyboardDisplayOnProgrammaticFocus`, the composer's `FontFamily` (the leaves keep
  theirs), and the list as its automation `ControlledPeers`.
- **Old-box fixes from step 1**: `CaptionTextBox.EmojiFlyout_Closed` subscribed again with `+=`,
  so every closed flyout stayed referenced; `FormattedTextFlyout` read `Bold`, which is never set
  because bold is applied as a weight. The `SendFilesPopup` offset is not touched: that file has
  uncommitted work.

Checked: the engine harness (354 pass); the view harness against the Modern `Telegram.dll` from
2026-09-15, whose only errors are what that dll predates (new strings, `RichTextTonAddress`,
`MessageHelper.SetTextAsync`, `DirectTextBlock.LineCount` and `HyperlinkColorChanged`, the new
setting, `FormattedTextBox` without the interface) and `ClientEx` being internal to another
assembly. `FormattedTextBox.cs` was compiled the same way for its interface members.

To test first:
- Diagnostics, "Native Text Input", Popups, restart; then a new poll, Quiz mode, the explanation
  field: typing, bold and italic by keys and from the menu, Ctrl+K, a URL pasted over a word,
  `:)`, Shift+Enter and Enter, the placeholder, Narrator; send it and check the explanation as
  received. A fact check on a channel post adds Enter to confirm and the 1024 limit. Then Off
  again, and the same fields on the old box.
- Dark theme and high contrast: the frame's states, the caret, the selection.
- An IME (Japanese, Chinese) in the box, and the touch keyboard.

Tracked, not ours: Chinese testers (2026-10-14) report the text moving down while a composition
holds Latin letters and back up once they convert to Chinese characters. Stock `TextBox` and
`RichEditBox` do the same, so it is the IME's composition string's font metrics, not the editor.
Worth looking at once the rest is done: a fixed line height in the leaves would hide it.

Right to left (2026-10-14): each line runs the way its first strong character does
(`NativeUtils.GetDirectionality` in `RichLeafView.Render`, handed to `DirectTextBlock.SetText`); a
line with none follows the editor's `FlowDirection`. An article's direction is its first strong
line's (`RichEditorView.UpdateDocumentDirection`): it sets `RichDocument.IsRtl`, which is sent,
and mirrors the editor as an instant view mirrors. A message's text keeps the app's direction.
The leaves' text blocks, formulas and media frames are pinned left to right, since they draw into
surfaces that a mirrored parent would show mirrored; `RichLeafView` converts across that one flip,
so everything else, which shares the leaf view's direction, reads its coordinates straight. The
arrows ask the line (`DirectTextBlock.IsRightToLeft`), and Home and End go to the line's logical
start and end (`RichLeafView.GetLineEdge`), which they did not for an RTL line before.

Every surface (2026-10-15): steps 5 and 6, and the box's open gaps.
- **Host:** a `Caption` kind (`CaptionTextBox` on the old side). `Padding`, `Background`,
  `BorderBrush`, `BorderThickness`, `CornerRadius`, `FontFamily`, `FontSize` and `MinHeight` set on
  the host go to the box, and so do later changes to them, theme ones included. `MinHeight` goes too
  because the box's style has its own minimum, which would hold it taller than a host of
  `Height="0"`. `InputScope` (a name, mapped to the edit context's scope),
  `PreventKeyboardDisplayOnProgrammaticFocus`, `CustomEmoji` (the old box's canvas; the native box
  draws its own), `ViewModel` (the session and chat that emoji are suggested for) and
  `ControlledList` (the page's list, as the box's automation controlled peer, on both boxes).
  `DefaultChatMessageTextBoxStyle` in `CommonStyles.xaml` is `DefaultChatTextBoxStyle` for the
  native box.
- **Surfaces:** `FolderPage`, `UserEditPage`, `BusinessChatLinkPopup` and `SendGiftPopup`; the
  poll's question, description and options; the checklist's title, tasks and new task;
  `SendFilesPopup` and `ChooseChatsPopup`. Those are all on `Tier="Popups"`. `StoriesWindow`,
  `StoryLiveInteractionBar` and `GroupCallWindow` are on `Secondary`. Callers moved onto
  `ContentChanged`, `Submitted`, `FocusInput` and `MoveCaretToEnd`. The poll and the checklist
  find the host from the focused element, since what has focus is the box inside it.
- **Captions:** `CaptionController` (`Controls/Chats/`) holds the mentions under a caption: the
  list, its keys on `PreviewKeyDown` and the pick. It works on either box, as the composer's does.
  `CaptionTextBox` keeps only its emoji flyout and the space rule; `IViewWithAutocomplete` moved
  with the controller and lost `Accept`, since the page now listens to `Submitted`. Media pasted
  or dropped comes through `PasteRequested`; with text among it, the text is pasted, as before.
  The `HandwritingView` workaround is gone from both popups.
- **Code language:** `CodeLanguagePopup` (an `AutoSuggestBox`, as `CountryBox` is), for the
  native box and the rich editor alike, in place of the rich editor's flyout. It offers the
  highlighter's languages by name or id and takes any other, as printable ASCII without spaces or
  a backquote: that is what TDLib keeps when it writes a language into markdown. The 32-character
  limit is ours, since TDLib sets none. A name typed in full is sent as its id, and an empty box
  means no language.
- **Native box:** Formatted date in the menu (`ChooseDateTimeToast.ChooseDateUrlAsync`, which the
  old box now uses too); `PreventKeyboardDisplayOnProgrammaticFocus`, with a tap raising the
  keyboard; the input scope; the disabled state following `IsEnabled`. `SetText` skips text the box
  already holds, raw or with markdown parsed: `FolderPage` writes the title back on every change,
  and reloading would move the caret and end a composition. A `SetText` or `ClearText` made from
  inside `ContentChanged` waits for the next dispatcher turn: the checklist turns what is typed
  into a task there, inside the input service's callback.
- **Fallback:** an exception leaving the edit context's text, selection or format handlers, or the
  editor's key handling, sets the switch to `Off` for the next launch. A filter notes it and lets
  it go on, so the crash report keeps its stack. Release builds have no method names to tell
  where an exception came from, so `WatchDog` could not do it.
- **Quotes:** a message's quote collapses exactly as a rich message's does (see "Quotes in a
  message" above).
- **Not RichEdit any more:** `TextEditorPopup`'s AI fix result, a read-only `RichEditBox`, is a
  `FormattedTextBlock`. Deletions are struck through and insertions underlined, where the old box
  drew every change with a wavy underline.
- **Left on RichEdit:** `SupergroupReactionsPopup`. It is an emoji picker shaped like a text box
  and blocks typing by handling keys, but the native box's text arrives from the input service,
  past any key handler.
- Up and Down to the ends already worked: past the first or last line the editor goes to the
  document's edge.

To test first: each surface on Off and on its tier.
- `StoryLiveInteractionBar` and `GroupCallWindow` declare their field with `Height="0"`, and
  nothing changes that height; the old box shows anyway, and how is not known. Check the native
  one is visible there.
- The checklist's new task box: type into it, which makes a task and moves focus.
- Mentions in a caption, with the arrows and Enter.
- The keyboard staying down when a story or chat opens, and coming up on a tap.

Not done yet, in the order they matter:
- The composer's `FontFamily`: the leaves draw with the message font.
- The cost of a keystroke and of making the control, measured against `RichEditBox`.
- Removal (step 8): `UsernameCollection`, `EmojiCollection` and `SearchHashtagsCollection` are
  nested in `ChatTextBox` and used by both controllers and the editor. They move out first.
  `ChatTextFlyout` and `FormattedTextFlyout` are built on `FormattedTextBox`, and the
  `RichEditBox` styles go last.
