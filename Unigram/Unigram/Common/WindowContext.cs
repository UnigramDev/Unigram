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
using Unigram.Services;
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
    public class WindowContext : IHandle<UpdateAuthorizationState>, IHandle<UpdateConnectionState>
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        private readonly Window _window;

        public WindowContext(IProtoService protoService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _aggregator.Subscribe(this);

            _window = Window.Current;
            _window.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
            _window.Activated += OnActivated;

            var app = App.Current as App;
            app.UISettings.ColorValuesChanged += UISettings_ColorValuesChanged;

            _window.CoreWindow.Closed += (s, e) =>
            {
                try
                {
                    app.UISettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
                }
                catch { }

                _aggregator.Unsubscribe(this);
            };
            _window.Closed += (s, e) =>
            {
                try
                {
                    app.UISettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
                }
                catch { }

                _aggregator.Unsubscribe(this);
            };
        }

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
            if (ApplicationSettings.Current.CurrentTheme == ElementTheme.Dark || (ApplicationSettings.Current.CurrentTheme == ElementTheme.Default && current == Colors.Black))
            {
                background = Color.FromArgb(255, 31, 31, 31);
                foreground = Colors.White;
                buttonHover = Color.FromArgb(255, 53, 53, 53);
                buttonPressed = Color.FromArgb(255, 76, 76, 76);
            }
            else if (ApplicationSettings.Current.CurrentTheme == ElementTheme.Light || (ApplicationSettings.Current.CurrentTheme == ElementTheme.Default && current == Colors.White))
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
            titleBar.ButtonHoverForegroundColor = foreground;

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

        #endregion

        public CoreWindowActivationState ActivationState { get; private set; }

        public ContactPanel ContactPanel { get; private set; }

        public void SetContactPanel(ContactPanel panel)
        {
            ContactPanel = panel;
        }

        public bool IsContactPanel()
        {
            return ContactPanel != null;
        }

        public event TypedEventHandler<CoreDispatcher, AcceleratorKeyEventArgs> AcceleratorKeyActivated;

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

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            ActivationState = e.WindowActivationState;
        }



        private INavigationService _service;
        private IActivatedEventArgs _args;

        public void SetActivatedArgs(IActivatedEventArgs args, INavigationService service)
        {
            _args = args;
            _service = service = WindowWrapper.Current().NavigationServices.FirstOrDefault();

            UseActivatedArgs(args, service, _protoService.GetAuthorizationState());
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
                        service.Navigate(typeof(Views.IntroPage));
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
                service = WindowWrapper.Current().NavigationServices.FirstOrDefault();
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
                                    var response = await _protoService.SendAsync(new CreatePrivateChat(userId, false));
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

        public async void Handle(UpdateAuthorizationState update)
        {
            await _window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UseActivatedArgs(_args, _service, update.AuthorizationState);
            });
        }

        public async void Handle(UpdateConnectionState update)
        {
            await _window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
            });
        }

        private async void ShowStatus(string text)
        {
            ApplicationView.GetForCurrentView().Title = text;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                StatusBar.GetForCurrentView().ProgressIndicator.Text = text;
                await StatusBar.GetForCurrentView().ProgressIndicator.ShowAsync();
            }
        }

        private async void HideStatus()
        {
            ApplicationView.GetForCurrentView().Title = string.Empty;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                StatusBar.GetForCurrentView().ProgressIndicator.Text = string.Empty;
                await StatusBar.GetForCurrentView().ProgressIndicator.HideAsync();
            }
        }



        private static Dictionary<int, WindowContext> _windowContext = new Dictionary<int, WindowContext>();
        public static WindowContext GetForCurrentView()
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WindowContext value))
            {
                return value;
            }

            var context = new WindowContext(UnigramContainer.Current.Resolve<IProtoService>(), UnigramContainer.Current.Resolve<IEventAggregator>());
            _windowContext[id] = context;

            return context;
        }
    }
}
