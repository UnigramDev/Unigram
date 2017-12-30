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
using Unigram.Views;
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
using Unigram.Common;
using Windows.Media;
using System.IO;
using Template10.Services.NavigationService;
using Unigram.Views.SignIn;
using Windows.UI.Core;
using Unigram.Converters;
using Windows.Foundation.Metadata;
using Windows.ApplicationModel.Core;
using System.Collections;
using Telegram.Api.TL;
using System.Collections.Generic;
using Unigram.Core.Services;
using Template10.Controls;
using Windows.Foundation;
using Windows.ApplicationModel.Contacts;
using Telegram.Api.Aggregator;
using Unigram.Controls;
using Unigram.Views.Users;
using System.Linq;
using Telegram.Logs;
using Windows.Media.Playback;
using Windows.UI.StartScreen;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Resources;
using Unigram.Services;

namespace Unigram
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : BootStrapper
    {
        public static ShareOperation ShareOperation { get; set; }
        public static DataPackageView DataPackage { get; set; }

        public static AppServiceConnection Connection { get; private set; }

        public static AppInMemoryState InMemoryState { get; } = new AppInMemoryState();

        private readonly UISettings _uiSettings;

        public UISettings UISettings => _uiSettings;

        public static event TypedEventHandler<CoreDispatcher, AcceleratorKeyEventArgs> AcceleratorKeyActivated;

        public ViewModelLocator Locator
        {
            get
            {
                return Resources["Locator"] as ViewModelLocator;
            }
        }

        public static MediaPlayer Playback { get; } = new MediaPlayer();

        private BackgroundTaskDeferral appServiceDeferral = null;

        private MediaExtensionManager m_mediaExtensionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            if (ApplicationSettings.Current.RequestedTheme != ElementTheme.Default)
            {
                RequestedTheme = ApplicationSettings.Current.RequestedTheme == ElementTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            }

            CustomXamlResourceLoader.Current = new XamlResourceLoader();

            InitializeComponent();

            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += ColorValuesChanged;

            m_mediaExtensionManager = new MediaExtensionManager();
            m_mediaExtensionManager.RegisterByteStreamHandler("Unigram.Native.OpusByteStreamHandler", ".ogg", "audio/ogg");

            if (SettingsHelper.SwitchAccount >= 0)
            {
                SettingsHelper.SelectedAccount = SettingsHelper.SwitchAccount;
                SettingsHelper.SwitchAccount = -1;
            }

            FileUtils.CreateTemporaryFolder();

            InactivityHelper.Detected += Inactivity_Detected;

            UnhandledException += async (s, args) =>
            {
                args.Handled = true;

                try
                {
                    await new TLMessageDialog(args.Exception?.ToString() ?? string.Empty, "Unhandled exception").ShowQueuedAsync();
                }
                catch { }
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

        private void Inactivity_Detected(object sender, EventArgs e)
        {
            Execute.BeginOnUIThread(() =>
            {
                var passcode = UnigramContainer.Current.ResolveType<IPasscodeService>();
                if (passcode != null && UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                {
                    passcode.Lock();
                    ShowPasscode();
                }
            });
        }

        private static volatile bool _passcodeShown;
        public static async void ShowPasscode()
        {
            if (_passcodeShown)
            {
                return;
            }

            _passcodeShown = true;

            var dialog = new PasscodePage();
            var result = await dialog.ShowQueuedAsync();

            _passcodeShown = false;
        }

        public static bool IsActive { get; private set; }
        public static bool IsVisible { get; private set; }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            IsActive = e.WindowActivationState != CoreWindowActivationState.Deactivated;
            HandleActivated(e.WindowActivationState != CoreWindowActivationState.Deactivated);
        }

        private void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            IsVisible = e.Visible;
            HandleActivated(e.Visible);

            //if (e.Visible)
            //{
            //    Log.Write("Window_VisibilityChanged");

            //    Task.Run(() => Locator.LoadStateAndUpdate());
            //}
            //else
            //{
            //    var cacheService = UnigramContainer.Current.ResolveType<ICacheService>();
            //    if (cacheService != null)
            //    {
            //        cacheService.TryCommit();
            //    }

            //    var updatesService = UnigramContainer.Current.ResolveType<IUpdatesService>();
            //    if (updatesService != null)
            //    {
            //        updatesService.SaveState();
            //        updatesService.CancelUpdating();
            //    }
            //}

            var passcode = UnigramContainer.Current.ResolveType<IPasscodeService>();
            if (passcode != null)
            {
                if (e.Visible && passcode.IsLockscreenRequired)
                {
                    ShowPasscode();
                }
                else
                {
                    if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Touch)
                    {
                        passcode.CloseTime = DateTime.Now;
                    }
                    else
                    {
                        passcode.CloseTime = DateTime.Now.AddYears(1);
                    }
                }
            }
        }

        private void HandleActivated(bool active)
        {
            var aggregator = UnigramContainer.Current.ResolveType<ITelegramEventAggregator>();
            aggregator.Publish(active ? "Window_Activated" : "Window_Deactivated");

            if (active)
            {
                var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>();
                protoService.RaiseSendStatus(false);
            }
            else
            {
                var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>();
                protoService.RaiseSendStatus(true);
            }
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

        public override UIElement CreateRootElement(IActivatedEventArgs e)
        {
            var navigationFrame = new Frame();
            var navigationService = NavigationServiceFactory(BackButton.Ignore, ExistingContent.Include, navigationFrame) as NavigationService;
            navigationService.SerializationService = TLSerializationService.Current;

            return navigationFrame;
        }

        public override Task OnInitializeAsync(IActivatedEventArgs args)
        {
            Locator.Configure();

            var passcode = UnigramContainer.Current.ResolveType<IPasscodeService>();
            if (passcode != null && passcode.IsEnabled)
            {
                passcode.Lock();
                InactivityHelper.Initialize(passcode.AutolockTimeout);
            }

            if (Window.Current != null)
            {
                Execute.Initialize();

                Window.Current.Activated -= Window_Activated;
                Window.Current.Activated += Window_Activated;
                Window.Current.VisibilityChanged -= Window_VisibilityChanged;
                Window.Current.VisibilityChanged += Window_VisibilityChanged;
                Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
                Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

                UpdateBars();
                ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

                Theme.Current.Update();
                NotifyThemeChanged();
            }

            return base.OnInitializeAsync(args);
        }

        public override async Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            if (SettingsHelper.IsAuthorized)
            {
                if (args is ShareTargetActivatedEventArgs share)
                {
                    var package = new DataPackage();
                    var operation = share.ShareOperation.Data;
                    if (operation.Contains(StandardDataFormats.ApplicationLink))
                    {
                        package.SetApplicationLink(await operation.GetApplicationLinkAsync());
                    }
                    if (operation.Contains(StandardDataFormats.Bitmap))
                    {
                        package.SetBitmap(await operation.GetBitmapAsync());
                    }
                    //if (operation.Contains(StandardDataFormats.Html))
                    //{
                    //    package.SetHtmlFormat(await operation.GetHtmlFormatAsync());
                    //}
                    //if (operation.Contains(StandardDataFormats.Rtf))
                    //{
                    //    package.SetRtf(await operation.GetRtfAsync());
                    //}
                    if (operation.Contains(StandardDataFormats.StorageItems))
                    {
                        package.SetStorageItems(await operation.GetStorageItemsAsync());
                    }
                    if (operation.Contains(StandardDataFormats.Text))
                    {
                        package.SetText(await operation.GetTextAsync());
                    }
                    //if (operation.Contains(StandardDataFormats.Uri))
                    //{
                    //    package.SetUri(await operation.GetUriAsync());
                    //}
                    if (operation.Contains(StandardDataFormats.WebLink))
                    {
                        package.SetWebLink(await operation.GetWebLinkAsync());
                    }

                    ShareOperation = share.ShareOperation;
                    DataPackage = package.GetView();

                    var options = new LauncherOptions();
                    options.TargetApplicationPackageFamilyName = Package.Current.Id.FamilyName;

                    await Launcher.LaunchUriAsync(new Uri("tg://"), options);
                }
                else if (args is VoiceCommandActivatedEventArgs voice)
                {
                    Execute.Initialize();

                    SpeechRecognitionResult speechResult = voice.Result;
                    string command = speechResult.RulePath[0];

                    if (command == "ShowAllDialogs")
                    {
                        NavigationService.NavigateToMain(null);
                    }
                    if (command == "ShowSpecificDialog")
                    {
                        //#TODO: Fix that this'll open a specific dialog
                        NavigationService.NavigateToMain(null);
                    }
                    else
                    {
                        NavigationService.NavigateToMain(null);
                    }
                }
                else if (args is ContactPanelActivatedEventArgs contact)
                {
                    var backgroundBrush = Application.Current.Resources["TelegramBackgroundTitlebarBrush"] as SolidColorBrush;
                    contact.ContactPanel.HeaderColor = backgroundBrush.Color;

                    var annotationStore = await ContactManager.RequestAnnotationStoreAsync(ContactAnnotationStoreAccessType.AppAnnotationsReadWrite);
                    var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);
                    if (store != null && annotationStore != null)
                    {
                        var full = await store.GetContactAsync(contact.Contact.Id);
                        if (full == null)
                        {
                            NavigationService.NavigateToMain(null);
                        }
                        else
                        {
                            var annotations = await annotationStore.FindAnnotationsForContactAsync(full);

                            var first = annotations.FirstOrDefault();
                            if (first == null)
                            {
                                NavigationService.NavigateToMain(null);
                            }
                            else
                            {
                                var remote = first.RemoteId;
                                if (int.TryParse(remote.Substring(1), out int userId))
                                {
                                    NavigationService.Navigate(typeof(DialogPage), new TLPeerUser { UserId = userId });
                                }
                                else
                                {
                                    NavigationService.NavigateToMain(null);
                                }
                            }
                        }
                    }
                    else
                    {
                        NavigationService.NavigateToMain(null);
                    }
                }
                else if (args is ProtocolActivatedEventArgs protocol)
                {
                    Execute.Initialize();

                    if (ShareOperation != null)
                    {
                        ShareOperation.ReportCompleted();
                        ShareOperation = null;
                    }

                    if (NavigationService?.Frame?.Content is MainPage page)
                    {
                        page.Activate(protocol.Uri);
                    }
                    else
                    {
                        NavigationService.NavigateToMain(protocol.Uri.ToString());
                    }
                }
                else
                {
                    Execute.Initialize();

                    var activate = args as ToastNotificationActivatedEventArgs;
                    var launched = args as LaunchActivatedEventArgs;
                    var launch = activate?.Argument ?? launched?.Arguments;

                    if (NavigationService?.Frame?.Content is MainPage page)
                    {
                        page.Activate(launch);
                    }
                    else
                    {
                        NavigationService.NavigateToMain(launch);
                    }
                }
            }
            else
            {
                Execute.Initialize();

                NavigationService.Navigate(typeof(IntroPage));
            }

            Window.Current.Activated -= Window_Activated;
            Window.Current.Activated += Window_Activated;
            Window.Current.VisibilityChanged -= Window_VisibilityChanged;
            Window.Current.VisibilityChanged += Window_VisibilityChanged;
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

            UpdateBars();
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            Theme.Current.Update();
            NotifyThemeChanged();

            Task.Run(() => OnStartSync());
            //return Task.CompletedTask;
        }

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (AcceleratorKeyActivated is MulticastDelegate multicast)
            {
                var list = multicast.GetInvocationList();
                for (int i = list.Length - 1; i >= 0; i--)
                {
                    var result = list[i].DynamicInvoke(sender, args);
                    if (args.Handled)
                    {
                        return;
                    }
                }
            }
        }

        private async void OnStartSync()
        {
            //#if DEBUG
            await VoIPConnection.Current.ConnectAsync();
            //#endif

            await Toast.RegisterBackgroundTasks();

            BadgeUpdateManager.CreateBadgeUpdaterForApplication("App").Clear();
            TileUpdateManager.CreateTileUpdaterForApplication("App").Clear();
            ToastNotificationManager.History.Clear("App");

            if (SettingsHelper.UserId > 0 && ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 2) && JumpList.IsSupported())
            {
                var current = await JumpList.LoadCurrentAsync();
                current.SystemGroupKind = JumpListSystemGroupKind.None;
                current.Items.Clear();

                var cloud = JumpListItem.CreateWithArguments(string.Format("from_id={0}", SettingsHelper.UserId), Strings.Android.SavedMessages);
                cloud.Logo = new Uri("ms-appx:///Assets/JumpList/SavedMessages/SavedMessages.png");

                current.Items.Add(cloud);

                await current.SaveAsync();
            }

