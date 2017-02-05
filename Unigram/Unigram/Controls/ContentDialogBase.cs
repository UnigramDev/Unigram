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

namespace Unigram.Controls
{
    public class ContentDialogBase : ContentControl
    {
        private Popup _popupHost;

        private TaskCompletionSource<ContentDialogBaseResult> _callback;
        private ContentDialogBaseResult _result;

        private Border BackgroundElement;
        private AppViewBackButtonVisibility BackButtonVisibility;

        public event EventHandler Closing;

        public ContentDialogBase()
        {
            DefaultStyleKey = typeof(ContentDialogBase);

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

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync()
        {
            return AsyncInfo.Run(async (token) =>
            {
                Margin = new Thickness();

                _result = ContentDialogBaseResult.None;
                _callback = new TaskCompletionSource<ContentDialogBaseResult>();

                if (_popupHost == null)
                {
                    _popupHost = new Popup();
                    _popupHost.Child = this;
                    _popupHost.Opened += _popupHost_Opened;
                    _popupHost.Closed += _popupHost_Closed;
                }

                _popupHost.IsOpen = true;

                return await _callback.Task;
            });
        }

        private void _popupHost_Opened(object sender, object e)
        {
            //BackButtonVisibility = SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility;
            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            ApplicationView.GetForCurrentView().VisibleBoundsChanged += OnVisibleBoundsChanged;
            //BootStrapper.BackRequested += BootStrapper_BackRequested;
            //Window.Current.SizeChanged += OnSizeChanged;

            OnVisibleBoundsChanged(ApplicationView.GetForCurrentView(), null);
        }

        private void _popupHost_Closed(object sender, object e)
        {
            _callback.TrySetResult(_result);

            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = BackButtonVisibility;
            ApplicationView.GetForCurrentView().VisibleBoundsChanged -= OnVisibleBoundsChanged;
            BootStrapper.BackRequested -= BootStrapper_BackRequested;
        }

        private void BootStrapper_BackRequested(object sender, HandledEventArgs e)
        {
            e.Handled = true;
            Hide();
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateViewBase();
        }

        private void UpdateViewBase()
        {
            var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
            MinWidth = bounds.Width;
            MinHeight = bounds.Height;
            MaxWidth = bounds.Width;
            MaxHeight = bounds.Height;

            UpdateView(bounds);
        }

        protected virtual void UpdateView(Rect bounds)
        {
            if (BackgroundElement == null) return;

            if ((HorizontalAlignment == HorizontalAlignment.Stretch && VerticalAlignment == VerticalAlignment.Stretch) || (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile" && (bounds.Width < 500 || bounds.Height < 500)))
            {
                BackgroundElement.MinWidth = bounds.Width;
                BackgroundElement.MinHeight = bounds.Height;
                BackgroundElement.BorderThickness = new Thickness(0);
            }
            else
            {
                BackgroundElement.MinWidth = Math.Min(640, bounds.Width);
                BackgroundElement.MinHeight = Math.Min(500, bounds.Height);
                BackgroundElement.MaxWidth = Math.Min(640, bounds.Width);
                BackgroundElement.MaxHeight = Math.Min(500, bounds.Height);

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
}
