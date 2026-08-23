"""A contact sheet of every glyph, drawn from the converted outlines.

Deliberately not rendered with the built font: the point is to see what the
conversion produced, so a winding mistake or a clipPath swallowed whole shows up
as a black square here instead of in the app.
"""

import os

from fontTools.misc.transform import Transform
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen

from iconfont import sources as sourcelib
from iconfont import svgdoc
from iconfont.outline import art_to_glyph, natural_advance

HEAD = """<!doctype html>
<meta charset="utf-8">
<title>%(title)s</title>
<style>
 :root { color-scheme: light dark; --bg:#fff; --fg:#1a1a1a; --dim:#767676; --line:#e3e3e3; --bad:#c42b1c; }
 @media (prefers-color-scheme: dark) { :root { --bg:#1f1f1f; --fg:#f0f0f0; --dim:#a0a0a0; --line:#3a3a3a; } }
 body { background:var(--bg); color:var(--fg); font:14px/1.4 "Segoe UI Variable Text","Segoe UI",system-ui,sans-serif; margin:24px; }
 h1 { font-size:20px; font-weight:600; margin:0 0 4px; }
 p.sub { color:var(--dim); margin:0 0 20px; }
 input { width:100%%; max-width:420px; padding:8px 10px; font:inherit; margin-bottom:20px;
         border:1px solid var(--line); border-radius:6px; background:transparent; color:inherit; }
 .grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(148px,1fr)); gap:8px; }
 figure { margin:0; padding:12px 8px; border:1px solid var(--line); border-radius:8px; text-align:center; }
 figure.remote { border-color:#0f7b0f55; }
 figure.bad { border-color:var(--bad); }
 svg { display:block; margin:0 auto 8px; }
 svg path { fill:var(--fg); }
 figcaption { font-size:11px; word-break:break-all; }
 .code { color:var(--dim); font:11px ui-monospace,Consolas,monospace; }
 .src { color:var(--dim); font-size:10px; }
 .use { color:var(--dim); font-size:10px; }
 .unused { color:var(--bad); font-size:10px; font-weight:600; }
 .err { color:var(--bad); font-size:10px; }
</style>
<h1>%(title)s</h1>
<p class="sub">%(count)d glyphs &middot; %(remote)d tracking a live source &middot; %(local)d local artwork &middot; %(alias)d sharing another glyph</p>
%(lede)s
<input id="q" placeholder="Filter by name, codepoint or source" autofocus>
<div class="grid" id="grid">
"""

TAIL = """</div>
<script>
 var q = document.getElementById('q'), items = [].slice.call(document.querySelectorAll('figure'));
 q.addEventListener('input', function () {
   var needle = q.value.toLowerCase();
   items.forEach(function (el) {
     el.style.display = !needle || el.dataset.k.indexOf(needle) >= 0 ? '' : 'none';
   });
 });
</script>
"""


def _path_data(art, upem, ascent):
    glyph = art_to_glyph(art, upem, ascent)
    raw = RecordingPen()
    glyph.draw(raw, {})
    pen = SVGPathPen(None)
    # Back to y-down so the sheet can use the same box as an ordinary SVG.
    out = TransformPen(pen, Transform(1, 0, 0, -1, 0, ascent))
    for op, args in raw.value:
        getattr(out, "closePath" if op == "endPath" else op)(*args)
    return pen.getCommands()


def write(manifest, path, references=None, only=None):
    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))
    sources = sourcelib.build(manifest)
    references = references or {}

    icons = sorted(manifest.icons, key=lambda i: i.code)
    if only == "unused":
        icons = [i for i in icons if not references.get(i.code)]
    elif only == "used":
        icons = [i for i in icons if references.get(i.code)]

    cells = []
    remote = 0
    for icon in icons:
        classes = ["remote"] if icon.is_remote else []
        if icon.is_remote:
            remote += 1
        where = sorted(set(references.get(icon.code) or []))
        try:
            holder = manifest.resolve(icon)
            art = svgdoc.parse(sourcelib.read(holder, sources), name=holder.src)
            trouble = art.errors
            data = "" if trouble else _path_data(art, upem, ascent)
            width = icon.advance or natural_advance(art, upem)
        except Exception as e:
            trouble = ["%s: %s" % (type(e).__name__, e)]
            data, width = "", upem
        if trouble:
            classes.append("bad")
        box = "0 %d %d %d" % (-descent, width, upem)
        key = ("%s %04x %s %s" % (icon.name, icon.code, icon.src,
                                  " ".join(where) or "unused")).lower()
        usage = ('<br><span class="use">%s</span>'
                 % _escape(", ".join(where)[:70]) if where
                 else '<br><span class="unused">unused</span>')
        cells.append(
            '<figure class="%s" data-k="%s">'
            '<svg width="%d" height="40" viewBox="%s"><path d="%s"/></svg>'
            '<figcaption>%s<br><span class="code">U+%04X</span>'
            '<br><span class="src">%s</span>%s%s</figcaption></figure>'
            % (" ".join(classes), _escape(key), round(40.0 * width / upem), box, data,
               _escape(icon.name), icon.code, _escape(icon.src), usage,
               "".join('<br><span class="err">%s</span>' % _escape(t) for t in trouble)))

    family = _escape(str(cfg.get("family", "Telegram")))
    lede = ""
    if only == "unused":
        title = "%s &mdash; unreferenced glyphs" % family
        lede = ('<p class="sub">Nothing in the app names these codepoints: not '
                '<code>Icons.cs</code>, not a <code>\uE9F1</code> escape in any other C# '
                'file, not a <code>&amp;#xE9F1;</code> literal in any XAML. They are '
                'candidates for removal, but a codepoint reached some other way &mdash; '
                'built at runtime, or from a string the scan cannot see &mdash; would look '
                'the same, so check before deleting.</p>')
    elif only == "used":
        title = "%s &mdash; referenced glyphs" % family
    else:
        title = "%s %s" % (family, _escape(str(cfg.get("version", ""))))
    head = HEAD % {
        "title": title,
        "lede": lede,
        "count": len(icons),
        "remote": remote,
        "local": len(icons) - remote - sum(1 for i in icons if i.is_alias),
        "alias": sum(1 for i in icons if i.is_alias),
    }
    directory = os.path.dirname(os.path.abspath(path))
    if directory and not os.path.isdir(directory):
        os.makedirs(directory)
    with open(path, "w", encoding="utf-8", newline="\r\n") as fp:
        fp.write(head + "\n".join(cells) + TAIL)
    return path


def _escape(text):
    return (str(text).replace("&", "&amp;").replace("<", "&lt;")
            .replace(">", "&gt;").replace('"', "&quot;"))
