// serialize.js
// The mapping layer between the ProseMirror document and TDLib's
// PageBlock / RichText JSON (using "@type" like tdjson).
//
//  toTDLib(doc)            PM doc  -> pageBlock[]   (for saving)
//  fromTDLib(blocks, sch)  blocks  -> PM node       (for loading)
//
// Round-trip invariants that the unit tests should pin:
//  - authored marks survive; adjacent equal runs merge
//  - entities (mention/hashtag/url/...) are NOT auto-detected here — unmarked text
//    stays plain; TDLib detects them server-side
//  - inline atoms (emoji, math) keep their attrs

const T = (type, rest = {}) => ({ "@type": type, ...rest });

// Mark name -> richText wrapper. Applied outermost-first in this order.
const MARK_WRAP = [
  ["link", (t, a) => T("richTextUrl", { text: t, url: a.href, is_cached: !!a.isCached })],
  ["date_time", (t, a) => T("richTextDateTime", { text: t, unix_time: a.unixTime | 0 })],
  ["mention_name", (t, a) => T("richTextMentionName", { text: t, user_id: a.userId })],
  ["spoiler", (t) => T("richTextSpoiler", { text: t })],
  ["marked", (t) => T("richTextMarked", { text: t })],
  ["code", (t) => T("richTextFixed", { text: t })],
  ["strike", (t) => T("richTextStrikethrough", { text: t })],
  ["underline", (t) => T("richTextUnderline", { text: t })],
  ["subscript", (t) => T("richTextSubscript", { text: t })],
  ["superscript", (t) => T("richTextSuperscript", { text: t })],
  ["em", (t) => T("richTextItalic", { text: t })],
  ["strong", (t) => T("richTextBold", { text: t })],
];

const plain = (s) => T("richTextPlain", { text: s });

// Flat text of a RichText tree. Only used to LABEL things the editor renders but
// doesn't own — button faces — never to pull page text out of the model.
export function richTextToPlain(rt) {
  if (!rt) return "";
  const t = rt["@type"];
  if (t === "richTextPlain") return rt.text || "";
  if (t === "richTexts") return (rt.texts || []).map(richTextToPlain).join("");
  if (t === "richTextCustomEmoji") return rt.alternative_text || "";
  if (t === "richTextMathematicalExpression") return rt.expression || "";
  return richTextToPlain(rt.text);
}

function wrapMarks(text, marks) {
  let rt = plain(text);
  const has = (name) => marks.find((m) => m.type.name === name);
  for (const [name, make] of MARK_WRAP) {
    const m = has(name);
    if (m) rt = make(rt, m.attrs);
  }
  return rt;
}

// Inline fragment -> RichText (single node, or richTexts concatenation).
function inlineToRichText(fragment) {
  const parts = [];
  fragment.forEach((node) => {
    if (node.isText) {
      const marked = node.marks.length > 0;
      // Entity detection (@mention, #hashtag, url, email, ...) is intentionally NOT done
      // here — TDLib detects those server-side. Unmarked runs stay plain text.
      if (marked) parts.push(wrapMarks(node.text, node.marks));
      else parts.push(plain(node.text));
    } else if (node.type.name === "custom_emoji") {
      parts.push(T("richTextCustomEmoji", {
        custom_emoji_id: node.attrs.customEmojiId,
        alternative_text: node.attrs.alt,
      }));
    } else if (node.type.name === "math_inline") {
      parts.push(T("richTextMathematicalExpression", { expression: node.attrs.latex }));
    } else if (node.type.name === "button") {
      // Carried through untouched — the editor never rewrites a button.
      parts.push(T("richTextButton", { button: node.attrs.button }));
    }
  });
  if (parts.length === 0) return plain("");
  if (parts.length === 1) return parts[0];
  return T("richTexts", { texts: parts });
}

const FIGURE_TYPE = {
  photo: "pageBlockPhoto",
  video: "pageBlockVideo",
  audio: "pageBlockAudio",
  animation: "pageBlockAnimation",
  voice: "pageBlockVoiceNote",
  document: "pageBlockDocument",
};

// PageBlockHorizontalAlignment <-> the `align` attr, shared by table cells and
// button rows.
export const alignType = (a) =>
  a === "center" ? T("pageBlockHorizontalAlignmentCenter")
  : a === "right" ? T("pageBlockHorizontalAlignmentRight")
  : T("pageBlockHorizontalAlignmentLeft");

