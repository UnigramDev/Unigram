"""Work out what a nameless glyph actually is, by matching its shape.

IcoMoon kept a name only for the icons someone typed one for. The rest arrived
as uniE0E2 and stayed that way, which makes them unsearchable and impossible to
re-point at a live source - name matching cannot help when there is no name.

So they are matched by what they draw. Every icon in the source is rasterised
once into a coarse bitmap, packed into an integer, and compared by Hamming
distance; the few best candidates are then re-compared at full resolution. A
comparison against twenty thousand icons has to be cheap, and one big XOR is.
"""

import json
import os

from iconfont import sources as sourcelib
from iconfont import tidy
from iconfont import svgdoc
from iconfont.raster import art_coverage, difference

COARSE = 32
FINE = 96
SHORTLIST = 12
NEIGHBOURS = 4

# Below this the artwork is the same drawing, so the icon can take the source's
# name and track it live.
IDENTICAL = 0.002
# Below this it is recognisably the same icon - a different size or weight, or
# edited here - so the name is worth taking but the local artwork is kept.
RELATED = 0.06


def signature(art, upem, ascent, descent, resolution=COARSE):
    bitmap = art_coverage(art, upem, ascent, descent, resolution)
    value = 0
    for cell in bitmap:
        value = (value << 1) | (1 if cell else 0)
    return value


def _cache_path(root, source, resolution):
    stamp = source.stamp.replace("/", "-").replace("@", "-").strip("-")
    return os.path.join(root, sourcelib.CACHE_DIR, "%s-sig%d.json" % (stamp, resolution))


def source_signatures(manifest, source, say, resolution=COARSE):
    """Coarse bitmaps for every icon in the source, computed once and cached."""
    path = _cache_path(manifest.root, source, resolution)
    if os.path.exists(path):
        with open(path, "r", encoding="utf-8") as fp:
            return {k: int(v, 16) for k, v in json.load(fp).items()}

    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))

    names = source.names()
    say("rasterising %d icons from %s (once; cached afterwards)"
        % (len(names), source.describe()))
    out = {}
    for n, name in enumerate(names):
        if n and n % 2500 == 0:
            say("   %d/%d" % (n, len(names)))
        try:
            art = svgdoc.parse(source.read(name), name=name)
            if art.errors:
                continue
            out[name] = signature(art, upem, ascent, descent, resolution)
        except Exception:
            continue
    with open(path, "w", encoding="utf-8") as fp:
        json.dump({k: "%x" % v for k, v in out.items()}, fp)
    return out