#if !DEBUG && !PREVIEW
            Execute.BeginOnThreadPool(async () =>
            {
                await new AppUpdateService().CheckForUpdatesAsync();
            });
#endif

            //if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            //{
            //    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            //}

            try
            {
                // Prepare stuff for Cortana
                var localVoiceCommands = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///VoiceCommands/VoiceCommands.xml"));
                await VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(localVoiceCommands);
            }
            catch { }
        }

        public override async void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            Log.Write("OnResuming");

            if (SettingsHelper.IsAuthorized)
            {
                var updatesService = UnigramContainer.Current.ResolveType<IUpdatesService>();
                updatesService.LoadStateAndUpdate(() => { });
            }

            //#if DEBUG
            await VoIPConnection.Current.ConnectAsync();
            //#endif

            base.OnResuming(s, e, previousExecutionState);
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            var cacheService = UnigramContainer.Current.ResolveType<ICacheService>();
            if (cacheService != null)
            {
                cacheService.TryCommit();
            }

            var updatesService = UnigramContainer.Current.ResolveType<IUpdatesService>();
            if (updatesService != null)
            {
                updatesService.SaveState();
                updatesService.CancelUpdating();
            }

            Log.Write("OnSuspendingAsync");

            return base.OnSuspendingAsync(s, e, prelaunchActivated);
        }

        private void ColorValuesChanged(UISettings sender, object args)
        {
            Execute.BeginOnUIThread(() => UpdateBars());
        }

        /// <summary>
        /// Update the Title and Status Bars colors.
        /// </summary>
        private void UpdateBars()
        {
            Color background;
            Color foreground;
            Color buttonHover;
            Color buttonPressed;

            var current = _uiSettings.GetColorValue(UIColorType.Background);

            // Apply buttons feedback based on Light or Dark theme
            if (ApplicationSettings.Current.CurrentTheme == ElementTheme.Dark || current == Colors.Black)
            {
                background = Color.FromArgb(255, 31, 31, 31);
                foreground = Colors.White;
                buttonHover = Color.FromArgb(255, 53, 53, 53);
                buttonPressed = Color.FromArgb(255, 76, 76, 76);
            }
            else if (ApplicationSettings.Current.CurrentTheme == ElementTheme.Light || current == Colors.White)
            {
                background = Color.FromArgb(255, 230, 230, 230);
                foreground = Colors.Black;
                buttonHover = Color.FromArgb(255, 207, 207, 207);
                buttonPressed = Color.FromArgb(255, 184, 184, 184);
            }

            // Desktop Title Bar
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = false;

            // Background
            titleBar.BackgroundColor = background;
            titleBar.InactiveBackgroundColor = background;

            // Foreground
            titleBar.ForegroundColor = foreground;
            titleBar.ButtonForegroundColor = foreground;

            // Buttons
            titleBar.ButtonBackgroundColor = background;
            titleBar.ButtonInactiveBackgroundColor = background;

            // Buttons feedback
            titleBar.ButtonPressedBackgroundColor = buttonPressed;
            titleBar.ButtonHoverBackgroundColor = buttonHover;

            // Mobile Status Bar
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundColor = background;
                statusBar.ForegroundColor = foreground;
                statusBar.BackgroundOpacity = 1;
            }
        }

        //private void Window_Activated(object sender, WindowActivatedEventArgs e)
        //{
        //    ((SolidColorBrush)Resources["TelegramBackgroundTitlebarBrush"]).Color = e.WindowActivationState != CoreWindowActivationState.Deactivated ? ((SolidColorBrush)Resources["TelegramBackgroundTitlebarBrushBase"]).Color : ((SolidColorBrush)Resources["TelegramBackgroundTitlebarBrushDeactivated"]).Color;
        //}

        public static void NotifyThemeChanged()
        {
            var frame = Window.Current.Content as Frame;
            if (frame == null)
            {
                return;
            }

            var current = App.Current as App;
            var theme = current.UISettings.GetColorValue(UIColorType.Background);

            frame.RequestedTheme = ApplicationSettings.Current.CurrentTheme == ElementTheme.Dark || (ApplicationSettings.Current.CurrentTheme == ElementTheme.Default && theme.R == 0 && theme.G == 0 && theme.B == 0) ? ElementTheme.Light : ElementTheme.Dark;
            frame.RequestedTheme = ApplicationSettings.Current.CurrentTheme;

            //var dark = (bool)App.Current.Resources["IsDarkTheme"];

            //frame.RequestedTheme = dark ? ElementTheme.Light : ElementTheme.Dark;
            //frame.RequestedTheme = ElementTheme.Default;
        }
    }

    public class AppInMemoryState
    {
        public IEnumerable<TLMessage> ForwardMessages { get; set; }

        public TLKeyboardButtonSwitchInline SwitchInline { get; set; }
        public TLUser SwitchInlineBot { get; set; }

        public string SendMessage { get; set; }
        public bool SendMessageUrl { get; set; }

        public int? NavigateToMessage { get; set; }
        public string NavigateToAccessToken { get; set; }
    }
}
