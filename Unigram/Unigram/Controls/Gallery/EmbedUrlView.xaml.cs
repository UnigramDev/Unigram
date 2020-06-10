using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.System.Display;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Controls.Gallery
{
    public sealed partial class EmbedUrlView : OverlayPage, INavigatingPage
    {
        private Func<FrameworkElement> _closing;

        private DisplayRequest _request;
        private MediaPlayerElement _mediaPlayerElement;
        private WebView _surface;
        private AppWindow _window;

        private WebPage _webPage;

        private Visual _layer;

        public EmbedUrlView()
        {
            this.InitializeComponent();

            //            var w = webPage.EmbedWidth ?? 340;
            //            var h = webPage.EmbedHeight ?? 200;

            //            double ratioX = (double)340 / w;
            //            double ratioY = (double)340 / h;
            //            double ratio = Math.Min(ratioX, ratioY);

        }

        private static Dictionary<int, WeakReference<EmbedUrlView>> _windowContext = new Dictionary<int, WeakReference<EmbedUrlView>>();
        public static EmbedUrlView GetForCurrentView()
        {
            return new EmbedUrlView();

            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WeakReference<EmbedUrlView> reference) && reference.TryGetTarget(out EmbedUrlView value))
            {
                return value;
            }

            var context = new EmbedUrlView();
            _windowContext[id] = new WeakReference<EmbedUrlView>(context);

            return context;
        }

        public IAsyncOperation<ContentDialogResult> ShowAsync(MessageViewModel message, WebPage parameter, Func<FrameworkElement> closing = null)
        {
            return AsyncInfo.Run(async (token) =>
            {
                _closing = closing;

                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", _closing());
                Load(message, parameter);

                //if (_compactLifetime != null)
                //{
                //    var compact = _compactLifetime;
                //    await compact.CoreDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                //    {
                //        compact.StopViewInUse();
                //        compact.WindowWrapper.Close();
                //    });

                //    _compactLifetime = null;
                //    Dispose();
                //}

                //parameter.Delegate = this;
                //parameter.Items.CollectionChanged -= OnCollectionChanged;
                //parameter.Items.CollectionChanged += OnCollectionChanged;

                //Load(parameter);

                //PrepareNext(0, true);

                //RoutedEventHandler handler = null;
                //handler = new RoutedEventHandler(async (s, args) =>
                //{
                //    Transport.Focus(FocusState.Programmatic);

                //    Loaded -= handler;
                //    await ViewModel?.OnNavigatedToAsync(parameter, NavigationMode.New, null);
                //});

                //Loaded += handler;
                return await ShowAsync();
            });
        }

        private void Load(MessageViewModel message, WebPage webPage)
        {
            _webPage = webPage;

            Aspect.Constraint = message;
            Preview.UpdateItem(null, new ViewModels.Gallery.GalleryMessage(message.ProtoService, message.Get()));

            var w = Math.Max(webPage.EmbedWidth, 340);
            var h = Math.Max(webPage.EmbedHeight, 200);

            double ratioX = (double)340 / w;
            double ratioY = (double)340 / h;
            double ratio = Math.Min(ratioX, ratioY);

            Presenter.Child = _surface = new WebView { Source = new Uri(webPage.EmbedUrl) };
        }

        public void OnBackRequesting(HandledEventArgs e)
        {
            //Unload();
            //Dispose();

            e.Handled = true;
        }

        protected override void OnBackRequestedOverride(object sender, HandledEventArgs e)
        {
            //var container = GetContainer(0);
            //var root = container.Presenter;
            if (_closing != null)
            {
                Presenter.Opacity = 0;
                Preview.Opacity = 1;

                var root = Preview.Presenter;

                var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", root);
                if (animation != null)
                {
                    if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Media.Animation.ConnectedAnimation", "Configuration"))
                    {
                        animation.Configuration = new BasicConnectedAnimationConfiguration();
                    }

                    var element = _closing();
                    if (element.ActualWidth > 0 && animation.TryStart(element))
                    {
                        animation.Completed += (s, args) =>
                        {
                            Hide();
                        };
                    }
                    else
                    {
                        Hide();
                    }
                }
                else
                {
                    Hide();
                }
            }
            else
            {
                //var batch = _layout.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                //_layout.StartAnimation("Offset.Y", CreateScalarAnimation(_layout.Offset.Y, (float)ActualHeight));

                //batch.End();
                //batch.Completed += (s, args) =>
                //{
                //    ScrollingHost.Opacity = 0;
                //    Preview.Opacity = 1;

                //    Hide();
                //};
            }

            //_layer.StartAnimation("Opacity", CreateScalarAnimation(1, 0));

            //if (Transport.IsVisible)
            //{
            //    Transport.Hide();
            //}

            //Unload();
            //Dispose();

            e.Handled = true;
        }

        private void Preview_ImageOpened(object sender, RoutedEventArgs e)
        {
            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
            if (animation != null)
            {
                if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Media.Animation.ConnectedAnimation", "Configuration"))
                {
                    animation.Configuration = new BasicConnectedAnimationConfiguration();
                }

                //_layer.StartAnimation("Opacity", CreateScalarAnimation(0, 1));

                if (animation.TryStart(Preview.Presenter))
                {
                    animation.Completed += (s, args) =>
                    {
                        Presenter.Opacity = 1;
                        Preview.Opacity = 0;
                    };

                    return;
                }
            }
        }

        private async void Compact_Click(object sender, RoutedEventArgs e)
        {
            if (_window == null)
            {
                OnBackRequestedOverride(this, new HandledEventArgs());

                // Create a new AppWindow
                _window = await AppWindow.TryCreateAsync();
                // Make sure we release the reference to this window, and release XAML resources, when it's closed
                _window.Closed += delegate { _window = null; _surface.NavigateToString(string.Empty); };
                // Is CompactOverlay supported for this AppWindow? If not, then stop.
                if (_window.Presenter.IsPresentationSupported(AppWindowPresentationKind.CompactOverlay))
                {
                    // Create a new frame for the window
                    // Navigate the frame to the CompactOverlay page inside it.
                    //appWindowFrame.Navigate(typeof(SecondaryAppWindowPage));
                    // Attach the frame to the window
                    Presenter.Child = new Border();

                    var w = Math.Max(_webPage.EmbedWidth, 340);
                    var h = Math.Max(_webPage.EmbedHeight, 200);

                    double ratioX = (double)340 / w;
                    double ratioY = (double)340 / h;
                    double ratio = Math.Min(ratioX, ratioY);

                    ElementCompositionPreview.SetAppWindowContent(_window, _surface);

                    // Let's set the title so that we can tell the windows apart
                    _window.Title = _webPage.Title;
                    _window.TitleBar.ExtendsContentIntoTitleBar = true;
                    _window.RequestSize(new Size(w * ratio, h * ratio));

                    // Request the Presentation of the window to CompactOverlay
                    var switched = _window.Presenter.RequestPresentation(AppWindowPresentationKind.CompactOverlay);
                    if (switched)
                    {
                        // If the request was satisfied, show the window
                        await _window.TryShowAsync();

                    }
                }
            }
            else
            {
                await _window.TryShowAsync();
            }
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {

        }
    }
}
