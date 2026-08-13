// clipboard.test.mjs — the Android-compatible clipboard format, with no browser.
// The serializer's spec table and the parser's rules are plain functions over
// ProseMirror nodes / attribute lookups, so they run in Node against a stub
// element. What can't be covered here is the DOM assembly itself (ProseMirror
// does that) — these tests pin the format, which is the part Android agrees on.
import { build } from "esbuild";
import { writeFileSync, mkdirSync } from "node:fs";

mkdirSync("dist", { recursive: true });
writeFileSync(
  "dist/_clipboard-entry.js",
  'export { schema } from "../src/schema.js";\nexport { CLIPBOARD_NODES, CLIPBOARD_MARKS, CLIPBOARD_RULES, mediaClipboard, restoreNewlines, ORIGIN_ATTRIBUTE } from "../src/clipboard.js";\n'
);
await build({
  entryPoints: ["dist/_clipboard-entry.js"],
  bundle: true,
  format: "esm",
  platform: "node",
  outfile: "dist/_clipboard.mjs",
  logLevel: "silent",
});
const { schema, CLIPBOARD_NODES, CLIPBOARD_MARKS, CLIPBOARD_RULES, mediaClipboard, restoreNewlines, ORIGIN_ATTRIBUTE } =
  await import("../dist/_clipboard.mjs?" + Date.now());

let pass = 0, fail = 0;
function ok(name, fn) {
  try { fn(); console.log("  PASS  " + name); pass++; }
  catch (e) { console.log("  FAIL  " + name + "  ->  " + e.message); fail++; }
}
const eq = (a, b, what) => {
  const x = JSON.stringify(a), y = JSON.stringify(b);
  if (x !== y) throw new Error(what + ": " + x + " != " + y);
};

const N = schema.nodes;
const M = schema.marks;
const node = (name, attrs, content) => N[name].create(attrs, content);
const text = (s) => schema.text(s);

// Stub element: the surface a ParseRule's getAttrs actually touches.
const el = (tag, attrs = {}, textContent = "", child = null) => ({
  tagName: tag.toUpperCase(),
  getAttribute: (n) => (n in attrs ? attrs[n] : null),
  hasAttribute: (n) => n in attrs,
  textContent,
  querySelector: () => child,
});
const rule = (tag, context) =>
  CLIPBOARD_RULES.find((r) => r.tag === tag && (context === undefined || r.context === context));

console.log("coverage:");
ok("every node and mark in the schema has a clipboard spec", () => {
  // DOMSerializer throws on the first node type it has no entry for, and that
  // only happens when someone copies one — fail here instead.
  const nodes = Object.keys(schema.nodes).filter((n) => n !== "doc" && !CLIPBOARD_NODES[n]);
  const marks = Object.keys(schema.marks).filter((n) => !CLIPBOARD_MARKS[n]);
  if (nodes.length || marks.length) throw new Error("missing: " + [...nodes, ...marks].join(", "));
});

ok("the origin marker matches the one the native side looks for", () => {
  // RichHtml.OriginAttribute in Telegram/Common/RichHtml.cs. Only content carrying
  // this reopens the rich editor on paste, so the two spellings must agree.
  eq(ORIGIN_ATTRIBUTE, "data-telegram-rich", "attribute");
});

console.log("blocks:");
ok("heading writes h1..h6", () => {
  eq(CLIPBOARD_NODES.heading(node("heading", { size: 3 })), ["h3", {}, 0], "h3");
  // Out-of-range sizes still have to produce a tag that exists.
  eq(CLIPBOARD_NODES.heading(node("heading", { size: 9 })), ["h6", {}, 0], "clamped");
});
ok("footer is <footer>, the tag Android parses back to pageBlockFooter", () => {
  eq(CLIPBOARD_NODES.footer(node("footer")), ["footer", {}, 0], "footer");
});
ok("preformatted carries the language in the attribute Android reads", () => {
  eq(CLIPBOARD_NODES.preformatted(node("preformatted", { language: "python" })), ["pre", { language: "python" }, 0], "pre");
  eq(CLIPBOARD_NODES.preformatted(node("preformatted", { language: "" })), ["pre", { language: null }, 0], "no language");
});
ok("a quote of blocks is a plain <blockquote> with a trailing <cite>", () => {
  eq(CLIPBOARD_NODES.blockquote(node("blockquote", null, node("paragraph"))), ["blockquote", {}, 0], "blockquote");
  eq(CLIPBOARD_NODES.blockquote_credit(node("blockquote_credit")), ["cite", {}, 0], "cite");
});
ok("pull and expandable quotes are blockquotes Android can tell apart", () => {
  eq(CLIPBOARD_NODES.pullquote(node("pullquote", null, [node("pullquote_text"), node("pullquote_credit")])),
    ["blockquote", { class: "pull" }, 0], "pull");
  eq(CLIPBOARD_NODES.expandable_blockquote(node("expandable_blockquote", null, [node("expandable_text"), node("expandable_credit")])),
    ["blockquote", { class: "expandable" }, 0], "expandable");
  // Inline wrapper: a block child would make Android read the <cite> as a
  // quote-of-blocks author instead of the quote's own credit.
  eq(CLIPBOARD_NODES.pullquote_text(node("pullquote_text")), ["span", { class: "pm-quote-text" }, 0], "text region");
});
ok("checklist state travels as bare attributes", () => {
  eq(CLIPBOARD_NODES.list_item(node("list_item", { hasCheckbox: true, isChecked: true }, node("paragraph"))),
    ["li", { "data-checkbox": "", "data-checked": "" }, 0], "checked");
  // Android tests for presence, so an unchecked item must not carry the attribute at all.
  eq(CLIPBOARD_NODES.list_item(node("list_item", { hasCheckbox: true, isChecked: false }, node("paragraph"))),
    ["li", { "data-checkbox": "", "data-checked": null }, 0], "unchecked");
  eq(CLIPBOARD_NODES.list_item(node("list_item", null, node("paragraph"))),
    ["li", { "data-checkbox": null, "data-checked": null }, 0], "plain item");
});
ok("table cells write align/valign, not the editor's data-valign", () => {
  const cell = node("table_cell", { colspan: 2, rowspan: 1, align: "center", valign: "bottom" }, node("paragraph"));
  eq(CLIPBOARD_NODES.table_cell(cell), ["td", { colspan: 2, rowspan: null, align: "center", valign: "bottom" }, 0], "td");
  const plain = node("table_cell", { colspan: 1, rowspan: 1, align: "left", valign: "top" }, node("paragraph"));
  eq(CLIPBOARD_NODES.table_cell(plain), ["td", { colspan: null, rowspan: null, align: null, valign: null }, 0], "defaults omitted");
});

