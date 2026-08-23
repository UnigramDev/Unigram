"""Command line for the icon font builder."""

import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOL))

DEFAULT_MANIFEST = os.path.join(TOOL, "icons.json")
DEFAULT_SVG_DIR = os.path.join(TOOL, "icons")
DEFAULT_FONT = os.path.join(REPO, "Telegram", "Assets", "Fonts", "Telegram.ttf")
DEFAULT_ICOMOON = os.path.join(REPO, "Telegram", "Assets", "Fonts", "Telegram.json")
DEFAULT_ICONS_CS = os.path.join(REPO, "Telegram", "Controls", "Media", "Icons.cs")

# Seeded by `extract`; `update` moves the pin.
DEFAULT_SOURCES = {
    "fluent": {
        "type": "npm",
        "package": "@fluentui/svg-icons",
        "version": "1.1.338",
        "prefix": "icons/",
    },
}


def _say(message=""):
    sys.stdout.write(message + "\n")


def _load(args):
    from iconfont.manifest import Manifest
    return Manifest.load(args.manifest)


def cmd_extract(args):
    from fontTools.ttLib import TTFont
    from iconfont import extract
    from iconfont.manifest import Manifest

    if os.path.exists(args.manifest) and not args.force:
        _say("%s already exists; pass --force to overwrite." % args.manifest)
        return 1

    with open(args.icomoon, "r", encoding="utf-8") as fp:
        data = json.load(fp)
    font = TTFont(args.font)

    version = ""
    for record in font["name"].names:
        if record.nameID == 5:
            version = record.toUnicode().replace("Version", "").strip()
            break

    config = {
        "family": font["name"].getDebugName(1) or "Telegram",
        "unitsPerEm": font["head"].unitsPerEm,
        "ascent": font["hhea"].ascent,
        "descent": -font["hhea"].descent,
        "version": version or "1.0",
    }
    icons, skipped = extract.run(data, font, args.out, config)
    manifest = Manifest(args.manifest, config, dict(DEFAULT_SOURCES), icons)
    manifest.save()

    _say("extracted %d glyphs to %s" % (len(icons), args.out))
    _say("wrote %s" % args.manifest)
    for line in skipped:
        _say("  note: " + line)
    return 0


def cmd_build(args):
    from iconfont import fontbuild
    manifest = _load(args)
    result = fontbuild.build(manifest, strict=not args.lax)
    for line in result.warnings:
        _say("  warning: " + line)
    for line in result.errors:
        _say("  error:   " + line)
    fontbuild.save(result, args.out)
    _say("built %d glyphs -> %s" % (len(result.origins), args.out))
    return 1 if result.errors else 0


def cmd_verify(args):
    from iconfont import verify
    report = verify.compare(args.built, args.reference, tolerance=args.tolerance)
    for line in report.lines():
        _say("  " + line)
    _say("%d glyphs render identically; %d differ" % (report.same, len(report.changed)))
    return 0 if report.ok else 1


def cmd_check(args):
    from iconfont import check
    manifest = _load(args)
    problems, notes = check.run(manifest, args.icons_cs, REPO)
    for line in problems:
        _say("  error: " + line)
    for line in notes:
        _say("  note:  " + line)
    _say("%d problem(s), %d note(s)" % (len(problems), len(notes)))
    return 1 if problems else 0


def cmd_sheet(args):
    from iconfont import check, sheet
    manifest = _load(args)
    refs = check.references(REPO, args.icons_cs) if args.only else {}
    path = sheet.write(manifest, args.out, refs, args.only)
    _say("wrote %s" % path)
    return 0


def cmd_adopt(args):
    from iconfont import adopt
    manifest = _load(args)
    picks = args.only.split(",") if args.only else None
    changed = adopt.run(manifest, args.source, args.tolerance, _say, args.apply, picks)
    if args.apply and changed:
        manifest.save()
        _say("updated %s" % manifest.path)
    elif changed:
        _say("dry run; pass --apply to write these into the manifest")
    return 0


