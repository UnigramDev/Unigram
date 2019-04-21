using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Views;
using Unigram.Services;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Unigram.ViewModels.Dialogs;
using Windows.UI.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class ZoomableGridView : GridView
    {
        private GestureRecognizer _recognizer;

        private ScrollViewer _scrollingHost;

        private Popup _popupHost;
        private ZoomableGridViewPopup _popupPanel;
        private object _popupContent;

        private Pointer _pointer;

        public ZoomableGridView()
        {
            _recognizer = new GestureRecognizer();
            _recognizer.GestureSettings = GestureSettings.Hold | GestureSettings.HoldWithMouse;
            _recognizer.Holding += Recognizer_Holding;

            _popupHost = new Popup();
            _popupHost.IsHitTestVisible = false;
            _popupHost.Child = _popupPanel = new ZoomableGridViewPopup();

            PointerMoved += ZoomableGridView_PointerMoved;
        }

        private void Recognizer_Holding(GestureRecognizer sender, HoldingEventArgs args)
        {
            if (args.HoldingState == HoldingState.Started)
            {
                if (_pointer != null)
                {
                    CapturePointer(_pointer);
                }

                var children = VisualTreeHelper.FindElementsInHostCoordinates(args.Position, this);
                var selector = children?.FirstOrDefault(x => x is SelectorItem) as SelectorItem;
                if (selector != null)
                {
                    VisualStateManager.GoToState(selector, "Normal", false);

                    OnItemHolding(selector, selector.Tag);
                }
            }
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

        internal void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                _pointer = e.Pointer;
                _recognizer.ProcessDownEvent(e.GetCurrentPoint(Window.Current.Content as FrameworkElement));
            }
            catch
            {
                _recognizer.CompleteGesture();
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            ReleasePointerCapture(e.Pointer);

            try
            {
                _recognizer.ProcessUpEvent(e.GetCurrentPoint(Window.Current.Content as FrameworkElement));
            }
            catch
            {
                _recognizer.CompleteGesture();
            }

            if (_popupHost.IsOpen)
            {
                _popupHost.IsOpen = false;
                e.Handled = true;
            }

            base.OnPointerReleased(e);
        }

        internal void OnItemHolding(object sender, object item)
        {
            //if (item is TLBotInlineMediaResult inlineMediaResult)
            //{
            //    if (inlineMediaResult.HasDocument)
            //    {
            //        item = inlineMediaResult.Document;
            //    }
            //    else
            //    {
            //        return;
            //    }
            //}

            if (item is StickerViewModel sticker)
            {
                _popupPanel.SetSticker(sticker.ProtoService, sticker.Aggregator, sticker.Get());
            }

            var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
            if (bounds != Window.Current.Bounds)
            {
                _popupPanel.Margin = new Thickness(bounds.X, bounds.Y, Window.Current.Bounds.Width - bounds.Right, Window.Current.Bounds.Height - bounds.Bottom);
            }
            else
            {
                _popupPanel.Margin = new Thickness();
            }

            //if (item is TLDocument content && content.StickerSet != null)
            //{
            //    Debug.WriteLine(string.Join(" ", UnigramContainer.Current.ResolveType<IStickersService>().GetEmojiForSticker(content.Id)));
            //}

            _popupPanel.Width = bounds.Width;
            _popupPanel.Height = bounds.Height;
            _popupContent = item;
            _popupHost.IsOpen = true;

            _scrollingHost.CancelDirectManipulations();
        }

        internal void OnItemPointerEntered(object sender, object content)
        {
            if (_popupHost.IsOpen && _popupContent != content)
            {
                if (content is StickerViewModel sticker)
                {
                    _popupPanel.SetSticker(sticker.ProtoService, sticker.Aggregator, sticker.Get());
                }

                _popupContent = content;
            }
        }

        private void ZoomableGridView_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                _recognizer.ProcessMoveEvents(e.GetIntermediatePoints(Window.Current.Content as FrameworkElement));
            }
            catch
            {
                _recognizer.CompleteGesture();
            }

            if (_popupHost.IsOpen && e.OriginalSource is FrameworkElement element)
            {
                OnItemPointerEntered(sender, element.Tag);
            }
        }

        //protected override void OnPointerReleased(PointerRoutedEventArgs e)
        //{
        //    if (_popupHost.IsOpen)
        //    {
        //        _popupHost.IsOpen = false;
        //        //_popupContent.Content = null;
        //        e.Handled = true;
        //    }

        //    base.OnPointerReleased(e);
        //}

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            if (_popupHost.IsOpen)
            {
                _popupHost.IsOpen = false;
                //_popupContent.Content = null;
                e.Handled = true;
            }

            base.OnPointerCaptureLost(e);
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            if (_popupHost.IsOpen)
            {
                _popupHost.IsOpen = false;
                //_popupContent.Content = null;
                e.Handled = true;
            }

            base.OnPointerCanceled(e);
        }
    }
}
