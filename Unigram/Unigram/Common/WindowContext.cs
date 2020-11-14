﻿using System;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Popups;
using Unigram.Views.SignIn;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Media.SpeechRecognition;
using Windows.UI;
using Windows.UI.Core;
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

        public TLWindowContext(Window window, int id)
            : base(window)
        {
            _id = id;

            _window = window;

            _lifetime = TLContainer.Current.Lifetime;

            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

            UpdateTitleBar();

            window.Activated += OnActivated;
        }

        private static readonly object _activeLock = new object();
        public static TLWindowContext ActiveWindow { get; private set; }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            lock (_activeLock)
            {
                if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
                {
                    ActiveWindow = this;
                }
                else if (ActiveWindow == this)
                {
                    ActiveWindow = null;
                }
            }
        }

        public int Id => _id;

        #region UI

        /// <summary>
        /// Update the Title and Status Bars colors.
        /// </summary>
        public void UpdateTitleBar()
        {
            //Color background;
            Color foreground;
            Color buttonHover;
            Color buttonPressed;

            // Apply buttons feedback based on Light or Dark theme
            var theme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            if (theme == ApplicationTheme.Dark)
            {
                //background = Color.FromArgb(255, 43, 43, 43);
                foreground = Colors.White;
                buttonHover = Color.FromArgb(25, 255, 255, 255);
                buttonPressed = Color.FromArgb(51, 255, 255, 255);
            }
            else if (theme == ApplicationTheme.Light)
            {
                //background = Color.FromArgb(255, 230, 230, 230);
                foreground = Colors.Black;
                buttonHover = Color.FromArgb(25, 0, 0, 0);
                buttonPressed = Color.FromArgb(51, 0, 0, 0);
            }

            // Desktop Title Bar
            var titleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            // Background
            //titleBar.BackgroundColor = background;
            //titleBar.InactiveBackgroundColor = background;

            // Foreground
            titleBar.ForegroundColor = foreground;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;

            // Buttons
            //titleBar.ButtonBackgroundColor = background;
            //titleBar.ButtonInactiveBackgroundColor = background;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // Buttons feedback
            titleBar.ButtonPressedBackgroundColor = buttonPressed;
            titleBar.ButtonHoverBackgroundColor = buttonHover;
        }

        #endregion

        public CoreWindowActivationMode ActivationMode => _window.CoreWindow.ActivationMode;

        public ContactPanel ContactPanel { get; private set; }

        public void SetContactPanel(ContactPanel panel)
        {
            ContactPanel = panel;
        }

        public bool IsContactPanel()
        {
            return ContactPanel != null;
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
                    case AuthorizationStateWaitOtherDeviceConfirmation waitOtherDeviceConfirmation:
                        service.Navigate(service.CurrentPageType != null ? typeof(SignInPage) : typeof(IntroPage));
                        break;
                    case AuthorizationStateWaitCode waitCode:
                        service.Navigate(typeof(SignInSentCodePage));
                        break;
                    case AuthorizationStateWaitRegistration waitRegistration:
                        service.Navigate(typeof(SignUpPage));
                        break;
                    case AuthorizationStateWaitPassword waitPassword:
                        if (!string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern))
                        {
                            await MessagePopup.ShowAsync(string.Format(Strings.Resources.RestoreEmailSent, waitPassword.RecoveryEmailAddressPattern), Strings.Resources.AppName, Strings.Resources.OK);
                        }

                        service.Navigate(typeof(SignInPasswordPage));
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

                try
                {
                    var operation = share.ShareOperation.Data;
                    if (operation.AvailableFormats.Contains(StandardDataFormats.ApplicationLink))
                    {
                        package.SetApplicationLink(await operation.GetApplicationLinkAsync());
                    }
                    if (operation.AvailableFormats.Contains(StandardDataFormats.Bitmap))
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
                    if (operation.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                    {
                        package.SetStorageItems(await operation.GetStorageItemsAsync());
                    }
                    if (operation.AvailableFormats.Contains(StandardDataFormats.Text))
                    {
                        package.SetText(await operation.GetTextAsync());
                    }
                    //if (operation.Contains(StandardDataFormats.Uri))
                    //{
                    //    package.SetUri(await operation.GetUriAsync());
                    //}
                    if (operation.AvailableFormats.Contains(StandardDataFormats.WebLink))
                    {
                        package.SetWebLink(await operation.GetWebLinkAsync());
                    }
                }
                catch { }

                var query = "tg://";

                var contactId = await ContactsService.GetContactIdAsync(share.ShareOperation.Contacts.FirstOrDefault());
                if (contactId is int userId)
                {
                    var response = await _lifetime.ActiveItem.ProtoService.SendAsync(new CreatePrivateChat(userId, false));
                    if (response is Chat chat)
                    {
                        query = $"ms-contact-profile://meh?ContactRemoteIds=u" + userId;
                        App.DataPackages[chat.Id] = package.GetView();
                    }
                    else
                    {
                        App.DataPackages[0] = package.GetView();
                    }
                }
                else
                {
                    App.DataPackages[0] = package.GetView();
                }

                App.ShareOperation = share.ShareOperation;
                App.ShareWindow = _window;

                var options = new Windows.System.LauncherOptions();
                options.TargetApplicationPackageFamilyName = Package.Current.Id.FamilyName;

                try
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(query), options);
                }
                catch
                {
                    // It's too early?
                }
            }
            else if (args is VoiceCommandActivatedEventArgs voice)
            {
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

                if (Application.Current.Resources.TryGet("PageHeaderBackgroundBrush", out SolidColorBrush backgroundBrush))
                {
                    contact.ContactPanel.HeaderColor = backgroundBrush.Color;
                }

                var contactId = await ContactsService.GetContactIdAsync(contact.Contact.Id);
                if (contactId is int userId)
                {
                    var response = await _lifetime.ActiveItem.ProtoService.SendAsync(new CreatePrivateChat(userId, false));
                    if (response is Chat chat)
                    {
                        service.NavigateToChat(chat);
                    }
                    else
                    {
                        ContactPanelFallback(service);
                    }
                }
                else
                {
                    ContactPanelFallback(service);
                }
            }
            else if (args is ProtocolActivatedEventArgs protocol)
            {
                if (service?.Frame?.Content is MainPage page)
                {
                    page.Activate(protocol.Uri.ToString());
                }
                else
                {
                    service.NavigateToMain(protocol.Uri.ToString());
                }

                if (App.ShareOperation != null)
                {
                    try
                    {
                        App.ShareOperation.ReportCompleted();
                        App.ShareOperation = null;
                    }
                    catch { }
                }

                if (App.ShareWindow != null)
                {
                    try
                    {
                        await App.ShareWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            App.ShareWindow.Close();
                            App.ShareWindow = null;
                        });
                    }
                    catch { }
                }
            }
            else if (args is FileActivatedEventArgs file)
            {
                if (service?.Frame?.Content is MainPage page)
                {
                    //page.Activate(launch);
                }
                else
                {
                    service.NavigateToMain(string.Empty);
                }

                await new ThemePreviewPopup(file.Files[0].Path).ShowQueuedAsync();
            }
            else
            {
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