def cmd_tidy(args):
    from iconfont import tidy
    manifest = _load(args)
    changed = tidy.run(manifest, args.apply, _say)
    if args.apply and changed:
        manifest.save()
        _say("")
        _say("updated %s" % manifest.path)
    elif changed:
        _say("")
        _say("dry run; pass --apply to rename and delete")
    return 0


def cmd_import(args):
    from iconfont import importer
    manifest = _load(args)
    count = importer.run(manifest, args.source_dir, args.tolerance, args.force,
                         args.apply, _say)
    if count and not args.apply:
        _say("")
        _say("dry run; pass --apply to copy these in")
    return 0


def cmd_identify(args):
    import re
    from iconfont import identify
    manifest = _load(args)
    placeholder = re.compile(r"u(ni)?[0-9A-Fa-f]{4,6}\Z")
    targets = [i for i in manifest.icons
               if (args.all or placeholder.match(i.name))
               and not i.is_remote and not i.is_alias]
    _say("%d icon(s) to identify" % len(targets))
    refs, pinned = {}, {}
    if args.report:
        from iconfont import check, rename
        refs = check.references(REPO, args.icons_cs)
        pinned = rename.comparisons(os.path.join(TOOL, "identified.txt"))
    changed = identify.run(manifest, args.source, targets, args.top, args.apply,
                           args.include_related, args.threshold, _say,
                           args.report, refs, pinned)
    if args.apply and changed:
        manifest.save()
        _say("")
        _say("updated %s" % manifest.path)
    elif changed:
        _say("")
        _say("dry run; pass --apply to rename these in the manifest")
    return 0


def cmd_drift(args):
    from iconfont import check, drift
    manifest = _load(args)
    refs = check.references(REPO, args.icons_cs)
    path, drifted, gone = drift.write(args.out, manifest, args.source, refs)
    _say("%d drifted, %d gone from the package -> %s" % (drifted, gone, path))
    return 0


def cmd_missing(args):
    from fontTools.ttLib import TTFont
    from iconfont import missing
    manifest = _load(args)
    reference = TTFont(args.reference) if args.reference else None
    source = None
    if args.suggest:
        from iconfont import sources as sourcelib
        source = sourcelib.build(manifest).get(args.source)
    path, codes = missing.write(args.out, manifest, REPO, args.icons_cs, reference, source)
    for code in codes:
        _say("   U+%04X" % code)
    _say("%d codepoint(s) referenced with no glyph -> %s" % (len(codes), path))
    return 0


def cmd_rename(args):
    from iconfont import rename
    manifest = _load(args)
    changed = rename.run(manifest, args.list, args.source, args.apply, _say)
    if args.apply and changed:
        manifest.save()
        _say("")
        _say("updated %s" % manifest.path)
    elif changed:
        _say("")
        _say("dry run; pass --apply to write these into the manifest")
    return 0


def cmd_update(args):
    from iconfont import update
    manifest = _load(args)
    return update.run(manifest, args.source, args.pin, args.apply, _say)


