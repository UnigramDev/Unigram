"""How far the local copies have drifted from the icons they were taken from.

These are the glyphs that carry a Fluent name but did not get re-pointed at the
live source, because their drawing no longer matches it. Each one is a decision:
take the current upstream artwork, or keep this as a deliberate Telegram
variant. Seeing the two side by side is the only way to make it.

The ones upstream has dropped entirely are listed too - those cannot track
anything and are only here so the manifest stops implying otherwise.
"""

import os

from iconfont import sources as sourcelib
from iconfont import svgdoc
from iconfont.raster import art_coverage, difference
from iconfont.sheet import _escape, _path_data

PREFIX = "ic_fluent_"

BANDS = (
    (0.00, 0.01, "Under 1% &mdash; imperceptible",
     "Nothing an eye resolves at icon sizes. These could track the live source "
     "and stay current, with no visible change: <code>adopt --tolerance 0.01</code>."),
    (0.01, 0.05, "1&ndash;5% &mdash; a slightly older drawing",
     "Small refinements upstream: a corner radius, a stroke weight."),
    (0.05, 0.15, "5&ndash;15% &mdash; a visibly older drawing",
     "Adopting these updates the artwork. Worth deciding one at a time."),
    (0.15, 1.01, "Over 15% &mdash; a different icon",
     "Either a deliberate Telegram variant, or the name is wrong."),
)

HEAD = """<!doctype html>
<meta charset="utf-8">
<title>Drift from upstream</title>
<style>
 :root { color-scheme: light dark; --bg:#fff; --fg:#1a1a1a; --dim:#767676; --line:#e3e3e3;
         --card:#fafafa; --bad:#c42b1c; --warn:#9a5b00; }
 @media (prefers-color-scheme: dark) {
   :root { --bg:#1f1f1f; --fg:#f0f0f0; --dim:#a0a0a0; --line:#3a3a3a; --card:#272727;
           --bad:#ff99a4; --warn:#fcd34d; } }
 body { background:var(--bg); color:var(--fg); margin:24px auto; max-width:1040px;
        font:14px/1.5 "Segoe UI Variable Text","Segoe UI",system-ui,sans-serif; }
 h1 { font-size:22px; margin:0 0 4px; }
 h2 { font-size:16px; margin:34px 0 2px; }
 p.sub { color:var(--dim); margin:0 0 14px; }
 input { width:100%; max-width:420px; padding:8px 10px; font:inherit; margin:8px 0 20px;
         border:1px solid var(--line); border-radius:6px; background:transparent; color:inherit; }
 .grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(232px,1fr)); gap:10px; }
 .row { display:flex; gap:12px; align-items:center; padding:10px;
        border:1px solid var(--line); border-radius:10px; background:var(--card); }
 .box { border:1px solid var(--line); border-radius:8px; padding:5px; background:var(--bg); }
 .arrow { color:var(--dim); font-size:16px; }
 svg { display:block; }
 svg path { fill:var(--fg); }
 .meta { min-width:0; }
 .name { font-size:11px; word-break:break-all; }
 .code { font:11px ui-monospace,Consolas,monospace; color:var(--dim); }
 .diff { font:11px ui-monospace,Consolas,monospace; }
 .unused { color:var(--warn); font-size:10px; }
 .gone { color:var(--bad); font-size:10px; }
</style>
"""

TAIL = """<script>
 var q = document.getElementById('q'), items = [].slice.call(document.querySelectorAll('.row'));
 q.addEventListener('input', function () {
   var needle = q.value.toLowerCase();
   items.forEach(function (el) {
     el.style.display = !needle || el.dataset.k.indexOf(needle) >= 0 ? '' : 'none';
   });
 });
</script>
"""


def write(path, manifest, source_name, references=None):
    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))
    sources = sourcelib.build(manifest)
    source = sources[source_name]
    references = references or {}

    def draw(art, size=40):
        width = int(round(upem * art.width / art.height))
        return ('<div class="box"><svg width="%d" height="%d" viewBox="0 %d %d %d">'
                '<path d="%s"/></svg></div>'
                % (size * width // upem, size, -descent, width, upem,
                   _path_data(art, upem, ascent)))

    drifted, gone = [], []
    for icon in manifest.icons:
        if icon.is_alias or icon.is_remote or not icon.name.startswith(PREFIX):
            continue
        ident = icon.name[len(PREFIX):]
        try:
            mine = svgdoc.parse(sourcelib.read(icon, sources), name=icon.src)
        except Exception:
            continue
        if not source.contains(ident):
            gone.append((icon, mine))
            continue
        try:
            theirs = svgdoc.parse(source.read(ident), name=ident)
            apart = difference(art_coverage(mine, upem, ascent, descent, 96),
                               art_coverage(theirs, upem, ascent, descent, 96))
        except Exception:
            continue
        drifted.append((apart, icon, mine, theirs))
    drifted.sort(key=lambda r: r[0])

    def cell(icon, mine, theirs, apart):
        usage = ("" if references.get(icon.code)
                 else '<br><span class="unused">unused</span>')
        key = ("%s %04x" % (icon.name, icon.code)).lower()
        return ('<div class="row" data-k="%s">%s<span class="arrow">&rarr;</span>%s'
                '<div class="meta"><div class="name">%s</div>'
                '<div class="code">U+%04X</div><div class="diff">%s</div>%s</div></div>'
                % (_escape(key), draw(mine),
                   draw(theirs) if theirs is not None
                   else '<div class="box"><span class="gone">gone</span></div>',
                   _escape(icon.name[len(PREFIX):]), icon.code,
                   "%.1f%% apart" % (apart * 100) if theirs is not None
                   else "not in the package", usage))

    out = [HEAD, "<h1>Drift from upstream</h1>",
           '<p class="sub">%d local glyphs carry a Fluent name. The left glyph is what the '
           'app ships; the right is what %s draws under that name today. They are here '
           'because the two no longer match, which is why they were not re-pointed at the '
           'live source.</p>' % (len(drifted) + len(gone), _escape(source.describe())),
           '<input id="q" placeholder="Filter by name or codepoint" autofocus>']

    for lo, hi, title, blurb in BANDS:
        band = [r for r in drifted if lo <= r[0] < hi]
        if not band:
            continue
        out.append("<h2>%s &middot; %d</h2>" % (title, len(band)))
        out.append('<p class="sub">%s</p>' % blurb)
        out.append('<div class="grid">%s</div>'
                   % "".join(cell(i, mine, theirs, d) for d, i, mine, theirs in band))

    if gone:
        out.append("<h2>Gone from the package &middot; %d</h2>" % len(gone))
        out.append('<p class="sub">No icon of this name exists upstream any more. Some were '
                   'renamed or dropped by Microsoft; the ones ending in a digit never existed '
                   '- that is IcoMoon\'s collision suffix on a locally modified copy, so they '
                   'are really Telegram artwork wearing a Fluent name.</p>')
        out.append('<div class="grid">%s</div>'
                   % "".join(cell(i, mine, None, 0) for i, mine in
                             sorted(gone, key=lambda r: r[0].code)))

    directory = os.path.dirname(os.path.abspath(path))
    if directory and not os.path.isdir(directory):
        os.makedirs(directory)
    with open(path, "w", encoding="utf-8", newline="\r\n") as fp:
        fp.write("\n".join(out) + TAIL)
    return path, len(drifted), len(gone)
