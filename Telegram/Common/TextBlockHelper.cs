//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Text.RegularExpressions;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
{
    public static class TextBlockHelper
    {
        #region IsLink

        public static bool GetIsLink(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsLinkProperty);
        }

        public static void SetIsLink(DependencyObject obj, bool value)
        {
            obj.SetValue(IsLinkProperty, value);
        }

        public static readonly DependencyProperty IsLinkProperty =
            DependencyProperty.RegisterAttached("IsLink", typeof(bool), typeof(TextBlockHelper), new PropertyMetadata(false));

        #endregion

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

            var entities = ClientEx.GetTextEntities(markdown);
            var handleLinks = GetIsLink(d);

            if (handleLinks is false)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    if (entities[i].Type is TextEntityTypeUrl)
                    {
                        entities.RemoveAt(i);
                        i--;
                    }
                }
            }

            var formatted = ClientEx.ParseMarkdown(markdown, entities);
            var text = formatted.Text;
            var previous = 0;

            if (handleLinks && formatted.Entities.Count == 1 && formatted.Entities[0].Type is TextEntityTypeBold)
            {
                formatted.Entities[0].Type = new TextEntityTypeTextUrl(string.Empty);
            }

            foreach (var entity in formatted.Entities)
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

                var substring = text.Substring(entity.Offset, entity.Length);

                if (entity.Type is TextEntityTypeBold)
                {
                    sender.Inlines.Add(substring, FontWeights.SemiBold);
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    sender.Inlines.Add(substring, FontStyle.Italic);
                }
                else if (entity.Type is TextEntityTypeTextUrl textUrl)
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(substring);
                    hyperlink.Click += (s, args) => Hyperlink_Click(s, entity.Type, textUrl.Url);
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    sender.Inlines.Add(hyperlink);
                }
                else if (entity.Type is TextEntityTypeMention)
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(substring);
                    hyperlink.Click += (s, args) => Hyperlink_Click(s, entity.Type, substring);
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    sender.Inlines.Add(hyperlink);
                }
                else if (entity.Type is TextEntityTypeUrl && handleLinks)
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(substring);
                    hyperlink.Click += (s, args) => Hyperlink_Click(s, entity.Type, substring);
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    sender.Inlines.Add(hyperlink);
                }
                else
                {
                    sender.Inlines.Add(substring);
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                sender.Inlines.Add(text.Substring(previous));
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
                    var substring = text.Substring(entity.Offset, entity.Length);

                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Click(s, entity.Type, textUrl.Url);
                        hyperlink.UnderlineStyle = UnderlineStyle.None;
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

            var entities = ClientEx.GetTextEntities(newValue);
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
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Click(s, entity.Type, textUrl.Url);
                        hyperlink.UnderlineStyle = UnderlineStyle.None;
                        span.Inlines.Add(hyperlink);
                        local = hyperlink;
                    }
                    else if (entity.Type is TextEntityTypeUrl url)
                    {
                        var data = text.Substring(entity.Offset, entity.Length);
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Click(s, entity.Type, data);
                        hyperlink.UnderlineStyle = UnderlineStyle.None;
                        span.Inlines.Add(hyperlink);
                        local = hyperlink;
                    }
                    else if (entity.Type is TextEntityTypeMention mention)
                    {
                        var data = text.Substring(entity.Offset + 1, entity.Length - 1);
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Click(s, entity.Type, data);
                        hyperlink.UnderlineStyle = UnderlineStyle.None;
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

        private static void Hyperlink_Click(Hyperlink sender, TextEntityType type, string data)
        {
            var navigationService = WindowContext.Current.GetNavigationService();
            if (navigationService == null)
            {
                return;
            }

            var clientService = TypeResolver.Current.Resolve<IClientService>(navigationService.SessionId);

            if (type is TextEntityTypeTextUrl textUrl)
            {
                if (string.IsNullOrEmpty(textUrl.Url))
                {
                    var header = sender.GetParent<HeaderedControl>();
                    if (header != null)
                    {
                        header?.OnClick(string.Empty);
                        return;
                    }

                    var footer = sender.GetParent<SettingsFooter>();
                    footer?.OnClick(string.Empty);

                    var headline = sender.GetParent<SettingsHeadline>();
                    headline?.OnClick(string.Empty);
                }
                else
                {
                    MessageHelper.OpenUrl(clientService, navigationService, data);
                }
            }
            else if (type is TextEntityTypeMention)
            {
                MessageHelper.NavigateToUsername(clientService, navigationService, data.TrimStart('@'));
            }
            else if (type is TextEntityTypeUrl)
            {
                var header = sender.GetParent<HeaderedControl>();
                if (header != null)
                {
                    header?.OnClick(data);
                    return;
                }

                var footer = sender.GetParent<SettingsFooter>();
                footer?.OnClick(data);

                var headline = sender.GetParent<SettingsHeadline>();
                headline?.OnClick(string.Empty);
            }
        }
    }
}
