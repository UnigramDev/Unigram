using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.ViewModels.Drawers;
using Unigram.Views.Popups;
using Windows.Devices.Input;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class ZoomableRepeaterHandler
    {
        private readonly ItemsRepeater _listView;
        private readonly DispatcherTimer _throttler;

        private FrameworkElement _element;
        private Pointer _pointer;

        private ZoomableMediaPopup _popupPanel;
        private Popup _popupHost;
        private object _popupContent;

        private bool _shouldCapture;

        public ZoomableRepeaterHandler(ItemsRepeater listView)
        {
            _listView = listView;
            _listView.Loaded += OnLoaded;
            _listView.Unloaded += OnUnloaded;

            _popupHost = new Popup();
            _popupHost.IsHitTestVisible = false;
            _popupHost.Child = _popupPanel = new ZoomableMediaPopup();

            _throttler = new DispatcherTimer();
            _throttler.Interval = TimeSpan.FromMilliseconds(Constants.HoldingThrottle);
            _throttler.Tick += (s, args) =>
            {
                _throttler.Stop();

                try
                {
                    DoSomething(_popupContent);
                }
                catch
                {
                    _shouldCapture = false;
                    _popupContent = null;
                    _pointer = null;

                    if (_popupHost.IsOpen)
                    {
                        _popupHost.IsOpen = false;
                        Closing?.Invoke();
                    }
                }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _listView.PointerMoved += OnPointerMoved;
            _listView.PointerReleased += OnPointerReleased;
            _listView.PointerCanceled += OnPointerReleased;
            _listView.PointerCaptureLost += OnPointerReleased;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _listView.PointerMoved -= OnPointerMoved;
            _listView.PointerReleased -= OnPointerReleased;
            _listView.PointerCanceled -= OnPointerReleased;
            _listView.PointerCaptureLost -= OnPointerReleased;
        }

        public Action<int> DownloadFile
        {
            get => _popupPanel.DownloadFile;
            set => _popupPanel.DownloadFile = value;
        }

        public Action Opening { get; set; }
        public Action Closing { get; set; }

        public void UpdateFile(File file)
        {
            _popupPanel.UpdateFile(file);
        }

        private PointerEventHandler _handlerPressed;
        private PointerEventHandler _handlerReleased;
        private PointerEventHandler _handlerExited;

        public void ElementPrepared(UIElement container)
        {
            container.AddHandler(UIElement.PointerPressedEvent, _handlerPressed ??= new PointerEventHandler(OnPointerPressed), true);
            container.AddHandler(UIElement.PointerReleasedEvent, _handlerReleased ??= new PointerEventHandler(OnPointerReleased), true);
            container.AddHandler(UIElement.PointerExitedEvent, _handlerExited ??= new PointerEventHandler(OnPointerExited), true);
            container.AddHandler(UIElement.PointerCaptureLostEvent, _handlerExited ??= new PointerEventHandler(OnPointerExited), true);
        }

        public void ElementClearing(UIElement container)
        {
            if (_handlerPressed == null || _handlerReleased == null || _handlerExited == null)
            {
                return;
            }

            container.RemoveHandler(UIElement.PointerPressedEvent, _handlerPressed);
            container.RemoveHandler(UIElement.PointerReleasedEvent, _handlerReleased);
            container.RemoveHandler(UIElement.PointerExitedEvent, _handlerExited);
            container.RemoveHandler(UIElement.PointerCaptureLostEvent, _handlerExited);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }

            _element = sender as FrameworkElement;
            _pointer = e.Pointer;

            _popupContent = ItemFromContainer(_element);
            _throttler.Stop();
            _throttler.Start();
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _throttler.Stop();

            _shouldCapture = false;
            _popupContent = null;
            _pointer = null;

            if (_popupHost.IsOpen)
            {
                _popupHost.IsOpen = false;

                Closing?.Invoke();
                e.Handled = true;
            }
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_shouldCapture)
            {
                _shouldCapture = false;
                _listView.CapturePointer(e.Pointer);
            }

            _throttler.Stop();
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_popupHost.IsOpen)
            {
                var pointer = e.GetCurrentPoint(Window.Current.Content);
                var children = VisualTreeHelper.FindElementsInHostCoordinates(pointer.Position, _listView);

                var container = children?.FirstOrDefault(x => x is Button) as Button;
                if (container != null)
                {
                    var content = ItemFromContainer(container);
                    if (content is Sticker sticker && _popupContent != content)
                    {
                        _popupPanel.SetSticker(sticker);
                    }
                    else if (content is Animation animation && _popupContent != content)
                    {
                        _popupPanel.SetAnimation(animation);
                    }

                    _popupContent = content;
                }
            }
        }

        private object ItemFromContainer(FrameworkElement container)
        {
            return GetContent(container.DataContext);
        }

        private object GetContent(object content)
        {
            if (content is StickerViewModel stickerViewModel)
            {
                return (Sticker)stickerViewModel;
            }
            else if (content is InlineQueryResultAnimation resultAnimation)
            {
                return resultAnimation.Animation;
            }
            else if (content is InlineQueryResultSticker resultSticker)
            {
                return resultSticker.Sticker;
            }
            else if (content is Sticker || content is Animation)
            {
                return content;
            }

            return null;
        }

        private void DoSomething(object item)
        {
            var content = GetContent(item);
            if (content == null)
            {
                return;
            }

            if (_pointer != null)
            {
                _shouldCapture = true;
                _element.ReleasePointerCapture(_pointer);
                _pointer = null;
            }

            if (_element != null)
            {
                VisualStateManager.GoToState(_element as Control, "Normal", false);
                _element = null;
            }

            if (content is Sticker sticker)
            {
                _popupPanel.SetSticker(sticker);
            }
            else if (content is Animation animation)
            {
                _popupPanel.SetAnimation(animation);
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

            Opening?.Invoke();

            _popupPanel.Width = bounds.Width;
            _popupPanel.Height = bounds.Height;
            _popupContent = content;
            _popupHost.IsOpen = true;

            //_scrollingHost.CancelDirectManipulations();
        }
    }
}
