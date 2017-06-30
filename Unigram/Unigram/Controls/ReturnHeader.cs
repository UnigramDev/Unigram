using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    class ReturnHeader : ContentControl
    {
        private ScrollViewer _scrollViewer;
        private double _previousVerticalScrollOffset;
        private CompositionPropertySet _scrollProperties;
        private CompositionPropertySet _animationProperties;
        private Visual _headerVisual;

        public ReturnHeader()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
        }

        public static readonly DependencyProperty TargetListViewBaseProperty =
            DependencyProperty.Register(nameof(TargetListViewBase), typeof(ListViewBase), typeof(ReturnHeader), new PropertyMetadata(null));

        public ListView TargetListViewBase
        {
            get { return (ListView)GetValue(TargetListViewBaseProperty); }
            set { SetValue(TargetListViewBaseProperty, value); }
        }

        public void Show()
        {
            if (_headerVisual != null && _scrollViewer != null)
            {
                _previousVerticalScrollOffset = _scrollViewer.VerticalOffset;
                _animationProperties.InsertScalar("OffsetY", 0.0f);
            }
        }

        protected override void OnApplyTemplate()
        {
            SizeChanged -= ReturnHeader_SizeChanged;
            SizeChanged += ReturnHeader_SizeChanged;

            if (TargetListViewBase != null)
            {
                _scrollViewer = GetScrollViewer(TargetListViewBase);

                //var panel = TargetListViewBase.ItemsPanelRoot;
                //Canvas.SetZIndex(panel, -1);
            }

            if (_scrollViewer != null)
            { 
                _scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
                _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
            }

            StartAnimation();
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_animationProperties != null)
            {
                _animationProperties.TryGetScalar("OffsetY", out float oldOffsetY);

                var delta = _scrollViewer.VerticalOffset - _previousVerticalScrollOffset;
                _previousVerticalScrollOffset = _scrollViewer.VerticalOffset;

                var newOffsetY = oldOffsetY - (float)delta;

                FrameworkElement header = (FrameworkElement)TargetListViewBase.Header;
                newOffsetY = Math.Max((float)-header.ActualHeight, newOffsetY);
                newOffsetY = Math.Min(0, newOffsetY);

                if (oldOffsetY != newOffsetY)
                    _animationProperties.InsertScalar("OffsetY", newOffsetY);
            }
        }

        private void ReturnHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (TargetListViewBase != null)
            {
                StopAnimation();
                StartAnimation();
            }
        }

        private static ScrollViewer GetScrollViewer(DependencyObject s)
        {
            if (s is ScrollViewer)
                return s as ScrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(s); i++)
            {
                var child = VisualTreeHelper.GetChild(s, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void StartAnimation()
        {
            if (_scrollViewer == null)
            {
                return;
            }

            if (_scrollProperties == null)
            {
                _scrollProperties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(_scrollViewer);
            }

            var compositor = _scrollProperties.Compositor;

            if (_animationProperties == null)
            {
                _animationProperties = compositor.CreatePropertySet();
            }

            _previousVerticalScrollOffset = _scrollViewer.VerticalOffset;
            _headerVisual = ElementCompositionPreview.GetElementVisual((UIElement)TargetListViewBase.Header);
            _animationProperties.InsertScalar("OffsetY", 0.0f);
            ExpressionAnimation expressionAnimation = compositor.CreateExpressionAnimation($"Round(max(animationProperties.OffsetY - ScrollingProperties.Translation.Y, 0))");
            expressionAnimation.SetReferenceParameter("ScrollingProperties", _scrollProperties);
            expressionAnimation.SetReferenceParameter("animationProperties", _animationProperties);

            if (_headerVisual != null)
            {
                _headerVisual.StartAnimation("Offset.Y", expressionAnimation);
            }
        }

        private void StopAnimation()
        {
            if (_headerVisual != null)
            {
                _headerVisual.StopAnimation("Offset.Y");
                _animationProperties.InsertScalar("OffsetY", 0.0f);
                var offset = _headerVisual.Offset;
                offset.Y = 0.0f;
                _headerVisual.Offset = offset;
            }
        }
    }
}
