//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using Telegram.Assets.Icons;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.UI;

namespace Telegram.Controls.Chats
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

            var color = Fill?.Color ?? Colors.Black;
            var visual = GetVisual(type, BootStrapper.Current.Compositor, color, out _props);

            _action = type;
            _previous = visual;

            if (visual?.RootVisual != null)
            {
                visual.RootVisual.Scale = new Vector3(0.1f, 0.1f, 1.0f);
            }

            ElementCompositionPreview.SetElementChildVisual(this, visual?.RootVisual);
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
            try
            {
                var source = GetVisual(action, compositor, color);
                if (source == null)
                {
                    properties = null;
                    return null;
                }

                // Line that needs to be try-catched
                // TODO: Should we generate try-catch inside TryCreateAnimatedVisual?
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
            catch
            {
                properties = null;
                return null;
            }
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
    }
}
