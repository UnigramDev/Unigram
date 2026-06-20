//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Core.Preview;
using Windows.UI.StartScreen;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Views
{
    public partial record WebAppAgeVerificationCompletedEventArgs(bool Passed, double Age);

    public sealed partial class WebAppPage : UserControlEx, IPopupHost
    {
        private readonly IClientService _clientService;
        private readonly SecondaryNavigationService _navigationService;
        private readonly IEventAggregator _aggregator;

        private WebAppStorage _deviceStorage;
        private WebAppStorage _secureStorage;

        private readonly OpenUrlSource _source;
        private readonly User _botUser;
        private readonly AttachmentMenuBot _menuBot;

        private readonly InternalLinkType _sourceLink;
        private readonly string _buttonText;

        private readonly long _launchId;

        private readonly long _gameChatId;
        private readonly long _gameMessageId;

        private bool _fullscreen;

        private bool _blockingAction;
        private bool _closeNeedConfirmation;

        private bool _sentData;

        private bool _settingsVisible;

        private CompositionAnimation _placeholderShimmer;
        private ShapeVisual _placeholderVisual;

        // TODO: constructor should take a function and URL should be loaded asynchronously
        public WebAppPage(IClientService clientService, INavigationService navigationService, User botUser, WebAppUrl url, long launchId = 0, AttachmentMenuBot menuBot = null, OpenUrlSource source = null, InternalLinkType sourceLink = null, string buttonText = null)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = new SecondaryNavigationService(clientService.Session, navigationService, WindowContext.Current);
            _aggregator = clientService.Session.Resolve<IEventAggregator>();

            _aggregator.Subscribe<UpdateWebAppMessageSent>(this, Handle)
                .Subscribe<UpdatePaymentCompleted>(Handle)
                .Subscribe<UpdateChatJoinResult>(Handle);

            _botUser = botUser;
            _launchId = launchId;
            _menuBot = menuBot;
            _source = source;
            _sourceLink = sourceLink != null ? new InternalLinkTypeMainWebApp(botUser.ActiveUsername(), string.Empty, new WebAppOpenModeFullSize()) : null;
            _buttonText = buttonText;

            TitleText.Text = botUser.FullName();
            Photo.Source = ProfilePictureSource.User(clientService, botUser);

            View.Navigate(url.Url);

            var panel = ElementComposition.GetElementVisual(BottomBarPanel);
            panel.Clip = panel.Compositor.CreateInsetClip(0, 96, 0, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(TitleText, true);

            WindowContext.Current.SetTitleBar(TitleBar, true);
            Window.Current.Activated += OnActivated;

            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;
            ApplicationView.GetForCurrentView().Consolidated += OnConsolidated;
            ApplicationView.GetForCurrentView().VisibleBoundsChanged += OnVisibleBoundsChanged;

            LoadPlaceholder();
        }

        private bool _ageVerificationRaised;
        public event EventHandler<WebAppAgeVerificationCompletedEventArgs> AgeVerificationCompleted;

        private async void LoadPlaceholder()
        {
            _clientService.TryGetUserFull(_botUser.Id, out UserFullInfo fullInfo);
            fullInfo ??= await _clientService.SendAsync(new GetUserFullInfo(_botUser.Id)) as UserFullInfo;

            if (fullInfo?.BotInfo == null)
            {
                return;
            }

            var header = RequestedTheme == ElementTheme.Light
                ? fullInfo.BotInfo.WebAppHeaderLightColor
                : fullInfo.BotInfo.WebAppHeaderDarkColor;
            var background = RequestedTheme == ElementTheme.Light
                ? fullInfo.BotInfo.WebAppBackgroundLightColor
                : fullInfo.BotInfo.WebAppBackgroundDarkColor;

            if (header != -1)
            {
                ProcessHeaderColor(header.ToColor());
            }

            if (background != -1)
            {
                ProcessBackgroundColor(background.ToColor());
            }

            var response = await _clientService.SendAsync(new GetWebAppPlaceholder(_botUser.Id));
            if (response is Outline outline && outline.Paths.Count > 0)
            {
                _placeholderShimmer = CompositionPathParser.ParseThumbnail(512, 512, outline.Paths, out _placeholderVisual);
                ElementCompositionPreview.SetElementChildVisual(PlaceholderPanel, _placeholderVisual);
            }
        }

        public bool AreTheSame(InternalLinkType internalLink)
        {
            if (_sourceLink is InternalLinkTypeAttachmentMenuBot xMenu && internalLink is InternalLinkTypeAttachmentMenuBot yMenu)
            {
                return xMenu.TargetChat is TargetChatCurrent
                    && yMenu.TargetChat is TargetChatCurrent
                    && xMenu.BotUsername == yMenu.BotUsername
                    && xMenu.Url == xMenu.Url;
            }
            else if (_sourceLink is InternalLinkTypeMainWebApp xMain && internalLink is InternalLinkTypeMainWebApp yMain)
            {
                return xMain.BotUsername == yMain.BotUsername
                    && xMain.StartParameter == yMain.StartParameter;
            }
            else if (_sourceLink is InternalLinkTypeWebApp xWebApp && internalLink is InternalLinkTypeWebApp yWebApp)
            {
                return xWebApp.BotUsername == yWebApp.BotUsername
                    && xWebApp.WebAppShortName == yWebApp.WebAppShortName
                    && xWebApp.StartParameter == yWebApp.StartParameter;
            }

            return false;
        }

        public WebAppPage(IClientService clientService, INavigationService navigationService, User botUser, string url, string title, long gameChatId = 0, long gameMessageId = 0)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = new SecondaryNavigationService(clientService.Session, navigationService, WindowContext.Current);
            _aggregator = clientService.Session.Resolve<IEventAggregator>();

            _botUser = botUser;
            _gameChatId = gameChatId;
            _gameMessageId = gameMessageId;

            TitleText.Text = title;
            Photo.Source = ProfilePictureSource.User(clientService, botUser);

            View.Navigate(url);

            var panel = ElementComposition.GetElementVisual(BottomBarPanel);
            panel.Clip = panel.Compositor.CreateInsetClip(0, 96, 0, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(TitleText, true);

            WindowContext.Current.SetTitleBar(TitleBar, true);

            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;
            ApplicationView.GetForCurrentView().VisibleBoundsChanged += OnVisibleBoundsChanged;
        }

        #region IToastHost

        public void PopupOpened()
        {
            Window.Current.SetTitleBar(null);
        }

        public void PopupClosed()
        {
            Window.Current.SetTitleBar(TitleBar);
        }

        #endregion

        private void Handle(UpdateWebAppMessageSent update)
        {
            if (update.WebAppLaunchId == _launchId)
            {
                _closeNeedConfirmation = false;
                this.BeginOnUIThread(Close);
            }
        }

        private void Handle(UpdatePaymentCompleted update)
        {
            PostEvent("invoice_closed", "slug", update.Slug, "status", update.Status);
        }

        private void Handle(UpdateChatJoinResult update)
        {
            if (_source is OpenUrlSourceJoinChatRequest joinChatRequest && joinChatRequest.QueryId == update.QueryId && joinChatRequest.ChatId == update.ChatId)
            {
                _closeNeedConfirmation = false;
                this.BeginOnUIThread(Close);
            }
        }

        private bool _closed;

        private async void Close()
        {
            if (_closeNeedConfirmation)
            {
                var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.BotWebViewChangesMayNotBeSaved, _botUser.FirstName, Strings.BotWebViewCloseAnyway, Strings.Cancel, destructive: true);
                if (confirm == ContentDialogResult.Primary)
                {
                    _closeNeedConfirmation = false;
                    CloseImpl();
                }
            }
            else
            {
                CloseImpl();
            }
        }

        private void CloseImpl()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;

            if (WindowContext.Current != null)
            {
                _ = WindowContext.Current.ConsolidateAsync();
            }
            else
            {
                _ = ApplicationView.GetForCurrentView().TryConsolidateAsync();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_launchId != 0)
            {
                _clientService.Send(new CloseWebApp(_launchId));
            }

            View.Close();
        }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            PostEvent("visibility_changed", "is_visible", e.WindowActivationState != CoreWindowActivationState.Deactivated);
        }

        private async void OnCloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            if (_closeNeedConfirmation)
            {
                var deferral = e.GetDeferral();

                var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.BotWebViewChangesMayNotBeSaved, _botUser.FirstName, Strings.BotWebViewCloseAnyway, Strings.Cancel, destructive: true);
                if (confirm == ContentDialogResult.Primary)
                {
                    _closeNeedConfirmation = false;
                }
                else
                {
                    e.Handled = true;
                }

                deferral.Complete();
            }
        }

        private void OnConsolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            if (_ageVerificationRaised)
            {
                return;
            }

            AgeVerificationCompleted?.Invoke(this, new WebAppAgeVerificationCompletedEventArgs(false, 0));
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (_fullscreen != sender.IsFullScreenMode)
            {
                _fullscreen = sender.IsFullScreenMode;
                PostEvent("fullscreen_changed", "is_fullscreen", _fullscreen);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_mainButtonCollapsed && !_secondaryButtonCollapsed)
            {
                var main = ElementComposition.GetElementVisual(MainButtonPanel);
                var seco = ElementComposition.GetElementVisual(SecondaryButtonPanel);

                var compositor = main.Compositor;

                var half = BottomBarPanel.ActualSize.X / 2;
                var quart = half / 2;

                if (_secondaryButtonPosition == SecondaryButtonPosition.Left)
                {
                    main.Offset = new Vector3(half - quart, 48, 0);
                    main.Clip = compositor.CreateInsetClip(quart, 0, quart, 0);

                    seco.Offset = new Vector3(-quart, 48, 0);
                    seco.Clip = compositor.CreateInsetClip(quart, 0, quart, 0);
                }
                else if (_secondaryButtonPosition == SecondaryButtonPosition.Top)
                {
                    main.Offset = new Vector3(0, 48, 0);
                    seco.Offset = new Vector3(0, 0, 0);

                    seco.Clip = compositor.CreateInsetClip(0, 0, 0, 0);
                }
                else if (_secondaryButtonPosition == SecondaryButtonPosition.Right)
                {
                    main.Offset = new Vector3(-quart, 48, 0);
                    main.Clip = compositor.CreateInsetClip(quart, 0, quart, 0);

                    seco.Offset = new Vector3(half - quart, 48, 0);
                    seco.Clip = compositor.CreateInsetClip(quart, 0, quart, 0);
                }
                else if (_secondaryButtonPosition == SecondaryButtonPosition.Bottom)
                {
                    seco.Offset = new Vector3(0, 48, 0);
                    main.Offset = new Vector3(0, 0, 0);

                    seco.Clip = compositor.CreateInsetClip(0, 0, 0, 0);
                }
            }
        }

        private void View_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            PostViewportChanged();
        }

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            PostEvent("main_button_pressed");
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            PostEvent("secondary_button_pressed");
        }

        private void View_NewWindowRequested(object sender, WebViewerNewWindowRequestedEventArgs e)
        {
            MessageHelper.OpenUrl(_clientService, _navigationService, e.Url);
            e.Cancel = true;
        }

        private async void View_ScriptDialogOpening(object sender, WebViewerScriptDialogOpeningEventArgs args)
        {
            var deferral = args.GetDeferral();

            if (args.Kind == WebViewerScriptDialogKind.Prompt)
            {
                var popup = new InputPopup(InputPopupType.Text)
                {
                    Title = _botUser.FirstName,
                    Header = args.Message,
                    Text = args.DefaultText,
                    PrimaryButtonText = Strings.OK,
                    PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                    SecondaryButtonText = Strings.Cancel,
                    RequestedTheme = ActualTheme
                };

                var confirm = await popup.ShowQueuedAsync(XamlRoot);
                if (confirm == ContentDialogResult.Primary)
                {
                    args.ResultText = popup.Text;
                    args.Accept();
                }
            }
            else
            {
                var confirm = await MessagePopup.ShowAsync(XamlRoot, args.Message, _botUser.FirstName, Strings.OK, args.Kind == WebViewerScriptDialogKind.Confirm ? Strings.Cancel : string.Empty);
                if (confirm == ContentDialogResult.Primary)
                {
                    args.Accept();
                }
            }

            deferral.Complete();
        }

        private void View_Navigating(object sender, WebViewerNavigatingEventArgs e)
        {
            if (Uri.TryCreate(e.Url, UriKind.Absolute, out Uri uri))
            {
                var host = uri.Host;

                var splitHostName = uri.Host.Split('.');
                if (splitHostName.Length >= 2)
                {
                    host = splitHostName[^2] + "." +
                           splitHostName[^1];
                }

                if (host.Equals("t.me", StringComparison.OrdinalIgnoreCase))
                {
                    MessageHelper.OpenTelegramUrl(_clientService, _navigationService, uri);
                    e.Cancel = true;
                }
                else if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    e.Cancel = true;
                }
            }
        }

        private void View_Navigated(object sender, WebViewerNavigatedEventArgs e)
        {
            PostViewportChanged();
            PostThemeChanged();

            _placeholderShimmer = null;
            _placeholderVisual = null;

            PlaceholderPanel.Visibility = Visibility.Collapsed;
        }

        private void View_EventReceived(object sender, WebViewerEventReceivedEventArgs e)
        {
            ReceiveEvent(e.EventName, e.EventData);
        }

        private void ReceiveEvent(string eventName, JsonObject eventData)
        {
            Logger.Info(string.Format("{0}: {1}", eventName, eventData));

            if (eventName == "web_app_close")
            {
                _closeNeedConfirmation = false;
                Close();
            }
            else if (eventName == "web_app_data_send")
            {
                SendDataMessage(eventData);
            }
            else if (eventName == "web_app_switch_inline_query")
            {
                SwitchInlineQueryMessage(eventData);
            }
            else if (eventName == "web_app_setup_main_button")
            {
                ProcessMainButtonMessage(eventData);
            }
            else if (eventName == "web_app_setup_secondary_button")
            {
                ProcessSecondaryButtonMessage(eventData);
            }
            else if (eventName == "web_app_setup_back_button")
            {
                ProcessBackButtonMessage(eventData);
            }
            else if (eventName == "web_app_setup_settings_button")
            {
                ProcessSettingsButtonMessage(eventData);
            }
            else if (eventName == "web_app_request_theme")
            {
                PostThemeChanged();
            }
            else if (eventName == "web_app_request_viewport")
            {
                PostViewportChanged();
            }
            else if (eventName == "web_app_open_tg_link")
            {
                OpenInternalLink(eventData);
            }
            else if (eventName == "web_app_open_link")
            {
                OpenExternalLink(eventData);
            }
            else if (eventName == "web_app_open_invoice")
            {
                OpenInvoice(eventData);
            }
            else if (eventName == "web_app_open_popup")
            {
                OpenPopup(eventData);
            }
            else if (eventName == "web_app_request_write_access")
            {
                RequestWriteAccess();
            }
            else if (eventName == "web_app_request_phone")
            {
                RequestPhone();
            }
            else if (eventName == "web_app_invoke_custom_method")
            {
                InvokeCustomMethod(eventData);
            }
            else if (eventName == "web_app_setup_closing_behavior")
            {
                SetupClosingBehaviour(eventData);
            }
            else if (eventName == "web_app_read_text_from_clipboard")
            {
                RequestClipboardText(eventData);
            }
            else if (eventName == "web_app_set_header_color")
            {
                ProcessHeaderColor(eventData);
            }
            else if (eventName == "web_app_set_background_color")
            {
                ProcessBackgroundColor(eventData);
            }
            else if (eventName == "web_app_set_bottom_bar_color")
            {
                ProcessBottomBarColor(eventData);
            }
            else if (eventName == "web_app_share_to_story")
            {
                ProcessShareToStory(eventData);
            }
            else if (eventName == "web_app_request_fullscreen")
            {
                ProcessRequestFullScreen();
            }
            else if (eventName == "web_app_exit_fullscreen")
            {
                ProcessExitFullScreen();
            }
            else if (eventName == "web_app_check_home_screen")
            {
                ProcessCheckHomeScreen(eventData);
            }
            else if (eventName == "web_app_add_to_home_screen")
            {
                ProcessAddToHomeScreen(eventData);
            }
            else if (eventName == "web_app_set_emoji_status")
            {
                ProcessSetEmojiStatus(eventData);
            }
            else if (eventName == "web_app_send_prepared_message")
            {
                ProcessSendPreparedMessage(eventData);
            }
            else if (eventName == "web_app_request_file_download")
            {
                ProcessRequestFileDownload(eventData);
            }
            else if (eventName == "web_app_start_accelerometer")
            {
                PostEvent("accelerometer_failed", "error", "UNSUPPORTED");
            }
            else if (eventName == "web_app_start_device_orientation")
            {
                PostEvent("device_orientation_failed", "error", "UNSUPPORTED");
            }
            else if (eventName == "web_app_start_gyroscope")
            {
                PostEvent("gyroscope_failed", "error", "UNSUPPORTED");
            }
            else if (eventName == "web_app_verify_age")
            {
                ProcessVerifyAge(eventData);
            }
            else if (eventName == "web_app_device_storage_save_key")
            {
                if (_botUser == null) return;
                if (_deviceStorage == null) _deviceStorage = new WebAppStorage(_clientService, _botUser.Id, false);
                SetStorageKey(_deviceStorage, eventData, "device_storage_key_saved", "device_storage_failed");
            }
            else if (eventName == "web_app_device_storage_get_key")
            {
                if (_botUser == null) return;
                if (_deviceStorage == null) _deviceStorage = new WebAppStorage(_clientService, _botUser.Id, false);
                GetStorageKey(_deviceStorage, eventData, "device_storage_key_received", "device_storage_failed");
            }
            else if (eventName == "web_app_device_storage_clear")
            {
                if (_botUser == null) return;
                if (_deviceStorage == null) _deviceStorage = new WebAppStorage(_clientService, _botUser.Id, false);
                ClearStorageKey(_deviceStorage, eventData, "device_storage_cleared", "device_storage_failed");
            }
            else if (eventName == "web_app_secure_storage_save_key")
            {
                if (_botUser == null) return;
                if (_secureStorage == null) _secureStorage = new WebAppStorage(_clientService, _botUser.Id, true);
                SetStorageKey(_secureStorage, eventData, "secure_storage_key_saved", "secure_storage_failed");
            }
            else if (eventName == "web_app_secure_storage_get_key")
            {
                if (_botUser == null) return;
                if (_secureStorage == null) _secureStorage = new WebAppStorage(_clientService, _botUser.Id, true);
                GetStorageKey(_secureStorage, eventData, "secure_storage_key_received", "secure_storage_failed");
            }
            else if (eventName == "web_app_secure_storage_clear")
            {
                if (_botUser == null) return;
                if (_secureStorage == null) _secureStorage = new WebAppStorage(_clientService, _botUser.Id, true);
                ClearStorageKey(_secureStorage, eventData, "secure_storage_cleared", "secure_storage_cleared");
            }
            else if (eventName == "web_app_secure_storage_restore_key")
            {
                if (_botUser == null) return;
                if (_secureStorage == null) _secureStorage = new WebAppStorage(_clientService, _botUser.Id, true);
                RestoreStorageKey(_secureStorage, eventData, "secure_storage_key_restored", "secure_storage_cleared");
            }
            // Games
            else if (eventName == "share_game")
            {
                ProcessShareGame(false);
            }
            else if (eventName == "share_score")
            {
                ProcessShareGame(true);
            }
        }

        private void ProcessVerifyAge(JsonObject eventData)
        {
            var passed = eventData.GetNamedBoolean("passed", false);
            var age = eventData.GetNamedNumber("age", 0);
            // gender, string
            // genderProbability, double

            _ageVerificationRaised = true;
            AgeVerificationCompleted?.Invoke(this, new WebAppAgeVerificationCompletedEventArgs(passed, age));

            _navigationService.Switch();
        }

        private async void SetStorageKey(WebAppStorage storage, JsonObject eventData, String eventSuccess, String eventFail)
        {
            if (storage == null || _botUser == null) return;
            String req_id = "";
            try
            {
                req_id = eventData.GetNamedString("req_id");
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return;
            }
            String key;
            try
            {
                key = eventData.GetNamedString("key");
            }
            catch (Exception)
            {
                PostEvent(eventFail, "req_id", req_id, "error", "KEY_INVALID");
                return;
            }
            if (string.IsNullOrEmpty(key))
            {
                PostEvent(eventFail, "req_id", req_id, "error", "KEY_INVALID");
                return;
            }
            String value;
            try
            {
                var valueData = eventData.GetNamedValue("value");
                if (valueData.ValueType == JsonValueType.String)
                {
                    value = valueData.GetString();
                }
                else if (valueData.ValueType == JsonValueType.Null)
                {
                    value = null;
                }
                else
                {
                    PostEvent(eventFail, "req_id", req_id, "error", "VALUE_INVALID");
                    return;
                }
            }
            catch (Exception)
            {
                PostEvent(eventFail, "req_id", req_id, "error", "VALUE_INVALID");
                return;
            }
            try
            {
                await storage.SetKeyAsync(key, value);
            }
            catch (InvalidOperationException e)
            {
                PostEvent(eventFail, "req_id", req_id, "error", e.Message);
                return;
            }

            PostEvent(eventSuccess, "req_id", req_id);
        }

        private async void GetStorageKey(WebAppStorage storage, JsonObject eventData, String eventSuccess, String eventFail)
        {
            if (storage == null || _botUser == null) return;
            String req_id = "";
            try
            {
                req_id = eventData.GetNamedString("req_id");
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return;
            }
            String key;
            try
            {
                key = eventData.GetNamedString("key");
            }
            catch (Exception)
            {
                PostEvent(eventFail, "req_id", req_id, "error", "KEY_INVALID");
                return;
            }
            if (string.IsNullOrEmpty(key))
            {
                PostEvent(eventFail, "req_id", req_id, "error", "KEY_INVALID");
                return;
            }
            try
            {
                (String value, Boolean canRestore) = await storage.GetKeyAsync(key);
                if (storage.Secured && value == null)
                {
                    PostEvent(eventSuccess, "req_id", req_id, "value", value, "can_restore", canRestore);
                }
                else
                {
                    PostEvent(eventSuccess, "req_id", req_id, "value", value);
                }
            }
            catch (InvalidOperationException e)
            {
                PostEvent(eventFail, "req_id", req_id, "error", e.Message);
            }
        }

        private async void RestoreStorageKey(WebAppStorage storage, JsonObject eventData, String eventSuccess, String eventFail)
        {
            if (storage == null || _botUser == null) return;
            String req_id = "";
            try
            {
                req_id = eventData.GetNamedString("req_id");
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return;
            }
            String key;
            try
            {
                key = eventData.GetNamedString("key");
            }
            catch (Exception)
            {
                PostEvent(eventFail, "req_id", req_id, "error", "KEY_INVALID");
                return;
            }
            if (string.IsNullOrEmpty(key))
            {
                PostEvent(eventFail, "req_id", req_id, "error", "KEY_INVALID");
                return;
            }
            List<WebAppStorageConfig> storages;
            try
            {
                storages = await storage.GetStoragesWithKeyAsync(key);
            }
            catch (Exception e)
            {
                PostEvent(eventFail, "req_id", req_id, "error", e.Message);
                return;
            }
            if (storages.Empty())
            {
                PostEvent(eventFail, "req_id", req_id, "error", "RESTORE_UNAVAILABLE");
                return;
            }

            var popup = new WebAppRestorePopup(_clientService, _botUser, storages);

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary || popup.SelectedItem == null)
            {
                PostEvent(eventFail, "req_id", req_id, "error", "RESTORE_CANCELLED");
            }

            (String Value, bool) restoredValue;
            try
            {
                await storage.RestoreFromAsync(popup.SelectedItem.StorageId);
                restoredValue = await storage.GetKeyAsync(key);
            }
            catch (Exception e)
            {
                PostEvent(eventFail, "req_id", req_id, "error", e.Message);
                return;
            }

            PostEvent(eventSuccess, "req_id", req_id, "value", restoredValue.Value);
        }

        private async void ClearStorageKey(WebAppStorage storage, JsonObject eventData, String eventSuccess, String eventFail)
        {
            if (storage == null || _botUser == null) return;
            String req_id = "";
            try
            {
                req_id = eventData.GetNamedString("req_id");
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return;
            }
            try
            {
                await storage.ClearAsync();
            }
            catch (InvalidOperationException e)
            {
                PostEvent(eventFail, "req_id", req_id, "error", e.Message);
                return;
            }

            PostEvent(eventSuccess, "req_id", req_id);
        }

        private async void ProcessSendPreparedMessage(JsonObject eventData)
        {
            var preparedMessageId = eventData.GetNamedString("id", string.Empty);

            var response = await _clientService.SendAsync(new GetPreparedInlineMessage(_botUser.Id, preparedMessageId));
            if (response is PreparedInlineMessage prepared)
            {
                // TODO: is this correct?
                var response2 = await _clientService.SendAsync(new SendInlineQueryResultMessage(_clientService.Options.MyId, null, null, Constants.PreviewOnly, prepared.InlineQueryId, prepared.Result.GetId(), false));
                if (response2 is not Message message)
                {
                    PostEvent("prepared_message_failed", "error", "UNKNOWN_ERROR");
                    return;
                }

                var confirm2 = await _navigationService.ShowPopupAsync(new SendPreparedMessagePopup(_clientService, _navigationService, message, _botUser));
                if (confirm2 != ContentDialogResult.Primary)
                {
                    PostEvent("prepared_message_failed", "error", "USER_DECLINED");
                    return;
                }

                var confirm = await _navigationService.ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationSwitchInline(prepared, _botUser));
                if (confirm == ContentDialogResult.Primary)
                {
                    PostEvent("prepared_message_sent");
                }
                else
                {
                    PostEvent("prepared_message_failed", "error", "USER_DECLINED");
                }
            }
            else
            {
                PostEvent("prepared_message_failed", "error", "USER_DECLINED");
            }
        }

        private void ProcessRequestFileDownload(JsonObject eventData)
        {
            var url = eventData.GetNamedString("url", string.Empty);
            var fileName = eventData.GetNamedString("file_name", string.Empty);

            if (string.IsNullOrEmpty(fileName) || !Uri.TryCreate(url, UriKind.Absolute, out Uri result))
            {

            }
        }

        private void ProcessRequestFullScreen()
        {
            ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
        }

        private void ProcessExitFullScreen()
        {
            ApplicationView.GetForCurrentView().ExitFullScreenMode();
        }

        private async void ProcessSetEmojiStatus(JsonObject eventData)
        {
            var customEmoji = eventData.GetNamedString("custom_emoji_id", string.Empty);
            var expirationDate = eventData.GetNamedInt32("expiration_date", 0);

            if (string.IsNullOrEmpty(customEmoji) || !long.TryParse(customEmoji, out long customEmojiId))
            {
                PostEvent("emoji_status_failed", "error", "SUGGESTED_EMOJI_INVALID");
                return;
            }

            if (expirationDate != 0 && expirationDate < DateTime.Now.ToTimestamp())
            {
                PostEvent("emoji_status_failed", "error", "EXPIRATION_DATE_INVALID");
                return;
            }

            var response = await _clientService.SendAsync(new GetCustomEmojiStickers(new[] { customEmojiId }));
            if (response is not Stickers stickers || stickers.StickersValue.Count != 1)
            {
                PostEvent("emoji_status_failed", "error", "SUGGESTED_EMOJI_INVALID");
                return;
            }

            var popup = new EmojiStatusPopup(_clientService, _navigationService, _botUser.Id, stickers.StickersValue[0], expirationDate);

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                PostEvent("emoji_status_set");
            }
            else if (confirm == ContentDialogResult.Secondary)
            {
                PostEvent("emoji_status_failed", "error", "SERVER_ERROR");
            }
            else
            {
                PostEvent("emoji_status_failed", "error", "USER_DECLINED");
            }
        }

        private void ProcessCheckHomeScreen(JsonObject eventData)
        {
            if (_botUser.Type is not UserTypeBot { HasMainWebApp: true })
            {
                PostEvent("home_screen_checked", "status", "unsupported");
            }
            else if (SecondaryTile.Exists("web_app_" + _clientService.SessionId + "_" + _botUser.Id))
            {
                PostEvent("home_screen_checked", "status", "added");
            }
            else
            {
                PostEvent("home_screen_checked", "status", "missed");
            }
        }

        private void ProcessAddToHomeScreen(JsonObject eventData)
        {
            MenuItemAddToStartMenu();
        }

        private void ProcessShareGame(bool withMyScore)
        {
            this.ShowPopup(_clientService.Session, new ChooseChatsPopup(), new ChooseChatsConfigurationShareGame(_gameChatId, _gameMessageId, withMyScore));
        }

        private void ProcessShareToStory(JsonObject eventData)
        {
            _ = MessagePopup.ShowAsync(XamlRoot, Strings.WebAppShareStoryNotSupported, Strings.AppName, Strings.OK);
        }

        private async void RequestClipboardText(JsonObject eventData)
        {
            var requestId = eventData.GetNamedString("req_id", string.Empty);
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            var clipboard = ClipboardEx.TryGetContent();
            if (clipboard != null && clipboard.Contains(StandardDataFormats.Text) && _menuBot != null)
            {
                var text = await clipboard.GetTextAsync();
                PostEvent("clipboard_text_received", "req_id", requestId, "data", text);
            }
            else
            {
                PostEvent("clipboard_text_received", "req_id", requestId);
            }
        }

        private void SetupClosingBehaviour(JsonObject eventData)
        {
            _closeNeedConfirmation = eventData.GetNamedBoolean("need_confirmation", false);
        }

        private async void InvokeCustomMethod(JsonObject eventData)
        {
            var requestId = eventData.GetNamedString("req_id", string.Empty);
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            var method = eventData.GetNamedString("method");
            var parameters = eventData.GetNamedObject("params");

            var response = await _clientService.SendAsync(new SendWebAppCustomRequest(_botUser.Id, method, parameters.Stringify()));
            if (response is CustomRequestResult result)
            {
                PostEvent("custom_method_invoked", "req_id", requestId, "result", result.Result);
            }
            else if (response is Error error)
            {
                PostEvent("custom_method_invoked", "req_id", requestId, "error", error.Message);
            }
        }

        private string _headerColorKey;
        private string _backgroundColorKey;
        private string _bottomBarColorKey;

        private void ProcessHeaderColor(JsonObject eventData)
        {
            ProcessHeaderColor(ProcessColor(eventData, out _headerColorKey));
        }

        private void ProcessHeaderColor(Color? color)
        {
            if (color is Color c)
            {
                var luminance = 0.2126 * (c.R / 255d) + 0.7152 * (c.G / 255d) + 0.0722 * (c.B / 255d);
                var foreground = luminance > 0.5 ? Colors.Black : Colors.White;

                var brush = new SolidColorBrush(foreground);
                var theme = luminance > 0.5 ? ElementTheme.Light : ElementTheme.Dark;

                TitlePanel.Background = new SolidColorBrush(c);
                TitleText.Foreground = brush;
                BackButton.RequestedTheme = theme;
                MoreButton.RequestedTheme = theme;
                HideButton.RequestedTheme = theme;
            }
            else
            {
                TitlePanel.ClearValue(Panel.BackgroundProperty);
                TitleText.ClearValue(TextBlock.ForegroundProperty);
                BackButton.RequestedTheme = ElementTheme.Default;
                MoreButton.RequestedTheme = ElementTheme.Default;
                HideButton.RequestedTheme = ElementTheme.Default;
            }
        }

        private void ProcessBackgroundColor(JsonObject eventData)
        {
            ProcessBackgroundColor(ProcessColor(eventData, out _backgroundColorKey));
        }

        private void ProcessBackgroundColor(Color? color)
        {
            if (color is Color c)
            {
                BackgroundPanel.Background = new SolidColorBrush(c);
            }
            else
            {
                BackgroundPanel.Background = null;
            }
        }

        private void ProcessBottomBarColor(JsonObject eventData)
        {
            ProcessBottomBarColor(ProcessColor(eventData, out _bottomBarColorKey));
        }

        private void ProcessBottomBarColor(Color? color)
        {
            if (color is Color c)
            {
                BottomBarPanel.Background = new SolidColorBrush(c);
            }
            else
            {
                BottomBarPanel.Background = null;
            }
        }

        private Color? ProcessColor(JsonObject eventData, out string key)
        {
            if (eventData.ContainsKey("color"))
            {
                var colorValue = eventData.GetNamedString("color");
                var color = ParseColor(colorValue);

                key = null;
                return color;
            }
            else if (eventData.ContainsKey("color_key"))
            {
                var colorKey = eventData.GetNamedString("color_key");
                var color = colorKey switch
                {
                    "bg_color" => Theme.Current.Parameters.BackgroundColor.ToColor(),
                    "secondary_bg_color" => Theme.Current.Parameters.SecondaryBackgroundColor.ToColor(),
                    "bottom_bar_bg_color" => Theme.Current.Parameters.BottomBarBackgroundColor.ToColor(),
                    _ => new Color?(),
                };

                key = colorKey;
                return color;
            }

            key = null;
            return null;
        }

        private async void RequestPhone()
        {
            if (_blockingAction)
            {
                PostEvent("phone_requested", "status", "cancelled");
                return;
            }

            _blockingAction = true;

            var confirm = await MessagePopup.ShowAsync(XamlRoot, string.Format(Strings.AreYouSureShareMyContactInfoWebapp, _botUser.FullName()), Strings.ShareYouPhoneNumberTitle, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary && _clientService.TryGetUser(_clientService.Options.MyId, out User user))
            {
                var chat = await _clientService.SendAsync(new CreatePrivateChat(_botUser.Id, false)) as Chat;
                if (chat == null)
                {
                    _blockingAction = false;
                    PostEvent("phone_requested", "status", "cancelled");

                    return;
                }

                if (chat.BlockList is BlockListMain)
                {
                    await _clientService.SendAsync(new SetMessageSenderBlockList(new MessageSenderUser(_botUser.Id), null));
                }

                await _clientService.SendAsync(new SendMessage(chat.Id, null, null, null, new InputMessageContact(new Contact(user.PhoneNumber, user.FirstName, user.LastName, string.Empty, user.Id))));

                _blockingAction = false;
                PostEvent("phone_requested", "status", "sent");
            }
            else
            {
                _blockingAction = false;
                PostEvent("phone_requested", "status", "cancelled");
            }
        }

        private async void RequestWriteAccess()
        {
            if (_blockingAction)
            {
                PostEvent("write_access_requested", "status", "cancelled");
                return;
            }

            _blockingAction = true;

            var request = await _clientService.SendAsync(new CanBotSendMessages(_botUser.Id));
            if (request is Ok)
            {
                _blockingAction = false;
                PostEvent("write_access_requested", "status", "allowed");

                return;
            }

            var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.BotWebViewRequestWriteMessage, Strings.BotWebViewRequestWriteTitle, Strings.BotWebViewRequestAllow, Strings.BotWebViewRequestDontAllow);
            if (confirm == ContentDialogResult.Primary)
            {
                await _clientService.SendAsync(new AllowBotToSendMessages(_botUser.Id));

                _blockingAction = false;
                PostEvent("write_access_requested", "status", "allowed");
            }
            else
            {
                _blockingAction = false;
                PostEvent("write_access_requested", "status", "cancelled");
            }
        }

        private async void OpenPopup(JsonObject eventData)
        {
            var title = eventData.GetNamedString("title", string.Empty);
            var message = eventData.GetNamedString("message", string.Empty);
            var buttons = eventData.GetNamedArray("buttons");

            if (string.IsNullOrEmpty(message) || buttons.Empty())
            {
                return;
            }

            if (string.IsNullOrEmpty(title))
            {
                title = _botUser.FirstName;
            }

            var label = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            };

            Grid.SetColumnSpan(label, int.MaxValue);

            var panel = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 24
            };

            panel.RowDefinitions.Add(1, GridUnitType.Star);
            panel.RowDefinitions.Add(1, GridUnitType.Auto);
            panel.Children.Add(label);

            var popup = new ContentPopup
            {
                Title = title,
                Content = panel,
                ContentMaxWidth = 388,
            };

            void unloaded(object sender, RoutedEventArgs e)
            {
                PostEvent("popup_closed");
                popup.Unloaded -= unloaded;
            }

            popup.Unloaded += unloaded;

            void click(object sender, RoutedEventArgs e)
            {
                if (sender is Button button && button.CommandParameter is string id)
                {
                    PostEvent("popup_closed", "button_id", id);
                    button.Click -= click;
                }

                popup.Unloaded -= unloaded;
                popup.Hide();
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i].GetObject();

                var id = button.GetNamedString("id");
                var type = button.GetNamedString("type");
                var text = button.GetNamedString("text", string.Empty);

                var action = new Button
                {
                    Content = text,
                    CommandParameter = id,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                action.Click += click;

                switch (type)
                {
                    case "default":
                        action.Style = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
                        break;
                    case "destructive":
                        action.Style = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
                        break;
                    case "ok":
                        action.Content = Strings.OK;
                        break;
                    case "close":
                        action.Content = Strings.Close;
                        break;
                    case "cancel":
                        action.Content = Strings.Cancel;
                        break;
                }

                Grid.SetRow(action, 1);

                if (buttons.Count == 1)
                {
                    Grid.SetColumn(action, 1);
                    panel.ColumnDefinitions.Add(new ColumnDefinition());
                }
                else
                {
                    Grid.SetColumn(action, i);
                }

                panel.ColumnDefinitions.Add(new ColumnDefinition());
                panel.Children.Add(action);
            }

            await popup.ShowQueuedAsync(XamlRoot);
        }

        private void OpenInvoice(JsonObject eventData)
        {
            var value = eventData.GetNamedString("slug", string.Empty);
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            _navigationService.NavigateToInvoice(new InputInvoiceName(value));
        }

        private void OpenExternalLink(JsonObject eventData)
        {
            // Ignoring try_instant_view for now
            var value = eventData.GetNamedString("url", string.Empty);
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            MessageHelper.OpenUrl(null, null, value);
        }

        private void OpenInternalLink(JsonObject eventData)
        {
            var value = eventData.GetNamedString("path_full", string.Empty);
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            //Hide();
            MessageHelper.OpenUrl(_clientService, _navigationService, "https://t.me" + value);
        }

        private void PostViewportChanged()
        {
            PostEvent("viewport_changed", "height", View.ActualHeight, "is_state_stable", true, "is_expanded", true);
        }

        private void ProcessBackButtonMessage(JsonObject eventData)
        {
            ShowHideBackButton(eventData.GetNamedBoolean("is_visible", false));
        }

        private void ProcessSettingsButtonMessage(JsonObject eventData)
        {
            _settingsVisible = eventData.GetNamedBoolean("is_visible", false);
        }

        private bool _backButtonCollapsed = true;

        private void ShowHideBackButton(bool show)
        {
            if (_backButtonCollapsed != show)
            {
                return;
            }

            _backButtonCollapsed = !show;
            BackButton.Visibility = Visibility.Visible;

            var visual1 = ElementComposition.GetElementVisual(BackButton);
            var visual2 = ElementComposition.GetElementVisual(Photo);

            var batch = visual1.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                BackButton.Visibility = _backButtonCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var scale1 = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale1.InsertKeyFrame(show ? 1 : 0, Vector3.One);
            scale1.Duration = Constants.FastAnimation;

            var opacity1 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration = Constants.FastAnimation;

            var scale2 = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale2.InsertKeyFrame(show ? 0 : 1, Vector3.One);
            scale2.InsertKeyFrame(show ? 1 : 0, Vector3.Zero);
            scale2.Duration = Constants.FastAnimation;

            var opacity2 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration = Constants.FastAnimation;

            visual1.CenterPoint = new Vector3(24, 20, 0);
            visual2.CenterPoint = new Vector3(12);

            visual1.StartAnimation("Scale", scale1);
            visual1.StartAnimation("Opacity", opacity1);
            visual2.StartAnimation("Scale", scale2);
            visual2.StartAnimation("Opacity", opacity2);
            batch.End();
        }

        private Color? ParseColor(string color)
        {
            if (color.StartsWith("#") && int.TryParse(color.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexColor))
            {
                byte r = (byte)((hexColor & 0xff0000) >> 16);
                byte g = (byte)((hexColor & 0x00ff00) >> 8);
                byte b = (byte)(hexColor & 0x0000ff);

                return Color.FromArgb(0xFF, r, g, b);
            }

            return null;
        }

        private void ProcessMainButtonMessage(JsonObject eventData)
        {
            var is_visible = eventData.GetNamedBoolean("is_visible", false); // whether to show the button(false by default);
            var is_active = eventData.GetNamedBoolean("is_active", true); // whether the button is active(true by default);
            var is_progress_visible = eventData.GetNamedBoolean("is_progress_visible", false); // whether to show the loading process on the button(false by default);
            var text = eventData.GetNamedString("text", string.Empty); // text on the button(trim(text) should be non-empty, if empty, the button can be hidden);
            var color = eventData.GetNamedString("color", string.Empty); // background color of the button(by default button_colorfrom the theme);
            var text_color = eventData.GetNamedString("text_color", string.Empty); // text color on the button(by default button_text_colorfrom the theme).

            bool hasIcon = false;
            long customEmojiId = 0;
            if (eventData.TryGetValue("icon_custom_emoji_id", out IJsonValue icon_custom_emoji_id))
            {
                if (icon_custom_emoji_id.ValueType == JsonValueType.String)
                {
                    hasIcon = long.TryParse(icon_custom_emoji_id.GetString(), out customEmojiId);
                }
            }

            var hasText = !string.IsNullOrEmpty(text.Trim());

            if (is_visible && (hasIcon || hasText))
            {
                void SetColor(DependencyObject element, string value, DependencyProperty property)
                {
                    var color = ParseColor(value);
                    if (color is Color c)
                    {
                        element.SetValue(property, new SolidColorBrush(c));
                    }
                    else
                    {
                        element.ClearValue(property);
                    }
                }

                SetColor(MainButtonPanel, color, Grid.BackgroundProperty);
                SetColor(MainButton, text_color, ForegroundProperty);
                SetColor(MainProgress, text_color, Microsoft.UI.Xaml.Controls.ProgressRing.ForegroundProperty);

                if (hasIcon)
                {
                    var player = new CustomEmojiIcon();
                    player.Width = 20;
                    player.Height = 20;
                    player.FrameSize = new Size(20, 20);
                    player.Source = new CustomEmojiFileSource(_clientService, customEmojiId);
                    player.HorizontalAlignment = HorizontalAlignment.Left;
                    player.FlowDirection = FlowDirection.LeftToRight;
                    player.IsHitTestVisible = false;
                    player.Margin = new Thickness(0, -2, 0, -6);

                    var inline = new InlineUIContainer();
                    inline.Child = player;

                    // If the Span starts with a InlineUIContainer the RichTextBlock bugs and shows ellipsis
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(Icons.ZWNJ);
                    paragraph.Inlines.Add(inline);
                    paragraph.Inlines.Add(Icons.ZWNJ);

                    if (hasText)
                    {
                        paragraph.Inlines.Add(Icons.Space);
                        paragraph.Inlines.Add(text);
                    }

                    var textBlock = new RichTextBlock();
                    textBlock.IsTextSelectionEnabled = false;
                    textBlock.Blocks.Add(paragraph);

                    MainButton.Content = textBlock;
                }
                else
                {
                    MainButton.Content = text;
                }

                MainButton.IsEnabled = is_active || is_progress_visible;

                ShowHideProgress(is_progress_visible, MainButton, MainProgress, ref _mainProgressCollapsed);

                ShowHideButtons(true, !_secondaryButtonCollapsed, _secondaryButtonPosition);
            }
            else
            {
                ShowHideButtons(false, !_secondaryButtonCollapsed, _secondaryButtonPosition);
            }
        }

        private void ProcessSecondaryButtonMessage(JsonObject eventData)
        {
            var is_visible = eventData.GetNamedBoolean("is_visible", false); // whether to show the button(false by default);
            var is_active = eventData.GetNamedBoolean("is_active", true); // whether the button is active(true by default);
            var is_progress_visible = eventData.GetNamedBoolean("is_progress_visible", false); // whether to show the loading process on the button(false by default);
            var has_shine_effect = eventData.GetNamedBoolean("has_shine_effect", false);
            var text = eventData.GetNamedString("text", string.Empty); // text on the button(trim(text) should be non-empty, if empty, the button can be hidden);
            var color = eventData.GetNamedString("color", string.Empty); // background color of the button(by default button_colorfrom the theme);
            var text_color = eventData.GetNamedString("text_color", string.Empty); // text color on the button(by default button_text_colorfrom the theme).
            var position = eventData.GetNamedString("position", "left");
            var icon_custom_emoji_id = eventData.GetNamedString("icon_custom_emoji_id", string.Empty);

            var hasIcon = long.TryParse(icon_custom_emoji_id, out long customEmojiId);
            var hasText = !string.IsNullOrEmpty(text.Trim());

            if (is_visible && (hasIcon || hasText))
            {
                void SetColor(DependencyObject element, string value, DependencyProperty property)
                {
                    var color = ParseColor(value);
                    if (color is Color c)
                    {
                        element.SetValue(property, new SolidColorBrush(c));
                    }
                    else
                    {
                        element.ClearValue(property);
                    }
                }

                SetColor(SecondaryButtonPanel, color, Grid.BackgroundProperty);
                SetColor(SecondaryButton, text_color, ForegroundProperty);
                SetColor(SecondaryProgress, text_color, Microsoft.UI.Xaml.Controls.ProgressRing.ForegroundProperty);

                if (hasIcon)
                {
                    var player = new CustomEmojiIcon();
                    player.Width = 20;
                    player.Height = 20;
                    player.FrameSize = new Size(20, 20);
                    player.Source = new CustomEmojiFileSource(_clientService, customEmojiId);
                    player.HorizontalAlignment = HorizontalAlignment.Left;
                    player.FlowDirection = FlowDirection.LeftToRight;
                    player.IsHitTestVisible = false;
                    player.Margin = new Thickness(0, -2, 0, -6);

                    var inline = new InlineUIContainer();
                    inline.Child = player;

                    // If the Span starts with a InlineUIContainer the RichTextBlock bugs and shows ellipsis
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(Icons.ZWNJ);
                    paragraph.Inlines.Add(inline);
                    paragraph.Inlines.Add(Icons.ZWNJ);

                    if (hasText)
                    {
                        paragraph.Inlines.Add(Icons.Space);
                        paragraph.Inlines.Add(text);
                    }

                    var textBlock = new RichTextBlock();
                    textBlock.IsTextSelectionEnabled = false;
                    textBlock.Blocks.Add(paragraph);

                    SecondaryButton.Content = textBlock;
                }
                else
                {
                    SecondaryButton.Content = text;
                }

                SecondaryButton.IsEnabled = is_active || is_progress_visible;

                ShowHideProgress(is_progress_visible, SecondaryButton, SecondaryProgress, ref _secondaryProgressCollapsed);

                ShowHideButtons(!_mainButtonCollapsed, true, position switch
                {
                    "left" => SecondaryButtonPosition.Left,
                    "top" => SecondaryButtonPosition.Top,
                    "right" => SecondaryButtonPosition.Right,
                    "bottom" => SecondaryButtonPosition.Bottom,
                    _ => SecondaryButtonPosition.Left
                });
            }
            else
            {
                ShowHideButtons(!_mainButtonCollapsed, false, _secondaryButtonPosition);
            }
        }

        private bool _mainProgressCollapsed = true;
        private bool _secondaryProgressCollapsed = true;

        private void ShowHideProgress(bool show, UIElement button, UIElement progress, ref bool collapsed)
        {
            if (collapsed == show)
            {
                return;
            }

            collapsed = show;

            var visualShow = ElementComposition.GetElementVisual(show ? progress : button);
            var visualHide = ElementComposition.GetElementVisual(show ? button : progress);

            visualShow.CenterPoint = new Vector3(visualShow.Size / 2, 0);
            visualHide.CenterPoint = new Vector3(visualHide.Size / 2, 0);

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                button.Visibility = (button == MainButton ? _mainProgressCollapsed : _secondaryProgressCollapsed)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                progress.Visibility = (button == MainButton ? _mainProgressCollapsed : _secondaryProgressCollapsed)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var hide1 = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(0, new Vector3(1));
            hide1.InsertKeyFrame(1, new Vector3(0));

            var hide2 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            visualHide.StartAnimation("Scale", hide1);
            visualHide.StartAnimation("Opacity", hide2);

            var show1 = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            show1.InsertKeyFrame(1, new Vector3(1));
            show1.InsertKeyFrame(0, new Vector3(0));

            var show2 = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(1, 1);
            show2.InsertKeyFrame(0, 0);

            visualShow.StartAnimation("Scale", show1);
            visualShow.StartAnimation("Opacity", show2);
        }

        private bool _mainButtonCollapsed = true;
        private bool _secondaryButtonCollapsed = true;
        private SecondaryButtonPosition _secondaryButtonPosition;

        enum SecondaryButtonPosition
        {
            Left,
            Top,
            Right,
            Bottom
        }

        private void ShowHideButtons(bool main1, bool secondary1, SecondaryButtonPosition position)
        {
            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                MainButtonPanel.Visibility = _mainButtonCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                SecondaryButtonPanel.Visibility = _secondaryButtonCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            if (_mainButtonCollapsed != main1 && _secondaryButtonCollapsed != secondary1)
            {
                // Both buttons were already visible, position may have changed

                if (_secondaryButtonPosition == position || _secondaryButtonCollapsed || _mainButtonCollapsed)
                {
                    return;
                }

                _secondaryButtonPosition = position;

                var panel = ElementComposition.GetElementVisual(BottomBarPanel);
                var main = ElementComposition.GetElementVisual(MainButtonPanel);
                var seco = ElementComposition.GetElementVisual(SecondaryButtonPanel);

                var compositor = main.Compositor;

                panel.Clip ??= compositor.CreateInsetClip();
                main.Clip ??= compositor.CreateInsetClip();
                seco.Clip ??= compositor.CreateInsetClip();

                var mainTranslation = compositor.CreateVector3KeyFrameAnimation();
                var secoTranslation = compositor.CreateVector3KeyFrameAnimation();

                var panelClip = compositor.CreateScalarKeyFrameAnimation();
                var buttonClip = compositor.CreateScalarKeyFrameAnimation();

                var half = BottomBarPanel.ActualSize.X / 2;
                var quart = half / 2;

                if (position == SecondaryButtonPosition.Left)
                {
                    mainTranslation.InsertKeyFrame(1, new Vector3(half - quart, 48, 0));
                    secoTranslation.InsertKeyFrame(1, new Vector3(-quart, 48, 0));

                    panelClip.InsertKeyFrame(1, 48);
                    buttonClip.InsertKeyFrame(1, quart);
                }
                else if (position == SecondaryButtonPosition.Top)
                {
                    mainTranslation.InsertKeyFrame(1, new Vector3(0, 48, 0));
                    secoTranslation.InsertKeyFrame(1, new Vector3(0, 0, 0));

                    panelClip.InsertKeyFrame(1, 0);
                    buttonClip.InsertKeyFrame(1, 0);
                }
                else if (position == SecondaryButtonPosition.Right)
                {
                    secoTranslation.InsertKeyFrame(1, new Vector3(half - quart, 48, 0));
                    mainTranslation.InsertKeyFrame(1, new Vector3(-quart, 48, 0));

                    panelClip.InsertKeyFrame(1, 48);
                    buttonClip.InsertKeyFrame(1, quart);
                }
                else if (position == SecondaryButtonPosition.Bottom)
                {
                    secoTranslation.InsertKeyFrame(1, new Vector3(0, 48, 0));
                    mainTranslation.InsertKeyFrame(1, new Vector3(0, 0, 0));

                    panelClip.InsertKeyFrame(1, 0);
                    buttonClip.InsertKeyFrame(1, 0);
                }

                main.StartAnimation("Offset", mainTranslation);
                seco.StartAnimation("Offset", secoTranslation);

                main.Clip.StartAnimation("LeftInset", buttonClip);
                seco.Clip.StartAnimation("LeftInset", buttonClip);
                main.Clip.StartAnimation("RightInset", buttonClip);
                seco.Clip.StartAnimation("RightInset", buttonClip);

                panel.Clip.StartAnimation("TopInset", panelClip);
            }
            else
            {
                void ShowHideDouble(UIElement first, UIElement second, bool show, SecondaryButtonPosition Position)
                {
                    first.Visibility = Visibility.Visible;

                    var panel = ElementComposition.GetElementVisual(BottomBarPanel);
                    var main = ElementComposition.GetElementVisual(first);
                    var seco = ElementComposition.GetElementVisual(second);

                    var compositor = main.Compositor;

                    panel.Clip ??= compositor.CreateInsetClip();
                    main.Clip ??= compositor.CreateInsetClip();
                    seco.Clip ??= compositor.CreateInsetClip();

                    var mainTranslation = compositor.CreateVector3KeyFrameAnimation();
                    var secoTranslation = compositor.CreateVector3KeyFrameAnimation();

                    var panelClip = compositor.CreateScalarKeyFrameAnimation();
                    var buttonClip = compositor.CreateScalarKeyFrameAnimation();

                    var buttonOpacity = compositor.CreateScalarKeyFrameAnimation();

                    var half = BottomBarPanel.ActualSize.X / 2;
                    var quart = half / 2;

                    Canvas.SetZIndex(first, 0);
                    Canvas.SetZIndex(second, 1);

                    buttonOpacity.InsertKeyFrame(1, show ? 1 : 0);

                    if (Position == SecondaryButtonPosition.Left)
                    {
                        main.Offset = new Vector3(half - quart, 48, 0);
                        main.Clip = compositor.CreateInsetClip(quart, 0, quart, 0);

                        //mainTranslation.InsertKeyFrame(1, new Vector3(half - quart, 48, 0));
                        secoTranslation.InsertKeyFrame(1, new Vector3(show ? -quart : 0, 48, 0));

                        panelClip.InsertKeyFrame(1, 48);
                        buttonClip.InsertKeyFrame(1, show ? quart : 0);
                    }
                    else if (Position == SecondaryButtonPosition.Top)
                    {
                        mainTranslation.InsertKeyFrame(1, new Vector3(0, show ? 48 : 96, 0));
                        secoTranslation.InsertKeyFrame(1, new Vector3(0, show ? 0 : 48, 0));

                        panelClip.InsertKeyFrame(1, show ? 0 : 48);
                        buttonClip.InsertKeyFrame(1, 0);
                    }
                    else if (Position == SecondaryButtonPosition.Right)
                    {
                        main.Offset = new Vector3(-quart, 48, 0);
                        main.Clip = compositor.CreateInsetClip(quart, 0, quart, 0);

                        secoTranslation.InsertKeyFrame(1, new Vector3(show ? half - quart : 0, 48, 0));
                        //mainTranslation.InsertKeyFrame(1, new Vector3(-quart, 48, 0));

                        panelClip.InsertKeyFrame(1, 48);
                        buttonClip.InsertKeyFrame(1, show ? quart : 0);

                        buttonOpacity.InsertKeyFrame(1, show ? 1 : 0);
                    }
                    else if (Position == SecondaryButtonPosition.Bottom)
                    {
                        seco.Offset = new Vector3(0, 48, 0);
                        //secoTranslation.InsertKeyFrame(1, new Vector3(0, 48, 0));
                        mainTranslation.InsertKeyFrame(1, new Vector3(0, show ? 0 : 48, 0));

                        panelClip.InsertKeyFrame(1, show ? 0 : 48);
                        buttonClip.InsertKeyFrame(1, 0);
                    }

                    main.StartAnimation("Opacity", buttonOpacity);
                    main.StartAnimation("Offset", mainTranslation);
                    seco.StartAnimation("Offset", secoTranslation);

                    //main.Clip.StartAnimation("LeftInset", buttonClip);
                    seco.Clip.StartAnimation("LeftInset", buttonClip);
                    //main.Clip.StartAnimation("RightInset", buttonClip);
                    seco.Clip.StartAnimation("RightInset", buttonClip);

                    panel.Clip.StartAnimation("TopInset", panelClip);
                }

                void ShowHideSingle(UIElement first, bool show)
                {
                    first.Visibility = Visibility.Visible;

                    var panel = ElementComposition.GetElementVisual(BottomBarPanel);
                    var main = ElementComposition.GetElementVisual(first);

                    var compositor = main.Compositor;

                    panel.Clip ??= compositor.CreateInsetClip();
                    main.Clip ??= compositor.CreateInsetClip();

                    var mainTranslation = compositor.CreateVector3KeyFrameAnimation();

                    var panelClip = compositor.CreateScalarKeyFrameAnimation();

                    var buttonOpacity = compositor.CreateScalarKeyFrameAnimation();

                    var half = BottomBarPanel.ActualSize.X / 2;
                    var quart = half / 2;

                    buttonOpacity.InsertKeyFrame(1, show ? 1 : 0);

                    mainTranslation.InsertKeyFrame(1, new Vector3(0, show ? 48 : 96, 0));

                    panelClip.InsertKeyFrame(1, show ? 48 : 96);

                    main.StartAnimation("Opacity", buttonOpacity);
                    main.StartAnimation("Offset", mainTranslation);

                    panel.Clip.StartAnimation("TopInset", panelClip);
                }

                if (_mainButtonCollapsed == main1 && secondary1)
                {
                    ShowHideDouble(MainButtonPanel, SecondaryButtonPanel, main1, main1 ? position : _secondaryButtonPosition);
                }
                else if (_secondaryButtonCollapsed == secondary1 && main1)
                {
                    ShowHideDouble(SecondaryButtonPanel, MainButtonPanel, secondary1, (secondary1 ? position : _secondaryButtonPosition) switch
                    {
                        SecondaryButtonPosition.Left => SecondaryButtonPosition.Right,
                        SecondaryButtonPosition.Top => SecondaryButtonPosition.Bottom,
                        SecondaryButtonPosition.Right => SecondaryButtonPosition.Left,
                        SecondaryButtonPosition.Bottom => SecondaryButtonPosition.Top
                    });
                }
                else if (_mainButtonCollapsed == main1)
                {
                    ShowHideSingle(MainButtonPanel, main1);
                }
                else if (_secondaryButtonCollapsed == secondary1)
                {
                    ShowHideSingle(SecondaryButtonPanel, secondary1);
                }

                _mainButtonCollapsed = !main1;
                _secondaryButtonCollapsed = !secondary1;

                _secondaryButtonPosition = position;
            }

            batch.End();

            double margin;
            if (_secondaryButtonPosition == SecondaryButtonPosition.Top || _secondaryButtonPosition == SecondaryButtonPosition.Bottom)
            {
                margin = _mainButtonCollapsed && _secondaryButtonCollapsed ? 0 : !_mainButtonCollapsed && !_secondaryButtonCollapsed ? 96 : 48;
            }
            else
            {
                margin = !_mainButtonCollapsed || !_secondaryButtonCollapsed ? 48 : 0;
            }

            BackgroundPanel.Margin = new Thickness(0, 0, 0, margin);
            View.Margin = new Thickness(0, 0, 0, margin);
        }

        private async void SwitchInlineQueryMessage(JsonObject eventData)
        {
            var query = eventData.GetNamedString("query", string.Empty);
            var types = eventData.GetNamedArray("chat_types", null);
            var values = new HashSet<string>();

            foreach (var type in types)
            {
                if (type.ValueType == JsonValueType.String)
                {
                    values.Add(type
                        .GetString()
                        .ToLowerInvariant());
                }
            }

            var target = new TargetChatChosen
            {
                Types = new TargetChatTypes
                {
                    AllowBotChats = values.Contains("bots"),
                    AllowUserChats = values.Contains("users"),
                    AllowGroupChats = values.Contains("groups"),
                    AllowChannelChats = values.Contains("channels")
                }
            };

            if (target.Types.AllowBotChats || target.Types.AllowUserChats || target.Types.AllowGroupChats || target.Types.AllowChannelChats)
            {
                var confirm = await _navigationService.ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationSwitchInline(query, target, _botUser));
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }
            else if (_source is OpenUrlSourceChat openUrlSourceChat)
            {
                _aggregator.Publish(new UpdateChatSwitchInlineQuery(openUrlSourceChat.ChatId, _botUser.Id, query));
            }

            _closeNeedConfirmation = false;
            Close();
        }

        private async void SendDataMessage(JsonObject eventData)
        {
            var data = eventData.GetNamedString("data");
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(_buttonText) || _sentData)
            {
                return;
            }

            _sentData = true;

            await _clientService.SendAsync(new SendWebAppData(_botUser.Id, _buttonText, data));

            _closeNeedConfirmation = false;
            Close();
        }

        private void PostEvent(string eventName, params object[] eventData)
        {
            if (eventData.Length % 2 == 0)
            {
                var data = new JsonObject();

                for (int i = 0; i < eventData.Length; i += 2)
                {
                    if (eventData[i] is string key)
                    {
                        data[key] = eventData[i + 1] switch
                        {
                            string stringValue => CreateStringValue(stringValue),
                            double numberValue => Windows.Data.Json.JsonValue.CreateNumberValue(numberValue),
                            bool booleanValue => Windows.Data.Json.JsonValue.CreateBooleanValue(booleanValue),
                            _ => Windows.Data.Json.JsonValue.CreateNullValue(),
                        };

                        static Windows.Data.Json.JsonValue CreateStringValue(string stringValue)
                        {
                            try
                            {
                                if (Windows.Data.Json.JsonValue.TryParse(stringValue, out Windows.Data.Json.JsonValue obj))
                                {
                                    return obj;
                                }
                            }
                            catch
                            {
                                Logger.Debug("Unable to parse JSON string: " + stringValue);
                            }

                            return Windows.Data.Json.JsonValue.CreateStringValue(stringValue);
                        }
                    }
                }

                PostEventImpl(eventName, data.Stringify());
            }
            else if (eventData.Length > 0)
            {
                PostEventImpl(eventName, string.Join(' ', eventData));
            }
            else
            {
                PostEventImpl(eventName, "null");
            }
        }

        private void PostEventImpl(string eventName, string eventData = "null")
        {
            Logger.Info(string.Format("{0}: {1}", eventName, eventData));
            View.InvokeScript($"window.Telegram.WebView.receiveEvent('{eventName}', {eventData});");
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            if (_gameChatId != 0 && _gameMessageId != 0)
            {
                if (_fullscreen)
                {
                    flyout.CreateFlyoutItem(ProcessExitFullScreen, Strings.BotWebViewExitFullScreen, Icons.ArrowMinimize);
                }
                else
                {
                    flyout.CreateFlyoutItem(ProcessRequestFullScreen, Strings.BotWebViewEnterFullScreen, Icons.ArrowMaximize);
                }

                flyout.CreateFlyoutItem(MenuItemShare, Strings.ShareFile, Icons.Share);
                flyout.CreateFlyoutItem(MenuItemReloadPage, Strings.BotWebViewReloadPage, Icons.ArrowClockwise);
            }
            else
            {
                if (_settingsVisible)
                {
                    flyout.CreateFlyoutItem(MenuItemSettings, Strings.BotWebViewSettings, Icons.Settings);
                }

                if (_fullscreen)
                {
                    flyout.CreateFlyoutItem(ProcessExitFullScreen, Strings.BotWebViewExitFullScreen, Icons.ArrowMinimize);
                }
                else
                {
                    flyout.CreateFlyoutItem(ProcessRequestFullScreen, Strings.BotWebViewEnterFullScreen, Icons.ArrowMaximize);
                }

                // TODO: check opening chat?
                flyout.CreateFlyoutItem(MenuItemOpenBot, Strings.BotWebViewOpenBot, Icons.Bot);

                flyout.CreateFlyoutItem(MenuItemReloadPage, Strings.BotWebViewReloadPage, Icons.ArrowClockwise);

                if (_botUser.Type is UserTypeBot { HasMainWebApp: true })
                {
                    if (SecondaryTile.Exists("web_app_" + _clientService.SessionId + "_" + _botUser.Id))
                    {
                        flyout.CreateFlyoutItem(MenuItemRemoveFromStartMenu, Strings.BotWebViewRemoveFromStart, Icons.HomeDismiss);
                    }
                    else
                    {
                        flyout.CreateFlyoutItem(MenuItemAddToStartMenu, Strings.BotWebViewAddToStart, Icons.HomeAdd);
                    }
                }

                flyout.CreateFlyoutItem(MenuItemTerms, Strings.BotWebViewToS, Icons.Info);

                if (_menuBot != null && _menuBot.IsAdded)
                {
                    flyout.CreateFlyoutItem(MenuItemDeleteBot, Strings.BotWebViewDeleteBot, Icons.Delete, destructive: true);
                }
            }

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void MenuItemTerms()
        {
            MessageHelper.OpenUrl(null, null, Strings.BotWebViewToSLink);
        }

        private void MenuItemSettings()
        {
            PostEvent("settings_button_pressed");
        }

        private void MenuItemOpenBot()
        {
            _navigationService.NavigateToUser(_botUser.Id);
        }

        private void MenuItemShare()
        {
            ProcessShareGame(false);
        }

        private void MenuItemReloadPage()
        {
            View.Reload();
        }

        private async void MenuItemRemoveFromStartMenu()
        {
            if (SecondaryTile.Exists("web_app_" + _clientService.SessionId + "_" + _botUser.Id))
            {
                var secondaryTile = new SecondaryTile("web_app_" + _clientService.SessionId + "_" + _botUser.Id);
                await secondaryTile.RequestDeleteForSelectionAsync(new Windows.Foundation.Rect(0, 0, ActualWidth, ActualHeight));
            }
        }

        private async void MenuItemAddToStartMenu()
        {
            try
            {
                var user = _botUser;
                if (user.Type is not UserTypeBot { HasMainWebApp: true })
                {
                    return;
                }

                var response = await _clientService.SendAsync(new GetInternalLink(new InternalLinkTypeMainWebApp(_botUser.ActiveUsername(), string.Empty, new WebAppOpenModeFullSize()), false));
                if (response is not HttpUrl url)
                {
                    return;
                }

                var arguments = new Dictionary<string, string>();
                arguments.Add("session", _clientService.SessionId.ToString());
                arguments.Add("web_app", Toast.ToBase64(url.Url));

                var builder = new StringBuilder();

                foreach (var item in arguments)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append("&");
                    }

                    builder.AppendFormat("{0}={1}", item.Key, item.Value);
                }

                var photo = await GenerateTileLogoAsync(user.ProfilePhoto);
                var secondaryTile = new SecondaryTile("web_app_" + _clientService.SessionId + "_" + _botUser.Id,
                                                      _botUser.FirstName,
                                                      builder.ToString(),
                                                      photo,
                                                      TileSize.Default);

                secondaryTile.VisualElements.Square71x71Logo = photo;

#pragma warning disable CS0618 // Type or member is obsolete
                secondaryTile.VisualElements.Square30x30Logo = photo;
#pragma warning restore CS0618 // Type or member is obsolete

                secondaryTile.VisualElements.ShowNameOnSquare150x150Logo = true;

                var pinned = await secondaryTile.RequestCreateForSelectionAsync(new Windows.Foundation.Rect(0, 0, ActualWidth, ActualHeight));
                if (pinned)
                {
                    PostEvent("home_screen_added");
                }
            }
            catch
            {
                //
            }
        }

        private async Task<Uri> GenerateTileLogoAsync(ProfilePhoto photo)
        {
            try
            {
                if (photo != null && photo.Small.Local.IsDownloadingCompleted)
                {
                    var device = ElementComposition.GetSharedDevice();
                    var bitmap = await CanvasBitmap.LoadAsync(device, _botUser.ProfilePhoto.Small.Local.Path, 96, CanvasAlphaMode.Premultiplied);

                    var target = new CanvasRenderTarget(bitmap, bitmap.Size);
                    var half = (float)bitmap.Size.Width / 2;

                    using (var session = target.CreateDrawingSession())
                    {
                        using (var layer = session.CreateLayer(1, CanvasGeometry.CreateEllipse(device, half, half, half, half)))
                        {
                            session.DrawImage(bitmap);
                        }
                    }

                    var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("WebApps", CreationCollisionOption.OpenIfExists);
                    var file = await folder.CreateFileAsync("web_app_" + _clientService.SessionId + "_" + _botUser.Id + ".png", CreationCollisionOption.ReplaceExisting);

                    using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await target.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                    }

                    return new Uri("ms-appdata:///local/WebApps/web_app_" + _clientService.SessionId + "_" + _botUser.Id + ".png");
                }
            }
            catch
            {
                // Catching as this would break the UI otherwise
            }

            return new Uri("ms-appx:///Assets/Logos/WebAppLogo.png");
        }

        private async void MenuItemDeleteBot()
        {
            var confirm = await MessagePopup.ShowAsync(XamlRoot, string.Format(Strings.BotRemoveFromMenu, _menuBot.Name), Strings.BotRemoveFromMenuTitle, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                _menuBot.IsAdded = false;
                _clientService.Send(new ToggleBotIsAddedToAttachmentMenu(_menuBot.BotUserId, false, false));
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            PostEvent("back_button_pressed");
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            PostThemeChanged();

            if (_headerColorKey != null)
            {
                ProcessHeaderColor(_headerColorKey switch
                {
                    "bg_color" => Theme.Current.Parameters.BackgroundColor.ToColor(),
                    "secondary_bg_color" => Theme.Current.Parameters.SecondaryBackgroundColor.ToColor(),
                    "bottom_bar_bg_color" => Theme.Current.Parameters.BottomBarBackgroundColor.ToColor(),
                    _ => new Color?(),
                });
            }

            if (_backgroundColorKey != null)
            {
                ProcessBackgroundColor(_backgroundColorKey switch
                {
                    "bg_color" => Theme.Current.Parameters.BackgroundColor.ToColor(),
                    "secondary_bg_color" => Theme.Current.Parameters.SecondaryBackgroundColor.ToColor(),
                    "bottom_bar_bg_color" => Theme.Current.Parameters.BottomBarBackgroundColor.ToColor(),
                    _ => new Color?(),
                });
            }

            if (_bottomBarColorKey != null)
            {
                ProcessBottomBarColor(_bottomBarColorKey switch
                {
                    "bg_color" => Theme.Current.Parameters.BackgroundColor.ToColor(),
                    "secondary_bg_color" => Theme.Current.Parameters.SecondaryBackgroundColor.ToColor(),
                    "bottom_bar_bg_color" => Theme.Current.Parameters.BottomBarBackgroundColor.ToColor(),
                    _ => new Color?(),
                });
            }
        }

        private void PostThemeChanged()
        {
            var theme = ClientEx.GetThemeParametersJsonString(Theme.Current.Parameters);
            PostEvent("theme_changed", "theme_params", theme);
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public partial class SecondaryNavigationService : TLNavigationService
    {
        private readonly INavigationService _source;

        public SecondaryNavigationService(ISession session, INavigationService source, WindowContext window)
            : base(session, window, null, string.Empty)
        {
            _source = source;
        }

        public override bool Navigate(Type page, object parameter = null, NavigationState state = null, NavigationTransitionInfo infoOverride = null, bool navigationStackEnabled = true)
        {
            _source.Dispatcher.Dispatch(() => _source.Navigate(page, parameter, state, infoOverride, navigationStackEnabled));
            _ = ApplicationViewSwitcher.SwitchAsync(_source.Window.Id);

            return true;
        }

        public void Switch()
        {
            _ = ApplicationViewSwitcher.SwitchAsync(_source.Window.Id);
        }
    }
}
