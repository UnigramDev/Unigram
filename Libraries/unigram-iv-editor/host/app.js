// app.js — THE NATIVE SIDE (simulated).
// In production this logic lives in C#/XAML: button Click handlers call
// webView.ExecuteScriptAsync("UnigramEditor.exec('cmd', args)"), and
// CoreWebView2.WebMessageReceived receives the {type:'state'} pushes to update
// the CommandBar. Here it's plain JS talking to the same bridge surface.

const ed = PMEditor.mountEditor(document.getElementById("editor"), PMEditor.SAMPLE);
const exec = (cmd, args) => window.UnigramEditor.exec(cmd, args);

// Toolbar buttons with a plain data-cmd. mousedown + preventDefault keeps the
// editor selection so mark toggles apply where the caret is.
document.querySelectorAll("[data-cmd]").forEach((btn) => {
  btn.addEventListener("mousedown", (e) => {
    e.preventDefault();
    const args = btn.dataset.arg ? JSON.parse(btn.dataset.arg) : undefined;
    exec(btn.dataset.cmd, args);
  });
});

// Buttons that need to gather input first (prompts) — the native equivalent is
// a flyout / dialog before calling exec.
document.querySelectorAll("[data-action]").forEach((btn) => {
  btn.addEventListener("mousedown", (e) => {
    e.preventDefault();
    const a = btn.dataset.action;
    if (a === "link") {
      const href = window.prompt("Link URL (empty to remove):", "https://");
      if (href !== null) exec("setLink", { href });
    } else if (a === "datetime") {
      const v = window.prompt("Unix timestamp (empty to remove):", String(Math.floor(Date.now() / 1000)));
      if (v !== null) exec("setDateTime", { unixTime: v.trim() === "" ? null : +v });
    } else if (a === "anchor") {
      const name = window.prompt("Anchor name:", "section");
      if (name) exec("insertAnchor", { name });
    } else if (a === "emoji") {
      exec("insertEmoji", { alt: "🔥" });
    } else if (a === "mathInline") {
      const latex = window.prompt("Inline LaTeX:", "a^2 + b^2 = c^2");
      if (latex) exec("insertMathInline", { latex });
    } else if (a === "mathBlock") {
      const latex = window.prompt("Block LaTeX:", "\\int_0^\\infty e^{-x}\\,dx = 1");
      if (latex) exec("insertMathBlock", { latex });
    }
  });
});

// Block-style dropdown.
const blockSel = document.getElementById("blockType");
blockSel.addEventListener("change", () => {
  const v = blockSel.value;
  if (v === "paragraph") exec("setParagraph");
  else if (v[0] === "h") exec("setHeading", { size: +v[1] });
  else if (v === "preformatted") exec("setPreformatted");
  window.UnigramEditor.focus();
});

// Code-language dropdown. The supported list comes from the editor (exec);
// "none" is the first option and clears the block's language.
const langSel = document.getElementById("langType");
(function initLanguages() {
  const langs = exec("getCodeLanguages") || [];
  langSel.add(new Option("none", ""));
  for (const l of langs) langSel.add(new Option(l, l));
})();
langSel.addEventListener("change", () => {
  exec("setLanguage", { language: langSel.value });
  window.UnigramEditor.focus();
});

// Inspector view toggle.
let modelView = "tdlib";
document.getElementById("seg").addEventListener("click", (e) => {
  const b = e.target.closest("button");
  if (!b) return;
  modelView = b.dataset.view;
  document.querySelectorAll("#seg button").forEach((x) => x.classList.toggle("on", x === b));
  renderModel();
});

const modelEl = document.getElementById("model");
function renderModel() {
  const data = modelView === "tdlib" ? exec("getModel") : exec("getProseMirrorJSON");
  modelEl.textContent = JSON.stringify(data, null, 2);
}

