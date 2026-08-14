# FormattedTextBlock — test plan

Covers both branches: `formatted-text-block-review` (14 commits) and
`formatted-text-block-dates` (1 commit, stacked on it). Each item says what to send, what to
do, and **what wrong looks like** — the last one matters most, because several of these fail
by a couple of characters rather than by crashing.

Send everything to **Saved Messages**. `ClientEx.ParseMarkdown` runs on send
(`FormattedTextBox.cs:1516`), so the markdown below converts as typed. Dates are the one
exception — there is no markdown for them, see part C.

Composer shortcuts, from `FormattedTextBox.cs:620-650`: mono `Ctrl+Shift+M`, spoiler
`Ctrl+Shift+P`, quote `Ctrl+Shift+.`, link `Ctrl+K`, strikethrough `Ctrl+Shift+X`, plain
`Ctrl+Shift+N`. The formatted date is a flyout item only (calendar icon).

---

## A. Paste-as-markdown

### A1 — copy from a plain paragraph between two code blocks · `ceef6be3f`

The one that fails loudest, and the reason the first branch exists.

    ```csharp
    var before = 1;
    ```
    the middle line, plain text
    ```csharp
    var after = 2;
    ```

- Select **the middle line** by dragging across it, `Ctrl+C`, paste somewhere.
- Double-click a word in it, then triple-click it.
- Search the chat (the in-chat search) for `middle`.

**Wrong:** the paste gives you `var before = 1;` — text from the start of the message, of the
right length. Double-click selects a word from the wrong paragraph. The search highlight lands
somewhere other than on `middle`, or nowhere.
**Right:** you get exactly what you selected.

Worth repeating with a quote instead of the first code block, since quotes take the same path:

    > quoted opening line
    the middle line, plain text
    ```csharp
    var after = 2;
    ```

### A2 — monospace everywhere · `434254d3e`

    Inline `code span` in a sentence.

    ```python
    def f(x):
        return x * 2
    ```

- Check both render in Cascadia Mono.
- Then put `` `code` `` in **your bio** (Settings → Edit profile) and look at your profile —
  that is `ProfileHeader.xaml.cs`, one of the six sites that used to build its own font.
- Emoji inside a code span — `` `code 🙂 span` `` — now renders the emoji instead of a box.
  **That is the deliberate behaviour change**: those six sites gained the font fallback tail.

### A3 — syntax colours survive a theme switch · `bf2d2faf0`

Send the Python block from A2, then switch light/dark in Settings → Appearance.

**Wrong:** colours stay from the old theme, or the block goes uncoloured.

### A4 — recycled runs keep their formatting · `afebb683b`

    **bold** __italic__ ~~strike~~ `mono` and ||spoiler|| in one line

Scroll it out of view and back several times (the run pool is what `ApplyRunProperties` resets).

**Wrong:** after scrolling, a run keeps formatting from a different message — bold text that
should be plain, a leftover font, a stray colour.

### A5 — expandable quote, re-measured · `a2df7ad33`

Send a quote longer than three lines, made expandable (quote via `Ctrl+Shift+.`, then the
expand toggle):

    > line one of a long quotation that should wrap
    > line two of a long quotation that should wrap
    > line three of a long quotation that should wrap
    > line four of a long quotation that should wrap

Resize the window narrow → wide → narrow a few times.

**Wrong:** the expand/collapse affordance disappears or appears when it shouldn't, or stops
matching whether the text is actually clipped.

### A6 — spoiler covered on first render · `90886797c`

    ||a spoiler as the last message in the chat||

Close the chat, open it again, watch the first frame.

**Wrong:** the text is readable for a frame (or permanently) before the cover appears.

