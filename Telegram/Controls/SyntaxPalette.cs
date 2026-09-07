//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    /// <summary>
    /// The colours a syntax token is drawn in, one table per theme. Shared by the two text
    /// engines: the inline one builds a brush per token type, the direct one colours a range of
    /// its layout, and neither should carry a copy of the table.
    /// </summary>
    public static class SyntaxPalette
    {
        /// <summary>
        /// The colour for a token type, or false when it has none - which is most of the tree,
        /// where a token only groups the ones under it.
        /// </summary>
        public static bool TryGetColor(string type, ElementTheme theme, out Color color)
        {
            return (theme == ElementTheme.Light ? _light : _dark).TryGetValue(type, out color);
        }

        private static readonly Dictionary<string, Color> _light = new()
        {
            { "comment", Colors.SlateGray },
            { "block-comment", Colors.SlateGray },
            { "prolog", Colors.SlateGray },
            { "doctype", Colors.SlateGray },
            { "cdata", Colors.SlateGray },
            { "punctuation", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "property", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "tag", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "boolean", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "number", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "constant", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "symbol", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "deleted", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "selector", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "attr-name", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "string", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "char", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "builtin", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "inserted", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "operator", Color.FromArgb(0xFF, 0x9a, 0x6e, 0x3a) },
            { "entity", Color.FromArgb(0xFF, 0x9a, 0x6e, 0x3a) },
            { "url", Color.FromArgb(0xFF, 0x9a, 0x6e, 0x3a) },
            { "atrule", Color.FromArgb(0xFF, 0x00, 0x77, 0xAA) },
            { "attr-value", Color.FromArgb(0xFF, 0x00, 0x77, 0xAA) },
            { "keyword", Color.FromArgb(0xFF, 0x00, 0x77, 0xAA) },
            { "function", Color.FromArgb(0xFF, 0x00, 0x77, 0xAA) },
            { "class-name", Color.FromArgb(0xFF, 0xDD, 0x4A, 0x68) },
        };

        private static readonly Dictionary<string, Color> _dark = new()
        {
            { "comment", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "block-comment", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "prolog", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "doctype", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "cdata", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "punctuation", Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC) },
            { "property", Color.FromArgb(0xFF, 0xf8, 0xc5, 0x55) },
            { "tag", Color.FromArgb(0xFF, 0xe2, 0x77, 0x7a) },
            { "boolean", Color.FromArgb(0xFF, 0xf0, 0x8d, 0x49) },
            { "number", Color.FromArgb(0xFF, 0xf0, 0x8d, 0x49) },
            { "constant", Color.FromArgb(0xFF, 0xf8, 0xc5, 0x55) },
            { "symbol", Color.FromArgb(0xFF, 0xf8, 0xc5, 0x55) },
            { "deleted", Color.FromArgb(0xFF, 0xe2, 0x77, 0x7a) },
            { "selector", Color.FromArgb(0xFF, 0xcc, 0x99, 0xcd) },
            { "attr-name", Color.FromArgb(0xFF, 0xe2, 0x77, 0x7a) },
            { "string", Color.FromArgb(0xFF, 0x7e, 0xc6, 0x99) },
            { "char", Color.FromArgb(0xFF, 0x7e, 0xc6, 0x99) },
            { "builtin", Color.FromArgb(0xFF, 0xcc, 0x99, 0xcd) },
            { "inserted", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "operator", Color.FromArgb(0xFF, 0x67, 0xcd, 0xcc) },
            { "entity", Color.FromArgb(0xFF, 0x67, 0xcd, 0xcc) },
            { "url", Color.FromArgb(0xFF, 0x67, 0xcd, 0xcc) },
            { "atrule", Color.FromArgb(0xFF, 0xcc, 0x99, 0xcd) },
            { "attr-value", Color.FromArgb(0xFF, 0x7e, 0xc6, 0x99) },
            { "keyword", Color.FromArgb(0xFF, 0xcc, 0x99, 0xcd) },
            { "function", Color.FromArgb(0xFF, 0xf0, 0x8d, 0x49) },
            { "class-name", Color.FromArgb(0xFF, 0xf8, 0xc5, 0x55) },
            // namespace 0xe2, 0x77, 0x7a
            // function-name 6196cc
        };
    }
}