// ---- language menu: the editor asks us to show it (with the label's rect) ----
function showLanguageMenu(m) {
  document.getElementById("lang-menu")?.remove();
  const menu = document.createElement("div");
  menu.id = "lang-menu";
  menu.style.cssText = `position:fixed; left:${m.rect.x}px; top:${m.rect.y + m.rect.height + 2}px;
    background:var(--surface); border:1px solid var(--line); border-radius:6px; box-shadow:0 4px 16px rgba(20,32,43,.18);
    padding:4px; z-index:1000; max-height:240px; overflow:auto; font-size:13px;`;
  for (const label of ["none", ...exec("getCodeLanguages")]) {
    const item = document.createElement("div");
    item.textContent = label;
    item.style.cssText = "padding:4px 12px; border-radius:4px; cursor:pointer; white-space:nowrap;";
    if ((m.language || "") === (label === "none" ? "" : label)) item.style.color = "var(--accent)";
    item.addEventListener("mouseenter", () => item.style.background = "var(--accent-soft)");
    item.addEventListener("mouseleave", () => item.style.background = "");
    item.addEventListener("mousedown", (e) => {
      e.preventDefault();
      exec("setLanguage", { language: label === "none" ? "" : label });
      menu.remove();
    });
    menu.appendChild(item);
  }
  document.body.appendChild(menu);
  setTimeout(() => document.addEventListener("mousedown", function close(ev) {
    if (!menu.contains(ev.target)) { menu.remove(); document.removeEventListener("mousedown", close); }
  }), 0);
}

// ---- receive state pushed from the editor (JS -> native) ----
let raf = 0;
window.addEventListener("pm-host-message", (e) => {
  const m = e.detail;
  if (m && m.type === "preformattedLanguage") { showLanguageMenu(m); return; }
  if (!m || m.type !== "state") return;

  // reflect mark toggles
  document.querySelectorAll("[data-mark]").forEach((b) => {
    b.classList.toggle("active", !!m.marks[b.dataset.mark]);
  });
  // reflect block style — m.block is { type, size, listType }. The dropdown only
  // covers text blocks; for list/blockquote/media leave it as-is.
  const b = m.block || {};
  if (b.type === "heading") blockSel.value = "h" + b.size;
  else if (b.type === "paragraph" || b.type === "preformatted") blockSel.value = b.type;
  // language dropdown: enabled only inside a code block, reflects its language
  const isPre = b.type === "preformatted";
  langSel.disabled = !isPre;
  langSel.value = isPre ? (b.language || "") : "";
  // history availability
  document.getElementById("undoBtn").disabled = !m.can.undo;
  document.getElementById("redoBtn").disabled = !m.can.redo;
  // table context — m.table is null unless the caret is in a table; it carries
  // align/valign/isHeader and the can* applicability flags.
  const t = m.table;
  const tablegroup = document.getElementById("tablegroup");
  tablegroup.setAttribute("aria-disabled", String(!t));
  if (t) {
    tablegroup.querySelectorAll("[data-can]").forEach((x) => { x.disabled = !t[x.dataset.can]; });
    tablegroup.querySelectorAll("[data-align]").forEach((x) => x.classList.toggle("active", t.align === x.dataset.align));
    tablegroup.querySelectorAll("[data-valign]").forEach((x) => x.classList.toggle("active", t.valign === x.dataset.valign));
    const th = tablegroup.querySelector("[data-th]");
    if (th) th.classList.toggle("active", t.isHeader === true);
  }
  // status bar
  document.getElementById("st-block").textContent =
    b.type === "heading" ? "heading " + b.size
    : b.type === "list" ? "list (" + b.listType + ")"
    : b.type || "—";
  document.getElementById("st-table").textContent = t ? "yes" : "no";

  // refresh the model panel (debounced to one frame)
  cancelAnimationFrame(raf);
  raf = requestAnimationFrame(renderModel);
});

renderModel();
window.UnigramEditor.focus();
