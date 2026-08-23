"""Codepoints the app asks for that the font has no glyph for.

App.xaml points SymbolThemeFontFamily at this font, so these do not fall back to
a system icon font - they render as nothing at all. Most are left over from
before that override, when the same codepoints resolved against Segoe. Passing
the Segoe font in shows what each one was meant to be, which is usually enough
to decide whether to draw it, repoint the call site, or delete it.
"""

import os
import re

from fontTools.misc.transform import Transform
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen

from iconfont.check import CS_ESCAPE, XAML_LITERAL, references
from iconfont.outline import split_contours, _flatten
from iconfont.raster import art_coverage, coverage, difference
from iconfont.sheet import _escape, _path_data

HEAD = """<!doctype html>
<meta charset="utf-8">
<title>Missing glyphs</title>
<style>
 :root { color-scheme: light dark; --bg:#fff; --fg:#1a1a1a; --dim:#767676; --line:#e3e3e3;
         --card:#fafafa; --bad:#c42b1c; }
 @media (prefers-color-scheme: dark) {
   :root { --bg:#1f1f1f; --fg:#f0f0f0; --dim:#a0a0a0; --line:#3a3a3a; --card:#272727;
           --bad:#ff99a4; } }
 body { background:var(--bg); color:var(--fg); margin:24px auto; max-width:960px;
        font:14px/1.5 "Segoe UI Variable Text","Segoe UI",system-ui,sans-serif; }
 h1 { font-size:22px; margin:0 0 4px; }
 p.sub { color:var(--dim); margin:0 0 20px; }
 .row { display:flex; gap:18px; align-items:flex-start; padding:14px;
        border:1px solid var(--line); border-radius:10px; margin-bottom:10px;
        background:var(--card); }
 .box { border:1px solid var(--line); border-radius:8px; padding:6px; background:var(--bg); }
 svg path { fill:var(--fg); }
 .none { color:var(--bad); font-size:11px; width:56px; text-align:center; }
 .code { font:13px ui-monospace,Consolas,monospace; }
 .where { margin-top:6px; font-size:12px; }
 .where div { color:var(--dim); }
 .where code { color:var(--fg); font:11px ui-monospace,Consolas,monospace;
               background:var(--bg); border:1px solid var(--line); border-radius:4px;
               padding:1px 4px; }
 .cands { display:flex; gap:10px; flex-wrap:wrap; margin-top:8px; }
 .cand { text-align:center; width:96px; }
 .cand .name { font-size:10px; word-break:break-all; }
 .glabel { font-size:10px; text-transform:uppercase; letter-spacing:.06em;
           color:var(--dim); margin-top:10px; }
</style>
"""

SKIP_DIRS = {"bin", "obj", ".git", ".vs", "packages", ".claude"}


def hint_paths(font, codes, upem, ascent):
    """Outlines for the same codepoints in a reference font, scaled to our em."""
    out = {}
    if font is None:
        return out
    cmap = font.getBestCmap()
    glyphs = font.getGlyphSet()
    scale = float(upem) / font["head"].unitsPerEm
    for code in codes:
        name = cmap.get(code)
        if name is None:
            continue
        raw = RecordingPen()
        glyphs[name].draw(raw)
        pen = SVGPathPen(None)
        # The reference font is y-up like ours; only the em size differs, and
        # the sheet draws y-down.
        target = TransformPen(pen, Transform(scale, 0, 0, -scale, 0, ascent))
        for op, args in raw.value:
            getattr(target, "closePath" if op == "endPath" else op)(*args)
        out[code] = pen.getCommands()
    return out


def _context(repo_root, codes):
    """The source line each reference sits on, so the intent is visible."""
    wanted = {c: [] for c in codes}
    for base, dirs, files in os.walk(repo_root):
        dirs[:] = [d for d in dirs if d.lower() not in SKIP_DIRS]
        for name in files:
            if not name.endswith((".cs", ".xaml")):
                continue
            path = os.path.join(base, name)
            try:
                with open(path, "r", encoding="utf-8-sig", errors="replace") as fp:
                    lines = fp.read().splitlines()
            except Exception:
                continue
            rel = os.path.relpath(path, repo_root)
            pattern = CS_ESCAPE if name.endswith(".cs") else XAML_LITERAL
            for number, line in enumerate(lines, 1):
                for point in pattern.findall(line):
                    code = int(point, 16)
                    if code in wanted and len(wanted[code]) < 4:
                        wanted[code].append((rel, number, line.strip()[:110]))
    return wanted


