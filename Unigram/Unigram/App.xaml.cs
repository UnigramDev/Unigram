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
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Media.SpeechRecognition;
using Windows.ApplicationModel.VoiceCommands;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Networking.PushNotifications;
using Unigram.Tasks;
using Windows.UI.Notifications;
using Windows.Storage;
using Windows.UI.Popups;
using Unigram.Views;
using Windows.Media;
using System.IO;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Views.Login;
using Windows.UI.Core;
using Unigram.Converters;
using Windows.Foundation.Metadata;
using Windows.ApplicationModel.Core;
using System.Collections;
using Telegram.Api.TL;
using System.Collections.Generic;

namespace Unigram
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Template10.Common.BootStrapper
    {
        public static ShareOperation ShareOperation { get; private set; }
        public static AppServiceConnection Connection { get; private set; }

        public static AppState State { get; } = new AppState();

        public ViewModelLocator Locator
        {
            get
            {
                return Resources["Locator"] as ViewModelLocator;
            }
        }

        private BackgroundTaskDeferral appServiceDeferral = null;

        private MediaExtensionManager m_mediaExtensionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            m_mediaExtensionManager = new MediaExtensionManager();
            m_mediaExtensionManager.RegisterByteStreamHandler("Unigram.Native.OpusByteStreamHandler", ".ogg", "audio/ogg");

            FileUtils.CreateTemporaryFolder();

            UnhandledException += async (s, args) =>
            {
                args.Handled = true;
                await new MessageDialog(args.Message ?? "Error", args.Exception?.ToString() ?? "Error").ShowAsync();
            };

#if !DEBUG

            HockeyClient.Current.Configure("7d36a4260af54125bbf6db407911ed3b",
                new TelemetryConfiguration()
                {
                    EnableDiagnostics = true,
                    Collectors = WindowsCollectors.Metadata |
                                 WindowsCollectors.Session |
                                 WindowsCollectors.UnhandledException
                });

#endif
        }

        /////// <summary>
        /////// Initializes the app service on the host process 
        /////// </summary>
        ////protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        ////{
        ////    base.OnBackgroundActivated(args);

        ////    if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails)
        ////    {
        ////        appServiceDeferral = args.TaskInstance.GetDeferral();
        ////        AppServiceTriggerDetails details = args.TaskInstance.TriggerDetails as AppServiceTriggerDetails;
        ////        Connection = details.AppServiceConnection;
        ////    }
        ////    else if (args.TaskInstance.TriggerDetails is RawNotification)
        ////    {
        ////        var task = new NotificationTask();
        ////        task.Run(args.TaskInstance);
        ////    }
        ////    else if (args.TaskInstance.TriggerDetails is ToastNotificationActionTriggerDetail)
        ////    {
        ////        // TODO: upgrade the task to take advanges from in-process execution.
        ////        var task = new InteractiveTask();
        ////        task.Run(args.TaskInstance);
        ////    }
        ////}

        public override Task OnInitializeAsync(IActivatedEventArgs args)
        {
            Execute.Initialize();
            Locator.Configure();
            return base.OnInitializeAsync(args);
        }

        public override Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            //NavigationService.Navigate(typeof(BlankPage1));
            //return Task.CompletedTask;

            ModalDialog.ModalBackground = (SolidColorBrush)Resources["ContentDialogLightDismissOverlayBackground"];
            ModalDialog.ModalBackground = new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00));
            ModalDialog.CanBackButtonDismiss = true;
            ModalDialog.DisableBackButtonWhenModal = false;

            var timer = Stopwatch.StartNew();

            if (SettingsHelper.IsAuthorized)
            {
                //MTProtoService.Current.CurrentUserId = SettingsHelper.UserId;

                var share = args as ShareTargetActivatedEventArgs;
                var voice = args as VoiceCommandActivatedEventArgs;

                if (share != null)
                {
                    ShareOperation = share.ShareOperation;
                    NavigationService.Navigate(typeof(Views.ShareTargetPage));
                }
                else if (voice != null)
                {
                    SpeechRecognitionResult speechResult = voice.Result;
                    string command = speechResult.RulePath[0];

                    if (command == "ShowAllDialogs")
                    {
                        NavigationService.Navigate(typeof(Views.MainPage));
                    }
                    if (command == "ShowSpecificDialog")
                    {
                        //#TODO: Fix that this'll open a specific dialog
                        NavigationService.Navigate(typeof(Views.MainPage));
                    }
                    else
                    {
                        NavigationService.Navigate(typeof(Views.MainPage));
                    }
                }
                else
                {
                    var activate = args as ToastNotificationActivatedEventArgs;
                    var launch = activate?.Argument ?? null;

                    NavigationService.Navigate(typeof(Views.MainPage), launch);

                    timer.Stop();
                    Debug.WriteLine($"LAUNCH TIME: {timer.Elapsed}");
                }
            }
            else
            {
                NavigationService.Navigate(typeof(Views.Login.LoginWelcomePage));
            }

            ShowStatusBar();
            ColourTitleBar();
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Windows.Foundation.Size(320, 500));
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            Task.Run(() => OnStartSync());
            return Task.CompletedTask;
        }

        private async void OnStartSync()
        {
            await Toast.RegisterBackgroundTasks();

            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
            ToastNotificationManager.History.Clear();

            try
            {
                // Prepare stuff for Cortana
                var localVoiceCommands = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///VoiceCommands/VoiceCommands.xml"));
                await VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(localVoiceCommands);
            }
            catch { }
        }

        public override void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            var updatesService = UnigramContainer.Current.ResolveType<IUpdatesService>();
            updatesService.LoadStateAndUpdate(() => { });

            base.OnResuming(s, e, previousExecutionState);
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            //DefaultPhotoConverter.BitmapContext.Clear();

            var cacheService = UnigramContainer.Current.ResolveType<ICacheService>();
            cacheService.TryCommit();

            var updatesService = UnigramContainer.Current.ResolveType<IUpdatesService>();
            updatesService.SaveState();
            updatesService.CancelUpdating();

            return base.OnSuspendingAsync(s, e, prelaunchActivated);
        }

        // Methods
        private void ShowStatusBar()
        {
            // Show StatusBar on Win10 Mobile, in theme of the pass
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();

                var bgcolor = Application.Current.Resources["SystemControlBackgroundChromeMediumBrush"] as SolidColorBrush;

                // Background
                statusBar.BackgroundColor = bgcolor.Color;
                statusBar.BackgroundOpacity = 1;

                // Branding colour
                //statusBar.BackgroundColor = Color.FromArgb(255, 54, 173, 225);
                //statusBar.ForegroundColor = Colors.White;
                //statusBar.BackgroundOpacity = 1;
            }
        }

        private void ColourTitleBar()
        {
            try
            {
                // Changes to the titlebar (colour, and such)
                ApplicationViewTitleBar titlebar = ApplicationView.GetForCurrentView().TitleBar;
                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = false;

                // Accent Color
                var accentBrush = Application.Current.Resources["SystemControlHighlightAccentBrush"] as SolidColorBrush;
                var titleBrush = Application.Current.Resources["TelegramBackgroundTitlebarBrush"] as SolidColorBrush;
                var subtitleBrush = Application.Current.Resources["TelegramBackgroundSubtitleBarBrush"] as SolidColorBrush;

                // Foreground
                titlebar.ButtonForegroundColor = Colors.White;
                titlebar.ButtonHoverForegroundColor = Colors.White;
                titlebar.ButtonInactiveForegroundColor = Colors.LightGray;
                titlebar.ButtonPressedForegroundColor = Colors.White;
                titlebar.ForegroundColor = Colors.White;
                titlebar.InactiveForegroundColor = Colors.LightGray;

                // Background
                titlebar.BackgroundColor = titleBrush.Color;
                titlebar.ButtonBackgroundColor = titleBrush.Color;

                titlebar.InactiveBackgroundColor = subtitleBrush.Color;
                titlebar.ButtonInactiveBackgroundColor = subtitleBrush.Color;

                titlebar.ButtonHoverBackgroundColor = Helpers.ColorHelper.ChangeShade(titleBrush.Color, -0.06f);
                titlebar.ButtonPressedBackgroundColor = Helpers.ColorHelper.ChangeShade(titleBrush.Color, -0.09f);

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

    public class AppState
    {
        public IEnumerable<TLMessage> ForwardMessages { get; set; }
    }
}
