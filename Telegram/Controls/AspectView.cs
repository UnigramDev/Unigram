//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public class AspectView : Grid
    {
        #region Constraint

        public object Constraint
        {
            get => GetValue(ConstraintProperty);
            set => SetValue(ConstraintProperty, value);
        }

        public static readonly DependencyProperty ConstraintProperty =
            DependencyProperty.Register("Constraint", typeof(object), typeof(AspectView), new PropertyMetadata(null, OnConstraintChanged));

        private static void OnConstraintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AspectView)d).InvalidateMeasure();
        }

        #endregion

        #region Stretch

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register("Stretch", typeof(Stretch), typeof(AspectView), new PropertyMetadata(Stretch.Uniform, OnStretchChanged));

        private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AspectView)d).InvalidateMeasure();
        }

        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Constraint == null)
            {
                return base.MeasureOverride(availableSize);
            }

            var availableWidth = Math.Min(availableSize.Width, Math.Min(double.IsNaN(Width) ? double.PositiveInfinity : Width, MaxWidth));
            var availableHeight = Math.Min(availableSize.Height, Math.Min(double.IsNaN(Height) ? double.PositiveInfinity : Height, MaxHeight));

            var ttl = false;
            var width = 0.0;
            var height = 0.0;

            var constraint = Constraint;
            if (constraint is MessageViewModel viewModel)
            {
                //ttl = viewModel.SelfDestructTime > 0;
                constraint = viewModel.Content;

                if (viewModel.MediaAlbumId != 0 && Tag is true)
                {
                    return base.MeasureOverride(availableSize);
                }
            }
            else if (constraint is Message message)
            {
                //ttl = message.SelfDestructTime > 0;
                constraint = message.Content;

                if (message.MediaAlbumId != 0 && Tag is true)
                {
                    return base.MeasureOverride(availableSize);
                }
            }
            else if (Constraint is ViewModels.Chats.ChartViewData)
            {
                width = 640;
                height = 420;
            }
            else if (Constraint is Size size)
            {
                width = size.Width;
                height = size.Height;
            }

            #region MessageContent

            if (constraint is MessageAnimation animationMessage)
            {
                ttl = animationMessage.IsSecret;
                constraint = animationMessage.Animation;
            }
            else if (constraint is MessageInvoice invoiceMessage)
            {
                if (invoiceMessage.ExtendedMedia is MessageExtendedMediaPhoto extendedMediaPhoto)
                {
                    constraint = extendedMediaPhoto.Photo;
                }
                else if (invoiceMessage.ExtendedMedia is MessageExtendedMediaVideo extendedMediaVideo)
                {
                    constraint = extendedMediaVideo.Video;
                }
                else if (invoiceMessage.ExtendedMedia is MessageExtendedMediaPreview extendedMediaPreview)
                {
                    width = extendedMediaPreview.Width;
                    height = extendedMediaPreview.Height;
                }
                else
                {
                    constraint = invoiceMessage.Photo;
                }
            }
            else if (constraint is MessageGame gameMessage)
            {
                if (gameMessage.Game.Animation != null)
                {
                    constraint = gameMessage.Game.Animation;
                }
                else if (gameMessage.Game.Photo != null)
                {
                    constraint = gameMessage.Game.Photo;
                }
            }
            else if (constraint is MessageLocation locationMessage)
            {
                constraint = locationMessage.Location;
            }
            else if (constraint is MessagePhoto photoMessage)
            {
                ttl = photoMessage.IsSecret;
                constraint = photoMessage.Photo;
            }
            else if (constraint is MessageSticker stickerMessage)
            {
                constraint = stickerMessage.Sticker;
            }
            else if (constraint is MessageText textMessage)
            {
                if (string.Equals(textMessage?.WebPage?.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
                {
                    width = 900;
                    height = 1600;
                }
                else if (textMessage?.WebPage?.Animation != null)
                {
                    constraint = textMessage?.WebPage?.Animation;
                }
                else if (textMessage?.WebPage?.Document != null)
                {
                    constraint = textMessage?.WebPage?.Document;
                }
                else if (textMessage?.WebPage?.Photo != null)
                {
                    constraint = textMessage?.WebPage?.Photo;
                }
                else if (textMessage?.WebPage?.Sticker != null)
                {
                    constraint = textMessage?.WebPage?.Sticker;
                }
                else if (textMessage?.WebPage?.Video != null)
                {
                    constraint = textMessage?.WebPage?.Video;
                }
                else if (textMessage?.WebPage?.VideoNote != null)
                {
                    constraint = textMessage?.WebPage?.VideoNote;
                }
            }
            else if (constraint is MessageVenue venueMessage)
            {
                constraint = venueMessage.Venue;
            }
            else if (constraint is MessageVideo videoMessage)
            {
                ttl = videoMessage.IsSecret;
                constraint = videoMessage.Video;
            }
            else if (constraint is MessageVideoNote videoNoteMessage)
            {
                ttl = videoNoteMessage.IsSecret;
                constraint = videoNoteMessage.VideoNote;
            }
            else if (constraint is MessageChatChangePhoto chatChangePhoto)
            {
                constraint = chatChangePhoto.Photo;
            }
            else if (constraint is MessageExtendedMediaPreview extendedMediaPreview)
            {
                width = extendedMediaPreview.Width;
                height = extendedMediaPreview.Height;
            }
            else if (constraint is MessageAsyncStory asyncStory)
            {
                width = 720;
                height = 1280;
            }

            #endregion

            #region InlineQueryResult

            if (constraint is InlineQueryResultAnimation animationResult)
            {
                constraint = animationResult.Animation;
            }
            else if (constraint is InlineQueryResultLocation locationResult)
            {
                constraint = locationResult.Location;
            }
            else if (constraint is InlineQueryResultPhoto photoResult)
            {
                constraint = photoResult.Photo;
            }
            else if (constraint is InlineQueryResultSticker stickerResult)
            {
                constraint = stickerResult.Sticker;
            }
            else if (constraint is InlineQueryResultVideo videoResult)
            {
                constraint = videoResult.Video;
            }

            #endregion

            if (constraint is Animation animation)
            {
                width = animation.Width;
                height = animation.Height;
            }
            else if (constraint is Document document)
            {
                width = document.Thumbnail?.Width ?? width;
                height = document.Thumbnail?.Height ?? height;
            }
            else if (constraint is Location location)
            {
                width = 320;
                height = 200;
            }
            else if (constraint is Photo photo)
            {
                if (ttl)
                {
                    width = 240;
                    height = 240;
                }
                else
                {
                    constraint = photo.Sizes.OrderByDescending(x => x.Width).FirstOrDefault();
                }
            }
            else if (constraint is ChatPhoto chatPhoto)
            {
                constraint = chatPhoto.Sizes.OrderByDescending(x => x.Width).FirstOrDefault();
            }
            else if (constraint is Sticker sticker)
            {
                width = sticker.Width;
                height = sticker.Height;
            }
            else if (constraint is Venue venue)
            {
                width = 320;
                height = 200;
            }
            else if (constraint is Video video)
            {
                if (ttl)
                {
                    width = 240;
                    height = 240;
                }
                else
                {
                    width = video.Width;
                    height = video.Height;
                }
            }
            else if (constraint is VideoNote videoNote)
            {
                width = 200;
                height = 200;
            }
            else if (constraint is WebPage webPage)
            {
                width = webPage.EmbedWidth;
                height = webPage.EmbedHeight;
            }

            if (constraint is PhotoSize photoSize)
            {
                width = photoSize.Width;
                height = photoSize.Height;
            }

            if (constraint is PageBlockMap map)
            {
                width = map.Width;
                height = map.Height;
            }

            if (constraint is Background wallpaper)
            {
                width = 900;
                height = 1600;
            }

            if (width == 0 && height == 0)
            {
                width = int.MaxValue;
                height = int.MaxValue;
            }

        Calculate:
            if (width > availableWidth || height > availableHeight || Constraint is Size || Stretch == Stretch.UniformToFill)
            {
                var ratioX = availableWidth / width;
                var ratioY = availableHeight / height;
                var ratio = Math.Min(ratioX, ratioY);

                //if (Holder != null)
                //{
                //    Holder.Width = width * ratio;
                //    Holder.Height = height * ratio;
                //}

                base.MeasureOverride(new Size(width * ratio, height * ratio));
                return new Size(width * ratio, height * ratio);
            }
            else
            {
                base.MeasureOverride(new Size(width, height));
                return new Size(width, height);
            }
        }
    }
}
