"""Re-point local artwork at a live source.

Extraction leaves every icon pointing at a checked-in file, which is correct but
gives up the reason for having a live source at all. This walks the local icons,
finds the ones a remote source also has under the same name, and switches those
whose artwork still matches - so a rebuild picks up Microsoft's current drawing
instead of a copy frozen at whatever date it was imported.

Only icons that match are switched. One that has drifted stays local: either it
was deliberately modified here, or upstream redrew it, and neither is a decision
this tool should make silently.
"""

import os

from iconfont import sources as sourcelib
from iconfont import svgdoc
from iconfont.raster import art_coverage, difference

# Microsoft's own names carry this; the npm package drops it.
FLUENT_PREFIX = "ic_fluent_"


def candidate_id(icon, source):
    """The identifier this icon would have in the remote source, if any."""
    name = icon.name
    if name.startswith(FLUENT_PREFIX):
        name = name[len(FLUENT_PREFIX):]
    return name if getattr(source, "contains", None) and source.contains(name) else None


def run(manifest, source_name, tolerance, say, apply_changes=False):
    sources = sourcelib.build(manifest)
    source = sources.get(source_name)
    if source is None:
        say("no source named %r in the manifest" % source_name)
        return 0

    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))

    matched, drifted, absent, failed = [], [], [], []
    for icon in manifest.icons:
        if icon.is_remote or icon.is_alias:
            continue
        ident = candidate_id(icon, source)
        if ident is None:
            absent.append(icon)
            continue
        try:
            local = svgdoc.parse(sourcelib.read(icon, sources), name=icon.src)
            remote = svgdoc.parse(source.read(ident), name=ident)
            if local.errors or remote.errors:
                failed.append((icon, (local.errors + remote.errors)[0]))
                continue
            diff = difference(art_coverage(local, upem, ascent, descent),
                              art_coverage(remote, upem, ascent, descent))
        except Exception as e:
            failed.append((icon, "%s: %s" % (type(e).__name__, e)))
            continue
        if diff <= tolerance:
            matched.append((icon, ident))
        else:
            drifted.append((icon, ident, diff))

    for icon, ident in matched:
        if apply_changes:
            local = os.path.join(manifest.root, icon.src.replace("/", os.sep))
            if os.path.exists(local):
                os.remove(local)
        icon.src = "%s:%s" % (source_name, ident)

    say("%d icon(s) now track %s" % (len(matched), source.describe()))
    if drifted:
        say("")
        say("%d have the same name upstream but different artwork, and stay local:"
            % len(drifted))
        for icon, ident, diff in sorted(drifted, key=lambda r: -r[2]):
            say("   %-48s %5.1f%% of the em differs" % (icon.name, diff * 100))
    if failed:
        say("")
        say("%d could not be compared:" % len(failed))
        for icon, why in failed:
            say("   %-48s %s" % (icon.name, why))
    say("")
    say("%d have no counterpart in %s and remain local artwork"
        % (len(absent), source_name))
    return len(matched)
