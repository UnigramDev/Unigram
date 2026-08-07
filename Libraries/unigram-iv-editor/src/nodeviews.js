// nodeviews.js
// NodeViews render the non-text nodes. Each is a factory (node, view, getPos).
//
// Production note on emoji/media: inside a NodeView you are in Chromium, so
// Unigram's native rlottie / FFmpeg pipeline cannot composite here. The edit
// surface only needs to show WHICH atom is present and let you place/select/
// delete it — render a STATIC preview (first frame / thumbnail). The real
// animated rendering stays in the native read/display view. Bytes come from
// TDLib's cache exposed via WebView2 SetVirtualHostNameToFolderMapping, so
// `src` is just an appassets:// URL. This demo uses inline SVG/glyphs so the
// file stays fully offline.

import { NodeSelection, TextSelection } from "prosemirror-state";
import { mosaicBase, scaleMosaic } from "./mosaic.js";

const svgPlaceholder = (kind, w, h) => {
  const label = kind.toUpperCase();
  const ar = w && h ? `${w} / ${h}` : "16 / 9";
  const svg = encodeURIComponent(
    `<svg xmlns='http://www.w3.org/2000/svg' width='320' height='180'>
       <defs><linearGradient id='g' x1='0' y1='0' x2='1' y2='1'>
         <stop offset='0' stop-color='#2b3a4a'/><stop offset='1' stop-color='#16202b'/>
       </linearGradient></defs>
       <rect width='320' height='180' fill='url(#g)'/>
       <text x='50%' y='50%' font-family='monospace'
         font-size='15' text-anchor='middle' dominant-baseline='middle'
         fill='#7b93ad'>${label}</text>
     </svg>`.replace(/\s+/g, " ")
  );
  return { url: `data:image/svg+xml,${svg}`, ar };
};

// --- custom emoji (inline atom) --------------------------------------------
// An EMPTY, invisible 20x20 placeholder: it only reserves layout space and
// carries the emoji id (for the native-overlay position tracker). The native
// side paints the real (animated) emoji on top — nothing is drawn or fetched
// here. CSS makes it invisible and non-clickable.
export function customEmojiView(node) {
  const dom = document.createElement("span");
  dom.className = "pm-emoji";
  dom.contentEditable = "false";
  const render = (n) => {
    dom.dataset.emojiId = n.attrs.customEmojiId || "";
  };
  render(node);
  return {
    dom,
    update(n) {
      if (n.type.name !== "custom_emoji") return false;
      render(n);
      return true;
    },
  };
}

// --- math (inline + block atoms) -------------------------------------------
// Demo renders the LaTeX source in a styled chip. Swap renderMath() for KaTeX
// or a bridge call to your MicroTeX/Direct2D backend in production.
// Double-clicking a formula selects the math node and calls onEdit(latex, rect) — the
// host shows its own LaTeX editor positioned at that rect and writes the result back via
// the setMathExpression command (mirrors the preformatted language flow). No inline
// web-view prompt.
export function makeMathView(block, onEdit) {
  return (node, view, getPos) => {
    const dom = document.createElement(block ? "div" : "span");
    dom.className = block ? "pm-math-block" : "pm-math-inline";
    dom.contentEditable = "false";
    const render = (n) => {
      dom.textContent = n.attrs.latex || (block ? "\\,(empty)\\," : "x");
    };
    render(node);
    dom.addEventListener("dblclick", (e) => {
      e.preventDefault();
      const pos = getPos();
      if (pos == null) return;
      // Select the math node first so setMathExpression (which targets the selected
      // node) always applies to this formula.
      view.dispatch(view.state.tr.setSelection(NodeSelection.create(view.state.doc, pos)));
      view.focus();
      onEdit(node.attrs.latex || "", dom.getBoundingClientRect());
    });
    return {
      dom,
      update(n) {
        if (n.type.name !== (block ? "math_block" : "math_inline")) return false;
        node = n;
        render(n);
        return true;
      },
    };
  };
}

