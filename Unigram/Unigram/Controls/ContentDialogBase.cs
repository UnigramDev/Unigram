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
using Unigram.Helpers;
using Windows.UI;
using Windows.Foundation.Metadata;
using Template10.Services.NavigationService;
using Template10.Services.ViewService;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Controls
{
    public class ContentDialogBase : ContentControl
    {
        private ApplicationView _applicationView;
        private Popup _popupHost;

        private TaskCompletionSource<ContentDialogBaseResult> _callback;
        private ContentDialogBaseResult _result;

        protected Border BackgroundElement;
        private AppViewBackButtonVisibility BackButtonVisibility;

        public event EventHandler Closing;

        public ContentDialogBase()
        {
            DefaultStyleKey = typeof(ContentDialogBase);

            Loading += OnLoading;
            Loaded += OnLoaded;
            //FullSizeDesired = true;

            //Opened += OnOpened;
            //Closed += OnClosed;
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (BackgroundElement != null && sender.VisibleBounds != Window.Current.Bounds)
            {
                Margin = new Thickness(sender.VisibleBounds.X, sender.VisibleBounds.Y, Window.Current.Bounds.Width - sender.VisibleBounds.Right, Window.Current.Bounds.Height - sender.VisibleBounds.Bottom);
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
            var backgroundBrush = Application.Current.Resources["TelegramBackgroundTitlebarBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;
            var overlayBrush = OverlayBrush as SolidColorBrush;

            if (overlayBrush != null)
            {
                var maskBackground = ColorsHelper.AlphaBlend(backgroundBrush.Color, overlayBrush.Color);
                var maskForeground = ColorsHelper.AlphaBlend(foregroundBrush.Color, overlayBrush.Color);

                titlebar.BackgroundColor = maskBackground;
                titlebar.ForegroundColor = maskForeground;
                titlebar.ButtonBackgroundColor = maskBackground;
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
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var backgroundBrush = Application.Current.Resources["TelegramBackgroundTitlebarBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;

            titlebar.BackgroundColor = backgroundBrush.Color;
            titlebar.ForegroundColor = foregroundBrush.Color;
            titlebar.ButtonBackgroundColor = backgroundBrush.Color;
            titlebar.ButtonForegroundColor = foregroundBrush.Color;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundColor = backgroundBrush.Color;
                statusBar.ForegroundColor = foregroundBrush.Color;
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

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync()
        {
            return AsyncInfo.Run(async (token) =>
            {
                Margin = new Thickness();

                if (DataContext is INavigable navigable)
                {
                    navigable.NavigationService = new ContentDialogNavigationService(this);
                }

                _result = ContentDialogBaseResult.None;
                _callback = new TaskCompletionSource<ContentDialogBaseResult>();

                _applicationView = ApplicationView.GetForCurrentView();
                OnVisibleBoundsChanged(_applicationView, null);

                if (_popupHost == null)
                {
                    _popupHost = new Popup();
                    _popupHost.Child = this;
                    _popupHost.Loading += PopupHost_Loading;
                    _popupHost.Opened += PopupHost_Opened;
                    _popupHost.Closed += PopupHost_Closed;
                }

                _popupHost.IsOpen = true;

                return await _callback.Task;
            });
        }

        private void PopupHost_Loading(FrameworkElement sender, object args)
        {
            OnVisibleBoundsChanged(_applicationView, null);
        }

        private void PopupHost_Opened(object sender, object e)
        {
            MaskTitleAndStatusBar();

            //BackButtonVisibility = SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility;
            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
            BootStrapper.BackRequested += OnBackRequested;
            //Window.Current.SizeChanged += OnSizeChanged;

            OnVisibleBoundsChanged(_applicationView, null);
        }

        private void PopupHost_Closed(object sender, object e)
        {
            UnmaskTitleAndStatusBar();

            _callback.TrySetResult(_result);

            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = BackButtonVisibility;
            _applicationView.VisibleBoundsChanged -= OnVisibleBoundsChanged;
            BootStrapper.BackRequested -= OnBackRequested;
        }

        private void OnBackRequested(object sender, HandledEventArgs e)
        {
            BootStrapper.BackRequested -= OnBackRequested;
            OnBackRequestedOverride(sender, e);
        }

        protected virtual void OnBackRequestedOverride(object sender, HandledEventArgs e)
        {
            e.Handled = true;
            Hide(ContentDialogBaseResult.None);
        }

        protected void Prepare()
        {
            Margin = new Thickness(Window.Current.Bounds.Width, Window.Current.Bounds.Height, 0, 0);
            Closing?.Invoke(this, EventArgs.Empty);
        }

        public void Hide()
        {
            Hide(ContentDialogBaseResult.None);
        }

        public void Hide(ContentDialogBaseResult result)
        {
            _result = result;
            _popupHost.IsOpen = false;
        }

        protected override void OnApplyTemplate()
        {
            BackgroundElement = (Border)GetTemplateChild("BackgroundElement");
        }

        private void OnSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            UpdateViewBase();
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
            MinWidth = bounds.Width;
            MinHeight = bounds.Height;
            MaxWidth = bounds.Width;
            MaxHeight = bounds.Height;

            UpdateView(bounds);
        }

        protected bool IsFullScreenMode()
        {
            var bounds = _applicationView.VisibleBounds;
            return IsFullScreenMode(bounds);
        }

        protected virtual bool IsFullScreenMode(Rect bounds)
        {
            return (HorizontalAlignment == HorizontalAlignment.Stretch && VerticalAlignment == VerticalAlignment.Stretch) || (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile" && (bounds.Width < 500 || bounds.Height < 500));
        }

        protected virtual void UpdateView(Rect bounds)
        {
            if (BackgroundElement == null) return;

            if (IsFullScreenMode(bounds))
            {
                BackgroundElement.MinWidth = bounds.Width;
                BackgroundElement.MinHeight = bounds.Height;
                BackgroundElement.BorderThickness = new Thickness(0);
            }
            else
            {
                BackgroundElement.MinWidth = Math.Min(360, bounds.Width);
                BackgroundElement.MinHeight = Math.Min(460, bounds.Height);
                BackgroundElement.MaxWidth = Math.Min(360, bounds.Width);
                BackgroundElement.MaxHeight = Math.Min(460, bounds.Height);

                if (BackgroundElement.MinWidth == bounds.Width && BackgroundElement.MinHeight == bounds.Height)
                {
                    BackgroundElement.BorderThickness = new Thickness(0);
                }
                else if (BackgroundElement.MinWidth == bounds.Width && BackgroundElement.MinHeight != bounds.Height)
                {
                    BackgroundElement.BorderThickness = new Thickness(0, 0, 0, 1);
                }
                else if (BackgroundElement.MinWidth != bounds.Width && BackgroundElement.MinHeight == bounds.Height)
                {
                    BackgroundElement.BorderThickness = new Thickness(1, 0, 1, 0);
                }
                else
                {
                    var left = 0;
                    var right = 0;
                    var top = 0;
                    var bottom = 0;
                    switch (HorizontalAlignment)
                    {
                        case HorizontalAlignment.Left:
                            left = 0;
                            right = 1;
                            break;
                        case HorizontalAlignment.Right:
                            left = 1;
                            right = 0;
                            break;
                        default:
                            left = 1;
                            right = 1;
                            break;
                    }
                    switch (VerticalAlignment)
                    {
                        case VerticalAlignment.Top:
                            top = 0;
                            bottom = 1;
                            break;
                        case VerticalAlignment.Bottom:
                            top = 1;
                            bottom = 0;
                            break;
                        default:
                            top = 1;
                            bottom = 1;
                            break;
                    }

                    BackgroundElement.BorderThickness = new Thickness(left, top, right, bottom);
                }
            }
        }

        #region OverlayBrush

        public Brush OverlayBrush
        {
            get { return (Brush)GetValue(OverlayBrushProperty); }
            set { SetValue(OverlayBrushProperty, value); }
        }

        public static readonly DependencyProperty OverlayBrushProperty =
            DependencyProperty.Register("OverlayBrush", typeof(Brush), typeof(ContentDialogBase), new PropertyMetadata(null));

        #endregion
    }

    public enum ContentDialogBaseResult
    {
        Cancel,
        No,
        None,
        OK,
        Yes
    }

    public class ContentDialogNavigationService : INavigationService
    {
        private readonly ContentDialogBase _contentDialog;

        public ContentDialogNavigationService(ContentDialogBase contentDialog)
        {
            _contentDialog = contentDialog;
        }

        public void GoBack(NavigationTransitionInfo infoOverride = null)
        {
            _contentDialog.Hide(ContentDialogBaseResult.None);
        }

        public object Content => throw new NotImplementedException();

        public bool CanGoBack => throw new NotImplementedException();

        public bool CanGoForward => throw new NotImplementedException();

        public string NavigationState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public object CurrentPageParam => throw new NotImplementedException();

        public Type CurrentPageType => throw new NotImplementedException();

        public DispatcherWrapper Dispatcher => throw new NotImplementedException();

        public Frame Frame => throw new NotImplementedException();

        public FrameFacade FrameFacade => throw new NotImplementedException();

        public bool IsInMainView => throw new NotImplementedException();

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

        public void Navigate(Type page, object parameter = null, NavigationTransitionInfo infoOverride = null)
        {
            throw new NotImplementedException();
        }

        public void Navigate<T>(T key, object parameter = null, NavigationTransitionInfo infoOverride = null) where T : struct, IConvertible
        {
            throw new NotImplementedException();
        }

        public Task<bool> NavigateAsync(Type page, object parameter = null, NavigationTransitionInfo infoOverride = null)
        {
            throw new NotImplementedException();
        }

        public Task<bool> NavigateAsync<T>(T key, object parameter = null, NavigationTransitionInfo infoOverride = null) where T : struct, IConvertible
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
