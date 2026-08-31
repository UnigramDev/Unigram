# IconFont

Builds `Telegram/Assets/Fonts/Telegram.ttf` from SVG sources, replacing IcoMoon.

Every glyph is described by an entry in `icons.json`. An entry names the glyph,
fixes its codepoint, and says where the artwork comes from: either a file in
`icons/`, or a live source that is fetched and pinned to a version.

```json
{"name": "ic_fluent_arrow_download_20_regular", "code": "EA00", "src": "fluent:arrow_download_20_regular"},
{"name": "tl_fluent_boost_24_filled",           "code": "E9F1", "src": "icons/tl_fluent_boost_24_filled.svg"}
```

## Setup

```
py -m pip install -r requirements.txt
```

Everything is run as `py -m iconfont <command>` from this folder.

## Commands

| Command | What it does |
| --- | --- |
| `build` | Manifest to `Telegram.ttf`. Refuses to build if anything is wrong; `--lax` overrides. |
| `verify --reference <ttf>` | Compares a build against another font glyph by glyph, by what each one paints. |
| `changes --reference <ttf>` | The same comparison as a page, drawing each changed glyph before and after. |
| `check` | Validates the manifest and cross-checks it against `Icons.cs` and the XAML. |
| `sheet` | Writes `contact-sheet.html`, every glyph drawn from its converted outline. |
| `update` | Moves a live source to a newer version, reporting what that redraws. |
| `adopt` | Re-points local icons at a live source where the artwork still matches; `--only` takes named ones regardless. |
| `identify` | Names nameless `uniXXXX` glyphs by matching their shape against a source. `--report` writes a side-by-side page for the ones it cannot settle. |
| `rename` | Applies `identified.txt`, the hand-written list of what each nameless glyph turned out to be. |
| `import --from <dir>` | Brings original artwork in from a folder, where it renders identically. |
| `tidy` | Renames files in `icons/` to match the glyph they hold, and deletes ones nothing points at. |
| `missing` | Codepoints the app names that the font has no glyph for; `--reference <ttf>` shows what each used to draw. |
| `drift` | Local glyphs carrying a Fluent name, next to what upstream draws under that name today. |
| `extract` | The one-time migration out of IcoMoon. Already run and `Telegram.json` is gone; kept to document how the sources got here. |

`adopt`, `identify`, `import`, `rename`, `tidy` and `update` are dry runs unless
given `--apply`.

Every local icon's file is named after the glyph it holds, and `rename` keeps it
that way. `tidy` is the repair for when it drifts anyway.

`identify` exists because IcoMoon kept a name only where somebody typed one. The
rest arrived as `uniE0E2` and could never be matched by name, so they are matched
by what they draw: every icon in the source is rasterised once into a coarse
bitmap packed into an integer, compared by Hamming distance, and the best dozen
candidates re-compared at full resolution. It also finds glyphs the font carries
twice under different codepoints - but only after comparing the two glyphs
directly. Two glyphs matching the same upstream name proves nothing on its own,
because the comparison normalises every icon into the em: for simple shapes like
`add` or `checkmark` two sizes of the same drawing land under a percent apart.

## identified.txt

The record of the by-hand pass over the glyphs IcoMoon left nameless. `rename`
reads it and it is safe to re-run: lines already applied are recognised as such.

```
uniE917 -> attach_20_regular                          rename, and track upstream if the drawing matches
uniE001 -> duplicate of ic_fluent_checkmark_20_regular    make it an alias, if the two really are one drawing
uniE946 -> duplicate of ic_fluent_info_20_regular (confirmed)   alias it even though the ink differs slightly
uniE72E -> tbd   # compare with ic_fluent_lock_closed_20_regular    not settled; pin this pair in the review page
```

`(confirmed)` exists because measurement can only say how far apart two glyphs'
ink is, and a fraction of a percent is below what an eye resolves at icon sizes.
It is per line on purpose: a global tolerance would quietly alias the next thing
that happened to fall under it.

`# compare with` exists because coverage distance does not always put the right
pair together - one closed padlock turned out to be further from the other
closed padlock (15.2%) than from the open one (11.6%).

## Aliases

Some drawings sit at more than one codepoint - the checkmark at four - because
the app reaches them through different names. Those entries say

```json
{"name": "uniE001", "code": "E001", "src": "alias:E8FB", "note": "same glyph as ic_fluent_checkmark_20_regular"}
```

and the build gives both codepoints the same glyph rather than storing the
outline twice. An alias is only ever created after comparing the two glyphs:
matching the same upstream name is not evidence, because the comparison
normalises into the em and two sizes of a simple icon land a fraction of a
percent apart.