### A7 — the I-beam · `9e37111c2`

    some text with a [link](https://telegram.org) in the middle of it

Move the pointer along the line: over text, onto the link, back onto text.

**Wrong:** the cursor stays a hand after leaving the link — that is the regression the flag
clearing guards against, and the reason this is not just "test the flag".

---

## B. Needs scrolling or recycling

### B1 — service messages don't inherit highlights · `2b43daee4`

Find a chat with service messages (someone joined, pinned a message, changed the photo) mixed
in with normal ones, ideally with a spoiler message nearby. Scroll fast enough that containers
recycle, repeatedly.

**Wrong:** a service message shows a spoiler cover or a coloured highlight that belongs to
another message.

### B2 — code block tokenization after recycling · `37ca696e1`

Needs a chat with many fenced code blocks with a language (```` ```csharp ````, ```` ```python ````,
…) — twenty or more. Scroll hard up and down.

**Wrong:** a code block briefly shows another message's syntax-coloured code, or renders
coloured code where the text says something else entirely.

This is a race and may not reproduce on demand; it is the one item where "did not see it" is
weak evidence.

---

## C. Dates — the second branch

There is no markdown for a date entity. Two ways to make one:

1. **The script** — `formatted-text-block-test-messages.py` sends every case below with explicit
   entities. Run it immediately before testing, since the dates are anchored a few seconds
   before send so they reformat while you watch:

       python formatted-text-block-test-messages.py <bot-token> <chat-id>

2. **Composer** — type the text, select the part that should become the date, open the
   formatting flyout, pick the calendar item (**Formatted date**), choose a time and tick
   *relative*. It stores a `tg-time://<unix>?r` link, which becomes the entity on send
   (`TdExtensions.cs:39-50`, `FormattedTextBox.cs:896`).

The Bot API entity, confirmed by experiment:

    {"type": "date_time", "offset": N, "length": L, "unix_time": <int>, "date_time_format": "r"}

`date_time_format` is the grammar in `TdExtensions.cs:32-38` — `r` alone for relative, or `w`
with `d`/`D` and `t`/`T`. Get the field name wrong and it is **silently ignored**, leaving the
format empty, which renders the source text and never updates: it looks like the feature is
broken rather than like the request was.

The relative formatter **counts seconds** — `Formatter.RelativeDateAgo` ends in
`Declension(SecondsAgo, value)` — so the text re-renders every second. Only a change of
*width* moves anything, and those happen here:

| at | from | to | Δ |
|---|---|---|---|
| 2s | `1 second ago` (12) | `2 seconds ago` (13) | **+1** |
| 10s | `9 seconds ago` (13) | `10 seconds ago` (14) | **+1** |
| 60s | `59 seconds ago` (14) | `a minute ago` (12) | **−2** |
| 2min | `a minute ago` (12) | `2 minutes ago` (13) | **+1** |
| 10min | `9 minutes ago` (13) | `10 minutes ago` (14) | **+1** |
| 1h | `59 minutes ago` (14) | `an hour ago` (11) | **−3** |

The script anchors most dates **8 seconds** old, which gives three width changes inside two
minutes — at roughly +2s, +52s and +112s from send — with the counter ticking visibly in
between. T11 carries a second date at 9m50s (crosses at +10s) and T12 one at 59m50s
(crosses at +10s), so have the chat open when you run it.

**A spoiler cannot overlap a date.** Sending one that does, the server splits the spoiler
around the date rather than rejecting the message — `spoiler("A ") date("X") spoiler(" B")`.
So "a date inside a spoiler" is not a state the app can be given, and T10 tests the split
instead, which is the better case anyway: one cover in front of the date that must not move,
and one behind it that must.

### C1 — a date before a spoiler

Text: `Meeting X and the answer is ||42||`, with `X` made a relative date.

Watch across the tick.

**Wrong:** after the text reformats, the blur no longer sits exactly over `42` — it slides a
couple of characters off, or the particles and the hidden text disagree.

### C2 / T10 — a spoiler split around a date

Text: `the answer is A X B and that is all`, spoiler over `A X B`, `X` a relative date. The
server stores this as two spoilers with the date between them.

The sharpest one, because the two covers have to do *different* things across the same tick.

**Wrong:** after the date reformats, the second cover no longer sits over `B` — it lags by the
length change. Or the first cover moves, which it must not. Before the branch, neither moved at
all, so the second one drifted off by two characters a minute.

### C3 — two dates before one spoiler

Text: `From X until Y the answer is ||42||`, with both `X` and `Y` made relative dates.

**Wrong:** the cover drifts further off with each tick, and the two dates fight — this is the
case the old per-date delta could not represent at all.

### C4 — copy after a tick

Text: `X and then the last word`, with `X` a relative date.

Wait for the tick, then select **`the last word`** and copy.

**Wrong:** you get a slice shifted by the length change — `he last word` or `the last wor`.
This one has nothing to do with spoilers; it is the index map, and it is why the dates branch
touches copy at all.

---

## D. Notes from building the set

- The Bot API **does** carry the date entity, as `date_time` with `unix_time` and
  `date_time_format`. Unknown extra fields are accepted and ignored, so a wrong field name
  fails silently — see part C.
- **A spoiler may not overlap a date.** The server splits the spoiler around it. That makes the
  "date inside a spoiler" case unreachable, which in turn means the *stretch* branch of
  `ShiftRanges` is never taken for a date. It is still reachable for the marked and cached
  highlighters, which are built by the app rather than by the server and can span anything.
- **Custom emoji inside a spoiler is still untested.** It is the other case where a spoiler's
  rendered length differs from its source length — one container plus a ZWNJ against however
  many source characters the placeholder has — and it is what the "measure the range from what
  was emitted" change was really written for. Bots can only send `custom_emoji` entities for
  sticker sets they can access, so this one wants a hand-composed message.
- T14 is a deliberate negative: a date entity with an empty format renders its source text and
  never updates. Nothing about it should ever move.

---

## Before you start

Both branches are checked out in worktrees, which **blocks checking them out in the main
tree** — git refuses a branch that is already checked out elsewhere. Either test from
`C:\Source\Telegram.worktrees\…`, or remove the worktrees first:

    git worktree remove C:/Source/Telegram.worktrees/formatted-text-block-review
    git worktree remove C:/Source/Telegram.worktrees/formatted-text-block-dates

Both branches sit on **`origin/develop`**, not on your local `develop` — so they do not include
your unpushed work. To test with it, merge into a scratch branch rather than testing the branch
directly:

    git checkout -b test-ftb develop
    git merge formatted-text-block-dates
