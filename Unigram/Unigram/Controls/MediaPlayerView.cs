using System;
using System.Linq;
using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class MediaPlayerView : MediaPlayerPresenter
    {
        //public MediaPlayerView()
        //{
        //    DefaultStyleKey = typeof(MediaPlayerView);
        //}

        #region Constraint

        public object Constraint
        {
            get { return (object)GetValue(ConstraintProperty); }
            set { SetValue(ConstraintProperty, value); }
        }

        public static readonly DependencyProperty ConstraintProperty =
            DependencyProperty.Register("Constraint", typeof(object), typeof(MediaPlayerView), new PropertyMetadata(null, OnConstraintChanged));

        private static void OnConstraintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MediaPlayerView)d).InvalidateMeasure();
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
                ttl = viewModel.Ttl > 0;
                constraint = viewModel.Content;
            }
            else if (constraint is Message message)
            {
                ttl = message.Ttl > 0;
                constraint = message.Content;
            }

            #region MessageContent

            if (constraint is MessageAnimation animationMessage)
            {
                constraint = animationMessage.Animation;
            }
            else if (constraint is MessageInvoice invoiceMessage)
            {
                constraint = invoiceMessage.Photo;
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
                constraint = photoMessage.Photo;
            }
            else if (constraint is MessageSticker stickerMessage)
            {
                constraint = stickerMessage.Sticker;
            }
            else if (constraint is MessageText textMessage)
            {
                if (textMessage?.WebPage?.Animation != null)
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
                constraint = videoMessage.Video;
            }
            else if (constraint is MessageVideoNote videoNoteMessage)
            {
                constraint = videoNoteMessage.VideoNote;
            }
            else if (constraint is MessageChatChangePhoto chatChangePhoto)
            {
                constraint = chatChangePhoto.Photo;
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

            if (constraint is PhotoSize photoSize)
            {
                width = photoSize.Width;
                height = photoSize.Height;
            }



            Calculate:
            if (width > availableWidth || height > availableHeight)
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
