//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Popups;
using Microsoft.UI.Input;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Telegram.Common
{
    public class ZoomableListHandler
    {
        private readonly ListViewBase _listView;
        private readonly FrameworkElementState _manager;
        private readonly DispatcherTimer _throttler;

        private Control _element;
        private Pointer _pointer;

        private readonly ZoomableMediaPopup _popupPanel;
        private readonly Popup _popupHost;
        private object _popupContent;

        public ZoomableListHandler(ListViewBase listView)
        {
            _listView = listView;

            _manager = new FrameworkElementState(listView);
            _manager.Loaded += OnLoaded;
            _manager.Unloaded += OnUnloaded;

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

        public void Release()
        {
            // These are strong references and prevent
            // owner classes from being disposed
            Opening = null;
            Closing = null;
            DownloadFile = null;
            SessionId = null;
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


        public Func<int> SessionId
        {
            get => _popupPanel.SessionId;
            set => _popupPanel.SessionId = value;
        }

        public Action Opening { get; set; }
        public Action Closing { get; set; }

        private PointerEventHandler _handlerPressed;
        private PointerEventHandler _handlerReleased;
        private PointerEventHandler _handlerExited;

        public void ElementPrepared(SelectorItem container)
        {
            container.AddHandler(UIElement.PointerPressedEvent, _handlerPressed ??= new PointerEventHandler(OnPointerPressed), true);
            container.AddHandler(UIElement.PointerReleasedEvent, _handlerReleased ??= new PointerEventHandler(OnPointerReleased), true);
            container.AddHandler(UIElement.PointerExitedEvent, _handlerExited ??= new PointerEventHandler(OnPointerExited), true);
        }

        public void ElementClearing(SelectorItem container)
        {
            if (_handlerPressed == null || _handlerReleased == null || _handlerExited == null)
            {
                return;
            }

            container.RemoveHandler(UIElement.PointerPressedEvent, _handlerPressed);
            container.RemoveHandler(UIElement.PointerReleasedEvent, _handlerReleased);
            container.RemoveHandler(UIElement.PointerExitedEvent, _handlerExited);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }

            _element = sender as Control;
            _pointer = e.Pointer;

            _popupContent = ItemFromContainer(sender as FrameworkElement);
            _throttler.Stop();
            _throttler.Start();
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _throttler.Stop();

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
            _throttler.Stop();
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_popupHost.IsOpen)
            {
                var pointer = e.GetCurrentPoint(_listView.XamlRoot.Content);
                var children = VisualTreeHelper.FindElementsInHostCoordinates(pointer.Position, _listView);

                var container = children?.FirstOrDefault(x => x is SelectorItem) as SelectorItem;
                if (container != null)
                {
                    var content = ItemFromContainer(container);
                    if (content == _popupContent)
                    {
                        return;
                    }

                    _popupContent = content;

                    if (content is StickerViewModel stickerViewModel)
                    {
                        _popupPanel.SetSticker(stickerViewModel);
                    }
                    else if (content is Sticker sticker)
                    {
                        _popupPanel.SetSticker(sticker);
                    }
                    else if (content is Animation animation)
                    {
                        _popupPanel.SetAnimation(animation);
                    }
                }
            }
        }

        private object ItemFromContainer(FrameworkElement container)
        {
            var content = _listView.ItemFromContainer(container);
            if (content is StickerViewModel stickerViewModel)
            {
                return stickerViewModel;
            }
            else if (content is InlineQueryResultAnimation resultAnimation)
            {
                return resultAnimation.Animation;
            }
            else if (content is InlineQueryResultSticker resultSticker)
            {
                return resultSticker.Sticker;
            }
            else if (content is Sticker or Animation)
            {
                return content;
            }

            return null;
        }

        private void DoSomething(object item)
        {
            if (item is null or not (StickerViewModel or Sticker or Animation))
            {
                return;
            }

            //if (_pointer != null)
            //{
            //    _listView.CapturePointer(_pointer);
            //    _pointer = null;
            //}

            if (_pointer != null)
            {
                _listView.CapturePointer(_pointer);
                //_listView.ReleasePointerCapture(_pointer);
                _pointer = null;
            }

            if (_element != null)
            {
                VisualStateManager.GoToState(_element, "Normal", false);
                _element = null;
            }

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

            if (item is StickerViewModel stickerViewModel)
            {
                item = (Sticker)stickerViewModel;
            }

            if (item is Sticker sticker)
            {
                _popupPanel.SetSticker(sticker);
            }
            else if (item is Animation animation)
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

            //if (item is TLDocument content && content.StickerSet != null)
            //{
            //    Debug.WriteLine(string.Join(" ", UnigramContainer.Current.ResolveType<IStickersService>().GetEmojiForSticker(content.Id)));
            //}

            Opening?.Invoke();

            _popupPanel.Width = bounds.Width;
            _popupPanel.Height = bounds.Height;
            _popupContent = item;
            _popupHost.IsOpen = true;

            //_scrollingHost.CancelDirectManipulations();
        }
    }
}
