using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using LinqToVisualTree;

namespace Unigram.Controls
{
    public class ScrollViewerBackground : Control
    {
        private Compositor _compositor;
        private SpriteVisual _visual;
        private ExpressionAnimation _animationSize;
        private ExpressionAnimation _animationOffset;

        public ScrollViewerBackground()
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _visual = _compositor.CreateSpriteVisual();

            ElementCompositionPreview.SetElementChildVisual(this, _visual);

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;

            RegisterPropertyChangedCallback(BackgroundProperty, OnBackgroundChanged);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = ScrollingHost as ScrollViewer;
            if (scrollViewer == null)
            {
                scrollViewer = ScrollingHost.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            }

            if (scrollViewer == null)
            {
                return;
            }

            var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);
            if (props == null)
            {
                return;
            }

            if (VerticalAlignment == VerticalAlignment.Top)
            {
                _animationSize = _compositor.CreateExpressionAnimation("Max(Scroll.Translation.Y + 1, 0)");
                _animationSize.SetReferenceParameter("Scroll", props);

                _visual.StartAnimation("Size.Y", _animationSize);
            }
            else if (VerticalAlignment == VerticalAlignment.Bottom)
            {

            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _visual.Size = new Vector2((float)e.NewSize.Width, _visual.Size.Y);

            if (_animationSize != null)
                _visual.StartAnimation("Size.Y", _animationSize);

            if (_animationOffset != null)
                _visual.StartAnimation("Offset.Y", _animationSize);
        }

        private void OnBackgroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Background is SolidColorBrush brush)
            {
                _visual.Brush = _compositor.CreateColorBrush(brush.Color);
            }
        }

        public FrameworkElement ScrollingHost { get; set; }
    }
}
