// main.js — assembles the editor, exposes window.UnigramEditor (the bridge the
// NATIVE toolbar drives), and emits state to the host on every change.
//
// Bridge contract
//   native -> JS : window.UnigramEditor.exec(command, args)   (returns a value)
//                  In WebView2: webView.ExecuteScriptAsync(
//                      "UnigramEditor.exec('toggleBold')")
//   JS -> native : sendToHost({type:'state', ...}) on every selection/doc change
//                  In WebView2: window.chrome.webview.postMessage(obj),
//                      handled by CoreWebView2.WebMessageReceived in C#.
// The toolbar never touches ProseMirror directly — only this command surface.

import { EditorState, Plugin, Selection, NodeSelection, TextSelection } from "prosemirror-state";
import { EditorView, Decoration, DecorationSet } from "prosemirror-view";
import { history, undo, redo } from "prosemirror-history";
import { keymap } from "prosemirror-keymap";
import {
  baseKeymap, toggleMark, setBlockType, chainCommands, exitCode, selectAll, deleteSelection,
} from "prosemirror-commands";
import { findWrapping } from "prosemirror-transform";
import { wrapInList, splitListItem, liftListItem, sinkListItem } from "prosemirror-schema-list";
import { inputRules, wrappingInputRule, textblockTypeInputRule } from "prosemirror-inputrules";
import { gapCursor } from "prosemirror-gapcursor";
import { dropCursor } from "prosemirror-dropcursor";
import {
  tableEditing, goToNextCell, isInTable, CellSelection, selectedRect,
  addRowAfter, addRowBefore, addColumnAfter, addColumnBefore,
  deleteRow, deleteColumn, deleteTable, mergeCells, splitCell, setCellAttr, toggleHeaderCell,
} from "prosemirror-tables";

import { schema } from "./schema.js";
import {
  customEmojiView, makeMathView, detailsView, figureView, listItemView, anchorView,
  makePreformattedView, pullquoteTextView, pullquoteCreditView, blockquoteView, blockquoteCreditView, collageView,
  expandableTextView, expandableCreditView,
} from "./nodeviews.js";
import { toTDLib, toInputBlocks, fromTDLib } from "./serialize.js";

const N = schema.nodes;
const M = schema.marks;

// --- InputRichMessage limits ------------------------------------------------
// TDLib getOption values, provided by the host on `ready` via setConfig. Keys are
// the getOption names verbatim. Infinity = unlimited, so the editor works
// unconfigured; the host overwrites these with the real option values.
const limits = {
  rich_message_text_length_max: Infinity,
  rich_message_block_count_max: Infinity,
  rich_message_depth_max: Infinity,
  rich_message_media_count_max: Infinity,
  rich_message_table_column_count_max: Infinity,
};

// metric field -> the limit that caps it.
const LIMIT_OF = {
  textLength: "rich_message_text_length_max",
  blockCount: "rich_message_block_count_max",
  depth: "rich_message_depth_max",
  mediaCount: "rich_message_media_count_max",
  tableColumnCount: "rich_message_table_column_count_max",
};

// --- host messaging ---------------------------------------------------------
function sendToHost(msg) {
  if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
    window.chrome.webview.postMessage(msg);                          // WebView2 (Chromium)
  } else {
    window.dispatchEvent(new CustomEvent("pm-host-message", { detail: msg })); // browser demo
  }
}

function markActive(state, type) {
  const { from, to, empty, $from } = state.selection;
  if (empty) return !!type.isInSet(state.storedMarks || $from.marks());
  return state.doc.rangeHasMark(from, to, type);
}

// Toggle a blockquote around the selection.
//  - Inside a blockquote: remove THAT blockquote by promoting its content, so a
//    list-in-blockquote becomes just the list (we don't strip the inner list).
//  - Otherwise: wrap at the outermost "free" block container (doc/blockquote/
//    details), so a plain list is wrapped as a whole rather than failing to wrap
//    a single list item's first paragraph.
function blockquoteToggle(view) {
  const { state } = view;
  const { $from, $to } = state.selection;
  for (let d = $from.depth; d > 0; d--) {
    if ($from.node(d).type === N.blockquote) {
      const before = $from.before(d);
      const bq = $from.node(d);
      // Promote the body, dropping the credit — it's inline-only and can't live outside
      // a blockquote. (block+ guarantees at least one body block remains.)
      const body = [];
      bq.forEach((c) => { if (c.type !== N.blockquote_credit) body.push(c); });
      view.dispatch(state.tr.replaceWith(before, before + bq.nodeSize, body).scrollIntoView());
      view.focus();
      return;
    }
  }
  const range = $from.blockRange($to, (node) =>
    node.type === N.doc || node.type === N.blockquote || node.type === N.details);
  const wrapping = range && findWrapping(range, N.blockquote);
  if (wrapping) view.dispatch(state.tr.wrap(range, wrapping).scrollIntoView());
  view.focus();
}

// Toggle a pull quote. Pullquote is a two-region node (quote text + credit), not
// a text block, so this converts a paragraph <-> pullquote rather than setBlockType.
//  - In a pullquote: replace it with a paragraph holding the quote text.
//  - Otherwise: replace the current text block with a pullquote whose quote text
//    is the block's content, plus an empty credit region.
function pullquoteToggle(view) {
  const { state } = view;
  const { $from } = state.selection;
  for (let d = $from.depth; d > 0; d--) {
    if ($from.node(d).type === N.pullquote) {
      const pq = $from.node(d);
      const before = $from.before(d);
      const para = N.paragraph.create(null, pq.firstChild ? pq.firstChild.content : null);
      const tr = state.tr.replaceWith(before, before + pq.nodeSize, para);
      tr.setSelection(TextSelection.near(tr.doc.resolve(before + 1)));
      view.dispatch(tr.scrollIntoView());
      view.focus();
      return;
    }
  }
  for (let d = $from.depth; d > 0; d--) {
    const node = $from.node(d);
    if (node.isTextblock) {
      const before = $from.before(d);
      const pq = N.pullquote.create(null, [N.pullquote_text.create(null, node.content), N.pullquote_credit.create()]);
      const tr = state.tr.replaceWith(before, before + node.nodeSize, pq);
      tr.setSelection(TextSelection.near(tr.doc.resolve(before + 2 + node.content.size)));
      view.dispatch(tr.scrollIntoView());
      view.focus();
      return;
    }
  }
}

