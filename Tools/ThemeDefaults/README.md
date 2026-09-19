# ThemeDefaults

Builds `Telegram/Services/Theme/ThemeDefaults.g.cs` from `light.tsv` and `dark.tsv`.

Those two files are the theme defaults: every resource key the app pins, with the value
it pins for that theme, in the order the theme declares them. One row per key.

```
ApplicationPageBackgroundThemeBrush     #FFFFFFFF
PageHeaderHighlightBrush                shade:Default
SystemControlHighlightListAccentHighBrush   shade:Default@E6
CheckBoxCheckGlyphForegroundChecked     alias:TextOnAccentFillColorPrimaryBrush
ToolTipBackgroundBrush                  acrylic:#FF2C2C2C,#FF2C2C2C,0.15,0.96
MessageReactionBackgroundOutgoing       custom
```

`alias:` is the edge the framework writes, kept instead of the colour behind it, so that one
row moves everything that names it - changing `TextOnAccentFillColorPrimaryBrush` in
`dark.tsv` moves 43 rows. About two thirds of each table is edges. `pack` resolves them
(`table.flatten`), so the generated file is unchanged by recording one: it is a change of
representation, and `pack --check` is what proves that. An edge is only recorded where the
target is a row here and already holds the same value; where the two disagree the row keeps
its value and `compare --write --aliases` says so.

`@E6` on a shade is the alpha it is applied at. The framework writes that as `Brush.Opacity`,
which a shade cannot carry: it is not a colour until `Theme.Update` resolves it against the
user's accent, so the transparency has to travel beside it. A colour has an alpha byte
already and never takes the suffix.

## Overrides

`overrides.tsv` is where the app departs from the framework on a framework key — one row,
both themes, a reason above it:

```
# WinUI pairs black-on-accent with SystemAccentColorLight2, a pale tint, and Theme.Update
# remaps that shade to AccentShade.Default.
TextOnAccentFillColorPrimaryBrush       -       like:light
```

`-` leaves a theme as upstream has it; `like:light` takes the other theme's value, which
does not go stale when that theme moves. It exists so the mutation is written down: editing
the value in `dark.tsv` by hand works exactly once, because the next `compare --write` takes
it straight back and the row reads as staleness in the meantime.

Order matters and `cmd_compare` does it in this one: refresh, **then** record edges, **then**
override. Edges are recorded against the framework's own values, where the rule that an edge
may never move a colour holds; overriding first leaves every dependent disagreeing with its
target and nothing gets linked. Overriding last is also what makes an override propagate —
the root takes its new value and `flatten` carries it to the 41 rows aliasing it.

`custom` is the different case: it marks a key whose colours live in the hand-written overlay in `ThemeDefaults.cs`
instead. Those 38 are Telegram's own and are the ones that still change, so they stay in
readable C# and the packed arrays leave their slots empty for the overlay to fill.

Everything else is framework material, and **it is frozen** - Windows.UI.Xaml and
Microsoft.UI.Xaml 2.8 are both done. That is why the output is checked in rather than
generated during the build, and why running this at all is a one-off.

## Why the values are pinned

`Theme.Update` recolours brushes it owns, in place. That is what repaints the whole app on
a theme change without walking the visual tree. A key missing from these tables resolves to
the *framework's* brush instead, which the app cannot mutate, so it keeps its old colour
after a runtime theme switch.

So a row whose value is identical to the framework's own is **not** redundant, and pruning
one is a bug that only shows as a stale colour, only after a theme switch, only if that
control happens to be on screen. Do not prune.

## Setup

Standard library only, no packages. Everything is run as `py -m themedefaults <command>`
from this folder.

## Commands

| Command | What it does |
| --- | --- |
| `pack` | Tables to `ThemeDefaults.g.cs`. `--check` reports staleness without writing, `--force` rewrites regardless. |
| `verify` | Reads the generated C# back the way the app does and diffs every row against the tables, values and order both. |
| `export <source>` | The one-time import out of the old `Dictionary<string, object>` pair. Already run; kept to document how the tables got here. |
| `resources <package>` | Pulls the compiled XBF theme resources out of a shipped Microsoft.UI.Xaml package, and reports the keys in each. |
| `compare` | Reads the framework's own dictionaries and diffs them against the tables. `--key` lists what resolves to one key instead; `--write` takes the framework's values, and `--aliases` with it keeps the framework's edges. Reports only, unless `--write`. |

`verify` is the one worth running. The packed arrays cannot be reviewed by eye, so it is
the only thing standing between a bad edit and a theme that silently loses a colour.

## What `compare` reads

Two sources, merged in the order the app merges them: the Windows SDK's design-time
`generic.xaml` for the OS dictionary, then Microsoft.UI.Xaml's per-control
`*_themeresources.xaml` over it, because `XamlControlsResources` is merged into
`Application.Resources` and wins there for every key it defines.

It reads muxc out of the **tag the csproj names**, not out of the checkout. `winui2/main` is
not descended from the release tags, so a checkout sitting on main carries changes that never
shipped: on 2024-09-12 it repointed `TextControlPlaceholderForegroundFocused` at a brush
`v2.8.7` does not use. `--ref ""` reads the working tree anyway and says so.

The `_v1` dictionaries are `ControlsResourcesVersion.Version1`, which the app never asks
for, and are skipped; `_any`, `_v2.5` and `_21h1` layer over the base file in that order.
That selection is the part worth reviewing, not the parsing - it is why the tables describe
the pre-Win11 CalendarView, which came from a snapshot that excluded `_21h1`.

The section below says reading those files does not give what the app resolves. That is true
of the *set* of files; it is not true of the values. The first run reproduced 1419 of 1593
light rows and 1500 of 1590 dark rows exactly, and derived 45 of the 49 per-theme
`Brush.Opacity` alphas with nothing contradicting. Differences are not failures - the tables
came from a snapshot that predates April 2021, so some of what `compare` reports is the
tables being stale.

## If the app ever moves to WinUI 3

The tables would need rebuilding against the new framework. Three things to know before
starting:

- The merged theme dictionary does not exist as a file in the WinUI source tree. It is
  assembled at build time from the sparse per-control `*_themeresources.xaml`, so reading
  those does not give you what the app actually resolves.
- `C:\Source\microsoft-ui-xaml` is a WinUI **3** clone with the resources under
  `src/controls/dev`. The old external generator (`UnigramUtils\TdGenerateThemeTemplates`)
  points at a `dev` folder that no longer exists, and its `.taml` input is checked in
  nowhere. It cannot be re-run as it stands.
- `resources` gets you the shipped XBF blobs and the keys inside them, which is enough to
  check coverage. It does **not** get you values: those are converted into the XBF node
  stream and reading them needs a real XBF2 decoder.

The cheap way to values is to run the framework rather than parse it - a throwaway app
referencing the target framework, walking `Application.Current.Resources.ThemeDictionaries`
and dumping what it resolves. That flattens the `{ThemeResource}` alias chains for you and
covers the OS resources too, which no source file states outright.

Whatever produces them, the result is two `.tsv` files and `pack` does the rest.
