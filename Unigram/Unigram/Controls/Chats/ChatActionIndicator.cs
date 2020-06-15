using Microsoft.Toolkit.Uwp.UI.Animations.Expressions;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using EF = Microsoft.Toolkit.Uwp.UI.Animations.Expressions.ExpressionFunctions;

namespace Unigram.Controls.Chats
{
    public class ChatActionIndicator : FrameworkElement
    {
        private Visual _previous;
        private AnimationType _action;

        private enum AnimationType
        {
            None,
            Typing,
            Uploading,
            Playing,
            VideoRecording,
            VoiceRecording
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

            if (!ApiInfo.CanUseDirectComposition)
            {
                return;
            }

            var width = 18f;
            var height = 8f;
            var color = Fill?.Color ?? Colors.Black;

            var visual = GetVisual(type, Window.Current.Compositor, width, height, color);
            //if (visual != null)
            {
                _action = type;
                _previous = visual;

                ElementCompositionPreview.SetElementChildVisual(this, visual);
                InvalidateMeasure();
            }
        }

        #region Fill

        public SolidColorBrush Fill
        {
            get { return (SolidColorBrush)GetValue(FillProperty); }
            set { SetValue(FillProperty, value); }
        }

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register("Fill", typeof(SolidColorBrush), typeof(ChatActionIndicator), new PropertyMetadata(null));

        #endregion

        private AnimationType GetAnimationType(ChatAction action)
        {
            switch (action)
            {
                case ChatActionTyping typing:
                    return AnimationType.Typing;
                case ChatActionUploadingDocument document:
                case ChatActionUploadingPhoto photo:
                case ChatActionUploadingVideo video:
                case ChatActionUploadingVideoNote videoNote:
                case ChatActionUploadingVoiceNote voiceNote:
                    return AnimationType.Uploading;
                case ChatActionStartPlayingGame game:
                    return AnimationType.Playing;
                case ChatActionRecordingVideo recordingVideo:
                case ChatActionRecordingVideoNote recordingVideoNote:
                    return AnimationType.VideoRecording;
                case ChatActionRecordingVoiceNote recordingVoiceNote:
                    return AnimationType.VoiceRecording;
                default:
                    return AnimationType.None;
            }
        }

        private Visual GetVisual(AnimationType action, Compositor compositor, float width, float height, Color color)
        {
            switch (action)
            {
                case AnimationType.Typing:
                    return GetTyping(compositor, width, height, color);
                case AnimationType.Uploading:
                    return GetUploading(compositor, width, height, color);
                case AnimationType.Playing:
                    return GetPlaying(compositor, 24, height, color);
                case AnimationType.VideoRecording:
                    return GetVideoRecording(compositor, 8, height, color);
                case AnimationType.VoiceRecording:
                    return GetVoiceRecording(compositor, width, height, color);
            }

            return null;
        }
        private Visual GetTyping(Compositor compositor, float width, float height, Color color)
        {
            float radius = 1.00f;

            // Begin dot1
            var dot1 = compositor.CreateEllipseGeometry();
            dot1.Radius = new Vector2(radius, radius);

            var spriteDot1 = compositor.CreateSpriteShape(dot1);
            var brushDot1 = spriteDot1.FillBrush = compositor.CreateColorBrush(color);
            spriteDot1.Offset = new Vector2(3, 3.5f);

            // Begin dot2
            var dot2 = compositor.CreateEllipseGeometry();
            dot2.Radius = new Vector2(radius, radius);

            var spriteDot2 = compositor.CreateSpriteShape(dot2);
            var brushDot2 = spriteDot2.FillBrush = compositor.CreateColorBrush(color);
            spriteDot2.Offset = new Vector2(8, 3.5f);

            // Begin dot2
            var dot3 = compositor.CreateEllipseGeometry();
            dot3.Radius = new Vector2(radius, radius);

            var spriteDot3 = compositor.CreateSpriteShape(dot3);
            var brushDot3 = spriteDot3.FillBrush = compositor.CreateColorBrush(color);
            spriteDot3.Offset = new Vector2(13, 3.5f);

            // Begin shape
            var shape = compositor.CreateShapeVisual();
            shape.Shapes.Add(spriteDot1);
            shape.Shapes.Add(spriteDot2);
            shape.Shapes.Add(spriteDot3);
            shape.Size = new Vector2(24, 16);
            shape.Scale = new Vector3(2, 2, 0);

            // Begin animation
            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0, 0), new Vector2(0.58f, 1));
            var radiusDot1 = compositor.CreateVector2KeyFrameAnimation();
            radiusDot1.InsertKeyFrame(0.0f, new Vector2(1.00f, 1.00f), easing);
            radiusDot1.InsertKeyFrame(0.4f, new Vector2(1.80f, 1.80f), easing);
            radiusDot1.InsertKeyFrame(0.8f, new Vector2(1.00f, 1.00f), easing);
            radiusDot1.InsertKeyFrame(1.0f, new Vector2(1.00f, 1.00f), easing);
            radiusDot1.Duration = TimeSpan.FromMilliseconds(800);
            radiusDot1.IterationBehavior = AnimationIterationBehavior.Forever;

