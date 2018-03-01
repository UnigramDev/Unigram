using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.Helpers;
using Template10.Services.NavigationService;
using Unigram.Services;
using Unigram.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
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

            _window.CoreWindow.Closed += (s, e) =>
            {
                _aggregator.Unsubscribe(this);
            };
            _window.Closed += (s, e) =>
            {
                _aggregator.Unsubscribe(this);
            };
        }

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
            _service = service;

            UseActivatedArgs(args, service, _protoService.GetAuthorizationState());
        }

        private void UseActivatedArgs(IActivatedEventArgs args, INavigationService service, AuthorizationState state)
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
                        service.Navigate(typeof(Views.SignIn.SignInSentCodePage));
                        break;
                    case AuthorizationStateWaitPassword waitPassword:
                        service.Navigate(typeof(Views.SignIn.SignInPasswordPage));
                        break;
                }
            }
            catch { }
        }

        private async void UseActivatedArgs(IActivatedEventArgs args, INavigationService service)
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
                    var full = await store.GetContactAsync(contact.Contact.Id);
                    if (full == null)
                    {
                        service.NavigateToMain(null);
                    }
                    else
                    {
                        var annotations = await annotationStore.FindAnnotationsForContactAsync(full);

                        var first = annotations.FirstOrDefault();
                        if (first == null)
                        {
                            service.NavigateToMain(null);
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
                                service.NavigateToMain(null);
                            }
                        }
                    }
                }
                else
                {
                    service.NavigateToMain(null);
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
                        ApplicationView.GetForCurrentView().Title = Strings.Resources.WaitingForNetwork;
                        break;
                    case ConnectionStateConnecting connecting:
                        ApplicationView.GetForCurrentView().Title = Strings.Resources.Connecting;
                        break;
                    case ConnectionStateConnectingToProxy connectingToProxy:
                        ApplicationView.GetForCurrentView().Title = Strings.Resources.ConnectingToProxy;
                        break;
                    case ConnectionStateUpdating updating:
                        ApplicationView.GetForCurrentView().Title = Strings.Resources.Updating;
                        break;
                    case ConnectionStateReady ready:
                        ApplicationView.GetForCurrentView().Title = string.Empty;
                        return;
                }
            });
        }



        private static Dictionary<int, WindowContext> _windowContext = new Dictionary<int, WindowContext>();
        public static WindowContext GetForCurrentView()
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WindowContext value))
            {
                return value;
            }

            var context = new WindowContext(UnigramContainer.Current.ResolveType<IProtoService>(), UnigramContainer.Current.ResolveType<IEventAggregator>());
            _windowContext[id] = context;

            return context;
        }
    }
}
