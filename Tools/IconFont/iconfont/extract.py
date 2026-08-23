"""One-time migration out of IcoMoon.

Telegram.json holds every outline the font was built from, in a 1024-unit y-down
grid. Writing each one back out as an ordinary SVG makes the repository the
source of truth: the ~550 glyphs whose original artwork is lost stop depending
on a website, and the rest can be re-pointed at a live source afterwards.

Which glyphs actually shipped is read from the font, not the JSON. IcoMoon's
selection list keeps entries it does not build - 62 of them are marked order 0
and dropped, and 47 codepoints are claimed twice - so the font is the only
honest record of what the app has been rendering.
"""

import os
import re

from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.misc.transform import Transform
from fontTools.svgLib.path import parse_path

from iconfont.fontbuild import LEADING
from iconfont.manifest import Icon

ICOMOON_GRID = 1024.0


def _number(value):
    return ("%.4f" % value).rstrip("0").rstrip(".") or "0"


def _safe(name):
    return re.sub(r"[^A-Za-z0-9._-]+", "_", name).strip("_") or "icon"


def selection(data):
    """Map codepoint -> (name, import size), preferring entries IcoMoon builds."""
    chosen = {}
    for icon_set in data["iconSets"]:
        size = icon_set["metadata"].get("importSize") or {}
        height = float(size.get("height") or icon_set.get("height") or ICOMOON_GRID)
        by_id = {i["id"]: i for i in icon_set["icons"]}
        for entry in icon_set["selection"]:
            icon = by_id.get(entry["id"])
            if icon is None:
                continue
            code = entry["code"]
            candidate = (entry["order"], entry["name"], height, icon["paths"])
            # order 0 means IcoMoon leaves the entry out of the font.
            if code not in chosen or candidate[0] > chosen[code][0]:
                chosen[code] = candidate
    return chosen


def to_svg(paths, height, width):
    """Re-serialise IcoMoon's 1024-grid outlines at the icon's own size."""
    scale = height / ICOMOON_GRID
    body = []
    for d in paths:
        raw = RecordingPen()
        parse_path(d, raw)
        pen = SVGPathPen(None, ntos=_number)
        out = TransformPen(pen, Transform(scale, 0, 0, scale, 0, 0))
        for op, args in raw.value:
            getattr(out, "closePath" if op == "endPath" else op)(*args)
        commands = pen.getCommands()
        if commands:
            body.append('    <path d="%s"/>' % commands)
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="%s" height="%s" '
            'viewBox="0 0 %s %s">\n%s\n</svg>\n'
            % (_number(width), _number(height), _number(width), _number(height),
               "\n".join(body)))


def run(json_data, font, out_dir, font_config):
    """Write one SVG per shipped glyph and return the manifest icon list."""
    upem = float(font_config.get("unitsPerEm", 1024))
    chosen = selection(json_data)
    cmap = font.getBestCmap()
    advances = font["hmtx"].metrics
    reserved = {code for _, code, _ in LEADING if code is not None}

    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)

    icons, skipped, used = [], [], {}
    for code in sorted(cmap):
        if code in reserved:
            continue
        entry = chosen.get(code)
        if entry is None:
            skipped.append("U+%04X is in the font but not in Telegram.json" % code)
            continue
        _, name, height, paths = entry
        advance = advances[cmap[code]][0]
        width = advance * height / upem

        base = _safe(name)
        if base.lower() in used:
            base = "%s_%04X" % (base, code)
        used[base.lower()] = code

        filename = base + ".svg"
        markup = to_svg(paths, height, width)
        # The repository keeps text files CRLF in the working tree.
        with open(os.path.join(out_dir, filename), "w", encoding="utf-8", newline="\r\n") as fp:
            fp.write(markup)

        natural = int(round(upem * width / height))
        icons.append(Icon(
            name=name,
            code=code,
            src="%s/%s" % (os.path.basename(out_dir), filename),
            advance=advance if advance != natural else None,
            blank="<path" not in markup,
        ))
    return icons, skipped
