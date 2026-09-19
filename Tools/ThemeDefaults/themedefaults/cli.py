"""Command line for the theme defaults packer."""

import argparse
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOL))

DEFAULT_LIGHT = os.path.join(TOOL, "light.tsv")
DEFAULT_DARK = os.path.join(TOOL, "dark.tsv")
DEFAULT_OVERRIDES = os.path.join(TOOL, "overrides.tsv")

THEME_DIR = os.path.join(REPO, "Telegram", "Services", "Theme")
DEFAULT_GENERATED = os.path.join(THEME_DIR, "ThemeDefaults.g.cs")
DEFAULT_OVERLAY = os.path.join(THEME_DIR, "ThemeDefaults.cs")

SDK_ROOT = r"C:\Program Files (x86)\Windows Kits\10\DesignTime\CommonConfiguration\Neutral\UAP"
DEFAULT_MUXC = r"C:\Source\winui2\dev"


def _say(message=""):
    sys.stdout.write(message + "\n")


def _package_version():
    """The Microsoft.UI.Xaml the app references, which is the only muxc worth reading."""
    csproj = os.path.join(REPO, "Telegram", "Telegram.csproj")
    with open(csproj, "r", encoding="utf-8-sig") as fp:
        match = re.search(r'PackageReference Include="Microsoft\.UI\.Xaml">\s*'
                          r"<Version>([^<]+)</Version>", fp.read())
    return match.group(1) if match else None


def _checkout(muxc, ref, into):
    """Extracts `ref` out of the repository `muxc` lives in, and returns the dev folder.

    The working tree is the wrong thing to read. `winui2/main` is not descended from the
    release tags, so a checkout sitting on main carries changes that never shipped - on
    2024-09-12 it repointed TextControlPlaceholderForegroundFocused at a brush v2.8.7 does
    not use. Reading the tag the csproj names is the only reproducible answer.
    """
    try:
        root = subprocess.check_output(["git", "-C", muxc, "rev-parse", "--show-toplevel"],
                                       stderr=subprocess.DEVNULL).decode().strip()
    except (subprocess.CalledProcessError, OSError):
        return None, "not a git checkout"

    archive = os.path.join(into, "muxc.tar")
    try:
        subprocess.check_call(["git", "-C", root, "archive", "--format=tar", "-o", archive,
                               ref, "dev"], stderr=subprocess.DEVNULL)
    except subprocess.CalledProcessError:
        return None, "%s is not in %s" % (ref, root)

    with tarfile.open(archive) as tar:
        tar.extractall(into)
    return os.path.join(into, "dev"), ref


def _newest_sdk():
    """The design-time generic.xaml of the highest installed SDK.

    It is the OS dictionary as the designer sees it, and the only readable copy of it there
    is - the running system's is not a file anywhere.
    """
    if not os.path.isdir(SDK_ROOT):
        return None
    for version in sorted(os.listdir(SDK_ROOT), reverse=True):
        candidate = os.path.join(SDK_ROOT, version, "Generic", "generic.xaml")
        if os.path.exists(candidate):
            return candidate
    return None


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
    from themedefaults import table, verify

    light, dark = _load(args)
    # The generated file has no edges in it, so the tables are compared as they pack.
    light, dark = table.flatten(light), table.flatten(dark)
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


