using System.Numerics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    // This is a subset of AnimatedTextBlock that animates the whole text
    public partial class SlideTextBlock : Control
    {
        private TextBlock PrevPart;
        private TextBlock NextPart;

        public SlideTextBlock()
        {
            DefaultStyleKey = typeof(SlideTextBlock);
        }

        protected override void OnApplyTemplate()
        {
            ChangePartText(ref NextPart, nameof(NextPart), Text, true, true);
        }

        private void ChangePartText(ref TextBlock part, string name, string text, bool resize, bool length = false)
        {
            if (part != null || text.Length > 0 || length)
            {
                if (part == null)
                {
                    part = GetTemplateChild(name) as TextBlock;

                    if (resize)
                    {
                        part.SizeChanged += Part_SizeChanged;
                    }
                    else
                    {
                        ElementCompositionPreview.SetIsTranslationEnabled(part, true);
                    }
                }

                part.Text = text;
            }
        }

        private void Part_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var visual = ElementComposition.GetElementVisual(sender as UIElement);
            var point = e.NewSize.ToVector2();

            if (sender == PrevPart)
            {
                visual.CenterPoint = new Vector3(point.X / 2, -4, 0);
            }
            else if (sender == NextPart)
            {
                visual.CenterPoint = new Vector3(point.X / 2, point.Y + 4, 0);
            }
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SlideTextBlock), new PropertyMetadata(string.Empty, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SlideTextBlock)d).OnTextChanged((string)e.NewValue, (string)e.OldValue);
        }

        #endregion

        #region TextAlignment

        public TextAlignment TextAlignment
        {
            get { return (TextAlignment)GetValue(TextAlignmentProperty); }
            set { SetValue(TextAlignmentProperty, value); }
        }

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(SlideTextBlock), new PropertyMetadata(TextAlignment.Left));

        #endregion

        #region TextStyle

        public Style TextStyle
        {
            get { return (Style)GetValue(TextStyleProperty); }
            set { SetValue(TextStyleProperty, value); }
        }

        public static readonly DependencyProperty TextStyleProperty =
            DependencyProperty.Register("TextStyle", typeof(Style), typeof(SlideTextBlock), new PropertyMetadata(null));

        #endregion

        #region

        public TextWrapping TextWrapping
        {
            get { return (TextWrapping)GetValue(TextWrappingProperty); }
            set { SetValue(TextWrappingProperty, value); }
        }

        public static readonly DependencyProperty TextWrappingProperty =
            DependencyProperty.Register("TextWrapping", typeof(TextWrapping), typeof(SlideTextBlock), new PropertyMetadata(TextWrapping.Wrap));

        #endregion

        public bool SkipAnimation { get; set; }

        private void OnTextChanged(string newValue, string oldValue)
        {
            if (NextPart == null)
            {
                return;
            }

            var prevLength = SkipAnimation ? 0 : oldValue.Length;
            var nextLength = SkipAnimation ? 0 : newValue.Length;

            if (prevLength > 0 || nextLength > 0)
            {
                ChangePartText(ref PrevPart, nameof(PrevPart), oldValue, true, true);
                ChangePartText(ref NextPart, nameof(NextPart), newValue, true);

                var prevVisual = ElementComposition.GetElementVisual(PrevPart);
                var nextVisual = ElementComposition.GetElementVisual(NextPart);

                var easing = prevVisual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1));

                var fadeOut = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
                fadeOut.InsertKeyFrame(0, 1);
                fadeOut.InsertKeyFrame(1, 0, easing);
                fadeOut.Duration = Constants.FastAnimation;

                var fadeIn = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(0, 0);
                fadeIn.InsertKeyFrame(1, 1, easing);
                fadeIn.Duration = Constants.FastAnimation;

                var slideOut = prevVisual.Compositor.CreateVector3KeyFrameAnimation();
                slideOut.InsertKeyFrame(0, new Vector3(1, 1, 1));
                slideOut.InsertKeyFrame(1, new Vector3(1, 0, 1), easing);
                slideOut.Duration = Constants.FastAnimation;

                var slideIn = prevVisual.Compositor.CreateVector3KeyFrameAnimation();
                slideIn.InsertKeyFrame(0, new Vector3(1, 0, 1));
                slideIn.InsertKeyFrame(1, new Vector3(1, 1, 1), easing);
                slideIn.Duration = Constants.FastAnimation;

                prevVisual.StartAnimation("Opacity", fadeOut);
                nextVisual.StartAnimation("Opacity", fadeIn);
                prevVisual.StartAnimation("Scale", slideOut);
                nextVisual.StartAnimation("Scale", slideIn);
            }
            else
            {
                ChangePartText(ref PrevPart, nameof(PrevPart), string.Empty, true);
                ChangePartText(ref NextPart, nameof(NextPart), newValue, true);
            }

            //InvalidateMeasure();
            SkipAnimation = false;
        }
    }
}