// --- details (collapsible) -------------------------------------------------
// Toggle lives OUTSIDE contentDOM so it isn't part of the editable flow.
// CSS hides everything after <summary> when data-open="false".
export function detailsView(node, view, getPos) {
  const dom = document.createElement("div");
  dom.className = "pm-details";
  dom.setAttribute("data-open", String(node.attrs.open));

  const bar = document.createElement("div");
  bar.className = "pm-details-bar";
  bar.contentEditable = "false";
  const btn = document.createElement("button");
  btn.type = "button";
  btn.className = "pm-details-toggle";
  btn.setAttribute("aria-label", "Toggle section");
  btn.textContent = node.attrs.open ? "▾" : "▸";
  btn.addEventListener("mousedown", (e) => {
    e.preventDefault();
    const pos = getPos();
    if (pos == null) return;
    view.dispatch(
      view.state.tr.setNodeMarkup(pos, undefined, { open: !node.attrs.open })
    );
  });
  bar.appendChild(btn);

  const content = document.createElement("div");
  content.className = "pm-details-content";

  dom.appendChild(bar);
  dom.appendChild(content);

  return {
    dom,
    contentDOM: content,
    update(n) {
      if (n.type.name !== "details") return false;
      node = n;
      dom.setAttribute("data-open", String(n.attrs.open));
      btn.textContent = n.attrs.open ? "▾" : "▸";
      return true;
    },
    ignoreMutation(m) {
      return bar.contains(m.target);
    },
  };
}

// --- figure (media + editable caption) -------------------------------------
export function figureView(node, view, getPos) {
  const dom = document.createElement("figure");
  dom.className = "pm-figure";

  const media = document.createElement("div");
  media.className = "pm-figure-media";
  media.contentEditable = "false";

  const render = (n) => {
    media.innerHTML = "";
    media.setAttribute("data-kind", n.attrs.kind);
    const { url, ar } = svgPlaceholder(n.attrs.kind, n.attrs.width, n.attrs.height);
    const img = document.createElement("img");
    img.src = n.attrs.src || url;
    img.style.aspectRatio = ar;
    media.appendChild(img);
    if (n.attrs.hasSpoiler) {
      const sp = document.createElement("div");
      sp.className = "pm-figure-spoiler";
      sp.textContent = "spoiler";
      media.appendChild(sp);
    }
    const tag = document.createElement("span");
    tag.className = "pm-figure-tag";
    tag.textContent = n.attrs.kind;
    media.appendChild(tag);
  };
  render(node);

  // Click the media region to select the whole figure node.
  media.addEventListener("mousedown", (e) => {
    e.preventDefault();
    const pos = getPos();
    if (pos == null) return;
    view.dispatch(view.state.tr.setSelection(NodeSelection.create(view.state.doc, pos)));
    view.focus();
  });

  const content = document.createElement("div");
  content.className = "pm-figure-caption-wrap";

  dom.appendChild(media);
  dom.appendChild(content);

  return {
    dom,
    contentDOM: content,
    update(n) {
      if (n.type.name !== "figure") return false;
      node = n;
      render(n);
      return true;
    },
    ignoreMutation(m) {
      // ignore the inline-style positioning the collage layout writes onto us
      return m.type === "attributes" || media.contains(m.target);
    },
    selectNode() {
      dom.classList.add("pm-selected");
    },
    deselectNode() {
      dom.classList.remove("pm-selected");
    },
  };
}

