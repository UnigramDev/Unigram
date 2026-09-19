"""The framework's own theme dictionaries, read from source.

Two sources, merged in the order the app merges them:

  * the Windows SDK's design-time `generic.xaml` - the OS dictionary. It is frozen at the
    pre-2021 spelling: none of the Fluent tokens are in it, and `AccentButtonForeground`
    still resolves to `SystemControlBackgroundChromeWhiteBrush`.
  * Microsoft.UI.Xaml's per-control `*_themeresources.xaml`, which win over it. The app
    merges `XamlControlsResources` into `Application.Resources`, so muxc beats the OS
    dictionary at runtime too - this is that precedence, not an approximation of it.

The README warns that reading the per-control files does not give what the app resolves,
because the merged dictionary is assembled at build time. That is true of the *set* of files,
which is why the variant rules below are written down; it is not true of the values. Every
one of the 42 keys behind TextOnAccentFillColor{Primary,Secondary}Brush comes out of this
byte-identical to the checked-in tables.

Brushes are kept as the framework writes them: either a value, or an alias to another key.
Flattening is what lost the structure in the first place, so it happens in `resolve`, on
request, not here.
"""

import collections
import glob
import os
import re
import xml.etree.ElementTree as ET

X = "{http://schemas.microsoft.com/winfx/2006/xaml}"

# HighContrast is deliberately absent: the app pins nothing in it, and the system colours it
# maps to have no value until the OS supplies one.
THEMES = {"Default": "dark", "Light": "light"}

# WinUI ships several dictionaries per control and picks between them at runtime, so the
# suffix is a selection rule rather than a naming habit. `_v1` is ControlsResourcesVersion
# .Version1, which the app never asks for. The rest layer over the base file in this order,
# which is why the rank matters and alphabetical order is not enough to trust.
VARIANTS = (
    ("_themeresources.xaml", 0),
    ("_themeresources_v2.5.xaml", 1),
    ("_themeresources_any.xaml", 2),
    ("_themeresources_21h1.xaml", 3),
)

# The accent is not a colour at this layer - it is whichever shade the theme asked for, and
# Theme.Update resolves it against the user's accent at apply time.
SHADES = {
    "SystemAccentColor": "Default",
    "SystemAccentColorLight1": "Light1",
    "SystemAccentColorLight2": "Light2",
    "SystemAccentColorLight3": "Light3",
    "SystemAccentColorDark1": "Dark1",
    "SystemAccentColorDark2": "Dark2",
    "SystemAccentColorDark3": "Dark3",
}

NAMED = {"Transparent": "#00FFFFFF", "White": "#FFFFFFFF", "Black": "#FF000000"}

# Reveal did not survive Fluent and nothing in the app uses it; LinearGradientBrush and the
# rest have no single colour to pin. Recorded as `other` so coverage numbers stay honest.
BRUSHES = ("SolidColorBrush", "AcrylicBrush", "RevealBackgroundBrush", "RevealBorderBrush",
           "LinearGradientBrush", "StaticResource")

_REFERENCE = re.compile(r"^\{\s*(?:Theme|Static)Resource\s+([A-Za-z0-9_]+)\s*\}$")
_HEX = re.compile(r"^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")

Entry = collections.namedtuple("Entry", "kind args brush source")


def _color(text):
    """The spellings the dictionaries use, normalised to #AARRGGBB."""
    text = (text or "").strip()
    if text in NAMED:
        return NAMED[text]
    if not _HEX.match(text):
        return None
    digits = text[1:].upper()
    return "#" + (digits if len(digits) == 8 else "FF" + digits)


def _shaded(argb, opacity):
    """Brush.Opacity folded into alpha.

    A SolidColorBrush over an opaque colour renders the same either way, and the packed table
    has one slot per key with nowhere to put a second number. The app does the same thing.
    """
    if opacity is None:
        return argb
    alpha = int(round(int(argb[1:3], 16) * float(opacity)))
    return "#%02X%s" % (max(0, min(255, alpha)), argb[3:])


def rank(path):
    """The merge rank of one muxc file, or None if the app never loads it."""
    name = os.path.basename(path)
    if name.endswith("_v1.xaml"):
        return None
    for suffix, order in VARIANTS:
        if name.endswith(suffix):
            return order
    return None


def _parents(root):
    parent = {}
    for node in root.iter():
        for child in node:
            parent[child] = node
    return parent