function blockToTDLib(node) {
  const n = node.type.name;
  switch (n) {
    case "paragraph":
      return T("pageBlockParagraph", { text: inlineToRichText(node.content) });
    case "heading":
      return T("pageBlockSectionHeading", { text: inlineToRichText(node.content), size: node.attrs.size });
    case "footer":
      return T("pageBlockFooter", { footer: inlineToRichText(node.content) });
    case "preformatted":
      return T("pageBlockPreformatted", {
        text: plain(node.textContent),
        language: node.attrs.language || "",
      });
    case "blockquote": {
      const blocks = [];
      let credit = null;
      node.forEach((c) => {
        if (c.type.name === "blockquote_credit") credit = c.content.size ? inlineToRichText(c.content) : null;
        else blocks.push(blockToTDLib(c));
      });
      return T("pageBlockBlockQuote", { blocks, credit });
    }
    case "pullquote": {
      let text = plain(""), credit = null;
      node.forEach((c) => {
        if (c.type.name === "pullquote_text") text = inlineToRichText(c.content);
        else if (c.type.name === "pullquote_credit") credit = c.content.size ? inlineToRichText(c.content) : null;
      });
      return T("pageBlockPullQuote", { text, credit });
    }
    case "expandable_blockquote": {
      let text = plain(""), credit = null;
      node.forEach((c) => {
        if (c.type.name === "expandable_text") text = inlineToRichText(c.content);
        else if (c.type.name === "expandable_credit") credit = c.content.size ? inlineToRichText(c.content) : null;
      });
      return T("pageBlockExpandableBlockQuote", { text, credit });
    }
    case "button_row":
      return T("pageBlockButtonRow", {
        buttons: node.attrs.buttons || [],
        align: alignType(node.attrs.align),
      });
    case "divider":
      return T("pageBlockDivider");
    case "anchor":
      return T("pageBlockAnchor", { name: node.attrs.name });
    case "math_block":
      return T("pageBlockMathematicalExpression", { expression: node.attrs.latex });
    case "bullet_list":
    case "ordered_list":
      return T("pageBlockList", { items: listItems(node) });
    case "details":
      return detailsToTDLib(node);
    case "figure":
      return figureToTDLib(node);
    case "collage":
      return collageToTDLib(node, "pageBlockCollage");
    case "slideshow":
      return collageToTDLib(node, "pageBlockSlideshow");
    case "table":
      return tableToTDLib(node);
    default:
      return T("pageBlockParagraph", { text: inlineToRichText(node.content) });
  }
}

function childBlocks(node) {
  const out = [];
  node.forEach((c) => out.push(blockToTDLib(c)));
  return out;
}

function listItems(listNode) {
  const items = [];
  const type = listNode.type.name === "ordered_list" ? (listNode.attrs.listType || "1") : "";
  let value = listNode.type.name === "ordered_list" ? (listNode.attrs.order || 1) : 0;
  listNode.forEach((li) => {
    const blocks = [];
    li.forEach((c) => blocks.push(blockToTDLib(c)));
    items.push(T("pageBlockListItem", {
      label: type ? String(value) : "",
      blocks,
      has_checkbox: li.attrs.hasCheckbox,
      is_checked: li.attrs.isChecked,
      value: type ? value : 0,
      type,
    }));
    if (type) value++;
  });
  return items;
}

function detailsToTDLib(node) {
  let header = plain("");
  const blocks = [];
  node.forEach((c) => {
    if (c.type.name === "details_summary") header = inlineToRichText(c.content);
    else blocks.push(blockToTDLib(c));
  });
  return T("pageBlockDetails", { header, blocks, is_open: node.attrs.open });
}