// Rich description of the block context at the selection for the native toolbar.
//   type     headline category, by precedence:
//            selected media node > list > blockquote > innermost text block.
//            One of: paragraph | heading | preformatted | blockquote | list |
//            photo | video | audio | animation | voice | map.
//   size     heading size 1..6, only when type === "heading".
//   listType bullet | ordered | checkbox, only when type === "list".
// The old version returned the innermost text block first, so it could never
// see an enclosing blockquote/list (the "blockquote not detected" bug).
function describeBlock(state) {
  const sel = state.selection;

  // A directly-selected block atom: its parent path doesn't include the node
  // itself, so read the selected node. Covers a tapped photo/video/map and the
  // block math node ("math"). Inline atoms (inline math, emoji) are not blocks.
  if (sel instanceof NodeSelection && sel.node) {
    const name = sel.node.type.name;
    if (name === "figure") return { type: sel.node.attrs.kind || "photo", size: null, listType: null };
    if (name === "math_block") return { type: "math", size: null, listType: null };
    if (name === "button_row") return { type: "buttons", size: null, listType: null };
    if (name === "anchor") return { type: "anchor", size: null, listType: null, name: sel.node.attrs.name || "" };
  }

  const { $from } = sel;
  let textBlock = null, size = null, figureKind = null, listType = null, inBlockquote = false, inPullquote = false, language = "", inDetailsHeader = false, inExpandable = false;
  for (let d = $from.depth; d > 0; d--) {
    const node = $from.node(d);
    switch (node.type.name) {
      case "paragraph":
        if (!textBlock) textBlock = "paragraph";
        break;
      case "pullquote":
        inPullquote = true; // caret is in the quote text or the author region
        break;
      case "preformatted":
        if (!textBlock) { textBlock = "preformatted"; language = node.attrs.language || ""; }
        break;
      case "heading":
        if (!textBlock) { textBlock = "heading"; size = node.attrs.size; }
        break;
      case "footer":
        if (!textBlock) textBlock = "footer";
        break;
      case "list_item":
        if (node.attrs.hasCheckbox) listType = "checkbox"; // checkbox wins over the list's own kind
        break;
      case "bullet_list":
        if (!listType) listType = "bullet";
        break;
      case "ordered_list":
        if (!listType) listType = "ordered";
        break;
      case "blockquote":
        inBlockquote = true;
        break;
      case "expandable_blockquote":
        inExpandable = true; // caret is in the quote text or the credit region
        break;
      case "details_summary":
        inDetailsHeader = true; // caret is in the details HEADER (not the collapsible body)
        break;
      case "figure":
        figureKind = node.attrs.kind; // caret inside a media caption
        break;
    }
  }

  // Table cells hold only formatted text (one paragraph, no nested blocks), so
  // being in a table is the relevant context — it wins over list/blockquote.
  const type = figureKind || (
    // Only the details HEADER reports "details"; the collapsible body reports its own block.
    inDetailsHeader ? "details"
    : inPullquote ? "pullquote"
    : inExpandable ? "expandable_blockquote"
    : isInTable(state) ? "table"
    : listType ? "list"
    : inBlockquote ? "blockquote"
    : (textBlock || "paragraph")
  );
  return { type, size: type === "heading" ? size : null, listType, language: type === "preformatted" ? language : null };
}

// The cells covered by the selection: every cell of a CellSelection, or the
// single cell containing the caret.
function selectedCells(state) {
  const sel = state.selection;
  const cells = [];
  if (sel instanceof CellSelection) {
    sel.forEachCell((node) => cells.push(node));
  } else {
    const { $from } = sel;
    for (let d = $from.depth; d > 0; d--) {
      const n = $from.node(d);
      if (n.type.name === "table_cell" || n.type.name === "table_header") { cells.push(n); break; }
    }
  }
  return cells;
}

// Contextual table state for the toolbar (only emitted when in a table). align/
// valign/isHeader are the shared value across the selected cells, or null when
// they disagree (mixed). The can* flags are the table commands' own applicability
// checks, so the toolbar can enable/disable exactly what will work.
function describeTable(state) {
  const cells = selectedCells(state);
  const aligns = new Set(), valigns = new Set(), headers = new Set();
  for (const c of cells) {
    aligns.add(c.attrs.align || "left");
    valigns.add(c.attrs.valign || "top");
    headers.add(c.type.name === "table_header");
  }
  const only = (set) => (set.size === 1 ? [...set][0] : null);

  const sel = state.selection;
  const isCell = sel instanceof CellSelection;
  const rowSel = isCell && sel.isRowSelection();   // selection spans full width (whole rows)
  const colSel = isCell && sel.isColSelection();   // selection spans full height (whole cols)

  // Selection geometry within the table (rows/cols covered, and the table's size).
  const rect = selectedRect(state);
  const width = rect.map.width, height = rect.map.height;
  const selRows = rect.bottom - rect.top;
  const selCols = rect.right - rect.left;

  // In a 1-row / 1-column table a full selection is BOTH a row and a column selection.
  // Treat it as a selection of the SINGULAR dimension: selecting every row of the one
  // column is a column selection (offer add/delete COLUMN), and every column of the one
  // row is a row selection. So offer the axis with FEWER cells selected.
  const offerRow = rowSel && (!colSel || selRows <= selCols);
  const offerCol = colSel && (!rowSel || selCols <= selRows);

  // Block operations whose result would empty the table or collapse it to a single (1x1)
  // cell: merging the whole table, or deleting all-but-one row/column of a single col/row.
  const wholeTable = selRows === height && selCols === width;
  const rowDeleteOk = height - selRows >= 1 && !(height - selRows === 1 && width === 1);
  const colDeleteOk = width - selCols >= 1 && !(width - selCols === 1 && height === 1);

  return {
    cellCount: cells.length,
    align: only(aligns),       // "left" | "center" | "right" | null (mixed)
    valign: only(valigns),     // "top" | "middle" | "bottom" | null (mixed)
    isHeader: only(headers),   // true | false | null (mixed)
    canMerge: mergeCells(state) && !wholeTable,
    canUnmerge: splitCell(state),
    canAddRow: offerRow && addRowAfter(state),
    canAddColumn: offerCol && addColumnAfter(state),
    canDeleteRow: offerRow && deleteRow(state) && rowDeleteOk,
    canDeleteColumn: offerCol && deleteColumn(state) && colDeleteOk,
  };
}

// True when the document holds no content — a single empty text block (the initial state).
function isDocEmpty(doc) {
  return doc.childCount === 1 && doc.firstChild.isTextblock && doc.firstChild.content.size === 0;
}

// --- rich (premium-only) content detection ---------------------------------
// True when the document contains anything that CANNOT be represented as a message FormattedText —
// i.e. it's a premium-only rich message. Mirrors PageBlockHelper.TryGetFormattedText's boundary:
// only paragraphs, code blocks, and blockquotes (empty credit, paragraph bodies) are representable,
// and inline is limited to the message-supported TextEntityType set. NOT representable (=> rich):
// any other block (heading, list, table, media, divider, details, pull-quote, anchor, collage,
// slideshow) AND the subscript/superscript/marked marks + inline math (no matching message entity —
// unlike RichText, message FormattedText has no such entities). Cached per (immutable) doc.
const _richCache = new WeakMap();
// expandable_blockquote is representable: it holds RichText directly, so it maps
// 1:1 onto TextEntityTypeExpandableBlockQuote (see PageBlockHelper.TryAppendBlock).
const RICH_OK_BLOCKS = new Set(["doc", "paragraph", "preformatted", "blockquote", "expandable_blockquote", "expandable_text"]);
const RICH_UNSUPPORTED_MARKS = new Set(["subscript", "superscript", "marked"]);
function hasRichContent(doc) {
  let cached = _richCache.get(doc);
  if (cached === undefined) { cached = computeHasRichContent(doc); _richCache.set(doc, cached); }
  return cached;
}
function computeHasRichContent(doc) {
  let rich = false;
  doc.descendants((node) => {
    if (rich) return false;
    if (node.isText) {
      if (node.marks.some((m) => RICH_UNSUPPORTED_MARKS.has(m.type.name))) rich = true;
      return false;
    }
    const name = node.type.name;
    if (node.isInline) {
      // custom_emoji -> TextEntityTypeCustomEmoji (representable); inline math has no message entity.
      // A button has none either — and a message has no way to act on one anyway.
      if (name === "math_inline" || name === "button") rich = true;
      return false;
    }
    if (name === "blockquote_credit" || name === "expandable_credit") {
      if (node.content.size > 0) rich = true; // a non-empty credit can't live in a blockquote entity
      return false;
    }
    if (!RICH_OK_BLOCKS.has(name)) { rich = true; return false; }
    return true; // descend into doc / paragraph / preformatted / blockquote
  });
  return rich;
}

