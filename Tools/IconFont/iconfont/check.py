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
