//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;

namespace Telegram.Common
{
    public class TextStyleRun
    {
        public TextStyle Flags { get; set; }

        public int Start { get; set; }
        public int Offset => Start;

        public int End { get; set; }
        public int Length => End - Start;

        public TextEntityType Type { get; set; }

        public TextStyleRun()
        {

        }

        private TextStyleRun(TextStyleRun run)
        {
            Flags = run.Flags;
            Start = run.Start;
            End = run.End;
            Type = run.Type;
        }

        public bool HasFlag(TextStyle flag)
        {
            return Flags.HasFlag(flag);
        }

        private void Merge(TextStyleRun run)
        {
            Flags |= run.Flags;
            Type ??= run.Type;
        }

        public static IList<TextStyleRun> GetRuns(FormattedText formatted)
        {
            return GetRuns(formatted.Text, formatted.Entities);
        }

        public static IList<TextStyleRun> GetRuns(string text, IList<TextEntity> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                return Array.Empty<TextStyleRun>();
            }

            var runs = new List<TextStyleRun>();
            var entitiesCopy = new List<TextEntity>(entities);

            entitiesCopy.Sort((x, y) =>
            {
                if (x.Offset > y.Offset)
                {
                    return 1;
                }
                else if (y.Offset > x.Offset)
                {
                    return -1;
                }

                return 0;
            });

            for (int a = 0, N = entitiesCopy.Count; a < N; a++)
            {
                var entity = entitiesCopy[a];
                if (entity.Length <= 0 || entity.Offset < 0 || entity.Offset >= text.Length)
                {
                    continue;
                }
                else if (entity.Offset + entity.Length > text.Length)
                {
                    entity.Length = text.Length - entity.Offset;
                }

                var newRun = new TextStyleRun();
                newRun.Start = entity.Offset;
                newRun.End = newRun.Start + entity.Length;

                if (entity.Type is TextEntityTypeStrikethrough)
                {
                    newRun.Flags = TextStyle.Strikethrough;
                }
                else if (entity.Type is TextEntityTypeUnderline)
                {
                    newRun.Flags = TextStyle.Underline;
                }
                else if (entity.Type is TextEntityTypeSpoiler)
                {
                    newRun.Flags = TextStyle.Spoiler;
                    newRun.Type = entity.Type;
                }
                else if (entity.Type is TextEntityTypeBold)
                {
                    newRun.Flags = TextStyle.Bold;
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    newRun.Flags = TextStyle.Italic;
                }
                else if (entity.Type is TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode)
                {
                    newRun.Flags = TextStyle.Monospace;
                    newRun.Type = entity.Type;
                }
                else if (entity.Type is TextEntityTypeMentionName)
                {
                    newRun.Flags = TextStyle.Mention;
                    newRun.Type = entity.Type;
                }
                else if (entity.Type is TextEntityTypeCustomEmoji)
                {
                    newRun.Flags = TextStyle.Emoji;
                    newRun.Type = entity.Type;
                }
                else
                {
                    newRun.Flags = TextStyle.Url;
                    newRun.Type = entity.Type;
                }

                for (int b = 0, N2 = runs.Count; b < N2; b++)
                {
                    TextStyleRun run = runs[b];

                    if (newRun.Start > run.Start)
                    {
                        if (newRun.Start >= run.End)
                        {
                            continue;
                        }

                        if (newRun.End < run.End)
                        {
                            TextStyleRun r = new TextStyleRun(newRun);
                            r.Merge(run);
                            b++;
                            N2++;
                            runs.Insert(b, r);

                            r = new TextStyleRun(run);
                            r.Start = newRun.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);
                        }
                        else if (newRun.End >= run.End)
                        {
                            TextStyleRun r = new TextStyleRun(newRun);
                            r.Merge(run);
                            r.End = run.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);
                        }

                        int temp = newRun.Start;
                        newRun.Start = run.End;
                        run.End = temp;
                    }
                    else
                    {
                        if (run.Start >= newRun.End)
                        {
                            continue;
                        }
                        int temp = run.Start;
                        if (newRun.End == run.End)
                        {
                            run.Merge(newRun);
                        }
                        else if (newRun.End < run.End)
                        {
                            TextStyleRun r = new TextStyleRun(run);
                            r.Merge(newRun);
                            r.End = newRun.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);

                            run.Start = newRun.End;
                        }
                        else
                        {
                            TextStyleRun r = new TextStyleRun(newRun);
                            r.Start = run.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);