console.log("inline:");
ok("marks use Android's tags", () => {
  eq(CLIPBOARD_MARKS.strong(M.strong.create()), ["b", {}, 0], "bold");
  eq(CLIPBOARD_MARKS.spoiler(M.spoiler.create()), ["spoiler", {}, 0], "spoiler");
  eq(CLIPBOARD_MARKS.marked(M.marked.create()), ["mark", {}, 0], "marked");
  eq(CLIPBOARD_MARKS.link(M.link.create({ href: "https://t.me/", isCached: true })),
    ["a", { href: "https://t.me/", "data-cached": "1" }, 0], "link");
});
ok("a date is not a link to nowhere", () => {
  const spec = CLIPBOARD_MARKS.date_time(M.date_time.create({ unixTime: 1700000000 }));
  if (spec[1].href !== undefined) throw new Error("date_time must not write an href");
  eq(spec[1]["data-unix-time"], "1700000000", "unix time");
});
ok("custom emoji carries the document id, with its alt as the text", () => {
  eq(CLIPBOARD_NODES.custom_emoji(node("custom_emoji", { customEmojiId: "5321", alt: "🔥" })),
    ["animated-emoji", { "data-document-id": "5321" }, "🔥"], "animated-emoji");
});
ok("inline math degrades to its own source", () => {
  eq(CLIPBOARD_NODES.math_inline(node("math_inline", { latex: "x^2" })),
    ["span", { class: "pm-math-inline", "data-latex": "x^2" }, "x^2"], "math");
});
ok("a button keeps its label as text and the button in data-button", () => {
  const button = { "@type": "inlineButton", text: { "@type": "richTextPlain", text: "Open" } };
  const spec = CLIPBOARD_NODES.button(node("button", { button }));
  eq(spec[0], "span", "tag");
  eq(spec[2], "Open", "label");
  eq(JSON.parse(spec[1]["data-button"]), button, "round-trip");
});

ok("a button row travels as the whole TDLib block, and comes back", () => {
  const buttons = [{ "@type": "inlineButton", text: { "@type": "richTextPlain", text: "Go" } }];
  const spec = CLIPBOARD_NODES.button_row(node("button_row", { buttons, align: "center" }));
  const json = spec[1]["data-block"];
  const block = JSON.parse(json);
  // The native side rebuilds this with ClientJson, which needs a whole typed object.
  eq(block["@type"], "pageBlockButtonRow", "block type");
  eq(block.align["@type"], "pageBlockHorizontalAlignmentCenter", "align");
  eq(block.buttons, buttons, "buttons");
  eq(spec[2], ["span", { class: "pm-button" }, "Go"], "label");

  const attrs = rule("div.pm-button-row").getAttrs(el("div", { "data-block": json }));
  eq(attrs.align, "center", "align read back");
  eq(attrs.buttons, buttons, "buttons read back");
});