// --- InputRichMessage metrics ----------------------------------------------
// Measured off the PM doc but shaped to match the SERIALIZED richMessageSourceBlocks
// that TDLib counts (see serialize.js for the block / list-item / row / media /
// richtext boundaries). Cached per doc (docs are immutable) so a keystroke computes
// once and both the transaction filter and the state emit reuse it.
const _metricsCache = new WeakMap();
function metricsOf(doc) {
  let m = _metricsCache.get(doc);
  if (!m) { m = computeMetrics(doc); _metricsCache.set(doc, m); }
  return m;
}

// Depth of the richText tree a single inline fragment serializes to: each mark
// wraps its run (+1 per mark), a multi-part fragment adds the richTexts wrapper,
// empty -> 1 (a lone richTextPlain). NOTE: depth counting (blocks + richtext) is a
// best-effort approximation until TDLib's count source is available.
function richTextDepth(fragment) {
  let parts = 0, childMax = 1;
  fragment.forEach((node) => {
    parts++;
    const d = node.isText ? 1 + node.marks.length : 1; // atoms (emoji / math) = 1
    if (d > childMax) childMax = d;
  });
  if (parts === 0) return 1;
  return (parts > 1 ? 1 : 0) + childMax;
}

function computeMetrics(doc) {
  let textLength = 0, mediaCount = 0, tableColumnCount = 0;

  // Text length (UTF-16 units): every text run + custom-emoji alt + math source.
  // Media: every figure (photo/video/audio/animation/voice AND map). Columns: the
  // widest table (first row's total colspan; prosemirror keeps tables rectangular).
  doc.descendants((node) => {
    if (node.isText) { textLength += node.text.length; return; }
    const n = node.type.name;
    if (n === "custom_emoji") textLength += (node.attrs.alt || "").length;
    else if (n === "math_inline" || n === "math_block") textLength += (node.attrs.latex || "").length;
    else if (n === "figure") mediaCount++;
    else if (n === "table") {
      let cols = 0;
      if (node.firstChild) node.firstChild.forEach((cell) => { cols += cell.attrs.colspan || 1; });
      if (cols > tableColumnCount) tableColumnCount = cols;
    }
  });

  // Block count (blocks + list items + table rows) and max nesting depth, via a
  // structural walk mirroring blockToInput.
  let blockCount = 0, depth = 0;
  const bump = (d) => { if (d > depth) depth = d; };
  const inlineAt = (fragment, d) => bump(d + richTextDepth(fragment) - 1);

  function visitBlock(node, d) {
    blockCount++;               // every block-producing node is one block
    bump(d);
    switch (node.type.name) {
      case "blockquote":
        node.forEach((c) => c.type.name === "blockquote_credit"
          ? inlineAt(c.content, d + 1) : visitBlock(c, d + 1));
        break;
      case "details":
        node.forEach((c) => c.type.name === "details_summary"
          ? inlineAt(c.content, d + 1) : visitBlock(c, d + 1));
        break;
      case "collage":
      case "slideshow":
        node.forEach((c) => c.type.name === "caption"
          ? inlineAt(c.content, d + 1) : visitBlock(c, d + 1));
        break;
      case "bullet_list":
      case "ordered_list":
        node.forEach((li) => {           // each list item counts toward block_count
          blockCount++;
          bump(d + 1);
          li.forEach((c) => visitBlock(c, d + 2));
        });
        break;
      case "table":
        node.forEach((row) => {          // each table row counts toward block_count
          blockCount++;
          bump(d + 1);
          row.forEach((cell) => cell.forEach((para) => inlineAt(para.content, d + 2)));
        });
        break;
      case "figure":
        node.forEach((c) => { if (c.type.name === "caption") inlineAt(c.content, d + 1); });
        break;
      case "pullquote":
      case "expandable_blockquote":
        node.forEach((c) => inlineAt(c.content, d + 1));
        break;
      case "paragraph":
      case "heading":
      case "footer":
      case "preformatted":
        inlineAt(node.content, d + 1);
        break;
      // divider, anchor, math_block: no nested blocks or richtext
    }
  }
  doc.forEach((node) => visitBlock(node, 1));

  return { textLength, blockCount, depth, mediaCount, tableColumnCount };
}

// True when a metric is over its cap (any of them). Cheap; unconfigured limits are
// Infinity, so this short-circuits to false and enforcement is a no-op.
function limitsExceeded(m) {
  for (const key in LIMIT_OF) if (m[key] > limits[LIMIT_OF[key]]) return true;
  return false;
}

// State pushed to the native toolbar so it can light up toggles / enable buttons.
function emitState(view) {
  const s = view.state;
  const sel = s.selection;
  const m = metricsOf(s.doc);
  sendToHost({
    type: "state",
    marks: {
      bold: markActive(s, M.strong),
      italic: markActive(s, M.em),
      underline: markActive(s, M.underline),
      strike: markActive(s, M.strike),
      code: markActive(s, M.code),
      spoiler: markActive(s, M.spoiler),
      marked: markActive(s, M.marked),
      subscript: markActive(s, M.subscript),
      superscript: markActive(s, M.superscript),
      link: markActive(s, M.link),
      dateTime: markActive(s, M.date_time),
    },
    block: describeBlock(s),
    table: isInTable(s) ? describeTable(s) : null,
    isEmpty: isDocEmpty(s.doc),   // the document has no content yet
    // True when the doc has content that can't be sent as a plain message FormattedText (premium-only
    // rich content). Native shows the send-button lock for free users, and gates on send.
    hasRichContent: hasRichContent(s.doc),
    can: {
      undo: undo(s),
      redo: redo(s),
      copy: !sel.empty,                 // a selection exists to copy / cut
      paste: view.editable !== false,   // editor accepts a paste (native gates on clipboard)
    },
    // InputRichMessage usage (live counts vs the getOption maxes the host set via
    // setConfig and already knows), so the toolbar can show X/Y and gate Send / inserts.
    usage: {
      text_length: m.textLength,
      block_count: m.blockCount,
      depth: m.depth,
      media_count: m.mediaCount,
      table_column_count: m.tableColumnCount,
    },
    selection: {
      empty: sel.empty,                                            // caret only, nothing selected
      hasText: sel instanceof TextSelection && !sel.empty,         // a text range is selected
      isNode: sel instanceof NodeSelection,                        // an atom/figure is selected
      from: sel.from,
      to: sel.to,
    },
  });
}