function figureToTDLib(node) {
  let caption = null;
  node.forEach((c) => {
    if (c.type.name === "caption") {
      caption = T("pageBlockCaption", { text: inlineToRichText(c.content), credit: null });
    }
  });
  // pageBlockMap has its own shape (location/zoom/size, no file). Preserve every
  // property so it survives the round-trip even though it renders like a photo.
  if (node.attrs.kind === "map") {
    return T("pageBlockMap", {
      location: node.attrs.location,
      zoom: node.attrs.zoom | 0,
      width: node.attrs.width | 0,
      height: node.attrs.height | 0,
      caption,
    });
  }
  // pageBlockDocument is just document + caption — no url, no spoiler.
  if (node.attrs.kind === "document") {
    return T("pageBlockDocument", { file_id: node.attrs.fileId, caption });
  }
  const type = FIGURE_TYPE[node.attrs.kind] || "pageBlockPhoto";
  // The structured photo/video/... object is replaced by `file_id` (the host
  // resolves it back to a file); the preview `url` is preserved as-is.
  return T(type, {
    file_id: node.attrs.fileId,
    url: node.attrs.src,
    caption,
    has_spoiler: node.attrs.hasSpoiler,
    ...(type === "pageBlockVideo" || type === "pageBlockAnimation"
      ? { need_autoplay: node.attrs.needAutoplay, is_looped: node.attrs.isLooped }
      : {}),
  });
}

// pageBlockCollage / pageBlockSlideshow: figures -> blocks, trailing caption ->
// the group caption. Both TDLib types share this shape.
function collageToTDLib(node, type) {
  const blocks = [];
  let caption = null;
  node.forEach((c) => {
    if (c.type.name === "caption") caption = T("pageBlockCaption", { text: inlineToRichText(c.content), credit: null });
    else blocks.push(blockToTDLib(c));
  });
  return T(type, { blocks, caption });
}

// A table cell maps to ONE RichText. A loaded cell has a single paragraph; a
// merged cell may hold several, which are joined with newlines into one RichText.
function cellTextToRichText(cell) {
  const parts = [];
  cell.forEach((para, _offset, i) => {
    if (i > 0) parts.push(plain("\n"));
    parts.push(inlineToRichText(para.content));
  });
  if (parts.length === 0) return plain("");
  if (parts.length === 1) return parts[0];
  return T("richTexts", { texts: parts });
}

function tableToTDLib(node) {
  const rows = [];
  node.forEach((row) => {
    const cells = [];
    row.forEach((cell) => {
      cells.push(T("pageBlockTableCell", {
        text: cellTextToRichText(cell),
        is_header: cell.type.name === "table_header",
        colspan: cell.attrs.colspan || 1,
        rowspan: cell.attrs.rowspan || 1,
        align: alignType(cell.attrs.align),
        valign: valignType(cell.attrs.valign),
      }));
    });
    rows.push(cells);
  });
  return T("pageBlockTable", { caption: null, cells: rows, is_bordered: true, is_striped: false });
}

const valignType = (v) =>
  v === "middle" ? T("pageBlockVerticalAlignmentMiddle")
  : v === "bottom" ? T("pageBlockVerticalAlignmentBottom")
  : T("pageBlockVerticalAlignmentTop");

export function toTDLib(doc) {
  const blocks = [];
  doc.forEach((node) => blocks.push(blockToTDLib(node)));
  return blocks;
}

// ---------------------------------------------------------------------------
// toInputBlocks: PM doc -> InputPageBlock[] for richMessageSourceBlocks (sending).
// Mirrors toTDLib but emits the inputPageBlock* family: type names change, list items
// drop `label`, and media blocks carry the full input* media converted from the stored
// display object. Every nested block vector (list items / details / collage / slideshow /
// blockquote) is named `blocks`. RichText, pageBlockCaption, pageBlockTableCell and
// location are reused unchanged.
// ---------------------------------------------------------------------------
const INPUT_FIGURE = {
  photo: "inputPageBlockPhoto",
  video: "inputPageBlockVideo",
  audio: "inputPageBlockAudio",
  animation: "inputPageBlockAnimation",
  voice: "inputPageBlockVoiceNote",
  document: "inputPageBlockDocument",
};

// TDLib File -> InputFile: prefer the persistent remote id, else the session id.
function fileToInput(file) {
  if (!file) return null;
  if (file.remote && file.remote.id) return T("inputFileRemote", { id: file.remote.id });
  if (file.id != null) return T("inputFileId", { id: file.id });
  return null;
}

// display thumbnail (format/width/height/file) -> inputThumbnail; null when absent.
function thumbnailToInput(thumb) {
  if (!thumb || !thumb.file) return null;
  return T("inputThumbnail", { thumbnail: fileToInput(thumb.file), width: thumb.width || 0, height: thumb.height || 0 });
}

