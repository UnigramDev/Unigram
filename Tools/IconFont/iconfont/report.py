"""A side-by-side review page for the glyphs the matcher could not settle.

The exact matches need no review; these do. Each row puts the glyph the app
actually ships next to the candidates it resembles, with the codepoint and every
place in the source that references it - which is what turns "is this a folder
icon?" into a question you can answer by looking at the call site.
"""

import os

from iconfont import sources as sourcelib
from iconfont import svgdoc
from iconfont.sheet import _path_data, _escape

HEAD = """<!doctype html>
<meta charset="utf-8">
<title>Icon review</title>
<style>
 :root { color-scheme: light dark; --bg:#fff; --fg:#1a1a1a; --dim:#767676; --line:#e3e3e3;
         --card:#fafafa; --warn:#9a5b00; --good:#0f7b0f; }
 @media (prefers-color-scheme: dark) {
   :root { --bg:#1f1f1f; --fg:#f0f0f0; --dim:#a0a0a0; --line:#3a3a3a; --card:#272727;
           --warn:#fcd34d; --good:#6cc46c; } }
 body { background:var(--bg); color:var(--fg); margin:24px auto; max-width:1100px;
        font:14px/1.5 "Segoe UI Variable Text","Segoe UI",system-ui,sans-serif; }
 h1 { font-size:22px; margin:0 0 4px; }
 h2 { font-size:16px; margin:32px 0 4px; }
 p.sub { color:var(--dim); margin:0 0 8px; }
 .row { display:flex; gap:20px; align-items:flex-start; padding:14px;
        border:1px solid var(--line); border-radius:10px; margin-bottom:10px;
        background:var(--card); }
 .mine { flex:0 0 210px; }
 .groups { display:flex; gap:22px; flex-wrap:wrap; }
 .group { border-left:2px solid var(--line); padding-left:14px; }
 .glabel { font-size:10px; text-transform:uppercase; letter-spacing:.06em;
           color:var(--dim); margin-bottom:6px; }
 .cands { display:flex; gap:14px; flex-wrap:wrap; }
 .cand { text-align:center; width:104px; }
 svg { display:block; }
 svg path { fill:var(--fg); }
 .box { border:1px solid var(--line); border-radius:8px; padding:6px; background:var(--bg);
        display:inline-block; }
 .code { font:12px ui-monospace,Consolas,monospace; color:var(--dim); }
 .name { font-size:11px; word-break:break-all; }
 .diff { font:11px ui-monospace,Consolas,monospace; }
 .best { color:var(--good); font-weight:600; }
 .uses { font-size:11px; color:var(--dim); margin-top:6px; }
 .uses b { color:var(--fg); font-weight:600; }
 .none { color:var(--warn); }
</style>
"""


