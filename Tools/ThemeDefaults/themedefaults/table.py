"""The flat theme table: one row per key, in the order that theme declares it.

A value is one of

    ("color", "#AARRGGBB")
    ("shade", "Default", 0xE6 or None)
    ("alias", "TextOnAccentFillColorPrimaryBrush")
    ("acrylicColor", "#AARRGGBB", "#AARRGGBB", "0.15", "0.96" or None)
    ("acrylicShade", "Dark1", "Dark1", "0.8", None)
    ("custom",)

An alias is the edge the framework actually writes - `<StaticResource ResourceKey="..."/>` -
kept instead of the colour behind it, so that changing one key moves everything that names it.
`flatten` resolves them, because the packed table is one value per slot.

A shade carries the alpha it is applied at, written `shade:Default@E6`. The framework holds
that as Brush.Opacity, which a shade cannot: it is not a colour until Theme.Update resolves
it against the user's accent, so the transparency has to travel separately. Only shades need
it - a colour already has an alpha byte of its own - and `None` means opaque.

`custom` marks a key whose colours live in the hand-written overlay in ThemeDefaults.cs
rather than here, so Telegram's own values are written down in exactly one place. The
packed arrays leave those slots empty and the overlay fills them at startup.
"""

import os
import re

SHADES = ("Default", "Light1", "Light2", "Light3", "Dark1", "Dark2", "Dark3")

KINDS = {"color": 1, "shade": 2, "acrylicColor": 3, "acrylicShade": 4}

_COLOR = re.compile(r"^#[0-9A-F]{8}$")

_ALPHA = re.compile(r"^[0-9A-F]{2}$")


def parse_value(text):
    text = text.strip()
    if text == "custom":
        return ("custom",)
    if _COLOR.match(text):
        return ("color", text)
    if text.startswith("alias:"):
        target = text[len("alias:"):]
        if not target:
            raise ValueError("an alias wants a key: " + text)
        return ("alias", target)
    if text.startswith("shade:"):
        shade, _, alpha = text[len("shade:"):].partition("@")
        if shade not in SHADES:
            raise ValueError("unknown shade: " + text)
        if alpha and not _ALPHA.match(alpha):
            raise ValueError("a shade alpha is two hex digits: " + text)
        return ("shade", shade, int(alpha, 16) if alpha else None)

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
    if kind == "alias":
        return "alias:" + value[1]
    if kind == "shade":
        return "shade:" + value[1] + ("@%02X" % value[2] if value[2] else "")

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


def load_overrides(path):
    """Reads the override table: key<TAB>light<TAB>dark, in the shape `apply` wants.

    The app's departures from the framework live here rather than in the two tables, so that
    refreshing the tables cannot take them back and so the tables stay a record of what
    upstream says rather than a mix of the two.
    """
    out = {"light": {}, "dark": {}}
    if not os.path.exists(path):
        return out

    with open(path, "r", encoding="utf-8") as fp:
        for number, line in enumerate(fp, 1):
            line = line.rstrip("\n").rstrip("\r")
            if not line.strip() or line.startswith("#"):
                continue
            parts = line.split("\t")
            if len(parts) != 3:
                raise ValueError("%s:%d: expected key<TAB>light<TAB>dark" % (path, number))

            key = parts[0].strip()
            for theme, text in (("light", parts[1]), ("dark", parts[2])):
                text = text.strip()
                if text in ("", "-"):
                    continue
                if text.startswith("like:"):
                    source = text[len("like:"):]
                    if source not in out or source == theme:
                        raise ValueError("%s:%d: like: wants the other theme" % (path, number))
                    out[theme][key] = ("like", source)
                else:
                    out[theme][key] = parse_value(text)
    return out


def apply(rows, overrides, other):
    """`rows` with the declared overrides applied. `other` is the other theme, flattened.

    Runs after the framework's values and before edges are recorded, so an overridden key
    holds its own value and everything aliasing it follows.
    """
    applied, out = [], []
    for key, value in rows:
        want = overrides.get(key)
        if want is None:
            out.append((key, value))
            continue
        if want[0] == "like":
            want = other.get(key)
            if want is None:
                raise ValueError("%s has no value in the theme it copies" % key)
        out.append((key, want))
        if want != value:
            applied.append((key, value, want))
    return out, applied


def save(path, rows, title):
    with open(path, "w", encoding="utf-8", newline="\r\n") as fp:
        fp.write("# %s\n" % title)
        fp.write("# key<TAB>value, in the order the theme declares them. See table.py.\n")
        for key, value in rows:
            fp.write("%s\t%s\n" % (key, format_value(value)))


def flatten(rows):
    """One theme's rows with every alias replaced by the value it points at.

    The packed table is one value per slot, so the edges cannot survive into it - they are
    a property of the tables, not of what the app loads. A target the theme does not define
    is an error rather than a silent hole: it would pack as zero, which reads as a key this
    theme leaves to the other one.
    """
    values = dict(rows)

    def resolve(key, seen):
        value = values.get(key)
        if value is None:
            raise ValueError("alias target is not in this theme: " + key)
        if value[0] == "custom":
            raise ValueError("alias points at an overlay key, which has no value here: " + key)
        if value[0] != "alias":
            return value
        if key in seen:
            raise ValueError("alias cycle through " + key)
        return resolve(value[1], seen | {key})

    return [(key, resolve(value[1], {key}) if value[0] == "alias" else value)
            for key, value in rows]


def pack(value, acrylic_colors, acrylic_shades):
    """The eight bytes ThemeValue holds: kind in the high word, payload in the low one.

    Acrylic payloads are an index into the shared side tables, which this appends to on
    first sight - so the order values are packed in is what fixes those indices.
    """
    kind = value[0]
    if kind == "custom":
        return 0
    if kind == "alias":
        raise ValueError("flatten before packing: alias to " + value[1])
    alpha = 0
    if kind == "color":
        payload = int(value[1][1:], 16)
    elif kind == "shade":
        payload = SHADES.index(value[1])
        alpha = value[2] or 0
    else:
        side = acrylic_colors if kind == "acrylicColor" else acrylic_shades
        record = tuple(value[1:])
        if record not in side:
            side.append(record)
        payload = side.index(record)
    return (alpha << 40) | (KINDS[kind] << 32) | payload


def unpack(packed, acrylic_colors, acrylic_shades):
    # The kind is one byte, not the whole high word: bits 40-47 carry a shade's alpha, and
    # reading them as part of the kind turns every dimmed shade into an acrylic.
    kind = (packed >> 32) & 0xFF
    alpha, payload = (packed >> 40) & 0xFF, packed & 0xFFFFFFFF
    if kind == 0:
        return None
    if kind == 1:
        return ("color", "#%08X" % payload)
    if kind == 2:
        return ("shade", SHADES[payload], alpha or None)
    if kind == 3:
        return ("acrylicColor",) + tuple(acrylic_colors[payload])
    return ("acrylicShade",) + tuple(acrylic_shades[payload])