// --- collage (mosaic album layout) -----------------------------------------
// Lays out the figure children using Telegram's album tiling (see mosaic.js),
// matching the native PageBlockCollageContent. The mosaic isn't purely row-based
// (some layouts have cells that span rows/columns), so we map the scaled rects
// onto a CSS GRID: the rects' distinct x/y edges become the grid lines, and each
// figure is placed by grid-column/grid-row span. Grid children stay IN FLOW, so
// ProseMirror's cursor/position mapping and node selection keep working. The
// group caption spans all columns on a final auto row. Re-runs on doc + width.
export function collageView(node) {
  const dom = document.createElement("div");
  dom.className = "pm-collage";

  let raf = 0;
  let retries = 0;
  const relayout = () => {
    raf = 0;
    const figures = [...dom.children].filter((c) => c.classList && c.classList.contains("pm-figure"));
    const width = dom.clientWidth;
    if ((!width || !figures.length) && retries++ < 120) { schedule(); return; }
    if (!figures.length) return;
    retries = 0;

    const sizes = [];
    node.forEach((child) => {
      if (child.type.name !== "figure") return;
      const w = child.attrs.width || 0, h = child.attrs.height || 0;
      sizes.push(w > 0 && h > 0 ? { width: w, height: h } : { width: 1, height: 1 });
    });

    const base = mosaicBase(sizes);
    const scaled = width ? scaleMosaic(base, width) : base;
    const rects = scaled.rects.slice(0, figures.length).map((r) => ({
      x0: Math.round(r.x), y0: Math.round(r.y),
      x1: Math.round(r.x + r.width), y1: Math.round(r.y + r.height),
    }));

    // distinct edges -> grid lines; track sizes are the gaps between them
    const xs = [...new Set(rects.flatMap((r) => [r.x0, r.x1]))].sort((a, b) => a - b);
    const ys = [...new Set(rects.flatMap((r) => [r.y0, r.y1]))].sort((a, b) => a - b);
    const track = (arr) => arr.slice(1).map((v, i) => (v - arr[i]) + "px").join(" ");

    dom.style.gridTemplateColumns = track(xs);
    dom.style.gridTemplateRows = track(ys) + " auto"; // final auto row = caption

    figures.forEach((fig, i) => {
      const r = rects[i];
      if (!r) return;
      fig.style.gridColumn = (xs.indexOf(r.x0) + 1) + " / " + (xs.indexOf(r.x1) + 1);
      fig.style.gridRow = (ys.indexOf(r.y0) + 1) + " / " + (ys.indexOf(r.y1) + 1);
    });
    const caption = [...dom.children].find((c) => c.tagName === "FIGCAPTION");
    if (caption) {
      caption.style.gridColumn = "1 / -1";
      caption.style.gridRow = (ys.length) + " / " + (ys.length + 1); // the trailing auto row
    }
  };
  const schedule = () => { if (!raf) raf = requestAnimationFrame(relayout); };

  const ro = new ResizeObserver(schedule);
  ro.observe(dom);
  schedule();

  return {
    dom,
    contentDOM: dom,
    update(n) {
      if (n.type.name !== "collage") return false;
      node = n;
      retries = 0;
      schedule();
      return true;
    },
    ignoreMutation(m) {
      return m.type === "attributes"; // the per-figure width/height we write
    },
    destroy() {
      ro.disconnect();
      if (raf) cancelAnimationFrame(raf);
    },
  };
}

// --- preformatted (code block with a clickable language label) -------------
// The language label is a real element (not a CSS pseudo) so it can be clicked.
// Clicking it calls onLanguageClick(language, rect) — the host shows its language
// menu positioned at that rect. contentDOM is the <code> holding the editable text.
export function makePreformattedView(onLanguageClick) {
  return (node, view, getPos) => {
    const dom = document.createElement("pre");
    const lang = document.createElement("div");
    lang.className = "pm-pre-lang";
    lang.contentEditable = "false";
    const code = document.createElement("code");
    dom.appendChild(lang);
    dom.appendChild(code);

    const render = (n) => {
      dom.dataset.language = n.attrs.language || "";
      lang.textContent = n.attrs.language || "language";
    };
    render(node);

    lang.addEventListener("mousedown", (e) => {
      e.preventDefault();
      const rect = lang.getBoundingClientRect();
      // Put the caret inside this code block first, so the host's setLanguage
      // (which targets the preformatted block at the selection) always succeeds.
      const pos = getPos();
      if (pos != null) {
        view.dispatch(view.state.tr.setSelection(TextSelection.near(view.state.doc.resolve(pos + 1))));
        view.focus();
      }
      onLanguageClick(node.attrs.language || "", rect);
    });

    return {
      dom,
      contentDOM: code,
      update(n) {
        if (n.type.name !== "preformatted") return false;
        node = n;
        render(n);
        return true;
      },
      ignoreMutation(m) {
        return lang.contains(m.target);
      },
    };
  };
}

// --- pullquote text regions (quote + author) -------------------------------
// Each region toggles a `pm-empty` class so CSS can show its placeholder, and
// (for the quote) so CSS can hide the author line until the quote has content.
function placeholderRegion(className, placeholder) {
  return (node) => {
    const dom = document.createElement("div");
    dom.className = className;
    dom.setAttribute("data-placeholder", placeholder);
    dom.classList.toggle("pm-empty", node.content.size === 0);
    return {
      dom,
      contentDOM: dom,
      update(n) {
        if (n.type !== node.type) return false;
        dom.classList.toggle("pm-empty", n.content.size === 0);
        return true;
      },
    };
  };
}
export const pullquoteTextView = placeholderRegion("pm-pullquote-text", "Enter quote");
export const pullquoteCreditView = placeholderRegion("pm-pullquote-credit", "Add author");