def write(path, manifest, repo_root, icons_cs, reference=None, source=None):
    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))

    codes = {i.code for i in manifest.icons}
    referenced = references(repo_root, icons_cs)
    missing = sorted(set(referenced) - codes)

    hints = hint_paths(reference, missing, upem, ascent)
    context = _context(repo_root, missing)
    options = suggest(reference, missing, source, manifest) if source is not None else {}

    out = [HEAD, "<h1>Missing glyphs</h1>",
           '<p class="sub">%d codepoints the app names that this font has no glyph for. '
           'App.xaml points both TelegramThemeFontFamily and SymbolThemeFontFamily at '
           'Telegram.ttf, so these do not fall back to a system icon font - they render '
           'as nothing.%s</p>'
           % (len(missing),
              " The glyph shown is what %s draws at the same codepoint."
              % _escape(reference["name"].getDebugName(1) or "the reference font")
              if reference is not None else "")]

    for code in missing:
        data = hints.get(code)
        art = ('<div class="box"><svg width="56" height="56" viewBox="0 %d %d %d">'
               '<path d="%s"/></svg></div>' % (-descent, upem, upem, data)) if data else (
            '<div class="none">not in the reference font either</div>')
        rows = []
        for rel, number, line in context.get(code, ()):
            rows.append('<div>%s:%d<br><code>%s</code></div>'
                        % (_escape(rel), number, _escape(line)))
        for where in referenced[code]:
            if where.startswith("Icons."):
                rows.insert(0, "<div><b>%s</b></div>" % _escape(where))
        cells = []
        for diff, name, data, width in options.get(code, ()):
            cells.append('<div class="cand"><div class="box">'
                         '<svg width="%d" height="40" viewBox="0 %d %d %d"><path d="%s"/></svg>'
                         '</div><div class="name">%s</div></div>'
                         % (40 * width // upem, -descent, width, upem, data, _escape(name)))
        suggestions = ('<div class="glabel">closest in the MIT package - pick by eye, the '
                       'numbers are meaningless for thin shapes</div>'
                       '<div class="cands">%s</div>' % "".join(cells)) if cells else ""
        out.append('<div class="row">%s<div><div class="code">U+%04X</div>'
                   '<div class="where">%s</div>%s</div></div>'
                   % (art, code, "".join(rows) or "<div>referenced, source line not found</div>",
                      suggestions))

    directory = os.path.dirname(os.path.abspath(path))
    if directory and not os.path.isdir(directory):
        os.makedirs(directory)
    with open(path, "w", encoding="utf-8", newline="\r\n") as fp:
        fp.write("\n".join(out))
    return path, missing


def suggest(reference, codes, source, manifest, shortlist=48, keep=5):
    """Closest icons in the MIT package to whatever the reference font draws.

    The Segoe artwork itself cannot be shipped - it is Microsoft's, licensed for
    use as a system font rather than for redistribution inside another font - so
    the useful question is which icon in the package it corresponds to. Coverage
    distance ranks poorly for thin shapes, so this offers candidates to choose
    between rather than picking one.
    """
    from iconfont.identify import COARSE, FINE, source_signatures

    if reference is None:
        return {}
    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))
    box = (0, -descent, upem, ascent)

    library = source_signatures(manifest, source, lambda *_: None)
    items = list(library.items())
    cmap, glyphs = reference.getBestCmap(), reference.getGlyphSet()
    scale = float(upem) / reference["head"].unitsPerEm

    def cover(code, resolution):
        raw = RecordingPen()
        glyphs[cmap[code]].draw(raw)
        out = RecordingPen()
        pen = TransformPen(out, Transform(scale, 0, 0, scale, 0, 0))
        for op, args in raw.value:
            getattr(pen, "closePath" if op == "endPath" else op)(*args)
        return coverage([_flatten(c) for c in split_contours(out.value)], box, resolution)

    from iconfont import svgdoc
    out = {}
    for code in codes:
        if code not in cmap:
            continue
        packed = 0
        for cell in cover(code, COARSE):
            packed = (packed << 1) | (1 if cell else 0)
        fine = cover(code, FINE)
        scored = []
        for name, _ in sorted(items, key=lambda kv: bin(kv[1] ^ packed).count("1"))[:shortlist]:
            try:
                art = svgdoc.parse(source.read(name), name=name)
                scored.append((difference(fine, art_coverage(art, upem, ascent, descent, FINE)),
                               name, _path_data(art, upem, ascent),
                               int(round(upem * art.width / art.height))))
            except Exception:
                continue
        scored.sort(key=lambda r: (round(r[0], 4), "/" in r[1], len(r[1]), r[1]))
        out[code] = scored[:keep]
    return out
