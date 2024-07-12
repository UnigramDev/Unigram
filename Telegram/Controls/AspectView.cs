//
// Copyright Fela Ameghino 2015-2024
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
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public enum RotationAngle
    {
        Angle0,
        Angle90,
        Angle180,
        Angle270
    }

    public struct FixedSize
    {
        public FixedSize(double  width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; set; }

        public double Height { get; set; }
    }

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

        #region Rotate

        public RotationAngle RotationAngle
        {
            get => (RotationAngle)GetValue(RotationAngleProperty);
            set => SetValue(RotationAngleProperty, value);
        }

        public static readonly DependencyProperty RotationAngleProperty =
            DependencyProperty.Register("RotationAngle", typeof(RotationAngle), typeof(AspectView), new PropertyMetadata(RotationAngle.Angle0, OnRotationAngleChanged));

        private static void OnRotationAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
            else if (Constraint is FixedSize fixedSize)
            {
                base.MeasureOverride(new Size(fixedSize.Width, fixedSize.Height));
                return new Size(fixedSize.Width, fixedSize.Height);
            }

            var availableWidth = Math.Min(availableSize.Width, Math.Min(double.IsNaN(Width) ? double.PositiveInfinity : Width, MaxWidth));
            var availableHeight = Math.Min(availableSize.Height, Math.Min(double.IsNaN(Height) ? double.PositiveInfinity : Height, MaxHeight));

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
                constraint = animationMessage.Animation;
            }
            else if (constraint is MessageInvoice invoiceMessage)
            {
                if (invoiceMessage.PaidMedia is PaidMediaPhoto paidMediaPhoto)
                {
                    constraint = paidMediaPhoto.Photo;
                }
                else if (invoiceMessage.PaidMedia is PaidMediaVideo paidMediaVideo)
                {
                    constraint = paidMediaVideo.Video;
                }
                else if (invoiceMessage.PaidMedia is PaidMediaPreview paidMediaPreview)
                {
                    width = paidMediaPreview.Width;
                    height = paidMediaPreview.Height;
                }
                else
                {
                    constraint = invoiceMessage.ProductInfo.Photo;
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
                constraint = photoMessage.Photo;
            }
            else if (constraint is MessageSticker stickerMessage)
            {
                constraint = stickerMessage.Sticker;
            }
            else if (constraint is MessageText textMessage)
            {
                if (textMessage?.LinkPreview?.Type is LinkPreviewTypeBackground)
                {
                    width = 900;
                    height = 1600;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeAnimation previewAnimation)
                {
                    constraint = previewAnimation.Animation;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeDocument previewDocument)
                {
                    constraint = previewDocument.Document;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypePhoto previewPhoto)
                {
                    constraint = previewPhoto.Photo;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeSticker previewSticker)
                {
                    constraint = previewSticker.Sticker;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeVideo previewVideo)
                {
                    constraint = previewVideo.Video;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeVideoNote videoNote)
                {
                    constraint = videoNote.VideoNote;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeApp app)
                {
                    constraint = app.Photo;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeArticle article)
                {
                    constraint = article.Photo;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeChannelBoost channelBoost)
                {
                    constraint = channelBoost.Photo;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeChat chat)
                {
                    constraint = chat.Photo;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeSupergroupBoost supergroupBoost)
                {
                    constraint = supergroupBoost.Photo;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeUser user)
                {
                    constraint = user.Photo;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeVideoChat videoChat)
                {
                    constraint = videoChat.Photo;
                }
                else if (textMessage?.LinkPreview?.Type is LinkPreviewTypeWebApp webApp)
                {
                    constraint = webApp.Photo;
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
            else if (constraint is PaidMediaPhoto paidMediaPhoto)
            {
                constraint = paidMediaPhoto.Photo;
            }
            else if (constraint is PaidMediaVideo paidMediaVideo)
            {
                constraint = paidMediaVideo.Video;
            }
            else if (constraint is PaidMediaPreview paidMediaPreview)
            {
                width = paidMediaPreview.Width;
                height = paidMediaPreview.Height;
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
                var size = photo.Sizes.Count > 0 ? photo.Sizes[^1] : null;
                if (size != null)
                {
                    width = size.Width;
                    height = size.Height;
                }
            }
            else if (constraint is ChatPhoto chatPhoto)
            {
                var size = chatPhoto.Sizes.Count > 0 ? chatPhoto.Sizes[^1] : null;
                if (size != null)
                {
                    width = size.Width;
                    height = size.Height;
                }
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
                width = video.Width;
                height = video.Height;
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

            var rotate = RotationAngle
                is RotationAngle.Angle90
                or RotationAngle.Angle270;

            var cw = rotate ? height : width;
            var ch = rotate ? width : height;

            if (cw > availableWidth || ch > availableHeight || Constraint is Size || Stretch == Stretch.UniformToFill)
            {
                var ratioX = availableWidth / cw;
                var ratioY = availableHeight / ch;
                var ratio = Math.Min(ratioX, ratioY);

                cw *= ratio;
                ch *= ratio;
            }

            width = rotate ? ch : cw;
            height = rotate ? cw : ch;

            width = Math.Max(width, MinWidth);
            height = Math.Max(height, MinHeight);

            base.MeasureOverride(new Size(width, height));
            return new Size(width, height);
        }

        private bool _applyingRotation;
        private RotationAngle _appliedRotation;

        protected override Size ArrangeOverride(Size finalSize)
        {
            ApplyRotation();
            return base.ArrangeOverride(finalSize);
        }

        private void ApplyRotation()
        {
            if (_applyingRotation || _appliedRotation == RotationAngle)
            {
                return;
            }

            _applyingRotation = true;
            VisualUtilities.QueueCallbackForCompositionRendering(ApplyRotationImpl);
        }

        public event RoutedEventHandler RotationAngleChanged;

        private void ApplyRotationImpl()
        {
            _applyingRotation = false;

            if (_appliedRotation == RotationAngle)
            {
                return;
            }

            _appliedRotation = RotationAngle;
            RotationAngleChanged?.Invoke(this, new RoutedEventArgs());
        }
    }
}
