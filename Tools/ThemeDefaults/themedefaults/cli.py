"""Command line for the theme defaults packer."""

import argparse
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOL))

DEFAULT_LIGHT = os.path.join(TOOL, "light.tsv")
DEFAULT_DARK = os.path.join(TOOL, "dark.tsv")

THEME_DIR = os.path.join(REPO, "Telegram", "Services", "Theme")
DEFAULT_GENERATED = os.path.join(THEME_DIR, "ThemeDefaults.g.cs")
DEFAULT_OVERLAY = os.path.join(THEME_DIR, "ThemeDefaults.cs")


def _say(message=""):
    sys.stdout.write(message + "\n")


def _load(args):
    from themedefaults import table
    return table.load(args.light), table.load(args.dark)


def cmd_pack(args):
    from themedefaults import pack

    light, dark = _load(args)
    text = pack.emit(light, dark)
    name = os.path.basename(args.out)

    current = None
    if os.path.exists(args.out):
        with open(args.out, "r", encoding="utf-8", newline="") as fp:
            current = fp.read()

    if args.check:
        if current == text:
            _say("%s is up to date." % name)
            return 0
        _say("%s %s. Run pack to rewrite it."
             % (name, "does not exist" if current is None else "differs from the tables"))
        return 1

    if current == text and not args.force:
        _say("%s is already up to date." % name)
        return 0

    with open(args.out, "w", encoding="utf-8", newline="") as fp:
        fp.write(text)

    _say("Wrote %s: %d light, %d dark." % (name, len(light), len(dark)))
    return 0


def cmd_verify(args):
    from themedefaults import verify

    light, dark = _load(args)
    rebuilt_light, rebuilt_dark, custom = verify.read(args.generated, args.overlay)

    failures = []
    ok = verify.compare(light, rebuilt_light, custom, "light", failures.append)
    ok = verify.compare(dark, rebuilt_dark, custom, "dark", failures.append) and ok

    for failure in failures[:20]:
        _say("  " + failure)
    if len(failures) > 20:
        _say("  ... and %d more." % (len(failures) - 20))

    if ok:
        _say("%d light and %d dark rows come back identical, in order."
             % (len(light), len(dark)))
        return 0
    return 1


def cmd_export(args):
    from themedefaults import legacy, table

    light, dark = legacy.read(args.source)
    custom = set(args.custom.split(",")) if args.custom else set()

    def mark(rows):
        return [(k, ("custom",) if k in custom else v) for k, v in rows]

    table.save(args.light, mark(light), "Light theme defaults.")
    table.save(args.dark, mark(dark), "Dark theme defaults.")

    _say("Wrote %d light and %d dark rows, %d marked custom."
         % (len(light), len(dark), len(custom)))
    return 0


def cmd_resources(args):
    from themedefaults import resources

    source = args.package
    if source.lower().endswith((".appx", ".msix")):
        source = resources.unpack_package(source, args.out)

    written = resources.extract(source, args.out)
    _say("Extracted %d embedded resources to %s." % (len(written), args.out))

    for path in written:
        if path.lower().endswith("themeresources.xbf"):
            _say("  %-38s %d keys" % (os.path.basename(path), len(resources.keys(path))))
    return 0


def main(argv=None):
    parser = argparse.ArgumentParser(prog="themedefaults", description=__doc__)
    parser.add_argument("--light", default=DEFAULT_LIGHT)
    parser.add_argument("--dark", default=DEFAULT_DARK)
    commands = parser.add_subparsers(dest="command", required=True)

    pack = commands.add_parser("pack", help="tables to ThemeDefaults.g.cs")
    pack.add_argument("--out", default=DEFAULT_GENERATED)
    pack.add_argument("--force", action="store_true", help="rewrite even if it differs")
    pack.add_argument("--check", action="store_true", help="report staleness, write nothing")
    pack.set_defaults(func=cmd_pack)

    verify = commands.add_parser("verify", help="read the generated C# back and diff it")
    verify.add_argument("--generated", default=DEFAULT_GENERATED)
    verify.add_argument("--overlay", default=DEFAULT_OVERLAY)
    verify.set_defaults(func=cmd_verify)

    export = commands.add_parser("export", help="the one-time import of the old dictionaries")
    export.add_argument("source", help="a ThemeService.Defaults.cs in the pre-2026 shape")
    export.add_argument("--custom", default="",
                        help="comma separated keys the overlay owns")
    export.set_defaults(func=cmd_export)

    res = commands.add_parser("resources", help="embedded XBF out of a MUX package")
    res.add_argument("package", help="an .appx, .msix or resources.pri")
    res.add_argument("--out", default=os.path.join(TOOL, "build"))
    res.set_defaults(func=cmd_resources)

    args = parser.parse_args(argv)
    return args.func(args)
