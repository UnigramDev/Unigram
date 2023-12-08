//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Text.RegularExpressions;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Host;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
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

            var entities = Client.Execute(new GetTextEntities(markdown)) as TextEntities;

            for (int i = 0; i < entities.Entities.Count; i++)
            {
                if (entities.Entities[i].Type is TextEntityTypeUrl)
                {
                    entities.Entities.RemoveAt(i);
                    i--;
                }
            }

            var formatted = ClientEx.ParseMarkdown(markdown, entities.Entities);
            var text = formatted.Text;
            var previous = 0;

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
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    hyperlink.Click += (s, args) => Hyperlink_Click(entity.Type, textUrl.Url);
                    sender.Inlines.Add(hyperlink);
                }
                else if (entity.Type is TextEntityTypeMention)
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    hyperlink.Click += (s, args) => Hyperlink_Click(entity.Type, text.Substring(entity.Offset, entity.Length));
                    sender.Inlines.Add(hyperlink);
                }
                else
                {
                    sender.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
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
                    var substring = text.Substring(entity.Offset, entity.Length);

                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Click(entity.Type, textUrl.Url);
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
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Click(entity.Type, textUrl.Url);
                        span.Inlines.Add(hyperlink);
                        local = hyperlink;
                    }
                    else if (entity.Type is TextEntityTypeUrl url)
                    {
                        var data = text.Substring(entity.Offset, entity.Length);
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Click(entity.Type, data);
                        span.Inlines.Add(hyperlink);
                        local = hyperlink;
                    }
                    else if (entity.Type is TextEntityTypeMention mention)
                    {
                        var data = text.Substring(entity.Offset + 1, entity.Length - 1);
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Click(entity.Type, data);
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

        private static void Hyperlink_Click(TextEntityType type, string data)
        {
            IClientService clientService = null;
            INavigationService navigationService = null;

            // TODO: move the code to resolve the current session from Window to TypeResolver
            if (Window.Current.Content is RootPage rootPage && rootPage.NavigationService != null)
            {
                navigationService = rootPage.NavigationService;
                clientService = TypeResolver.Current.Resolve<IClientService>(navigationService.SessionId);
            }
            else if (Window.Current.Content is StandalonePage standalonePage && standalonePage.NavigationService != null)
            {
                navigationService = standalonePage.NavigationService;
                clientService = TypeResolver.Current.Resolve<IClientService>(navigationService.SessionId);
            }

            if (clientService != null && navigationService != null)
            {
                if (type is TextEntityTypeTextUrl)
                {
                    MessageHelper.OpenUrl(clientService, navigationService, data);
                }
                else if (type is TextEntityTypeMention)
                {
                    MessageHelper.NavigateToUsername(clientService, navigationService, data.TrimStart('@'));
                }
            }
        }
    }
}