// blockquote credit — attribution line that shows an "Add credit" placeholder while empty,
// same as the pullquote author line.
export const blockquoteCreditView = placeholderRegion("pm-blockquote-credit", "Add credit");

// expandable blockquote — inline text + credit regions (it holds RichText, not
// blocks), so both get the same placeholder treatment as the pullquote's.
export const expandableTextView = placeholderRegion("pm-expandable-text", "Enter quote");
export const expandableCreditView = placeholderRegion("pm-expandable-credit", "Add credit");

// blockquote — toggles `pm-empty-body` while the quoted text is empty so CSS can hide the
// credit line (and its placeholder) until the user types, mirroring how the pullquote
// author line stays hidden until the quote has content.
export function blockquoteView(node) {
  const dom = document.createElement("blockquote");
  const sync = (n) => {
    let body = 0;
    n.forEach((c) => { if (c.type.name !== "blockquote_credit") body += c.content.size; });
    dom.classList.toggle("pm-empty-body", body === 0);
  };
  sync(node);
  return {
    dom,
    contentDOM: dom,
    update(n) {
      if (n.type !== node.type) return false;
      sync(n);
      return true;
    },
  };
}

// --- anchor (invisible navigation target) ----------------------------------
// pageBlockAnchor renders nothing in the published article, but in the editor it
// needs to be visible, selectable, renamable and deletable. Shown as a small
// non-editable chip with the anchor name (⚓ name). Select it to delete; in the
// browser demo double-click renames it (production renames via setAnchorName).
export function anchorView(node, view, getPos) {
  // Outer block keeps the base font/line-height so the anchor occupies the same
  // height as a paragraph; the small chip sits inside it.
  const dom = document.createElement("div");
  dom.className = "pm-anchor";
  dom.contentEditable = "false";
  const chip = document.createElement("span");
  chip.className = "pm-anchor-chip";
  dom.appendChild(chip);
  const render = (n) => {
    chip.dataset.anchor = n.attrs.name || "";
    chip.textContent = n.attrs.name || "anchor";
    dom.title = `Anchor: ${n.attrs.name || "(unnamed)"}`;
  };
  render(node);
  dom.addEventListener("dblclick", (e) => {
    e.preventDefault();
    if (typeof window.prompt !== "function") return;
    const name = window.prompt("Anchor name:", node.attrs.name || "");
    if (name == null) return;
    const pos = getPos();
    if (pos == null) return;
    view.dispatch(view.state.tr.setNodeMarkup(pos, undefined, { name }));
    view.focus();
  });
  return {
    dom,
    update(n) {
      if (n.type.name !== "anchor") return false;
      node = n;
      render(n);
      return true;
    },
  };
}

// --- list item with optional checkbox --------------------------------------
export function listItemView(node, view, getPos) {
  const dom = document.createElement("li");
  const content = document.createElement("div");
  content.className = "pm-li-content";

  let box = null;
  const sync = (n) => {
    dom.className = n.attrs.hasCheckbox ? "pm-checklist-item" : "";
    if (n.attrs.hasCheckbox) {
      if (!box) {
        box = document.createElement("span");
        box.className = "pm-checkbox";
        box.contentEditable = "false";
        box.setAttribute("role", "checkbox");
        box.addEventListener("mousedown", (e) => {
          e.preventDefault();
          const pos = getPos();
          if (pos == null) return;
          view.dispatch(
            view.state.tr.setNodeMarkup(pos, undefined, {
              hasCheckbox: true,
              isChecked: !node.attrs.isChecked,
            })
          );
        });
        dom.insertBefore(box, content);
      }
      box.setAttribute("aria-checked", String(n.attrs.isChecked));
      box.dataset.checked = String(n.attrs.isChecked);
    } else if (box) {
      box.remove();
      box = null;
    }
  };

  dom.appendChild(content);
  sync(node);

  return {
    dom,
    contentDOM: content,
    update(n) {
      if (n.type.name !== "list_item") return false;
      node = n;
      sync(n);
      return true;
    },
    ignoreMutation(m) {
      return box ? box.contains(m.target) : false;
    },
  };
}
