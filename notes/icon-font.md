# Replacing IcoMoon

`Telegram.ttf` is now built by `Tools/IconFont` from SVG sources and a manifest, instead of by
icomoon.io from `Telegram.json`. The website is no longer in the loop and the repository holds
every outline the font is made of.

## What the font is made of now

663 manifest entries, 638 glyphs, 166 KB - down from 667 glyphs and 184 KB, because 29 codepoints
that carried the same drawing now share one glyph.

| | |
| --- | --- |
| 428 | fetched from `@fluentui/svg-icons`, pinned at 1.1.338 |
| 206 | local artwork in `Tools/IconFont/icons/` |
| 29 | aliases: a codepoint that carries another codepoint's glyph |

Two thirds of the font now follows upstream, so it stops going stale on its own.

Microsoft's icons come from npm rather than the GitHub repository on purpose. The repository has
over a hundred thousand entries so the git tree API truncates, and an icon's folder name cannot be
derived from its own name. The npm package is one request, is versioned, and lays every icon out
flat.

## The swap is verified, not asserted

`iconfont verify` compares two fonts by what each codepoint paints - rasterised coverage, not
outline points, because contour order and winding direction are free choices that nonzero filling
cannot see. `iconfont changes --reference <ttf>` renders the same comparison as a page, each
changed glyph drawn before and after.

Against the IcoMoon font: **530 of 666 codepoints render identically**. The 136 that differ are
almost all deliberate - icons re-pointed at the live source, taking upstream's current drawing in
place of a copy frozen years ago:

| | |
| --- | --- |
| 20 | over 15%: a different drawing, or a name that did not describe the artwork |
| 28 | 5-15%: the same icon visibly redrawn |
| 30 | 1-5%: a corner radius, a stroke weight |
| 58 | under 1%: imperceptible |

Nothing below 0.2% is counted at all; that is smaller than the cubic-to-quadratic conversion moves
an edge, and every glyph went through it.

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

- 8 glyphs were never identified and none is referenced by the app: `uniE600`, `uniE602`,
  `uniE60B`, `uniE60C`, `uniE90A`, `uE6000` (outside the private use area), plus `uniE601` and
  `uniE603`, which are the seen and empty message-state badges and keep their IcoMoon names on
  purpose - `identified.txt` marks those `legacy` rather than `tbd`. Four of the rest are the same
  36x20 message-state family and probably belong with them.
- 46 glyphs are referenced nowhere in the app; 27 of those are local artwork rather than a live
  source, so they are the ones that cost anything to keep.
- 30 local glyphs still carry a Fluent name whose upstream drawing has changed. `iconfont drift`
  puts each next to today's version; `iconfont adopt --only <name>` takes one.
- Generating `Icons.cs` from the manifest, so a codepoint cannot drift between the font and the
  constants.

## Working on it

Everything is `py -m iconfont <command>` from `Tools/IconFont`, and `adopt`, `identify`, `import`,
`rename`, `tidy` and `update` are dry runs until given `--apply`. `README.md` there is the
reference; `identified.txt` records the by-hand identification of the glyphs IcoMoon left nameless,
and re-running `rename` over it is a no-op.
