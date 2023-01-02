//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Assets.Icons;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Chats
{
    public class ChatActionIndicator : FrameworkElement
    {
        // This should be held in memory, or animation will stop
        private CompositionPropertySet _props;

        private IAnimatedVisual _previous;
        private AnimationType _action;

        private enum AnimationType
        {
            None,
            Typing,
            Uploading,
            Playing,
            VideoRecording,
            VoiceRecording,
            Watching
        }

        public void UpdateAction(ChatAction action)
        {
            var type = GetAnimationType(action);
            if (type == _action)
            {
                return;
            }

            if (_previous != null)
            {
                _previous.Dispose();
                _previous = null;
            }

            if (_props != null)
            {
                _props.Dispose();
                _props = null;
            }

            var color = Fill?.Color ?? Colors.Black;
            var visual = GetVisual(type, Window.Current.Compositor, color, out _props);

            _action = type;
            _previous = visual;

            ElementCompositionPreview.SetElementChildVisual(this, visual?.RootVisual);
            InvalidateArrange();
        }

        #region Fill

        public SolidColorBrush Fill
        {
            get => (SolidColorBrush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register("Fill", typeof(SolidColorBrush), typeof(ChatActionIndicator), new PropertyMetadata(null));

        #endregion

        private AnimationType GetAnimationType(ChatAction action)
        {
            switch (action)
            {
                case ChatActionTyping:
                    return AnimationType.Typing;
                case ChatActionUploadingDocument:
                case ChatActionUploadingPhoto:
                case ChatActionUploadingVideo:
                case ChatActionUploadingVideoNote:
                case ChatActionUploadingVoiceNote:
                    return AnimationType.Uploading;
                case ChatActionStartPlayingGame:
                    return AnimationType.Playing;
                case ChatActionRecordingVideo:
                case ChatActionRecordingVideoNote:
                    return AnimationType.VideoRecording;
                case ChatActionRecordingVoiceNote:
                    return AnimationType.VoiceRecording;
                case ChatActionChoosingSticker:
                case ChatActionWatchingAnimations:
                    return AnimationType.Watching;
                default:
                    return AnimationType.None;
            }
        }

        private IAnimatedVisual GetVisual(AnimationType action, Compositor compositor, Color color, out CompositionPropertySet properties)
        {
            var source = GetVisual(action, compositor, color);
            if (source == null)
            {
                properties = null;
                return null;
            }

            var visual = source.TryCreateAnimatedVisual(compositor, out _);
            if (visual == null)
            {
                properties = null;
                return null;
            }

            var linearEasing = compositor.CreateLinearEasingFunction();
            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = visual.Duration;
            animation.InsertKeyFrame(1, 1, linearEasing);
            animation.IterationBehavior = AnimationIterationBehavior.Forever;

            properties = compositor.CreatePropertySet();
            properties.InsertScalar("Progress", 0);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 0.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);

            properties.StartAnimation("Progress", animation);

            return visual;
        }

        private IAnimatedVisualSource2 GetVisual(AnimationType action, Compositor compositor, Color color)
        {
            switch (action)
            {
                case AnimationType.Typing:
                    return new ActionTyping(color);
                case AnimationType.Uploading:
                    return new ActionFile(color);
                case AnimationType.Playing:
                    return new ActionGame(color);
                case AnimationType.VideoRecording:
                    return new ActionVideo(color);
                case AnimationType.VoiceRecording:
                    return new ActionVoice(color);
                case AnimationType.Watching:
                    return new ActionSticker(color);
            }

            return null;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var visual = _previous;
            if (visual == null)
            {
                // If we don't have a visual, we will show the fallback icon, so we need to do a traditional measure.
                return base.MeasureOverride(availableSize);
            }

            // Animated Icon scales using the Uniform strategy, meaning that it scales the horizonal and vertical
            // dimensions equally by the maximum amount that doesn't exceed the available size in either dimension.
            // If the available size is infinite in both dimensions then we don't scale the visual. Otherwise, we
            // calculate the scale factor by comparing the default visual size to the available size. This produces 2
            // scale factors, one for each dimension. We choose the smaller of the scale factors to not exceed the
            // available size in that dimension.
            var visualSize = visual.Size;
            if (visualSize != Vector2.Zero)
            {
                var widthScale = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width / visualSize.X;
                var heightScale = double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : availableSize.Height / visualSize.Y;
                if (double.IsInfinity(widthScale) && double.IsInfinity(heightScale))
                {
                    return visualSize.ToSize();
                }
                else if (double.IsInfinity(widthScale))
                {
                    return new Size(visualSize.X * heightScale, availableSize.Height);
                }
                else if (double.IsInfinity(heightScale))
                {
                    return new Size(availableSize.Width, visualSize.Y * widthScale);
                }
                else
                {
                    return heightScale > widthScale
                        ? new Size(availableSize.Width, visualSize.Y * widthScale)
                        : new Size(visualSize.X * heightScale, availableSize.Height);
                }
            }

            return visualSize.ToSize();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var visual = _previous;
            if (visual == null)
            {
                return base.ArrangeOverride(finalSize);
            }

            var visualSize = visual.Size;
            Vector2 Scale()
            {
                var scale = finalSize.ToVector2() / visualSize;
                if (scale.X < scale.Y)
                {
                    scale.Y = scale.X;
                }
                else
                {
                    scale.X = scale.Y;
                }
                return scale;
            };

            var scale = Scale();
            var arrangedSize = new Vector2(
                MathF.Min((float)finalSize.Width / scale.X, visualSize.X),
                MathF.Min((float)finalSize.Height / scale.Y, visualSize.Y));
            var offset = (finalSize.ToVector2() - (visualSize * scale)) / 2;
            var rootVisual = visual.RootVisual;
            rootVisual.Offset = new Vector3(offset, 0.0f);
            rootVisual.Size = arrangedSize;
            rootVisual.Scale = new Vector3(scale, 1.0f);
            return finalSize;
        }
    }
}
