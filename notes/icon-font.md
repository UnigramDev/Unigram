# Replacing IcoMoon

`Telegram.ttf` is now built by `Tools/IconFont` from SVG sources and a manifest, instead of by
icomoon.io from `Telegram.json`. The website is no longer in the loop and the repository holds
every outline the font is made of.

## What the font is made of now

663 manifest entries, 640 glyphs, 167 KB - down from 667 glyphs and 184 KB, because 27 codepoints
that carried the same drawing now share one glyph.

| | |
| --- | --- |
| 356 | fetched from `@fluentui/svg-icons`, pinned at 1.1.338 |
| 280 | local artwork in `Tools/IconFont/icons/` |
| 27 | aliases: a codepoint that carries another codepoint's glyph |

Microsoft's icons come from npm rather than the GitHub repository on purpose. The repository has
over a hundred thousand entries so the git tree API truncates, and an icon's folder name cannot be
derived from its own name. The npm package is one request, is versioned, and lays every icon out
flat.

## The swap is verified, not asserted

`iconfont verify` compares two fonts by what each codepoint paints - rasterised coverage, not
outline points, because contour order and winding direction are free choices that nonzero filling
cannot see.

Against the IcoMoon font: **605 of 666 codepoints render identically**. Of the 61 that differ, 57
are at or under 1% of the em - cubic-to-quadratic conversion, plus 47 icons deliberately adopted
onto the live source where the drawing had drifted imperceptibly. Four are real changes, all
requested:

| | |
| --- | --- |
| U+E72E 15.2% | `Icons.LockClosed` - was an older drawing of the same icon |
| U+E8CB 12.1% | the sort icon in `PlaybackHeader` and `ContactsSortedByHeader` |
| U+EA1A 1.5% | shield/task, now sharing U+E9F9 |
| U+E9AB 1.1% | `payment_16_regular`, now tracking upstream |

## Things that must not change

- **Metrics.** 1024 units per em, ascender 960, descender 64. Every glyph in the app is positioned
  against them.
- **Codepoints are append-only.** Besides `Icons.cs`, the app names codepoints directly in ~760
  `&#xE9F1;` literals across 211 XAML files, and `App.xaml` points both `TelegramThemeFontFamily`
  and `SymbolThemeFontFamily` at this font. A reshuffled codepoint changes icons all over the app;
  a missing one renders as nothing rather than falling back to a system icon font.
- **The left side bearing is the real xMin.** IcoMoon wrote 0 for all 663 glyphs. fontTools
  translates every outline so xMin equals the lsb it is given, so passing that through shoves the
  whole font against the left edge of the em.

## Found on the way

- `SharedLinkCell.xaml.cs:250` draws `""` for the Instant View bolt, but that glyph is at
  **U+E60E** - the two places that render it correctly (`TLNavigationService.cs:164`,
  `WebPageContent.xaml.cs:670`) use E60E. IcoMoon had named the glyph `uniE611` while assigning it
  E60E, and someone read the name. It has always rendered blank. One-character fix, not made here.
- Seven more codepoints are referenced with no glyph behind them, all leftovers from before
  `SymbolThemeFontFamily` was overridden: U+E052, U+E1CD (`Icons.Loading`), U+E73C, U+E8B1
  (shuffle), U+EE35, U+F13D and U+F13E (the poll ticks). `iconfont missing --reference <segoe.ttf>
  --suggest` shows what each used to draw and offers replacements from the MIT package.
- 46 glyphs are referenced nowhere in the app; 27 of those are local artwork.
- 101 local glyphs carry a Fluent name whose upstream drawing has since changed, 34 of them
  visibly. `iconfont drift` puts each next to today's upstream version.

## Still open

- 9 glyphs were never identified, 7 of them referenced nowhere: `uniE600`, `uniE601`, `uniE602`,
  `uniE603` (paints nothing), `uniE60B`, `uniE60C`, `uniE90A`, `uE6000` (outside the private use
  area), and `uniE915` (`Icons.SmallVideoFilled` - the 20px video drawn at 93.7% scale, so it needs
  a `tl_` name rather than a Fluent one).
- `ic_fluent_alert_24_regular1` is 0.2% from `ic_fluent_alert_24_regular`; it keeps its own glyph
  until somebody confirms the two look the same.
- `Telegram/Assets/Fonts/Telegram.json` is IcoMoon's project file. Nothing reads it any more; it is
  kept only as the record the extraction came from.
- Generating `Icons.cs` from the manifest, so a codepoint cannot drift between the font and the
  constants.

## Working on it

Everything is `py -m iconfont <command>` from `Tools/IconFont`, and `adopt`, `identify`, `import`,
`rename`, `tidy` and `update` are dry runs until given `--apply`. `README.md` there is the
reference; `identified.txt` records the by-hand identification of the glyphs IcoMoon left nameless,
and re-running `rename` over it is a no-op.
