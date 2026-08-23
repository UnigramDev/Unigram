"""A deliberately small SVG reader: enough of the spec for icon artwork, and
loud about everything else.

Icon SVGs in the wild are not all as clean as Microsoft's. The files from the
designer folder use evenodd fills, <g> wrappers, no-op clipPaths left behind by
export tools, <rect> shapes and the occasional transform. Silently dropping any
of those produces a glyph that is subtly wrong, or - in the clipPath case - a
solid black square, so anything not understood here is reported rather than
skipped.
"""

import math
import re
import xml.etree.ElementTree as ET

from fontTools.misc.transform import Identity, Transform

# Elements that describe how to paint, not what to paint.
IGNORED = {"defs", "title", "desc", "metadata", "style", "clipPath", "mask", "filter",
           "linearGradient", "radialGradient", "pattern", "symbol", "marker"}

_NUM = r"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?"
_TRANSFORM = re.compile(r"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)")
_RULE = re.compile(r"([^{}]+)\{([^{}]*)\}")

# Colours dark enough that the difference between them is invisible once the
# artwork becomes a single-colour glyph. Illustrator exports habitually mix
# #212121 with plain black in one file.
DARK_LUMA = 0.35

NAMED_COLOURS = {
    "black": (0, 0, 0), "white": (255, 255, 255), "red": (255, 0, 0),
    "green": (0, 128, 0), "blue": (0, 0, 255), "yellow": (255, 255, 0),
    "gray": (128, 128, 128), "grey": (128, 128, 128), "silver": (192, 192, 192),
    "maroon": (128, 0, 0), "purple": (128, 0, 128), "fuchsia": (255, 0, 255),
    "lime": (0, 255, 0), "olive": (128, 128, 0), "navy": (0, 0, 128),
    "teal": (0, 128, 128), "aqua": (0, 255, 255), "orange": (255, 165, 0),
}


class SvgError(Exception):
    pass


class Contour:
    """One `d` string plus the transform and fill rule in force where it sat."""

    __slots__ = ("d", "transform", "evenodd")

    def __init__(self, d, transform, evenodd):
        self.d = d
        self.transform = transform
        self.evenodd = evenodd


class SvgArt:
    def __init__(self, width, height, contours, warnings, errors):
        self.width = width
        self.height = height
        self.contours = contours
        self.warnings = warnings
        self.errors = errors


def _tag(el):
    t = el.tag
    return t.rsplit("}", 1)[-1] if "}" in t else t


def _floats(text):
    return [float(x) for x in re.findall(_NUM, text or "")]


def parse_transform(text):
    t = Identity
    for name, args in _TRANSFORM.findall(text or ""):
        a = _floats(args)
        if name == "matrix" and len(a) == 6:
            t = t.transform(tuple(a))
        elif name == "translate" and a:
            t = t.translate(a[0], a[1] if len(a) > 1 else 0)
        elif name == "scale" and a:
            t = t.scale(a[0], a[1] if len(a) > 1 else a[0])
        elif name == "rotate" and len(a) == 1:
            t = t.rotate(math.radians(a[0]))
        elif name == "rotate" and len(a) == 3:
            t = t.translate(a[1], a[2]).rotate(math.radians(a[0])).translate(-a[1], -a[2])
        elif name == "skewX" and a:
            t = t.skew(math.radians(a[0]), 0)
        elif name == "skewY" and a:
            t = t.skew(0, math.radians(a[0]))
        else:
            raise SvgError("unsupported transform %s(%s)" % (name, args))
    return t


def _rect_path(el):
    x, y = float(el.get("x", 0)), float(el.get("y", 0))
    w, h = float(el.get("width", 0)), float(el.get("height", 0))
    rx, ry = el.get("rx"), el.get("ry")
    if rx is None and ry is None:
        return "M%g %gH%gV%gH%gZ" % (x, y, x + w, y + h, x)
    rx = float(rx if rx is not None else ry)
    ry = float(ry if ry is not None else rx)
    rx, ry = min(rx, w / 2.0), min(ry, h / 2.0)
    return ("M%g %gH%gA%g %g 0 0 1 %g %gV%gA%g %g 0 0 1 %g %gH%gA%g %g 0 0 1 %g %gV%gA%g %g 0 0 1 %g %gZ"
            % (x + rx, y, x + w - rx,
               rx, ry, x + w, y + ry, y + h - ry,
               rx, ry, x + w - rx, y + h, x + rx,
               rx, ry, x, y + h - ry, y + ry,
               rx, ry, x + rx, y))


