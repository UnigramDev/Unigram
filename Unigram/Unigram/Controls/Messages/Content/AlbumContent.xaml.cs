using System;
using System.Linq;
using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class AlbumContent : Grid, IContentWithFile
    {
        public MessageViewModel Message => _message;
        private MessageViewModel _message;

        public AlbumContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);

            // I don't like this much, but it's the easier way to add margins between children
            Margin = new Thickness(0, 0, -MessageAlbum.ITEM_MARGIN, -MessageAlbum.ITEM_MARGIN);
        }

        private (Rect[], Size) _positions;

        protected override Size MeasureOverride(Size availableSize)
        {
            var album = _message.Content as MessageAlbum;
            if (album == null || album.Messages.Count == 1)
            {
                return base.MeasureOverride(availableSize);
            }

            var positions = album.GetPositionsForWidth(availableSize.Width);

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Measure(new Size(positions.Item1[i].Width, positions.Item1[i].Height));
            }

            _positions = positions;
            return positions.Item2;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var positions = _positions;
            if (positions.Item1 == null || positions.Item1.Length == 1)
            {
                return base.ArrangeOverride(finalSize);
            }

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Arrange(positions.Item1[i]);
            }

            return positions.Item2;
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

                return;
            }

            foreach (var pos in album.Messages)
            {
                AspectView element = null;
                if (pos.Content is MessagePhoto)
                {
                    element = new PhotoContent(pos);
                }
                else if (pos.Content is MessageVideo)
                {
                    element = new VideoContent(pos);
                }

                if (element != null)
                {
                    element.MinWidth = 0;
                    element.MinHeight = 0;
                    element.MaxWidth = MessageAlbum.MAX_WIDTH;
                    element.MaxHeight = MessageAlbum.MAX_HEIGHT;
                    element.BorderThickness = new Thickness(0, 0, MessageAlbum.ITEM_MARGIN, MessageAlbum.ITEM_MARGIN);
                    element.Tag = true;

                    Children.Add(element);
                }
            }
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var album = message.Content as MessageAlbum;
            if (album == null)
            {
                return;
            }

            foreach (var child in Children)
            {
                if (child is IContentWithFile content)
                {
                    var media = album.Messages.FirstOrDefault(x => x.Id == content.Message?.Id);
                    if (media != null)
                    {
                        content.UpdateFile(media, file);
                    }
                }
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageAlbum album)
            {
                return true;
            }

            return false;
        }
    }
}