def cmd_compare(args):
    from themedefaults import compare, harvest, table

    system = args.system or _newest_sdk()
    if not system or not os.path.exists(system):
        _say("No design-time generic.xaml found. Pass --system.")
        return 2
    if not os.path.isdir(args.muxc):
        _say("No Microsoft.UI.Xaml sources at %s. Pass --muxc." % args.muxc)
        return 2

    ref = args.ref
    if ref is None:
        version = _package_version()
        ref = "v" + version if version else None

    scratch, muxc, provenance = None, args.muxc, "working tree"
    if ref:
        scratch = tempfile.mkdtemp(prefix="themedefaults-")
        extracted, provenance = _checkout(args.muxc, ref, scratch)
        if extracted:
            muxc = extracted
        else:
            _say("Reading the working tree: %s." % provenance)
            provenance = "working tree"

    try:
        _say("system %s" % system)
        _say("muxc   %s (%s)" % (args.muxc, provenance))
        if provenance == "working tree":
            _say("       unpinned - whatever this checkout is on, which need not be what ships")
        _say()

        entries = harvest.collect(system, muxc)
        light, dark = _load(args)
        overrides = table.load_overrides(args.overrides)

        if args.write:
            prepared, changes = {}, {}
            for name, rows in (("light", light), ("dark", dark)):
                rows, refreshed = compare.refresh(rows, entries[name])
                # Edges are recorded against the framework's own values, where the rule that
                # an edge may not move a colour holds. Overriding first would leave every
                # dependent disagreeing with its target, and none of them would be linked.
                linked, refused = [], []
                if args.aliases:
                    rows, linked, refused = compare.relink(rows, entries[name])
                prepared[name] = rows
                changes[name] = (refreshed, linked, refused)

            # Then the overrides, which is what makes them propagate: the root takes its new
            # value and `flatten` carries it to everything aliasing it. One theme may be
            # declared to copy the other, so both are refreshed before either is written.
            for name in ("light", "dark"):
                other = table.flatten(prepared["dark" if name == "light" else "light"])
                prepared[name], applied = table.apply(prepared[name], overrides[name], dict(other))
                changes[name] += (applied,)

            for name in ("light", "dark"):
                refreshed, linked, refused, applied = changes[name]
                table.save(getattr(args, name), prepared[name],
                           "%s theme defaults." % name.title())

                _say("%s: %d values from the framework, %d overridden, %d rows kept as edges."
                     % (name, len(refreshed), len(applied), len(linked)))
                for key, was, now in applied:
                    _say("    ours  %-50s %-22s -> %s"
                         % (key, table.format_value(was), table.format_value(now)))
                for key, was, now in refreshed[:args.limit]:
                    _say("          %-50s %-22s -> %s"
                         % (key, table.format_value(was), table.format_value(now)))
                if args.limit and len(refreshed) > args.limit:
                    _say("          ... and %d more." % (len(refreshed) - args.limit))
                if refused:
                    _say("       %d edges not recorded - the table and the target disagree:"
                         % len(refused))
                    for key, parent, mine, theirs in refused[:6]:
                        _say("         %-48s %s, but %s is %s"
                             % (key, table.format_value(mine), parent,
                                table.format_value(theirs)))
                _say()
            return 0

        failures = 0
        for name, rows in (("light", light), ("dark", dark)):
            report = compare.run(rows, entries[name], overrides[name])
            if args.key:
                compare.render_aliases(report, args.key, _say, args.limit)
            else:
                compare.render(name, report, rows, _say, args.limit)
            failures += len(report.differs)
            _say()
            if args.key:
                break
    finally:
        if scratch:
            shutil.rmtree(scratch, ignore_errors=True)

    return 1 if failures else 0


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

    comp = commands.add_parser("compare", help="harvest the framework and diff it against the tables")
    comp.add_argument("--system", default=None, help="a design-time generic.xaml")
    comp.add_argument("--overrides", default=DEFAULT_OVERRIDES,
                      help="the table of deliberate departures from the framework")
    comp.add_argument("--muxc", default=DEFAULT_MUXC, help="a Microsoft.UI.Xaml dev folder")
    comp.add_argument("--ref", default=None,
                      help="the tag to read out of it; defaults to the csproj's package "
                           "version, and \"\" reads the working tree instead")
    comp.add_argument("--key", default=None, help="instead, list what resolves to this key")
    comp.add_argument("--limit", type=int, default=15, help="rows to print per listing")
    comp.add_argument("--write", action="store_true",
                      help="take the framework's values into the tables")
    comp.add_argument("--aliases", action="store_true",
                      help="with --write, keep the framework's edges instead of "
                           "the colour behind them")
    comp.set_defaults(func=cmd_compare)

    res = commands.add_parser("resources", help="embedded XBF out of a MUX package")
    res.add_argument("package", help="an .appx, .msix or resources.pri")
    res.add_argument("--out", default=os.path.join(TOOL, "build"))
    res.set_defaults(func=cmd_resources)

    args = parser.parse_args(argv)
    return args.func(args)
