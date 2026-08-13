// clipboard.js
// Copy and paste in the HTML shape Telegram Android writes and reads
// (org/telegram/ui/iv/RichHtml.java), so a selection survives a round trip
// between the two clients. ProseMirror's default clipboard DOM is the editor's
// own render DOM (pm-* classes and wrappers), which Android's parser flattens to
// plain paragraphs — every quote, list and table would arrive as loose text.
//
// Everything this adds on top of Android's format is additive, so its parser
// still reads ours:
//  - nodes it has no tag for (math, buttons, anchors, mentions) ride in data-*
//    attributes on a tag it degrades sensibly (a span keeps its text, a div
//    becomes a paragraph);
//  - checklist state is written out — Android's parser reads data-checkbox /
//    data-checked but its serializer never emits them;
//  - a nested list is written inside the <li> that owns it, which is what its
//    parser expects. Android writes it as a SIBLING of the <li> and then drops
//    it on the way back in, so its own nesting doesn't survive a round trip.
//    We read both shapes.
//
// Media never crosses the process boundary: the HTML carries only the file id,
// and the attrs behind it live in `mediaClipboard` until the next copy — the
// same trade Android makes with RichMediaClipboard. An id we can't resolve is
// dropped on paste rather than pasted as a broken figure.

import { DOMParser as PMDOMParser, DOMSerializer, Fragment, Slice } from "prosemirror-model";
import { schema } from "./schema.js";
import { alignName, alignType, richTextToPlain } from "./serialize.js";

// In-process registry of the media behind the last copy, keyed by file id.
// Replaced wholesale on every copy (see RichClipboardSerializer below).
export const mediaClipboard = new Map();

// A newline inside a block is <br> on the wire, but ProseMirror's DOM parser
// collapses whitespace in a text node, so "\n" would come back as a space.
// Carry it across the parse as a private-use character and restore it after.
const NEWLINE_MARKER = String.fromCharCode(0xE000);

// =============================================================================
// Serialize — ProseMirror slice -> Android-shaped DOM
// =============================================================================

const MEDIA_TAG = {
    photo: "img",
    video: "video",
    animation: "video",
    audio: "audio",
    voice: "audio",
};

// The media element itself, or null for a kind Android has no tag for
// (documents) — the figure then contributes only its caption, which is what
// Android does with a block it can't represent.
function mediaSpec(node) {
    const attrs = node.attrs;
    if (attrs.kind === "map") {
        const location = attrs.location || {};
        return ["location", {
            lat: location.latitude ?? null,
            long: location.longitude ?? null,
            zoom: attrs.zoom || null,
            w: attrs.width || null,
            h: attrs.height || null,
        }];
    }

    const tag = MEDIA_TAG[attrs.kind];
    if (!tag || !attrs.fileId) {
        return null;
    }

    // The copy side of the registry: the id in `src` means nothing on its own.
    mediaClipboard.set(String(attrs.fileId), attrs);

    return [tag, {
        src: String(attrs.fileId),
        width: attrs.width || null,
        height: attrs.height || null,
        "data-spoiler": attrs.hasSpoiler ? "1" : null,
        // Android has one tag for two kinds each; keep ours so a pasted
        // animation doesn't come back as a video.
        "data-kind": attrs.kind,
    }];
}

const buttonLabel = (button) => ["span", { class: "pm-button" }, richTextToPlain(button?.text) || ""];

const json = (value) => (value == null ? null : JSON.stringify(value));

const cellAttrs = (node) => ({
    colspan: node.attrs.colspan > 1 ? node.attrs.colspan : null,
    rowspan: node.attrs.rowspan > 1 ? node.attrs.rowspan : null,
    align: node.attrs.align && node.attrs.align !== "left" ? node.attrs.align : null,
    // `valign`, not the editor's own data-valign: this is the attribute Android reads.
    valign: node.attrs.valign && node.attrs.valign !== "top" ? node.attrs.valign : null,
});