// display media object -> input* media (first photo size = thumbnail, last = main).
function mediaToInput(kind, media) {
  if (!media) return null;
  switch (kind) {
    case "photo": {
      const sizes = media.sizes || [];
      if (!sizes.length) return null;
      const main = sizes[sizes.length - 1];
      const thumb = sizes[0];
      return T("inputPhoto", {
        photo: fileToInput(main.photo),
        thumbnail: thumb !== main
          ? T("inputThumbnail", { thumbnail: fileToInput(thumb.photo), width: thumb.width || 0, height: thumb.height || 0 })
          : null,
        video: null,
        added_sticker_file_ids: [],
        width: main.width || 0,
        height: main.height || 0,
      });
    }
    case "video":
      return T("inputVideo", {
        video: fileToInput(media.video),
        thumbnail: thumbnailToInput(media.thumbnail),
        cover: null,
        start_timestamp: 0,
        added_sticker_file_ids: [],
        duration: media.duration || 0,
        width: media.width || 0,
        height: media.height || 0,
        supports_streaming: !!media.supports_streaming,
      });
    case "animation":
      return T("inputAnimation", {
        animation: fileToInput(media.animation),
        thumbnail: thumbnailToInput(media.thumbnail),
        added_sticker_file_ids: [],
        duration: media.duration || 0,
        width: media.width || 0,
        height: media.height || 0,
      });
    case "audio":
      return T("inputAudio", {
        audio: fileToInput(media.audio),
        album_cover_thumbnail: thumbnailToInput(media.album_cover_thumbnail),
        duration: media.duration || 0,
        title: media.title || "",
        performer: media.performer || "",
      });
    case "voice":
      return T("inputVoiceNote", {
        voice_note: fileToInput(media.voice),
        duration: media.duration || 0,
        waveform: media.waveform || "",
      });
    case "document":
      return T("inputDocument", {
        document: fileToInput(media.document),
        thumbnail: thumbnailToInput(media.thumbnail),
        // The server already typed this file, so there's nothing to re-detect.
        disable_content_type_detection: false,
      });
    default:
      return null;
  }
}

function captionOf(node) {
  let caption = null;
  node.forEach((c) => {
    if (c.type.name === "caption") caption = T("pageBlockCaption", { text: inlineToRichText(c.content), credit: null });
  });
  return caption;
}

function figureToInput(node) {
  const caption = captionOf(node);
  if (node.attrs.kind === "map") {
    return T("inputPageBlockMap", {
      location: node.attrs.location,
      zoom: node.attrs.zoom | 0,
      width: node.attrs.width | 0,
      height: node.attrs.height | 0,
      caption,
    });
  }
  const type = INPUT_FIGURE[node.attrs.kind] || "inputPageBlockPhoto";
  const kind = node.attrs.kind;
  const out = { caption };
  // media field name matches the kind (photo/video/animation/audio/voice_note)
  const field = kind === "voice" ? "voice_note" : kind;
  out[field] = mediaToInput(kind, node.attrs.media);
  // Only the visual kinds carry a spoiler — audio, voice and document don't.
  if (kind !== "audio" && kind !== "voice" && kind !== "document") out.has_spoiler = node.attrs.hasSpoiler;
  return T(type, out);
}

function collageToInput(node, type) {
  const blocks = [];
  let caption = null;
  node.forEach((c) => {
    if (c.type.name === "caption") caption = T("pageBlockCaption", { text: inlineToRichText(c.content), credit: null });
    else blocks.push(blockToInput(c));
  });
  return T(type, { blocks, caption });
}

