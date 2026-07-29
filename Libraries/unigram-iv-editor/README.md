# Unigram Instant View editor (ProseMirror + WebView2)

A single-flow editor for TDLib `pageBlock` / `richText` documents, hosted in a
WebView2. The **editor** lives in the WebView; the **toolbar is native** (XAML)
and drives the editor through a small bridge. The serialization layer maps the
ProseMirror document to/from TDLib JSON (using `@type`, like tdjson).

This is a secondary feature: the goal is production-solid with minimal surface,
not to own a text engine.

## Layout

```
src/
  schema.js      Node/mark schema. THE map between PM and pageBlock/richText.
  nodeviews.js   NodeViews for atoms (emoji, math), details, media figure, checklists.
  serialize.js   toTDLib(doc) and fromTDLib(blocks, schema). The save/load mapping.
  main.js        Plugins, command registry, host bridge, sample document, mountEditor().
host/
  editor.css     CSS REQUIRED by the editor (design tokens + ProseMirror base/node
                 styling). Shared by BOTH builds; injected at the shell's CSS marker.
  editor-shell.html  PRODUCTION shell (editor only): #editor + bundle + one-line
                 mountEditor boot. No toolbar/inspector/app.js. The native build.
  shell.html     DEV shell: editor + simulated native toolbar + model inspector.
                 Has /*__EDITOR_CSS__*/, <script>/*__BUNDLE__*/</script>, /*__APP__*/.
  app.js         Stands in for the NATIVE side (dev shell only): toolbar wiring +
                 state reflection + model inspector. In production this is C#/XAML.
test/
  serialize.test.mjs   Browser-free fromTDLib/toTDLib tests (round-trips + hardening).
assemble.js      Injects editor.css + bundle (+ app.js with --shell) into the chosen
                 shell -> editor.html. Default = editor only; --shell = dev shell.
```

## Build

```
npm install
npm run build       # minified bundle -> ./editor.html  (EDITOR ONLY: production WebView)
npm run dev         # unminified + sourcemap, with the dev shell (toolbar + inspector)
npm run build:shell # minified, with the dev shell (toolbar + inspector)
npm test            # serialization tests, no browser needed
```

`editor.html` is fully self-contained (no network deps) and opens in a browser
or loads straight into WebView2.

**Two build flavors** (selected by `assemble.js`; pass `--shell` to opt in to the
chrome):

- **Editor only** (`npm run build`, default) — assembles `host/editor-shell.html`:
  just the `#editor` surface, the bundle, and a one-line `mountEditor` boot. No
  toolbar, no model inspector, no `app.js`. This is what the native app embeds —
  the C#/XAML side provides the toolbar and drives the bridge. It boots an EMPTY
  document; the host loads content via `setModel` on `ready`. (`mountEditor(mount,
  initialBlocks)` takes optional seed blocks; the dev shell passes `PMEditor.SAMPLE`.)
- **With dev shell** (`npm run dev` / `npm run build:shell`, `--shell`) — assembles
  `host/shell.html` + `host/app.js`: the simulated native toolbar, state
  reflection, and the live TDLib/ProseMirror model inspector, for testing in a
  plain browser.

Both flavors share `host/editor.css` (design tokens + the required ProseMirror
styling), injected at the shell's CSS marker so there is one source of truth.

## Bridge contract

The toolbar (native) talks to the editor ONLY through this surface. It never
touches ProseMirror directly.

**Native -> JS (commands).** Two transports, pick one:

- Data channel (preferred for payloads like setModel; no escaping/size issues):
  `PostWebMessageAsJson({ command, id?, args? })`. The editor routes it to
  `exec` and posts back `{ type:"result", id, command, result }`.
- Script eval: `ExecuteScriptAsync("UnigramEditor.exec('toggleBold')")`.
  For anything returning a value, wrap in `JSON.stringify(...)`. Do NOT inline
  large JSON object literals as script source — that re-parses data as JS and
  breaks on characters like U+2028/29 (and on any stray trailing byte).

**JS -> Native (state + lifecycle).** `sendToHost(msg)` auto-selects:
`chrome.webview.postMessage` (WebView2) / `CustomEvent` (browser demo). Messages:

- `{ type:"ready" }` — emitted once when the editor is mounted. **Call setModel
  in response to this, NOT NavigationCompleted**, to avoid the script-not-ready
  race (the classic "ExecuteScriptAsync returns null" symptom).
