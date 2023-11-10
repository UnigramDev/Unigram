//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Telegram.Views.Host;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public abstract class OverlayWindow : ContentControl, INavigablePage
    {
        private ApplicationView _applicationView;

        private Popup _popupHost;

        private bool _closing;

        private TaskCompletionSource<ContentDialogResult> _callback;
        private ContentDialogResult _result;

        protected Border Container;
        protected Border BackgroundElement;

        public event EventHandler Closing;

        [ThreadStatic]
        public static OverlayWindow Current;

        public OverlayWindow()
        {
            DefaultStyleKey = typeof(OverlayWindow);

            Loading += OnLoading;
            Loaded += OnLoaded;
            //FullSizeDesired = true;

            //Opened += OnOpened;
            //Closed += OnClosed;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);
            if (!IsConstrainedToRootBounds && InputListener.IsPointerGoBackGesture(pointer))
            {
                var args = new BackRequestedRoutedEventArgs();
                OnBackRequested(args);
                e.Handled = args.Handled;
            }
            else if (pointer.Properties.IsLeftButtonPressed && IsLightDismissEnabled && e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                OnBackRequested(new BackRequestedRoutedEventArgs());
            }

            base.OnPointerPressed(e);
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (sender == null)
            {
                return;
            }

            if (/*BackgroundElement != null &&*/ Window.Current?.Bounds is Rect bounds && sender.VisibleBounds != bounds)
            {
                Margin = new Thickness(sender.VisibleBounds.X - bounds.Left, sender.VisibleBounds.Y - bounds.Top, bounds.Width - (sender.VisibleBounds.Right - bounds.Left), bounds.Height - (sender.VisibleBounds.Bottom - bounds.Top));
                UpdateViewBase();
            }
            else
            {
                Margin = new Thickness();
                UpdateViewBase();
            }
        }

        protected virtual void MaskTitleAndStatusBar()
        {
            if (Window.Current.Content is RootPage root)
            {
                root.PopupOpened();
            }

            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            //titlebar.BackgroundColor = Colors.Black;
            titlebar.ForegroundColor = Colors.White;
            //titlebar.ButtonBackgroundColor = Colors.Black;
            titlebar.ButtonForegroundColor = Colors.White;
        }

        protected void UnmaskTitleAndStatusBar()
        {
            if (Window.Current.Content is RootPage root)
            {
                root.PopupClosed();
            }

            WindowContext.Current.UpdateTitleBar();
        }

        public bool IsConstrainedToRootBounds => _popupHost?.IsConstrainedToRootBounds ?? !CanUnconstrainFromRootBounds;

        public bool CanUnconstrainFromRootBounds => SettingsService.Current.FullScreenGallery;

        public async Task<ContentDialogResult> ShowAsync()
        {
            Current = this;
            Margin = new Thickness();

#if DEBUG
            if (DataContext is INavigable navigable && navigable.NavigationService == null)
            {
                throw new InvalidOperationException();
            }
#endif

            var previous = _callback;

            _result = ContentDialogResult.None;
            _callback = new TaskCompletionSource<ContentDialogResult>();

            if (_popupHost == null)
            {
                _popupHost = new Popup();
                _popupHost.Child = this;
                _popupHost.IsLightDismissEnabled = false;
                _popupHost.Loading += PopupHost_Loading;
                _popupHost.Loaded += PopupHost_Loaded;
                _popupHost.Opened += PopupHost_Opened;
                _popupHost.Closed += PopupHost_Closed;

                if (CanUnconstrainFromRootBounds)
                {
                    _popupHost.ShouldConstrainToRootBounds = false;
                }

                Unloaded += PopupHost_Unloaded;
            }

            // Cool down
            if (previous != null)
            {
                await previous.Task;
            }
            //if (Environment.TickCount - _lastHide < 500)
            //{
            //    await Task.Delay(200);
            //}

            _applicationView = ApplicationView.GetForCurrentView();
            OnVisibleBoundsChanged(_applicationView, null);

            Padding = new Thickness(0, 40, 0, 0);

            _closing = false;
            _popupHost.IsOpen = true;

            return await _callback.Task;
        }

        private void DisplayRegion_Changed(Rect sender, object args)
        {
            var scaleFactor = XamlRoot.RasterizationScale;

            var x = Window.Current.Bounds.X - sender.X / scaleFactor;
            var y = Window.Current.Bounds.Y - sender.Y / scaleFactor;

            var width = sender.Width / scaleFactor;
            var height = sender.Height / scaleFactor;

            Width = width;
            Height = height;

            _popupHost.Margin = new Thickness(-x, -y, 0, 0);
            _popupHost.Width = width;
            _popupHost.Height = height;
        }

        private void PopupHost_Unloaded(object sender, RoutedEventArgs e)
        {
            _callback.TrySetResult(_result);
        }

        private void PopupHost_Loading(FrameworkElement sender, object args)
        {
            if (_applicationView != null)
            {
                OnVisibleBoundsChanged(_applicationView, null);
            }
        }

        private void PopupHost_Loaded(object sender, RoutedEventArgs e)
        {
            Focus(FocusState.Programmatic);
        }

        private void PopupHost_Opened(object sender, object e)
        {
            MaskTitleAndStatusBar();

            if (_applicationView != null)
            {
                _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
                OnVisibleBoundsChanged(_applicationView, null);
            }
        }

        private void PopupHost_Closed(object sender, object e)
        {
            UnmaskTitleAndStatusBar();

            //_callback.TrySetResult(_result);

            if (_applicationView != null)
            {
                _applicationView.VisibleBoundsChanged -= OnVisibleBoundsChanged;
            }
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs e)
        {
            if (_closing)
            {
                e.Handled = true;
                return;
            }

            //BootStrapper.BackRequested -= OnBackRequested;
            _closing = true;
            OnBackRequestedOverride(this, e);
        }

        protected void Cancel()
        {
            _closing = false;
        }

        protected virtual void OnBackRequestedOverride(object sender, BackRequestedRoutedEventArgs e)
        {
            e.Handled = true;
            Hide(ContentDialogResult.None);
        }

        protected void Prepare()
        {
            //Margin = new Thickness(Window.Current.Bounds.Width, Window.Current.Bounds.Height, 0, 0);
            Closing?.Invoke(this, EventArgs.Empty);
        }

        public void TryHide(ContentDialogResult result)
        {
            var e = new BackRequestedRoutedEventArgs();
            OnBackRequestedOverride(this, e);

            if (e.Handled)
            {
                return;
            }

            Hide(result);
        }

        public void Hide()
        {
            Hide(ContentDialogResult.None);
        }

        public void Hide(ContentDialogResult result)
        {
            if (_popupHost == null || !_popupHost.IsOpen)
            {
                return;
            }

            _result = result;
            _popupHost.IsOpen = false;

            if (Current == this)
            {
                Current = null;
            }
        }

        protected override void OnApplyTemplate()
        {
            Container = (Border)GetTemplateChild("Container");
            BackgroundElement = (Border)GetTemplateChild("BackgroundElement");

            if (_applicationView != null)
            {
                OnVisibleBoundsChanged(_applicationView, null);
            }

            Container.Tapped += Outside_Tapped;
            BackgroundElement.Tapped += Inside_Tapped;
        }

        private void Inside_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.OriginalSource == BackgroundElement && IsLightDismissEnabled)
            {
                Hide();
            }

            e.Handled = true;
        }

        private void Outside_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (IsLightDismissEnabled)
            {
                Hide();
            }
        }

        public bool IsLightDismissEnabled { get; set; }

        private void OnSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            //UpdateViewBase();
        }

        private void OnLoading(FrameworkElement sender, object args)
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            UpdateViewBase();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            UpdateViewBase();
        }

        private void UpdateViewBase()
        {
            if (_applicationView != null)
            {
                var bounds = _applicationView.VisibleBounds;
                Width = bounds.Width;
                Height = bounds.Height;
            }
        }

        #region OverlayBrush

        public Brush OverlayBrush
        {
            get => (Brush)GetValue(OverlayBrushProperty);
            set => SetValue(OverlayBrushProperty, value);
        }

        public static readonly DependencyProperty OverlayBrushProperty =
            DependencyProperty.Register("OverlayBrush", typeof(Brush), typeof(OverlayWindow), new PropertyMetadata(null));

        #endregion
    }
}