            var radiusDot2 = compositor.CreateVector2KeyFrameAnimation();
            radiusDot2.InsertKeyFrame(0.0f, new Vector2(1.00f, 1.00f), easing);
            radiusDot2.InsertKeyFrame(0.4f, new Vector2(1.80f, 1.80f), easing);
            radiusDot2.InsertKeyFrame(0.8f, new Vector2(1.00f, 1.00f), easing);
            radiusDot2.InsertKeyFrame(1.0f, new Vector2(1.00f, 1.00f), easing);
            radiusDot2.Duration = TimeSpan.FromMilliseconds(800);
            radiusDot2.DelayTime = TimeSpan.FromMilliseconds(150);
            radiusDot2.IterationBehavior = AnimationIterationBehavior.Forever;

            var radiusDot3 = compositor.CreateVector2KeyFrameAnimation();
            radiusDot3.InsertKeyFrame(0.0f, new Vector2(1.00f, 1.00f), easing);
            radiusDot3.InsertKeyFrame(0.4f, new Vector2(1.80f, 1.80f), easing);
            radiusDot3.InsertKeyFrame(0.8f, new Vector2(1.00f, 1.00f), easing);
            radiusDot3.InsertKeyFrame(1.0f, new Vector2(1.00f, 1.00f), easing);
            radiusDot3.Duration = TimeSpan.FromMilliseconds(800);
            radiusDot3.DelayTime = TimeSpan.FromMilliseconds(300);
            radiusDot3.IterationBehavior = AnimationIterationBehavior.Forever;

            dot1.StartAnimation("Radius", radiusDot1);
            dot2.StartAnimation("Radius", radiusDot2);
            dot3.StartAnimation("Radius", radiusDot3);

