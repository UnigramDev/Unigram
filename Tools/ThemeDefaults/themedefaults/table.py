"""The flat theme table: one row per key, in the order that theme declares it.

A value is one of

    ("color", "#AARRGGBB")
    ("shade", "Default")
    ("acrylicColor", "#AARRGGBB", "#AARRGGBB", "0.15", "0.96" or None)
    ("acrylicShade", "Dark1", "Dark1", "0.8", None)
    ("custom",)

`custom` marks a key whose colours live in the hand-written overlay in ThemeDefaults.cs
rather than here, so Telegram's own values are written down in exactly one place. The
packed arrays leave those slots empty and the overlay fills them at startup.
"""

import re

SHADES = ("Default", "Light1", "Light2", "Light3", "Dark1", "Dark2", "Dark3")

KINDS = {"color": 1, "shade": 2, "acrylicColor": 3, "acrylicShade": 4}

_COLOR = re.compile(r"^#[0-9A-F]{8}$")


def parse_value(text):
    text = text.strip()
    if text == "custom":
        return ("custom",)
    if _COLOR.match(text):
        return ("color", text)
    if text.startswith("shade:"):
        shade = text[len("shade:"):]
        if shade not in SHADES:
            raise ValueError("unknown shade: " + text)
        return ("shade", shade)

    for prefix, kind in (("acrylic:", "acrylicColor"), ("acrylic-shade:", "acrylicShade")):
        if text.startswith(prefix):
            parts = [p.strip() for p in text[len(prefix):].split(",")]
            if len(parts) not in (3, 4):
                raise ValueError("acrylic wants tint,fallback,opacity[,luminosity]: " + text)
            if len(parts) == 3:
                parts.append(None)
            return (kind,) + tuple(parts)

    raise ValueError("unrecognised value: " + text)


def format_value(value):
    kind = value[0]
    if kind == "custom":
        return "custom"
    if kind == "color":
        return value[1]
    if kind == "shade":
        return "shade:" + value[1]

    prefix = "acrylic:" if kind == "acrylicColor" else "acrylic-shade:"
    return prefix + ",".join(p for p in value[1:] if p is not None)


def load(path):
    """Reads one theme's table. Returns [(key, value)] in file order."""
    rows, seen = [], set()
    with open(path, "r", encoding="utf-8") as fp:
        for number, line in enumerate(fp, 1):
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            key, tab, text = line.partition("\t")
            if not tab:
                raise ValueError("%s:%d: expected key<TAB>value" % (path, number))
            if key in seen:
                raise ValueError("%s:%d: duplicate key %s" % (path, number, key))
            seen.add(key)
            rows.append((key, parse_value(text)))
    return rows


def save(path, rows, title):
    with open(path, "w", encoding="utf-8", newline="\r\n") as fp:
        fp.write("# %s\n" % title)
        fp.write("# key<TAB>value, in the order the theme declares them. See table.py.\n")
        for key, value in rows:
            fp.write("%s\t%s\n" % (key, format_value(value)))


def pack(value, acrylic_colors, acrylic_shades):
    """The eight bytes ThemeValue holds: kind in the high word, payload in the low one.

    Acrylic payloads are an index into the shared side tables, which this appends to on
    first sight - so the order values are packed in is what fixes those indices.
    """
    kind = value[0]
    if kind == "custom":
        return 0
    if kind == "color":
        payload = int(value[1][1:], 16)
    elif kind == "shade":
        payload = SHADES.index(value[1])
    else:
        side = acrylic_colors if kind == "acrylicColor" else acrylic_shades
        record = tuple(value[1:])
        if record not in side:
            side.append(record)
        payload = side.index(record)
    return (KINDS[kind] << 32) | payload


def unpack(packed, acrylic_colors, acrylic_shades):
    kind, payload = packed >> 32, packed & 0xFFFFFFFF
    if kind == 0:
        return None
    if kind == 1:
        return ("color", "#%08X" % payload)
    if kind == 2:
        return ("shade", SHADES[payload])
    if kind == 3:
        return ("acrylicColor",) + tuple(acrylic_colors[payload])
    return ("acrylicShade",) + tuple(acrylic_shades[payload])