def read(path, into=None):
    """Adds one dictionary's entries to `into`, keyed by theme then resource key."""
    into = into if into is not None else {name: {} for name in THEMES.values()}

    root = ET.parse(path).getroot()
    parent = _parents(root)
    source = os.path.basename(path)

    def theme_of(node):
        while node is not None:
            if node.tag.split("}")[-1] == "ResourceDictionary":
                key = node.get(X + "Key")
                if key in THEMES:
                    return THEMES[key]
                if key == "HighContrast":
                    return None
            node = parent.get(node)
        # Outside ThemeDictionaries entirely: a shared entry both themes see.
        return "both"

    for node in root.iter():
        key = node.get(X + "Key")
        if not key:
            continue

        theme = theme_of(node)
        if theme is None:
            continue

        tag = node.tag.split("}")[-1]
        entry = _entry(tag, node, source)
        if entry is None:
            continue

        for name in (THEMES.values() if theme == "both" else (theme,)):
            into[name][key] = entry

    return into


def _entry(tag, node, source):
    if tag == "Color":
        argb = _color(node.text)
        return Entry("color", (argb,), False, source) if argb else None

    if tag == "StaticResource":
        target = node.get("ResourceKey")
        # A brush key aliasing a Color key is a WinUI bug rather than a kind of its own
        # (RadioButtonCheckGlyphFillDisabled is the one). Resolution reports it.
        return Entry("alias", (target,), True, source) if target else None

    if tag == "SolidColorBrush":
        opacity = node.get("Opacity")
        value = (node.get("Color") or "").strip()

        reference = _REFERENCE.match(value)
        if reference:
            target = reference.group(1)
            if target in SHADES:
                return Entry("shade", (SHADES[target], opacity), True, source)
            # Points at a Color key, so it is an alias that has to resolve before it is
            # worth anything - but it is still one edge, not a value.
            return Entry("alias", (target, opacity), True, source)

        argb = _color(value)
        return Entry("color", (_shaded(argb, opacity),), True, source) if argb else \
            Entry("other", (tag,), True, source)

    if tag == "AcrylicBrush":
        return Entry("acrylic", (node.get("TintColor"), node.get("FallbackColor"),
                                 node.get("TintOpacity"), node.get("TintLuminosityOpacity")),
                     True, source)

    if tag in BRUSHES:
        return Entry("other", (tag,), True, source)

    return None


def collect(system, muxc):
    """The merged dictionaries: the OS file first, then every muxc file the app loads."""
    into = {name: {} for name in THEMES.values()}
    sources = [(-1, system)]

    for path in glob.glob(os.path.join(muxc, "**", "*_themeresources*.xaml"), recursive=True):
        order = rank(path)
        if order is not None:
            sources.append((order, path))

    # Rank first, path second, so a rerun on a different machine merges in the same order.
    for _, path in sorted(sources, key=lambda item: (item[0], item[1].lower())):
        read(path, into)

    return into


def resolve(entries, key, _seen=None):
    """Follows alias edges to the value behind `key`.

    Returns (entry, chain). `chain` is the keys walked, so a caller can say what follows what;
    an unresolvable edge comes back as an `other` entry naming what broke.
    """
    _seen = _seen if _seen is not None else []
    entry = entries.get(key)

    if entry is None:
        return Entry("other", ("dangling:" + key,), True, None), _seen
    if key in _seen:
        return Entry("other", ("cycle:" + key,), True, entry.source), _seen

    _seen = _seen + [key]
    if entry.kind != "alias":
        return entry, _seen

    target, opacity = (entry.args + (None,))[:2]
    resolved, chain = resolve(entries, target, _seen)

    # Opacity on the aliasing brush still applies to whatever it resolved to.
    if opacity is not None and resolved.kind == "color":
        resolved = resolved._replace(args=(_shaded(resolved.args[0], opacity),))
    return resolved, chain


def roots(entries):
    """Every alias grouped under each key it resolves *through*, not only the terminal.

    A brush chains to a brush and that brush to a Color, so grouping by the terminal alone
    would answer for `TextOnAccentFillColorPrimary` and draw a blank for the Brush key
    everything actually names.
    """
    out = collections.defaultdict(list)
    for key, entry in entries.items():
        if entry.kind != "alias":
            continue
        _, chain = resolve(entries, key)
        for ancestor in chain[1:]:
            out[ancestor].append(key)
    return out
