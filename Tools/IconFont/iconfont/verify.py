"""Compare a built font against a reference, by what it renders.

The question this answers is the only one that matters when replacing the build
pipeline of a font 763 hard-coded literals point at: does every codepoint still
paint the same shape at the same width? Outlines are compared as coverage rather
than as points, because contour order and winding direction are free choices
that nonzero filling cannot see.
"""

from fontTools.ttLib import TTFont

from iconfont.raster import coverage, difference, polygons

RESOLUTION = 96

# A glyph is called unchanged below this fraction of differing sample cells.
# Cubic-to-quadratic conversion moves edges by a fraction of a font unit, which
# lands a handful of boundary samples on the other side.
SAME = 0.002


class Report:
    def __init__(self):
        self.missing = []
        self.added = []
        self.advance = []
        self.changed = []
        self.same = 0
        self.metrics = []

    @property
    def ok(self):
        return not (self.missing or self.added or self.advance or self.changed
                    or self.metrics)

    def lines(self):
        out = []
        for code in self.missing:
            out.append("U+%04X is in the reference but missing from the build" % code)
        for code in self.added:
            out.append("U+%04X is new in the build" % code)
        for code, was, now in self.advance:
            out.append("U+%04X advance changed %d -> %d" % (code, was, now))
        for code, diff in sorted(self.changed, key=lambda r: -r[1]):
            out.append("U+%04X renders differently (%.2f%% of the em)" % (code, diff * 100))
        out.extend(self.metrics)
        return out


def _metrics(font):
    return {
        "unitsPerEm": font["head"].unitsPerEm,
        "ascent": font["hhea"].ascent,
        "descent": font["hhea"].descent,
        "typoAscender": font["OS/2"].sTypoAscender,
        "typoDescender": font["OS/2"].sTypoDescender,
        "winAscent": font["OS/2"].usWinAscent,
        "winDescent": font["OS/2"].usWinDescent,
    }


def compare(built, reference, resolution=RESOLUTION, tolerance=SAME):
    if isinstance(built, str):
        built = TTFont(built)
    if isinstance(reference, str):
        reference = TTFont(reference)

    report = Report()
    a, b = _metrics(built), _metrics(reference)
    for key in sorted(a):
        if a[key] != b[key]:
            report.metrics.append("%s changed %s -> %s" % (key, b[key], a[key]))

    upem = a["unitsPerEm"]
    box = (0, a["descent"], upem, a["ascent"])

    built_map, ref_map = built.getBestCmap(), reference.getBestCmap()
    built_set, ref_set = built.getGlyphSet(), reference.getGlyphSet()
    built_mtx, ref_mtx = built["hmtx"].metrics, reference["hmtx"].metrics

    report.missing = sorted(set(ref_map) - set(built_map))
    report.added = sorted(set(built_map) - set(ref_map))

    for code in sorted(set(built_map) & set(ref_map)):
        bg, rg = built_map[code], ref_map[code]
        if built_mtx[bg][0] != ref_mtx[rg][0]:
            report.advance.append((code, ref_mtx[rg][0], built_mtx[bg][0]))
        diff = difference(
            coverage(polygons(built_set[bg]), box, resolution),
            coverage(polygons(ref_set[rg]), box, resolution),
        )
        if diff > tolerance:
            report.changed.append((code, diff))
        else:
            report.same += 1
    return report