export const CLIPBOARD_NODES = {
    text: (node) => node.text,

    paragraph: () => ["p", {}, 0],
    heading: (node) => ["h" + Math.min(6, Math.max(1, node.attrs.size | 0 || 2)), {}, 0],
    footer: () => ["footer", {}, 0],
    preformatted: (node) => ["pre", { language: node.attrs.language || null }, 0],
    divider: () => ["hr"],
    anchor: (node) => ["a", { "data-anchor": node.attrs.name }],

    // A quote holding blocks. Android nests the same way and reads the trailing
    // <cite> as the quote's author.
    blockquote: () => ["blockquote", {}, 0],
    blockquote_credit: () => ["cite", {}, 0],

    // Pull and expandable quotes hold a text region plus a credit region rather
    // than blocks. The text region needs a wrapper Android treats as inline, or
    // the <cite> would make it look like a quote-of-blocks; <span> qualifies.
    pullquote: () => ["blockquote", { class: "pull" }, 0],
    pullquote_text: () => ["span", { class: "pm-quote-text" }, 0],
    pullquote_credit: () => ["cite", {}, 0],
    expandable_blockquote: () => ["blockquote", { class: "expandable" }, 0],
    expandable_text: () => ["span", { class: "pm-quote-text" }, 0],
    expandable_credit: () => ["cite", {}, 0],

    bullet_list: () => ["ul", {}, 0],
    ordered_list: (node) => ["ol", { start: node.attrs.order === 1 ? null : node.attrs.order }, 0],
    list_item: (node) => ["li", {
        // Bare attributes: Android tests for presence, so data-checked="false"
        // would read as checked.
        "data-checkbox": node.attrs.hasCheckbox ? "" : null,
        "data-checked": node.attrs.hasCheckbox && node.attrs.isChecked ? "" : null,
    }, 0],

    details: (node) => ["details", { open: node.attrs.open ? "" : null }, 0],
    details_summary: () => ["summary", {}, 0],

    table: () => ["table", { border: "1" }, 0],
    table_row: () => ["tr", {}, 0],
    table_cell: (node) => ["td", cellAttrs(node), 0],
    table_header: (node) => ["th", cellAttrs(node), 0],

    figure: (node) => {
        const media = mediaSpec(node);
        const caption = node.firstChild;
        // <figure> only when there is a caption to hold — a bare media tag
        // otherwise, which is also the shape Android expects inside a gallery.
        if (!caption || caption.content.size === 0) {
            return media || ["figure", {}];
        }
        return media ? ["figure", {}, media, 0] : ["figure", {}, 0];
    },
    caption: () => ["figcaption", {}, 0],
    collage: () => ["div", { class: "collage" }, 0],
    slideshow: () => ["div", { class: "slideshow" }, 0],

    // No tag on either side. The source doubles as the text so the formula still
    // reads after a paste into anything else — Android does the same when it
    // can't render one.
    math_block: (node) => ["div", { class: "pm-math-block", "data-latex": node.attrs.latex }, node.attrs.latex || ""],
    math_inline: (node) => ["span", { class: "pm-math-inline", "data-latex": node.attrs.latex }, node.attrs.latex || ""],

    custom_emoji: (node) => ["animated-emoji", { "data-document-id": String(node.attrs.customEmojiId || "") }, node.attrs.alt || ""],

    // Buttons degrade to their label elsewhere; data-button carries the real one back.
    button: (node) => ["span", {
        class: "pm-button",
        "data-button": json(node.attrs.button),
    }, richTextToPlain(node.attrs.button?.text) || ""],
    // The whole block as TDLib JSON rather than its parts: the native side rebuilds
    // it with ClientJson when this is pasted into a chat field (see RichHtml.cs).
    button_row: (node) => ["div", {
        class: "pm-button-row",
        "data-block": json({
            "@type": "pageBlockButtonRow",
            buttons: node.attrs.buttons || [],
            align: alignType(node.attrs.align),
        }),
    }, ...(node.attrs.buttons || []).map(buttonLabel)],
};

export const CLIPBOARD_MARKS = {
    strong: () => ["b", {}, 0],
    em: () => ["i", {}, 0],
    underline: () => ["u", {}, 0],
    strike: () => ["s", {}, 0],
    code: () => ["code", {}, 0],
    spoiler: () => ["spoiler", {}, 0],
    marked: () => ["mark", {}, 0],
    subscript: () => ["sub", {}, 0],
    superscript: () => ["sup", {}, 0],
    link: (mark) => ["a", { href: mark.attrs.href, "data-cached": mark.attrs.isCached ? "1" : null }, 0],
    // No href: Android reads <a> for its url only, so a date arrives as plain
    // text there instead of a link to nowhere.
    date_time: (mark) => ["a", { class: "pm-datetime", "data-unix-time": String(mark.attrs.unixTime | 0) }, 0],
    mention_name: (mark) => ["a", { class: "pm-mention", "data-user-id": String(mark.attrs.userId || "") }, 0],
};

class RichClipboardSerializer extends DOMSerializer {
    constructor(nodes, marks) {
        super(nodes, marks);
        this.depth = 0;
    }

    // Called recursively for every node's content, so the once-per-copy work is
    // guarded by the depth counter.
    serializeFragment(fragment, options = {}, target) {
        const outermost = this.depth === 0;
        if (outermost) {
            mediaClipboard.clear();
        }

        this.depth++;
        try {
            const dom = super.serializeFragment(fragment, options, target);
            if (outermost) {
                breakNewlines(dom, options.document || document);
                markOrigin(dom);
            }
            return dom;
        } finally {
            this.depth--;
        }
    }
}

