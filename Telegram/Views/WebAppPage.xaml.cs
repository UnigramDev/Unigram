//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
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
using Telegram.Services.ViewService;
using Telegram.Td;
using Telegram.Td.Api;
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
    public sealed partial class WebAppPage : UserControlEx
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly Chat _sourceChat;
        private readonly User _botUser;
        private readonly AttachmentMenuBot _menuBot;

        private readonly long _launchId;

        private bool _blockingAction;
        private bool _closeNeedConfirmation;

        private bool _settingsVisible;

        // TODO: constructor should take a function and URL should be loaded asynchronously
        public WebAppPage(IClientService clientService, User botUser, string url, long launchId = 0, AttachmentMenuBot menuBot = null, Chat sourceChat = null)
        {
            InitializeComponent();

            _clientService = clientService;
            _aggregator = TypeResolver.Current.Resolve<IEventAggregator>(clientService.SessionId);

            _aggregator.Subscribe<UpdateWebAppMessageSent>(this, Handle)
                .Subscribe<UpdatePaymentCompleted>(Handle);

            _botUser = botUser;
            _launchId = launchId;
            _menuBot = menuBot;
            _sourceChat = sourceChat;

            Title.Text = botUser.FullName();
            View.Navigate(url);

            ElementCompositionPreview.SetIsTranslationEnabled(MainButton, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Title, true);

            Window.Current.SetTitleBar(TitleGrip);

            ViewLifetimeControl.GetForCurrentView().Released += OnReleased;
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;

            var coreWindow = (IInternalCoreWindowPhone)(object)Window.Current.CoreWindow;
            var navigationClient = (IApplicationWindowTitleBarNavigationClient)coreWindow.NavigationClient;

            navigationClient.TitleBarPreferredVisibilityMode = AppWindowTitleBarVisibility.AlwaysHidden;
        }

        private void Handle(UpdateWebAppMessageSent update)
        {
            if (update.WebAppLaunchId == _launchId)
            {
                _closeNeedConfirmation = false;
                Hide();
            }
        }

        private void Handle(UpdatePaymentCompleted update)
        {
            PostEvent("invoice_closed", "{ slug: \"" + update.Slug + "\", status: " + update.Status + "}");
        }

        private async void Hide()
        {
            await WindowContext.Current.ConsolidateAsync();
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

                var confirm = await MessagePopup.ShowAsync(Strings.BotWebViewChangesMayNotBeSaved, _botUser.FirstName, Strings.BotWebViewCloseAnyway, Strings.Cancel, destructive: true);
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
                Hide();
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
                Title.Foreground = brush;
                BackButton.RequestedTheme = theme;
                MoreButton.RequestedTheme = theme;
                HideButton.RequestedTheme = theme;
            }
            else
            {
                TitlePanel.ClearValue(Panel.BackgroundProperty);
                Title.ClearValue(TextBlock.ForegroundProperty);
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

            var confirm = await MessagePopup.ShowAsync(string.Format(Strings.AreYouSureShareMyContactInfoWebapp, _botUser.FullName()), Strings.ShareYouPhoneNumberTitle, Strings.OK, Strings.Cancel);
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

            var confirm = await MessagePopup.ShowAsync(Strings.BotWebViewRequestWriteMessage, Strings.BotWebViewRequestWriteTitle, Strings.BotWebViewRequestAllow, Strings.BotWebViewRequestDontAllow);
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

            await popup.ShowQueuedAsync();
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
            var visual2 = ElementComposition.GetElementVisual(Title);

            var batch = visual1.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual2.Properties.InsertVector3("Translation", Vector3.Zero);
                BackButton.Visibility = show
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            };

            var offset = visual1.Compositor.CreateScalarKeyFrameAnimation();
            offset.InsertKeyFrame(0, show ? -28 : 0);
            offset.InsertKeyFrame(1, show ? 0 : -28);
            offset.Duration = Constants.FastAnimation;

            var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);
            scale.Duration = Constants.FastAnimation;

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(show ? 0 : 1, 0);
            opacity.InsertKeyFrame(show ? 1 : 0, 1);

            visual1.CenterPoint = new Vector3(24);

            visual2.StartAnimation("Translation.X", offset);
            visual1.StartAnimation("Scale", scale);
            visual1.StartAnimation("Opacity", opacity);
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
                void SetColor(string value, DependencyProperty property)
                {
                    var color = ParseColor(value);
                    if (color is Color c)
                    {
                        MainButton.SetValue(property, new SolidColorBrush(c));
                    }
                    else
                    {
                        MainButton.ClearValue(property);
                    }
                }

                SetColor(color, BackgroundProperty);
                SetColor(text_color, ForegroundProperty);

                MainButton.Content = text;
                MainButton.IsEnabled = is_active;

                ShowHideMainButton(true);
            }
            else
            {
                ShowHideMainButton(false);
            }
        }

        private bool _mainButtonCollapsed = true;

        private void ShowHideMainButton(bool show)
        {
            if (_mainButtonCollapsed != show)
            {
                return;
            }

            _mainButtonCollapsed = !show;

            Grid.SetRowSpan(View, 2);
            MainButton.Visibility = Visibility.Visible;

            var visual = ElementComposition.GetElementVisual(MainButton);
            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Grid.SetRowSpan(View, show ? 1 : 2);
                MainButton.Visibility = show
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            };

            var anim = visual.Compositor.CreateScalarKeyFrameAnimation();
            anim.InsertKeyFrame(0, show ? 48 : 0);
            anim.InsertKeyFrame(1, show ? 0 : 48);
            anim.Duration = Constants.FastAnimation;

            visual.StartAnimation("Translation.Y", anim);
            batch.End();
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

            Hide();

            if (target.AllowBotChats || target.AllowUserChats || target.AllowGroupChats || target.AllowChannelChats)
            {
                ByNavigation(navigation => navigation.ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationSwitchInline(query, target, _botUser)));
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

        private void MenuItemReloadPage()
        {
            View.Reload();
        }

        private async void MenuItemDeleteBot()
        {
            var confirm = await MessagePopup.ShowAsync(string.Format(Strings.BotRemoveFromMenu, _menuBot.Name), Strings.BotRemoveFromMenuTitle, Strings.OK, Strings.Cancel);
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
            var service = WindowContext.Main.NavigationServices.GetByFrameId($"Main{_clientService.SessionId}");
            if (service != null)
            {
                action(service);
                await ApplicationViewSwitcher.SwitchAsync(WindowContext.Main.Id);
            }
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
                    _ => new Color?(),
                });
            }

            if (_backgroundColorKey != null)
            {
                ProcessHeaderColor(_backgroundColorKey switch
                {
                    "bg_color" => Theme.Current.Parameters.BackgroundColor.ToColor(),
                    "secondary_bg_color" => Theme.Current.Parameters.SecondaryBackgroundColor.ToColor(),
                    _ => new Color?(),
                });
            }
        }

        private void PostThemeChanged()
        {
            var theme = ClientEx.GetThemeParametersJsonString(Theme.Current.Parameters);
            PostEvent("theme_changed", "{\"theme_params\": " + theme + "}");
        }
    }
}