function blockToInput(node) {
  switch (node.type.name) {
    case "paragraph":
      return T("inputPageBlockParagraph", { text: inlineToRichText(node.content) });
    case "heading":
      return T("inputPageBlockSectionHeading", { text: inlineToRichText(node.content), size: node.attrs.size });
    case "footer":
      return T("inputPageBlockFooter", { footer: inlineToRichText(node.content) });
    case "preformatted":
      return T("inputPageBlockPreformatted", { text: plain(node.textContent), language: node.attrs.language || "" });
    case "blockquote": {
      const blocks = [];
      let credit = null;
      node.forEach((c) => {
        if (c.type.name === "blockquote_credit") credit = c.content.size ? inlineToRichText(c.content) : null;
        else blocks.push(blockToInput(c));
      });
      return T("inputPageBlockBlockQuote", { blocks, credit });
    }
    case "pullquote": {
      let text = plain(""), credit = null;
      node.forEach((c) => {
        if (c.type.name === "pullquote_text") text = inlineToRichText(c.content);
        else if (c.type.name === "pullquote_credit") credit = c.content.size ? inlineToRichText(c.content) : null;
      });
      return T("inputPageBlockPullQuote", { text, credit });
    }
    case "expandable_blockquote": {
      let text = plain(""), credit = null;
      node.forEach((c) => {
        if (c.type.name === "expandable_text") text = inlineToRichText(c.content);
        else if (c.type.name === "expandable_credit") credit = c.content.size ? inlineToRichText(c.content) : null;
      });
      return T("inputPageBlockExpandableBlockQuote", { text, credit });
    }
    case "button_row":
      // inputPageBlockButtonRow takes inlineButton, not an input twin — the
      // buttons cross over unchanged.
      return T("inputPageBlockButtonRow", {
        buttons: node.attrs.buttons || [],
        align: alignType(node.attrs.align),
      });
    case "divider":
      return T("inputPageBlockDivider");
    case "anchor":
      return T("inputPageBlockAnchor", { name: node.attrs.name });
    case "math_block":
      return T("inputPageBlockMathematicalExpression", { expression: node.attrs.latex });
    case "bullet_list":
    case "ordered_list":
      return T("inputPageBlockList", { items: inputListItems(node) });
    case "details": {
      let header = plain("");
      const blocks = [];
      node.forEach((c) => {
        if (c.type.name === "details_summary") header = inlineToRichText(c.content);
        else blocks.push(blockToInput(c));
      });
      return T("inputPageBlockDetails", { header, blocks, is_open: node.attrs.open });
    }
    case "figure":
      return figureToInput(node);
    case "collage":
      return collageToInput(node, "inputPageBlockCollage");
    case "slideshow":
      return collageToInput(node, "inputPageBlockSlideshow");
    case "table":
      // inputPageBlockTable reuses the display pageBlockTableCell shape.
      return { ...tableToTDLib(node), "@type": "inputPageBlockTable" };
    default:
      return T("inputPageBlockParagraph", { text: inlineToRichText(node.content) });
  }
}

function inputListItems(listNode) {
  const items = [];
  const type = listNode.type.name === "ordered_list" ? (listNode.attrs.listType || "1") : "";
  let value = listNode.type.name === "ordered_list" ? (listNode.attrs.order || 1) : 0;
  listNode.forEach((li) => {
    const blocks = [];
    li.forEach((c) => blocks.push(blockToInput(c)));
    items.push(T("inputPageBlockListItem", {
      blocks,
      has_checkbox: li.attrs.hasCheckbox,
      is_checked: li.attrs.isChecked,
      value: type ? value : 0,
      type,
    }));
    if (type) value++;
  });
  return items;
}

export function toInputBlocks(doc) {
  const blocks = [];
  doc.forEach((node) => blocks.push(blockToInput(node)));
  return blocks;
}

// ---------------------------------------------------------------------------
// fromTDLib: enough to seed the editor from a sample document. Flattens the
// richText tree back into PM inline content (text+marks / atoms).
// ---------------------------------------------------------------------------
const RT_MARK = {
  richTextBold: "strong",
  richTextItalic: "em",
  richTextUnderline: "underline",
  richTextStrikethrough: "strike",
  richTextFixed: "code",
  richTextSpoiler: "spoiler",
  richTextMarked: "marked",
  richTextSubscript: "subscript",
  richTextSuperscript: "superscript",
};