// --- custom-emoji position tracking (native overlay) -----------------------
// For NATIVE overlay rendering: the WebView reserves layout space for each custom
// emoji (the .pm-emoji span), and native paints animated frames on top, so it
// needs each emoji's on-screen rect refreshed whenever layout moves. Coordinates
// are CSS px relative to the WebView viewport; `dpr` converts to device px.
//
// Long articles can hold many emoji, most off-screen, so we CULL to the visible
// set with an IntersectionObserver: only on-screen emoji are measured and sent.
// The observer (engine-batched, off the layout hot path) just tracks WHICH emoji
// are visible; exact per-frame positions still come from getBoundingClientRect on
// that small set during scroll. rootMargin pre-includes emoji just outside the
// viewport so native can warm them up before they scroll in.
const emojiVisible = new Set();  // currently on-screen .pm-emoji elements
let emojiObserved = new Set();   // elements currently observed (to diff/unobserve)
let emojiObserver = null;
let emojiView = null;

function ensureEmojiObserver() {
  if (!emojiObserver) {
    // The IO only maintains WHICH emoji are visible; the rAF loop reads the set.
    emojiObserver = new IntersectionObserver((entries) => {
      for (const e of entries) {
        if (e.isIntersecting) emojiVisible.add(e.target);
        else emojiVisible.delete(e.target);
      }
    }, { root: null, rootMargin: "200px" });
  }
  return emojiObserver;
}

// Sync the observed set with the emoji currently in the DOM. O(N) but only on
// document changes, never per scroll frame. Unobserves removed nodes (IO holds a
// strong ref, so this also prevents leaks).
function syncEmojiObserver(view) {
  emojiView = view;
  const obs = ensureEmojiObserver();
  const current = new Set(view.dom.querySelectorAll(".pm-emoji"));
  for (const el of emojiObserved) if (!current.has(el)) { obs.unobserve(el); emojiVisible.delete(el); }
  for (const el of current) if (!emojiObserved.has(el)) obs.observe(el);
  emojiObserved = current;
}

const emojiRect = (el) => {
  const r = el.getBoundingClientRect();
  return { id: el.dataset.emojiId || "", x: r.left, y: r.top, w: r.width, h: r.height };
};

// The pushed set: the IO-tracked visible elements (cheap, no full doc scan).
function collectVisibleEmoji() {
  const out = [];
  for (const el of emojiVisible) if (el.isConnected) out.push(emojiRect(el));
  return out;
}

// Synchronous viewport cull for the on-demand pull — avoids the IO's async
// settle so getCustomEmoji is always correct even right after a doc change.
function collectVisibleEmojiNow(view, margin = 200) {
  const out = [], vw = window.innerWidth, vh = window.innerHeight;
  view.dom.querySelectorAll(".pm-emoji").forEach((el) => {
    const r = el.getBoundingClientRect();
    if (r.bottom >= -margin && r.top <= vh + margin && r.right >= -margin && r.left <= vw + margin) {
      out.push({ id: el.dataset.emojiId || "", x: r.left, y: r.top, w: r.width, h: r.height });
    }
  });
  return out;
}