Changing the glyph an alias points at changes every codepoint that shares it, and
that can arrive from the direction you are not looking. `ic_fluent_compose_20_regular`
at U+E994 is referenced nowhere, so adopting it looks free - but `Icons.Compose` is
U+E932, which aliases it, and the compose button moved by 9.6%. Before adopting or
re-pointing anything, check whether another codepoint aliases it: `changes` will
show both, and the second one carries the call site.

## Adding an icon

1. Put the SVG in `icons/`, or find its name in the live source.
2. Add an entry to `icons.json` with the next free codepoint.
3. `py -m iconfont build && py -m iconfont check`
4. Add the constant to `Icons.cs` by hand.

**Codepoints are append-only.** Besides `Icons.cs`, 763 raw `&#xE9F1;`-style
literals across 211 XAML files name codepoints directly, and `App.xaml` points
both `TelegramThemeFontFamily` and `SymbolThemeFontFamily` at this font - so a
reshuffled codepoint silently changes icons all over the app, and a missing one
renders as nothing rather than falling back to a system icon font.

## Before shipping a rebuild

`verify` says how much moved; `changes` shows it. Keep a copy of the font you are
replacing - `build` overwrites it in place - and compare against it:

```
git show HEAD:Telegram/Assets/Fonts/Telegram.ttf > before.ttf
py -m iconfont build
py -m iconfont changes --reference before.ttf
```

The page bands the differences, because the decision differs by size: over 15% is
a different drawing, 5-15% is the same icon redrawn, and under 1% is not worth
looking at. Anything below 0.2% is left out entirely - that is smaller than the
cubic-to-quadratic conversion moves an edge, so it says nothing about what
changed.

## Taking a drifted icon from upstream

A local glyph carrying a Fluent name whose drawing no longer matches upstream is
left alone by `adopt` - switching it would change what the app renders. `drift`
lists them with both versions side by side; when you decide one of them should be
Microsoft's again, name it:

```
py -m iconfont drift                                    # then read drift.html
py -m iconfont adopt --only pin_20_regular,U+E77A       # what would change
py -m iconfont adopt --only pin_20_regular,U+E77A --apply
py -m iconfont build
py -m iconfont verify --reference <the previous ttf>
```

`--only` accepts glyph names, with or without the `ic_fluent_` prefix, and
`U+XXXX` codepoints. It ignores `--tolerance`: naming an icon is the decision.
The local file is deleted, so the glyph follows upstream from then on. Adopting
in bulk is the same command with `--tolerance` and no `--only`.

## Updating Microsoft's icons

```
py -m iconfont update              # what would change
py -m iconfont update --apply      # move the pin
py -m iconfont build
py -m iconfont verify --reference <the previous ttf>
```

`update` refuses to move the pin if any tracked icon has been renamed or removed
upstream, because bumping it would drop the glyph and leave the codepoint blank.

Microsoft's icons come from the `@fluentui/svg-icons` npm package rather than the
GitHub repository: the repository has over a hundred thousand entries, so the git
tree API truncates, and an icon's folder name cannot be derived from its own
name. The package is one request, is versioned, and lays every icon out flat.

## What the tool will not build

- **Multicolour artwork.** More than one distinct fill colour, ignoring shades of
  black, cannot become a monochrome glyph. Microsoft's `flag_pride_*` icons are
  the only ones in the package that hit this.
- **Strokes.** A stroke is a paint operation, not an outline. Convert it to a
  filled path in the drawing program first.
- **Gradients and patterns.**

Things it does handle, because real artwork contains them: `evenodd` fills, which
TrueType has no way to express and which are converted by re-winding contours by
nesting depth; the no-op `<g clip-path>` wrappers that SVGO leaves behind, which
a naive importer turns into a solid black square; Illustrator's `<style>` classes,
including the `display:none` alternates it keeps in the file; `<rect>` and friends;
transforms; and a drawing command straight after a `z`.

## Notes on the migration

- The font's metrics are IcoMoon's and must not change: 1024 units per em,
  ascender 960, descender 64. Every glyph in the app is positioned against them.
- IcoMoon wrote a left side bearing of 0 for every glyph regardless of where the
  artwork sat. This tool writes the real one. Rasterisers draw the stored
  outline, so nothing moves.
- `Telegram/Assets/Fonts/Telegram.json`, IcoMoon's project file, has been deleted.
  Everything it held is in `icons/` and `icons.json`, and its history is in git if a
  glyph ever needs tracing back.
