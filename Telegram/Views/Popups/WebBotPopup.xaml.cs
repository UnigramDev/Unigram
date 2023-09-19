//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Data.Json;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Popups
{
    public sealed partial class WebBotPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly User _botUser;
        private readonly AttachmentMenuBot _menuBot;

        private bool _blockingAction;
        private bool _closeNeedConfirmation;

        // TODO: constructor should take a function and URL should be loaded asynchronously
        public WebBotPopup(IClientService clientService, INavigationService navigationService, User user, WebAppInfo info, AttachmentMenuBot menuBot = null)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _botUser = user;
            _menuBot = menuBot;

            Title.Text = user.FullName();

            ElementCompositionPreview.SetIsTranslationEnabled(MainButton, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Title, true);

            View.SizeChanged += View_SizeChanged;
            View.Navigate(info.Url);
        }

        // TODO: constructor should take a function and URL should be loaded asynchronously
        public WebBotPopup(IClientService clientService, INavigationService navigationService, User user, string url, AttachmentMenuBot menuBot = null)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _botUser = user;
            _menuBot = menuBot;

            Title.Text = user.FullName();

            ElementCompositionPreview.SetIsTranslationEnabled(MainButton, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Title, true);

            View.SizeChanged += View_SizeChanged;
            View.Navigate(url);
        }

        private void View_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Logger.Debug();

            SendViewport();
        }

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            PostEvent("main_button_pressed");
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
            else if (eventName == "web_app_request_theme")
            {
                //_themeUpdateForced.fire({ });
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
        }

        private void RequestClipboardText(JsonObject eventData)
        {

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

        private void ProcessHeaderColor(JsonObject eventData)
        {
            if (eventData.ContainsKey("color"))
            {
                var colorValue = eventData.GetNamedString("color");
                var color = ParseColor(colorValue);

                if (color is Color c)
                {
                    var luminance = 0.2126 * (c.R / 255d) + 0.7152 * (c.G / 255d) + 0.0722 * (c.B / 255d);
                    var foreground = luminance > 0.5 ? Colors.Black : Colors.White;

                    var brush = new SolidColorBrush(foreground);

                    TitlePanel.Background = new SolidColorBrush(c);
                    Title.Foreground = brush;
                    BackButton.Foreground = brush;
                    MoreButton.Foreground = brush;
                    HideButton.Foreground = brush;
                }
                else
                {
                    TitlePanel.ClearValue(Panel.BackgroundProperty);
                    Title.ClearValue(TextBlock.ForegroundProperty);
                    BackButton.ClearValue(ForegroundProperty);
                    MoreButton.ClearValue(ForegroundProperty);
                    HideButton.ClearValue(ForegroundProperty);
                }
            }
            else if (eventData.ContainsKey("color_key"))
            {
                var colorKey = eventData.GetNamedString("color_key");
            }
        }

        private async void RequestPhone()
        {
            if (_blockingAction)
            {
                PostEvent("phone_requested", "{ status: \"cancelled\" }");
                return;
            }

            _blockingAction = true;

            var confirm = await MessagePopup.ShowAsync(null, string.Format(Strings.AreYouSureShareMyContactInfoWebapp, _botUser.FullName()), Strings.ShareYouPhoneNumberTitle, Strings.OK, Strings.Cancel);
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

            var confirm = await MessagePopup.ShowAsync(null, Strings.BotWebViewRequestWriteMessage, Strings.BotWebViewRequestWriteTitle, Strings.BotWebViewRequestAllow, Strings.BotWebViewRequestDontAllow);
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

        private void OpenPopup(JsonObject eventData)
        {
            var title = eventData.GetNamedString("title", string.Empty);
            var message = eventData.GetNamedString("message", string.Empty);
            var buttons = eventData.GetNamedArray("buttons");

            if (string.IsNullOrEmpty(message) || buttons.Empty())
            {
                return;
            }

            var panel = new Grid
            {
                ColumnSpacing = 8,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var popup = new TeachingTip
            {
                Title = title,
                Subtitle = message,
                Content = panel,
                PreferredPlacement = TeachingTipPlacementMode.Center,
                Width = 388,
                MinWidth = 388,
                MaxWidth = 388,
                Target = null,
                IsLightDismissEnabled = false,
                ShouldConstrainToRootBounds = true,
            };

            void handler(object sender, RoutedEventArgs e)
            {
                if (sender is Button button && button.CommandParameter is string id)
                {
                    PostEvent("popup_closed", "{ button_id: \"" + id + "\" }");
                    button.Click -= handler;
                }

                popup.IsOpen = false;
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

                action.Click += handler;

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

            if (Window.Current.Content is FrameworkElement element)
            {
                element.Resources["TeachingTip"] = popup;
            }

            popup.IsOpen = true;
        }

        private void OpenInvoice(JsonObject eventData)
        {

        }

        private void OpenExternalLink(JsonObject eventData)
        {
            // Ignoring try_instant_view for now
            var value = eventData.GetNamedString("url", string.Empty);
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            MessageHelper.OpenUrl(_clientService, _navigationService, value);
        }

        private void OpenInternalLink(JsonObject eventData)
        {
            var value = eventData.GetNamedString("path_full", string.Empty);
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            Hide();
            MessageHelper.OpenUrl(_clientService, _navigationService, "https://t.me" + value);
        }

        private void SendViewport()
        {
            PostEvent("viewport_changed", "{ height: " + View.ActualHeight + ", is_state_stable: true, is_expanded: true }");
        }

        private void ProcessBackButtonMessage(JsonObject eventData)
        {
            ShowHideBackButton(eventData.GetNamedBoolean("is_visible", false));
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

            var visual1 = ElementCompositionPreview.GetElementVisual(BackButton);
            var visual2 = ElementCompositionPreview.GetElementVisual(Title);

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

            var visual = ElementCompositionPreview.GetElementVisual(MainButton);
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

        }

        private void SendDataMessage(JsonObject eventData)
        {

        }

        private void PostEvent(string eventName, string eventData = "null")
        {
            Logger.Info(string.Format("{0}: {1}", eventName, eventData));
            View.InvokeScript($"window.Telegram.WebView.receiveEvent('{eventName}', {eventData});");
        }

        private async void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (_closeNeedConfirmation)
            {
                args.Cancel = true;

                var confirm = await MessagePopup.ShowAsync(target: null, Strings.BotWebViewChangesMayNotBeSaved, _botUser.FirstName, Strings.BotWebViewCloseAnyway, Strings.Cancel, destructive: true);
                if (confirm == ContentDialogResult.Primary)
                {
                    _closeNeedConfirmation = false;
                    Hide();
                }

                return;
            }

            View.Close();
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            if (_menuBot != null && _menuBot.SupportsSettings)
            {
                flyout.CreateFlyoutItem(MenuItemSettings, Strings.BotWebViewSettings, Icons.Settings);
            }

            // TODO: check opening chat?
            flyout.CreateFlyoutItem(MenuItemOpenBot, Strings.BotWebViewOpenBot, Icons.Bot);

            flyout.CreateFlyoutItem(MenuItemReloadPage, Strings.BotWebViewReloadPage, Icons.ArrowClockwise);

            if (_menuBot != null && _menuBot.IsAdded)
            {
                flyout.CreateFlyoutItem(MenuItemDeleteBot, Strings.BotWebViewDeleteBot, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void MenuItemSettings()
        {
            PostEvent("popup_closed", "{ status: \"cancelled\" }");


            PostEvent("settings_button_pressed");
        }

        private void MenuItemOpenBot()
        {

        }

        private void MenuItemReloadPage()
        {
            View.Reload();
        }

        private void MenuItemDeleteBot()
        {

        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            PostEvent("back_button_pressed");
        }
    }
}
