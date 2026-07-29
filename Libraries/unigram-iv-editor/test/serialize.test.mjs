// serialize.test.mjs — fromTDLib/toTDLib tests with no browser.
// fromTDLib/toTDLib build/walk ProseMirror nodes only (no DOM), so they run in
// plain Node. We bundle schema+serialize to a temp ESM module via esbuild.
import { build } from "esbuild";
import { writeFileSync, mkdirSync } from "node:fs";

mkdirSync("dist", { recursive: true });
writeFileSync(
  "dist/_test-entry.js",
  'export { schema } from "../src/schema.js";\nexport { fromTDLib, toTDLib, toInputBlocks } from "../src/serialize.js";\nexport { mosaicBase, scaleMosaic } from "../src/mosaic.js";\n'
);
await build({
  entryPoints: ["dist/_test-entry.js"],
  bundle: true,
  format: "esm",
  platform: "node",
  outfile: "dist/_test.mjs",
  logLevel: "silent",
});
const { schema, fromTDLib, toTDLib, toInputBlocks, mosaicBase, scaleMosaic } = await import("../dist/_test.mjs?" + Date.now());

const RT = (t) => ({ "@type": "richTextPlain", text: t });
let pass = 0, fail = 0;
function ok(name, fn) {
  try { fn(); console.log("  PASS  " + name); pass++; }
  catch (e) { console.log("  FAIL  " + name + "  ->  " + e.message); fail++; }
}
const load = (blocks) => { const d = fromTDLib(blocks, schema); d.check(); return d; };

console.log("hardening (these used to throw on valid TDLib data):");
ok("empty blocks -> non-empty doc", () => load([]));
ok("empty blockquote", () => load([{ "@type": "pageBlockBlockQuote", blocks: [] }]));
ok("empty details body", () => load([{ "@type": "pageBlockDetails", is_open: true, header: RT("H"), blocks: [] }]));
ok("list item starting with non-paragraph", () =>
  load([{ "@type": "pageBlockList", items: [
    { "@type": "pageBlockListItem", blocks: [{ "@type": "pageBlockMathematicalExpression", expression: "x" }], has_checkbox: false, is_checked: false, value: 0, type: "" },
  ] }]));

console.log("structure:");
ok("table cell holds exactly one paragraph", () => {
  const d = load([{ "@type": "pageBlockTable", cells: [[
    { "@type": "pageBlockTableCell", text: RT("A"), is_header: true, colspan: 1, rowspan: 1 },
  ]] }]);
  const cell = d.firstChild.firstChild.firstChild; // table > row > cell
  if (cell.childCount !== 1 || cell.firstChild.type.name !== "paragraph")
    throw new Error("cell content is not a single paragraph");
});
ok("richTextDateTime round-trips with its unix_time", () => {
  const d = load([{ "@type": "pageBlockParagraph", text: { "@type": "richTexts", texts: [
    RT("on "), { "@type": "richTextDateTime", text: RT("June 22"), unix_time: 1782086400, formatting_type: null },
  ] } }]);
  const out = toTDLib(d);
  const dt = out[0].text.texts.find((x) => x["@type"] === "richTextDateTime");
  if (!dt) throw new Error("richTextDateTime not reconstructed on save");
  if (dt.unix_time !== 1782086400) throw new Error("unix_time lost: " + dt.unix_time);
  if (dt.text?.text !== "June 22") throw new Error("inner text lost");
});

ok("pageBlockMap round-trips with all properties preserved", () => {
  const map = { "@type": "pageBlockMap", zoom: 14, width: 640, height: 360,
    location: { "@type": "location", latitude: 55.7558, longitude: 37.6173, horizontal_accuracy: 12.5 },
    caption: { "@type": "pageBlockCaption", text: RT("Red Square"), credit: null } };
  const out = toTDLib(load([map]));
  const b = out[0];
  if (b["@type"] !== "pageBlockMap") throw new Error("not a map: " + b["@type"]);
  if (b.zoom !== 14 || b.width !== 640 || b.height !== 360) throw new Error("zoom/size lost");
  if (JSON.stringify(b.location) !== JSON.stringify(map.location)) throw new Error("location not preserved");
  if (b.caption?.text?.text !== "Red Square") throw new Error("caption lost");
});