function richTextToInline(rt, schema, marks = []) {
  if (!rt) return [];
  const out = [];
  const t = rt["@type"];
  if (t === "richTextPlain") {
    if (rt.text) out.push(schema.text(rt.text, marks));
  } else if (t === "richTexts") {
    rt.texts.forEach((x) => out.push(...richTextToInline(x, schema, marks)));
  } else if (RT_MARK[t]) {
    out.push(...richTextToInline(rt.text, schema, marks.concat(schema.marks[RT_MARK[t]].create())));
  } else if (t === "richTextUrl") {
    out.push(...richTextToInline(rt.text, schema, marks.concat(schema.marks.link.create({ href: rt.url, isCached: rt.is_cached }))));
  } else if (t === "richTextDateTime") {
    out.push(...richTextToInline(rt.text, schema, marks.concat(schema.marks.date_time.create({ unixTime: rt.unix_time | 0 }))));
  } else if (t === "richTextMentionName") {
    out.push(...richTextToInline(rt.text, schema, marks.concat(schema.marks.mention_name.create({ userId: rt.user_id }))));
  } else if (t === "richTextCustomEmoji") {
    out.push(schema.nodes.custom_emoji.create({ customEmojiId: rt.custom_emoji_id, alt: rt.alternative_text }));
  } else if (t === "richTextMathematicalExpression") {
    out.push(schema.nodes.math_inline.create({ latex: rt.expression }));
  } else if (t === "richTextButton") {
    // Note this must come before the generic `rt.text` fallback below: a button
    // HAS no `text` of its own (the label is button.text), and falling through
    // would silently turn the action into the words on its face.
    out.push(schema.nodes.button.create({ button: rt.button || null }));
  } else if (rt.text) {
    // entity wrappers (mention/hashtag/...) collapse back to plain text
    out.push(...richTextToInline(rt.text, schema, marks));
  }
  return out;
}

// pageBlockCollage / pageBlockSlideshow -> a figure group + group caption.
function collageFromTDLib(b, schema, nodeType) {
  const N = schema.nodes;
  const figures = (b.blocks || [])
    .map((x) => blockFromTDLib(x, schema))
    .filter((n) => n.type === N.figure)
    // media inside a collage/slideshow carries no caption — keep only its attrs.
    .map((fig) => N.figure.create(fig.attrs));
  if (!figures.length) figures.push(N.figure.create());
  const cap = N.caption.create(null, b.caption ? richTextToInline(b.caption.text, schema) : []);
  return nodeType.create(null, [...figures, cap]);
}

// Pull the file id, preview url and intrinsic size out of a TDLib media block.
// The block carries a structured photo/video/audio/animation/voice object whose
// nested `file` holds the id; we keep just the id (getModel re-emits it as
// `file_id` for the host's ClientResultHandler to resolve back to a file). For
// photos the largest size wins. Falls back to a plain `file_id` (the demo sample).
function mediaInfo(b) {
  let file = null, width = 0, height = 0;
  switch (b["@type"]) {
    case "pageBlockPhoto": {
      const sizes = b.photo?.sizes || [];
      const s = sizes[sizes.length - 1];
      file = s?.photo; width = s?.width || 0; height = s?.height || 0;
      break;
    }
    case "pageBlockVideo": file = b.video?.video; width = b.video?.width || 0; height = b.video?.height || 0; break;
    case "pageBlockAnimation": file = b.animation?.animation; width = b.animation?.width || 0; height = b.animation?.height || 0; break;
    case "pageBlockAudio": file = b.audio?.audio; break;
    case "pageBlockVoiceNote": file = b.voice_note?.voice; break;
    case "pageBlockDocument": file = b.document?.document; break;
  }
  return { fileId: file?.id ?? b.file_id ?? "", src: b.url || "", width, height };
}

