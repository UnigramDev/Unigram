//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    public sealed class AlbumContent : Grid, IContentWithFile
    {
        public MessageViewModel Message => _message;
        private MessageViewModel _message;

        public AlbumContent(MessageViewModel message)
        {
            UpdateMessage(message);

            // I don't like this much, but it's the easier way to add margins between children
            Margin = new Thickness(0, 0, -MessageAlbum.ITEM_MARGIN, -MessageAlbum.ITEM_MARGIN);
        }

        private (Rect[], Size) _positions;

        protected override Size MeasureOverride(Size availableSize)
        {
            var album = _message?.Content as MessageAlbum;
            if (album == null || album.Messages.Count <= 1)
            {
                return base.MeasureOverride(availableSize);
            }
            else if (!album.IsMedia)
            {
                var width = 0d;
                var height = 0d;

                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.Measure(availableSize);
                    width = Math.Max(child.DesiredSize.Width, width);
                    height += child.DesiredSize.Height;
                }

                return new Size(width, height);
            }

            var positions = album.GetPositionsForWidth(availableSize.Width);

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Measure(positions.Item1[i].ToSize());
            }

            _positions = positions;
            return positions.Item2;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var album = _message?.Content as MessageAlbum;
            if (album == null || album.Messages.Count <= 1)
            {
                return base.ArrangeOverride(finalSize);
            }
            else if (!album.IsMedia)
            {
                var width = 0d;
                var height = 0d;

                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.Arrange(new Rect(0, height, child.DesiredSize.Width, child.DesiredSize.Height));
                    width = Math.Max(child.DesiredSize.Width, width);
                    height += child.DesiredSize.Height;
                }

                return finalSize;
            }

            var positions = _positions;
            if (positions.Item1 == null || positions.Item1.Length == 1)
            {
                return base.ArrangeOverride(finalSize);
            }

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Arrange(positions.Item1[i]);
            }

            return finalSize;
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var album = message.Content as MessageAlbum;
            if (album == null)
            {
                return;
            }

            Children.Clear();

            if (album.Messages.Count == 1)
            {
                if (album.Messages[0].Content is MessagePhoto)
                {
                    Children.Add(new PhotoContent(album.Messages[0]));
                }
                else if (album.Messages[0].Content is MessageVideo)
                {
                    Children.Add(new VideoContent(album.Messages[0]));
                }
                else if (album.Messages[0].Content is MessageAudio)
                {
                    Children.Add(new AudioContent(album.Messages[0]));
                }
                else if (album.Messages[0].Content is MessageDocument)
                {
                    Children.Add(new DocumentContent(album.Messages[0]));
                }

                return;
            }

            foreach (var pos in album.Messages)
            {
                FrameworkElement element;
                if (pos.Content is MessagePhoto)
                {
                    element = new PhotoContent(pos, true);
                }
                else if (pos.Content is MessageVideo)
                {
                    element = new VideoContent(pos);
                }
                else if (pos.Content is MessageAudio)
                {
                    element = new AudioContent(pos);
                }
                else if (pos.Content is MessageDocument)
                {
                    element = new DocumentContent(pos);
                }
                else
                {
                    continue;
                }

                Children.Add(new MessageSelector(pos, element));

                if (album.IsMedia)
                {
                    element.MinWidth = 0;
                    element.MinHeight = 0;
                    element.MaxWidth = MessageAlbum.MAX_WIDTH;
                    element.MaxHeight = MessageAlbum.MAX_HEIGHT;
                    element.Margin = new Thickness(0, 0, MessageAlbum.ITEM_MARGIN, MessageAlbum.ITEM_MARGIN);
                    element.Tag = true;

                    continue;
                }
                else if (pos == album.Messages.Last())
                {
                    return;
                }

                element.Margin = new Thickness(0, 0, 0, 8);

                var caption = pos.GetCaption();
                if (string.IsNullOrEmpty(caption?.Text))
                {
                    continue;
                }

                var span = new Span();
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(span);

                var rich = new RichTextBlock();
                rich.Style = BootStrapper.Current.Resources["EmojiRichTextBlockStyle"] as Style;
                rich.Blocks.Add(paragraph);
                rich.Margin = new Thickness(0, 0, 0, 8);

                ReplaceEntities(message, rich, span, caption, out _);
                Children.Add(rich);
            }
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
        }

        public void UpdateSelectionEnabled(bool value, bool animate)
        {
            foreach (var child in Children)
            {
                if (child is MessageSelector selector)
                {
                    selector.UpdateSelectionEnabled(value, animate);
                }
            }
        }

        #region Caption

        private bool ReplaceEntities(MessageViewModel message, RichTextBlock rich, Span span, FormattedText text, out bool adjust)
        {
            if (text == null)
            {
                adjust = false;
                return false;
            }

            return ReplaceEntities(message, rich, span, text.Text, text.Entities, out adjust);
        }

        private bool ReplaceEntities(MessageViewModel message, RichTextBlock rich, Span span, string text, IList<TextEntity> entities, out bool adjust)
        {
            if (string.IsNullOrEmpty(text))
            {
                adjust = false;
                return false;
            }

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

                    if (entity.HasFlag(TextStyle.Mention) || entity.HasFlag(TextStyle.Url))
                    {
                        if (entity.Type is TextEntityTypeMentionName or TextEntityTypeTextUrl)
                        {
                            var hyperlink = new Hyperlink();
                            object data;
                            if (entity.Type is TextEntityTypeTextUrl textUrl)
                            {
                                data = textUrl.Url;
                                MessageHelper.SetEntityData(hyperlink, textUrl.Url);
                                MessageHelper.SetEntityType(hyperlink, entity.Type);

                                Extensions.SetToolTip(hyperlink, textUrl.Url);
                            }
                            else if (entity.Type is TextEntityTypeMentionName mentionName)
                            {
                                data = mentionName.UserId;
                            }

                            hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, null);
                            hyperlink.Foreground = GetBrush("MessageForegroundLinkBrush");
                            //hyperlink.Foreground = foreground;

                            span.Inlines.Add(hyperlink);
                            local = hyperlink;
                        }
                        else
                        {
                            var hyperlink = new Hyperlink();
                            var original = entities.FirstOrDefault(x => x.Offset <= entity.Offset && x.Offset + x.Length >= entity.End);

                            var data = text.Substring(entity.Offset, entity.Length);

                            if (original != null)
                            {
                                data = text.Substring(original.Offset, original.Length);
                            }

                            hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, data);
                            hyperlink.Foreground = GetBrush("MessageForegroundLinkBrush");
                            //hyperlink.Foreground = foreground;

                            //if (entity.Type is TextEntityTypeUrl || entity.Type is TextEntityTypeEmailAddress || entity.Type is TextEntityTypeBankCardNumber)
                            {
                                MessageHelper.SetEntityData(hyperlink, data);
                                MessageHelper.SetEntityType(hyperlink, entity.Type);
                            }

                            span.Inlines.Add(hyperlink);
                            local = hyperlink;
                        }
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

                    if (entity.Type is TextEntityTypeHashtag)
                    {
                        var data = text.Substring(entity.Offset, entity.Length);
                        var hex = data.TrimStart('#');

                        if ((hex.Length == 6 || hex.Length == 8) && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgba))
                        {
                            byte r, g, b, a;
                            if (hex.Length == 8)
                            {
                                r = (byte)((rgba & 0xff000000) >> 24);
                                g = (byte)((rgba & 0x00ff0000) >> 16);
                                b = (byte)((rgba & 0x0000ff00) >> 8);
                                a = (byte)(rgba & 0x000000ff);
                            }
                            else
                            {
                                r = (byte)((rgba & 0xff0000) >> 16);
                                g = (byte)((rgba & 0x00ff00) >> 8);
                                b = (byte)(rgba & 0x0000ff);
                                a = 0xFF;
                            }

                            var color = Color.FromArgb(a, r, g, b);
                            var border = new Border
                            {
                                Width = 12,
                                Height = 12,
                                Margin = new Thickness(4, 4, 0, -2),
                                Background = new SolidColorBrush(color)
                            };

                            span.Inlines.Add(new InlineUIContainer { Child = border });
                        }
                    }
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                span.Inlines.Add(new Run { Text = text.Substring(previous) });
            }

            var direction = NativeUtils.GetDirectionality(text);
            if (direction == TextDirectionality.RightToLeft && LocaleService.Current.FlowDirection == FlowDirection.LeftToRight)
            {
                //Footer.HorizontalAlignment = HorizontalAlignment.Left;
                //span.Inlines.Add(new LineBreak());
                rich.FlowDirection = FlowDirection.RightToLeft;
                adjust = true;
            }
            else if (direction == TextDirectionality.LeftToRight && LocaleService.Current.FlowDirection == FlowDirection.RightToLeft)
            {
                //Footer.HorizontalAlignment = HorizontalAlignment.Left;
                //span.Inlines.Add(new LineBreak());
                rich.FlowDirection = FlowDirection.LeftToRight;
                adjust = true;
            }
            else
            {
                //Footer.HorizontalAlignment = HorizontalAlignment.Right;
                rich.FlowDirection = LocaleService.Current.FlowDirection;
                adjust = false;
            }

            return true;
        }

        private Brush GetBrush(string key)
        {
            if (Resources.TryGetValue(key, out object value))
            {
                return value as SolidColorBrush;
            }

            return BootStrapper.Current.Resources[key] as SolidColorBrush;
        }

        private void Entity_Click(MessageViewModel message, TextEntityType type, string data)
        {
            if (type is TextEntityTypeBotCommand)
            {
                message.Delegate.SendBotCommand(data);
            }
            else if (type is TextEntityTypeEmailAddress)
            {
                message.Delegate.OpenUrl("mailto:" + data, false);
            }
            else if (type is TextEntityTypePhoneNumber)
            {
                message.Delegate.OpenUrl("tel:" + data, false);
            }
            else if (type is TextEntityTypeHashtag or TextEntityTypeCashtag)
            {
                message.Delegate.OpenHashtag(data);
            }
            else if (type is TextEntityTypeMention)
            {
                message.Delegate.OpenUsername(data);
            }
            else if (type is TextEntityTypeMentionName mentionName)
            {
                message.Delegate.OpenUser(mentionName.UserId);
            }
            else if (type is TextEntityTypeTextUrl textUrl)
            {
                message.Delegate.OpenUrl(textUrl.Url, true);
            }
            else if (type is TextEntityTypeUrl)
            {
                message.Delegate.OpenUrl(data, false);
            }
            else if (type is TextEntityTypeBankCardNumber)
            {
                message.Delegate.OpenBankCardNumber(data);
            }
        }

        #endregion

        public void Recycle()
        {
            _message = null;

            foreach (var child in Children)
            {
                if (child is MessageSelector selector)
                {
                    selector.Recycle();
                }
            }

            _positions = default;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageAlbum)
            {
                return true;
            }

            return false;
        }
    }
}
