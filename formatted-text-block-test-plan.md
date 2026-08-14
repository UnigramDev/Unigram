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

1. **Composer** — type the text, select the part that should become the date, open the
   formatting flyout, pick the calendar item (**Formatted date**), choose a time and tick
   *relative*. It stores a `tg-time://<unix>?r` link, which becomes the entity on send
   (`TdExtensions.cs:39-50`, `FormattedTextBox.cs:896`).
2. **A bot**, if the entity survives the Bot API — see part D.

Pick a timestamp **about 55 seconds in the past** for each of these. Within a minute the text
goes from `55 seconds ago` to `1 minute ago`, i.e. **14 characters to 12**, and it is that
length change that every one of these bugs rides on. `59 minutes ago` → `1 hour ago` works too
if you want a slower one.

**First, confirm the entity survives the round trip.** Send one date-only message to Saved
Messages. If the bubble shows a live relative date that reformats on its own, the round trip
works and the rest of part C is testable. If it comes back as plain text or as a link, stop —
the read view never sees a date entity that way, and the bot route is the only option.

### C1 — a date before a spoiler

Text: `Meeting X and the answer is ||42||`, with `X` made a relative date.

Watch across the tick.

**Wrong:** after the text reformats, the blur no longer sits exactly over `42` — it slides a
couple of characters off, or the particles and the hidden text disagree.

### C2 — a date **inside** a spoiler

Text: `The answer is ||X||`, with `X` made a relative date.

This is the sharpest one: the source text is a single character and the displayed text is
fourteen.

**Wrong:** the cover is one character wide over a fourteen-character date — wrong from the very
first frame, before any tick. That is the range being built from the source length.

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

## D. What a bot would add

Markdown plus the composer covers everything above. A bot would be better for:

- **Exact entity layouts** — C2 and C3 depend on a spoiler and a date overlapping in a specific
  way. Doing that by hand in the composer works but is fiddly to reproduce identically.
- **Repeatability** — the same message, byte for byte, after every change.
- **Offsets we can't type** — a spoiler that starts mid-date, adjacent entities with no
  separator, an entity at offset 0, and other boundaries the composer will not let you build.

Open question worth one experiment: whether the Bot API can send the date entity at all.
`TdExtensions.cs:32-38` says the format string is "the same format string bots use", which
suggests yes, but I have not confirmed the field name — one `sendMessage` with an explicit
entity would settle it.

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