def main(argv=None):
    parser = argparse.ArgumentParser(
        prog="iconfont", description="Build Telegram.ttf from SVG sources.")
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST)
    sub = parser.add_subparsers(dest="command")

    p = sub.add_parser("extract", help="one-time migration out of IcoMoon")
    p.add_argument("--icomoon", default=DEFAULT_ICOMOON)
    p.add_argument("--font", default=DEFAULT_FONT)
    p.add_argument("--out", default=DEFAULT_SVG_DIR)
    p.add_argument("--force", action="store_true")
    p.set_defaults(func=cmd_extract)

    p = sub.add_parser("build", help="build the font from the manifest")
    p.add_argument("--out", default=DEFAULT_FONT)
    p.add_argument("--lax", action="store_true", help="build despite errors")
    p.set_defaults(func=cmd_build)

    p = sub.add_parser("verify", help="compare a built font against a reference")
    p.add_argument("--built", default=DEFAULT_FONT)
    p.add_argument("--reference", required=True)
    p.add_argument("--tolerance", type=float, default=0.002)
    p.set_defaults(func=cmd_verify)

    p = sub.add_parser("check", help="validate the manifest against the app")
    p.add_argument("--icons-cs", dest="icons_cs", default=DEFAULT_ICONS_CS)
    p.set_defaults(func=cmd_check)

    p = sub.add_parser("sheet", help="write a contact sheet of every glyph")
    p.add_argument("--out", default=os.path.join(TOOL, "contact-sheet.html"))
    p.add_argument("--only", choices=("used", "unused"),
                   help="restrict to glyphs the app does or does not reference")
    p.add_argument("--icons-cs", dest="icons_cs", default=DEFAULT_ICONS_CS)
    p.set_defaults(func=cmd_sheet)

    p = sub.add_parser("adopt", help="re-point local icons at a live source")
    p.add_argument("--source", default="fluent")
    p.add_argument("--only", help="comma-separated names or U+XXXX codepoints to take "
                                  "regardless of how far they have drifted")
    p.add_argument("--tolerance", type=float, default=0.002)
    p.add_argument("--apply", action="store_true")
    p.set_defaults(func=cmd_adopt)

    p = sub.add_parser("tidy", help="make the icons folder match the manifest")
    p.add_argument("--apply", action="store_true")
    p.set_defaults(func=cmd_tidy)

    p = sub.add_parser("import", help="bring original artwork in from a folder")
    p.add_argument("--from", dest="source_dir", required=True)
    p.add_argument("--tolerance", type=float, default=0.002)
    p.add_argument("--force", action="store_true",
                   help="import even where the artwork has diverged")
    p.add_argument("--apply", action="store_true")
    p.set_defaults(func=cmd_import)

    p = sub.add_parser("identify", help="name nameless glyphs by matching their shape")
    p.add_argument("--source", default="fluent")
    p.add_argument("--all", action="store_true",
                   help="search every local icon, not just the uniXXXX ones")
    p.add_argument("--top", type=int, default=3)
    p.add_argument("--include-related", dest="include_related", action="store_true",
                   help="also rename the near-misses, not just the exact matches")
    p.add_argument("--threshold", type=float, default=0.025,
                   help="how far a near-miss may differ and still be renamed")
    p.add_argument("--report", nargs="?", const=os.path.join(TOOL, "icon-review.html"),
                   help="write a side-by-side review page for the unsettled ones")
    p.add_argument("--icons-cs", dest="icons_cs", default=DEFAULT_ICONS_CS)
    p.add_argument("--apply", action="store_true")
    p.set_defaults(func=cmd_identify)

    p = sub.add_parser("drift", help="local glyphs whose upstream namesake has changed")
    p.add_argument("--out", default=os.path.join(TOOL, "drift.html"))
    p.add_argument("--source", default="fluent")
    p.add_argument("--icons-cs", dest="icons_cs", default=DEFAULT_ICONS_CS)
    p.set_defaults(func=cmd_drift)

    p = sub.add_parser("missing", help="codepoints the app uses that the font lacks")
    p.add_argument("--out", default=os.path.join(TOOL, "missing-icons.html"))
    p.add_argument("--reference", help="a font to show what each codepoint used to draw")
    p.add_argument("--suggest", action="store_true",
                   help="offer replacements from the live source for each one")
    p.add_argument("--source", default="fluent")
    p.add_argument("--icons-cs", dest="icons_cs", default=DEFAULT_ICONS_CS)
    p.set_defaults(func=cmd_missing)

    p = sub.add_parser("rename", help="apply a hand-written identification list")
    p.add_argument("--list", default=os.path.join(TOOL, "identified.txt"))
    p.add_argument("--source", default="fluent")
    p.add_argument("--apply", action="store_true")
    p.set_defaults(func=cmd_rename)

    p = sub.add_parser("update", help="pull newer artwork from a live source")
    p.add_argument("--source", default="fluent")
    p.add_argument("--pin", help="version to move to (default: the latest)")
    p.add_argument("--apply", action="store_true")
    p.set_defaults(func=cmd_update)

    args = parser.parse_args(argv)
    if not getattr(args, "func", None):
        parser.print_help()
        return 2
    from iconfont.fontbuild import BuildError
    from iconfont.sources import SourceError
    from iconfont.svgdoc import SvgError
    try:
        return args.func(args)
    except (BuildError, SourceError, SvgError) as e:
        sys.stderr.write(str(e) + "\n")
        return 1
