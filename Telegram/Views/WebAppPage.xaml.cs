//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core.Preview;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views
{
    public sealed partial class WebAppPage : UserControlEx, IToastHost
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly Chat _sourceChat;
        private readonly User _botUser;
        private readonly AttachmentMenuBot _menuBot;

        private readonly long _launchId;

        private readonly long _gameChatId;
        private readonly long _gameMessageId;

        private bool _blockingAction;
        private bool _closeNeedConfirmation;

        private bool _settingsVisible;

        // TODO: constructor should take a function and URL should be loaded asynchronously
        public WebAppPage(IClientService clientService, User botUser, string url, long launchId = 0, AttachmentMenuBot menuBot = null, Chat sourceChat = null)
        {
            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
            InitializeComponent();

            _clientService = clientService;
            _aggregator = TypeResolver.Current.Resolve<IEventAggregator>(clientService.SessionId);

            _aggregator.Subscribe<UpdateWebAppMessageSent>(this, Handle)
                .Subscribe<UpdatePaymentCompleted>(Handle);

            _botUser = botUser;
            _launchId = launchId;
            _menuBot = menuBot;
            _sourceChat = sourceChat;

            TitleText.Text = botUser.FullName();
            Photo.SetUser(clientService, botUser, 24);

            View.Navigate(url);

            var panel = ElementComposition.GetElementVisual(BottomBarPanel);
            panel.Clip = panel.Compositor.CreateInsetClip(0, 96, 0, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(TitleText, true);

            Window.Current.SetTitleBar(TitleBar);

            ViewLifetimeControl.GetForCurrentView().Released += OnReleased;
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;

            var coreWindow = (IInternalCoreWindowPhone)(object)Window.Current.CoreWindow;
            var navigationClient = (IApplicationWindowTitleBarNavigationClient)coreWindow.NavigationClient;

            navigationClient.TitleBarPreferredVisibilityMode = AppWindowTitleBarVisibility.AlwaysHidden;
        }

        public WebAppPage(IClientService clientService, User botUser, string url, string title, long gameChatId = 0, long gameMessageId = 0)
        {
            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
            InitializeComponent();

            _clientService = clientService;
            _aggregator = TypeResolver.Current.Resolve<IEventAggregator>(clientService.SessionId);

            _botUser = botUser;
            _gameChatId = gameChatId;
            _gameMessageId = gameMessageId;

            TitleText.Text = title;
            Photo.SetUser(clientService, botUser, 24);

            View.Navigate(url);

            var panel = ElementComposition.GetElementVisual(BottomBarPanel);
            panel.Clip = panel.Compositor.CreateInsetClip(0, 96, 0, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(TitleText, true);

            Window.Current.SetTitleBar(TitleBar);

            ViewLifetimeControl.GetForCurrentView().Released += OnReleased;
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;

            var coreWindow = (IInternalCoreWindowPhone)(object)Window.Current.CoreWindow;
            var navigationClient = (IApplicationWindowTitleBarNavigationClient)coreWindow.NavigationClient;

            navigationClient.TitleBarPreferredVisibilityMode = AppWindowTitleBarVisibility.AlwaysHidden;
        }

        #region IToastHost

        public void Connect(TeachingTip toast)
        {
            Resources.Remove("TeachingTip");
            Resources.Add("TeachingTip", toast);
        }

        public void Disconnect(TeachingTip toast)
        {
            if (Resources.TryGetValue("TeachingTip", out object cached))
            {
                if (cached == toast)
                {
                    Resources.Remove("TeachingTip");
                }
            }
        }

        #endregion

        private void Handle(UpdateWebAppMessageSent update)
        {
            if (update.WebAppLaunchId == _launchId)
            {
                _closeNeedConfirmation = false;
                Close();
            }
        }

        private void Handle(UpdatePaymentCompleted update)
        {
            PostEvent("invoice_closed", "{ slug: \"" + update.Slug + "\", status: " + update.Status + "}");
        }

        private bool _closed;

        private async void Close()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;

            if (WindowContext.Current != null)
            {
                await WindowContext.Current.ConsolidateAsync();
            }
            else
            {
                await ApplicationView.GetForCurrentView().TryConsolidateAsync();
            }
        }

        private void OnReleased(object sender, System.EventArgs e)
        {
            if (_launchId != 0)
            {
                _clientService.Send(new CloseWebApp(_launchId));
            }
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
                    View.Close();
                }
                else
                {
                    e.Handled = true;
                }

                deferral.Complete();
            }
        }

        private void View_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SendViewport();
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
            ByNavigation(navigation => MessageHelper.OpenUrl(_clientService, navigation, e.Url));
            e.Cancel = true;
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
                    ByNavigation(navigation => MessageHelper.OpenTelegramUrl(_clientService, navigation, uri));
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
            SendViewport();
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
                // TODO: probably need to inform the web view
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
                SendViewport();
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

        private async void ProcessShareGame(bool withMyScore)
        {
            await this.ShowPopupAsync(_clientService.SessionId, new ChooseChatsPopup(), new ChooseChatsConfigurationShareMessage(_gameChatId, _gameMessageId, withMyScore));
        }

        private async void RequestClipboardText(JsonObject eventData)
        {
            var requestId = eventData.GetNamedString("req_id", string.Empty);
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            var clipboard = Clipboard.GetContent();
            if (clipboard.Contains(StandardDataFormats.Text))
            {
                var text = await clipboard.GetTextAsync();
                PostEvent("clipboard_text_received", "{ req_id: \"" + requestId + "\", data: \"" + text + "\" }");
            }
            else
            {
                PostEvent("clipboard_text_received", "{ req_id: \"" + requestId + "\" }");
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
                PostEvent("custom_method_invoked", "{ req_id: \"" + requestId + "\", result: " + result.Result + " }");
            }
            else if (response is Error error)
            {
                PostEvent("custom_method_invoked", "{ req_id: \"" + requestId + "\", error: " + error.Message + " }");
            }
        }

        private string _headerColorKey;
        private string _backgroundColorKey;

        private void ProcessHeaderColor(JsonObject eventData)
        {
            ProcessHeaderColor(ProcessColor(eventData, out _backgroundColorKey));
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
            ProcessBottomBarColor(ProcessColor(eventData, out _backgroundColorKey));
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
                PostEvent("phone_requested", "{ status: \"cancelled\" }");
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
                    PostEvent("phone_requested", "{ status: \"cancelled\" }");

                    return;
                }

                if (chat.BlockList is BlockListMain)
                {
                    await _clientService.SendAsync(new SetMessageSenderBlockList(new MessageSenderUser(_botUser.Id), null));
                }

                await _clientService.SendAsync(new SendMessage(chat.Id, 0, null, null, null, new InputMessageContact(new Contact(user.PhoneNumber, user.FirstName, user.LastName, string.Empty, user.Id))));

                _blockingAction = false;
                PostEvent("phone_requested", "{ status: \"sent\" }");
            }
            else
            {
                _blockingAction = false;
                PostEvent("phone_requested", "{ status: \"cancelled\" }");
            }
        }

        private async void RequestWriteAccess()
        {
            if (_blockingAction)
            {
                PostEvent("write_access_requested", "{ status: \"cancelled\" }");
                return;
            }

            _blockingAction = true;

            var request = await _clientService.SendAsync(new CanBotSendMessages(_botUser.Id));
            if (request is Ok)
            {
                _blockingAction = false;
                PostEvent("write_access_requested", "{ status: \"allowed\" }");

                return;
            }

            var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.BotWebViewRequestWriteMessage, Strings.BotWebViewRequestWriteTitle, Strings.BotWebViewRequestAllow, Strings.BotWebViewRequestDontAllow);
            if (confirm == ContentDialogResult.Primary)
            {
                await _clientService.SendAsync(new AllowBotToSendMessages(_botUser.Id));

                _blockingAction = false;
                PostEvent("write_access_requested", "{ status: \"allowed\" }");
            }
            else
            {
                _blockingAction = false;
                PostEvent("write_access_requested", "{ status: \"cancelled\" }");
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

            var label = new TextBlock
            {
                Text = message,
            };

            Grid.SetColumnSpan(label, int.MaxValue);

            var panel = new Grid
            {
                ColumnSpacing = 8,
                Margin = new Thickness(0, 8, 0, 0)
            };

            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            panel.Children.Add(label);

            var popup = new ContentPopup
            {
                Title = title,
                Content = panel,
                Width = 388,
                MinWidth = 388,
                MaxWidth = 388,
            };

            void click(object sender, RoutedEventArgs e)
            {
                if (sender is Button button && button.CommandParameter is string id)
                {
                    PostEvent("popup_closed", "{ button_id: \"" + id + "\" }");
                    button.Click -= click;
                }

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

            ByNavigation(navigationService => navigationService.NavigateToInvoice(new InputInvoiceName(value)));
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
            ByNavigation(navigationService => MessageHelper.OpenUrl(_clientService, navigationService, "https://t.me" + value));
        }

        private void SendViewport()
        {
            PostEvent("viewport_changed", "{ height: " + View.ActualHeight + ", is_state_stable: true, is_expanded: true }");
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

            if (is_visible && !string.IsNullOrEmpty(text.Trim()))
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

                SetColor(MainButtonPanel, color, Border.BackgroundProperty);
                SetColor(MainButton, text_color, ForegroundProperty);

                MainButton.Content = text;
                MainButton.IsEnabled = is_active;

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

            if (is_visible && !string.IsNullOrEmpty(text.Trim()))
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

                SetColor(SecondaryButtonPanel, color, Border.BackgroundProperty);
                SetColor(SecondaryButton, text_color, ForegroundProperty);

                SecondaryButton.Content = text;
                SecondaryButton.IsEnabled = is_active;

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

        private void SwitchInlineQueryMessage(JsonObject eventData)
        {
            var query = eventData.GetNamedString("query", string.Empty);
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

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
                AllowBotChats = values.Contains("bots"),
                AllowUserChats = values.Contains("users"),
                AllowGroupChats = values.Contains("groups"),
                AllowChannelChats = values.Contains("channels")
            };

            Close();

            if (target.AllowBotChats || target.AllowUserChats || target.AllowGroupChats || target.AllowChannelChats)
            {
                ByNavigation(navigation => navigation.ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationSwitchInline(query, target, _botUser)));
            }
            else if (_sourceChat != null)
            {
                var aggregator = TypeResolver.Current.Resolve<IEventAggregator>(_clientService.SessionId);
                aggregator.Publish(new UpdateChatSwitchInlineQuery(_sourceChat.Id, _botUser.Id, query));
            }
        }

        private void SendDataMessage(JsonObject eventData)
        {
            var data = eventData.GetNamedString("data");
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            /*if (!_context
        || _context->fromSwitch
        || _context->fromBotApp
        || _context->fromMainMenu
        || _context->action.history->peer != _bot
        || _lastShownQueryId) {
        return;
        }*/
        }

        private void PostEvent(string eventName, string eventData = "null")
        {
            Logger.Info(string.Format("{0}: {1}", eventName, eventData));
            View.InvokeScript($"window.Telegram.WebView.receiveEvent('{eventName}', {eventData});");
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            if (_gameChatId != 0 && _gameMessageId != 0)
            {
                flyout.CreateFlyoutItem(MenuItemShare, Strings.ShareFile, Icons.Share);
                flyout.CreateFlyoutItem(MenuItemReloadPage, Strings.BotWebViewReloadPage, Icons.ArrowClockwise);
            }
            else
            {
                if (_settingsVisible)
                {
                    flyout.CreateFlyoutItem(MenuItemSettings, Strings.BotWebViewSettings, Icons.Settings);
                }

                // TODO: check opening chat?
                flyout.CreateFlyoutItem(MenuItemOpenBot, Strings.BotWebViewOpenBot, Icons.Bot);

                flyout.CreateFlyoutItem(MenuItemReloadPage, Strings.BotWebViewReloadPage, Icons.ArrowClockwise);

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
            ByNavigation(navigationService => navigationService.NavigateToUser(_botUser.Id));
            //Hide();
        }

        private void MenuItemShare()
        {
            ProcessShareGame(false);
        }

        private void MenuItemReloadPage()
        {
            View.Reload();
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

        private async void ByNavigation(Action<INavigationService> action)
        {
            WindowContext.Main.Dispatcher.Dispatch(() => action(WindowContext.Main.GetNavigationService()));
            await ApplicationViewSwitcher.SwitchAsync(WindowContext.Main.Id);
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
                ProcessHeaderColor(_backgroundColorKey switch
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
            PostEvent("theme_changed", "{\"theme_params\": " + theme + "}");
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
