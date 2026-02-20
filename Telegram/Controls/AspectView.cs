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
        public FixedSize(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    public struct MaximumSize
    {
        public MaximumSize(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    public partial class AspectView : Grid
    {
        public Size ActualConstraint { get; set; } = Size.Empty;

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

            switch (constraint)
            {
                case MessageViewModel viewModel:
                    constraint = viewModel.Content;
                    break;
                case Message message:
                    constraint = message.Content;
                    break;
                case ViewModels.Chats.ChartViewData:
                    width = 640;
                    height = 420;
                    break;
                case Size size:
                    width = size.Width;
                    height = size.Height;
                    break;
                case MaximumSize maximumSize:
                    width = maximumSize.Width;
                    height = maximumSize.Height;
                    break;
            }

            #region MessageContent

            switch (constraint)
            {
                case MessageAnimation animationMessage:
                    constraint = animationMessage.Animation;
                    break;
                case MessageInvoice invoiceMessage:
                    if (invoiceMessage.PaidMedia is PaidMediaPhoto invoicePaidMediaPhoto)
                    {
                        constraint = invoicePaidMediaPhoto.Photo;
                    }
                    else if (invoiceMessage.PaidMedia is PaidMediaVideo invoicePaidMediaVideo)
                    {
                        constraint = invoicePaidMediaVideo.Video;
                    }
                    else if (invoiceMessage.PaidMedia is PaidMediaPreview invoicePaidMediaPreview)
                    {
                        width = invoicePaidMediaPreview.Width;
                        height = invoicePaidMediaPreview.Height;
                    }
                    else
                    {
                        constraint = invoiceMessage.ProductInfo.Photo;
                    }
                    break;
                case MessageGame gameMessage:
                    if (gameMessage.Game.Animation != null)
                    {
                        constraint = gameMessage.Game.Animation;
                    }
                    else if (gameMessage.Game.Photo != null)
                    {
                        constraint = gameMessage.Game.Photo;
                    }
                    break;
                case MessageLocation locationMessage:
                    constraint = locationMessage.Location;
                    break;
                case MessagePhoto photoMessage:
                    constraint = photoMessage.Photo;
                    break;
                case MessageSticker stickerMessage:
                    constraint = stickerMessage.Sticker;
                    break;
                case MessageAnimatedEmoji animatedEmojiMessage:
                    if (animatedEmojiMessage.AnimatedEmoji.Sticker != null)
                    {
                        constraint = animatedEmojiMessage.AnimatedEmoji.Sticker;
                    }
                    else
                    {
                        width = animatedEmojiMessage.AnimatedEmoji.StickerWidth;
                        height = animatedEmojiMessage.AnimatedEmoji.StickerHeight;
                    }
                    break;
                case MessageText textMessage:
                    switch (textMessage?.LinkPreview?.Type)
                    {
                        case LinkPreviewTypeBackground:
                            width = 900;
                            height = 1600;
                            break;
                        case LinkPreviewTypeAnimation previewAnimation:
                            constraint = previewAnimation.Animation;
                            break;
                        case LinkPreviewTypeDocument previewDocument:
                            constraint = previewDocument.Document;
                            break;
                        case LinkPreviewTypeEmbeddedAnimationPlayer previewEmbeddedAnimationPlayer:
                            width = previewEmbeddedAnimationPlayer.Width;
                            height = previewEmbeddedAnimationPlayer.Height;
                            break;
                        case LinkPreviewTypeEmbeddedVideoPlayer previewEmbeddedVideoPlayer:
                            width = previewEmbeddedVideoPlayer.Width;
                            height = previewEmbeddedVideoPlayer.Height;
                            break;
                        case LinkPreviewTypePhoto previewPhoto:
                            constraint = previewPhoto.Photo;
                            break;
                        case LinkPreviewTypeSticker previewSticker:
                            constraint = previewSticker.Sticker;
                            break;
                        case LinkPreviewTypeVideo previewVideo:
                            constraint = previewVideo.Video;
                            break;
                        case LinkPreviewTypeVideoNote videoNote:
                            constraint = videoNote.VideoNote;
                            break;
                        case LinkPreviewTypeApp app:
                            constraint = app.Photo;
                            break;
                        case LinkPreviewTypeArticle article:
                            constraint = article.Photo;
                            break;
                        case LinkPreviewTypeChannelBoost channelBoost:
                            constraint = channelBoost.Photo;
                            break;
                        case LinkPreviewTypeChat chat:
                            constraint = chat.Photo;
                            break;
                        case LinkPreviewTypeSupergroupBoost supergroupBoost:
                            constraint = supergroupBoost.Photo;
                            break;
                        case LinkPreviewTypeUser user:
                            constraint = user.Photo;
                            break;
                        case LinkPreviewTypeVideoChat videoChat:
                            constraint = videoChat.Photo;
                            break;
                        case LinkPreviewTypeWebApp webApp:
                            constraint = webApp.Photo;
                            break;
                    }
                    break;
                case MessageVenue venueMessage:
                    constraint = venueMessage.Venue;
                    break;
                case MessageVideo videoMessage:
                    constraint = videoMessage.Video;
                    break;
                case MessageVideoNote videoNoteMessage:
                    constraint = videoNoteMessage.VideoNote;
                    break;
                case MessageChatChangePhoto chatChangePhoto:
                    constraint = chatChangePhoto.Photo;
                    break;
                case PaidMediaPhoto paidMediaPhoto:
                    constraint = paidMediaPhoto.Photo;
                    break;
                case PaidMediaVideo paidMediaVideo:
                    constraint = paidMediaVideo.Video;
                    break;
                case PaidMediaPreview paidMediaPreview:
                    width = paidMediaPreview.Width;
                    height = paidMediaPreview.Height;
                    break;
                case MessageAsyncStory asyncStory:
                    width = 720;
                    height = 1280;
                    break;
            }

            #endregion

            #region InlineQueryResult

            switch (constraint)
            {
                case InlineQueryResultAnimation animationResult:
                    constraint = animationResult.Animation;
                    break;
                case InlineQueryResultLocation locationResult:
                    constraint = locationResult.Location;
                    break;
                case InlineQueryResultPhoto photoResult:
                    constraint = photoResult.Photo;
                    break;
                case InlineQueryResultSticker stickerResult:
                    constraint = stickerResult.Sticker;
                    break;
                case InlineQueryResultVideo videoResult:
                    constraint = videoResult.Video;
                    break;
            }

            #endregion

            switch (constraint)
            {
                case Animation animation:
                    width = animation.Width;
                    height = animation.Height;
                    break;
                case Document document:
                    width = document.Thumbnail?.Width ?? width;
                    height = document.Thumbnail?.Height ?? height;
                    break;
                case Location location:
                    width = 320;
                    height = 200;
                    break;
                case Photo photo:
                    var size = photo.Sizes.Count > 0 ? photo.Sizes[^1] : null;
                    if (size != null)
                    {
                        width = size.Width;
                        height = size.Height;
                    }
                    break;
                case ChatPhoto chatPhoto:
                    var chatSize = chatPhoto.Sizes.Count > 0 ? chatPhoto.Sizes[^1] : null;
                    if (chatSize != null)
                    {
                        width = chatSize.Width;
                        height = chatSize.Height;
                    }
                    break;
                case Sticker sticker:
                    width = sticker.Width;
                    height = sticker.Height;
                    break;
                case Venue venue:
                    width = 320;
                    height = 200;
                    break;
                case Video video:
                    width = video.Width;
                    height = video.Height;
                    break;
                case VideoNote videoNote:
                    width = 224;
                    height = 224;
                    break;

                case PhotoSize photoSize:
                    width = photoSize.Width;
                    height = photoSize.Height;
                    break;

                case PageBlockMap map:
                    width = map.Width;
                    height = map.Height;
                    break;

                case Background wallpaper:
                    width = 900;
                    height = 1600;
                    break;
            }

            if (width == 0 && height == 0)
            {
                width = int.MaxValue;
                height = int.MaxValue;
            }

            ActualConstraint = new Size(width, height);

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
