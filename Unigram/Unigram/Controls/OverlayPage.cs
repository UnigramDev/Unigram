using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using LinqToVisualTree;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Controls.Primitives;
using Template10.Common;
using Windows.UI;
using Windows.Foundation.Metadata;
using Template10.Services.NavigationService;
using Template10.Services.ViewService;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Input;
using Windows.System;
using Unigram.Common;
using Windows.ApplicationModel.Core;

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

                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    var statusBar = StatusBar.GetForCurrentView();
                    statusBar.BackgroundColor = maskBackground;
                    statusBar.ForegroundColor = maskForeground;
                }
            }
        }

        protected void UnmaskTitleAndStatusBar()
        {
            TLWindowContext.GetForCurrentView().UpdateTitleBar();
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                Hide();
                e.Handled = true;
            }
            else
            {
                base.OnKeyDown(e);
            }
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

                _applicationView = ApplicationView.GetForCurrentView();
                OnVisibleBoundsChanged(_applicationView, null);

                Padding = new Thickness(0, CoreApplication.GetCurrentView().TitleBar.Height, 0, 0);

                if (_popupHost == null)
                {
                    _popupHost = new Popup();
                    _popupHost.Child = this;
                    _popupHost.IsLightDismissEnabled = false;
                    _popupHost.Loading += PopupHost_Loading;
                    _popupHost.Loaded += PopupHostLoaded;
                    _popupHost.Opened += PopupHost_Opened;
                    _popupHost.Closed += PopupHost_Closed;

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

                _closing = false;
                _popupHost.IsOpen = true;

                return await _callback.Task;
            });
        }

        private void PopupHost_Unloaded(object sender, RoutedEventArgs e)
        {
            _callback.TrySetResult(_result);
        }

        private void PopupHost_Loading(FrameworkElement sender, object args)
        {
            OnVisibleBoundsChanged(_applicationView, null);
        }

        private void PopupHostLoaded(object sender, RoutedEventArgs e)
        {
            Focus(FocusState.Programmatic);
        }

        private void PopupHost_Opened(object sender, object e)
        {
            MaskTitleAndStatusBar();

            //BackButtonVisibility = SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility;
            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
            //Window.Current.SizeChanged += OnSizeChanged;

            OnVisibleBoundsChanged(_applicationView, null);
        }

        private void PopupHost_Closed(object sender, object e)
        {
            UnmaskTitleAndStatusBar();

            //_callback.TrySetResult(_result);

            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = BackButtonVisibility;
            _applicationView.VisibleBoundsChanged -= OnVisibleBoundsChanged;
        }

        public void OnBackRequested(HandledRoutedEventArgs e)
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

        protected virtual void OnBackRequestedOverride(object sender, HandledRoutedEventArgs e)
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
            var e = new HandledRoutedEventArgs();
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

            OnVisibleBoundsChanged(_applicationView, null);

            Container.Tapped += Outside_Tapped;
            BackgroundElement.Tapped += Inside_Tapped;
        }

        private void Inside_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void Outside_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Hide();
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
            var bounds = _applicationView.VisibleBounds;
            Width = bounds.Width;
            Height = bounds.Height;
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

        public event TypedEventHandler<Type> AfterRestoreSavedNavigation;

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

        public void Navigate(Type page, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null)
        {
            throw new NotImplementedException();
        }

        public void Navigate<T>(T key, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null) where T : struct, IConvertible
        {
            throw new NotImplementedException();
        }

        public Task<bool> NavigateAsync(Type page, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null)
        {
            throw new NotImplementedException();
        }

        public Task<bool> NavigateAsync<T>(T key, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null) where T : struct, IConvertible
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
