//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    public partial class ScrollViewerScrim : Control
    {
        private CompositionPropertySet _propertySet;

        private Rectangle TopScrim;
        private Rectangle BottomScrim;
        private ScrollViewer _scrollViewer;

        public ScrollViewerScrim()
        {
            DefaultStyleKey = typeof(ScrollViewerScrim);

            this.CreateInsetClip();

            Loaded += OnLoaded;
            RegisterPropertyChangedCallback(BackgroundProperty, OnBackgroundChanged);
        }

        private void OnBackgroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Background is SolidColorBrush brush && TopScrim != null && BottomScrim != null)
            {
                Scrim.SetGradient(TopScrim.Fill, new CubicBezierGradient(brush, 1, brush, 0));
                Scrim.SetGradient(BottomScrim.Fill, new CubicBezierGradient(brush, 0, brush, 1));
            }
        }

        protected override void OnApplyTemplate()
        {
            TopScrim = GetTemplateChild(nameof(TopScrim)) as Rectangle;
            TopScrim.Height = _topInset;

            BottomScrim = GetTemplateChild(nameof(BottomScrim)) as Rectangle;
            BottomScrim.Height = _bottomInset;

            if (Background is SolidColorBrush brush && TopScrim != null && BottomScrim != null)
            {
                Scrim.SetGradient(TopScrim.Fill, new CubicBezierGradient(brush, 1, brush, 0));
                Scrim.SetGradient(BottomScrim.Fill, new CubicBezierGradient(brush, 0, brush, 1));
            }

            if (_scrollingHost != null && _scrollViewer == null)
            {
                SetScrollingHost(_scrollingHost);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_scrollingHost != null && _scrollViewer == null)
            {
                SetScrollingHost(_scrollingHost);
            }
        }

        private FrameworkElement _scrollingHost;
        public FrameworkElement ScrollingHost
        {
            get => _scrollingHost;
            set => SetScrollingHost(value);
        }

        private void SetScrollingHost(FrameworkElement value)
        {
            if (_scrollViewer?.Content is FrameworkElement oldElement)
            {
                oldElement.SizeChanged -= OnSizeChanged;
            }

            _scrollingHost = value;

            if (TopScrim == null || BottomScrim == null)
            {
                return;
            }

            var scrollViewer = value as ScrollViewer;
            if (scrollViewer == null && value != null)
            {
                if (value.IsLoaded)
                {
                    scrollViewer = ScrollingHost.GetChild<ScrollViewer>();
                }
                else
                {
                    value.Loaded += OnLoaded;
                    return;
                }
            }

            if (scrollViewer == null)
            {
                return;
            }

            _scrollViewer = scrollViewer;
            _propertySet = BootStrapper.Current.Compositor.CreatePropertySet();
            _propertySet.InsertScalar("ScrollableHeight", (float)scrollViewer.ScrollableHeight);
            _propertySet.InsertScalar("TopInset", _topInset);
            _propertySet.InsertScalar("BottomInset", _bottomInset);

            var top = ElementComposition.GetElementVisual(TopScrim);
            var bottom = ElementComposition.GetElementVisual(BottomScrim);
            var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);

            var topAnimation = BootStrapper.Current.Compositor.CreateExpressionAnimation("Clamp(-(Scroll.Translation.Y / Props.TopInset), 0, 1)");
            topAnimation.SetReferenceParameter("Scroll", props);
            topAnimation.SetReferenceParameter("Props", _propertySet);

            var bottomAnimation = BootStrapper.Current.Compositor.CreateExpressionAnimation("Clamp((Props.ScrollableHeight + Scroll.Translation.Y) / Props.BottomInset, 0, 1)");
            bottomAnimation.SetReferenceParameter("Scroll", props);
            bottomAnimation.SetReferenceParameter("Props", _propertySet);

            top.StartAnimation("Scale.Y", topAnimation);
            bottom.StartAnimation("Scale.Y", bottomAnimation);
            bottom.CenterPoint = new Vector3(0, _bottomInset, 0);

            if (scrollViewer.Content is FrameworkElement newElement)
            {
                newElement.SizeChanged += OnSizeChanged;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_scrollViewer != null)
            {
                _propertySet.InsertScalar("ScrollableHeight", (float)_scrollViewer.ScrollableHeight);
            }
        }

        private float _topInset = 32;
        public double TopInset
        {
            get => _topInset;
            set
            {
                _topInset = (float)value;
                _propertySet?.InsertScalar("TopInset", _topInset);

                TopScrim?.Height = _topInset;
            }
        }

        private float _bottomInset = 32;
        public double BottomInset
        {
            get => _bottomInset;
            set
            {
                _bottomInset = (float)value;
                _propertySet?.InsertScalar("BottomInset", _bottomInset);

                if (BottomScrim != null)
                {
                    BottomScrim.Height = _bottomInset;

                    var bottom = ElementComposition.GetElementVisual(BottomScrim);
                    bottom.CenterPoint = new Vector3(0, _bottomInset, 0);
                }
            }
        }
    }
}
