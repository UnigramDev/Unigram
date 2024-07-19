//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Controls.Messages.Content
{
    public sealed class PaidMediaContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public PaidMediaContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(PaidMediaContent);
        }

        #region InitializeComponent

        private PaidMediaContentPanel LayoutRoot;
        private Border Overlay;
        private TextBlock Subtitle;
        private Button Button;
        private Run TextPart1;
        private Run TextPart2;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as PaidMediaContentPanel;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            Button = GetTemplateChild(nameof(Button)) as Button;
            TextPart1 = GetTemplateChild(nameof(TextPart1)) as Run;
            TextPart2 = GetTemplateChild(nameof(TextPart2)) as Run;

            Button.Click += Button_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var album = GetContent(message);
            if (album == null || !_templateApplied)
            {
                return;
            }

            LayoutRoot.UpdateMessage(message);

            var locked = album.Media.Any(x => x is PaidMediaPreview);
            if (locked)
            {
                var text = Locale.Declension(Strings.R.UnlockPaidContent, album.StarCount);
                var index = text.IndexOf("\u2B50\uFE0F");

                TextPart1.Text = text.Substring(0, index);
                TextPart2.Text = text.Substring(index + 2);

                Button.Visibility = Visibility.Visible;
                Overlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                Button.Visibility = Visibility.Collapsed;
                Overlay.Visibility = Visibility.Visible;

                Subtitle.Text = Icons.Premium + "\u2004" + album.StarCount;
            }
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessagePaidAlbum;
        }

        private MessagePaidAlbum GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            if (content is MessagePaidAlbum paidAlbum)
            {
                return paidAlbum;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var album = GetContent(_message);
            if (album != null)
            {
                _message.Delegate.NavigationService.NavigateToInvoice(_message);
            }
        }
    }

    public sealed class PaidMediaContentPanel : Grid, IContentWithFile
    {
        public MessageViewModel Message => _message;
        private MessageViewModel _message;

        public PaidMediaContentPanel()
        {
            // I don't like this much, but it's the easier way to add margins between children
            Margin = new Thickness(0, 0, -MessageAlbum.ITEM_MARGIN, -MessageAlbum.ITEM_MARGIN);
        }

        private (Rect[], Size) _positions;

        protected override Size MeasureOverride(Size availableSize)
        {
            var album = _message?.Content as MessagePaidAlbum;
            if (album == null || album.Media.Count <= 1)
            {
                return base.MeasureOverride(availableSize);
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
            var album = _message?.Content as MessagePaidAlbum;
            if (album == null || album.Media.Count <= 1)
            {
                return base.ArrangeOverride(finalSize);
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

            var album = message.Content as MessagePaidAlbum;
            if (album == null)
            {
                return;
            }

            Children.Clear();

            if (album.Media.Count == 1)
            {
                if (album.Media[0] is PaidMediaPhoto extendedPhoto)
                {
                    Children.Add(new PhotoContent(message, extendedPhoto));
                }
                else if (album.Media[0] is PaidMediaVideo extendedVideo)
                {
                    Children.Add(new VideoContent(message, extendedVideo));
                }
                else if (album.Media[0] is PaidMediaPreview extendedPreview)
                {
                    Children.Add(new PreviewContent(message, extendedPreview));
                }
                //else if (album.Media[0] is PaidMediaUnsupported)
                //{
                //    Children.Add(new DocumentContent(album.Media[0]));
                //}

                return;
            }

            foreach (var pos in album.Media)
            {
                FrameworkElement element;
                if (pos is PaidMediaPhoto extendedPhoto)
                {
                    element = new PhotoContent(message, extendedPhoto, true);
                }
                else if (pos is PaidMediaVideo extendedVideo)
                {
                    element = new VideoContent(message, extendedVideo);
                }
                else if (pos is PaidMediaPreview extendedPreview)
                {
                    element = new PreviewContent(message, extendedPreview);
                }
                //else if (pos is PaidMediaUnsupported)
                //{
                //    element = new DocumentContent(pos);
                //}
                else
                {
                    continue;
                }

                Children.Add(element);

                element.MinWidth = 0;
                element.MinHeight = 0;
                element.MaxWidth = MessageAlbum.MAX_WIDTH;
                element.MaxHeight = MessageAlbum.MAX_HEIGHT;
                element.Margin = new Thickness(0, 0, MessageAlbum.ITEM_MARGIN, MessageAlbum.ITEM_MARGIN);
                element.Tag = true;
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
