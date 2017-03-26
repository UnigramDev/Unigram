using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class ZoomableGridView : GridView
    {
        private ScrollViewer _scrollingHost;

        private Popup _popupHost;
        private ZoomableGridViewPopup _popupPanel;
        private ContentControl _popupContent;

        public ZoomableGridView()
        {
            _popupHost = new Popup();
            _popupHost.IsHitTestVisible = false;
            _popupHost.Child = _popupPanel = new ZoomableGridViewPopup();
            _popupContent = _popupPanel.Children[0] as ContentControl;

            PointerMoved += ZoomableGridView_PointerMoved;
        }

        protected override void OnApplyTemplate()
        {
            _scrollingHost = GetTemplateChild("ScrollViewer") as ScrollViewer;

            base.OnApplyTemplate();
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ZoomableGridViewItem(this);
        }

        internal void OnItemHolding(object sender, object item)
        {
            var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
            if (bounds != Window.Current.Bounds)
            {
                _popupPanel.Margin = new Thickness(bounds.X, bounds.Y, Window.Current.Bounds.Width - bounds.Right, Window.Current.Bounds.Height - bounds.Bottom);
            }
            else
            {
                _popupPanel.Margin = new Thickness();
            }

            _popupPanel.Width = bounds.Width;
            _popupPanel.Height = bounds.Height;
            _popupContent.Content = item;
            _popupHost.IsOpen = true;

            _scrollingHost.CancelDirectManipulations();
        }

        internal void OnItemPointerEntered(object sender, object content)
        {
            if (_popupHost.IsOpen && _popupContent != content)
            {
                _popupContent.Content = content;
            }
        }

        private void ZoomableGridView_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_popupHost.IsOpen && e.OriginalSource is FrameworkElement element)
            {
                if (element.DataContext is TLDocument content && _popupContent.Content != content)
                {
                    _popupContent.Content = content;
                }
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            if (_popupHost.IsOpen)
            {
                _popupHost.IsOpen = false;
                _popupContent.Content = null;
                e.Handled = true;
            }

            base.OnPointerReleased(e);
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            if (_popupHost.IsOpen)
            {
                _popupHost.IsOpen = false;
                _popupContent.Content = null;
                e.Handled = true;
            }

            base.OnPointerCaptureLost(e);
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            if (_popupHost.IsOpen)
            {
                _popupHost.IsOpen = false;
                _popupContent.Content = null;
                e.Handled = true;
            }

            base.OnPointerCanceled(e);
        }
    }
}