// Stamps the copy as this app's own. Pasting into a chat field only takes the rich
// path — and only reopens the editor, a separate window and a premium feature — for
// content the app itself wrote; a heading copied from a web page stays plain text.
// Kept in sync with RichHtml.OriginAttribute on the native side.
export const ORIGIN_ATTRIBUTE = "data-telegram-rich";

function markOrigin(root) {
    for (let node = root.firstChild; node; node = node.nextSibling) {
        if (node.nodeType === 1) {
            node.setAttribute(ORIGIN_ATTRIBUTE, "1");
            return;
        }
    }
}

// A newline inside a block is a <br> in this format (Android's escape() writes
// one, and its parser turns it back into "\n").
function breakNewlines(root, doc) {
    const texts = [];
    const walker = doc.createTreeWalker(root, 4 /* SHOW_TEXT */);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        if (node.nodeValue.indexOf("\n") >= 0) {
            texts.push(node);
        }
    }

    for (const text of texts) {
        const parts = text.nodeValue.split("\n");
        const fragment = doc.createDocumentFragment();
        for (let i = 0; i < parts.length; i++) {
            if (i > 0) {
                fragment.appendChild(doc.createElement("br"));
            }
            if (parts[i]) {
                fragment.appendChild(doc.createTextNode(parts[i]));
            }
        }
        text.parentNode.replaceChild(fragment, text);
    }
}

export const clipboardSerializer = new RichClipboardSerializer(CLIPBOARD_NODES, CLIPBOARD_MARKS);

// =============================================================================
// Parse — Android-shaped DOM -> ProseMirror slice
// =============================================================================

const int = (value) => {
    const n = parseInt(value, 10);
    return Number.isFinite(n) ? n : 0;
};

const float = (value) => {
    const n = parseFloat(value);
    return Number.isFinite(n) ? n : 0;
};

const parseJson = (value) => {
    if (!value) return null;
    try {
        return JSON.parse(value);
    } catch {
        return null;
    }
};

function mapAttrs(el) {
    return {
        kind: "map",
        location: { "@type": "location", latitude: float(el.getAttribute("lat")), longitude: float(el.getAttribute("long")) },
        zoom: int(el.getAttribute("zoom")),
        width: int(el.getAttribute("w")),
        height: int(el.getAttribute("h")),
    };
}

// false (a rejected rule) for media this process didn't copy: the id is all the
// clipboard carries, so there is nothing to paste.
function mediaAttrs(el, fallbackKind) {
    if (el.tagName.toLowerCase() === "location") {
        return mapAttrs(el);
    }

    const saved = mediaClipboard.get(el.getAttribute("src") || "");
    if (!saved) {
        return false;
    }

    return {
        ...saved,
        kind: el.getAttribute("data-kind") || saved.kind || fallbackKind,
        hasSpoiler: el.hasAttribute("data-spoiler"),
    };
}

const MEDIA_SELECTOR = "img, video, audio, location";

