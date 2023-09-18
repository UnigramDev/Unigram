using System;
using System.Numerics;
using Telegram.Native.Composition;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    public class PremiumProgressBar : RangeBase
    {
        private AnimatedTextBlock ValueText;
        private Grid ValueRoot;
        private Grid Thumb;

        private Path NextArrow;
        private Path CoreArrow;
        private Path PrevArrow;

        private DirectRectangleClip _thumbClip;

        private Visual _valueRoot;
        private Visual _thumb;

        private HorizontalAlignment _arrowAlignment;
        private double _prevValue = 0;

        public PremiumProgressBar()
        {
            DefaultStyleKey = typeof(PremiumProgressBar);
        }

        protected override void OnApplyTemplate()
        {
            ValueText = GetTemplateChild(nameof(ValueText)) as AnimatedTextBlock;
            ValueRoot = GetTemplateChild(nameof(ValueRoot)) as Grid;
            Thumb = GetTemplateChild(nameof(Thumb)) as Grid;

            NextArrow = GetTemplateChild(nameof(NextArrow)) as Path;
            CoreArrow = GetTemplateChild(nameof(CoreArrow)) as Path;
            PrevArrow = GetTemplateChild(nameof(PrevArrow)) as Path;

            ElementCompositionPreview.SetIsTranslationEnabled(NextArrow, true);
            ElementCompositionPreview.SetIsTranslationEnabled(CoreArrow, true);
            ElementCompositionPreview.SetIsTranslationEnabled(PrevArrow, true);

            var next = ElementCompositionPreview.GetElementVisual(NextArrow);
            var core = ElementCompositionPreview.GetElementVisual(CoreArrow);
            var prev = ElementCompositionPreview.GetElementVisual(PrevArrow);

            next.Opacity =
                core.Opacity = 0;

            _valueRoot = ElementCompositionPreview.GetElementVisual(ValueRoot);
            _thumb = ElementCompositionPreview.GetElementVisual(Thumb);

            _thumbClip = CompositionDevice.CreateRectangleClip(Thumb.Children[0]);
            _thumbClip.Set(20, 20, 20, 0);

            ValueRoot.SizeChanged += OnSizeChanged;
            Thumb.SizeChanged += OnSizeChanged;

            ValueText.Text = Value.ToString("F0");

            base.OnApplyTemplate();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateClip();
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            base.OnValueChanged(oldValue, newValue);

            if (ValueText == null)
            {
                return;
            }

            ValueText.Text = newValue.ToString("F0");
            UpdateClip();

            if (_valueRoot.Clip is not InsetClip clip)
            {
                _valueRoot.Clip = clip = _valueRoot.Compositor.CreateInsetClip();
            }

            var next = ElementCompositionPreview.GetElementVisual(NextArrow);
            var core = ElementCompositionPreview.GetElementVisual(CoreArrow);
            var prev = ElementCompositionPreview.GetElementVisual(PrevArrow);

            var half = Thumb.ActualSize.X / 2;
            var duration = TimeSpan.FromSeconds(0.333 * 1);

            if (_prevValue == Minimum && false)
            {
                var animScale = _thumb.Compositor.CreateVector3KeyFrameAnimation();
                animScale.InsertKeyFrame(0, Vector3.Zero);
                animScale.InsertKeyFrame(1, Vector3.One);
                animScale.Duration = duration;

                //var animCenter = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                //animCenter.InsertKeyFrame(0, 0);
                //animCenter.InsertKeyFrame(1, Thumb.ActualSize.X / 2);
                //animCenter.Duration = duration;

                _thumb.StartAnimation("Scale", animScale);
                //_thumb.StartAnimation("CenterPoint.X", animCenter);
            }

            var animAngle = _thumb.Compositor.CreateSpringScalarAnimation();
            animAngle.InitialValue = _prevValue < Value ? -20 : 20;
            animAngle.FinalValue = 0;
            animAngle.Period = duration / 4;

            var animOffset = _thumb.Compositor.CreateScalarKeyFrameAnimation();
            animOffset.InsertKeyFrame(0, GetOffset(_prevValue, out float prevWidth, out _));
            animOffset.InsertKeyFrame(1, GetOffset(Value, out float nextWidth, out var alignment));
            animOffset.Duration = duration;

            var animClip = _thumb.Compositor.CreateScalarKeyFrameAnimation();
            animClip.InsertKeyFrame(0, ValueRoot.ActualSize.X - prevWidth);
            animClip.InsertKeyFrame(1, ValueRoot.ActualSize.X - nextWidth);
            animClip.Duration = duration / 2;

            _thumb.StartAnimation("Offset.X", animOffset);
            _thumb.StartAnimation("RotationAngleInDegrees", animAngle);
            clip.StartAnimation("RightInset", animClip);

            if ((_arrowAlignment == HorizontalAlignment.Left && alignment == HorizontalAlignment.Center) || (_arrowAlignment == HorizontalAlignment.Center && alignment == HorizontalAlignment.Left))
            {
                var show = alignment == HorizontalAlignment.Center;

                var animCore = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                animCore.InsertKeyFrame(show ? 0 : 1, -half + 2);
                animCore.InsertKeyFrame(show ? 1 : 0, 0);
                animCore.Duration = duration;

                var animPrev = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                animPrev.InsertKeyFrame(show ? 0 : 1, 0);
                animPrev.InsertKeyFrame(show ? 1 : 0, half - 2);
                animPrev.Duration = duration;

                var fadeCore = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                fadeCore.InsertKeyFrame(show ? 0 : 1, 0);
                fadeCore.InsertKeyFrame(show ? 1 : 0, 1);
                fadeCore.Duration = duration / 2;

                var fadePrev = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                fadePrev.InsertKeyFrame(show ? 0 : 1, 1);
                fadePrev.InsertKeyFrame(show ? 1 : 0, 0);
                fadePrev.Duration = duration;

                _thumbClip.AnimateBottomLeft(_thumb.Compositor, show ? 0 : 20, show ? 20 : 0, duration.TotalMilliseconds / 1000 + 0.1);

                core.StartAnimation("Translation.X", animCore);
                prev.StartAnimation("Translation.X", animPrev);
                core.StartAnimation("Opacity", fadeCore);
                prev.StartAnimation("Opacity", fadePrev);
                next.Opacity = 0;
            }
            else if ((_arrowAlignment == HorizontalAlignment.Right && alignment == HorizontalAlignment.Center) || (_arrowAlignment == HorizontalAlignment.Center && alignment == HorizontalAlignment.Right))
            {
                var show = alignment == HorizontalAlignment.Center;

                var animCore = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                animCore.InsertKeyFrame(show ? 0 : 1, half - 2);
                animCore.InsertKeyFrame(show ? 1 : 0, 0);
                animCore.Duration = duration;

                var animNext = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                animNext.InsertKeyFrame(show ? 0 : 1, 0);
                animNext.InsertKeyFrame(show ? 1 : 0, -half + 2);
                animNext.Duration = duration;

                var fadeCore = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                fadeCore.InsertKeyFrame(show ? 0 : 1, 0);
                fadeCore.InsertKeyFrame(show ? 1 : 0, 1);
                fadeCore.Duration = duration / 2;

                var fadeNext = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                fadeNext.InsertKeyFrame(show ? 0 : 1, 1);
                fadeNext.InsertKeyFrame(show ? 1 : 0, 0);
                fadeNext.Duration = duration;

                _thumbClip.AnimateBottomRight(_thumb.Compositor, show ? 0 : 20, show ? 20 : 0, duration.TotalMilliseconds / 1000 + 0.1);

                core.StartAnimation("Translation.X", animCore);
                next.StartAnimation("Translation.X", animNext);
                core.StartAnimation("Opacity", fadeCore);
                next.StartAnimation("Opacity", fadeNext);
                prev.Opacity = 0;
            }
            else if ((_arrowAlignment == HorizontalAlignment.Right && alignment == HorizontalAlignment.Left) || (_arrowAlignment == HorizontalAlignment.Left && alignment == HorizontalAlignment.Right))
            {
                var show = alignment == HorizontalAlignment.Left;

                var animCore = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                animCore.InsertKeyFrame(show ? 0 : 1, half - 2);
                animCore.InsertKeyFrame(show ? 1 : 0, -half + 2);
                animCore.Duration = duration;

                var animPrev = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                animPrev.InsertKeyFrame(show ? 0 : 1, half * 2 - 2);
                animPrev.InsertKeyFrame(show ? 1 : 0, 0);
                animPrev.Duration = duration;

                var animNext = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                animNext.InsertKeyFrame(show ? 0 : 1, 0);
                animNext.InsertKeyFrame(show ? 1 : 0, -half * 2 + 2);
                animNext.Duration = duration;

                var fadeCore = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                fadeCore.InsertKeyFrame(0.0f, 0);
                fadeCore.InsertKeyFrame(0.5f, 1);
                fadeCore.InsertKeyFrame(1.0f, 0);
                fadeCore.Duration = duration / 2;

                var fadePrev = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                fadePrev.InsertKeyFrame(show ? 0 : 1, 0);
                fadePrev.InsertKeyFrame(show ? 1 : 0, 1);
                fadePrev.Duration = duration;

                var fadeNext = _thumb.Compositor.CreateScalarKeyFrameAnimation();
                fadeNext.InsertKeyFrame(show ? 0 : 1, 1);
                fadeNext.InsertKeyFrame(show ? 1 : 0, 0);
                fadeNext.Duration = duration;

                _thumbClip.AnimateBottomLeft(_thumb.Compositor, show ? 20 : 0, show ? 0 : 20, duration.TotalMilliseconds / 1000 + 0.1);
                _thumbClip.AnimateBottomRight(_thumb.Compositor, show ? 0 : 20, show ? 20 : 0, duration.TotalMilliseconds / 1000 + 0.1);

                core.StartAnimation("Translation.X", animCore);
                prev.StartAnimation("Translation.X", animPrev);
                next.StartAnimation("Translation.X", animNext);
                core.StartAnimation("Opacity", fadeCore);
                prev.StartAnimation("Opacity", fadePrev);
                next.StartAnimation("Opacity", fadeNext);
            }

            _arrowAlignment = alignment;
            _prevValue = Value;
        }

        private int _prevvv = 0;

        private float GetOffset(double value, out float width, out HorizontalAlignment alignment)
        {
            width = ActualSize.X * (float)((value - Minimum) / (Maximum - Minimum));
            var center = Thumb.ActualSize.X / 2;

            if (width < center)
            {
                alignment = HorizontalAlignment.Left;
                return 0;
                //_thumb.Offset = new Vector3(width, 0, 0);
            }
            else if (width > ActualWidth - center)
            {
                alignment = HorizontalAlignment.Right;
                return ActualSize.X - center * 2;
                //_thumb.Offset = new Vector3(width - center * 2, 0, 0);
            }

            alignment = HorizontalAlignment.Center;
            return width - center;
        }

        private void UpdateClip()
        {
            if (ValueRoot == null || Thumb == null)
            {
                return;
            }

            _thumbClip.SetInset(0, 0, (float)Math.Truncate(Thumb.ActualWidth), 40);

            var value = (float)((Value - Minimum) / (Maximum - Minimum));
            var width = ActualSize.X * (float.IsNaN(value) ? 0 : value);
            var center = Thumb.ActualSize.X / 2;

            if (width < center)
            {
                _thumb.Offset = new Vector3(0, 0, 0);
                //_thumb.Offset = new Vector3(width, 0, 0);
            }
            else if (width > ActualWidth - center)
            {
                _thumb.Offset = new Vector3(ActualSize.X - center * 2, 0, 0);
                //_thumb.Offset = new Vector3(width - center * 2, 0, 0);
            }
            else
            {
                _thumb.Offset = new Vector3(width - center, 0, 0);
            }

            if (_valueRoot.Clip is not InsetClip clip)
            {
                _valueRoot.Clip = clip = _valueRoot.Compositor.CreateInsetClip();
            }

            clip.RightInset = ValueRoot.ActualSize.X - width;

            //if (alignment == _arrowAlignment)
            //{
            //    return;
            //}

            _thumb.Clip ??= _thumb.Compositor.CreateInsetClip();
            _thumb.CenterPoint = new Vector3(Thumb.ActualSize.X / 2, Thumb.ActualSize.Y, 0);
        }

        #region MinimumText

        public string MinimumText
        {
            get { return (string)GetValue(MinimumTextProperty); }
            set { SetValue(MinimumTextProperty, value); }
        }

        public static readonly DependencyProperty MinimumTextProperty =
            DependencyProperty.Register("MinimumText", typeof(string), typeof(PremiumProgressBar), new PropertyMetadata(string.Empty));

        #endregion

        #region MaximumText

        public string MaximumText
        {
            get { return (string)GetValue(MaximumTextProperty); }
            set { SetValue(MaximumTextProperty, value); }
        }

        public static readonly DependencyProperty MaximumTextProperty =
            DependencyProperty.Register("MaximumText", typeof(string), typeof(PremiumProgressBar), new PropertyMetadata(string.Empty));

        #endregion

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(PremiumProgressBar), new PropertyMetadata(string.Empty));

        #endregion
    }
}