- `{ type:"state", marks, block, table, can:{undo,redo}, selection:{empty,hasText,isNode,from,to} }`
  — on every selection/doc change. Drives toolbar toggle/enabled states.
  - `marks` — bool per authored mark: `bold/italic/underline/strike/code/spoiler/
    marked/subscript/superscript/link/dateTime`.
  - `block` — `{ type, size, listType, language }`. `type` is the headline
    category by precedence (selected media node > list > blockquote > innermost
    text block): `paragraph | heading | preformatted | blockquote | pullquote |
    list | table | photo | video | audio | animation | voice | map | math | anchor`
    (`table` = caret in a cell — cells hold only formatted text, so it wins over
    an enclosing list/blockquote; `math` = a selected block math node, inline math
    is not a block; `anchor` = a selected anchor node, with its `name`). `size` is
    the heading size (1..6) only when
    `type === "heading"`; `listType` is `bullet | ordered | checkbox` only when
    `type === "list"`; `language` is the code language (`""` when none) only when
    `type === "preformatted"`.
  - `table` — `null` unless the caret is in a table; otherwise contextual editing
    state for the selected cell(s): `{ cellCount, align, valign, isHeader,
    canMerge, canUnmerge, canAddRow, canAddColumn, canDeleteRow, canDeleteColumn }`.
    `align` (`left|center|right`), `valign` (`top|middle|bottom`) and `isHeader`
    (`bool`) are the shared value across the selection, or `null` when the cells
    disagree (mixed). `canMerge`/`canUnmerge` reflect the merge/split commands'
    applicability; `canAddRow`/`canDeleteRow` are true only when a whole row is
    selected, and `canAddColumn`/`canDeleteColumn` only when a whole column is.
    Use them to enable/disable toolbar buttons. Drive it with
    `setCellAlign`/`setCellValign`, `tableMergeCells`/`tableSplitCell`,
    `tableToggleHeader`, and `tableAddRow*`/`tableAddColumn*`/`tableDeleteRow`/
    `tableDeleteColumn`.
- `{ type:"result", id, command, result }` — reply to a posted command.
- `{ type:"preformattedLanguage", language, dpr, rect:{ x, y, width, height } }` —
  emitted when the user clicks a code block's language label. `rect` is the
  label's position (CSS px, viewport-relative; × `dpr` for device px) so the host
  can show its language menu anchored to it; respond with `setLanguage`.
- `{ type:"customEmoji", dpr, moving, emojis:[{ id, x, y, w, h }] }` — for NATIVE
  overlay rendering of (animated) custom emoji. `x/y/w/h` are CSS px relative to
  the WebView viewport; multiply by `dpr` for device px. **Viewport-culled**: only
  emoji on screen (plus a 200px margin) are sent, via an IntersectionObserver, so
  cost stays bounded on long articles. Pushed (coalesced to one message per frame)
  on every edit/scroll/resize/DPI change; also pullable on demand via the
  `getCustomEmoji` command (synchronous cull, always current). An empty array
  means "clear the overlay".
  - **`moving`** — true while a scroll is in flight. The overlay is a separate
    surface and CANNOT genlock to the WebView's compositor-driven scroll, so it
    will always lag a frame or two during motion. Recommended handling: while
    `moving` is true, HIDE the native overlay and let the in-page static first
    frame (rendered by the WebView) scroll perfectly with the text; when the
    settled push arrives (`moving:false`, ~120ms after the last scroll), snap the
    native overlay back and resume animation. (The only way to keep native
    rendering *during* scroll without lag is to have the native side own the
    scroll — e.g. host a full-height WebView inside a native ScrollViewer so both
    surfaces move together — at a real memory/perf cost.)