ok("pageBlockPullQuote round-trips text + credit", () => {
  const pq = { "@type": "pageBlockPullQuote", text: RT("Set apart"), credit: RT("Author") };
  const out = toTDLib(load([pq]));
  const b = out[0];
  if (b["@type"] !== "pageBlockPullQuote") throw new Error("not a pull quote: " + b["@type"]);
  if (b.text?.text !== "Set apart") throw new Error("text lost");
  if (b.credit?.text !== "Author") throw new Error("credit lost");
});

ok("pageBlockCollage round-trips blocks + caption, drops inner captions", () => {
  // inner photos carry a caption on input — it must be dropped (only the group keeps one)
  const photo = (id) => ({ "@type": "pageBlockPhoto", file_id: id, has_spoiler: false, caption: { "@type": "pageBlockCaption", text: RT("inner"), credit: null } });
  const col = { "@type": "pageBlockCollage", caption: { "@type": "pageBlockCaption", text: RT("Gallery"), credit: null }, blocks: [photo("a"), photo("b")] };
  const b = toTDLib(load([col]))[0];
  if (b["@type"] !== "pageBlockCollage") throw new Error("not a collage: " + b["@type"]);
  if ((b.blocks || []).length !== 2) throw new Error("blocks lost: " + (b.blocks || []).length);
  if (b.blocks[0]["@type"] !== "pageBlockPhoto" || b.blocks[0].file_id !== "a") throw new Error("inner photo lost");
  if (b.blocks.some((x) => x.caption != null)) throw new Error("inner caption not dropped");
  if (b.caption?.text?.text !== "Gallery") throw new Error("group caption lost");
});
ok("pageBlockSlideshow round-trips (incl. video block)", () => {
  const ss = { "@type": "pageBlockSlideshow", caption: { "@type": "pageBlockCaption", text: RT("Slides"), credit: null }, blocks: [
    { "@type": "pageBlockPhoto", file_id: "p", has_spoiler: false, caption: null },
    { "@type": "pageBlockVideo", file_id: "v", need_autoplay: true, is_looped: true, has_spoiler: false, caption: null },
  ] };
  const b = toTDLib(load([ss]))[0];
  if (b["@type"] !== "pageBlockSlideshow") throw new Error("not a slideshow: " + b["@type"]);
  if ((b.blocks || []).length !== 2) throw new Error("blocks lost");
  if (b.blocks[1]["@type"] !== "pageBlockVideo" || b.blocks[1].need_autoplay !== true) throw new Error("video block not preserved");
  if (b.blocks.some((x) => x.caption != null)) throw new Error("inner caption not dropped");
  if (b.caption?.text?.text !== "Slides") throw new Error("caption lost");
});

ok("pageBlockAnchor round-trips (loads as an anchor, not a paragraph)", () => {
  const b = toTDLib(load([{ "@type": "pageBlockAnchor", name: "section-2" }]))[0];
  if (b["@type"] !== "pageBlockAnchor") throw new Error("not an anchor: " + b["@type"]);
  if (b.name !== "section-2") throw new Error("name lost: " + b.name);
});

