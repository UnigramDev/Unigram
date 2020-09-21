using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services.Keyboard;
using Unigram.Services.ViewService;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Controls
{
    public class OverlayPage : ContentControl, INavigablePage
    {
        private int _lastHide;

        private ApplicationView _applicationView;
        private Popup _popupHost;

        private bool _closing;

        private TaskCompletionSource<ContentDialogResult> _callback;
        private ContentDialogResult _result;

        protected Border Container;
        protected Border BackgroundElement;
        private AppViewBackButtonVisibility BackButtonVisibility;

        public event EventHandler Closing;

        public OverlayPage()
        {
            DefaultStyleKey = typeof(OverlayPage);

            Loading += OnLoading;
            Loaded += OnLoaded;
            //FullSizeDesired = true;

            //Opened += OnOpened;
            //Closed += OnClosed;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (!IsConstrainedToRootBounds && KeyboardHelper.IsPointerGoBackGesture(e.GetCurrentPoint(this).Properties))
            {
                var args = new HandledEventArgs();
                OnBackRequested(args);
                e.Handled = args.Handled;
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
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var backgroundBrush = Application.Current.Resources["PageHeaderBackgroundBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;
            var overlayBrush = OverlayBrush as SolidColorBrush;

            if (overlayBrush != null)
            {
                var maskBackground = ColorsHelper.AlphaBlend(backgroundBrush.Color, overlayBrush.Color);
                var maskForeground = ColorsHelper.AlphaBlend(foregroundBrush.Color, overlayBrush.Color);

                titlebar.BackgroundColor = maskBackground;
                titlebar.ForegroundColor = maskForeground;
                //titlebar.ButtonBackgroundColor = maskBackground;
                titlebar.ButtonForegroundColor = maskForeground;
            }
        }

        protected void UnmaskTitleAndStatusBar()
        {
            TLWindowContext.GetForCurrentView().UpdateTitleBar();
        }

        public bool IsOpen
        {
            get
            {
                return _popupHost?.IsOpen ?? false;
            }
            set
            {
                if (value)
                {
                    ShowAsync();
                }
                else
                {
                    Hide();
                }
            }
        }

        public bool IsConstrainedToRootBounds => ApiInfo.CanUnconstrainFromBounds ? _popupHost?.IsConstrainedToRootBounds ?? true : true;

        public IAsyncOperation<ContentDialogResult> ShowAsync()
        {
            return AsyncInfo.Run(async (token) =>
            {
                Margin = new Thickness();

                if (DataContext is INavigable navigable)
                {
                    navigable.NavigationService = new ContentDialogNavigationService(this);
                }

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

                    if (ApiInfo.CanUnconstrainFromBounds)
                    {
                        _popupHost.ShouldConstrainToRootBounds = false;
                    }

                    this.Unloaded += PopupHost_Unloaded;
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

                if (ApiInfo.CanUseWindowManagement && ApiInfo.CanUnconstrainFromBounds)
                {
                    var regions = ApplicationView.GetForCurrentView().GetDisplayRegions();
                    var region = regions.FirstOrDefault();
                    region.Changed += DisplayRegion_Changed;

                    DisplayRegion_Changed(region, null);
                }
                else
                {
                    _applicationView = ApplicationView.GetForCurrentView();
                    OnVisibleBoundsChanged(_applicationView, null);

                    Padding = new Thickness(0, CoreApplication.GetCurrentView().TitleBar.Height, 0, 0);
                }

                _closing = false;
                _popupHost.IsOpen = true;

                return await _callback.Task;
            });
        }

        private void DisplayRegion_Changed(DisplayRegion sender, object args)
        {
            var scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;

            var x = Window.Current.Bounds.X - sender.WorkAreaOffset.X / scaleFactor;
            var y = Window.Current.Bounds.Y - sender.WorkAreaOffset.Y / scaleFactor;

            var width = sender.WorkAreaSize.Width / scaleFactor;
            var height = sender.WorkAreaSize.Height / scaleFactor;

            this.Width = width;
            this.Height = height;

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

        public void OnBackRequested(HandledEventArgs e)
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

        protected virtual void OnBackRequestedOverride(object sender, HandledEventArgs e)
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
            var e = new HandledEventArgs();
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

            _lastHide = Environment.TickCount;
            _result = result;
            _popupHost.IsOpen = false;
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
            e.Handled = true;
        }

        private void Outside_Tapped(object sender, TappedRoutedEventArgs e)
        {
            //Hide();
        }

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
            get { return (Brush)GetValue(OverlayBrushProperty); }
            set { SetValue(OverlayBrushProperty, value); }
        }

        public static readonly DependencyProperty OverlayBrushProperty =
            DependencyProperty.Register("OverlayBrush", typeof(Brush), typeof(OverlayPage), new PropertyMetadata(null));

        #endregion
    }

    public class ContentDialogNavigationService : INavigationService
    {
        private readonly OverlayPage _contentDialog;

        public ContentDialogNavigationService(OverlayPage contentDialog)
        {
            _contentDialog = contentDialog;
        }

        public void GoBack(NavigationTransitionInfo infoOverride = null)
        {
            _contentDialog.TryHide(ContentDialogResult.None);
        }

        public object Content => throw new NotImplementedException();

        public bool CanGoBack => throw new NotImplementedException();

        public bool CanGoForward => throw new NotImplementedException();

        public string NavigationState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public object CurrentPageParam => null;

        public Type CurrentPageType => throw new NotImplementedException();

        public DispatcherWrapper Dispatcher => throw new NotImplementedException();

        public Frame Frame => throw new NotImplementedException();

        public FrameFacade FrameFacade => throw new NotImplementedException();

        public bool IsInMainView => throw new NotImplementedException();

        public int SessionId => throw new NotImplementedException();

        public IDictionary<string, long> CacheKeyToChatId => throw new NotImplementedException();

        public event TypedEventHandler<INavigationService, Type> AfterRestoreSavedNavigation;

        public void ClearCache(bool removeCachedPagesInBackStack = false)
        {
            throw new NotImplementedException();
        }

        public void ClearHistory()
        {
            throw new NotImplementedException();
        }

        public void GoForward()
        {
            throw new NotImplementedException();
        }

        public Task<bool> LoadAsync()
        {
            throw new NotImplementedException();
        }

        public bool Navigate(Type page, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null)
        {
            throw new NotImplementedException();
        }

        public Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null, ViewSizePreference size = ViewSizePreference.UseHalf)
        {
            throw new NotImplementedException();
        }

        public void Refresh()
        {
            throw new NotImplementedException();
        }

        public void Refresh(object param)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RestoreSavedNavigationAsync()
        {
            throw new NotImplementedException();
        }

        public void Resuming()
        {
            throw new NotImplementedException();
        }

        public Task SaveAsync()
        {
            throw new NotImplementedException();
        }

        public Task SaveNavigationAsync()
        {
            throw new NotImplementedException();
        }

        public Task SuspendingAsync()
        {
            throw new NotImplementedException();
        }
    }
}
