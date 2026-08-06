// schema.js
// ProseMirror schema mapped 1:1 onto the editable subset of TDLib's
// PageBlock / RichText structure (instant-view-only elements omitted).
//
// Marks  -> richText* formatting wrappers
// Nodes  -> pageBlock* containers + inline/block atoms
//
// Two rules encoded here:
//  1. Authored marks (bold, italic, link, ...) live in the model.
//     Auto-detected entities (mention, hashtag, phone, ...) do NOT — they are
//     re-derived on serialize, so they never get baked into editable state.
//  2. Atoms (custom emoji, math, media) are `atom: true` void nodes: one caret
//     stop, selectable as a unit, never edited character-by-character.

import { Schema } from "prosemirror-model";
import { tableNodes } from "prosemirror-tables";
import { richTextToPlain } from "./serialize.js";

// --- Table nodes from prosemirror-tables, extended with align/valign --------
const baseTables = tableNodes({
  tableGroup: "block",
  // Cells hold paragraphs only (no lists, headings, nested tables, block math or
  // media). Loaded cells have exactly one paragraph; merging may leave several,
  // which are joined into a single RichText on save. Inline atoms (emoji, inline
  // math) are allowed.
  cellContent: "paragraph+",
  cellAttributes: {
    // PageBlockHorizontalAlignment — rendered as the `align` attr (left = default,
    // omitted). PageBlockVerticalAlignment — rendered as `data-valign` (top = default).
    align: {
      default: null,
      getFromDOM: (dom) => dom.getAttribute("align") || null,
      setDOMAttr: (value, attrs) => { if (value && value !== "left") attrs.align = value; },
    },
    valign: {
      default: null,
      getFromDOM: (dom) => dom.getAttribute("data-valign") || null,
      setDOMAttr: (value, attrs) => { if (value && value !== "top") attrs["data-valign"] = value; },
    },
  },
});