ok("media: structured photo object -> file_id (largest size) + url on save", () => {
  const photo = {
    "@type": "pageBlockPhoto",
    photo: { "@type": "photo", sizes: [
      { "@type": "photoSize", type: "s", width: 100, height: 80, photo: { "@type": "file", id: 42 } },
      { "@type": "photoSize", type: "y", width: 800, height: 600, photo: { "@type": "file", id: 43 } },
    ] },
    url: "appassets://photo/43",
    has_spoiler: false,
    caption: { "@type": "pageBlockCaption", text: RT("Cap"), credit: null },
  };
  const b = toTDLib(load([photo]))[0];
  if (b["@type"] !== "pageBlockPhoto") throw new Error("not a photo");
  if (b.file_id !== 43) throw new Error("file id not extracted from largest size: " + b.file_id);
  if (b.photo) throw new Error("structured photo object should be gone");
  if (b.url !== "appassets://photo/43") throw new Error("url lost: " + b.url);
  if (b.caption?.text?.text !== "Cap") throw new Error("caption lost");
});
ok("media: structured video -> file_id, autoplay/loop preserved", () => {
  const video = {
    "@type": "pageBlockVideo",
    video: { "@type": "video", width: 640, height: 360, video: { "@type": "file", id: 7 } },
    need_autoplay: true, is_looped: true, has_spoiler: false, caption: null,
  };
  const b = toTDLib(load([video]))[0];
  if (b["@type"] !== "pageBlockVideo" || b.file_id !== 7) throw new Error("video file id lost: " + b.file_id);
  if (b.need_autoplay !== true || b.is_looped !== true) throw new Error("video flags lost");
});

console.log("round-trip:");
ok("blockquote credit: preserved when present, dropped when absent", () => {
  const withCredit = { "@type": "pageBlockBlockQuote", blocks: [{ "@type": "pageBlockParagraph", text: RT("Quote") }], credit: RT("Author") };
  const a = toTDLib(load([withCredit]))[0];
  if (a.credit?.text !== "Author") throw new Error("credit not preserved: " + JSON.stringify(a.credit));

  const noCredit = { "@type": "pageBlockBlockQuote", blocks: [{ "@type": "pageBlockParagraph", text: RT("Quote") }], credit: null };
  const b = toTDLib(load([noCredit]))[0];
  if (b.credit != null) throw new Error("credit should stay null when absent: " + JSON.stringify(b.credit));
});
ok("pageBlockFooter round-trips (and isn't flattened to a paragraph)", () => {
  const footer = { "@type": "pageBlockFooter", footer: RT("Footer line") };
  const out = toTDLib(load([footer]))[0];
  if (out["@type"] !== "pageBlockFooter") throw new Error("expected pageBlockFooter, got " + out["@type"]);
  if (out.footer?.text !== "Footer line") throw new Error("footer text not preserved: " + JSON.stringify(out.footer));
  const input = toInputBlocks(load([footer]))[0];
  if (input["@type"] !== "inputPageBlockFooter") throw new Error("expected inputPageBlockFooter, got " + input["@type"]);
});
ok("marks survive fromTDLib -> toTDLib", () => {
  const d = fromTDLib([{ "@type": "pageBlockParagraph", text: { "@type": "richTexts", texts: [
    RT("hi "), { "@type": "richTextBold", text: RT("bold") },
  ] } }], schema);
  const out = toTDLib(d);
  const bold = out[0].text.texts.find((x) => x["@type"] === "richTextBold");
  if (!bold) throw new Error("bold mark lost");
});
ok("entities are NOT auto-detected on save (TDLib detects them)", () => {
  // @mention stays plain text — no richTextMention produced by the editor.
  const d = fromTDLib([{ "@type": "pageBlockParagraph", text: RT("ping @durov now") }], schema);
  const out = toTDLib(d);
  const json = JSON.stringify(out);
  if (json.includes("richTextMention")) throw new Error("entity was detected on serialize; should be left to TDLib");
  if (!json.includes("ping @durov now")) throw new Error("plain text not preserved");
});