console.log("media:");
ok("a figure with no caption is a bare media tag", () => {
  const figure = node("figure", { kind: "photo", fileId: "12", width: 640, height: 480 });
  eq(CLIPBOARD_NODES.figure(figure),
    ["img", { src: "12", width: 640, height: 480, "data-spoiler": null, "data-kind": "photo" }], "img");
});
ok("a captioned figure wraps the media in <figure>", () => {
  const figure = node("figure", { kind: "video", fileId: "13", hasSpoiler: true }, node("caption", null, text("hi")));
  const spec = CLIPBOARD_NODES.figure(figure);
  eq(spec[0], "figure", "wrapper");
  eq(spec[2][0], "video", "media tag");
  eq(spec[2][1]["data-spoiler"], "1", "spoiler");
  eq(spec[3], 0, "caption hole");
});
ok("copying media registers it for the paste side", () => {
  mediaClipboard.clear();
  CLIPBOARD_NODES.figure(node("figure", { kind: "animation", fileId: "77" }));
  if (!mediaClipboard.has("77")) throw new Error("file id not registered");
  eq(mediaClipboard.get("77").kind, "animation", "kind");
});
ok("a map is self-describing — no registry needed", () => {
  const map = node("figure", { kind: "map", location: { latitude: 1.5, longitude: -2.25 }, zoom: 13, width: 100, height: 80 });
  eq(CLIPBOARD_NODES.figure(map), ["location", { lat: 1.5, long: -2.25, zoom: 13, w: 100, h: 80 }], "location");
});
ok("media this process didn't copy is dropped, not pasted broken", () => {
  mediaClipboard.clear();
  eq(rule("img").getAttrs(el("img", { src: "999" })), false, "unknown id");
  mediaClipboard.set("999", { kind: "photo", fileId: "999", src: "appassets://x" });
  const attrs = rule("img").getAttrs(el("img", { src: "999", "data-spoiler": "1" }));
  eq(attrs.fileId, "999", "resolved");
  eq(attrs.hasSpoiler, true, "spoiler read from the DOM");
});
ok("a <figure> takes its attrs from the media element inside it", () => {
  mediaClipboard.clear();
  mediaClipboard.set("5", { kind: "photo", fileId: "5" });
  const media = el("img", { src: "5" });
  eq(rule("figure").getAttrs(el("figure", {}, "", media)).fileId, "5", "from child");
  eq(rule("figure").getAttrs(el("figure", {}, "", null)), false, "no media child");
});

console.log("parse rules:");
ok("pre reads Android's language attribute and our own", () => {
  eq(rule("pre").getAttrs(el("pre", { language: "rust" })).language, "rust", "android");
  eq(rule("pre").getAttrs(el("pre", { "data-language": "rust" })).language, "rust", "ours");
  eq(rule("pre").preserveWhitespace, "full", "whitespace");
});
ok("li reads both the attribute and the class Android accepts", () => {
  eq(rule("li").getAttrs(el("li", { "data-checkbox": "", "data-checked": "" })), { hasCheckbox: true, isChecked: true }, "attrs");
  eq(rule("li").getAttrs(el("li", { class: "checkbox-item" })).hasCheckbox, true, "class");
  eq(rule("li").getAttrs(el("li")), { hasCheckbox: false, isChecked: false }, "plain");
});
ok("cite maps to the credit of whichever quote holds it", () => {
  eq(rule("cite", "blockquote/").node, "blockquote_credit", "blockquote");
  eq(rule("cite", "pullquote/").node, "pullquote_credit", "pullquote");
  eq(rule("cite", "expandable_blockquote/").node, "expandable_credit", "expandable");
});
ok("animated-emoji reads the id and takes the alt from its text", () => {
  const attrs = rule("animated-emoji").getAttrs(el("animated-emoji", { "data-document-id": "42" }, "🐱"));
  eq(attrs, { customEmojiId: "42", alt: "🐱" }, "emoji");
});
ok("a label with no button behind it stays text", () => {
  eq(rule("span.pm-button").getAttrs(el("span", {}, "Open")), false, "no data-button");
});
ok("the media rules are tried before the generic figure descent", () => {
  const ignore = CLIPBOARD_RULES.findIndex((r) => r.ignore && r.context === "figure/");
  const figure = CLIPBOARD_RULES.findIndex((r) => r.tag === "figure");
  if (ignore < 0 || ignore > figure) throw new Error("the media element inside a <figure> must be ignored first");
});

console.log("newlines:");
ok("<br> survives the parse and comes back as a newline", () => {
  // The marker stands in for "\n" across the parse, where ProseMirror would
  // otherwise collapse it to a space.
  const marker = String.fromCharCode(0xE000);
  const before = node("paragraph", null, [
    schema.text("one" + marker + "two", [M.strong.create()]),
  ]).content;
  const after = restoreNewlines(before);
  eq(after.firstChild.text, "one\ntwo", "newline");
  eq(after.firstChild.marks.length, 1, "marks kept");
});
ok("text with no marker is returned untouched", () => {
  const content = node("paragraph", null, schema.text("plain")).content;
  if (restoreNewlines(content) !== content) throw new Error("fragment rebuilt for nothing");
});
ok("nested content is rewritten too", () => {
  const marker = String.fromCharCode(0xE000);
  const quote = node("blockquote", null, node("paragraph", null, schema.text("a" + marker + "b")));
  const after = restoreNewlines(quote.content);
  eq(after.firstChild.firstChild.text, "a\nb", "nested");
});

console.log(`\n${pass} passed, ${fail} failed`);
process.exit(fail ? 1 : 0);
