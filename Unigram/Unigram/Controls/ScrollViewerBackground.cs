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
        private ScrollViewer _scrollViewer;
        private Compositor _compositor;
        private SpriteVisual _visual;
        private Visual _rootVisual;
        private ExpressionAnimation _animationSize;
        private ExpressionAnimation _animationOffset;

        public ScrollViewerBackground()
        {
            //DefaultStyleKey = typeof(ScrollViewerBackground);
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

            var root = scrollViewer.Content as FrameworkElement;
            if (root == null && VerticalAlignment == VerticalAlignment.Bottom)
            {
                return;
            }

            _scrollViewer = scrollViewer;

            if (VerticalAlignment == VerticalAlignment.Top)
            {
                _animationSize = _compositor.CreateExpressionAnimation("Max(Scroll.Translation.Y + 1, 0)");
                _animationSize.SetReferenceParameter("Scroll", props);

                _visual.StartAnimation("Size.Y", _animationSize);
            }
            else if (VerticalAlignment == VerticalAlignment.Bottom)
            {
                _rootVisual = ElementCompositionPreview.GetElementVisual(root);
                _rootVisual.Properties.InsertVector2("Test", new Vector2((float)_scrollViewer.ScrollableWidth, (float)_scrollViewer.ScrollableHeight));

                root.SizeChanged -= OnViewportChanged;
                root.SizeChanged += OnViewportChanged;

                _animationSize = _compositor.CreateExpressionAnimation("-Scroll.Translation.Y - Root.Test.Y");
                _animationSize.SetReferenceParameter("Scroll", props);
                _animationSize.SetReferenceParameter("Root", _rootVisual);

                _animationOffset = _compositor.CreateExpressionAnimation("-Scroll.Translation.Y - Root.Size.Y");
                _animationOffset.SetReferenceParameter("Scroll", props);
                _animationOffset.SetReferenceParameter("Root", _rootVisual);

                //_visual.AnchorPoint = new Vector2(0, 1);
                _visual.StartAnimation("Size.Y", _animationSize);
                _visual.StartAnimation("Offset.Y", _animationOffset);
            }
        }

        private void OnViewportChanged(object sender, SizeChangedEventArgs e)
        {
            _rootVisual.Properties.InsertVector2("Test", new Vector2((float)_scrollViewer.ScrollableWidth, (float)_scrollViewer.ScrollableHeight));
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _visual.Size = new Vector2((float)e.NewSize.Width, _visual.Size.Y);

            if (_animationSize != null)
            {
                _visual.StopAnimation("Size.Y");
                _visual.StartAnimation("Size.Y", _animationSize);
            }

            if (_animationOffset != null)
            {
                _visual.StopAnimation("Offset.Y");
                _visual.StartAnimation("Offset.Y", _animationOffset);
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
