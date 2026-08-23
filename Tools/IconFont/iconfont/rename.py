"""Apply a hand-written identification list to the manifest.

The shape matcher gets most of the way and then stops being trustworthy: it
cannot tell `tab` from `open`, or a 20px drawing from the 48px one, because it
compares normalised ink. A person reading the call site can. This applies that
judgement, and does the parts a person should not have to: checking every name
exists upstream, refusing to reuse one, and deciding - by comparison, not by
assertion - whether a renamed glyph can also track the live source.
"""

import os
import re

from iconfont import sources as sourcelib
from iconfont import tidy
from iconfont import svgdoc
from iconfont.raster import art_coverage, difference

LINE = re.compile(r"^\s*(\S+)\s*->\s*(.+?)\s*$")
# `(confirmed)` means a person compared the two on screen and called them the
# same. Measurement can only say how far apart the ink is, and a fraction of a
# percent is below what an eye resolves at icon sizes, so the person wins.
DUPLICATE = re.compile(r"^duplicate of (\S+)(\s+\(confirmed\))?$")
# `# compare with <name>` on any line pins that glyph beside this one in the
# review page. Coverage distance does not always put the right pair together:
# one closed padlock can be further from another closed padlock than from an
# open one.
COMPARE = re.compile(r"^\s*(\S+)\s*->.*#\s*compare with\s+(\S+)", re.M)
# IcoMoon appended a digit when importing under a name already in use.
COLLISION = re.compile(r"^ic_fluent_.+[0-9]$")

IDENTICAL = 0.002
PREFIX = "ic_fluent_"
LOCAL_PREFIX = "tl_"


def _wanted_name(target):
    """The name a non-duplicate line asks the glyph to end up with."""
    if target.startswith(LOCAL_PREFIX):
        return target
    return PREFIX + (target[len(PREFIX):] if target.startswith(PREFIX) else target)


def parse(path):
    entries = []
    with open(path, "r", encoding="utf-8-sig") as fp:
        for number, line in enumerate(fp, 1):
            if not line.strip() or line.lstrip().startswith("#"):
                continue
            hit = LINE.match(line.split("#", 1)[0])
            if hit:
                entries.append((number, hit.group(1), hit.group(2)))
            elif line.split("#", 1)[0].strip():
                entries.append((number, None, line.strip()))
    return entries


