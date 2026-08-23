"""A tiny scanline rasteriser, used to compare glyphs by what they paint.

Comparing outlines point by point is the wrong test: contour order and winding
direction are free choices that nonzero filling cannot see, so two glyphs that
render identically routinely disagree on both. What matters is the coverage, so
that is what gets compared.
"""

from fontTools.pens.recordingPen import RecordingPen

from iconfont.outline import _flatten, split_contours


def polygons(drawable, glyph_set=None):
    pen = RecordingPen()
    try:
        drawable.draw(pen, glyph_set) if glyph_set is not None else drawable.draw(pen)
    except TypeError:
        drawable.draw(pen)
    return [_flatten(c) for c in split_contours(pen.value)]


def coverage(polys, box, resolution=64):
    """Sample the nonzero-filled area over `box` into a resolution^2 bitmap."""
    x0, y0, x1, y1 = box
    width = (x1 - x0) / float(resolution)
    height = (y1 - y0) / float(resolution)
    edges = []
    for poly in polys:
        for i in range(len(poly)):
            ax, ay = poly[i - 1]
            bx, by = poly[i]
            if ay != by:
                edges.append((ax, ay, bx, by))
    bitmap = bytearray(resolution * resolution)
    if not edges:
        return bitmap
    for row in range(resolution):
        y = y0 + (row + 0.5) * height
        crossings = []
        for ax, ay, bx, by in edges:
            if (ay > y) != (by > y):
                crossings.append((ax + (bx - ax) * (y - ay) / (by - ay), 1 if by > ay else -1))
        if not crossings:
            continue
        crossings.sort()
        winding = 0
        base = row * resolution
        for i in range(len(crossings) - 1):
            winding += crossings[i][1]
            if winding == 0:
                continue
            start = int((crossings[i][0] - x0) / width + 0.5)
            end = int((crossings[i + 1][0] - x0) / width + 0.5)
            for col in range(max(start, 0), min(end, resolution)):
                bitmap[base + col] = 1
    return bitmap


def difference(a, b):
    """Fraction of sampled cells the two bitmaps disagree on."""
    if not a and not b:
        return 0.0
    wrong = sum(1 for x, y in zip(a, b) if x != y)
    return wrong / float(len(a))


def art_coverage(art, upem, ascent, descent, resolution=96):
    """Rasterise parsed SVG art in font coordinates."""
    from iconfont.outline import art_to_glyph
    glyph = art_to_glyph(art, upem, ascent)
    return coverage(polygons(glyph, {}), (0, -descent, upem, ascent), resolution)
