"""Assemble the manifest into Telegram.ttf."""

import os

from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib import newTable

from iconfont import sources as sourcelib
from iconfont import svgdoc
from iconfont.outline import art_to_glyph, natural_advance

# The three glyphs IcoMoon puts in front of the icons. They carry no artwork but
# the app has shipped with them mapped for years, so they stay.
LEADING = [(".notdef", None, 1024), ("uni0000", 0x0000, 0), ("uni0001", 0x0001, 0),
           ("space", 0x0020, 0)]

# Fixed so that two builds of the same manifest produce the same bytes. The
# date is arbitrary; only its constancy matters.
EPOCH = 3155673600  # 2004-01-01 in the TrueType epoch


class BuildError(Exception):
    pass


def glyph_name(code):
    return "uni%04X" % code if code <= 0xFFFF else "u%X" % code


class Result:
    def __init__(self, font, warnings, errors, origins):
        self.font = font
        self.warnings = warnings
        self.errors = errors
        self.origins = origins


def build(manifest, strict=True, sources=None):
    font_cfg = manifest.font
    upem = int(font_cfg.get("unitsPerEm", 1024))
    ascent = int(font_cfg.get("ascent", 960))
    descent = int(font_cfg.get("descent", 64))
    sources = sources or sourcelib.build(manifest)

    problems = manifest.validate()
    if problems and strict:
        raise BuildError("manifest is not valid:\n  " + "\n  ".join(problems))

    order = [name for name, _, _ in LEADING]
    glyphs = {name: _blank() for name, _, _ in LEADING}
    metrics = {name: (advance, 0) for name, _, advance in LEADING}
    cmap = {code: name for name, code, _ in LEADING if code is not None}

    warnings, errors, origins = [], list(problems), {}

    for icon in sorted(manifest.icons, key=lambda i: i.code):
        if icon.is_alias:
            continue
        name = glyph_name(icon.code)
        try:
            text = sourcelib.read(icon, sources)
            art = svgdoc.parse(text, name=icon.src)
        except (sourcelib.SourceError, svgdoc.SvgError) as e:
            errors.append("%s: %s" % (icon.name, e))
            continue
        trouble = art.errors
        if icon.blank and trouble == ["no drawable geometry"]:
            trouble = []
        for message in trouble:
            errors.append("%s (%s): %s" % (icon.name, icon.src, message))
        for message in art.warnings:
            warnings.append("%s (%s): %s" % (icon.name, icon.src, message))
        if trouble:
            continue
        try:
            glyph = art_to_glyph(art, upem, ascent)
        except svgdoc.SvgError as e:
            errors.append("%s (%s): %s" % (icon.name, icon.src, e))
            continue
        order.append(name)
        glyphs[name] = glyph
        # The side bearing has to be the real xMin. fontTools translates every
        # outline so that xMin equals the lsb it is given, so passing IcoMoon's
        # blanket 0 would shove all 663 glyphs against the left edge of the em.
        # IcoMoon got away with writing 0 because rasterisers draw the stored
        # coordinates and only use lsb for hinting.
        metrics[name] = (icon.advance if icon.advance is not None
                         else natural_advance(art, upem), _left(glyph))
        cmap[icon.code] = name
        origins[icon.name] = icon.src

    # Aliases add a cmap entry only: both codepoints resolve to the one glyph.
    for icon in sorted(manifest.icons, key=lambda i: i.code):
        if not icon.is_alias:
            continue
        target = glyph_name(icon.alias_code)
        if target not in glyphs:
            errors.append("%s aliases U+%04X, which was not built"
                          % (icon.name, icon.alias_code))
            continue
        if icon.advance is not None and icon.advance != metrics[target][0]:
            errors.append("%s aliases U+%04X but wants a different advance (%d vs %d)"
                          % (icon.name, icon.alias_code, icon.advance,
                             metrics[target][0]))
            continue
        cmap[icon.code] = target
        origins[icon.name] = icon.src

    if errors and strict:
        raise BuildError("%d icon(s) could not be built:\n  %s"
                         % (len(errors), "\n  ".join(errors)))

    fb = FontBuilder(upem, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=ascent, descent=-descent, lineGap=0)
    fb.setupNameTable(_names(font_cfg))
    fb.setupOS2(
        sTypoAscender=ascent, sTypoDescender=-descent, sTypoLineGap=descent,
        usWinAscent=ascent, usWinDescent=descent,
        sxHeight=0, sCapHeight=0, achVendID="NONE", fsType=0,
        ulUnicodeRange1=0x00000001,
    )
    fb.setupPost(keepGlyphNames=False)
    fb.updateHead(created=EPOCH, modified=EPOCH,
                  fontRevision=float(font_cfg.get("version", "1.0")),
                  lowestRecPPEM=8)
    # Bit 3 asks the rasteriser to round advances to whole pixels, which is what
    # the shipped font does; without it icon columns drift at some scales.
    fb.font["head"].flags = 0x000B
    gasp = newTable("gasp")
    gasp.version = 1
    gasp.gaspRange = {0xFFFF: 15}
    fb.font["gasp"] = gasp

    return Result(fb.font, warnings, errors, origins)


def _blank():
    from fontTools.pens.ttGlyphPen import TTGlyphPen
    return TTGlyphPen(None).glyph()


def _left(glyph):
    coordinates = getattr(glyph, "coordinates", None)
    return min((x for x, _ in coordinates), default=0) if coordinates else 0


def _names(cfg):
    family = cfg.get("family", "Telegram")
    version = str(cfg.get("version", "1.0"))
    return {
        "familyName": family,
        "styleName": "Regular",
        "uniqueFontIdentifier": family,
        "fullName": family,
        "version": "Version " + version,
        "psName": family,
        "manufacturer": cfg.get("manufacturer", "Generated by Tools/IconFont."),
    }


def save(result, path):
    directory = os.path.dirname(os.path.abspath(path))
    if directory and not os.path.isdir(directory):
        os.makedirs(directory)
    result.font.save(path)