// TEST: instead of hooking scroll/resize/etc., push the visible emoji rects on
// EVERY animation frame. This keeps native as fresh as the pipeline allows
// without waiting for (and lagging behind) discrete scroll events. `moving` is
// derived by diffing this frame's rects against the last, so native still gets
// the hint. Cost: one message per frame (~60/s) even when idle.
let emojiLoopRunning = false;
let emojiLastKey = "";
function startEmojiLoop(view) {
  emojiView = view;
  if (emojiLoopRunning) return;
  emojiLoopRunning = true;
  const tick = () => {
    if (!emojiLoopRunning) return;
    const emojis = collectVisibleEmoji();
    const key = JSON.stringify(emojis);
    const moving = key !== emojiLastKey;
    emojiLastKey = key;
    sendToHost({ type: "customEmoji", dpr: window.devicePixelRatio || 1, moving, emojis });
    requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
}

// --- command registry (the native toolbar's vocabulary) ---------------------
const run = (cmd) => (view) => { cmd(view.state, view.dispatch, view); view.focus(); };

function insertNode(make) {
  return (view) => {
    const node = make(view.state);
    if (!node) return;
    const { state } = view;
    let tr;
    // A block node can't be a table-cell child, so replaceSelectionWith would
    // split the table in two. When in a table, drop the block AFTER the table
    // instead. (Inline atoms — emoji, inline math — still go at the caret.)
    if (node.isBlock && isInTable(state)) {
      const { $from } = state.selection;
      let depth = $from.depth;
      while (depth > 0 && $from.node(depth).type.name !== "table") depth--;
      const after = $from.after(depth);
      tr = state.tr.insert(after, node);
      tr.setSelection(Selection.near(tr.doc.resolve(after + 1), 1));
    } else {
      tr = state.tr.replaceSelectionWith(node);
    }
    view.dispatch(tr.scrollIntoView());
    view.focus();
  };
}

function setLink(view, args) {
  const { href, isCached } = args || {};
  if (!href) { toggleMark(M.link)(view.state, view.dispatch); view.focus(); return; }
  const { from, to } = view.state.selection;
  view.dispatch(view.state.tr.addMark(from, to, M.link.create({ href, isCached: !!isCached })));
  view.focus();
}

// richTextDateTime over the selection. Pass a unix timestamp to mark/re-mark;
// pass nothing (or null) to toggle the mark off where the caret is. Mirrors
// setLink; formatting_type is not handled yet.
function setDateTime(view, args) {
  const { unixTime } = args || {};
  if (unixTime == null) { toggleMark(M.date_time)(view.state, view.dispatch); view.focus(); return; }
  const { from, to } = view.state.selection;
  view.dispatch(view.state.tr.addMark(from, to, M.date_time.create({ unixTime: Math.trunc(+unixTime) || 0 })));
  view.focus();
}

// Theme: update the accent color and light/dark mode at runtime so the editor
// tracks the native app theme. --accent drives links, the caret, blockquote
// border/fill, selection highlights, etc.; --accent is written inline on <html>
// so it wins in both themes. Both args are optional; pass either or both.
function applyTheme(args) {
  const root = document.documentElement;
  const out = { "@type": "ok" };
  if (args && typeof args.accent === "string" && args.accent) {
    root.style.setProperty("--accent", args.accent);
    out.accent = args.accent;
  }
  if (args && typeof args.background === "string" && args.background) {
    root.style.setProperty("--surface", args.background); // the editor/page background
    out.background = args.background;
  }
  if (args && args.dark != null) {
    out.dark = !!args.dark;
    root.setAttribute("data-theme", out.dark ? "dark" : "light");
  }
  return out;
}

// Config: setConfig({ rich_message_text_length_max: 40000, ... }) — call on `ready`
// (and whenever the options change). Keys are the TDLib getOption names verbatim;
// only positive finite numbers are applied, so partial/absent config is fine. Later
// this same arg object can carry placeholder strings. Re-emits state so the toolbar
// gets the fresh limits + usage immediately.
function applyConfig(view, args) {
  if (args) {
    for (const key in limits) {
      const v = args[key];
      if (typeof v === "number" && isFinite(v) && v > 0) limits[key] = v;
    }
  }
  if (view) emitState(view);
  return { "@type": "ok", limits: { ...limits } };
}

// Languages offered for preformatted blocks. Native fetches this via the
// getCodeLanguages command to populate its dropdown; "none" (empty language) is
// the native side's first option, not listed here.
const CODE_LANGUAGES = [
  "Bash", "C", "C++", "C#", "CSS", "Dart", "Go", "HTML", "Java", "JavaScript",
  "JSON", "Kotlin", "Lua", "Markdown", "Objective-C", "PHP", "Python", "Ruby",
  "Rust", "Shell", "SQL", "Swift", "TypeScript", "XML", "YAML",
];

// Set the language of the preformatted block at the selection. Empty/none clears
// it (the block then shows the "language" placeholder).
function setPreformattedLanguage(view, args) {
  const language = (args && args.language) || "";
  const { $from } = view.state.selection;
  for (let d = $from.depth; d > 0; d--) {
    if ($from.node(d).type === N.preformatted) {
      view.dispatch(view.state.tr.setNodeMarkup($from.before(d), undefined, { language }));
      view.focus();
      return { "@type": "ok", language };
    }
  }
  return { "@type": "editorError", code: "not_in_preformatted", command: "setLanguage" };
}

// Rename the currently selected anchor node (the host calls this when block.type
// is "anchor"). No-op error if an anchor isn't selected.
function setAnchorName(view, args) {
  const name = (args && args.name) || "";
  const sel = view.state.selection;
  if (sel instanceof NodeSelection && sel.node.type === N.anchor) {
    view.dispatch(view.state.tr.setNodeMarkup(sel.from, undefined, { name }));
    view.focus();
    return { "@type": "ok", name };
  }
  return { "@type": "editorError", code: "no_anchor_selected", command: "setAnchorName" };
}

// Update the LaTeX of the currently selected math node (the host calls this after the
// user edits the expression; the math nodeview selects the node on double-click). No-op
// error if a math node isn't selected. Mirrors setAnchorName.
function setMathExpression(view, args) {
  const latex = (args && args.latex) || "";
  const sel = view.state.selection;
  if (sel instanceof NodeSelection && (sel.node.type === N.math_inline || sel.node.type === N.math_block)) {
    view.dispatch(view.state.tr.setNodeMarkup(sel.from, undefined, { latex }));
    view.focus();
    return { "@type": "ok", latex };
  }
  return { "@type": "editorError", code: "no_math_selected", command: "setMathExpression" };
}

// Nearest enclosing list (bullet/ordered) of the selection, with its position.
function listAncestor(state) {
  const { $from } = state.selection;
  for (let d = $from.depth; d > 0; d--) {
    const node = $from.node(d);
    if (node.type === N.bullet_list || node.type === N.ordered_list) return { node, pos: $from.before(d) };
  }
  return null;
}

// Single entry point for list style (matches RichEditorListType): "bullet" |
// "ordered" | "checkbox" set that style on the current list (checkbox = a bullet
// list of to-do items); anything else ("none"/undefined) removes the list.
function setList(view, type) {
  const target = type === "ordered" ? N.ordered_list
    : (type === "bullet" || type === "checkbox") ? N.bullet_list
    : null;
  const wantCheckbox = type === "checkbox";

  if (!target) { // none -> remove the list
    if (listAncestor(view.state)) liftListItem(N.list_item)(view.state, view.dispatch, view);
    view.focus();
    return;
  }

  // Wrap into a list first if we aren't in one (dispatch updates view.state).
  if (!listAncestor(view.state) && !wrapInList(target)(view.state, view.dispatch, view)) {
    view.focus();
    return;
  }

  // Now set the list's node type and the checkbox flag on every item, in one tr.
  const found = listAncestor(view.state);
  if (found) {
    const tr = view.state.tr;
    if (found.node.type !== target) {
      tr.setNodeMarkup(found.pos, target, target === N.ordered_list ? { order: found.node.attrs.order || 1 } : null);
    }
    found.node.forEach((child, offset) => {
      if (child.type !== N.list_item) return;
      const isChecked = wantCheckbox ? child.attrs.isChecked : false;
      if (child.attrs.hasCheckbox === wantCheckbox && child.attrs.isChecked === isChecked) return;
      tr.setNodeMarkup(found.pos + 1 + offset, undefined, { hasCheckbox: wantCheckbox, isChecked });
    });
    if (tr.docChanged) view.dispatch(tr);
  }
  view.focus();
}

// An empty table with default styling: plain cells (no headers), empty
// paragraphs, default (top-left) alignment.
function makeTable(rows, cols) {
  const makeRow = () =>
    N.table_row.create(null, Array.from({ length: cols }, () =>
      N.table_cell.create({ colspan: 1, rowspan: 1, colwidth: null }, N.paragraph.create())
    ));
  return N.table.create(null, Array.from({ length: rows }, makeRow));
}

const COMMANDS = {
  // marks
  toggleBold: run(toggleMark(M.strong)),
  toggleItalic: run(toggleMark(M.em)),
  toggleUnderline: run(toggleMark(M.underline)),
  toggleStrike: run(toggleMark(M.strike)),
  toggleCode: run(toggleMark(M.code)),
  toggleSpoiler: run(toggleMark(M.spoiler)),
  toggleMarked: run(toggleMark(M.marked)),
  toggleSubscript: run(toggleMark(M.subscript)),
  toggleSuperscript: run(toggleMark(M.superscript)),
  setLink: (v, a) => setLink(v, a),
  setDateTime: (v, a) => setDateTime(v, a),

  // theme: setTheme({ accent:"#2f86d6", dark:true }) — call on `ready` and on
  // any app theme change. Either field is optional.
  setTheme: (v, a) => applyTheme(a),

  // config: setConfig({ rich_message_*_max: ... }) — the InputRichMessage limits
  // from getOption. Call on `ready`. Enforced by limitFilterPlugin + emitted in state.
  setConfig: (v, a) => applyConfig(v, a),

  // on-demand pull of the VISIBLE custom-emoji rects (native usually relies on
  // the pushed { type:"customEmoji" } messages; this lets it resync at any time,
  // e.g. after un-hiding the overlay). Same culled set as the push.
  getCustomEmoji: (v) => ({ "@type": "customEmoji", dpr: window.devicePixelRatio || 1, moving: false, emojis: collectVisibleEmojiNow(v) }),

  // block types
  setParagraph: run(setBlockType(N.paragraph)),
  setHeading: (v, a) => run(setBlockType(N.heading, { size: (a && a.size) || 2 }))(v),
  setPreformatted: run(setBlockType(N.preformatted)),
  setFooter: run(setBlockType(N.footer)),
  getCodeLanguages: () => CODE_LANGUAGES,
  setLanguage: (v, a) => setPreformattedLanguage(v, a),
  toggleBlockquote: (v) => blockquoteToggle(v),
  togglePullquote: (v) => pullquoteToggle(v),

  // lists — one command; type matches RichEditorListType (bullet | ordered |
  // checkbox), and "none"/no type removes the list.
  toggleList: (v, a) => setList(v, a && a.type),
  indent: run(sinkListItem(N.list_item)),
  outdent: run(liftListItem(N.list_item)),

  // structural
  insertDivider: insertNode(() => N.divider.create()),
  insertAnchor: (v, a) => insertNode(() => N.anchor.create({ name: (a && a.name) || "" }))(v),
  setAnchorName: (v, a) => setAnchorName(v, a),
  insertDetails: insertNode(() =>
    N.details.create({ open: true }, [
      N.details_summary.create(null, schema.text("Summary")),
      N.paragraph.create(null, schema.text("Hidden content")),
    ])
  ),

  // atoms
  insertEmoji: (v, a) =>
    insertNode(() => N.custom_emoji.create({
      customEmojiId: (a && a.id) || "5368324170671202286",
      alt: (a && a.alt) || "🔥",
      src: (a && a.src) || "",
    }))(v),
  insertImage: (v, a) =>
    insertNode(() =>
      N.figure.create(
        { kind: (a && a.kind) || "photo", fileId: (a && a.fileId) || "", src: (a && a.src) || "", width: (a && a.width) || 0, height: (a && a.height) || 0, hasSpoiler: !!(a && a.hasSpoiler) },
        N.caption.create(null, (a && a.caption) ? schema.text(a.caption) : [])
      )
    )(v),
  insertMathInline: (v, a) =>
    insertNode(() => N.math_inline.create({ latex: (a && a.latex) || "x^2" }))(v),
  insertMathBlock: (v, a) =>
    insertNode(() => N.math_block.create({ latex: (a && a.latex) || "\\int_0^1 x\\,dx" }))(v),
  // set the LaTeX of the selected math node (host calls this after editing, like setLanguage)
  setMathExpression: (v, a) => setMathExpression(v, a),

  // table
  insertTable: (v, a) => insertNode(() => makeTable((a && a.rows) || 2, (a && a.cols) || 2))(v),
  tableAddRowAfter: run(addRowAfter),
  tableAddRowBefore: run(addRowBefore),
  tableAddColumnAfter: run(addColumnAfter),
  tableAddColumnBefore: run(addColumnBefore),
  tableDeleteRow: run(deleteRow),
  tableDeleteColumn: run(deleteColumn),
  tableMergeCells: run(mergeCells),
  tableSplitCell: run(splitCell),
  tableToggleHeader: run(toggleHeaderCell),
  tableDelete: run(deleteTable),
  setCellAlign: (v, a) => run(setCellAttr("align", (a && a.align) || "left"))(v),
  setCellValign: (v, a) => run(setCellAttr("valign", (a && a.valign) || "top"))(v),

  // history
  undo: run(undo),
  redo: run(redo),

  // clipboard / selection. Copy/cut/paste go through the DOM so ProseMirror's own
  // clipboard (de)serialization runs; paste relies on WebView2 clipboard-read being
  // granted (it's blocked for plain web content otherwise).
  selectAll: run(selectAll),
  delete: run(deleteSelection),
  copy: (v) => { v.focus(); document.execCommand("copy"); },
  cut: (v) => { v.focus(); document.execCommand("cut"); },
  paste: (v) => { v.focus(); document.execCommand("paste"); },

  // persistence (native calls these to save / load)
  getModel: (v) => ({ "@type": "richMessage", blocks: toTDLib(v.state.doc) }),
  // richMessageSourceBlocks (input blocks) — used to actually SEND the message.
  getInputModel: (v) => ({ "@type": "richMessageSourceBlocks", blocks: toInputBlocks(v.state.doc) }),
  getProseMirrorJSON: (v) => v.state.doc.toJSON(),
  setModel: (v, a) => {
    const blocks = (a && a.blocks) || [];
    const doc = fromTDLib(blocks, schema);
    const state = EditorState.create({ doc, plugins: v.state.plugins });
    v.updateState(state);
    emitState(v);
    return { "@type": "ok", blocks: blocks.length };
  },
};

// --- plugins ----------------------------------------------------------------
function buildInputRules() {
  const rules = [
    textblockTypeInputRule(/^#\s$/, N.heading, { size: 1 }),
    textblockTypeInputRule(/^##\s$/, N.heading, { size: 2 }),
    textblockTypeInputRule(/^###\s$/, N.heading, { size: 3 }),
    textblockTypeInputRule(/^```\s$/, N.preformatted),
    wrappingInputRule(/^\s*>\s$/, N.blockquote),
    wrappingInputRule(/^\s*([-*])\s$/, N.bullet_list),
    wrappingInputRule(/^(\d+)\.\s$/, N.ordered_list, (m) => ({ order: +m[1] }), (m, node) => node.childCount + node.attrs.order === +m[1]),
  ];
  return inputRules({ rules });
}

// Backspace/Delete over a CellSelection that covers the WHOLE table removes the table, instead of the
// prosemirror-tables default (which only clears the selected cells' text). A partial cell selection
// still just clears those cells. Returns false when not a full-table selection so the key falls through
// to the normal delete behaviour.
function deleteWholeTableSelection(state, dispatch) {
  const sel = state.selection;
  if (!(sel instanceof CellSelection)) return false;
  const rect = selectedRect(state);
  const wholeTable = rect.left === 0 && rect.top === 0
    && rect.right === rect.map.width && rect.bottom === rect.map.height;
  if (!wholeTable) return false;
  return deleteTable(state, dispatch);
}

const editorKeymap = keymap({
  "Mod-b": toggleMark(M.strong),
  "Mod-i": toggleMark(M.em),
  "Mod-u": toggleMark(M.underline),
  "Mod-`": toggleMark(M.code),
  "Mod-z": undo,
  "Mod-y": redo,
  "Shift-Mod-z": redo,
  "Enter": splitListItem(N.list_item),
  "Tab": chainCommands(goToNextCell(1), sinkListItem(N.list_item)),
  "Shift-Tab": chainCommands(goToNextCell(-1), liftListItem(N.list_item)),
  "Mod-Enter": chainCommands(exitCode),
  "Backspace": deleteWholeTableSelection,
  "Delete": deleteWholeTableSelection,
});

// Cell-selection presentation: (1) tag the editor so CSS can hide the browser's
// partial text-selection highlight while cells are selected; (2) decorate the
// selected cells with edge classes so a 2px accent outline traces just the OUTER
// perimeter of the selection rectangle (interior borders stay the grid).
const cellSelectionPlugin = new Plugin({
  props: {
    attributes(state) {
      return state.selection instanceof CellSelection ? { class: "pm-cell-selection" } : null;
    },
    decorations(state) {
      const sel = state.selection;
      if (!(sel instanceof CellSelection)) return null;
      const rect = selectedRect(state);
      const { map, table, tableStart } = rect;
      const decos = [];
      const seen = {};
      for (let row = rect.top; row < rect.bottom; row++) {
        for (let col = rect.left; col < rect.right; col++) {
          const cellPos = map.map[row * map.width + col];
          if (seen[cellPos]) continue;
          seen[cellPos] = true;
          const node = table.nodeAt(cellPos);
          if (!node) continue;
          const cell = map.findCell(cellPos);
          let cls = "pm-cell-selected";
          if (cell.top === rect.top) cls += " pm-edge-top";
          if (cell.bottom === rect.bottom) cls += " pm-edge-bottom";
          if (cell.left === rect.left) cls += " pm-edge-left";
          if (cell.right === rect.right) cls += " pm-edge-right";
          const from = tableStart + cellPos;
          decos.push(Decoration.node(from, from + node.nodeSize, { class: cls }));
        }
      }
      return DecorationSet.create(state.doc, decos);
    },
  },
});

// Make custom-emoji placeholders show the text-selection highlight. They're empty
// non-editable inline atoms, so the browser won't paint selection over them; when
// a non-empty text selection covers one, decorate it so CSS can paint it.
const emojiSelectionPlugin = new Plugin({
  props: {
    decorations(state) {
      const sel = state.selection;
      if (sel.empty || !(sel instanceof TextSelection)) return null;
      const decos = [];
      state.doc.nodesBetween(sel.from, sel.to, (node, pos) => {
        if (node.type === N.custom_emoji) decos.push(Decoration.node(pos, pos + node.nodeSize, { class: "pm-emoji-selected" }));
      });
      return decos.length ? DecorationSet.create(state.doc, decos) : null;
    },
  },
});

// Keep every blockquote ending with a credit region. The credit is optional in the schema
// (so wrapping content into a blockquote can't fail) and isn't auto-created, so this ensures
// it's always present — created when a blockquote appears (wrap/paste), and restored if the
// user deletes it. That makes it editable in place with a placeholder, like pullquote's
// always-present credit; an empty credit serializes back to null, so nothing persists.
const blockquoteCreditPlugin = new Plugin({
  appendTransaction(trs, _oldState, newState) {
    if (!trs.some((t) => t.docChanged)) return null;
    let tr = null;
    newState.doc.descendants((node, pos) => {
      if (node.type !== N.blockquote) return;
      if (node.lastChild?.type !== N.blockquote_credit) {
        tr = tr || newState.tr;
        // End of the blockquote's content (before its closing token), mapped through
        // any inserts already made this pass.
        tr.insert(tr.mapping.map(pos + node.nodeSize - 1), N.blockquote_credit.create());
      }
    });
    return tr;
  },
});

const reportPlugin = new Plugin({
  view(view) {
    emitState(view);
    syncEmojiObserver(view); // observe initial emoji; IO + listeners drive the rest
    return {
      update: (v, prev) => {
        emitState(v);
        // Re-sync the observed set only when the doc changed (emoji added/removed
        // or moved). Pure selection/scroll changes don't touch emoji layout.
        if (v.state.doc !== prev.doc) syncEmojiObserver(v);
      },
    };
  },
});

// Hard-enforce the InputRichMessage limits (enforcement mode a): veto a transaction
// only when it pushes an over-limit metric FURTHER over its cap. That way an already-
// too-big loaded/pasted doc can still be trimmed, deletions are never blocked, and
// selection-only transactions always pass. Per-doc metrics are cached, so this is one
// measured pass per new doc (shared with emitState).
const limitFilterPlugin = new Plugin({
  filterTransaction(tr, oldState) {
    if (!tr.docChanged) return true;
    const nu = metricsOf(tr.doc);
    if (!limitsExceeded(nu)) return true;      // fast path: nothing over a cap
    const old = metricsOf(oldState.doc);
    for (const key in LIMIT_OF) {
      const max = limits[LIMIT_OF[key]];
      if (nu[key] > max && nu[key] > old[key]) return false;
    }
    return true;
  },
});

// --- sample document (TDLib shape) — proves the loader & the bridge ---------
// Note: @mentions / #hashtags are kept as PLAIN TEXT; entity detection is left to
// TDLib server-side, not done on save.
const RT = (text) => ({ "@type": "richTextPlain", text });
export const SAMPLE = [
  { "@type": "pageBlockSectionHeading", size: 1, text: RT("Instant View editor") },
  {
    "@type": "pageBlockParagraph",
    text: {
      "@type": "richTexts",
      texts: [
        RT("A single editing flow over a heterogeneous block tree — "),
        { "@type": "richTextBold", text: RT("bold") },
        RT(", "),
        { "@type": "richTextItalic", text: RT("italic") },
        RT(", "),
        { "@type": "richTextSpoiler", text: RT("spoiler") },
        RT(", a custom emoji "),
        { "@type": "richTextCustomEmoji", custom_emoji_id: "5368324170671202286", alternative_text: "🔥" },
        RT(", inline math "),
        { "@type": "richTextMathematicalExpression", expression: "e^{i\\pi}+1=0" },
        RT(", a link "),
        { "@type": "richTextUrl", text: RT("to TDLib"), url: "https://core.telegram.org/tdlib", is_cached: false },
        RT(". Type @username or #hashtag and watch them appear in the serialized model on save."),
      ],
    },
  },
  {
    "@type": "pageBlockList",
    items: [
      { "@type": "pageBlockListItem", blocks: [{ "@type": "pageBlockParagraph", text: RT("Cross-node caret & selection") }], has_checkbox: true, is_checked: true, value: 0, type: "" },
      { "@type": "pageBlockListItem", blocks: [{ "@type": "pageBlockParagraph", text: RT("Document-wide undo / redo") }], has_checkbox: true, is_checked: true, value: 0, type: "" },
      { "@type": "pageBlockListItem", blocks: [{ "@type": "pageBlockParagraph", text: RT("Tables, media, math") }], has_checkbox: true, is_checked: false, value: 0, type: "" },
    ],
  },
  { "@type": "pageBlockBlockQuote", blocks: [{ "@type": "pageBlockParagraph", text: RT("The toolbar is native; only the editor lives in the WebView.") }], credit: null },
  { "@type": "pageBlockPullQuote", text: RT("A pull quote is one rich line, set apart."), credit: RT("Instant View") },
  { "@type": "pageBlockPreformatted", language: "JavaScript", text: RT("const editor = mountEditor(el);\neditor.focus();") },
  { "@type": "pageBlockDetails", is_open: true, header: RT("Implementation notes"), blocks: [
    { "@type": "pageBlockParagraph", text: RT("This collapsible block toggles without leaving the flow.") },
    { "@type": "pageBlockMathematicalExpression", expression: "\\sum_{k=1}^{n} k = \\frac{n(n+1)}{2}" },
  ] },
  { "@type": "pageBlockPhoto", file_id: "demo-file-1", has_spoiler: false, caption: { "@type": "pageBlockCaption", text: RT("Media is a figure with an editable caption."), credit: null } },
  { "@type": "pageBlockMap", zoom: 14, width: 640, height: 360,
    location: { "@type": "location", latitude: 55.7558, longitude: 37.6173, horizontal_accuracy: 0 },
    caption: { "@type": "pageBlockCaption", text: RT("A map renders like a photo for now, but its location/zoom round-trip."), credit: null } },
  { "@type": "pageBlockCollage", caption: { "@type": "pageBlockCaption", text: RT("A collage of media."), credit: null }, blocks: [
    { "@type": "pageBlockPhoto", file_id: "collage-1", has_spoiler: false, caption: null },
    { "@type": "pageBlockPhoto", file_id: "collage-2", has_spoiler: false, caption: null },
    { "@type": "pageBlockPhoto", file_id: "collage-3", has_spoiler: false, caption: null },
  ] },
  { "@type": "pageBlockSlideshow", caption: { "@type": "pageBlockCaption", text: RT("A slideshow."), credit: null }, blocks: [
    { "@type": "pageBlockPhoto", file_id: "slide-1", has_spoiler: false, caption: null },
    { "@type": "pageBlockVideo", file_id: "slide-2", need_autoplay: true, is_looped: true, has_spoiler: false, caption: null },
  ] },
  { "@type": "pageBlockTable", is_bordered: true, is_striped: false, caption: null, cells: [
    [
      { "@type": "pageBlockTableCell", text: RT("Concern"), is_header: true, colspan: 1, rowspan: 1, align: { "@type": "pageBlockHorizontalAlignmentLeft" }, valign: { "@type": "pageBlockVerticalAlignmentTop" } },
      { "@type": "pageBlockTableCell", text: RT("Owner"), is_header: true, colspan: 1, rowspan: 1, align: { "@type": "pageBlockHorizontalAlignmentLeft" }, valign: { "@type": "pageBlockVerticalAlignmentTop" } },
    ],
    [
      { "@type": "pageBlockTableCell", text: RT("Selection"), is_header: false, colspan: 1, rowspan: 1, align: { "@type": "pageBlockHorizontalAlignmentLeft" }, valign: { "@type": "pageBlockVerticalAlignmentTop" } },
      { "@type": "pageBlockTableCell", text: RT("ProseMirror"), is_header: false, colspan: 1, rowspan: 1, align: { "@type": "pageBlockHorizontalAlignmentLeft" }, valign: { "@type": "pageBlockVerticalAlignmentTop" } },
    ],
  ] },
  { "@type": "pageBlockDivider" },
  { "@type": "pageBlockAnchor", name: "section-2" },
  { "@type": "pageBlockParagraph", text: RT("Block math:") },
  { "@type": "pageBlockMathematicalExpression", expression: "\\int_{-\\infty}^{\\infty} e^{-x^2}\\,dx = \\sqrt{\\pi}" },
];

// --- boot -------------------------------------------------------------------
// `initialBlocks` defaults to empty: production boots a blank document and the
// native host loads content via setModel on `ready`. The dev shell passes SAMPLE.
export function mountEditor(mount, initialBlocks = []) {
  const doc = fromTDLib(initialBlocks, schema);
  const state = EditorState.create({
    doc,
    plugins: [
      buildInputRules(),
      editorKeymap,
      keymap(baseKeymap),
      dropCursor(),
      gapCursor(),
      tableEditing(), // column resizing intentionally omitted (no resize handles)
      cellSelectionPlugin,
      emojiSelectionPlugin,
      blockquoteCreditPlugin,
      limitFilterPlugin,
      history(),
      reportPlugin,
    ],
  });

  const view = new EditorView(mount, {
    state,
    nodeViews: {
      custom_emoji: customEmojiView,
      // Double-click a formula -> emit { type:"mathExpression", ... } with the caret rect
      // (like preformatted's language event); the host edits it and writes back via
      // setMathExpression. block distinguishes inline (richText) from block math.
      math_inline: makeMathView(false, (latex, rect) => sendToHost({
        type: "mathExpression",
        latex,
        block: false,
        dpr: window.devicePixelRatio || 1,
        rect: { x: rect.left, y: rect.top, width: rect.width, height: rect.height },
      })),
      math_block: makeMathView(true, (latex, rect) => sendToHost({
        type: "mathExpression",
        latex,
        block: true,
        dpr: window.devicePixelRatio || 1,
        rect: { x: rect.left, y: rect.top, width: rect.width, height: rect.height },
      })),
      details: detailsView,
      figure: figureView,
      collage: collageView,
      list_item: listItemView,
      anchor: anchorView,
      pullquote_text: pullquoteTextView,
      pullquote_credit: pullquoteCreditView,
      blockquote: blockquoteView,
      blockquote_credit: blockquoteCreditView,
      expandable_text: expandableTextView,
      expandable_credit: expandableCreditView,
      preformatted: makePreformattedView((language, rect) => sendToHost({
        type: "preformattedLanguage",
        language,
        dpr: window.devicePixelRatio || 1,
        rect: { x: rect.left, y: rect.top, width: rect.width, height: rect.height },
      })),
    },
  });

  // Native-overlay emoji tracking: re-measure on anything that moves layout but
  // isn't a ProseMirror transaction. Capture-phase `scroll` catches scrolling on
  // any ancestor; ResizeObserver catches editor reflow / word-wrap; the matchMedia
  // watcher re-arms itself to catch DPI/zoom changes (devicePixelRatio shifts).
  // TEST: drive emoji rect pushes from a continuous rAF loop instead of hooking
  // scroll/resize/DPI. The IntersectionObserver (synced on doc changes) still
  // culls to the visible set; the loop just measures + pushes it every frame.
  startEmojiLoop(view);

  // The bridge the native toolbar talks to. Returns the command's value on
  // success, or an editorError object on failure (so ExecuteScriptAsync's
  // result string tells you what went wrong instead of a silent null).
  window.UnigramEditor = {
    exec(command, args) {
      const fn = COMMANDS[command];
      if (!fn) return { "@type": "editorError", code: "unknown_command", command };
      try {
        return fn(view, args);
      } catch (e) {
        return {
          "@type": "editorError",
          code: "exception",
          command,
          message: String((e && e.message) || e),
        };
      }
    },
    focus: () => view.focus(),
  };

  // Host -> JS command channel. Lets the native side use PostWebMessageAsJson
  // to send large payloads (e.g. setModel) without inlining JSON into a script
  // string. Each command may carry an `id`; the result is posted back so the
  // host can correlate the response.
  function handleHostCommand(d) {
    if (!d || !d.command) return;
    const result = window.UnigramEditor.exec(d.command, d.args);
    sendToHost({ type: "result", id: d.id, command: d.command, result });
  }
  if (window.chrome && window.chrome.webview && window.chrome.webview.addEventListener) {
    window.chrome.webview.addEventListener("message", (e) => handleHostCommand(e.data)); // WebView2
  }
  window.addEventListener("message", (e) => handleHostCommand(e.data)); // browser / postMessage fallback

  // Tell the host the editor is mounted. The host should call setModel in
  // response to THIS, not NavigationCompleted, to avoid the script-not-ready race.
  sendToHost({ type: "ready" });

  return view;
}
