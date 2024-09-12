using System;
using Telegram.Common;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Cells.Business
{
    public sealed partial class BusinessRepliesCell : Grid
    {
        private int _thumbnailId;

        public BusinessRepliesCell()
        {
            InitializeComponent();
        }

        public string ChevronGlyph
        {
            get => Chevron.Text;
            set => Chevron.Text = value;
        }

        public void UpdateContent(IClientService clientService, QuickReplyShortcut replies, bool template)
        {
            if (clientService.TryGetUser(clientService.Options.MyId, out User user))
            {
                Photo.SetUser(clientService, user, 36);
            }

            if (template)
            {
                FromLabel.Text = user.FullName() + Environment.NewLine;
            }
            else
            {
                FromLabel.Text = string.Format("/{0} ", replies.Name);
            }

            var message = replies.FirstMessage;
            if (message != null)
            {
                CustomEmojiIcon.Add(BriefText, BriefLabel.Inlines, clientService, ChatCell.UpdateBriefLabel(null, message.Content, true, false, false, out MinithumbnailId thumbnail), "InfoCustomEmojiStyle");
                UpdateMinithumbnail(thumbnail);
            }

            if (replies.MessageCount > 1)
            {
                More.Text = Locale.Declension(Strings.R.BusinessRepliesMore, replies.MessageCount - 1);
                More.Visibility = Visibility.Visible;
            }
            else
            {
                More.Visibility = Visibility.Collapsed;
            }
        }

        private async void UpdateMinithumbnail(MinithumbnailId thumbnail)
        {
            if (thumbnail != null)
            {
                if (_thumbnailId == thumbnail.Id)
                {
                    return;
                }

                _thumbnailId = thumbnail.Id;

                double ratioX = (double)16 / thumbnail.Width;
                double ratioY = (double)16 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                var bitmap = new BitmapImage
                {
                    DecodePixelWidth = width,
                    DecodePixelHeight = height,
                    DecodePixelType = DecodePixelType.Logical
                };

                Minithumbnail.ImageSource = bitmap;
                MinithumbnailPanel.Visibility = Visibility.Visible;

                MinithumbnailPanel.CornerRadius = new CornerRadius(thumbnail.IsVideoNote ? 9 : 2);

                using (var stream = new InMemoryRandomAccessStream())
                {
                    try
                    {
                        PlaceholderImageHelper.WriteBytes(thumbnail.Data, stream);
                        await bitmap.SetSourceAsync(stream);
                    }
                    catch
                    {
                        // Throws when the data is not a valid encoded image,
                        // not so frequent, but if it happens during ContainerContentChanging it crashes the app.
                    }
                }
            }
            else
            {
                _thumbnailId = 0;

                MinithumbnailPanel.Visibility = Visibility.Collapsed;
                Minithumbnail.ImageSource = null;
            }
        }
    }

    public partial class BusinessRepliesPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var text1 = Children[0] as RichTextBlock;
            var more1 = Children[1];
            var text2 = Children[2];

            text1.Measure(availableSize);
            more1.Measure(availableSize);

            text2.Measure(new Size(availableSize.Width - more1.DesiredSize.Width, availableSize.Height));

            return new Size(availableSize.Width, text1.DesiredSize.Height * 2);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var text1 = Children[0] as RichTextBlock;
            var more1 = Children[1];
            var text2 = Children[2];

            text1.Arrange(new Rect(0, 0, text1.DesiredSize.Width, text1.DesiredSize.Height));
            text2.Arrange(new Rect(0, text1.DesiredSize.Height, text2.DesiredSize.Width, text2.DesiredSize.Height));

            if (text2.DesiredSize.Width > 0)
            {
                more1.Arrange(new Rect(text2.DesiredSize.Width, text1.DesiredSize.Height, more1.DesiredSize.Width, more1.DesiredSize.Height));
            }
            else if (text1.DesiredSize.Width < finalSize.Width - more1.DesiredSize.Width)
            {
                more1.Arrange(new Rect(text1.DesiredSize.Width, 0, more1.DesiredSize.Width, more1.DesiredSize.Height));
            }
            else
            {
                more1.Arrange(new Rect(-8, text1.DesiredSize.Height, more1.DesiredSize.Width, more1.DesiredSize.Height));
            }

            return finalSize;
        }
    }
}
