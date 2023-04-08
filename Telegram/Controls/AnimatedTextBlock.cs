using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
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

        #region TextAlignment

        public TextAlignment TextAlignment
        {
            get { return (TextAlignment)GetValue(TextAlignmentProperty); }
            set { SetValue(TextAlignmentProperty, value); }
        }

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(AnimatedTextBlock), new PropertyMetadata(TextAlignment.Left));

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

        #region

        public TextWrapping TextWrapping
        {
            get { return (TextWrapping)GetValue(TextWrappingProperty); }
            set { SetValue(TextWrappingProperty, value); }
        }

        public static readonly DependencyProperty TextWrappingProperty =
            DependencyProperty.Register("TextWrapping", typeof(TextWrapping), typeof(AnimatedTextBlock), new PropertyMetadata(TextWrapping.Wrap));

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

            // TODO: same should be done for j too
            // What this does is to play the animation from the next space instead of right away
            // Example use case is animating between "Select All" and "Deselect All"
            if (TextWrapping == TextWrapping.WrapWholeWords && k > 0)
            {
                var next = newValue.IndexOf(' ', newValue.Length - k);
                if (next >= newValue.Length - k)
                {
                    k = newValue.Length - next;
                }
            }

            var prefixLength = j;
            var suffixLength = newValue.Length - k;

            var prevLength = oldValue.Length - k - j;
            var nextLength = newValue.Length - k - j;

            if (prevLength > 0 || nextLength > 0)
            {
                var prefix = prefixLength > 0 ? newValue.Substring(0, prefixLength) : string.Empty;
                var nextValue = nextLength > 0 ? newValue.Substring(j, nextLength) : string.Empty;
                var prevValue = prevLength > 0 ? oldValue.Substring(j, prevLength) : string.Empty;
                var suffix = k > 0 ? newValue.Substring(suffixLength) : string.Empty;

                if (prefix.Length > 0 || suffix.Length > 0)
                {
                    SizeChanged -= OnSizeChanged;
                    SizeChanged += OnSizeChanged;
                }

                prefix = prefix.Replace(" ", " \u200B");
                nextValue = nextValue.Replace(" ", " \u200B");
                prevValue = prevValue.Replace(" ", " \u200B");
                suffix = suffix.Replace(" ", " \u200B");

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

            if (TextAlignment == TextAlignment.Left && SuffixPart != null)
            {
                var suffixVisual = ElementCompositionPreview.GetElementVisual(SuffixPart);

                var slide = suffixVisual.Compositor.CreateScalarKeyFrameAnimation();
                slide.InsertKeyFrame(0, oldSize.X - newSize.X);
                slide.InsertKeyFrame(1, 0);

                suffixVisual.StartAnimation("Translation.X", slide);
            }
            else if (TextAlignment == TextAlignment.Right && PrefixPart != null)
            {
                var prefixVisual = ElementCompositionPreview.GetElementVisual(PrefixPart);

                var slide = prefixVisual.Compositor.CreateScalarKeyFrameAnimation();
                slide.InsertKeyFrame(0, newSize.X - oldSize.X);
                slide.InsertKeyFrame(1, 0);

                prefixVisual.StartAnimation("Translation.X", slide);
            }
        }

        private double _prefixRight;
        private double _nextRight;

        protected override Size MeasureOverride(Size availableSize)
        {
            PrefixPart?.Measure(availableSize);
            SuffixPart?.Measure(availableSize);
            PrevPart?.Measure(availableSize);
            NextPart?.Measure(availableSize);

            static double Width(TextBlock block, out double height)
            {
                if (block == null)
                {
                    height = 0;
                    return 0;
                }

                // This is NOT optimal, but this control triggers a small amount of measures.
                var rect = block.ContentEnd.GetCharacterRect(LogicalDirection.Forward);
                height = block.DesiredSize.Height;
                return rect.Right;
            }

            _prefixRight = Width(PrefixPart, out double height1);
            _nextRight = Width(NextPart, out double height2);

            var width = Math.Round(_prefixRight + _nextRight + Width(SuffixPart, out double height3));
            var height = Math.Max(height1, Math.Max(height2, height3));

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            PrefixPart?.Arrange(new Rect(0, 0, PrefixPart.DesiredSize.Width, PrefixPart.DesiredSize.Height));
            var width = PrefixPart?.DesiredSize.Width ?? 0;

            if (NextPart != null)
            {
                if (TextAlignment == TextAlignment.Right)
                {
                    NextPart.Arrange(new Rect(width, 0, NextPart.DesiredSize.Width, NextPart.DesiredSize.Height));
                    PrevPart?.Arrange(new Rect(width + NextPart.DesiredSize.Width - PrevPart.DesiredSize.Width, 0, PrevPart.DesiredSize.Width, PrevPart.DesiredSize.Height));
                }
                else
                {
                    NextPart.Arrange(new Rect(_prefixRight, 0, NextPart.DesiredSize.Width, NextPart.DesiredSize.Height));
                    PrevPart?.Arrange(new Rect(_prefixRight, 0, PrevPart.DesiredSize.Width, PrevPart.DesiredSize.Height));
                }

                width += NextPart.DesiredSize.Width;
            }

            if (TextAlignment == TextAlignment.Right)
            {
                SuffixPart?.Arrange(new Rect(width, 0, SuffixPart.DesiredSize.Width, SuffixPart.DesiredSize.Height));
            }
            else
            {
                SuffixPart?.Arrange(new Rect(_prefixRight + _nextRight, 0, SuffixPart.DesiredSize.Width, SuffixPart.DesiredSize.Height));
            }

            return finalSize;
        }
    }

    public class AnimatedTextBlockPresenter : Panel
    {
        // This does nothing, it's only used to have custom Measure and Arrange in AnimatedTextBlock
    }
}
