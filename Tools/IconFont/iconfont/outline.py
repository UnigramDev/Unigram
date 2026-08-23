"""Turn SVG artwork into a TrueType glyph.

Two coordinate systems meet here. SVG is y-down with the origin top-left and an
icon-sized viewBox; the font is y-up with the origin on the baseline, which sits
6.25% of the em above the bottom of the design box. The mapping is therefore
`scale(upem / height), flip y, translate to ascent` - the same transform IcoMoon
used, which is why glyphs built here land on the shipped ones.

TrueType fills by the nonzero rule and has no way to express evenodd, so evenodd
artwork is converted: contours are re-wound by nesting depth, which renders
identically for the non-self-intersecting outlines icon art is made of.
"""

import re

from fontTools.misc.transform import Transform
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.pointPen import ReverseContourPointPen
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.svgLib.path import parse_path

from iconfont.svgdoc import SvgError

# Maximum deviation, in font units, allowed when the cubics that SVG uses are
# approximated by the quadratics TrueType stores. A 1024 em rendered at 20px
# means one font unit is a fiftieth of a pixel.
CU2QU_ERROR = 0.5

# A drawing command straight after a closepath, with no moveto in between.
_AFTER_CLOSE = re.compile(r"[zZ]\s*([LlHhVvCcSsQqTtAa])")


def _raw(d):
    pen = RecordingPen()
    try:
        parse_path(d, pen)
    except Exception as e:
        raise SvgError("bad path data (%s)" % e)
    return pen.value


def _parse(d):
    """Parse `d`, making the move that `z` implies explicit.

    After a closepath the current point returns to where the subpath began, and
    a drawing command may follow without a moveto. SVGO emits exactly that (four
    of Microsoft's icons end `...1.5zh7.5z`) and fontTools' parser loses the
    point, so the moveto is written out before handing the rest over.
    """
    ops = []
    while True:
        match = _AFTER_CLOSE.search(d)
        if not match:
            return ops + _raw(d)
        head, d = d[:match.start(1)], d[match.start(1):]
        recorded = _raw(head)
        ops.extend(recorded)
        start = next((args[0] for op, args in reversed(recorded) if op == "moveTo"), None)
        if start is None:
            return ops + _raw(d)
        d = "M%r %r%s" % (start[0], start[1], d)


def _record(d, transform):
    """Parse one `d` and replay it under `transform`, closing open subpaths.

    Icon SVGs routinely leave the final subpath unclosed. For a fill that is
    implicit, but a font glyph has no such thing as an open contour.
    """
    out = RecordingPen()
    pen = TransformPen(out, transform)
    for op, args in _parse(d):
        if op == "endPath":
            pen.closePath()
        else:
            getattr(pen, op)(*args)
    return out.value


def split_contours(value):
    """Split a recorded pen into one list of operations per closed contour."""
    contours, current = [], []
    for op, args in value:
        if op == "moveTo":
            if current:
                current.append(("closePath", ()))
                contours.append(current)
            current = [(op, args)]
        elif op in ("closePath", "endPath"):
            current.append(("closePath", ()))
            contours.append(current)
            current = []
        elif current:
            current.append((op, args))
    if current:
        current.append(("closePath", ()))
        contours.append(current)
    return [c for c in contours if len(c) > 2]


def _flatten(contour, steps=12):
    """A polygon approximation, good enough for area sign and containment."""
    pts, cur = [], (0.0, 0.0)
    for op, args in contour:
        if op == "moveTo":
            cur = args[0]
            pts.append(cur)
        elif op == "lineTo":
            cur = args[0]
            pts.append(cur)
        elif op == "curveTo":
            p0, (p1, p2, p3) = cur, args
            for i in range(1, steps + 1):
                t = i / float(steps)
                u = 1.0 - t
                pts.append((u * u * u * p0[0] + 3 * u * u * t * p1[0]
                            + 3 * u * t * t * p2[0] + t * t * t * p3[0],
                            u * u * u * p0[1] + 3 * u * u * t * p1[1]
                            + 3 * u * t * t * p2[1] + t * t * t * p3[1]))
            cur = p3
        elif op == "qCurveTo":
            points = list(args)
            on = points.pop()
            if on is None:  # all-offcurve TrueType special case
                on = points[0]
            prev = cur
            for n, ctrl in enumerate(points):
                end = on if n == len(points) - 1 else ((ctrl[0] + points[n + 1][0]) / 2.0,
                                                       (ctrl[1] + points[n + 1][1]) / 2.0)
                for i in range(1, steps + 1):
                    t = i / float(steps)
                    u = 1.0 - t
                    pts.append((u * u * prev[0] + 2 * u * t * ctrl[0] + t * t * end[0],
                                u * u * prev[1] + 2 * u * t * ctrl[1] + t * t * end[1]))
                prev = end
            cur = on
    return pts


def _area(polygon):
    total = 0.0
    for i in range(len(polygon)):
        x0, y0 = polygon[i - 1]
        x1, y1 = polygon[i]
        total += x0 * y1 - x1 * y0
    return total / 2.0


def _contains(polygon, point):
    x, y = point
    inside = False
    for i in range(len(polygon)):
        x0, y0 = polygon[i - 1]
        x1, y1 = polygon[i]
        if (y0 > y) != (y1 > y):
            if x < (x1 - x0) * (y - y0) / (y1 - y0) + x0:
                inside = not inside
    return inside


def _rewind(contours):
    """Re-wind contours so nonzero filling reproduces evenodd.

    Depth-even contours are made clockwise and depth-odd counter-clockwise, so a
    shape inside a shape cancels out into a hole. Absolute direction is
    irrelevant to nonzero filling; only the alternation matters.
    """
    polygons = [_flatten(c) for c in contours]
    out = []
    for i, contour in enumerate(contours):
        if len(polygons[i]) < 3:
            out.append(contour)
            continue
        depth = sum(1 for j, other in enumerate(polygons)
                    if j != i and len(other) >= 3 and _contains(other, polygons[i][0]))
        want_positive = depth % 2 == 1
        if (_area(polygons[i]) > 0) != want_positive:
            out.append(_reverse(contour))
        else:
            out.append(contour)
    return out


def _reverse(contour):
    from fontTools.pens.pointPen import SegmentToPointPen, PointToSegmentPen
    out = RecordingPen()
    pen = SegmentToPointPen(ReverseContourPointPen(PointToSegmentPen(out)))
    for op, args in contour:
        getattr(pen, op)(*args)
    return out.value


def art_to_glyph(art, upem, ascent, glyph_set=None, error=CU2QU_ERROR):
    """Draw parsed SVG art as a TrueType glyph."""
    scale = float(upem) / art.height
    to_font = Transform(scale, 0, 0, -scale, 0, ascent)

    contours = []
    for element in art.contours:
        recorded = _record(element.d, to_font.transform(element.transform))
        split = split_contours(recorded)
        # evenodd is a per-element rule in SVG, so nesting is resolved within
        # the element that declared it.
        contours.extend(_rewind(split) if element.evenodd else split)

    pen = TTGlyphPen(glyph_set)
    quad = Cu2QuPen(pen, error)
    for contour in contours:
        for op, args in contour:
            getattr(quad, op)(*args)
    return pen.glyph()


def natural_advance(art, upem):
    """Advance implied by the artwork's aspect: a 45x20 viewBox is 2.25 ems."""
    return int(round(float(upem) * art.width / art.height))