export const CLIPBOARD_RULES = [
    // Inside a <figure> the media element is the figure's own attrs, already read
    // by the rule below — parsing it again would nest a figure in a figure.
    { tag: MEDIA_SELECTOR, context: "figure/", ignore: true },
    {
        tag: "figure",
        node: "figure",
        getAttrs: (dom) => {
            const media = dom.querySelector(MEDIA_SELECTOR);
            return media ? mediaAttrs(media, "photo") : false;
        },
    },
    { tag: "img", node: "figure", getAttrs: (dom) => mediaAttrs(dom, "photo") },
    { tag: "video", node: "figure", getAttrs: (dom) => mediaAttrs(dom, "video") },
    { tag: "audio", node: "figure", getAttrs: (dom) => mediaAttrs(dom, "audio") },
    { tag: "location", node: "figure", getAttrs: (dom) => mediaAttrs(dom, "map") },
    { tag: "div.collage", node: "collage" },
    { tag: "div.slideshow", node: "slideshow" },

    { tag: "blockquote.pull", node: "pullquote" },
    { tag: "blockquote.expandable", node: "expandable_blockquote" },
    { tag: "span.pm-quote-text", context: "pullquote/", node: "pullquote_text" },
    { tag: "span.pm-quote-text", context: "expandable_blockquote/", node: "expandable_text" },
    { tag: "cite", context: "pullquote/", node: "pullquote_credit" },
    { tag: "cite", context: "expandable_blockquote/", node: "expandable_credit" },
    { tag: "cite", context: "blockquote/", node: "blockquote_credit" },

    { tag: "footer", node: "footer" },
    {
        tag: "pre",
        node: "preformatted",
        preserveWhitespace: "full",
        getAttrs: (dom) => ({ language: dom.getAttribute("language") || dom.getAttribute("data-language") || "" }),
    },
    {
        tag: "li",
        node: "list_item",
        getAttrs: (dom) => ({
            hasCheckbox: dom.hasAttribute("data-checkbox") || (dom.getAttribute("class") || "").toLowerCase().includes("checkbox"),
            isChecked: dom.hasAttribute("data-checked"),
        }),
    },

    { tag: "spoiler", mark: "spoiler" },
    {
        tag: "animated-emoji",
        node: "custom_emoji",
        getAttrs: (dom) => ({
            customEmojiId: dom.getAttribute("data-document-id") || "",
            alt: dom.textContent || "",
        }),
    },
    { tag: "span.pm-math-inline", node: "math_inline", getAttrs: (dom) => ({ latex: dom.getAttribute("data-latex") || dom.textContent || "" }) },
    { tag: "div.pm-math-block", node: "math_block", getAttrs: (dom) => ({ latex: dom.getAttribute("data-latex") || dom.textContent || "" }) },
    {
        tag: "span.pm-button",
        node: "button",
        getAttrs: (dom) => {
            const button = parseJson(dom.getAttribute("data-button"));
            // A label with no button behind it is just text — let it fall through.
            return button ? { button } : false;
        },
    },
    {
        tag: "div.pm-button-row",
        node: "button_row",
        getAttrs: (dom) => {
            const block = parseJson(dom.getAttribute("data-block"));
            return block?.buttons ? { buttons: block.buttons, align: alignName(block.align) } : false;
        },
    },
    { tag: "a[data-anchor]", node: "anchor", getAttrs: (dom) => ({ name: dom.getAttribute("data-anchor") || "" }) },
    { tag: "a.pm-datetime", mark: "date_time", getAttrs: (dom) => ({ unixTime: int(dom.getAttribute("data-unix-time")) }) },
    { tag: "a.pm-mention", mark: "mention_name", getAttrs: (dom) => ({ userId: dom.getAttribute("data-user-id") || "" }) },
    { tag: "a[href]", mark: "link", getAttrs: (dom) => ({ href: dom.getAttribute("href"), isCached: dom.hasAttribute("data-cached") }) },
];

const baseParser = PMDOMParser.fromSchema(schema);
const richParser = new PMDOMParser(schema, [...CLIPBOARD_RULES, ...baseParser.rules]);

// Rewrites the shapes a parse rule can't express, in place. The DOM here is the
// detached tree ProseMirror built from the clipboard HTML, so mutating it is safe.
export function normalizeClipboardDom(dom) {
    if (!dom || !dom.querySelectorAll) {
        return dom;
    }

    const doc = dom.ownerDocument || document;

    for (const br of Array.from(dom.querySelectorAll("br"))) {
        br.parentNode?.replaceChild(doc.createTextNode(NEWLINE_MARKER), br);
    }

    // Android writes a nested list as a sibling of the <li> it belongs to.
    for (const list of Array.from(dom.querySelectorAll("ul, ol"))) {
        const parent = list.parentElement;
        if (!parent || !/^(ul|ol)$/i.test(parent.tagName)) {
            continue;
        }
        let li = list.previousElementSibling;
        while (li && li.tagName.toLowerCase() !== "li") {
            li = li.previousElementSibling;
        }
        if (li) {
            li.appendChild(list);
        }
    }

    // prosemirror-tables reads the vertical alignment from data-valign.
    for (const cell of Array.from(dom.querySelectorAll("td[valign], th[valign]"))) {
        if (!cell.hasAttribute("data-valign")) {
            cell.setAttribute("data-valign", cell.getAttribute("valign"));
        }
    }

    return dom;
}

// Exported for the tests: the fiddliest pure step of the paste path.
export function restoreNewlines(fragment) {
    let changed = false;
    const out = [];

    fragment.forEach((node) => {
        if (node.isText && node.text.indexOf(NEWLINE_MARKER) >= 0) {
            out.push(node.type.schema.text(node.text.split(NEWLINE_MARKER).join("\n"), node.marks));
            changed = true;
            return;
        }
        if (node.content.size > 0) {
            const inner = restoreNewlines(node.content);
            if (inner !== node.content) {
                out.push(node.copy(inner));
                changed = true;
                return;
            }
        }
        out.push(node);
    });

    return changed ? Fragment.fromArray(out) : fragment;
}

export const clipboardParser = {
    parseSlice(dom, options) {
        const slice = richParser.parseSlice(normalizeClipboardDom(dom), options);
        const content = restoreNewlines(slice.content);
        return content === slice.content ? slice : new Slice(content, slice.openStart, slice.openEnd);
    },
    parse(dom, options) {
        const node = richParser.parse(normalizeClipboardDom(dom), options);
        const content = restoreNewlines(node.content);
        return content === node.content ? node : node.copy(content);
    },
};
