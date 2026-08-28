"""Reads the pre-2026 form: two Dictionary<string, object> collection initializers.

Kept for the same reason IconFont keeps `extract` - it is how the tables got here, and
it is the only reader for the shape the file had before it was packed. Point it at the
old Telegram/Services/ThemeService.Defaults.cs from git history.
"""

import re

ENTRY = re.compile(r'^\s*\{\s*"([^"]+)",\s*(.*?)\s*\},?\s*$')
COLOR = re.compile(r"^Color\.FromArgb\(0x([0-9A-Fa-f]{2}), 0x([0-9A-Fa-f]{2}), "
                   r"0x([0-9A-Fa-f]{2}), 0x([0-9A-Fa-f]{2})\)$")
ACRYLIC = re.compile(r"^Acrylic\.(Color|Shade)\((.*)\)$")


def split_args(text):
    """Splits a C# argument list on top-level commas."""
    out, depth, current = [], 0, ""
    for ch in text:
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        if ch == "," and depth == 0:
            out.append(current.strip())
            current = ""
        else:
            current += ch
    if current.strip():
        out.append(current.strip())
    return out


def _color(text):
    match = COLOR.match(text)
    assert match, text
    return "#" + "".join(g.upper() for g in match.groups())


def _value(text):
    if COLOR.match(text):
        return ("color", _color(text))
    if text.startswith("AccentShade."):
        return ("shade", text[len("AccentShade."):])

    match = ACRYLIC.match(text)
    if match:
        args = split_args(match.group(2))
        assert len(args) in (3, 4), text
        if match.group(1) == "Color":
            convert = _color
        else:
            convert = lambda x: x[len("AccentShade."):]
        return ("acrylic" + match.group(1), convert(args[0]), convert(args[1]),
                args[2], args[3] if len(args) == 4 else None)

    raise ValueError("unrecognised value: " + text)


def read(path):
    """Returns (light, dark), each [(key, value)] in declaration order."""
    with open(path, "r", encoding="utf-8-sig", newline="") as fp:
        lines = fp.read().splitlines()

    starts = [i for i, line in enumerate(lines)
              if "_defaultLight" in line or "_defaultDark" in line]
    if len(starts) != 2:
        raise ValueError("expected _defaultLight then _defaultDark in " + path)

    def parse(lo, hi):
        rows = []
        for line in lines[lo:hi]:
            match = ENTRY.match(line)
            if match:
                rows.append((match.group(1), _value(match.group(2))))
        return rows

    return parse(starts[0], starts[1]), parse(starts[1], len(lines))