**Commands** (see `COMMANDS` in `main.js`): mark toggles, `setHeading`/`setParagraph`/
`setPreformatted`, `getCodeLanguages` -> `[lang, ...]` / `setLanguage` <- `{ language }`
(sets the current code block's language; `""` = none), `toggleBlockquote`,
`togglePullquote`, lists (`toggleList` <- `{ type }` where type is `bullet |
ordered | checkbox`, or omitted/`none` to remove the list; `indent`/`outdent`),
`insertDivider`/`insertDetails`, anchors
(`insertAnchor` <- `{ name }` / `setAnchorName` <- `{ name }` renames the selected
anchor), atoms
(`insertEmoji`/`insertImage`/`insertMathInline`/`insertMathBlock`), table ops,
`undo`/`redo`, and persistence: `getModel` -> `{ "@type":"richMessage", blocks:[...] }`
(display `pageBlock*`), `setModel` <- `{ blocks:[...] }` (display `pageBlock*`),
`getInputModel` -> `{ "@type":"richMessageSourceBlocks", blocks:[...] }` (the
`inputPageBlock*` family, used to actually SEND the message — `setModel` stays on
display blocks, so the in/out shapes differ), plus `getProseMirrorJSON`, and theming: `setTheme` <- `{ accent?:"#2f86d6",
background?:"#ffffff", dark?:true }` (sets the `--accent` / `--surface` CSS vars
inline on `<html>` and a `data-theme` attribute; all fields optional). `exec`
catches exceptions and returns
`{ "@type":"editorError", code, command, message }` instead of throwing.

**Theme.** Call `setTheme` on `ready` (alongside `setModel`) and on any app theme
change. `accent` drives links, the caret, the blockquote border/fill (a 0.1-opacity
fill derived from the accent via `color-mix`), and selection highlights.
`background` sets the editor/page background (`--surface`). `dark:true` swaps the
surface/ink/line palette to a dark variant; `accent`/`background` set explicitly
are preserved over it.

### Minimal C# host (WebView2)

```csharp
View.CoreWebView2.WebMessageReceived += (s, e) => {
    var msg = JsonDocument.Parse(e.WebMessageAsJson).RootElement;
    switch (msg.GetProperty("type").GetString()) {
        case "ready":
            var envelope = $"{{\"command\":\"setModel\",\"id\":1,\"args\":{_message.ToJson()}}}";
            View.CoreWebView2.PostWebMessageAsJson(envelope);   // send model as DATA
            break;
        case "result": /* {"@type":"ok",...} or editorError */ break;
        case "state":  /* update CommandBar toggles + enabled state */ break;
    }
};
// command out: View.CoreWebView2.ExecuteScriptAsync("UnigramEditor.exec('toggleBold')");
```

## Schema <-> TDLib mapping (summary)

- Authored marks (`strong/em/underline/strike/code/spoiler/marked/sub/super/link/
  date_time/mention_name`) <-> `richText*` wrappers. `date_time` renders like a
  link and carries a `unix_time` (mutually exclusive with `link`); set it with the
  `setDateTime` command. **Auto-detected entities**
  (mention/hashtag/cashtag/bot_command/email) are NOT stored — `toTDLib` derives
  them from plain text on save, so editing across their boundary is never fought.
- Blocks: paragraph, heading(size), preformatted(language), blockquote, pullquote
  (centered pill with two editable regions: quote text + author/credit), divider,
  anchor, list(+checkbox/nesting), details(collapsible), figure(media+caption),
  map (renders as a photo for now; location/zoom/size preserved on round-trip),
  collage/slideshow (a figure group + caption; same TDLib shape — collage uses
  the native mosaic album tiling, see src/mosaic.js; slideshow is a simple row),
  table, inline+block math, custom emoji.
- **A table cell maps to one `RichText`.** Cells hold paragraphs only (no nested
  blocks/lists/tables); a loaded cell has exactly one, but merging cells can leave
  several, which are joined with newlines into a single `RichText` on save. Inline
  atoms (emoji, inline math) are allowed.
- TDLib up to date scheme is located in ../tdjson/td_api.tl, the only type needed
  for this project is richMessage and all underlying types, excluding blocks and
  richTexts explicitly marked as "instant view only".

## Production swap points

- **Math**: `nodeviews.js` renders LaTeX as a styled chip. Swap `mathView`'s
  `render()` for KaTeX, or bridge to the native MicroTeX/Direct2D backend.
- **Emoji / media**: NodeViews show static SVG/glyph previews. In WebView2, expose
  TDLib's cache via `SetVirtualHostNameToFolderMapping("appassets", cacheDir, ...)`
  and set node `src` to `appassets://...`. Animated rendering stays in the native
  read view (Chromium can't composite rlottie/FFmpeg surfaces).

## Known gaps / next steps

- `richTextDateTime` round-trips as a `date_time` mark (carries `unix_time`,
  styled like a link). `formatting_type` is not yet handled — it is dropped on
  load and omitted on save.
- Enter inside a table cell is a no-op (single-paragraph cells). Add a keymap
  entry if you want Enter to jump to the next cell/row.
```