def run(manifest, source_name, targets, top, apply_changes, include_related,
        threshold, say, report_path=None, references=None, pinned=None):
    sources = sourcelib.build(manifest)
    source = sources.get(source_name)
    if source is None or not hasattr(source, "names"):
        say("%r is not a source that can be searched" % source_name)
        return 0

    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))

    library = source_signatures(manifest, source, say)
    items = list(library.items())
    say("")

    taken = {i.name: i for i in manifest.icons}
    named, related, duplicate, unknown = [], [], [], []

    # The nearest glyphs inside the font matter as much as the nearest upstream
    # ones: an unnamed glyph is often a second drawing of something the font
    # already has, and the answer to "what is this?" is sitting a few codepoints
    # away rather than in the library.
    own = {}
    for icon in manifest.icons:
        if icon.is_alias:
            continue
        try:
            art = svgdoc.parse(sourcelib.read(icon, sources), name=icon.src)
            cover = art_coverage(art, upem, ascent, descent, FINE)
            # A spacer paints nothing, so its distance from any glyph is just
            # that glyph's own ink - which for a thin icon looks like a close
            # match and pushes the real neighbours out of the list.
            if any(cover):
                own[icon.code] = (icon, cover)
        except Exception:
            continue
    neighbours = {}

    for icon in targets:
        try:
            art = svgdoc.parse(sourcelib.read(icon, sources), name=icon.src)
            mine = signature(art, upem, ascent, descent)
            fine = art_coverage(art, upem, ascent, descent, FINE)
        except Exception as e:
            unknown.append((icon, [], "%s: %s" % (type(e).__name__, e)))
            continue

        if not any(fine):
            # A few glyphs paint nothing at all - they hold a codepoint and an
            # advance and no more. Matching empty ink against a library of real
            # icons returns whichever one happens to be thinnest, which is worse
            # than no answer.
            unknown.append((icon, [], "paints nothing; it is a spacer glyph"))
            continue

        close = sorted(((difference(fine, cover), other)
                        for code, (other, cover) in own.items() if code != icon.code),
                       key=lambda r: r[0])[:NEIGHBOURS]
        wanted = (pinned or {}).get(icon.name)
        if wanted and wanted not in [o.name for _, o in close]:
            for _, (other, cover) in own.items():
                if other.name == wanted:
                    close.insert(0, (difference(fine, cover), other))
                    break
        neighbours[icon.code] = close

        shortlist = sorted(items, key=lambda kv: bin(kv[1] ^ mine).count("1"))[:SHORTLIST]
        scored = []
        for name, _ in shortlist:
            try:
                other = svgdoc.parse(source.read(name), name=name)
                scored.append((difference(fine, art_coverage(other, upem, ascent, descent, FINE)),
                               name))
            except Exception:
                continue
        # The package ships locale variants beside the plain icon under names
        # like en/text_bold_20_regular. They are the same drawing, so on a tie
        # the plain name wins - and a slash has no business in a glyph name.
        scored.sort(key=lambda r: (round(r[0], 4), "/" in r[1], len(r[1]), r[1]))
        if not scored:
            unknown.append((icon, [], "no candidates"))
            continue

        best, best_name = scored[0]
        full = "ic_fluent_" + best_name
        if best > RELATED:
            unknown.append((icon, scored[:top], None))
        elif full in taken:
            # Two glyphs matching the same upstream name does NOT make them the
            # same drawing - the comparison normalises everything into the em, so
            # two sizes of a simple icon land within a percent of each other.
            # The only test that settles it is comparing the two glyphs.
            other = taken[full]
            try:
                theirs = svgdoc.parse(sourcelib.read(other, sources), name=other.src)
                against = difference(fine, art_coverage(theirs, upem, ascent, descent, FINE))
            except Exception:
                against = None
            duplicate.append((icon, other, full, best, against))
        elif best <= IDENTICAL:
            named.append((icon, full, best_name, best, True))
            taken[full] = icon
        else:
            related.append((icon, full, best_name, best, scored[1:top]))
            taken[full] = icon

    if apply_changes:
        for icon, full, ident, _, _ in named:
            local = os.path.join(manifest.root, icon.src.replace("/", os.sep))
            icon.name = full
            icon.src = "%s:%s" % (source_name, ident)
            if os.path.exists(local):
                os.remove(local)
        if include_related:
            for icon, full, ident, diff, _ in related:
                if diff > threshold:
                    continue
                icon.name = full
                # Same icon, a different drawing: keep ours, but say what it is.
                icon.note = "local variant of %s:%s" % (source_name, ident)
                tidy.sync_file(manifest, icon)
        for icon, other, full, _, against in duplicate:
            if against is not None and against <= IDENTICAL:
                icon.note = "same artwork as %s at U+%04X" % (full, other.code)

    say("%d matched exactly and can track %s:" % (len(named), source.describe()))
    for icon, full, _, diff, _ in named:
        say("   U+%04X  %-46s %.2f%%" % (icon.code, full, diff * 100))
    say("")
    say("%d are recognisably the same icon, drawn differently here%s:"
        % (len(related), "" if include_related else " (needs confirming)"))
    for icon, full, _, diff, others in sorted(related, key=lambda r: r[3]):
        alternatives = ", ".join("%s %.0f%%" % (n, d * 100) for d, n in others)
        mark = " " if not include_related or diff <= threshold else "?"
        say(" %s U+%04X  %-44s %4.1f%%   then %s"
            % (mark, icon.code, full, diff * 100, alternatives))
    say("")
    real = [r for r in duplicate if r[4] is not None and r[4] <= IDENTICAL]
    say("%d match a name the font already uses; %d of those are the same drawing:"
        % (len(duplicate), len(real)))
    for icon, other, full, diff, against in sorted(duplicate, key=lambda r: (r[4] is None,
                                                                            r[4] or 0)):
        verdict = ("identical to it" if against is not None and against <= IDENTICAL
                   else "unknown" if against is None
                   else "a different drawing - %.1f%% apart" % (against * 100))
        say("   U+%04X  best match %s, already at U+%04X: %s"
            % (icon.code, full, other.code, verdict))
    say("")
    if report_path:
        from iconfont import report
        report.write(report_path, manifest, source,
                     {"related": related, "duplicate": duplicate, "unknown": unknown},
                     references or {}, source_name, neighbours)
        say("wrote %s" % report_path)
        say("")
    say("%d have no convincing match:" % len(unknown))
    for icon, scored, why in unknown:
        if why:
            say("   U+%04X  %-20s %s" % (icon.code, icon.name, why))
        else:
            guesses = ", ".join("%s (%.0f%%)" % (n, d * 100) for d, n in scored)
            say("   U+%04X  %-20s closest: %s" % (icon.code, icon.name, guesses))
    return len(named) + len(related) + len(duplicate)
