using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public class AnimatedTextBlock : Control
    {
        private TextBlock PrefixPart;
        private TextBlock SuffixPart;
        private TextBlock PrevPart;
        private TextBlock NextPart;

        public AnimatedTextBlock()
        {
            DefaultStyleKey = typeof(AnimatedTextBlock);
        }

        protected override void OnApplyTemplate()
        {
            ChangePartText(ref NextPart, nameof(NextPart), Text, true);
        }

        private void ChangePartText(ref TextBlock part, string name, string text, bool length = false)
        {
            if (part != null || text.Length > 0 || length)
            {
                if (part == null)
                {
                    part = GetTemplateChild(name) as TextBlock;
                    ElementCompositionPreview.SetIsTranslationEnabled(part, true);
                }

                part.Text = text;
            }
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(AnimatedTextBlock), new PropertyMetadata(string.Empty, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedTextBlock)d).OnTextChanged((string)e.NewValue, (string)e.OldValue);
        }

        #endregion

        #region TextStyle

        public Style TextStyle
        {
            get { return (Style)GetValue(TextStyleProperty); }
            set { SetValue(TextStyleProperty, value); }
        }

        public static readonly DependencyProperty TextStyleProperty =
            DependencyProperty.Register("TextStyle", typeof(Style), typeof(AnimatedTextBlock), new PropertyMetadata(null));

        #endregion

        private void OnTextChanged(string newValue, string oldValue)
        {
            if (NextPart == null)
            {
                return;
            }

            var j = 0;
            var k = 0;
            var jcontinue = true;
            var kcontinue = true;

            var length = Math.Min(oldValue.Length, newValue.Length);

            while (j < length - k && (jcontinue || kcontinue))
            {
                if (jcontinue && oldValue[j] == newValue[j])
                {
                    j++;
                }
                else
                {
                    jcontinue = false;
                }

                if (kcontinue && j < length - k && oldValue[oldValue.Length - k - 1] == newValue[newValue.Length - k - 1])
                {
                    k++;
                }
                else
                {
                    kcontinue = false;
                }
            }

            var prefixLength = j;
            var suffixLength = newValue.Length - k;

            var prevLength = oldValue.Length - k - j;
            var nextLength = newValue.Length - k - j;

            if (prevLength > 0 || nextLength > 0)
            {
                var prefix = newValue.Substring(0, prefixLength);
                var nextValue = newValue.Substring(j, nextLength);
                var prevValue = oldValue.Substring(j, prevLength);
                var suffix = newValue.Substring(suffixLength);

                // TODO: TextBlock seems to trim leading spaces SOME TIMES
                // We still rely not to have more than a space in between words.
                if (prefix.EndsWith(' '))
                {
                    prefix = prefix.Substring(0, prefix.Length - 1);
                    prevValue = $" {prevValue}";
                    nextValue = $" {nextValue}";
                }

                if (nextValue.EndsWith(' '))
                {
                    nextValue = nextValue.Substring(0, nextValue.Length - 1);
                    suffix = $" {suffix}";
                }
                else if (prevValue.EndsWith(' '))
                {
                    prevValue = prevValue.Substring(0, prevValue.Length - 1);
                    suffix = $" {suffix}";
                }
                //

                if (prefix.Length > 0 || suffix.Length > 0)
                {
                    SizeChanged += OnSizeChanged;
                }

                ChangePartText(ref PrefixPart, nameof(PrefixPart), prefix);
                ChangePartText(ref SuffixPart, nameof(SuffixPart), suffix);

                ChangePartText(ref PrevPart, nameof(PrevPart), prevValue, true);
                ChangePartText(ref NextPart, nameof(NextPart), nextValue);

                var prevVisual = ElementCompositionPreview.GetElementVisual(PrevPart);
                var nextVisual = ElementCompositionPreview.GetElementVisual(NextPart);

                var delta = (float)FontSize;
                var easing = prevVisual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0, 0), new Vector2(0, 1));

                var fadeOut = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
                fadeOut.InsertKeyFrame(0, 1);
                fadeOut.InsertKeyFrame(1, 0, easing);
                fadeOut.Duration = TimeSpan.FromMilliseconds(167);

                var fadeIn = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(0, 0);
                fadeIn.InsertKeyFrame(1, 1, easing);
                fadeIn.Duration = TimeSpan.FromMilliseconds(167);

                var slideOut = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
                slideOut.InsertKeyFrame(0, 0);
                slideOut.InsertKeyFrame(1, delta, easing);
                slideOut.Duration = TimeSpan.FromMilliseconds(167);

                var slideIn = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
                slideIn.InsertKeyFrame(0, -delta);
                slideIn.InsertKeyFrame(1, 0, easing);
                slideIn.Duration = TimeSpan.FromMilliseconds(167);

                prevVisual.StartAnimation("Opacity", fadeOut);
                nextVisual.StartAnimation("Opacity", fadeIn);
                prevVisual.StartAnimation("Translation.Y", slideOut);
                nextVisual.StartAnimation("Translation.Y", slideIn);
            }
            else
            {
                ChangePartText(ref PrefixPart, nameof(PrefixPart), string.Empty);
                ChangePartText(ref SuffixPart, nameof(SuffixPart), string.Empty);

                ChangePartText(ref PrevPart, nameof(PrevPart), string.Empty);
                ChangePartText(ref NextPart, nameof(NextPart), newValue);
            }

            InvalidateMeasure();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SizeChanged -= OnSizeChanged;

            var newSize = e.NewSize.ToVector2();
            var oldSize = e.PreviousSize.ToVector2();

            if (HorizontalAlignment == HorizontalAlignment.Left && SuffixPart != null)
            {
                var suffixVisual = ElementCompositionPreview.GetElementVisual(SuffixPart);

                var slide = suffixVisual.Compositor.CreateScalarKeyFrameAnimation();
                slide.InsertKeyFrame(0, oldSize.X - newSize.X);
                slide.InsertKeyFrame(1, 0);

                suffixVisual.StartAnimation("Translation.X", slide);
            }
            else if (HorizontalAlignment == HorizontalAlignment.Right && PrefixPart != null)
            {
                var prefixVisual = ElementCompositionPreview.GetElementVisual(PrefixPart);

                var slide = prefixVisual.Compositor.CreateScalarKeyFrameAnimation();
                slide.InsertKeyFrame(0, newSize.X - oldSize.X);
                slide.InsertKeyFrame(1, 0);

                prefixVisual.StartAnimation("Translation.X", slide);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            PrefixPart?.Measure(availableSize);
            SuffixPart?.Measure(availableSize);
            PrevPart?.Measure(availableSize);
            NextPart?.Measure(availableSize);

            var width = (PrefixPart?.DesiredSize.Width ?? 0)
                + (SuffixPart?.DesiredSize.Width ?? 0)
                + (NextPart?.DesiredSize.Width ?? 0);
            var height = Math.Max(PrefixPart?.DesiredSize.Height ?? 0,
                Math.Max(SuffixPart?.DesiredSize.Height ?? 0,
                NextPart?.DesiredSize.Height ?? 0));

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = 0d;

            if (PrefixPart != null)
            {
                PrefixPart.Arrange(new Rect(0, 0, PrefixPart.DesiredSize.Width, PrefixPart.DesiredSize.Height));
                x += PrefixPart.DesiredSize.Width;
            }

            if (NextPart != null)
            {
                NextPart.Arrange(new Rect(x, 0, NextPart.DesiredSize.Width, NextPart.DesiredSize.Height));

                if (HorizontalAlignment == HorizontalAlignment.Right)
                {
                    PrevPart?.Arrange(new Rect(x + NextPart.DesiredSize.Width - PrevPart.DesiredSize.Width, 0, PrevPart.DesiredSize.Width, PrevPart.DesiredSize.Height));
                }
                else
                {
                    PrevPart?.Arrange(new Rect(x, 0, PrevPart.DesiredSize.Width, PrevPart.DesiredSize.Height));
                }

                x += NextPart.DesiredSize.Width;
            }

            SuffixPart?.Arrange(new Rect(x, 0, SuffixPart.DesiredSize.Width, SuffixPart.DesiredSize.Height));

            return finalSize;
        }
    }

    public class AnimatedTextBlockPresenter : Panel
    {
        // This does nothing, it's only used to have custom Measure and Arrange in AnimatedTextBlock
    }
}
