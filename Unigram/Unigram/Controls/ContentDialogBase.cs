using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class ContentDialogBase : ContentDialog
    {
        private TaskCompletionSource<ContentDialogBaseResult> _callback;
        private ContentDialogBaseResult _result;

        private Border BackgroundElement;
        private AppViewBackButtonVisibility BackButtonVisibility;

        public ContentDialogBase()
        {
            DefaultStyleKey = typeof(ContentDialogBase);

            Loaded += OnLoaded;
            Window.Current.SizeChanged += Current_SizeChanged;

            FullSizeDesired = true;

            Opened += OnOpened;
            Closed += OnClosed;
            
        }

        public new IAsyncOperation<ContentDialogBaseResult> ShowAsync()
        {
            return AsyncInfo.Run(async (token) =>
            {
                _result = ContentDialogBaseResult.None;
                _callback = new TaskCompletionSource<ContentDialogBaseResult>();
                await base.ShowAsync();
                return await _callback.Task;
            });
        }

        public new void Hide()
        {
            Hide(ContentDialogBaseResult.None);
        }

        public void Hide(ContentDialogBaseResult result)
        {
            _result = result;
            base.Hide();
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            BackButtonVisibility = SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility;
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            _callback.TrySetResult(_result);

            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = BackButtonVisibility;
        }

        protected override void OnApplyTemplate()
        {
            BackgroundElement = (Border)GetTemplateChild("BackgroundElement");
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            UpdateViewBase();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateViewBase();
        }

        private void UpdateViewBase()
        {
            MinWidth = Window.Current.Bounds.Width;
            MinHeight = Window.Current.Bounds.Height;

            UpdateView();
        }

        protected virtual void UpdateView()
        {
            if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile" && (Window.Current.Bounds.Width < 500 || Window.Current.Bounds.Height < 500))
            {
                BackgroundElement.MinWidth = Window.Current.Bounds.Width;
                BackgroundElement.MinHeight = Window.Current.Bounds.Height;
                BackgroundElement.BorderThickness = new Thickness(0);
            }
            else
            {
                BackgroundElement.MinWidth = Math.Min(/*(double)Resources["ContentDialogMinWidth"]*/ 600, Window.Current.Bounds.Width);
                BackgroundElement.MinHeight = Math.Min(/*(double)Resources["ContentDialogMinHeight"]*/ 500, Window.Current.Bounds.Height);
                BackgroundElement.MaxWidth = Math.Min(/*(double)Resources["ContentDialogMinWidth"]*/ 600, Window.Current.Bounds.Width);
                BackgroundElement.MaxHeight = Math.Min(/*(double)Resources["ContentDialogMinHeight"]*/ 500, Window.Current.Bounds.Height);

                if (BackgroundElement.MinWidth == Window.Current.Bounds.Width && BackgroundElement.MinHeight == Window.Current.Bounds.Height)
                {
                    BackgroundElement.BorderThickness = new Thickness(0);
                }
                else if (BackgroundElement.MinWidth == Window.Current.Bounds.Width && BackgroundElement.MinHeight != Window.Current.Bounds.Height)
                {
                    BackgroundElement.BorderThickness = new Thickness(0, 0, 0, 1);
                }
                else if (BackgroundElement.MinWidth != Window.Current.Bounds.Width && BackgroundElement.MinHeight == Window.Current.Bounds.Height)
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