function blockFromTDLib(b, schema) {
  const N = schema.nodes;
  const inline = (rt) => richTextToInline(rt, schema);
  switch (b["@type"]) {
    case "pageBlockParagraph":
      return N.paragraph.create(null, inline(b.text));
    case "pageBlockSectionHeading":
      return N.heading.create({ size: b.size || 2 }, inline(b.text));
    case "pageBlockFooter":
      return N.footer.create(null, inline(b.footer));
    case "pageBlockPreformatted":
      return N.preformatted.create({ language: b.language || "" }, b.text?.text ? schema.text(b.text.text) : null);
    case "pageBlockBlockQuote": {
      const blocks = nonEmptyBlocks((b.blocks || []).map((x) => blockFromTDLib(x, schema)), schema);
      // Always materialize the credit region (empty when the model had none) so it's
      // editable in place with a placeholder, exactly like the pullquote author line.
      // An empty credit serializes back to null, so it doesn't persist as an empty field.
      blocks.push(N.blockquote_credit.create(null, b.credit ? inline(b.credit) : []));
      return N.blockquote.create(null, blocks);
    }
    case "pageBlockPullQuote":
      return N.pullquote.create(null, [
        N.pullquote_text.create(null, inline(b.text)),
        N.pullquote_credit.create(null, b.credit ? inline(b.credit) : []),
      ]);
    case "pageBlockExpandableBlockQuote":
      return N.expandable_blockquote.create(null, [
        N.expandable_text.create(null, inline(b.text)),
        N.expandable_credit.create(null, b.credit ? inline(b.credit) : []),
      ]);
    case "pageBlockButtonRow":
      return N.button_row.create({
        buttons: b.buttons || [],
        align: alignName(b.align),
      });
    case "pageBlockDivider":
      return N.divider.create();
    case "pageBlockAnchor":
      return N.anchor.create({ name: b.name || "" });
    case "pageBlockMathematicalExpression":
      return N.math_block.create({ latex: b.expression });
    case "pageBlockList": {
      const items = (b.items || []).map((i) => {
        const kids = (i.blocks && i.blocks.length ? i.blocks : [T("pageBlockParagraph", { text: plain("") })])
          .map((x) => blockFromTDLib(x, schema));
        // list_item content is "paragraph block*": the first child MUST be a paragraph.
        if (!kids.length || kids[0].type !== N.paragraph) kids.unshift(N.paragraph.create());
        return N.list_item.create({ hasCheckbox: i.has_checkbox, isChecked: i.is_checked }, kids);
      });
      const ordered = (b.items || [])[0]?.type && (b.items || [])[0].type !== "";
      return (ordered ? N.ordered_list : N.bullet_list).create(null, items);
    }
    case "pageBlockDetails": {
      const summary = N.details_summary.create(null, inline(b.header));
      const blocks = nonEmptyBlocks((b.blocks || []).map((x) => blockFromTDLib(x, schema)), schema);
      return N.details.create({ open: b.is_open }, [summary, ...blocks]);
    }
    case "pageBlockPhoto":
    case "pageBlockVideo":
    case "pageBlockAnimation":
    case "pageBlockAudio":
    case "pageBlockVoiceNote":
    case "pageBlockDocument": {
      const kind = Object.keys(FIGURE_TYPE).find((k) => FIGURE_TYPE[k] === b["@type"]) || "photo";
      const cap = N.caption.create(null, b.caption ? inline(b.caption.text) : []);
      const { fileId, src, width, height } = mediaInfo(b);
      // keep the raw display media object so getInputModel can build input* media
      const media = b.photo || b.video || b.animation || b.audio || b.voice_note || b.document || null;
      return N.figure.create(
        { kind, fileId, src, width, height, media, hasSpoiler: !!b.has_spoiler, needAutoplay: !!b.need_autoplay, isLooped: !!b.is_looped },
        cap
      );
    }
    case "pageBlockMap": {
      const cap = N.caption.create(null, b.caption ? inline(b.caption.text) : []);
      return N.figure.create(
        { kind: "map", location: b.location || null, zoom: b.zoom || 0, width: b.width || 0, height: b.height || 0 },
        cap
      );
    }
    case "pageBlockCollage":
      return collageFromTDLib(b, schema, N.collage);
    case "pageBlockSlideshow":
      return collageFromTDLib(b, schema, N.slideshow);
    case "pageBlockTable": {
      const rows = (b.cells || []).map((row) =>
        N.table_row.create(
          null,
          row.map((c) => {
            const type = c.is_header ? N.table_header : N.table_cell;
            const para = N.paragraph.create(null, inline(c.text));
            return type.create(
              {
                colspan: c.colspan || 1,
                rowspan: c.rowspan || 1,
                colwidth: null,
                align: alignName(c.align),
                valign: valignName(c.valign),
              },
              para
            );
          })
        )
      );
      return N.table.create(null, rows);
    }
    default:
      return N.paragraph.create(null, inline(b.text));
  }
}

export const alignName = (a) =>
  a?.["@type"] === "pageBlockHorizontalAlignmentCenter" ? "center"
  : a?.["@type"] === "pageBlockHorizontalAlignmentRight" ? "right" : "left";
const valignName = (v) =>
  v?.["@type"] === "pageBlockVerticalAlignmentMiddle" ? "middle"
  : v?.["@type"] === "pageBlockVerticalAlignmentBottom" ? "bottom" : "top";

// block+ content cannot be empty; supply a paragraph when a container is empty.
function nonEmptyBlocks(arr, schema) {
  return arr.length ? arr : [schema.nodes.paragraph.create()];
}

export function fromTDLib(blocks, schema) {
  const content = nonEmptyBlocks((blocks || []).map((b) => blockFromTDLib(b, schema)), schema);
  return schema.nodes.doc.create(null, content);
}