console.log("input blocks (richMessageSourceBlocks):");
ok("inputPageBlock* type names + renamed nested vectors", () => {
  const d = load([
    { "@type": "pageBlockParagraph", text: RT("p") },
    { "@type": "pageBlockList", items: [
      { "@type": "pageBlockListItem", blocks: [{ "@type": "pageBlockParagraph", text: RT("li") }], has_checkbox: false, is_checked: false, value: 0, type: "" },
    ] },
    { "@type": "pageBlockDetails", is_open: true, header: RT("H"), blocks: [{ "@type": "pageBlockParagraph", text: RT("d") }] },
  ]);
  const out = toInputBlocks(d);
  if (out[0]["@type"] !== "inputPageBlockParagraph") throw new Error("paragraph type: " + out[0]["@type"]);
  if (out[1]["@type"] !== "inputPageBlockList") throw new Error("list type");
  const item = out[1].items[0];
  if (item["@type"] !== "inputPageBlockListItem") throw new Error("list item type");
  if (!Array.isArray(item.blocks) || "page_blocks" in item) throw new Error("list item must use blocks");
  if ("label" in item) throw new Error("list item must drop label");
  if (!Array.isArray(out[2].blocks) || "page_blocks" in out[2]) throw new Error("details must use blocks");
});
ok("inputPageBlockPhoto: display photo -> inputPhoto (first size thumb, last main)", () => {
  const photo = {
    "@type": "pageBlockPhoto",
    photo: { "@type": "photo", sizes: [
      { "@type": "photoSize", type: "s", width: 100, height: 80, photo: { "@type": "file", id: 1, remote: { "@type": "remoteFile", id: "rem-small" } } },
      { "@type": "photoSize", type: "y", width: 800, height: 600, photo: { "@type": "file", id: 2, remote: { "@type": "remoteFile", id: "rem-main" } } },
    ] },
    has_spoiler: true, caption: { "@type": "pageBlockCaption", text: RT("c"), credit: null },
  };
  const b = toInputBlocks(load([photo]))[0];
  if (b["@type"] !== "inputPageBlockPhoto") throw new Error("type: " + b["@type"]);
  if (b.has_spoiler !== true) throw new Error("has_spoiler lost");
  if (b.photo?.["@type"] !== "inputPhoto") throw new Error("no inputPhoto");
  if (b.photo.photo?.["@type"] !== "inputFileRemote" || b.photo.photo.id !== "rem-main") throw new Error("main not last size: " + JSON.stringify(b.photo.photo));
  if (b.photo.thumbnail?.thumbnail?.id !== "rem-small") throw new Error("thumb not first size");
  if (b.photo.width !== 800 || b.photo.height !== 600) throw new Error("main size dims");
  if (b.caption?.text?.text !== "c") throw new Error("caption lost");
});

console.log("mosaic (collage album layout):");
ok("two landscape photos stack into two equal rows", () => {
  // "ww" with tall average aspect -> two full-width rows (top/bottom)
  const base = mosaicBase([{ width: 1280, height: 720 }, { width: 1280, height: 720 }]);
  if (base.rects.length !== 2) throw new Error("expected 2 rects");
  const [a, b] = base.rects;
  if (a.width !== base.width || b.width !== base.width) throw new Error("rows should be full width");
  if (!(b.y >= a.y + a.height - 1)) throw new Error("second row should be below the first");
});
ok("two portrait photos sit side by side, equal width", () => {
  const base = mosaicBase([{ width: 720, height: 1280 }, { width: 720, height: 1280 }]);
  const [a, b] = base.rects;
  if (Math.abs(a.width - b.width) > 1) throw new Error("columns should be equal width");
  if (!(b.x >= a.x + a.width - 1)) throw new Error("second column should be to the right");
});
ok("scaleMosaic keeps aspect of total box and inserts inner gaps", () => {
  const base = mosaicBase([{ width: 720, height: 1280 }, { width: 720, height: 1280 }]);
  const scaled = scaleMosaic(base, 300);
  if (Math.abs(scaled.width - 300) > 1) throw new Error("scaled width should match request");
  // the right column isn't on the left edge -> shifted by the 1px gap
  if (!(scaled.rects[1].x > base.rects[1].x * (300 / base.width) - 0.001)) throw new Error("inner gap not applied");
});

console.log(`\n${pass} passed, ${fail} failed`);
process.exit(fail ? 1 : 0);
