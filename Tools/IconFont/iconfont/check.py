"""Validate the manifest, and cross-check it against the app that uses it.

The app reaches the font three ways: named constants in Icons.cs, "\\uE9F1"
escapes written inline in other C# files, and &#xE9F1;-style literals in XAML.
App.xaml points both TelegramThemeFontFamily and SymbolThemeFontFamily at
Telegram.ttf, so a reference the font has no glyph for renders as nothing rather
than falling back to a system icon font. All three surfaces are checked; missing
any one of them makes glyphs look dead when they are not.
"""

import os
import re

LOCAL_VARIANT = re.compile(r"^local variant of (\w+):(\S+)$")
SHARED_GLYPH = re.compile(r"^same glyph as (\S+)")

from iconfont import sources as sourcelib
from iconfont import svgdoc

CS_CONSTANT = re.compile(r'const\s+string\s+(\w+)\s*=\s*"((?:\\u[0-9A-Fa-f]{4})+)"')
CS_ESCAPE = re.compile(r"\\u([0-9A-Fa-f]{4})")
XAML_LITERAL = re.compile(r"&#x([0-9A-Fa-f]{4});")

PUA = range(0xE000, 0xF900)
SKIP_DIRS = {"bin", "obj", ".git", ".vs", "packages", ".claude"}

# Not every private-use escape in the app belongs to this font. EmojiSkinFlyout
# picks two-person emoji outlines out of the emoji font by codepoint, U+E001
# upwards, which overlaps this font's range by coincidence. Counting those would
# claim 29 glyphs are missing and mark U+E001 as used.
OTHER_FONTS = ("Controls/Drawers/EmojiSkinFlyout.xaml.cs",)


def run(manifest, icons_cs, repo_root):
    problems = list(manifest.validate())
    notes = list(manifest.notes())

    sources = sourcelib.build(manifest)
    for icon in manifest.icons:
        if icon.is_alias:
            continue
        try:
            art = svgdoc.parse(sourcelib.read(icon, sources), name=icon.src)
        except (sourcelib.SourceError, svgdoc.SvgError) as e:
            problems.append("%s: %s" % (icon.name, e))
            continue
        trouble = art.errors
        if icon.blank and trouble == ["no drawable geometry"]:
            trouble = []
        for message in trouble:
            problems.append("%s (%s): %s" % (icon.name, icon.src, message))
        for message in art.warnings:
            notes.append("%s (%s): %s" % (icon.name, icon.src, message))

    problems.extend(stale_notes(manifest))

    codes = {i.code for i in manifest.icons}
    referenced = references(repo_root, icons_cs)

    for code in sorted(set(referenced) - codes):
        where = referenced[code]
        notes.append("U+%04X is referenced by %s%s but the font has no glyph for it"
                     % (code, where[0], "" if len(where) == 1
                        else " and %d other place(s)" % (len(where) - 1)))

    unused = sorted(codes - set(referenced))
    if unused:
        notes.append("%d glyph(s) are in the font but referenced nowhere: %s"
                     % (len(unused), ", ".join("U+%04X" % c for c in unused[:12])
                        + (", ..." if len(unused) > 12 else "")))
    return problems, notes


def stale_notes(manifest):
    """Notes that have stopped being true.

    A note is prose and nothing reads it, so it rots quietly: an entry that gets
    re-pointed at a live source keeps a note calling it a local variant of that
    same source, and an alias keeps naming a glyph that has since been renamed.
    Both mislead the next person more than no note at all.
    """
    by_code = manifest.by_code()
    stale = []
    for icon in manifest.icons:
        if not icon.note:
            continue
        if LOCAL_VARIANT.match(icon.note):
            if icon.is_remote:
                stale.append("%s: note calls it a local variant, but it tracks %s"
                             % (icon.name, icon.src))
            elif icon.is_alias:
                stale.append("%s: note calls it a local variant, but it is an alias"
                             % icon.name)
            continue
        shared = SHARED_GLYPH.match(icon.note)
        if shared:
            if not icon.is_alias:
                stale.append("%s: note says it shares a glyph, but src is %s"
                             % (icon.name, icon.src))
                continue
            target = by_code.get(icon.alias_code)
            if target is None:
                stale.append("%s: aliases U+%04X, which is gone"
                             % (icon.name, icon.alias_code))
            elif target.name != shared.group(1):
                stale.append("%s: note names %s but U+%04X is now %s"
                             % (icon.name, shared.group(1), target.code, target.name))
            continue
        stale.append("%s: note is not in a form this tool recognises (%r)"
                     % (icon.name, icon.note))
    return stale


def references(repo_root, icons_cs):
    """Every codepoint the app names, and where it names it."""
    referenced = {}

    if os.path.exists(icons_cs):
        with open(icons_cs, "r", encoding="utf-8-sig") as fp:
            text = fp.read()
        for name, value in CS_CONSTANT.findall(text):
            for point in CS_ESCAPE.findall(value):
                code = int(point, 16)
                if code in PUA:
                    referenced.setdefault(code, []).append("Icons.%s" % name)

    # Plenty of call sites write the escape inline instead of going through a
    # constant - SessionCell alone has twelve - so every C# file counts, Icons.cs
    # included: 59 of its codepoints live in switch arms like
    # `ChatFolderIcon2.Cat => ("\\uE933", "\\uE931")` rather than in a constant,
    # and scanning it for constants alone reports them as dead.
    for path in _source_files(repo_root, ".cs"):
        with open(path, "r", encoding="utf-8-sig", errors="replace") as fp:
            text = fp.read()
        rel = os.path.relpath(path, repo_root)
        for point in set(CS_ESCAPE.findall(text)):
            code = int(point, 16)
            # A constant already gives a better name for the same reference.
            if code in PUA and not any(w.startswith("Icons.")
                                       for w in referenced.get(code, ())):
                referenced.setdefault(code, []).append(rel)

    for path in _source_files(repo_root, ".xaml"):
        with open(path, "r", encoding="utf-8-sig", errors="replace") as fp:
            text = fp.read()
        rel = os.path.relpath(path, repo_root)
        for point in set(XAML_LITERAL.findall(text)):
            code = int(point, 16)
            if code in PUA:
                referenced.setdefault(code, []).append(rel)

    return referenced


def _source_files(root, suffix):
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d.lower() not in SKIP_DIRS]
        for name in files:
            if not name.endswith(suffix):
                continue
            path = os.path.join(base, name)
            flat = path.replace(os.sep, "/")
            if any(flat.endswith(skip) for skip in OTHER_FONTS):
                continue
            yield path
