using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Controls;
using Unigram.Native;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Media.SpeechRecognition;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class TLWindowContext : WindowContext
    {
        private readonly Window _window;
        private readonly int _id;

        private readonly ILifetimeService _lifetime;

        private PlaceholderImageHelper _placeholderHelper;

        public TLWindowContext(Window window, int id)
            : base(window)
        {
            _id = id;

            _window = window;
            _window.Activated += OnActivated;

            _lifetime = TLContainer.Current.Lifetime;

            _placeholderHelper = PlaceholderImageHelper.GetForCurrentView();

            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            UpdateTitleBar();

            var app = App.Current as App;
            app.UISettings.ColorValuesChanged += UISettings_ColorValuesChanged;

            _window.CoreWindow.Closed += (s, e) =>
            {
                try
                {
                    _placeholderHelper = null;
                    app.UISettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
                }
                catch { }
            };
            _window.Closed += (s, e) =>
            {
                try
                {
                    _placeholderHelper = null;
                    app.UISettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
                }
                catch { }
            };
        }

        public int Id => _id;

        #region UI

        private async void UISettings_ColorValuesChanged(UISettings sender, object args)
        {
            await _window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateTitleBar);
        }

        /// <summary>
        /// Update the Title and Status Bars colors.
        /// </summary>
        public void UpdateTitleBar()
        {
            Color background;
            Color foreground;
            Color buttonHover;
            Color buttonPressed;

            var app = App.Current as App;
            var current = app.UISettings.GetColorValue(UIColorType.Background);

            // Apply buttons feedback based on Light or Dark theme
            if (SettingsService.Current.Appearance.CurrentTheme.HasFlag(TelegramTheme.Dark) || (SettingsService.Current.Appearance.CurrentTheme.HasFlag(TelegramTheme.Default) && current == Colors.Black))
            {
                background = Color.FromArgb(255, 31, 31, 31);
                foreground = Colors.White;
                buttonHover = Color.FromArgb(25, 255, 255, 255);
                buttonPressed = Color.FromArgb(51, 255, 255, 255);
            }
            else if (SettingsService.Current.Appearance.CurrentTheme.HasFlag(TelegramTheme.Light) || (SettingsService.Current.Appearance.CurrentTheme.HasFlag(TelegramTheme.Default) && current == Colors.White))
            {
                background = Color.FromArgb(255, 230, 230, 230);
                foreground = Colors.Black;
                buttonHover = Color.FromArgb(25, 0, 0, 0);
                buttonPressed = Color.FromArgb(51, 0, 0, 0);
            }

            // Desktop Title Bar
            var titleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            // Background
            titleBar.BackgroundColor = background;
            titleBar.InactiveBackgroundColor = background;

            // Foreground
            titleBar.ForegroundColor = foreground;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;

            // Buttons
            //titleBar.ButtonBackgroundColor = background;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
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

        #endregion

        private bool? _apiAvailable;

        private CoreWindowActivationMode _activationMode;
        public CoreWindowActivationMode ActivationMode
        {
            get
            {
                _apiAvailable = _apiAvailable ?? ApiInformation.IsReadOnlyPropertyPresent("Windows.UI.Core.CoreWindow", "ActivationMode");

                if (_apiAvailable == true)
                {
                    return _window.CoreWindow.ActivationMode;
                }

                return _activationMode;
            }
        }

        public ContactPanel ContactPanel { get; private set; }

        public void SetContactPanel(ContactPanel panel)
        {
            ContactPanel = panel;
        }

        public bool IsContactPanel()
        {
            return ContactPanel != null;
        }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            _activationMode = e.WindowActivationState == CoreWindowActivationState.Deactivated
                ? CoreWindowActivationMode.Deactivated
                : CoreWindowActivationMode.ActivatedInForeground;
        }



        private INavigationService _service;
        private IActivatedEventArgs _args;

        public void SetActivatedArgs(IActivatedEventArgs args, INavigationService service)
        {
            _args = args;
            _service = service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId(_lifetime.ActiveItem.Id.ToString());

            UseActivatedArgs(args, service, _lifetime.ActiveItem.ProtoService.GetAuthorizationState());
        }

        private async void UseActivatedArgs(IActivatedEventArgs args, INavigationService service, AuthorizationState state)
        {
            try
            {
                switch (state)
                {
                    case AuthorizationStateReady ready:
                        //App.Current.NavigationService.Navigate(typeof(Views.MainPage));
                        UseActivatedArgs(args, service);
                        break;
                    case AuthorizationStateWaitPhoneNumber waitPhoneNumber:
                        Execute.Initialize();
                        service.Navigate(service.CurrentPageType != null ? typeof(Views.SignIn.SignInPage) : typeof(Views.IntroPage));
                        break;
                    case AuthorizationStateWaitCode waitCode:
                        service.Navigate(waitCode.IsRegistered ? typeof(Views.SignIn.SignInSentCodePage) : typeof(Views.SignIn.SignUpPage));
                        break;
                    case AuthorizationStateWaitPassword waitPassword:
                        if (!string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern))
                        {
                            await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.RestoreEmailSent, waitPassword.RecoveryEmailAddressPattern), Strings.Resources.AppName, Strings.Resources.OK);
                        }

                        service.Navigate(typeof(Views.SignIn.SignInPasswordPage));
                        break;
                }
            }
            catch { }
        }

        private async void UseActivatedArgs(IActivatedEventArgs args, INavigationService service)
        {
            if (service == null)
            {
                service = WindowContext.GetForCurrentView().NavigationServices.FirstOrDefault();
            }

            if (service == null || args == null)
            {
                return;
            }

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

                App.ShareOperation = share.ShareOperation;
                App.DataPackage = package.GetView();

                var options = new Windows.System.LauncherOptions();
                options.TargetApplicationPackageFamilyName = Package.Current.Id.FamilyName;

                await Windows.System.Launcher.LaunchUriAsync(new Uri("tg://"), options);
            }
            else if (args is VoiceCommandActivatedEventArgs voice)
            {
                Execute.Initialize();

                SpeechRecognitionResult speechResult = voice.Result;
                string command = speechResult.RulePath[0];

                if (command == "ShowAllDialogs")
                {
                    service.NavigateToMain(null);
                }
                if (command == "ShowSpecificDialog")
                {
                    //#TODO: Fix that this'll open a specific dialog
                    service.NavigateToMain(null);
                }
                else
                {
                    service.NavigateToMain(null);
                }
            }
            else if (args is ContactPanelActivatedEventArgs contact)
            {
                SetContactPanel(contact.ContactPanel);

                var backgroundBrush = Application.Current.Resources["TelegramTitleBarBackgroundBrush"] as SolidColorBrush;
                contact.ContactPanel.HeaderColor = backgroundBrush.Color;

                var annotationStore = await ContactManager.RequestAnnotationStoreAsync(ContactAnnotationStoreAccessType.AppAnnotationsReadWrite);
                var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);
                if (store != null && annotationStore != null)
                {
                    try
                    {
                        var full = await store.GetContactAsync(contact.Contact.Id);
                        if (full == null)
                        {
                            ContactPanelFallback(service);
                        }
                        else
                        {
                            var annotations = await annotationStore.FindAnnotationsForContactAsync(full);

                            var first = annotations.FirstOrDefault();
                            if (first == null)
                            {
                                ContactPanelFallback(service);
                            }
                            else
                            {
                                var remote = first.RemoteId;
                                if (int.TryParse(remote.Substring(1), out int userId))
                                {
                                    var response = await _lifetime.ActiveItem.ProtoService.SendAsync(new CreatePrivateChat(userId, false));
                                    if (response is Chat chat)
                                    {
                                        service.NavigateToChat(chat);
                                    }
                                }
                                else
                                {
                                    ContactPanelFallback(service);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if ((uint)ex.HResult == 0x80004004)
                        {
                            ContactPanelFallback(service);
                        }
                    }
                }
                else
                {
                    ContactPanelFallback(service);
                }
            }
            else if (args is ProtocolActivatedEventArgs protocol)
            {
                Execute.Initialize();

                if (App.ShareOperation != null)
                {
                    App.ShareOperation.ReportCompleted();
                    App.ShareOperation = null;
                }

                if (service?.Frame?.Content is MainPage page)
                {
                    page.Activate(protocol.Uri);
                }
                else
                {
                    service.NavigateToMain(protocol.Uri.ToString());
                }
            }
            //else if (args is CommandLineActivatedEventArgs commandLine && TryParseCommandLine(commandLine, out int id, out bool test))
            //{

            //}
            else
            {
                Execute.Initialize();

                var activate = args as ToastNotificationActivatedEventArgs;
                var launched = args as LaunchActivatedEventArgs;
                var launch = activate?.Argument ?? launched?.Arguments;

                if (service?.Frame?.Content is MainPage page)
                {
                    page.Activate(launch);
                }
                else
                {
                    service.NavigateToMain(launch);
                }
            }
        }

        private void ContactPanelFallback(INavigationService service)
        {
            if (service == null)
            {
                return;
            }

            var hyper = new Hyperlink();
            hyper.NavigateUri = new Uri("ms-settings:privacy-contacts");
            hyper.Inlines.Add(new Run { Text = "Settings" });

            var text = new TextBlock();
            text.Padding = new Thickness(12);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.TextWrapping = TextWrapping.Wrap;
            text.TextAlignment = TextAlignment.Center;
            text.Inlines.Add(new Run { Text = "This app is not able to access your contacts. Go to " });
            text.Inlines.Add(hyper);
            text.Inlines.Add(new Run { Text = " to check the contacts privacy settings." });

            var page = new ContentControl();
            page.VerticalAlignment = VerticalAlignment.Center;
            page.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            page.Content = text;

            service.Frame.Content = page;
        }

        public void Handle(ISessionService session, UpdateAuthorizationState update)
        {
            if (!session.IsActive)
            {
                return;
            }

            Dispatcher.Dispatch(() =>
            {
                var root = NavigationServices.FirstOrDefault(x => x.SessionId == session.Id && x.FrameFacade.FrameId == $"{session.Id}") as IHandle<UpdateAuthorizationState>;
                if (root != null)
                {
                    root.Handle(update);
                }
            });

            //await _window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
            //    _service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId($"{session.Id}");
            //    UseActivatedArgs(_args, _service, update.AuthorizationState);
            //});
        }

        public void Handle(ISessionService session, UpdateConnectionState update)
        {
            if (!session.IsActive)
            {
                return;
            }

            Dispatcher.Dispatch(() =>
            {
                foreach (var service in NavigationServices)
                {
                    if (service.SessionId == session.Id && service.IsInMainView)
                    {
                        switch (update.State)
                        {
                            case ConnectionStateWaitingForNetwork waitingForNetwork:
                                ShowStatus(Strings.Resources.WaitingForNetwork);
                                break;
                            case ConnectionStateConnecting connecting:
                                ShowStatus(Strings.Resources.Connecting);
                                break;
                            case ConnectionStateConnectingToProxy connectingToProxy:
                                ShowStatus(Strings.Resources.ConnectingToProxy);
                                break;
                            case ConnectionStateUpdating updating:
                                ShowStatus(Strings.Resources.Updating);
                                break;
                            case ConnectionStateReady ready:
                                HideStatus();
                                return;
                        }

                        break;
                    }
                }
            });
        }

        private async void ShowStatus(string text)
        {
            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().Title = text;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                StatusBar.GetForCurrentView().ProgressIndicator.Text = text;
                await StatusBar.GetForCurrentView().ProgressIndicator.ShowAsync();
            }
        }

        private async void HideStatus()
        {
            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().Title = string.Empty;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                StatusBar.GetForCurrentView().ProgressIndicator.Text = string.Empty;
                await StatusBar.GetForCurrentView().ProgressIndicator.HideAsync();
            }
        }



        //private static Dictionary<int, WindowContext> _windowContext = new Dictionary<int, WindowContext>();
        //public static WindowContext GetForCurrentView()
        //{
        //    var id = Windows.UI.ViewManagement.ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
        //    if (_windowContext.TryGetValue(id, out WindowContext value))
        //    {
        //        return value;
        //    }

        //    var context = new WindowContext(null, id);
        //    _windowContext[id] = context;

        //    return context;
        //}

        public static new TLWindowContext GetForCurrentView()
        {
            return WindowContext.GetForCurrentView() as TLWindowContext;
        }
    }
}
