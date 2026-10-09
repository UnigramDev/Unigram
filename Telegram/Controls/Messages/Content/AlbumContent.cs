//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
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

        private readonly MessageContentRecyclePool _recyclePool;

        public AlbumContent(MessageViewModel message, MessageContentRecyclePool recyclePool)
        {
            _recyclePool = recyclePool;
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

            if (album.Messages.Count == 1)
            {
                var single = album.Messages[0];

                if (Children.Count == 1 && Children[0] is IContent content && content.IsValid(single.Content, true))
                {
                    content.UpdateMessage(single);
                    return;
                }

                ReleaseChildren();

                var element = CreateContent(single, false);
                if (element != null)
                {
                    Children.Add(element);
                }

                return;
            }

            var index = 0;
            var last = album.Messages[album.Messages.Count - 1];

            for (int i = 0; i < album.Messages.Count; i++)
            {
                var pos = album.Messages[i];
                if (pos.Content is not (MessagePhoto or MessageVideo or MessageAudio or MessageDocument))
                {
                    continue;
                }

                // A caption this item no longer has, or the lone control of a single-item
                // album, sits where a selector belongs and cannot be reused as one.
                while (index < Children.Count && Children[index] is not MessageSelector)
                {
                    ReleaseAt(index);
                }

                FrameworkElement element;
                var created = true;

                if (index < Children.Count && Children[index] is MessageSelector selector)
                {
                    if (selector.Content is IContent content && content.IsValid(pos.Content, true))
                    {
                        element = selector.Content as FrameworkElement;
                        content.UpdateMessage(pos);
                        created = false;
                    }
                    else
                    {
                        var previous = selector.Content;
                        selector.Content = null;
                        Release(previous);

                        element = CreateContent(pos, true);
                        selector.Content = element;
                    }

                    selector.UpdateMessage(pos, null, pos.Delegate?.IsSelectionEnabled ?? false);
                }
                else
                {
                    element = CreateContent(pos, true);
                    selector = new MessageSelector(pos, element)
                    {
                        IsTrackerEnabled = false
                    };

                    Children.Insert(index, selector);
                }

                index++;

                if (album.IsMedia)
                {
                    if (created)
                    {
                        element.MinWidth = 0;
                        element.MinHeight = 0;
                        element.MaxWidth = double.PositiveInfinity;
                        element.MaxHeight = double.PositiveInfinity;
                    }

                    continue;
                }
                else if (pos == last)
                {
                    element.ClearValue(MarginProperty);
                    break;
                }

                if (string.IsNullOrEmpty(pos.Text?.Text))
                {
                    element.Margin = new Thickness(0, 0, 0, -4);
                    continue;
                }

                element.ClearValue(MarginProperty);

                if (index >= Children.Count || Children[index] is not FormattedTextBlock textBlock)
                {
                    textBlock = new FormattedTextBlock
                    {
                        TextSelection = TextSelectionMode.Extended,
                        Margin = new Thickness(10, 0, 10, 8)
                    };

                    textBlock.TextEntityClick += Message_TextEntityClick;
                    Children.Insert(index, textBlock);
                }

                textBlock.SetText(message.ClientService, pos.Text);
                textBlock.Tag = pos;

                index++;
            }

            while (Children.Count > index)
            {
                ReleaseAt(Children.Count - 1);
            }

            InvalidateMeasure();
        }

        private FrameworkElement CreateContent(MessageViewModel message, bool album)
        {
            var recycled = _recyclePool?.TryGet(message.Content);
            if (recycled != null)
            {
                if (recycled is PhotoContent photo)
                {
                    photo.IsAlbum = album;
                }
                else if (recycled is VideoContent video)
                {
                    video.IsAlbum = album;
                }

                recycled.UpdateMessage(message);
                return recycled as FrameworkElement;
            }

            return message.Content switch
            {
                MessagePhoto => new PhotoContent(message, null, album),
                MessageVideo => new VideoContent(message, null, album),
                MessageAudio => new AudioContent(message),
                MessageDocument => new DocumentContent(message),
                _ => null
            };
        }

        public void ReleaseChildren()
        {
            while (Children.Count > 0)
            {
                ReleaseAt(Children.Count - 1);
            }
        }

        private void ReleaseAt(int index)
        {
            var child = Children[index];
            Children.RemoveAt(index);

            if (child is MessageSelector selector)
            {
                var content = selector.Content;
                selector.Content = null;
                Release(content);
            }
            else if (child is FormattedTextBlock textBlock)
            {
                textBlock.TextEntityClick -= Message_TextEntityClick;
            }
            else
            {
                Release(child);
            }
        }

        private void Release(object child)
        {
            if (child is not IContent content)
            {
                return;
            }

            content.Recycle();

            if (_recyclePool != null && child is FrameworkElement element)
            {
                // A bubble taking it from the pool expects what its style sets, not what
                // this album applied on top.
                element.ClearValue(MinWidthProperty);
                element.ClearValue(MinHeightProperty);
                element.ClearValue(MaxWidthProperty);
                element.ClearValue(MaxHeightProperty);
                element.ClearValue(MarginProperty);

                if (child is PhotoContent photo)
                {
                    photo.IsAlbum = false;
                }
                else if (child is VideoContent video)
                {
                    video.IsAlbum = false;
                }

                _recyclePool.Put(content);
            }
        }

        private void Message_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (sender is not FormattedTextBlock textBlock || textBlock.Tag is not MessageViewModel message || message.Delegate == null)
            {
                return;
            }

            MessageBubble.TextEntityClick(message, e);
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
                else if (child is IContent content)
                {
                    content.Recycle();
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
