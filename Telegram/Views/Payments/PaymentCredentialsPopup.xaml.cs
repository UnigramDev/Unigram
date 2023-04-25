//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Td.Api;
using Telegram.ViewModels.Payments;
using Windows.Data.Json;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Payments
{
    public sealed partial class PaymentCredentialsPopup : ContentPopup
    {
        public PaymentCredentialsViewModel ViewModel => DataContext as PaymentCredentialsViewModel;

        public SavedCredentials Credentials { get; private set; }

        public PaymentCredentialsPopup(PaymentForm paymentForm)
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentCredentialsViewModel>();

            Title = Strings.PaymentCardInfo;

            var url = ViewModel.Initialize(paymentForm);
            if (url != null)
            {
                FindName(nameof(WebPanel));
                InitializeWebView(url);
            }
            else
            {
                PrimaryButtonText = Strings.OK;
                SecondaryButtonText = Strings.Cancel;
            }
        }

        public PaymentCredentialsPopup(PaymentForm paymentForm, PaymentOption paymentOption)
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentCredentialsViewModel>();

            Title = Strings.PaymentCardInfo;

            var url = ViewModel.Initialize(paymentForm, paymentOption);
            if (url != null)
            {
                FindName(nameof(WebPanel));
                InitializeWebView(url);
            }
            else
            {
                SecondaryButtonText = Strings.Cancel;
            }
        }

        private async void InitializeWebView(string url)
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

            View.CoreWebView2.Navigate(url);
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "CARD_NUMBER_INVALID":
                    VisualUtilities.ShakeView(FieldCard);
                    break;
                case "CARD_EXPIRE_DATE_INVALID":
                    VisualUtilities.ShakeView(FieldDate);
                    break;
                case "CARD_HOLDER_NAME_INVALID":
                    VisualUtilities.ShakeView(FieldCardName);
                    break;
                case "CARD_CVC_INVALID":
                    VisualUtilities.ShakeView(FieldCVC);
                    break;
                case "CARD_COUNTRY_INVALID":
                    VisualUtilities.ShakeView(FieldCountry);
                    break;
                case "CARD_ZIP_INVALID":
                    VisualUtilities.ShakeView(FieldPostcode);
                    break;
            }
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Credentials == null)
            {
                var deferral = args.GetDeferral();
                var validated = await ViewModel.ValidateAsync();

                Credentials = validated;

                args.Cancel = validated == null;
                deferral.Complete();
            }
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

        private void ReceiveEvent(string eventName, JsonObject data)
        {
            if (eventName == "payment_form_submit")
            {
                var response = data.GetNamedValue("credentials");
                var title = data.GetNamedString("title", string.Empty);

                var credentials = response.Stringify();

                Credentials = new SavedCredentials(credentials, title);
                Hide(ContentDialogResult.Primary);
            }
        }
    }
}
