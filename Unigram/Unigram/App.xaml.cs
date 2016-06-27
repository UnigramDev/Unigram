using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.HockeyApp;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Updates;
using Template10.Common;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Unigram.Core.Dependency;
using Unigram.Core.Notifications;

namespace Unigram
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : BootStrapper
    {
        public IHockeyClient HockeyClient { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

#if RELEASE

            Microsoft.HockeyApp.HockeyClient.Current.Configure("0abc51f4bcaf409a9b86fc9b1cb21eeb",
                        new TelemetryConfiguration()
                        {
                            EnableDiagnostics = true,
                            Collectors = Microsoft.HockeyApp.WindowsCollectors.Metadata |
                                         Microsoft.HockeyApp.WindowsCollectors.PageView |
                                         Microsoft.HockeyApp.WindowsCollectors.Session |
                                         Microsoft.HockeyApp.WindowsCollectors.UnhandledException |
                                         Microsoft.HockeyApp.WindowsCollectors.WatsonData
                        });

#endif
        }

        //protected override void OnActivated(IActivatedEventArgs args)
        //{
        //    Frame rootFrame = Window.Current.Content as Frame;
        //    var data = Toast.GetData(args);
        //    if (data != null)
        //    {
        //        if (data.ContainsKey("messageId"))
        //        {
        //            string messageId = data["messageId"];
        //        }
        //    }

        //    if (rootFrame.BackStack.Count == 0)
        //    {
        //        rootFrame.BackStack.Add(new PageStackEntry(typeof(Views.Home), null, null));
        //    }

        //    Window.Current.Activate();
        //}

        public override async Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            new Bootstrapper().Configure();

            if (SettingsHelper.IsAuthorized)
            {
                var activate = args as ToastNotificationActivatedEventArgs;
                var launch = activate?.Argument ?? null;

                MTProtoService.Instance.CurrentUserId = SettingsHelper.UserId;
                NavigationService.Navigate(typeof(Views.MainPage), launch);
            }
            else
            {
                NavigationService.Navigate(typeof(Views.LoginWelcomePage));
            }

            ShowStatusBar();
            ColourTitleBar();

            await Toast.RegisterBackgroundTasks();
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            var cacheService = UnigramContainer.Instance.ResolverType<ICacheService>();
            cacheService.TryCommit();

            var updatesService = UnigramContainer.Instance.ResolverType<IUpdatesService>();
            updatesService.SaveState();
            updatesService.CancelUpdating();

            return base.OnSuspendingAsync(s, e, prelaunchActivated);
        }

        private void ShowStatusBar()
        {
            // Show StatusBar on Win10 Mobile, in theme of the pass
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                statusBar.BackgroundOpacity = 0;

                // Foreground
                //statusBar.ForegroundColor = Colors.White;

                // Branding colour
                //statusBar.BackgroundColor = Color.FromArgb(255, 54, 173, 225);

                // Accent colour
                //var accentColour = Application.Current.Resources["SystemControlHighlightAccentBrush"] as SolidColorBrush;
                //statusBar.BackgroundColor = accentColour.Color;
            }
        }

        private void ColourTitleBar()
        {
            try
            {
                // Changes to the titlebar (colour, and such)
                ApplicationViewTitleBar titlebar = ApplicationView.GetForCurrentView().TitleBar;

                // Accent Color
                var accentColour = Application.Current.Resources["SystemControlHighlightAccentBrush"] as SolidColorBrush;

                // Foreground
                titlebar.ButtonForegroundColor = Colors.White;
                titlebar.ButtonHoverForegroundColor = Colors.White;
                titlebar.ButtonInactiveForegroundColor = Colors.LightGray;
                titlebar.ButtonPressedForegroundColor = Colors.White;
                titlebar.ForegroundColor = Colors.White;
                titlebar.InactiveForegroundColor = Colors.LightGray;

                // Background
                titlebar.BackgroundColor = accentColour.Color;
                titlebar.ButtonBackgroundColor = accentColour.Color;
                titlebar.ButtonInactiveBackgroundColor = accentColour.Color;
                titlebar.ButtonHoverBackgroundColor = Helpers.ColorHelper.ChangeShade(accentColour.Color, -0.06f);   // TO-DO: needs to be a bit darker
                titlebar.ButtonPressedBackgroundColor = Helpers.ColorHelper.ChangeShade(accentColour.Color, -0.09f); // TO-DO: needs to be even darker
                titlebar.InactiveBackgroundColor = accentColour.Color;

                // Branding colours
                //titlebar.BackgroundColor = Color.FromArgb(255, 54, 173, 225);
                //titlebar.ButtonBackgroundColor = Color.FromArgb(255, 54, 173, 225);
                //titlebar.ButtonHoverBackgroundColor = Color.FromArgb(255, 69, 179, 227);
                //titlebar.ButtonPressedBackgroundColor = Color.FromArgb(255, 84, 185, 229);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Device does not have a Titlebar");
            }
        }
    }
}