def _circle_path(el, ellipse=False):
    cx, cy = float(el.get("cx", 0)), float(el.get("cy", 0))
    if ellipse:
        rx, ry = float(el.get("rx", 0)), float(el.get("ry", 0))
    else:
        rx = ry = float(el.get("r", 0))
    return "M%g %gA%g %g 0 1 0 %g %gA%g %g 0 1 0 %g %gZ" % (
        cx - rx, cy, rx, ry, cx + rx, cy, rx, ry, cx - rx, cy)


def _poly_path(el, close):
    pts = _floats(el.get("points"))
    if len(pts) < 4:
        return None
    d = "M%g %g" % (pts[0], pts[1])
    for i in range(2, len(pts) - 1, 2):
        d += "L%g %g" % (pts[i], pts[i + 1])
    return d + ("Z" if close else "")


def _shape_path(el):
    name = _tag(el)
    if name == "path":
        return el.get("d")
    if name == "rect":
        return _rect_path(el)
    if name == "circle":
        return _circle_path(el)
    if name == "ellipse":
        return _circle_path(el, ellipse=True)
    if name == "polygon":
        return _poly_path(el, True)
    if name == "polyline":
        return _poly_path(el, False)
    if name == "line":
        return "M%s %sL%s %s" % (el.get("x1", 0), el.get("y1", 0),
                                 el.get("x2", 0), el.get("y2", 0))
    return None


def _declarations(text):
    out = {}
    for decl in (text or "").split(";"):
        if ":" in decl:
            k, v = (p.strip() for p in decl.split(":", 1))
            out[k.lower()] = v
    return out


def parse_stylesheet(text):
    """Class selectors only - the one form Illustrator's SVG export emits."""
    sheet = {}
    text = re.sub(r"/\*.*?\*/", "", text or "", flags=re.S)
    for selectors, body in _RULE.findall(text):
        decls = _declarations(body)
        for selector in selectors.split(","):
            selector = selector.strip()
            if selector.startswith(".") and len(selector) > 1:
                sheet.setdefault(selector[1:], {}).update(decls)
    return sheet


def _paint(el, inherited, sheet):
    """Resolve the properties in force on an element.

    Order matters and is the one CSS specifies: presentation attributes lose to
    class rules, which lose to the style attribute. Getting it backwards makes
    the `.st1{display:none}` alternates that Illustrator leaves in the file
    render on top of the real artwork.
    """
    fill, stroke, rule, display = inherited
    props = {}
    for name in ("fill", "stroke", "fill-rule", "display", "visibility",
                 "opacity", "fill-opacity"):
        if el.get(name) is not None:
            props[name] = el.get(name)
    for cls in (el.get("class") or "").split():
        props.update(sheet.get(cls, {}))
    props.update(_declarations(el.get("style")))

    fill = props.get("fill", fill)
    stroke = props.get("stroke", stroke)
    rule = props.get("fill-rule", rule)
    if (props.get("display", "").strip() == "none"
            or props.get("visibility", "").strip() == "hidden"
            or _is_zero(props.get("opacity"))
            or _is_zero(props.get("fill-opacity"))):
        display = False
    return fill, stroke, rule, display


def _is_zero(value):
    try:
        return value is not None and float(value) == 0.0
    except ValueError:
        return False


def parse_colour(value):
    """Best-effort RGB, used only to tell monochrome art from multicolour."""
    if not value:
        return None
    value = value.strip().lower()
    if value in ("none", "transparent", "currentcolor", "inherit"):
        return None
    if value in NAMED_COLOURS:
        return NAMED_COLOURS[value]
    if value.startswith("#"):
        h = value[1:]
        if len(h) == 3:
            h = "".join(c * 2 for c in h)
        if len(h) >= 6:
            try:
                return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
            except ValueError:
                return None
        return None
    m = re.match(r"rgba?\(([^)]*)\)", value)
    if m:
        parts = _floats(m.group(1))
        if len(parts) >= 3:
            return tuple(int(p) for p in parts[:3])
    return None