const nodes = {
  doc: { content: "block+" },

  // pageBlockParagraph
  paragraph: {
    group: "block",
    content: "inline*",
    parseDOM: [{ tag: "p" }],
    toDOM: () => ["p", 0],
  },

  // pageBlockSectionHeading — size 1..6 (1 largest). Maps to h1..h6.
  heading: {
    group: "block",
    content: "inline*",
    attrs: { size: { default: 2 } },
    defining: true,
    parseDOM: [1, 2, 3, 4, 5, 6].map((l) => ({
      tag: "h" + l,
      attrs: { size: l },
    })),
    toDOM: (n) => ["h" + n.attrs.size, 0],
  },

  // pageBlockFooter — a footer/attribution line; plain inline text shown at 12px.
  // Its own block so it round-trips instead of flattening to a paragraph.
  footer: {
    group: "block",
    content: "inline*",
    defining: true,
    parseDOM: [{ tag: "p.pm-footer" }],
    toDOM: () => ["p", { class: "pm-footer" }, 0],
  },

  // pageBlockPreformatted — code block with a language tag
  preformatted: {
    group: "block",
    content: "text*",
    marks: "",
    code: true,
    defining: true,
    // isolating so a gap cursor can sit before/after it — otherwise a code block
    // (Enter inserts a newline) traps the caret when it's the last block.
    isolating: true,
    attrs: { language: { default: "" } },
    parseDOM: [{ tag: "pre", preserveWhitespace: "full" }],
    toDOM: (n) => [
      "pre",
      { "data-language": n.attrs.language || null },
      ["code", 0],
    ],
  },

  // pageBlockBlockQuote — block content, plus an OPTIONAL trailing credit region.
  // The credit region only exists when the loaded model had a credit (it's hidden
  // otherwise), so existing credits round-trip without showing an empty field.
  blockquote: {
    group: "block",
    content: "block+ blockquote_credit?",
    defining: true,
    parseDOM: [{ tag: "blockquote" }],
    toDOM: () => ["blockquote", 0],
  },
  blockquote_credit: {
    content: "inline*",
    isolating: true,
    parseDOM: [{ tag: "div.pm-blockquote-credit" }],
    toDOM: () => ["div", { class: "pm-blockquote-credit" }, 0],
  },

  // pageBlockPullQuote — a centered pill with two editable regions: the quote
  // text, then the credit/author line (richText `credit`). Unlike blockquote
  // (which holds blocks) these are inline-only text regions.
  pullquote: {
    group: "block",
    content: "pullquote_text pullquote_credit",
    // No gap cursor between the two regions — otherwise up/down lands the caret
    // "in between" and typing there splits the pull quote in two.
    allowGapCursor: false,
    parseDOM: [{ tag: "div.pm-pullquote" }],
    toDOM: () => ["div", { class: "pm-pullquote" }, 0],
  },
  pullquote_text: {
    content: "inline*",
    isolating: true,
    parseDOM: [{ tag: "div.pm-pullquote-text" }],
    toDOM: () => ["div", { class: "pm-pullquote-text" }, 0],
  },
  pullquote_credit: {
    content: "inline*",
    isolating: true,
    parseDOM: [{ tag: "div.pm-pullquote-credit" }],
    toDOM: () => ["div", { class: "pm-pullquote-credit" }, 0],
  },

  // pageBlockExpandableBlockQuote — text + credit held directly, like the pull
  // quote above, NOT a list of blocks like `blockquote`. Collapsing is a viewer
  // affordance with nothing to author, so this edits fully expanded and only the
  // block type round-trips.
  expandable_blockquote: {
    group: "block",
    content: "expandable_text expandable_credit",
    // Same reason as pullquote: a gap cursor between the two regions would let
    // typing split the quote in two.
    allowGapCursor: false,
    parseDOM: [{ tag: "blockquote.pm-expandable" }],
    toDOM: () => ["blockquote", { class: "pm-expandable" }, 0],
  },
  expandable_text: {
    content: "inline*",
    isolating: true,
    parseDOM: [{ tag: "div.pm-expandable-text" }],
    toDOM: () => ["div", { class: "pm-expandable-text" }, 0],
  },
  expandable_credit: {
    content: "inline*",
    isolating: true,
    parseDOM: [{ tag: "div.pm-expandable-credit" }],
    toDOM: () => ["div", { class: "pm-expandable-credit" }, 0],
  },

  // pageBlockButtonRow — a row of actions. A block atom holding the buttons
  // verbatim: label, style and type are the button's own business, and there is
  // no authoring UI for them, so this round-trips untouched the way pageBlockMap
  // does. The label is shown but is not document text — it can't be edited or
  // selected into a copy.
  button_row: {
    group: "block",
    atom: true,
    selectable: true,
    attrs: {
      buttons: { default: null },   // raw vector<inlineButton>
      align: { default: "left" },   // PageBlockHorizontalAlignment
    },
    toDOM: (n) => [
      "div",
      { class: "pm-button-row", "data-align": n.attrs.align },
      ...(n.attrs.buttons || []).map((b) => ["span", { class: "pm-button" }, richTextToPlain(b?.text)]),
    ],
  },

  // pageBlockDivider — a block atom so it can be clicked/selected (and deleted) as a unit.
  divider: {
    group: "block",
    atom: true,
    selectable: true,
    parseDOM: [{ tag: "hr" }],
    toDOM: () => ["hr"],
  },

  // pageBlockAnchor — invisible navigation target (block atom)
  anchor: {
    group: "block",
    atom: true,
    selectable: true,
    attrs: { name: { default: "" } },
    toDOM: (n) => ["a", { "data-anchor": n.attrs.name, class: "pm-anchor" }],
  },

  // pageBlockList / pageBlockListItem ----------------------------------------
  bullet_list: {
    group: "block",
    content: "list_item+",
    parseDOM: [{ tag: "ul" }],
    toDOM: () => ["ul", 0],
  },
  ordered_list: {
    group: "block",
    content: "list_item+",
    attrs: { order: { default: 1 }, listType: { default: "1" } }, // type: 1/a/A/i/I
    parseDOM: [
      {
        tag: "ol",
        getAttrs: (d) => ({ order: +d.getAttribute("start") || 1 }),
      },
    ],
    toDOM: (n) =>
      n.attrs.order === 1 ? ["ol", 0] : ["ol", { start: n.attrs.order }, 0],
  },
  // listItem carries the checkbox flags (has_checkbox / is_checked) and holds
  // block content, so nesting via listItem.blocks is natural.
  list_item: {
    content: "paragraph block*",
    defining: true,
    attrs: { hasCheckbox: { default: false }, isChecked: { default: false } },
    parseDOM: [{ tag: "li" }],
    toDOM: (n) => [
      "li",
      {
        class: n.attrs.hasCheckbox ? "pm-checklist-item" : null,
        "data-checked": n.attrs.hasCheckbox ? String(n.attrs.isChecked) : null,
      },
      0,
    ],
  },

  // pageBlockDetails — collapsible. summary = always-visible heading (editable
  // inline), then the collapsible block content. is_open as an attr.
  details: {
    group: "block",
    content: "details_summary block+",
    attrs: { open: { default: true } },
    parseDOM: [{ tag: "details", getAttrs: (d) => ({ open: d.hasAttribute("open") }) }],
    toDOM: (n) => ["details", { open: n.attrs.open ? "" : null, class: "pm-details" }, 0],
  },
  details_summary: {
    content: "inline*",
    defining: true,
    parseDOM: [{ tag: "summary" }],
    toDOM: () => ["summary", 0],
  },

  // Media as figure-with-caption. The media region is non-editable (rendered by
  // the NodeView); the caption is an editable child node (pageBlockCaption).
  // One node spec covers photo/video/audio/animation/voice_note via `kind`.
  figure: {
    group: "block",
    // caption is optional: standalone media has one; media inside a collage/
    // slideshow does not (only the group itself is captioned).
    content: "caption?",
    attrs: {
      kind: { default: "photo" },        // photo|video|audio|animation|voice|document|map
      fileId: { default: "" },           // TDLib file id / remote id
      src: { default: "" },              // resolved appassets:// url for preview
      width: { default: 0 },
      height: { default: 0 },
      hasSpoiler: { default: false },
      needAutoplay: { default: false },
      isLooped: { default: false },
      // Raw TDLib display media object (photo/video/animation/audio/voiceNote) as
      // received via setModel. Kept verbatim so getInputModel can convert it to the
      // matching input* media when sending. Null for newly-inserted media.
      media: { default: null },
      // pageBlockMap-only (kind === "map"): preserved verbatim so the block
      // round-trips even though it currently renders like a photo.
      location: { default: null },       // TDLib `location` object
      zoom: { default: 0 },
    },
    toDOM: () => ["figure", { class: "pm-figure" }, 0],
  },
  caption: {
    content: "inline*",
    parseDOM: [{ tag: "figcaption" }],
    toDOM: () => ["figcaption", 0],
  },

  // pageBlockCollage / pageBlockSlideshow — a group of media figures plus a group
  // caption. Identical shape in TDLib (blocks + caption); they differ only in how
  // they render (grid vs. carousel). Content: one-or-more figures, then the
  // group's caption.
  collage: {
    group: "block",
    content: "figure+ caption",
    toDOM: () => ["div", { class: "pm-collage" }, 0],
  },
  slideshow: {
    group: "block",
    content: "figure+ caption",
    toDOM: () => ["div", { class: "pm-slideshow" }, 0],
  },

  // pageBlockMathematicalExpression (block atom)
  math_block: {
    group: "block",
    atom: true,
    selectable: true,
    attrs: { latex: { default: "" } },
    toDOM: (n) => ["div", { class: "pm-math-block", "data-latex": n.attrs.latex }],
  },

  // table nodes (extended)
  ...baseTables,

  // --- inline ---------------------------------------------------------------
  text: { group: "inline" },

  // richTextCustomEmoji (inline atom)
  custom_emoji: {
    group: "inline",
    inline: true,
    atom: true,
    selectable: true,
    attrs: {
      customEmojiId: { default: "" },
      alt: { default: "" },        // alternative_text (also the fallback glyph)
      src: { default: "" },        // appassets:// preview (static frame)
    },
    toDOM: (n) => [
      "span",
      {
        class: "pm-emoji",
        "data-emoji-id": n.attrs.customEmojiId,
        "data-alt": n.attrs.alt,
      },
    ],
  },

  // richTextButton (inline atom) — an action sitting in a line of text. Held
  // verbatim like button_row above: the label renders, but it is the button's
  // content, not the document's, so it is never edited or harvested as text.
  button: {
    group: "inline",
    inline: true,
    atom: true,
    selectable: true,
    attrs: { button: { default: null } },   // raw inlineButton
    toDOM: (n) => ["span", { class: "pm-button" }, richTextToPlain(n.attrs.button?.text)],
  },

  // richTextMathematicalExpression (inline atom)
  math_inline: {
    group: "inline",
    inline: true,
    atom: true,
    selectable: true,
    attrs: { latex: { default: "" } },
    toDOM: (n) => ["span", { class: "pm-math-inline", "data-latex": n.attrs.latex }],
  },
};

