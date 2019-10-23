using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Unigram.Common
{

    public class TextStyleRun
    {
        public TextStyle Flags { get; set; }

        public int Start { get; set; }
        public int Offset => Start;

        public int End { get; set; }
        public int Length => End - Start;

        public TextEntity Entity { get; set; }

        public TextStyleRun()
        {

        }

        public TextStyleRun(TextStyleRun run)
        {
            Flags = run.Flags;
            Start = run.Start;
            End = run.End;
            Entity = run.Entity;
        }

        public bool HasFlag(TextStyle flag)
        {
            return Flags.HasFlag(flag);
        }

        private void Merge(TextStyleRun run)
        {
            Flags |= run.Flags;
            if (Entity == null && run.Entity != null)
            {
                Entity = run.Entity;
            }
        }

        private void Replace(TextStyleRun run)
        {
            Flags = run.Flags;
            Entity = run.Entity;
        }

        public static IList<TextStyleRun> GetRuns(FormattedText formatted)
        {
            return GetRuns(formatted.Text, formatted.Entities);
        }

        public static IList<TextStyleRun> GetRuns(string text, IList<TextEntity> entities)
        {
            var runs = new List<TextStyleRun>();
            var entitiesCopy = new List<TextEntity>(entities);

            //Collections.sort(entitiesCopy, (o1, o2)-> {
            //    if (o1.offset > o2.offset)
            //    {
            //        return 1;
            //    }
            //    else if (o1.offset < o2.offset)
            //    {
            //        return -1;
            //    }
            //    return 0;
            //});
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
                else if (entity.Type is TextEntityTypeBlockQuote)
                {
                    newRun.Flags = TextStyle.BlockQuote;
                }
                else if (entity.Type is TextEntityTypeBold)
                {
                    newRun.Flags = TextStyle.Bold;
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    newRun.Flags = TextStyle.Italic;
                }
                else if (entity.Type is TextEntityTypeCode || entity.Type is TextEntityTypePre || entity.Type is TextEntityTypePreCode)
                {
                    newRun.Flags = TextStyle.Monospace;
                }
                else if (entity.Type is TextEntityTypeMentionName)
                {
                    newRun.Flags = TextStyle.Mention;
                    newRun.Entity = entity;
                }
                else
                {
                    newRun.Flags = TextStyle.Url;
                    newRun.Entity = entity;
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
            return runs;
        }
    }

    [Flags]
    public enum TextStyle
    {
        Bold = 1,
        Italic = 2,
        Monospace = 4,
        Strikethrough = 8,
        Underline = 16,
        BlockQuote = 32,
        Mention = 64,
        Url = 128
    }
}