def _colour_key(rgb):
    """Collapse every near-black to one bucket; keep real hues apart."""
    luma = (0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]) / 255.0
    return "dark" if luma < DARK_LUMA else rgb


def _clip_is_noop(el, clips, box):
    """An export artefact: <g clip-path> whose clipPath is the whole viewBox.

    Five of Microsoft's monochrome icons ship like this. The clip does nothing,
    but an importer that just collects every `d` in the file picks up the
    clipPath rectangle and renders a filled square.
    """
    ref = el.get("clip-path")
    if not ref:
        return True
    m = re.match(r"url\(#(.+)\)", ref.strip())
    if not m or m.group(1) not in clips:
        return False
    nums = _floats(clips[m.group(1)])
    if len(nums) < 4:
        return False
    x, y, w, h = box
    return (abs(nums[0] - x) < 0.01 and abs(nums[1] - y) < 0.01
            and abs(max(nums) - max(w, h)) < 0.01)


def parse(source, name=None):
    """Read an SVG from a path or a string of markup."""
    if "<" in source[:200]:
        text = source
        name = name or "<svg>"
    else:
        with open(source, "r", encoding="utf-8-sig") as fp:
            text = fp.read()
        name = name or source
    try:
        root = ET.fromstring(text)
    except ET.ParseError as e:
        raise SvgError("%s: not valid XML (%s)" % (name, e))

    vb = _floats(root.get("viewBox")) if root.get("viewBox") else None
    if vb and len(vb) == 4:
        box = vb
    else:
        w, h = _floats(root.get("width")), _floats(root.get("height"))
        if not w or not h:
            raise SvgError("%s: no viewBox and no width/height" % name)
        box = [0.0, 0.0, w[0], h[0]]

    # Collect clipPath geometry up front; a clip is only ever accepted when it
    # turns out to be a no-op.
    clips = {}
    sheet = {}
    for el in root.iter():
        tag = _tag(el)
        if tag == "style":
            sheet.update(parse_stylesheet("".join(el.itertext())))
        elif tag == "clipPath" and el.get("id"):
            for child in el:
                d = _shape_path(child)
                if d:
                    clips[el.get("id")] = d
                    break

    contours = []
    warnings = []
    errors = []
    fills = []

    def walk(el, transform, inherited):
        for child in el:
            tag = _tag(child)
            if tag in IGNORED:
                continue
            paint = _paint(child, inherited, sheet)
            # Illustrator keeps rejected designs in the file behind
            # display:none. They are not part of the icon.
            if not paint[3]:
                continue
            ct = transform
            tr = child.get("transform")
            if tr:
                ct = transform.transform(parse_transform(tr))
            if child.get("clip-path") and not _clip_is_noop(child, clips, box):
                warnings.append("clip-path on <%s> is not a no-op and was ignored" % tag)
            if tag in ("g", "svg", "a"):
                walk(child, ct, paint)
                continue
            d = _shape_path(child)
            if d is None:
                errors.append("<%s> cannot be converted to an outline" % tag)
                continue
            fill, stroke, rule, _ = paint
            if stroke and stroke.strip() not in ("none", "transparent"):
                warnings.append("<%s> has stroke=%s; a stroke is not an outline and was "
                                "dropped" % (tag, stroke))
            if fill is not None and fill.strip() in ("none", "transparent"):
                continue
            if fill and fill.strip().startswith("url("):
                errors.append("<%s> is filled with a gradient or pattern (%s)" % (tag, fill))
            colour = parse_colour(fill)
            if colour:
                fills.append(colour)
            contours.append(Contour(d, ct, (rule or "").strip() == "evenodd"))

    walk(root, Identity, (None, None, None, True))

    if not contours:
        errors.append("no drawable geometry")
    distinct = {_colour_key(c) for c in fills}
    if len(distinct) > 1:
        errors.append("%d distinct fill colours - this icon is multicolour and cannot "
                      "become a monochrome glyph" % len(distinct))

    # Shift the viewBox origin to 0,0 so callers only deal with width/height.
    if box[0] or box[1]:
        shift = Transform().translate(-box[0], -box[1])
        contours = [Contour(c.d, shift.transform(c.transform), c.evenodd) for c in contours]

    return SvgArt(box[2], box[3], contours, warnings, errors)
