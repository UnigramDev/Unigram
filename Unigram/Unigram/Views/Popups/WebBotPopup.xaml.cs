//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Globalization;
using Telegram.Td.Api;
using Unigram.Controls;
using Telegram.Native;
using Windows.Data.Json;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Popups
{
    public sealed partial class WebBotPopup : ContentPopup
    {
        public WebBotPopup(int sessionId, WebAppInfo info, string buttonText)
        {
            InitializeComponent();

            View.Navigate(new Uri(info.Url));
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;

            await View.InvokeScriptAsync("eval", new string[]
            {
                "window.Telegram.WebView.receiveEvent('main_button_pressed', null);"
            });
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void View_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            sender.AddWebAllowedObject("TelegramWebviewProxy", new TelegramWebviewProxy(ReceiveEvent));
        }

        private async void ReceiveEvent(string eventName, string eventData)
        {
            System.Diagnostics.Debug.WriteLine("{0}: {1}", eventName, eventData);

            if (eventName == "web_app_data_send")
            {

            }
            else if (eventName == "web_app_setup_main_button")
            {
                var data = JsonObject.Parse(eventData);
                var is_visible = data.GetNamedBoolean("is_visible", false); // whether to show the button(false by default);
                var is_active = data.GetNamedBoolean("is_active", true); // whether the button is active(true by default);
                var is_progress_visible = data.GetNamedBoolean("is_progress_visible", false); // whether to show the loading process on the button(false by default);
                var text = data.GetNamedString("text", string.Empty); // text on the button(trim(text) should be non-empty, if empty, the button can be hidden);
                var color = data.GetNamedString("color", string.Empty); // background color of the button(by default button_colorfrom the theme);
                var text_color = data.GetNamedString("text_color", string.Empty); // text color on the button(by default button_text_colorfrom the theme).

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
            else if (eventName == "web_app_request_viewport")
            {
                await View.InvokeScriptAsync("eval", new string[]
                {
                    "window.Telegram.WebView.receiveEvent('viewport_changed', {\"height\": " + 500 + "});"
                });
            }
            else if (eventName == "web_app_ready")
            {

            }
            else if (eventName == "web_app_expand")
            {

            }
            else if (eventName == "web_app_close")
            {
                Hide();
            }
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            View.NavigateToString(string.Empty);
        }
    }
}