def _svg(path_data, upem, ascent, descent, width, size=44):
    return ('<div class="box"><svg width="%d" height="%d" viewBox="0 %d %d %d">'
            '<path d="%s"/></svg></div>'
            % (size * width // upem, size, -descent, width, upem, path_data))


def write(path, manifest, source, buckets, references, source_name, neighbours=None):
    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))
    sources = sourcelib.build(manifest)
    neighbours = neighbours or {}
    cache = {}

    def candidate(name):
        if name not in cache:
            try:
                art = svgdoc.parse(source.read(name), name=name)
                cache[name] = (_path_data(art, upem, ascent),
                               int(round(upem * art.width / art.height)))
            except Exception:
                cache[name] = ("", upem)
        return cache[name]

    def mine(icon):
        try:
            holder = manifest.resolve(icon)
            art = svgdoc.parse(sourcelib.read(holder, sources), name=holder.src)
            return (_path_data(art, upem, ascent),
                    int(round(upem * art.width / art.height)), art.height)
        except Exception:
            return ("", upem, 0)

    def uses(icon):
        where = references.get(icon.code) or []
        if not where:
            return '<div class="uses none">referenced nowhere in the app</div>'
        shown = ", ".join(_escape(w) for w in sorted(set(where))[:4])
        extra = "" if len(set(where)) <= 4 else " +%d more" % (len(set(where)) - 4)
        return '<div class="uses"><b>used by</b> %s%s</div>' % (shown, extra)

    def group(title, cells):
        if not cells:
            return ""
        return ('<div class="group"><div class="glabel">%s</div>'
                '<div class="cands">%s</div></div>' % (title, "".join(cells)))

    def row(icon, entries, note=""):
        data, width, grid = mine(icon)
        cells = []
        for n, (diff, name) in enumerate(entries):
            cdata, cwidth = candidate(name)
            cells.append('<div class="cand">%s<div class="name">%s</div>'
                         '<div class="diff%s">%.1f%%</div></div>'
                         % (_svg(cdata, upem, ascent, descent, cwidth),
                            _escape(name), " best" if n == 0 else "", diff * 100))
        # The glyph it is closest to inside this font, which is often the real
        # answer: a second drawing of something the font already carries.
        near = []
        for diff, other in neighbours.get(icon.code, ()):
            odata, owidth, _ = mine(other)
            near.append('<div class="cand">%s<div class="name">%s</div>'
                        '<div class="code">U+%04X</div><div class="diff">%.1f%%</div></div>'
                        % (_svg(odata, upem, ascent, descent, owidth),
                           _escape(other.name), other.code, diff * 100))
        return ('<div class="row"><div class="mine">%s'
                '<div class="code">U+%04X &middot; %gpx grid</div>'
                '<div class="name">%s</div>%s%s</div>'
                '<div class="groups">%s%s</div></div>'
                % (_svg(data, upem, ascent, descent, width, 56), icon.code, grid,
                   _escape(icon.name), note, uses(icon),
                   group("closest in %s" % _escape(source.describe()), cells),
                   group("closest already in this font", near)))

    out = [HEAD, "<h1>Icon review</h1>",
           '<p class="sub">Glyphs the shape matcher could not settle on its own. '
           'The left column is what the app ships today; the rest are the closest '
           'icons in %s.</p>' % _escape(source.describe())]

    related = buckets["related"]
    if related:
        out.append("<h2>%d near matches &mdash; probably the same icon</h2>" % len(related))
        out.append('<p class="sub">Close, but not the same drawing. Confirm the name, '
                   'then apply with <code>identify --include-related --apply</code>.</p>')
        for icon, full, ident, diff, others in sorted(related, key=lambda r: r[3]):
            out.append(row(icon, [(diff, ident)] + list(others)))

    duplicate = buckets["duplicate"]
    if duplicate:
        out.append("<h2>%d share a best match with a glyph already named</h2>"
                   % len(duplicate))
        out.append('<p class="sub">Two glyphs matching one upstream name does not make '
                   'them the same drawing - often they are two sizes of it. The verdict '
                   'below is a direct comparison of the two glyphs.</p>')
        for icon, other, full, diff, against in sorted(
                duplicate, key=lambda r: (r[4] is None, r[4] or 0)):
            if against is None:
                verdict = "could not compare"
            elif against <= 0.002:
                verdict = "identical to U+%04X" % other.code
            else:
                verdict = "%.1f%% apart from U+%04X - not a duplicate" % (against * 100,
                                                                          other.code)
            note = '<div class="diff">%s</div>' % _escape(verdict)
            out.append(row(icon, [(diff, full[len("ic_fluent_"):])], note))

    unknown = buckets["unknown"]
    out.append("<h2>%d with no convincing match</h2>" % len(unknown))
    out.append('<p class="sub">Most of these are Telegram\'s own artwork and will stay '
               'local. Worth a glance for the ones that are not.</p>')
    for icon, scored, why in unknown:
        out.append(row(icon, scored, '<div class="diff">%s</div>' % _escape(why or "")))

    directory = os.path.dirname(os.path.abspath(path))
    if directory and not os.path.isdir(directory):
        os.makedirs(directory)
    with open(path, "w", encoding="utf-8", newline="\r\n") as fp:
        fp.write("\n".join(out))
    return path
