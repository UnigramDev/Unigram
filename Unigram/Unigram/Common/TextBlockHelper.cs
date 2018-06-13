using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Strings;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
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

            var entities = Markdown.Parse(null, ref markdown);
            var text = markdown;
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

            sender.Inlines.Clear();

            if (markdown == null)
            {
                return;
            }

            var entities = markdown.Entities;
            var text = markdown.Text;
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

    }
}
