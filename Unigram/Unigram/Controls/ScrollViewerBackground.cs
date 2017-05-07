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
        private ExpressionAnimation _animation;

        public ScrollViewerBackground()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

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
            if (scrollViewer == null && ScrollingHost != null)
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
                _animation = _compositor.CreateExpressionAnimation("Max(Scroll.Translation.Y, 0)");
                _animation.SetReferenceParameter("Scroll", props);

                _visual.StopAnimation("Size.Y");
                _visual.StartAnimation("Size.Y", _animation);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _visual.Size = new Vector2((float)e.NewSize.Width, _visual.Size.Y);

            if (_animation != null)
            {
                _visual.StopAnimation("Size.Y");
                _visual.StartAnimation("Size.Y", _animation);
            }
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
