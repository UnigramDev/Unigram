//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.Controls.RichEditor
{
    public enum RichLimitError
    {
        None,
        Length,
        Blocks,
        Depth,
        Media,
        TableColumns,
    }

    /// <summary>The server's limits for a rich message; zero means no limit is known.</summary>
    public readonly struct RichLimits
    {
        public RichLimits(long textLength, long blocks, long depth, long media, long tableColumns)
        {
            TextLength = textLength;
            Blocks = blocks;
            Depth = depth;
            Media = media;
            TableColumns = tableColumns;
        }

        public long TextLength { get; }

        public long Blocks { get; }

        public long Depth { get; }

        public long Media { get; }

        public long TableColumns { get; }
    }

    /// <summary>
    /// What the server will refuse, measured before sending rather than learned from the error:
    /// the counts are the ones <c>inputRichMessage</c> documents. Text is all of it, custom
    /// emoji counted by their alternative text and formulas by their source; blocks are blocks,
    /// list items and table rows; depth is nested blocks and nested rich text together.
    /// </summary>
    public static class RichDocumentLimits
    {
        public static RichLimitError Validate(RichDocument document, RichLimits limits, out long actual, out long limit)
        {
            actual = 0;
            limit = 0;

            Measure(document, out long length, out long blocks, out long depth, out long media, out long columns);

            if (Exceeds(length, limits.TextLength, ref actual, ref limit))
            {
                return RichLimitError.Length;
            }

            if (Exceeds(blocks, limits.Blocks, ref actual, ref limit))
            {
                return RichLimitError.Blocks;
            }

            if (Exceeds(depth, limits.Depth, ref actual, ref limit))
            {
                return RichLimitError.Depth;
            }

            if (Exceeds(media, limits.Media, ref actual, ref limit))
            {
                return RichLimitError.Media;
            }

            if (Exceeds(columns, limits.TableColumns, ref actual, ref limit))
            {
                return RichLimitError.TableColumns;
            }

            return RichLimitError.None;
        }

        private static bool Exceeds(long value, long max, ref long actual, ref long limit)
        {
            if (max > 0 && value > max)
            {
                actual = value;
                limit = max;
                return true;
            }

            return false;
        }

        public static void Measure(RichDocument document, out long length, out long blocks, out long depth, out long media, out long columns)
        {
            length = 0;
            blocks = 0;
            depth = 0;
            media = 0;
            columns = 0;

            foreach (var leaf in document.Leaves)
            {
                length += leaf.ToPlainText().Length;

                var leafDepth = BlockDepth(leaf.Owner) + RichTextDepth(RichDocumentConverter.ToRichText(leaf));
                depth = Math.Max(depth, leafDepth);
            }

            foreach (var block in document.AllBlocks)
            {
                blocks++;

                switch (block)
                {
                    case RichListBlock list:
                        blocks += list.Items.Count;
                        break;
                    case RichTableBlock table:
                        blocks += table.Rows.Count;
                        columns = Math.Max(columns, RichTableGrid.From(table).Columns);
                        break;
                    case RichMediaBlock:
                        media++;
                        break;
                    case RichMathBlock math:
                        length += math.Expression.Length;
                        break;
                    case RichAnchorBlock anchor:
                        length += anchor.Name.Length;
                        break;
                    case RichButtonRowBlock row:
                        foreach (var button in row.Buttons)
                        {
                            length += RichDocumentConverter.GetPlainText(button.Text).Length;
                        }
                        break;
                }

                depth = Math.Max(depth, BlockDepth(block));
            }
        }

        /// <summary>How deep a block sits: one per container, two for a list - the list and its item.</summary>
        private static long BlockDepth(RichBlock block)
        {
            long depth = 0;

            for (var current = block; current != null; current = current.Parent)
            {
                depth += current is RichListBlock ? 2 : 1;
            }

            // The block itself was counted above; a list counts its item for what sits in it,
            // not for the list block standing alone.
            if (block is RichListBlock)
            {
                depth--;
            }

            return depth;
        }

        private static long RichTextDepth(RichText text)
        {
            switch (text)
            {
                case null:
                case RichTextPlain:
                    return 0;
                case RichTexts texts:
                    {
                        long max = 0;
                        foreach (var child in texts.Texts)
                        {
                            max = Math.Max(max, RichTextDepth(child));
                        }

                        return max;
                    }
                default:
                    {
                        var child = Child(text);
                        return child == null ? 1 : 1 + RichTextDepth(child);
                    }
            }
        }

        private static RichText Child(RichText text) => text switch
        {
            RichTextBold x => x.Text,
            RichTextItalic x => x.Text,
            RichTextUnderline x => x.Text,
            RichTextStrikethrough x => x.Text,
            RichTextSpoiler x => x.Text,
            RichTextFixed x => x.Text,
            RichTextMarked x => x.Text,
            RichTextSubscript x => x.Text,
            RichTextSuperscript x => x.Text,
            RichTextUrl x => x.Text,
            RichTextEmailAddress x => x.Text,
            RichTextPhoneNumber x => x.Text,
            RichTextMentionName x => x.Text,
            RichTextDateTime x => x.Text,
            _ => null
        };
    }
}
