"""Turns the flat tables into ThemeDefaults.g.cs."""

from themedefaults import table

HEADER = """//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Services.Settings;
using Windows.UI;

namespace Telegram.Services
{
    // Generated from the Microsoft.UI.Xaml 2.8 and Windows.UI.Xaml theme resources, which are
    // both frozen. Regenerating this is a one-off: nothing upstream moves any more.
    //
    // One key table shared by both themes, and one packed value per slot. Everything here is a
    // constant array initializer so the compiler folds it into a single memcpy rather than
    // several thousand stores in a static constructor.
    internal static partial class ThemeDefaults
    {
"""

ACRYLIC_NOTE = """        // 142 acrylic entries across the two themes collapse to these; the packed value holds
        // an index rather than widening every slot for the sake of 3% of them.
"""

FOOTER = """    }
}
"""


def _wrap(items, per_line, indent):
    out = []
    for i in range(0, len(items), per_line):
        out.append(indent + " ".join(x + "," for x in items[i:i + per_line]))
    return "\n".join(out)


def _array(declaration, items, per_line):
    return "        %s =\n        {\n%s\n        };\n\n" % (
        declaration, _wrap(items, per_line, "            "))


def _color(argb):
    return "Color.FromArgb(0x%s, 0x%s, 0x%s, 0x%s)" % (argb[1:3], argb[3:5], argb[5:7], argb[7:9])


def build(light, dark):
    """Slots, packed values and per-theme order, from the two ordered tables.

    The key table is shared, so it runs in light's order with dark-only keys appended.
    The orders are kept per theme because the two disagree for about twenty keys, and
    the theme editor lists them in that order.
    """
    light_keys = [k for k, _ in light]
    known = set(light_keys)
    keys = light_keys + [k for k, _ in dark if k not in known]
    slot = {k: i for i, k in enumerate(keys)}

    acrylic_colors, acrylic_shades = [], []
    values = {}

    # Light first, then dark, both in declaration order: the acrylic side tables are built
    # by first sight, so this walk is what fixes the indices the packed values refer to.
    for name, rows in (("light", light), ("dark", dark)):
        packed = [0] * len(keys)
        for key, value in rows:
            packed[slot[key]] = table.pack(value, acrylic_colors, acrylic_shades)
        values[name] = packed

    order = {"light": [slot[k] for k, _ in light], "dark": [slot[k] for k, _ in dark]}
    return keys, values, order, acrylic_colors, acrylic_shades


def emit(light, dark):
    keys, values, order, acrylic_colors, acrylic_shades = build(light, dark)

    out = [HEADER]
    out.append(_array("internal static readonly string[] Keys",
                      ['"%s"' % k for k in keys], 3))

    for name in ("light", "dark"):
        out.append(_array("private static readonly ulong[] _%sValues" % name,
                          ["0x%016X" % v for v in values[name]], 5))

    for name in ("light", "dark"):
        out.append(_array("private static readonly int[] _%sOrder" % name,
                          [str(v) for v in order[name]], 14))

    out.append(ACRYLIC_NOTE)

    rows = []
    for tint, fallback, opacity, luminosity in acrylic_colors:
        args = "%s, %s, %s" % (_color(tint), _color(fallback), opacity)
        rows.append("Acrylic.Color(%s)" % (args + (", " + luminosity if luminosity else "")))
    out.append(_array("internal static readonly Acrylic<Color>[] AcrylicColors", rows, 1))

    rows = []
    for tint, fallback, opacity, luminosity in acrylic_shades:
        args = "AccentShade.%s, AccentShade.%s, %s" % (tint, fallback, opacity)
        rows.append("Acrylic.Shade(%s)" % (args + (", " + luminosity if luminosity else "")))
    out.append(_array("internal static readonly Acrylic<AccentShade>[] AcrylicShades", rows, 1))

    # One place decides line endings; the repo is CRLF in the working tree.
    text = "".join(out) + FOOTER
    return text.replace("\r\n", "\n").replace("\n", "\r\n")
