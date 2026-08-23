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


def select(manifest, picks):
    """Resolve `--only` arguments, which may be names or codepoints."""
    by_name, by_code = manifest.by_name(), manifest.by_code()
    chosen, unknown = [], []
    for pick in picks:
        pick = pick.strip()
        if not pick:
            continue
        icon = by_name.get(pick) or by_name.get(FLUENT_PREFIX + pick)
        if icon is None and pick.upper().startswith("U+"):
            try:
                icon = by_code.get(int(pick[2:], 16))
            except ValueError:
                icon = None
        if icon is None:
            unknown.append(pick)
        else:
            chosen.append(icon)
    return chosen, unknown


def run(manifest, source_name, tolerance, say, apply_changes=False, picks=None):
    sources = sourcelib.build(manifest)
    source = sources.get(source_name)
    if source is None:
        say("no source named %r in the manifest" % source_name)
        return 0

    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))

    # Picking icons by name is a deliberate choice to take upstream's drawing,
    # so the tolerance that guards the bulk pass does not apply to them.
    wanted, unknown = (select(manifest, picks) if picks else (None, []))
    for pick in unknown:
        say("no glyph called %r" % pick)

    matched, drifted, absent, failed = [], [], [], []
    for icon in (wanted if wanted is not None else manifest.icons):
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
        if diff <= tolerance or wanted is not None:
            matched.append((icon, ident, diff))
        else:
            drifted.append((icon, ident, diff))

    for icon, ident, _ in matched:
        if apply_changes:
            local = os.path.join(manifest.root, icon.src.replace("/", os.sep))
            if os.path.exists(local):
                os.remove(local)
        icon.src = "%s:%s" % (source_name, ident)
        # The note said this was a local variant of the very icon it now tracks.
        if icon.note and icon.note.startswith("local variant of"):
            icon.note = None

    say("%d icon(s) now track %s" % (len(matched), source.describe()))
    if wanted is not None:
        for icon, ident, diff in sorted(matched, key=lambda r: -r[2]):
            say("   U+%04X  %-46s the drawing changes by %.1f%%"
                % (icon.code, icon.name, diff * 100))
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
