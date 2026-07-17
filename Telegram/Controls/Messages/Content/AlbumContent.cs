//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class AlbumContent : Grid, IContentWithFile
    {
        public MessageViewModel Message => _message;
        private MessageViewModel _message;

        public AlbumContent(MessageViewModel message)
        {
            UpdateMessage(message);

            // I don't like this much, but it's the easier way to add margins between children
            //Margin = new Thickness(0, 0, -MessageAlbum.ITEM_MARGIN, -MessageAlbum.ITEM_MARGIN);
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

            var positions = album.GetPositionsForWidth(availableSize.Width, true);

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

        public Rect Highlight(MessageBubbleHighlightOptions options)
        {
            foreach (var child in Children)
            {
                if (child is MessageSelector selector
                    && selector.Message.Id == options.MessageId)
                {
                    var transform = child.TransformToVisual(this);
                    var point = transform.TransformPoint(new Point());

                    return new Rect(point.X, point.Y, selector.ActualWidth, selector.ActualHeight);
                }
            }

            return Rect.Empty;
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
                    element = new PhotoContent(pos, null, true);
                }
                else if (pos.Content is MessageVideo)
                {
                    element = new VideoContent(pos, null, true);
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

                var selector = new MessageSelector(pos, element)
                {
                    IsTrackerEnabled = false
                };

                Children.Add(selector);

                if (album.IsMedia)
                {
                    element.MinWidth = 0;
                    element.MinHeight = 0;
                    element.MaxWidth = double.PositiveInfinity;
                    element.MaxHeight = double.PositiveInfinity;
                    continue;
                }
                else if (pos == album.Messages.Last())
                {
                    return;
                }

                element.Margin = new Thickness(0, 0, 0, 2);
                selector.Margin = new Thickness(0, 0, 0, 6);

                if (string.IsNullOrEmpty(pos.Text?.Text))
                {
                    continue;
                }

                var textBlock = new FormattedTextBlock
                {
                    AdjustLineEnding = true,
                    Margin = new Thickness(0, 0, 0, 12)
                };

                textBlock.SetText(message.ClientService, pos.Text);

                textBlock.Tag = pos;
                textBlock.TextEntityClick += Message_TextEntityClick;

                Children.Add(textBlock);
            }
        }

        private void Message_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (sender is not FormattedTextBlock textBlock || textBlock.Tag is not MessageViewModel message || message.Delegate == null)
            {
                return;
            }

            MessageBubble.TextEntityClick(message, textBlock, e);
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
        }

        public void UpdateSelection(long messageId)
        {
            foreach (var child in Children)
            {
                if (child is MessageSelector selector && selector.Message?.Id == messageId)
                {
                    selector.UpdateSelection();
                    return;
                }
            }
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
