"""Make the icons folder match the manifest.

Two things drift. A glyph that gets identified keeps the filename it was
extracted under, so `tl_instant_view_20_filled` still lives in `uniE611.svg`;
and a glyph that starts tracking a live source leaves its local copy behind.
Neither breaks the build - the manifest points at the right file either way -
but the folder stops being readable, which is most of why the artwork was
brought into the repository in the first place.
"""

import os

SUFFIX = ".svg"


def plan(manifest):
    """What the folder should contain, and how to get there from what it does."""
    root = manifest.root
    local = [i for i in manifest.icons if not i.is_alias and not i.is_remote]

    renames = []
    keep = set()
    for icon in local:
        current = icon.src.replace("/", os.sep)
        folder = os.path.dirname(current) or "icons"
        wanted = "%s/%s%s" % (folder.replace(os.sep, "/"), icon.name, SUFFIX)
        keep.add(os.path.basename(wanted).lower())
        if wanted != icon.src:
            renames.append((icon, icon.src, wanted))

    folders = {os.path.dirname(i.src.replace("/", os.sep)) or "icons" for i in local}
    on_disk = set()
    for folder in folders:
        directory = os.path.join(root, folder)
        if os.path.isdir(directory):
            for name in os.listdir(directory):
                if name.lower().endswith(SUFFIX):
                    on_disk.add("%s/%s" % (folder.replace(os.sep, "/"), name))

    # Anything not wanted afterwards is a leftover: a file whose icon now tracks
    # a live source, or a file nothing ever pointed at. The files a rename is
    # about to move are not leftovers - deletions run first, so counting them
    # here would delete the artwork and leave the rename with nothing to move.
    moving = {os.path.basename(old).lower() for _, old, _ in renames}
    orphans = sorted(p for p in on_disk
                     if os.path.basename(p).lower() not in keep
                     and os.path.basename(p).lower() not in moving)
    return renames, orphans


def run(manifest, apply_changes, say):
    root = manifest.root
    renames, orphans = plan(manifest)

    if apply_changes:
        # Leftovers go first: one of them may be sitting on a name a rename
        # wants, and on Windows that would fail or silently clobber.
        for rel in orphans:
            path = os.path.join(root, rel.replace("/", os.sep))
            if os.path.exists(path):
                os.remove(path)
        for icon, old, new in renames:
            source = os.path.join(root, old.replace("/", os.sep))
            target = os.path.join(root, new.replace("/", os.sep))
            if os.path.exists(source):
                os.replace(source, target)
            icon.src = new

    say("%d file(s) renamed to match the glyph they hold:" % len(renames))
    for icon, old, new in sorted(renames, key=lambda r: r[0].code)[:12]:
        say("   %-24s -> %s" % (os.path.basename(old), os.path.basename(new)))
    if len(renames) > 12:
        say("   ... and %d more" % (len(renames) - 12))
    say("")
    say("%d leftover file(s) nothing points at:" % len(orphans))
    for rel in orphans[:12]:
        say("   %s" % os.path.basename(rel))
    if len(orphans) > 12:
        say("   ... and %d more" % (len(orphans) - 12))
    return len(renames) + len(orphans)


def sync_file(manifest, icon):
    """Move a local icon's file so its name matches the glyph's."""
    if icon.is_alias or icon.is_remote:
        return
    folder = os.path.dirname(icon.src.replace("/", os.sep)) or "icons"
    wanted = "%s/%s%s" % (folder.replace(os.sep, "/"), icon.name, SUFFIX)
    if wanted == icon.src:
        return
    source = os.path.join(manifest.root, icon.src.replace("/", os.sep))
    target = os.path.join(manifest.root, wanted.replace("/", os.sep))
    if os.path.exists(source) and not os.path.exists(target):
        os.replace(source, target)
        icon.src = wanted