                            run.Merge(newRun);
                        }
                        newRun.End = temp;
                    }
                }
                if (newRun.Start < newRun.End)
                {
                    runs.Add(newRun);
                }
            }

            runs.Sort((x, y) =>
            {
                if (x.Offset > y.Offset)
                {
                    return 1;
                }
                else if (y.Offset > x.Offset)
                {
                    return -1;
                }

                return 0;
            });

            return runs;
        }

        public static IList<TextEntity> GetEntities(string text, IList<TextStyleRun> runs)
        {
            if (runs == null)
            {
                return Array.Empty<TextEntity>();
            }

            var results = new List<TextEntity>();

            foreach (var run in runs)
            {
                if (run.HasFlag(TextStyle.Emoji))
                {
                    Create(run.Offset, run.Length, results, run.Type);
                }
                else if (run.HasFlag(TextStyle.Monospace))
                {
                    var part = text.Substring(run.Offset, run.Length);
                    if (part.Contains('\v') || part.Contains('\r'))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypePre());
                    }
                    else
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeCode());
                    }
                }
                else
                {
                    if (run.HasFlag(TextStyle.Bold))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeBold());
                    }
                    if (run.HasFlag(TextStyle.Italic))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeItalic());
                    }
                    if (run.HasFlag(TextStyle.Strikethrough))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeStrikethrough());
                    }
                    if (run.HasFlag(TextStyle.Underline))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeUnderline());
                    }
                    if (run.HasFlag(TextStyle.Spoiler))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeSpoiler());
                    }

                    if (run.Type != null)
                    {
                        CreateOrMerge(run.Offset, run.Length, results, run.Type);
                    }
                }
            }

            return results;
        }

        private static void Create(int offset, int length, IList<TextEntity> entities, TextEntityType type)
        {
            entities.Add(new TextEntity(offset, length, type));
        }

        private static void CreateOrMerge(int offset, int length, IList<TextEntity> entities, TextEntityType type)
        {
            var last = entities.LastOrDefault(x => x.Length + x.Offset == offset && AreTheSame(x.Type, type));
            if (last != null)
            {
                last.Length += length;
            }
            else
            {
                entities.Add(new TextEntity(offset, length, type));
            }
        }

        private static bool AreTheSame(TextEntityType x, TextEntityType y)
        {
            if (x is TextEntityTypeTextUrl xTextUrl && y is TextEntityTypeTextUrl yTextUrl)
            {
                return string.Equals(xTextUrl.Url, yTextUrl.Url, StringComparison.OrdinalIgnoreCase);
            }
            else if (x is TextEntityTypeMentionName xMentionName && y is TextEntityTypeMentionName yMentionName)
            {
                return Equals(xMentionName.UserId, yMentionName.UserId);
            }

            return x.GetType() == y.GetType();
        }

        #region Paragraphs

        public static StyledText GetText(FormattedText text)
        {
            return new StyledText
            {
                Text = text.Text,
                Paragraphs = GetParagraphs(text.Text, text.Entities)
            };
        }

        public static StyledText GetText(string text, IList<TextEntity> entities)
        {
            return new StyledText
            {
                Text = text,
                Paragraphs = GetParagraphs(text, entities ?? Array.Empty<TextEntity>())
            };
        }

        private static IList<StyledParagraph> GetParagraphs(string text, IList<TextEntity> entities)
        {
            List<int> indexes = null;
            List<int> pre = null;

            var index = text.IndexOf('\n');

            while (index != -1)
            {
                indexes ??= new();
                indexes.Add(index);

                index = text.IndexOf('\n', index + 1);
            }

            foreach (var entity in entities)
            {
                if (entity.Type is TextEntityTypePre or TextEntityTypePreCode)
                {
                    indexes?.RemoveAll(x => x >= entity.Offset && x <= entity.Offset + entity.Length);

                    pre ??= new();
                    pre.Add(entity.Offset);
                    pre.Add(entity.Offset + entity.Length);
                }
            }

            if (indexes != null && pre != null)
            {
                indexes.AddRange(pre);
                indexes.Sort();
            }
            else indexes ??= pre;

            if (indexes != null)
            {
                var prev = 0;
                var list = new List<StyledParagraph>();

                for (int i = 0; i < indexes.Count; i++)
                {
                    var length = indexes[i] - prev;
                    var regular = pre == null || !pre.Contains(indexes[i]);

                    if (length > 0 || regular)
                    {
                        list.Add(Split(text, entities, prev, length));
                    }

                    prev = indexes[i] + (regular ? 1 : 0);
                }

                if (text.Length > prev)
                {
                    list.Add(Split(text, entities, prev, text.Length - prev));
                }

                return list;
            }

            return new[]
            {
                Split(text, entities, 0, text.Length)
            };
        }

        private static StyledParagraph Split(string text, IList<TextEntity> entities, long startIndex, long length)
        {
            if (length == 0)
            {
                return new StyledParagraph
                {
                    Offset = (int)startIndex,
                    Length = (int)length,
                    Runs = Array.Empty<TextStyleRun>()
                };
            }

            var message = text.Substring((int)startIndex, Math.Min(text.Length - (int)startIndex, (int)length));
            IList<TextEntity> sub = null;

            foreach (var entity in entities)
            {
                // Included, Included
                if (entity.Offset > startIndex && entity.Offset + entity.Length <= startIndex + length)
                {
                    var replace = new TextEntity { Offset = entity.Offset - (int)startIndex, Length = entity.Length, Type = entity.Type };
                    sub ??= new List<TextEntity>();
                    sub.Add(replace);
                }
                // Before, Included
                else if (entity.Offset <= startIndex && entity.Offset + entity.Length > startIndex && entity.Offset + entity.Length < startIndex + length)
                {
                    var replace = new TextEntity { Offset = 0, Length = entity.Length - ((int)startIndex - entity.Offset), Type = entity.Type };
                    sub ??= new List<TextEntity>();
                    sub.Add(replace);
                }
                // Included, After
                else if (entity.Offset > startIndex && entity.Offset < startIndex + length && entity.Offset + entity.Length > startIndex + length)
                {
                    var replace = new TextEntity { Offset = entity.Offset - (int)startIndex, Length = ((int)startIndex + (int)length) + entity.Offset, Type = entity.Type };
                    sub ??= new List<TextEntity>();
                    sub.Add(replace);
                }
                // Before, After
                else if (entity.Offset <= startIndex && entity.Offset + entity.Length >= startIndex + length)
                {
                    var replace = new TextEntity { Offset = 0, Length = message.Length, Type = entity.Type };
                    sub ??= new List<TextEntity>();
                    sub.Add(replace);
                }
            }

            return new StyledParagraph
            {
                Offset = (int)startIndex,
                Length = (int)length,
                Runs = GetRuns(message, sub)
            };
        }

        #endregion
    }

    [Flags]
    public enum TextStyle
    {
        Bold = 1,
        Italic = 2,
        Monospace = 4,
        Strikethrough = 8,
        Underline = 16,
        Spoiler = 32,
        Mention = 64,
        Url = 128,
        Emoji = 256
    }

    public class StyledText
    {
        public string Text { get; set; }

        public IList<StyledParagraph> Paragraphs { get; set; }
    }

    public class StyledParagraph
    {
        public int Offset { get; set; }

        public int Length { get; set; }

        public IList<TextStyleRun> Runs { get; set; }
    }
}