            return shape;
        }

        private Visual GetVoiceRecording(Compositor compositor, float width, float height, Color color)
        {
            float delta = 3.0f;
            float x = 3.0f;
            float y = height / 2.0f;

            // Begin dot1
            var dot1 = compositor.CreateEllipseGeometry();
            dot1.TrimStart = 0.5f - 0.05f;
            dot1.TrimEnd = 0.5f + 0.05f;

            var spriteDot1 = compositor.CreateSpriteShape(dot1);
            var brushDot1 = spriteDot1.StrokeBrush = compositor.CreateColorBrush(color);
            spriteDot1.StrokeThickness = 1.5f;
            spriteDot1.Offset = new Vector2(x, y);
            spriteDot1.StrokeStartCap = CompositionStrokeCap.Round;
            spriteDot1.StrokeEndCap = CompositionStrokeCap.Round;
            spriteDot1.RotationAngleInDegrees = -90;

            // Begin dot2
            var dot2 = compositor.CreateEllipseGeometry();
            dot2.TrimStart = 0.5f - 0.05f;
            dot2.TrimEnd = 0.5f + 0.05f;

            var spriteDot2 = compositor.CreateSpriteShape(dot2);
            var brushDot2 = spriteDot2.StrokeBrush = compositor.CreateColorBrush(color);
            spriteDot2.StrokeThickness = 1.5f;
            spriteDot2.Offset = new Vector2(x, y);
            spriteDot2.StrokeStartCap = CompositionStrokeCap.Round;
            spriteDot2.StrokeEndCap = CompositionStrokeCap.Round;
            spriteDot2.RotationAngleInDegrees = -90;

            // Begin dot2
            var dot3 = compositor.CreateEllipseGeometry();
            dot3.TrimStart = 0.5f - 0.05f;
            dot3.TrimEnd = 0.5f + 0.05f;

            var spriteDot3 = compositor.CreateSpriteShape(dot3);
            var brushDot3 = spriteDot3.StrokeBrush = compositor.CreateColorBrush(color);
            spriteDot3.StrokeThickness = 1.5f;
            spriteDot3.Offset = new Vector2(x, y);
            spriteDot3.StrokeStartCap = CompositionStrokeCap.Round;
            spriteDot3.StrokeEndCap = CompositionStrokeCap.Round;
            spriteDot3.RotationAngleInDegrees = -90;

            // Begin shape
            var shape = compositor.CreateShapeVisual();
            shape.Shapes.Add(spriteDot1);
            shape.Shapes.Add(spriteDot2);
            shape.Shapes.Add(spriteDot3);
            shape.Size = new Vector2(width, height);
            shape.Scale = new Vector3(2, 2, 0);

            // Begin animation
            var props = compositor.CreatePropertySet();
            props.InsertScalar("animationValue", 0.0f);

            var reference = props.GetReference();
            var animationValue = reference.GetScalarProperty("animationValue");

            var easing = compositor.CreateLinearEasingFunction();
            var animationValueImpl = compositor.CreateScalarKeyFrameAnimation();
            animationValueImpl.InsertKeyFrame(0, 0, easing);
            animationValueImpl.InsertKeyFrame(1, 1, easing);
            animationValueImpl.Duration = TimeSpan.FromMilliseconds(700);
            animationValueImpl.IterationBehavior = AnimationIterationBehavior.Forever;

            props.StartAnimation("animationValue", animationValueImpl);

            ScalarNode radiusz = animationValue * delta;
            ExpressionNode radiusDot1 = EF.Vector2(radiusz, radiusz);
            ExpressionNode radiusDot2 = EF.Vector2(radiusz + delta, radiusz + delta);
            ExpressionNode radiusDot3 = EF.Vector2(radiusz + delta * 2, radiusz + delta * 2);

            dot1.StartAnimation("Radius", radiusDot1);
            dot2.StartAnimation("Radius", radiusDot2);
            dot3.StartAnimation("Radius", radiusDot3);

            ScalarNode alpha;

            alpha = 1.0f - EF.Pow(EF.Cos(radiusz / (3.0f * delta) * MathF.PI), 10);
            ExpressionNode colorDot1 = EF.ColorRgb(alpha * 255, color.R, color.G, color.B);
            alpha = 1.0f - EF.Pow(EF.Cos((radiusz + delta) / (3.0f * delta) * MathF.PI), 10);
            ExpressionNode colorDot2 = EF.ColorRgb(alpha * 255, color.R, color.G, color.B);
            alpha = 1.0f - EF.Pow(EF.Cos((radiusz + delta * 2) / (3.0f * delta) * MathF.PI), 10);
            ExpressionNode colorDot3 = EF.ColorRgb(alpha * 255, color.R, color.G, color.B);

            brushDot1.StartAnimation("Color", colorDot1);
            brushDot2.StartAnimation("Color", colorDot2);
            brushDot3.StartAnimation("Color", colorDot3);

            return shape;
        }

        private Visual GetVideoRecording(Compositor compositor, float width, float height, Color color)
        {
            // Begin ellipse
            var ellipse = compositor.CreateEllipseGeometry();

            var ellipseSprite = compositor.CreateSpriteShape(ellipse);
            var ellipseBrush = ellipseSprite.FillBrush = compositor.CreateColorBrush(color);
            ellipseSprite.Offset = new Vector2(width / 2, height / 2);

            // Begin shape
            var shape = compositor.CreateShapeVisual();
            shape.Shapes.Add(ellipseSprite);
            shape.Size = new Vector2(width, height);
            shape.Scale = new Vector3(2, 2, 0);

            // Begin animation
            var props = compositor.CreatePropertySet();
            props.InsertScalar("animationValue", 0.0f);

            var reference = props.GetReference();
            var animationValue = reference.GetScalarProperty("animationValue");

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.42f, 0), new Vector2(0.58f, 1));
            var animationValueImpl = compositor.CreateScalarKeyFrameAnimation();
            animationValueImpl.InsertKeyFrame(0, 0, easing);
            animationValueImpl.InsertKeyFrame(1, 1, easing);
            animationValueImpl.Duration = TimeSpan.FromMilliseconds(900);
            animationValueImpl.IterationBehavior = AnimationIterationBehavior.Forever;

            props.StartAnimation("animationValue", animationValueImpl);

            ScalarNode animValue = EF.Conditional(animationValue < 0.5f, animationValue / 0.5f, (1 - animationValue) / 0.5f);

            ScalarNode alpha = 1.0f - animValue * 0.6f;
            ScalarNode radius = 3.5f - animValue * 0.66f;

            ExpressionNode colorEllipse = EF.ColorRgb(alpha * 255, color.R, color.G, color.B);
            ExpressionNode radiusEllipse = EF.Vector2(radius, radius);

            ellipseBrush.StartAnimation("Color", colorEllipse);
            ellipse.StartAnimation("Radius", radiusEllipse);

            return shape;
        }

        private Visual GetUploading(Compositor compositor, float width, float height, Color color)
        {
            float progressWidth = 26.0f / 2.0f;
            float progressHeight = 8.0f / 2.0f;

            float leftPadding = 0.0f;
            float topPadding = height / 2.0f - progressHeight / 2.0f;

            float round = 1.25f;

            // Begin background
            var background = compositor.CreateRoundedRectangleGeometry();
            background.Offset = new Vector2(leftPadding, topPadding);
            background.Size = new Vector2(progressWidth, progressHeight);
            background.CornerRadius = new Vector2(round);

            var spriteBackground = compositor.CreateSpriteShape(background);
            spriteBackground.FillBrush = compositor.CreateColorBrush(color);

            // Begin overlay
            var overlay = compositor.CreateRoundedRectangleGeometry();
            overlay.Offset = new Vector2(leftPadding, topPadding);
            overlay.Size = new Vector2(progressWidth, progressHeight);
            overlay.CornerRadius = new Vector2(round);

            var spriteOverlay = compositor.CreateSpriteShape(overlay);
            spriteOverlay.FillBrush = compositor.CreateColorBrush(Color.FromArgb(75, color.R, color.G, color.B));

            // Begin bar
            var bar = compositor.CreateRoundedRectangleGeometry();
            bar.Offset = new Vector2(leftPadding - progressWidth + 0, topPadding);
            bar.Size = new Vector2(progressWidth, progressHeight);
            bar.CornerRadius = new Vector2(round);

            var spriteBar = compositor.CreateSpriteShape(bar);
            spriteBar.FillBrush = compositor.CreateColorBrush(color);

            var shape = compositor.CreateShapeVisual();
            //shape.Shapes.Add(spriteBackground);
            shape.Shapes.Add(spriteOverlay);
            shape.Shapes.Add(spriteBar);
            shape.Size = new Vector2(width, height);
            shape.Scale = new Vector3(2, 2, 0);
            shape.Clip = compositor.CreateGeometricClip(background);

            // Begin animation
            var props = compositor.CreatePropertySet();
            props.InsertScalar("animationValue", 0.0f);

            var reference = props.GetReference();
            var animationValue = reference.GetScalarProperty("animationValue");

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.42f, 0), new Vector2(0.58f, 1));
            var animationValueImpl = compositor.CreateScalarKeyFrameAnimation();
            animationValueImpl.InsertKeyFrame(1, 0, easing);
            animationValueImpl.InsertKeyFrame(1, 1, easing);
            animationValueImpl.Duration = TimeSpan.FromMilliseconds(1750);
            animationValueImpl.IterationBehavior = AnimationIterationBehavior.Forever;

            props.StartAnimation("animationValue", animationValueImpl);

            ScalarNode progress = (animationValue * (progressWidth * 2.0f));
            ExpressionNode moveBar = EF.Vector2(leftPadding - progressWidth + progress, topPadding);

            bar.StartAnimation("Offset", moveBar);

            return shape;
        }

        private Visual GetPlaying(Compositor compositor, float width, float height, Color color)
        {
            float distance = 4.0f;
            float x = (width - distance * 2) / 2.0f;
            float y = height / 2.0f + 1;
            float radius = 1.0f;
            float mouthRadius = 3.5f;

            // Begin dot1
            var dot1 = compositor.CreateEllipseGeometry();
            dot1.Radius = new Vector2(radius, radius);

            var spriteDot1 = compositor.CreateSpriteShape(dot1);
            spriteDot1.FillBrush = compositor.CreateColorBrush(color);

            // Begin dot2
            var dot2 = compositor.CreateEllipseGeometry();
            dot2.Radius = new Vector2(radius, radius);

            var spriteDot2 = compositor.CreateSpriteShape(dot2);
            spriteDot2.FillBrush = compositor.CreateColorBrush(color);

            // Begin dot2
            var dot3 = compositor.CreateEllipseGeometry();
            dot3.Radius = new Vector2(radius, radius);

            var spriteDot3 = compositor.CreateSpriteShape(dot3);
            var brushDot3 = spriteDot3.FillBrush = compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));

            // Begin mouth
            var mouth = compositor.CreateEllipseGeometry();
            mouth.Radius = new Vector2(mouthRadius / 2, mouthRadius / 2);

            var spriteMouth = compositor.CreateSpriteShape(mouth);
            spriteMouth.StrokeThickness = mouthRadius;
            spriteMouth.StrokeBrush = compositor.CreateColorBrush(color);
            spriteMouth.RotationAngleInDegrees = 90;
            spriteMouth.Offset = new Vector2(mouthRadius, y - 1);

            // Begin shape
            var shape = compositor.CreateShapeVisual();
            shape.Shapes.Add(spriteDot1);
            shape.Shapes.Add(spriteDot2);
            shape.Shapes.Add(spriteDot3);
            shape.Shapes.Add(spriteMouth);
            shape.Size = new Vector2(width, height);
            shape.Scale = new Vector3(2, 2, 0);

            // Begin animation
            var props = compositor.CreatePropertySet();
            props.InsertScalar("animationValue", 0.0f);
            props.InsertScalar("dotsProgress", 0.0f);
            props.InsertScalar("dotsX", 0.0f);
            props.InsertScalar("bite", 0.0f);

            var reference = props.GetReference();
            var animationValue = reference.GetScalarProperty("animationValue");
            var dotsProgress = reference.GetScalarProperty("dotsProgress");
            var dotsX = reference.GetScalarProperty("dotsX");
            var bite = reference.GetScalarProperty("bite");

            var easing = compositor.CreateLinearEasingFunction();
            var animationValueImpl = compositor.CreateScalarKeyFrameAnimation();
            animationValueImpl.InsertKeyFrame(0, 0, easing);
            animationValueImpl.InsertKeyFrame(1, 1, easing);
            animationValueImpl.Duration = TimeSpan.FromMilliseconds(700);
            animationValueImpl.IterationBehavior = AnimationIterationBehavior.Forever;

            var biteImpl = compositor.CreateScalarKeyFrameAnimation();
            biteImpl.InsertKeyFrame(0.00f, 0, easing);
            biteImpl.InsertKeyFrame(0.25f, 1, easing);
            biteImpl.InsertKeyFrame(0.50f, 0, easing);
            biteImpl.InsertKeyFrame(0.75f, 1, easing);
            biteImpl.InsertKeyFrame(1.00f, 0, easing);
            biteImpl.Duration = TimeSpan.FromMilliseconds(700);
            biteImpl.IterationBehavior = AnimationIterationBehavior.Forever;

            props.StartAnimation("animationValue", animationValueImpl);
            props.StartAnimation("bite", biteImpl);

            ExpressionNode animationDotsProgress = (EF.Ceil(animationValue * 100) % 50) / 50;
            ExpressionNode animationDotsX = 1.5f + x - distance * dotsProgress;

            props.StartAnimation("dotsProgress", animationDotsProgress);
            props.StartAnimation("dotsX", animationDotsX);

            ExpressionNode moveDot1 = EF.Vector2(dotsX - radius, y - radius);
            ExpressionNode moveDot2 = EF.Vector2(dotsX - radius + distance, y - radius);
            ExpressionNode moveDot3 = EF.Vector2(dotsX - radius + distance * 2, y - radius);
            ExpressionNode colorDot3 = EF.ColorRgb(dotsProgress * 255, color.R, color.G, color.B);

            spriteDot1.StartAnimation("Offset", moveDot1);
            spriteDot2.StartAnimation("Offset", moveDot2);
            spriteDot3.StartAnimation("Offset", moveDot3);
            brushDot3.StartAnimation("Color", colorDot3);

            ExpressionNode start = bite * 0.125f;
            ExpressionNode end = 1 - bite * 0.125f;

            mouth.StartAnimation("TrimStart", start);
            mouth.StartAnimation("TrimEnd", end);

            return shape;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            switch (_action)
            {
                case AnimationType.Typing:
                    return new Size(36, 16);
                case AnimationType.Uploading:
                    return new Size(36, 16);
                case AnimationType.Playing:
                    return new Size(40, 16);
                case AnimationType.VideoRecording:
                    return new Size(22, 16);
                case AnimationType.VoiceRecording:
                    return new Size(34, 16);
            }

            return new Size(0, 16);
        }
    }
}
