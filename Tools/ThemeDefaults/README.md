# ThemeDefaults

Builds `Telegram/Services/Theme/ThemeDefaults.g.cs` from `light.tsv` and `dark.tsv`.

Those two files are the theme defaults: every resource key the app pins, with the value
it pins for that theme, in the order the theme declares them. One row per key.

```
ApplicationPageBackgroundThemeBrush     #FFFFFFFF
PageHeaderHighlightBrush                shade:Default
ToolTipBackgroundBrush                  acrylic:#FF2C2C2C,#FF2C2C2C,0.15,0.96
MessageReactionBackgroundOutgoing       custom
```

`custom` marks a key whose colours live in the hand-written overlay in `ThemeDefaults.cs`
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

`verify` is the one worth running. The packed arrays cannot be reviewed by eye, so it is
the only thing standing between a bad edit and a theme that silently loses a colour.

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
