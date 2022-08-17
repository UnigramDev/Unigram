using LinqToVisualTree;
using System.Linq;
using System.Numerics;
using Unigram.Common;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    public class ScrollViewerScrim : Control
    {
        private CompositionPropertySet _propertySet;

        private Rectangle _topScrim;
        private Rectangle _bottomScrim;
        private ScrollViewer _scrollViewer;

        public ScrollViewerScrim()
        {
            DefaultStyleKey = typeof(ScrollViewerScrim);

            Loaded += OnLoaded;
            RegisterPropertyChangedCallback(BackgroundProperty, OnBackgroundChanged);
        }

        private void OnBackgroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Background is SolidColorBrush brush && _topScrim != null && _bottomScrim != null)
            {
                Scrim.SetGradient(_topScrim.Fill, new CubicBezierGradient(brush, 1, brush, 0));
                Scrim.SetGradient(_bottomScrim.Fill, new CubicBezierGradient(brush, 0, brush, 1));
            }
        }

        protected override void OnApplyTemplate()
        {
            _topScrim = GetTemplateChild("TopScrim") as Rectangle;
            _bottomScrim = GetTemplateChild("BottomScrim") as Rectangle;

            if (Background is SolidColorBrush brush && _topScrim != null && _bottomScrim != null)
            {
                Scrim.SetGradient(_topScrim.Fill, new CubicBezierGradient(brush, 1, brush, 0));
                Scrim.SetGradient(_bottomScrim.Fill, new CubicBezierGradient(brush, 0, brush, 1));
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = ScrollingHost as ScrollViewer;
            if (scrollViewer == null && ScrollingHost != null)
            {
                scrollViewer = ScrollingHost.Descendants<ScrollViewer>().FirstOrDefault();
            }

            if (scrollViewer == null || _topScrim == null || _bottomScrim == null)
            {
                return;
            }

            _scrollViewer = scrollViewer;
            _propertySet = Window.Current.Compositor.CreatePropertySet();
            _propertySet.InsertScalar("ScrollableHeight", (float)scrollViewer.ScrollableHeight);
            _propertySet.InsertScalar("TopInset", (float)Padding.Top);
            _propertySet.InsertScalar("BottomInset", (float)Padding.Bottom);

            var top = ElementCompositionPreview.GetElementVisual(_topScrim);
            var bottom = ElementCompositionPreview.GetElementVisual(_bottomScrim);
            var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);

            var topAnimation = Window.Current.Compositor.CreateExpressionAnimation("Min(-((Scroll.Translation.Y < -Props.TopInset ? Scroll.Translation.Y + Props.TopInset : 0) / 32), 1)");
            topAnimation.SetReferenceParameter("Scroll", props);
            topAnimation.SetReferenceParameter("Props", _propertySet);

            var bottomAnimation = Window.Current.Compositor.CreateExpressionAnimation("Min((Props.ScrollableHeight + Scroll.Translation.Y) / 32, 1)");
            bottomAnimation.SetReferenceParameter("Scroll", props);
            bottomAnimation.SetReferenceParameter("Props", _propertySet);

            top.StartAnimation("Scale.Y", topAnimation);
            bottom.StartAnimation("Scale.Y", bottomAnimation);
            bottom.CenterPoint = new Vector3(0, 32, 0);

            if (scrollViewer.Content is FrameworkElement element)
            {
                element.SizeChanged += OnSizeChanged;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_scrollViewer != null)
            {
                _propertySet.InsertScalar("ScrollableHeight", (float)_scrollViewer.ScrollableHeight);
            }
        }

        public FrameworkElement ScrollingHost { get; set; }
    }
}
