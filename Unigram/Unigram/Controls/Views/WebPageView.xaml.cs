using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class WebPageView : BottomSheet
    {
        private WebPageView()
        {
            this.InitializeComponent();
        }

        private static WebPageView _current;
        public static WebPageView Current
        {
            get
            {
                if (_current == null)
                    _current = new WebPageView();

                return _current;
            }
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(TLWebPage webPage)
        {
            if (ApplicationView.GetForCurrentView().IsCompactOverlaySupported())
            {
                return AsyncInfo.Run(async token =>
                {
                    var w = webPage.EmbedWidth ?? 340;
                    var h = webPage.EmbedHeight ?? 200;

                    double ratioX = (double)340 / w;
                    double ratioY = (double)340 / h;
                    double ratio = Math.Min(ratioX, ratioY);

                    var preferences = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
                    preferences.CustomSize = new Size(w * ratio, h * ratio);

                    var newView = CoreApplication.CreateNewView();
                    var newViewId = 0;
                    await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(webPage.EmbedUrl));
                        request.Headers.Referer = new Uri("https://youtube.com");

                        var view = new WebView();
                        view.NavigateWithHttpRequestMessage(request);
                        //view.Navigate(new Uri(webPage.EmbedUrl));

                        var yolo = new GlyphButton();
                        yolo.HorizontalAlignment = HorizontalAlignment.Right;
                        yolo.VerticalAlignment = VerticalAlignment.Bottom;
                        yolo.Glyph = "\uE740";
                        yolo.Click += async (s, args) =>
                        {
                            var current = ApplicationView.GetForCurrentView();
                            if (current.ViewMode == ApplicationViewMode.CompactOverlay)
                            {
                                current.TryEnterFullScreenMode();
                            }
                            else
                            {
                                await current.TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, preferences);
                            }
                        };

                        var ciccio = new Grid();
                        ciccio.RequestedTheme = ElementTheme.Dark;
                        ciccio.Children.Add(view);
                        ciccio.Children.Add(yolo);

                        Window.Current.Content = ciccio;
                        Window.Current.Activate();
                        Window.Current.VisibilityChanged += (s, args) =>
                        {
                            if (args.Visible)
                            {
                                return;
                            }

                            view.NavigateToString(string.Empty);
                        };

                        newViewId = ApplicationView.GetForCurrentView().Id;

                        var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
                        coreTitleBar.ExtendViewIntoTitleBar = true;

                        var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                        titleBar.ButtonBackgroundColor = Colors.Transparent;
                        titleBar.ButtonForegroundColor = Colors.White;
                        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                        titleBar.ButtonInactiveForegroundColor = Colors.White;
                    });

                    var viewShown = await ApplicationViewSwitcher.TryShowAsViewModeAsync(newViewId, ApplicationViewMode.CompactOverlay, preferences);

                    return ContentDialogBaseResult.OK;
                });
            }
            else
            {
                if (webPage.HasEmbedUrl)
                {
                    Image.Constraint = webPage;
                    View.Navigate(new Uri(webPage.EmbedUrl));
                }

                return base.ShowAsync();
            }
        }

        private void Join_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.OK);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.Cancel);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            View.NavigateToString(string.Empty);
        }
    }
}
