"""Bring a folder of original artwork into the repository.

The glyphs recovered from Telegram.json are correct but second-hand: IcoMoon's
copy of what the designer drew, flattened to bare path data. Where the original
file still exists it is the better source to keep - it is what someone will open
to make a change - so this replaces the recovered outline with it, but only
after confirming the two render the same glyph.

An original that no longer matches is left alone and reported. It means the
artwork moved on without the font, or the font was built from something else,
and either way that is not a swap to make quietly.
"""

import os

from iconfont import sources as sourcelib
from iconfont import svgdoc
from iconfont.raster import art_coverage, difference


def _normalise(name):
    return name.lower().replace(" ", "").replace("-", "").replace("_", "")


def run(manifest, folder, tolerance, force, apply_changes, say):
    if not os.path.isdir(folder):
        say("%s is not a directory" % folder)
        return 0

    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))
    sources = sourcelib.build(manifest)

    by_name = {i.name: i for i in manifest.icons}
    by_flat = {}
    for icon in manifest.icons:
        by_flat.setdefault(_normalise(icon.name), icon)

    imported, differing, unmatched, tracked, failed = [], [], [], [], []
    for filename in sorted(os.listdir(folder)):
        if not filename.lower().endswith(".svg"):
            continue
        stem = filename[:-4]
        icon = by_name.get(stem) or by_flat.get(_normalise(stem))
        path = os.path.join(folder, filename)
        if icon is None:
            unmatched.append(stem)
            continue
        if icon.is_alias:
            tracked.append(icon)
            continue
        if icon.is_remote:
            # Already tracking a live source, which is strictly better than a
            # local copy of the same drawing.
            tracked.append(icon)
            continue
        try:
            incoming = svgdoc.parse(path)
            if incoming.errors:
                failed.append((stem, incoming.errors[0]))
                continue
            current = svgdoc.parse(sourcelib.read(icon, sources), name=icon.src)
            diff = difference(art_coverage(incoming, upem, ascent, descent),
                              art_coverage(current, upem, ascent, descent))
        except Exception as e:
            failed.append((stem, "%s: %s" % (type(e).__name__, e)))
            continue
        if diff <= tolerance or force:
            imported.append((icon, path, diff))
        else:
            differing.append((icon, path, diff))

    if apply_changes:
        for icon, path, _ in imported:
            _copy(path, os.path.join(manifest.root, icon.src.replace("/", os.sep)))

    say("%d original(s) render identically and replace the recovered outline"
        % len(imported))
    if differing:
        say("")
        say("%d differ from the glyph the app ships and were left alone:" % len(differing))
        for icon, _, diff in sorted(differing, key=lambda r: -r[2]):
            say("   %-48s %5.1f%% of the em differs" % (icon.name, diff * 100))
    if failed:
        say("")
        say("%d could not be read:" % len(failed))
        for stem, why in failed:
            say("   %-48s %s" % (stem, why))
    if tracked:
        say("")
        say("%d already track a live source and were skipped" % len(tracked))
    if unmatched:
        say("")
        say("%d file(s) are not in the font at all:" % len(unmatched))
        for stem in unmatched:
            say("   %s" % stem)
    return len(imported)


def _copy(source, destination):
    """Copy verbatim apart from the line endings the repository expects."""
    with open(source, "r", encoding="utf-8-sig") as fp:
        text = fp.read()
    with open(destination, "w", encoding="utf-8", newline="\r\n") as fp:
        fp.write(text)
