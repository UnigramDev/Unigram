"""Reads the generated C# back and checks it against the tables it came from.

The packed arrays are unreadable by design, so nothing about them can be reviewed by
eye. This walks ThemeDefaults.g.cs the way the app does - slot map, packed values, per
theme order, acrylic side tables - and reports any row that does not come back the same.
"""

import re

from themedefaults import table
from themedefaults.legacy import split_args

_COLOR = re.compile(r"^Color\.FromArgb\(0x([0-9A-Fa-f]{2}), 0x([0-9A-Fa-f]{2}), "
                    r"0x([0-9A-Fa-f]{2}), 0x([0-9A-Fa-f]{2})\)$")
_CUSTOM_ROW = re.compile(r"^\((.+)\),$")


def _block(text, declaration):
    """The body of an array initializer, by the declaration that introduces it."""
    start = text.index(declaration)
    start = text.index("{", start)
    return text[start + 1:text.index("};", start)]


def _color(text):
    match = _COLOR.match(text.strip())
    if not match:
        raise ValueError("expected a Color.FromArgb: " + text)
    return "#" + "".join(g.upper() for g in match.groups())


def _acrylics(body, colour):
    rows = []
    for line in body.splitlines():
        line = line.strip()
        if not line.startswith("Acrylic."):
            continue
        args = split_args(line[line.index("(") + 1:line.rindex(")")])
        convert = _color if colour else (lambda x: x.strip()[len("AccentShade."):])
        rows.append((convert(args[0]), convert(args[1]),
                     args[2], args[3] if len(args) == 4 else None))
    return rows


def read(generated, overlay):
    """Rebuilds (light, dark) as ordered [(key, value)] from the two C# files."""
    with open(generated, "r", encoding="utf-8") as fp:
        source = fp.read()
    with open(overlay, "r", encoding="utf-8") as fp:
        custom_source = fp.read()

    keys = re.findall(r'"([^"]*)"', _block(source, "Keys ="))
    values = {name: [int(x, 16) for x in
                     re.findall(r"0x([0-9A-F]{16})", _block(source, "_%sValues =" % name))]
              for name in ("light", "dark")}
    order = {name: [int(x) for x in
                    re.findall(r"(\d+)", _block(source, "_%sOrder =" % name))]
             for name in ("light", "dark")}

    acrylic_colors = _acrylics(_block(source, "AcrylicColors ="), True)
    acrylic_shades = _acrylics(_block(source, "AcrylicShades ="), False)

    # The overlay is applied exactly as the static constructor applies it.
    slot = {k: i for i, k in enumerate(keys)}
    custom = set()
    for line in _block(custom_source, "_custom =").splitlines():
        match = _CUSTOM_ROW.match(line.strip())
        if not match:
            continue
        parts = split_args(match.group(1))
        if len(parts) != 3:
            raise ValueError("overlay row wants key, light, dark: " + line)
        key = parts[0].strip().strip('"')
        custom.add(key)
        for name, text in (("light", parts[1]), ("dark", parts[2])):
            text = text.strip()
            if text.startswith("AccentShade."):
                value = ("shade", text[len("AccentShade."):])
            else:
                value = ("color", _color(text))
            values[name][slot[key]] = table.pack(value, acrylic_colors, acrylic_shades)

    out = {}
    for name in ("light", "dark"):
        rows = []
        for index in order[name]:
            value = table.unpack(values[name][index], acrylic_colors, acrylic_shades)
            if value is None:
                raise ValueError("%s declares %s but has no value for it" % (name, keys[index]))
            rows.append((keys[index], value))
        out[name] = rows
    return out["light"], out["dark"], custom


def compare(expected, actual, custom, name, report):
    """Reports the first thing that differs, or nothing if the two agree."""
    if len(expected) != len(actual):
        report("%s: %d rows expected, %d rebuilt" % (name, len(expected), len(actual)))
        return False

    ok = True
    for index, (want, got) in enumerate(zip(expected, actual)):
        if want[0] != got[0]:
            report("%s row %d: key %s expected, %s rebuilt" % (name, index, want[0], got[0]))
            ok = False
            break
        if want[1][0] == "custom":
            # The overlay owns the value; all this table records is that it leads the slot.
            if want[0] not in custom:
                report("%s: %s is marked custom but the overlay does not supply it" % (name, want[0]))
                ok = False
            continue
        if want[1] != got[1]:
            report("%s: %s is %s, rebuilt as %s"
                   % (name, want[0], table.format_value(want[1]), table.format_value(got[1])))
            ok = False
    return ok
