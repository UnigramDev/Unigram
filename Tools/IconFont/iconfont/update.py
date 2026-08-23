"""Move a live source to a newer version, and say what that changes.

The point of pinning is that a rebuild is reproducible; the point of updating is
that it does not stay frozen forever. Both are served by making the bump an
explicit step that reports, icon by icon, what the new version would draw
differently - and that refuses to lose an icon which upstream has since renamed
or deleted.
"""

import copy

from iconfont import sources as sourcelib
from iconfont import svgdoc
from iconfont.raster import art_coverage, difference

TOLERANCE = 0.002


def run(manifest, source_name, pin, apply_changes, say):
    config = manifest.sources.get(source_name)
    if config is None:
        say("no source named %r in the manifest" % source_name)
        return 1

    sources = sourcelib.build(manifest)
    current = sources[source_name]
    target_version = pin or current.latest_version()
    if target_version == config["version"]:
        say("%s is already at %s" % (source_name, target_version))
        return 0

    new_config = dict(config)
    new_config["version"] = target_version
    probe = sourcelib.NpmSource(manifest.root, new_config)

    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))

    tracked = [i for i in manifest.icons if i.source_kind == source_name]
    say("%s: %s -> %s, %d icon(s) tracked"
        % (source_name, config["version"], target_version, len(tracked)))

    vanished, changed, same, failed = [], [], 0, []
    for icon in tracked:
        ident = icon.source_id
        if not probe.contains(ident):
            vanished.append(icon)
            continue
        try:
            before = svgdoc.parse(current.read(ident), name=ident)
            after = svgdoc.parse(probe.read(ident), name=ident)
            diff = difference(art_coverage(before, upem, ascent, descent),
                              art_coverage(after, upem, ascent, descent))
        except Exception as e:
            failed.append((icon, "%s: %s" % (type(e).__name__, e)))
            continue
        if diff <= TOLERANCE:
            same += 1
        else:
            changed.append((icon, diff))

    say("")
    say("unchanged: %d" % same)
    if changed:
        say("redrawn upstream: %d" % len(changed))
        for icon, diff in sorted(changed, key=lambda r: -r[1]):
            say("   %-48s %5.1f%% of the em differs" % (icon.name, diff * 100))
    if failed:
        say("could not compare: %d" % len(failed))
        for icon, why in failed:
            say("   %-48s %s" % (icon.name, why))
    if vanished:
        say("")
        say("GONE from %s - renamed or removed upstream:" % target_version)
        for icon in vanished:
            say("   %-48s %s" % (icon.name, icon.src))
        say("")
        say("Refusing to update: bumping the pin would silently drop these glyphs.")
        say("Point each at a local file (or a new upstream name) first.")
        return 1

    if apply_changes:
        manifest.sources[source_name] = new_config
        manifest.save()
        say("")
        say("pinned %s at %s in %s" % (source_name, target_version, manifest.path))
    else:
        say("")
        say("dry run; pass --apply to move the pin")
    return 0
