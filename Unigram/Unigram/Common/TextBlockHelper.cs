using System;
using System.Linq;
using System.Text.RegularExpressions;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public static class TextBlockHelper
    {
        #region Markdown

        public static string GetMarkdown(DependencyObject obj)
        {
            return (string)obj.GetValue(MarkdownProperty);
        }

        public static void SetMarkdown(DependencyObject obj, string value)
        {
            obj.SetValue(MarkdownProperty, value);
        }

        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.RegisterAttached("Markdown", typeof(string), typeof(TextBlockHelper), new PropertyMetadata(null, OnMarkdownChanged));

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as TextBlock;
            var markdown = e.NewValue as string;

            sender.Inlines.Clear();

            if (markdown == null)
            {
                return;
            }

            if (markdown.Contains("</a>"))
            {
                markdown = Regex.Replace(markdown, "<a href=\"(.*?)\">(.*?)<\\/a>", "[$2]($1)");
            }

            var formatted = Client.Execute(new ParseMarkdown(new FormattedText(markdown, new TextEntity[0]))) as FormattedText;
            var text = formatted.Text;
            var entities = formatted.Entities;
            var previous = 0;

            foreach (var entity in entities.OrderBy(x => x.Offset))
            {
                if (entity.Offset > previous)
                {
                    sender.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                if (entity.Type is TextEntityTypeBold)
                {
                    sender.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontWeight = FontWeights.SemiBold });
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    sender.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontStyle = FontStyle.Italic });
                }
                else if (entity.Type is TextEntityTypeTextUrl textUrl)
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.NavigateUri = new Uri(textUrl.Url);
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    sender.Inlines.Add(hyperlink);
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                sender.Inlines.Add(new Run { Text = text.Substring(previous) });
            }

            //var previous = 0;
            //var index = markdown.IndexOf("**");
            //var next = index > -1 ? markdown.IndexOf("**", index + 2) : -1;

            //while (index > -1 && next > -1)
            //{
            //    if (index - previous > 0)
            //    {
            //        sender.Inlines.Add(new Run { Text = markdown.Substring(previous, index - previous) });
            //    }

            //    sender.Inlines.Add(new Run { Text = markdown.Substring(index + 2, next - index - 2), FontWeight = FontWeights.SemiBold });

            //    previous = next + 2;
            //    index = markdown.IndexOf("**", next + 2);
            //    next = index > -1 ? markdown.IndexOf("**", index + 2) : -1;
            //}

            //if (markdown.Length - previous > 0)
            //{
            //    sender.Inlines.Add(new Run { Text = markdown.Substring(previous, markdown.Length - previous) });
            //}
        }

        #endregion

        #region FormattedText

        public static FormattedText GetFormattedText(DependencyObject obj)
        {
            return (FormattedText)obj.GetValue(FormattedTextProperty);
        }

        public static void SetFormattedText(DependencyObject obj, FormattedText value)
        {
            obj.SetValue(FormattedTextProperty, value);
        }

        public static readonly DependencyProperty FormattedTextProperty =
            DependencyProperty.RegisterAttached("FormattedText", typeof(FormattedText), typeof(TextBlockHelper), new PropertyMetadata(null, OnFormattedTextChanged));

        private static void OnFormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as TextBlock;
            var markdown = e.NewValue as FormattedText;

            var span = new Span();
            sender.Inlines.Clear();
            sender.Inlines.Add(span);

            if (markdown == null)
            {
                return;
            }

            var entities = markdown.Entities;
            var text = markdown.Text;

            var runs = TextStyleRun.GetRuns(text, entities);
            var previous = 0;

            foreach (var entity in runs)
            {
                if (entity.Offset > previous)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                if (entity.HasFlag(TextStyle.Monospace))
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else
                {
                    var local = span;

                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        var hyperlink = new Hyperlink { NavigateUri = new Uri(textUrl.Url) };
                        span.Inlines.Add(hyperlink);
                        local = hyperlink;
                    }
                    else if (entity.Type is TextEntityTypeUrl url)
                    {
                        var data = text.Substring(entity.Offset, entity.Length);
                        var hyperlink = new Hyperlink { NavigateUri = new Uri(data) };
                        span.Inlines.Add(hyperlink);
                        local = hyperlink;
                    }

                    var run = new Run { Text = text.Substring(entity.Offset, entity.Length) };

                    if (entity.HasFlag(TextStyle.Bold))
                    {
                        run.FontWeight = FontWeights.SemiBold;
                    }
                    if (entity.HasFlag(TextStyle.Italic))
                    {
                        run.FontStyle |= FontStyle.Italic;
                    }
                    if (entity.HasFlag(TextStyle.Underline))
                    {
                        run.TextDecorations |= TextDecorations.Underline;
                    }
                    if (entity.HasFlag(TextStyle.Strikethrough))
                    {
                        run.TextDecorations |= TextDecorations.Strikethrough;
                    }

                    local.Inlines.Add(run);
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                span.Inlines.Add(new Run { Text = text.Substring(previous) });
            }

            //var previous = 0;
            //var index = markdown.IndexOf("**");
            //var next = index > -1 ? markdown.IndexOf("**", index + 2) : -1;

            //while (index > -1 && next > -1)
            //{
            //    if (index - previous > 0)
            //    {
            //        sender.Inlines.Add(new Run { Text = markdown.Substring(previous, index - previous) });
            //    }

            //    sender.Inlines.Add(new Run { Text = markdown.Substring(index + 2, next - index - 2), FontWeight = FontWeights.SemiBold });

            //    previous = next + 2;
            //    index = markdown.IndexOf("**", next + 2);
            //    next = index > -1 ? markdown.IndexOf("**", index + 2) : -1;
            //}

            //if (markdown.Length - previous > 0)
            //{
            //    sender.Inlines.Add(new Run { Text = markdown.Substring(previous, markdown.Length - previous) });
            //}
        }

        #endregion

        #region Text

        public static string GetText(DependencyObject obj)
        {
            return (string)obj.GetValue(TextProperty);
        }

        public static void SetText(DependencyObject obj, string value)
        {
            obj.SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached("Text", typeof(string), typeof(TextBlockHelper), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as TextBlock;
            var newValue = e.NewValue as string;

            var span = new Span();
            sender.Inlines.Clear();
            sender.Inlines.Add(span);

            var markdown = Client.Execute(new GetTextEntities(newValue)) as TextEntities;
            if (markdown == null)
            {
                return;
            }

            var entities = markdown.Entities;
            var text = newValue;

            var runs = TextStyleRun.GetRuns(text, entities);
            var previous = 0;

            foreach (var entity in runs)
            {
                if (entity.Offset > previous)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                if (entity.HasFlag(TextStyle.Monospace))
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else
                {
                    var local = span;

                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        var hyperlink = new Hyperlink { NavigateUri = new Uri(textUrl.Url) };
                        span.Inlines.Add(hyperlink);
                        local = hyperlink;
                    }
                    else if (entity.Type is TextEntityTypeUrl url)
                    {
                        var data = text.Substring(entity.Offset, entity.Length);
                        var hyperlink = new Hyperlink { NavigateUri = new Uri(data) };
                        span.Inlines.Add(hyperlink);
                        local = hyperlink;
                    }
                    else if (entity.Type is TextEntityTypeMention mention)
                    {
                        var data = text.Substring(entity.Offset + 1, entity.Length - 1);
                        var hyperlink = new Hyperlink { NavigateUri = new Uri("https://t.me/" + data) };
                        span.Inlines.Add(hyperlink);
                        local = hyperlink;
                    }

                    var run = new Run { Text = text.Substring(entity.Offset, entity.Length) };

                    if (entity.HasFlag(TextStyle.Bold))
                    {
                        run.FontWeight = FontWeights.SemiBold;
                    }
                    if (entity.HasFlag(TextStyle.Italic))
                    {
                        run.FontStyle |= FontStyle.Italic;
                    }
                    if (entity.HasFlag(TextStyle.Underline))
                    {
                        run.TextDecorations |= TextDecorations.Underline;
                    }
                    if (entity.HasFlag(TextStyle.Strikethrough))
                    {
                        run.TextDecorations |= TextDecorations.Strikethrough;
                    }

                    local.Inlines.Add(run);
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                span.Inlines.Add(new Run { Text = text.Substring(previous) });
            }

            //var previous = 0;
            //var index = markdown.IndexOf("**");
            //var next = index > -1 ? markdown.IndexOf("**", index + 2) : -1;

            //while (index > -1 && next > -1)
            //{
            //    if (index - previous > 0)
            //    {
            //        sender.Inlines.Add(new Run { Text = markdown.Substring(previous, index - previous) });
            //    }

            //    sender.Inlines.Add(new Run { Text = markdown.Substring(index + 2, next - index - 2), FontWeight = FontWeights.SemiBold });

            //    previous = next + 2;
            //    index = markdown.IndexOf("**", next + 2);
            //    next = index > -1 ? markdown.IndexOf("**", index + 2) : -1;
            //}

            //if (markdown.Length - previous > 0)
            //{
            //    sender.Inlines.Add(new Run { Text = markdown.Substring(previous, markdown.Length - previous) });
            //}
        }

        #endregion

    }
}
