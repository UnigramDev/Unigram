"""Every glyph that renders differently from a reference font, drawn both ways.

`verify` answers "how much moved" as a number, which is the right question while
building but the wrong one before shipping. This answers "show me", so the two
drawings can be compared by eye and a wrong name or a bad adoption is obvious.
"""

import os

from fontTools.misc.transform import Transform
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

from iconfont.raster import coverage, difference, polygons
from iconfont.sheet import _escape

BANDS = (
    (0.15, 1.01, "Over 15% &mdash; a different drawing",
     "A deliberate replacement, or a name that does not describe the artwork."),
    (0.05, 0.15, "5&ndash;15% &mdash; visibly redrawn",
     "The same icon brought up to date. Worth looking at each one."),
    (0.01, 0.05, "1&ndash;5% &mdash; a small refinement",
     "A corner radius, a stroke weight. Unlikely to be noticed in place."),
    (0.0, 0.01, "Under 1% &mdash; imperceptible",
     "Upstream tweaks too small to see at icon sizes."),
)

HEAD = """<!doctype html>
<meta charset="utf-8">
<title>Glyph changes</title>
<style>
 :root { color-scheme: light dark; --bg:#fff; --fg:#1a1a1a; --dim:#767676; --line:#e3e3e3;
         --card:#fafafa; --warn:#9a5b00; --add:#0f7b0f; --del:#c42b1c; }
 @media (prefers-color-scheme: dark) {
   :root { --bg:#1f1f1f; --fg:#f0f0f0; --dim:#a0a0a0; --line:#3a3a3a; --card:#272727;
           --warn:#fcd34d; --add:#6cc46c; --del:#ff99a4; } }
 body { background:var(--bg); color:var(--fg); margin:24px auto; max-width:1040px;
        font:14px/1.5 "Segoe UI Variable Text","Segoe UI",system-ui,sans-serif; }
 h1 { font-size:22px; margin:0 0 4px; }
 h2 { font-size:16px; margin:34px 0 2px; }
 p.sub { color:var(--dim); margin:0 0 14px; }
 input { width:100%; max-width:420px; padding:8px 10px; font:inherit; margin:8px 0 20px;
         border:1px solid var(--line); border-radius:6px; background:transparent; color:inherit; }
 .grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(300px,1fr)); gap:10px; }
 .row { display:flex; gap:10px; align-items:center; padding:10px;
        border:1px solid var(--line); border-radius:10px; background:var(--card); }
 .box { border:1px solid var(--line); border-radius:8px; padding:5px; background:var(--bg); }
 .box.before { border-color:var(--del); }
 .box.after { border-color:var(--add); }
 .arrow { color:var(--dim); }
 svg { display:block; }
 svg path { fill:var(--fg); }
 .meta { min-width:0; }
 .name { font-size:11px; word-break:break-all; }
 .code, .diff { font:11px ui-monospace,Consolas,monospace; color:var(--dim); }
 .use { font-size:10px; color:var(--dim); }
 .unused { font-size:10px; color:var(--warn); }
</style>
"""

TAIL = """<script>
 var q = document.getElementById('q'), items = [].slice.call(document.querySelectorAll('.row'));
 q.addEventListener('input', function () {
   var n = q.value.toLowerCase();
   items.forEach(function (el) {
     el.style.display = !n || el.dataset.k.indexOf(n) >= 0 ? '' : 'none';
   });
 });
</script>
"""


def _path(font, glyph_name, upem, ascent):
    """A glyph from a font as SVG path data in our y-down drawing space."""
    scale = float(upem) / font["head"].unitsPerEm
    raw = RecordingPen()
    font.getGlyphSet()[glyph_name].draw(raw)
    pen = SVGPathPen(None)
    out = TransformPen(pen, Transform(scale, 0, 0, -scale, 0, ascent))
    for op, args in raw.value:
        getattr(out, "closePath" if op == "endPath" else op)(*args)
    return pen.getCommands()


# Below this a difference is cubic-to-quadratic conversion, not a decision
# anyone made. Same threshold `verify` uses to call two glyphs the same.
FLOOR = 0.002


def write(path, manifest, built, reference, references=None, resolution=96, floor=FLOOR):
    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))
    box = (0, -descent, upem, ascent)
    references = references or {}
    by_code = manifest.by_code()

    new = TTFont(built) if isinstance(built, str) else built
    old = TTFont(reference) if isinstance(reference, str) else reference
    new_map, old_map = new.getBestCmap(), old.getBestCmap()
    new_set, old_set = new.getGlyphSet(), old.getGlyphSet()

    rows, quiet = [], [0]
    for code in sorted(set(new_map) & set(old_map)):
        a, b = new_map[code], old_map[code]
        diff = difference(coverage(polygons(new_set[a]), box, resolution),
                          coverage(polygons(old_set[b]), box, resolution))
        if diff <= floor:
            quiet[0] += 1
            continue
        rows.append((diff, code, _path(old, b, upem, ascent), _path(new, a, upem, ascent),
                     old["hmtx"].metrics[b][0], new["hmtx"].metrics[a][0]))
    rows.sort(key=lambda r: -r[0])

    def glyph(kind, data, width, size=40):
        return ('<div class="box %s"><svg width="%d" height="%d" viewBox="0 %d %d %d">'
                '<path d="%s"/></svg></div>'
                % (kind, size * width // upem, size, -descent, width, upem, data))

    def cell(diff, code, before, after, wide_before, wide_after):
        icon = by_code.get(code)
        name = icon.name if icon else "U+%04X" % code
        where = sorted(set(references.get(code) or []))
        usage = ('<div class="use">%s</div>' % _escape(", ".join(where)[:44]) if where
                 else '<div class="unused">unused</div>')
        key = ("%s %04x" % (name, code)).lower()
        return ('<div class="row" data-k="%s">%s<span class="arrow">&rarr;</span>%s'
                '<div class="meta"><div class="name">%s</div>'
                '<div class="code">U+%04X</div><div class="diff">%.1f%% of the em</div>%s'
                '</div></div>'
                % (_escape(key), glyph("before", before, wide_before),
                   glyph("after", after, wide_after), _escape(name), code, diff * 100, usage))

    out = [HEAD, "<h1>Glyph changes</h1>",
           '<p class="sub">%d codepoints render differently from the reference font. '
           'Left is the old drawing, right is the new one. The other %d are unchanged, or '
           'differ by less than %.1f%% of the em, which is below what the outline '
           'conversion itself moves an edge.</p>'
           % (len(rows), quiet[0], floor * 100),
           '<input id="q" placeholder="Filter by name or codepoint" autofocus>']

    for lo, hi, title, blurb in BANDS:
        band = [r for r in rows if lo <= r[0] < hi]
        if not band:
            continue
        out.append("<h2>%s &middot; %d</h2>" % (title, len(band)))
        out.append('<p class="sub">%s</p>' % blurb)
        out.append('<div class="grid">%s</div>' % "".join(cell(*r) for r in band))

    directory = os.path.dirname(os.path.abspath(path))
    if directory and not os.path.isdir(directory):
        os.makedirs(directory)
    with open(path, "w", encoding="utf-8", newline="\r\n") as fp:
        fp.write("\n".join(out) + TAIL)
    return path, len(rows)
