using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class AlbumContent : Grid, IContentWithFile
    {
        public MessageViewModel Message => _message;
        private MessageViewModel _message;

        public const double ITEM_MARGIN = 2;
        public const double MAX_WIDTH = 320 + ITEM_MARGIN;
        public const double MAX_HEIGHT = 420 + ITEM_MARGIN;

        public AlbumContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);

            // I don't like this much, but it's the easier way to add margins between children
            Margin = new Thickness(0, 0, -2, -2);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var album = _message.Content as MessageAlbum;
            if (album == null)
            {
                return base.MeasureOverride(availableSize);
            }

            var groupedMessages = album.Layout;
            if (groupedMessages.Messages.Count == 1)
            {
                return base.MeasureOverride(availableSize);
            }

            var positions = groupedMessages.Positions.ToList();

            var groupedWidth = (double)groupedMessages.Width;
            //var width = groupedMessages.Width / 800d * Math.Min(availableSize.Width, MAX_WIDTH);
            var width = availableSize.Width;
            var height = width / MAX_WIDTH * MAX_HEIGHT;
            //var height = groupedMessages.Width / 800d * MAX_HEIGHT;

            var size = new Size(width, groupedMessages.Height * height);

            for (int i = 0; i < Math.Min(positions.Count, Children.Count); i++)
            {
                Children[i].Measure(new Size(positions[i].Value.Width / groupedWidth * width, height * positions[i].Value.Height));
            }

            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var album = _message.Content as MessageAlbum;
            if (album == null)
            {
                return base.ArrangeOverride(finalSize);
            }

            var groupedMessages = album.Layout;
            if (groupedMessages.Messages.Count == 1)
            {
                return base.ArrangeOverride(finalSize);
            }

            var positions = groupedMessages.Positions.ToList();

            var groupedWidth = (double)groupedMessages.Width;
            var width = finalSize.Width;
            var height = width / MAX_WIDTH * MAX_HEIGHT;
            //var height = groupedMessages.Width / 800d * MAX_HEIGHT;

            var size = new Size(width, groupedMessages.Height * height);

            var total = 0d;
            var space = 0d;

            for (int i = 0; i < Math.Min(positions.Count, Children.Count); i++)
            {
                var position = positions[i];

                var top = total;
                var left = 0d;

                if (i > 0)
                {
                    var pos = positions[i - 1];
                    // in one row
                    if (pos.Value.MinY == position.Value.MinY)
                    {
                        for (var j = i - 1; j >= 0; j--)
                        {
                            pos = positions[j];
                            if (pos.Value.MinY == position.Value.MinY)
                            {
                                left += pos.Value.Width / groupedWidth * width;
                            }
                        }
                    }
                    // in one column
                    else if (position.Value.SpanSize == groupedMessages.MaxSizeWidth || position.Value.SpanSize == 1000)
                    {
                        left = position.Value.LeftSpanOffset / groupedWidth * width;
                        // find common big message
                        KeyValuePair<MessageViewModel, GroupedMessagePosition>? leftColumn = null;
                        for (var j = i - 1; j >= 0; j--)
                        {
                            pos = positions[j];
                            if (pos.Value.SiblingHeights != null)
                            {
                                leftColumn = pos;
                                break;
                            }
                            else if (pos.Value.LeftSpanOffset > 0)
                            {
                                top += height * pos.Value.Height;
                            }
                        }
                    }
                }

                space += positions[i].Value.Width;

                if (space >= groupedMessages.Width)
                {
                    space = 0;
                    total += height * position.Value.Height;
                }

                Children[i].Arrange(new Rect(left, top, positions[i].Value.Width / groupedWidth * width, height * positions[i].Value.Height));
            }

            return size;
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

            var groupedMessages = album.Layout;
            if (groupedMessages.Messages.Count == 1)
            {
                if (groupedMessages.Messages[0].Content is MessagePhoto)
                {
                    Children.Add(new PhotoContent(groupedMessages.Messages[0]));
                }
                else if (groupedMessages.Messages[0].Content is MessageVideo)
                {
                    Children.Add(new VideoContent(groupedMessages.Messages[0]));
                }

                return;
            }

            var positions = groupedMessages.Positions.ToList();

            foreach (var pos in positions)
            {
                AspectView element = null;
                if (pos.Key.Content is MessagePhoto)
                {
                    element = new PhotoContent(pos.Key);
                }
                else if (pos.Key.Content is MessageVideo)
                {
                    element = new VideoContent(pos.Key);
                }

                if (element != null)
                {
                    element.MinWidth = 0;
                    element.MinHeight = 0;
                    element.MaxWidth = MAX_WIDTH;
                    element.MaxHeight = MAX_HEIGHT;
                    element.BorderThickness = new Thickness(0, 0, ITEM_MARGIN, ITEM_MARGIN);
                    element.Tag = pos.Value;

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
                    var media = album.Layout.Messages.FirstOrDefault(x => x.Id == content.Message?.Id);
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