def run(manifest, path, source_name, apply_changes, say):
    sources = sourcelib.build(manifest)
    source = sources.get(source_name)
    cfg = manifest.font
    upem = int(cfg.get("unitsPerEm", 1024))
    ascent = int(cfg.get("ascent", 960))
    descent = int(cfg.get("descent", 64))

    by_name = {i.name: i for i in manifest.icons}
    by_code = manifest.by_code()
    reserved = {i.name for i in manifest.icons}

    def coverage_of(icon):
        holder = manifest.resolve(icon)
        art = svgdoc.parse(sourcelib.read(holder, sources), name=holder.src)
        return art_coverage(art, upem, ascent, descent, 96)

    renamed, adopted, aliased, refused, skipped, bad = [], [], [], [], [], []
    already = []
    planned = []

    for number, placeholder, target in parse(path):
        if placeholder is None:
            bad.append((number, "could not parse %r" % target))
            continue
        if target.lower() == "tbd":
            skipped.append(placeholder)
            continue
        icon = by_name.get(placeholder)
        if icon is None and placeholder.upper().startswith("U+"):
            icon = by_code.get(int(placeholder[2:], 16))
        if icon is None:
            # The list is checked in and outlives the rename, so a line whose
            # placeholder is gone is normally one already applied, not an error.
            duplicate = DUPLICATE.match(target)
            wanted = duplicate.group(1) if duplicate else _wanted_name(target)
            if wanted in by_name or duplicate:
                already.append(placeholder)
            else:
                bad.append((number, "%s is not a glyph in this font" % placeholder))
            continue

        duplicate = DUPLICATE.match(target)
        if not duplicate and icon.name == _wanted_name(target):
            # Looking a glyph up by codepoint always finds it, so unlike the
            # name form there is nothing to say the line has already been
            # applied - except that the glyph already carries the name.
            already.append(placeholder)
            continue

        if duplicate:
            other = by_name.get(duplicate.group(1))
            if other is not None and icon.is_alias and icon.alias_code == other.code:
                already.append(placeholder)
                continue
            if other is None:
                bad.append((number, "%s: no glyph named %s" % (placeholder, duplicate.group(1))))
                continue
            try:
                apart = difference(coverage_of(icon), coverage_of(other))
            except Exception as e:
                bad.append((number, "%s: %s" % (placeholder, e)))
                continue
            if apart <= IDENTICAL or duplicate.group(2):
                aliased.append((icon, other, apart))
            else:
                # Named as a duplicate, but the two glyphs are not the same
                # drawing. Sharing one outline would change what the app renders
                # at this codepoint, so it keeps its own.
                refused.append((icon, other, apart))
            continue

        # `tl_` is the repository's own prefix for artwork Telegram drew, and
        # there is nothing upstream to check it against or to track.
        if target.startswith(LOCAL_PREFIX):
            if target in reserved and by_name.get(target) is not icon:
                bad.append((number, "%s: the name %s is already used by U+%04X"
                            % (placeholder, target, by_name[target].code)))
                continue
            reserved.discard(icon.name)
            reserved.add(target)
            planned.append((icon, target, None, 1.0))
            renamed.append((icon, target, None, None))
            continue

        # The list is written by hand and the prefix is optional either way:
        # `zoom_out_20_regular` and `ic_fluent_zoom_out_20_regular` mean the same.
        target = target[len(PREFIX):] if target.startswith(PREFIX) else target
        full = PREFIX + target
        if source is None or not source.contains(target):
            bad.append((number, "%s: %s has no icon named %r"
                        % (placeholder, source_name if source else "source", target)))
            continue
        if full in reserved and by_name.get(full) is not icon:
            bad.append((number, "%s: the name %s is already used by U+%04X"
                        % (placeholder, full, by_name[full].code)))
            continue
        try:
            apart = difference(coverage_of(icon),
                               art_coverage(svgdoc.parse(source.read(target), name=target),
                                            upem, ascent, descent, 96))
        except Exception as e:
            bad.append((number, "%s: %s" % (placeholder, e)))
            continue
        reserved.discard(icon.name)
        reserved.add(full)
        planned.append((icon, full, target, apart))
        if apart <= IDENTICAL:
            adopted.append((icon, full, target, apart))
        else:
            renamed.append((icon, full, target, apart))

    if apply_changes:
        for icon, full, target, apart in planned:
            local = os.path.join(manifest.root, icon.src.replace("/", os.sep))
            icon.name = full
            if target is None:
                tidy.sync_file(manifest, icon)
                continue
            if apart <= IDENTICAL:
                icon.src = "%s:%s" % (source_name, target)
                if os.path.exists(local):
                    os.remove(local)
            else:
                icon.note = "local variant of %s:%s" % (source_name, target)
                tidy.sync_file(manifest, icon)
        for icon, other, apart in aliased:
            local = os.path.join(manifest.root, icon.src.replace("/", os.sep))
            # An alias needs no name of its own - the note says what it shares -
            # and keeping a name like ic_fluent_call_24_filled1 would go on
            # implying an upstream icon that has never existed.
            if COLLISION.match(icon.name):
                icon.name = "uni%04X" % icon.code
            icon.src = "alias:%04X" % other.code
            icon.advance = None
            icon.note = "same glyph as %s" % other.name
            if apart > IDENTICAL:
                icon.note += " (its own drawing was %.1f%% different)" % (apart * 100)
            if os.path.exists(local):
                os.remove(local)
        for icon, other, apart in refused:
            icon.note = ("meant as a duplicate of %s at U+%04X, but %.1f%% apart"
                         % (other.name, other.code, apart * 100))

    say("%d renamed and now track %s (the drawing still matches):"
        % (len(adopted), source.describe() if source else source_name))
    for icon, full, _, apart in sorted(adopted, key=lambda r: r[0].code):
        say("   U+%04X  %s" % (icon.code, full))
    say("")
    say("%d renamed, keeping their own artwork:" % len(renamed))
    for icon, full, _, apart in sorted(renamed, key=lambda r: r[0].code):
        say("   U+%04X  %-46s %s"
            % (icon.code, full, "Telegram's own artwork" if apart is None
               else "%4.1f%% from upstream" % (apart * 100)))
    say("")
    say("%d share a glyph with another codepoint and no longer store their own:"
        % len(aliased))
    for icon, other, apart in sorted(aliased, key=lambda r: r[0].code):
        say("   U+%04X  -> U+%04X  %-46s%s"
            % (icon.code, other.code, other.name,
               "" if apart <= IDENTICAL else "  (was %.1f%% apart)" % (apart * 100)))
    if refused:
        say("")
        say("%d were listed as duplicates but are NOT the same drawing, so they keep "
            "their own glyph:" % len(refused))
        for icon, other, apart in sorted(refused, key=lambda r: -r[2]):
            say("   U+%04X  %.1f%% from %s at U+%04X"
                % (icon.code, apart * 100, other.name, other.code))
    if already:
        say("")
        say("%d line(s) were applied by an earlier run" % len(already))
    if skipped:
        say("")
        say("%d still marked tbd: %s"
            % (len(skipped), ", ".join(sorted(skipped))))
    if bad:
        say("")
        say("%d line(s) could not be applied:" % len(bad))
        for number, why in bad:
            say("   line %d: %s" % (number, why))
    return len(planned) + len(aliased) + len(refused)


def comparisons(path):
    """Glyph pairs a person asked to see side by side."""
    if not os.path.exists(path):
        return {}
    with open(path, "r", encoding="utf-8-sig") as fp:
        return dict(COMPARE.findall(fp.read()))
