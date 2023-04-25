//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using Telegram.Controls;
using Telegram.Td.Api;
using Windows.Data.Json;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Popups
{
    public sealed partial class WebBotPopup : ContentPopup
    {
        public WebBotPopup(int sessionId, WebAppInfo info, string buttonText)
        {
            InitializeComponent();
            InitializeWebView(sessionId, info, buttonText);
        }

        private async void InitializeWebView(int sessionId, WebAppInfo info, string buttonText)
        {
            await View.EnsureCoreWebView2Async();
            await View.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"window.external={invoke:s=>window.chrome.webview.postMessage(s)}");
            await View.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
window.TelegramWebviewProxy = {
postEvent: function(eventType, eventData) {
	if (window.external && window.external.invoke) {
		window.external.invoke(JSON.stringify([eventType, eventData]));
	}
}
}");

            View.CoreWebView2.Navigate(info.Url);
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;

            await View.CoreWebView2.ExecuteScriptAsync("window.Telegram.WebView.receiveEvent('main_button_pressed', null);");
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void View_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            var json = args.TryGetWebMessageAsString();

            if (JsonArray.TryParse(json, out JsonArray message))
            {
                var eventName = message.GetStringAt(0);
                var eventData = message.GetStringAt(1);

                if (JsonObject.TryParse(eventData, out JsonObject data))
                {
                    ReceiveEvent(eventName, data);
                }
            }
        }

        private void ReceiveEvent(string eventName, JsonObject eventData)
        {
            System.Diagnostics.Debug.WriteLine("{0}: {1}", eventName, eventData);

            if (eventName == "web_app_close")
            {
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
                OpenTgLink(eventData);
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
            else if (eventName == "web_app_request_phone")
            {
                RequestPhone();
            }
            else if (eventName == "web_app_setup_closing_behavior")
            {
                SetupClosingBehaviour(eventData);
            }
            else if (eventName == "web_app_read_text_from_clipboard")
            {
                RequestClipboardText(eventData);
            }
        }

        private void RequestClipboardText(JsonObject eventData)
        {

        }

        private void SetupClosingBehaviour(JsonObject eventData)
        {

        }

        private void RequestPhone()
        {

        }

        private void OpenPopup(JsonObject eventData)
        {

        }

        private void OpenInvoice(JsonObject eventData)
        {

        }

        private void OpenExternalLink(JsonObject eventData)
        {

        }

        private void OpenTgLink(JsonObject eventData)
        {

        }

        private async void SendViewport()
        {
            await View.CoreWebView2.ExecuteScriptAsync("window.Telegram.WebView.receiveEvent('viewport_changed', {\"height\": " + 500 + "});");
        }

        private void ProcessBackButtonMessage(JsonObject eventData)
        {

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
                var button = GetTemplateChild("PrimaryButton") as Button;
                if (button != null)
                {
                    if (color.StartsWith("#") && int.TryParse(color.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexColor))
                    {
                        byte r = (byte)((hexColor & 0xff0000) >> 16);
                        byte g = (byte)((hexColor & 0x00ff00) >> 8);
                        byte b = (byte)(hexColor & 0x0000ff);

                        button.Background = new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
                    }
                    else
                    {
                        button.ClearValue(BackgroundProperty);
                    }

                    if (text_color.StartsWith("#") && int.TryParse(text_color.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexText_color))
                    {
                        byte r = (byte)((hexText_color & 0xff0000) >> 16);
                        byte g = (byte)((hexText_color & 0x00ff00) >> 8);
                        byte b = (byte)(hexText_color & 0x0000ff);

                        button.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
                    }
                    else
                    {
                        button.ClearValue(ForegroundProperty);
                    }
                }

                PrimaryButtonText = text;
                IsPrimaryButtonEnabled = is_active;
            }
            else
            {
                PrimaryButtonText = string.Empty;
            }
        }

        private void SwitchInlineQueryMessage(JsonObject eventData)
        {

        }

        private void SendDataMessage(JsonObject eventData)
        {

        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            View.Close();
        }
    }
}