// --- Marks: authored formatting only ---------------------------------------
const marks = {
  strong: { // richTextBold
    parseDOM: [{ tag: "strong" }, { tag: "b" }, { style: "font-weight=bold" }],
    toDOM: () => ["strong", 0],
  },
  em: { // richTextItalic
    parseDOM: [{ tag: "em" }, { tag: "i" }, { style: "font-style=italic" }],
    toDOM: () => ["em", 0],
  },
  underline: { // richTextUnderline
    parseDOM: [{ tag: "u" }, { style: "text-decoration=underline" }],
    toDOM: () => ["u", 0],
  },
  strike: { // richTextStrikethrough
    parseDOM: [{ tag: "s" }, { tag: "del" }],
    toDOM: () => ["s", 0],
  },
  code: { // richTextFixed (monospace)
    parseDOM: [{ tag: "code" }],
    toDOM: () => ["code", 0],
  },
  spoiler: { // richTextSpoiler
    parseDOM: [{ tag: "span.pm-spoiler" }],
    toDOM: () => ["span", { class: "pm-spoiler" }, 0],
  },
  marked: { // richTextMarked (highlight)
    parseDOM: [{ tag: "mark" }],
    toDOM: () => ["mark", 0],
  },
  subscript: { // richTextSubscript
    excludes: "superscript",
    parseDOM: [{ tag: "sub" }],
    toDOM: () => ["sub", 0],
  },
  superscript: { // richTextSuperscript
    excludes: "subscript",
    parseDOM: [{ tag: "sup" }],
    toDOM: () => ["sup", 0],
  },
  link: { // richTextUrl
    inclusive: false,
    excludes: "link date_time", // a run is a link OR a date, never both
    attrs: { href: { default: "" }, isCached: { default: false } },
    parseDOM: [
      {
        tag: "a[href]",
        getAttrs: (d) => ({ href: d.getAttribute("href") }),
      },
    ],
    toDOM: (m) => ["a", { href: m.attrs.href, rel: "noopener" }, 0],
  },
  date_time: { // richTextDateTime — displayed like a link, carries a unix_time.
    inclusive: false,
    excludes: "date_time link",
    attrs: { unixTime: { default: 0 } }, // formatting_type intentionally ignored
    parseDOM: [
      {
        tag: "a.pm-datetime",
        getAttrs: (d) => ({ unixTime: +d.getAttribute("data-unix-time") || 0 }),
      },
    ],
    toDOM: (m) => ["a", { class: "pm-datetime", "data-unix-time": String(m.attrs.unixTime) }, 0],
  },
  mention_name: { // richTextMentionName — authored mention of a specific user
    inclusive: false,
    attrs: { userId: { default: "" } },
    toDOM: (m) => ["a", { class: "pm-mention", "data-user-id": m.attrs.userId }, 0],
  },
};

export const schema = new Schema({ nodes, marks });
