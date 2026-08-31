//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.Storage.Streams;
using Windows.UI.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Common
{
    public static partial class Extensions
    {
        public static FormattedText AsFormattedText(this string str, bool allocate = true)
        {
            return new FormattedText(str, allocate ? Array.Empty<TextEntity>() : null);
        }

        public static FormattedText AsFormattedText(this string str, TextEntityType type)
        {
            return new FormattedText(str, new[]
            {
                new TextEntity(0, str.Length, type)
            });
        }

        public static int OffsetToIndex(this TextPointer pointer, StyledText text)
        {
            if (pointer.VisualParent is not RichTextBlock textBlock || text == null || textBlock.Blocks.Count != text.Paragraphs.Count)
            {
                return -1;
            }

            var index = 0;

            for (int i = 0; i < textBlock.Blocks.Count; i++)
            {
                var block = textBlock.Blocks[i] as Paragraph;
                var paragraph = text.Paragraphs[i];

                if (pointer.Offset == block.ElementStart.Offset)
                {
                    break;
                }

                // Element start
                index++;

                if (OffsetToIndex(textBlock, block, block.Inlines, pointer, ref index))
                {
                    break;
                }

                if (pointer.Offset < block.ElementEnd.Offset)
                {
                    if (pointer.Offset == block.ContentEnd.Offset)
                    {
                        //if (i == textBlock.Blocks.Count - 1)
                        //{
                        //    // Always close when ending on the last paragraph
                        //    index++;
                        //}
                        //else
                        {
                            index += paragraph.Padding;
                        }
                    }

                    break;
                }

                // Element end
                if (paragraph.Padding == 0)
                {
                    index++;
                }

                //index += paragraph.Padding;
            }

            // Adjust the offset if the selection ends on the text block itself
            if (pointer.Offset == textBlock.ContentEnd.Offset && pointer.Parent is RichTextBlock)
            {
                index++;
            }

            return pointer.Offset - index;
        }

        private static bool OffsetToIndex(RichTextBlock textBlock, TextElement parent, InlineCollection inlines, TextPointer pointer, ref int index)
        {
            if (parent.ContentStart.Offset == pointer.Offset && inlines.Empty())
            {
                index--;
            }

            foreach (var element in inlines)
            {
                if (pointer.Offset == element.ElementStart.Offset)
                {
                    return true;
                }

                // Element start
                index++;

                if (element is Span span && OffsetToIndex(textBlock, span, span.Inlines, pointer, ref index))
                {
                    return true;
                }
                if (element is Run { Text: Icons.ZWNJ or Icons.RTL or Icons.LTR })
                {
                    index++;
                }
                else if (element is InlineUIContainer container && container.Child is CustomEmojiIcon icon)
                {
                    index -= icon.Emoji.Length;
                }

                if (pointer.Offset < element.ElementEnd.Offset)
                {
                    return true;
                }

                // Element end
                index++;
            }

            return false;
        }

        public static int OffsetToIndex(this TextPointer pointer)
        {
            if (pointer.VisualParent is not RichTextBlock textBlock)
            {
                return -1;
            }

            return OffsetToIndex(pointer, textBlock);
        }

        public static int OffsetToIndex(this TextPointer pointer, RichTextBlock textBlock)
        {
            var index = 0;

            for (int i = 0; i < textBlock.Blocks.Count; i++)
            {
                var block = textBlock.Blocks[i] as Paragraph;

                if (pointer.Offset == block.ElementStart.Offset)
                {
                    break;
                }

                // Element start
                index++;

                if (OffsetToIndex(textBlock, block, block.Inlines, pointer, ref index))
                {
                    break;
                }

                if (pointer.Offset < block.ElementEnd.Offset)
                {
                    if (pointer.Offset == block.ContentEnd.Offset)
                    {
                        if (i == textBlock.Blocks.Count - 1)
                        {
                            // Always close when ending on the last paragraph
                            index++;
                        }
                        else
                        {
                            index += 1;//paragraph.Padding;
                        }
                    }

                    break;
                }

                // Element end
                index += 1;//paragraph.Padding;
            }

            // Adjust the offset if the selection ends on the text block itself
            if (pointer.Offset == textBlock.ContentEnd.Offset && pointer.Parent is RichTextBlock)
            {
                index += 2;
            }

            return pointer.Offset - index;
        }

        public static StringBuilder Prepend(this StringBuilder builder, string text, string prefix)
        {
            if (builder.Length > 0)
            {
                builder.Append(prefix);
            }

            return builder.Append(text);
        }

        public static string ToDuration(this TimeSpan duration, bool hours = false)
        {
            if (duration.TotalHours >= 1 || hours)
            {
                return duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return duration.ToString("mm\\:ss");
            }
        }

        public static void Clear(this ITextDocument document)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                document.LoadFromStream(TextSetOptions.None, stream);
            }
        }

        public static FormattedText ReplacePremiumLink(string text, PremiumFeature feature)
        {
            var markdown = ClientEx.ParseMarkdown(text);
            if (markdown.Entities.Count == 1)
            {
                // TODO: premium source
                markdown.Entities[0].Type = new TextEntityTypeTextUrl("tg://premium_offer");
            }

            return markdown;
        }

        public static void Add(this InlineCollection inline, string text)
        {
            inline.Add(new Run
            {
                Text = text
            });
        }

        public static void AddZWNJ(this InlineCollection inline)
        {
            inline.Add(new Run
            {
                Text = Icons.ZWNJ
            });
        }

        public static void Add(this InlineCollection inline, string text, FontWeight fontWeight)
        {
            inline.Add(new Run
            {
                Text = text,
                FontWeight = fontWeight
            });
        }

        public static void Add(this InlineCollection inline, string text, FontStyle fontStyle)
        {
            inline.Add(new Run
            {
                Text = text,
                FontStyle = fontStyle
            });
        }

        public static void Add(this InlineCollection inline, string text, TextDecorations textDecorations)
        {
            inline.Add(new Run
            {
                Text = text,
                TextDecorations = textDecorations
            });
        }

        public static string ReplaceStar(this string str, string value)
        {
            return str.Replace("\u2B50\uFE0F", value + "\u200A");
        }

        public static Regex _pattern = new("[\\-0-9]+", RegexOptions.Compiled);

        public static int ToInt32(this string value)
        {
            if (value == null)
            {
                return 0;
            }

            var val = 0;
            try
            {
                var matcher = _pattern.Match(value);
                if (matcher.Success)
                {
                    var num = matcher.Groups[0].Value;
                    val = int.Parse(num);
                }
            }
            catch (Exception)
            {
                //FileLog.e(e);
            }

            return val;
        }

        public static Dictionary<string, string> ParseQueryString(this string query, char separator = '&')
        {
            var first = query.Split('?');
            if (first.Length > 1)
            {
                query = first[^1];
            }

            var queryDict = new Dictionary<string, string>();
            foreach (var token in query.TrimStart(new char[] { '?' }).Split(new char[] { separator }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = token.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    queryDict[parts[0].Trim()] = WebUtility.UrlDecode(parts[1]).Trim();
                }
                else
                {
                    queryDict[parts[0].Trim()] = "";
                }
            }
            return queryDict;
        }

        public static bool IsValidUrl(this string text)
        {
            // IsValidEntity only accepts an entity spanning the whole string, and a URL never
            // spans whitespace, so this rejects without handing the text over to the parser.
            foreach (var character in text)
            {
                if (char.IsWhiteSpace(character))
                {
                    return false;
                }
            }

            return IsValidEntity<TextEntityTypeUrl>(text);
        }

        public static bool IsValidEmailAddress(this string text)
        {
            return IsValidEntity<TextEntityTypeEmailAddress>(text);
        }

        public static bool IsValidEntity<T>(this string text)
        {
            var entities = ClientEx.GetTextEntities(text);
            return entities.Count == 1 && entities[0].Offset == 0 && entities[0].Length == text.Length && entities[0].Type is T;
        }

        public static string Format(this string input)
        {
            if (input != null)
            {
                return input.Trim().Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
            }

            return string.Empty;
        }

        public static Hyperlink GetHyperlinkFromPoint(this RichTextBlock text, Point point)
        {
            return text.GetPositionFromPoint(point).GetHyperlinkFromPosition();
        }

        // The walk up from an already resolved position, for callers that need the position for
        // something else too: it projects a TextElement per level, so it is worth doing once.
        public static Hyperlink GetHyperlinkFromPosition(this TextPointer position)
        {
            return GetHyperlink(position?.Parent as TextElement);
        }

        private static Hyperlink GetHyperlink(TextElement parent)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is Hyperlink)
            {
                return parent as Hyperlink;
            }

            return GetHyperlink(parent.ElementStart.Parent as TextElement);
        }
    }
}
