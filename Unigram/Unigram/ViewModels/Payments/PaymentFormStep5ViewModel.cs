using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Native.TL;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Payments;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Windows.System;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep5ViewModel : PaymentFormViewModelBase
    {
        private TLPaymentsValidatedRequestedInfo _requestedInfo;
        private string _credentials;
        private bool _save;

        public PaymentFormStep5ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var buffer = parameter as byte[];
            if (buffer == null)
            {
                return Task.CompletedTask;
            }

            using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            {
                var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm, TLPaymentRequestedInfo, TLPaymentsValidatedRequestedInfo, TLShippingOption, string, string, bool>(from);

                Message = tuple.Item1;
                Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
                PaymentForm = tuple.Item2;
                Info = tuple.Item3;
                Shipping = tuple.Item5;
                CredentialsTitle = string.IsNullOrEmpty(tuple.Item6) ? null : tuple.Item6;
                Bot = tuple.Item2.Users.FirstOrDefault(x => x.Id == tuple.Item2.BotId) as TLUser;
                Provider = tuple.Item2.Users.FirstOrDefault(x => x.Id == tuple.Item2.ProviderId) as TLUser;

                if (_paymentForm.HasSavedCredentials && _paymentForm.SavedCredentials is TLPaymentSavedCredentialsCard savedCard && _credentialsTitle == null)
                {
                    CredentialsTitle = savedCard.Title;
                }

                var amount = 0L;
                foreach (var price in _paymentForm.Invoice.Prices)
                {
                    amount += price.Amount;
                }

                if (_shipping != null)
                {
                    foreach (var price in _shipping.Prices)
                    {
                        amount += price.Amount;
                    }
                }

                TotalAmount = amount;

                _requestedInfo = tuple.Item4;
                _credentials = tuple.Item7;
                _save = tuple.Item8;
            }

            return Task.CompletedTask;
        }

        private TLUser _bot;
        public TLUser Bot
        {
            get
            {
                return _bot;
            }
            set
            {
                Set(ref _bot, value);
            }
        }

        private TLUser _provider;
        public TLUser Provider
        {
            get
            {
                return _provider;
            }
            set
            {
                Set(ref _provider, value);
            }
        }

        private string _credentialsTitle;
        public string CredentialsTitle
        {
            get
            {
                return _credentialsTitle;
            }
            set
            {
                Set(ref _credentialsTitle, value);
            }
        }

        private long _totalAmount;
        public long TotalAmount
        {
            get
            {
                return _totalAmount;
            }
            set
            {
                Set(ref _totalAmount, value);
            }
        }

        private TLPaymentRequestedInfo _info;
        public TLPaymentRequestedInfo Info
        {
            get
            {
                return _info;
            }
            set
            {
                Set(ref _info, value);
            }
        }

        public TLShippingOption _shipping;
        public TLShippingOption Shipping
        {
            get
            {
                return _shipping;
            }
            set
            {
                Set(ref _shipping, value);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var disclaimer = await TLMessageDialog.ShowAsync(string.Format(Strings.Android.PaymentWarningText, _bot.FullName, _provider.FullName), Strings.Android.PaymentWarning, Strings.Android.OK);

            var confirm = await TLMessageDialog.ShowAsync(string.Format(Strings.Android.PaymentTransactionMessage, LocaleHelper.FormatCurrency(_totalAmount, _paymentForm.Invoice.Currency), _bot.FullName, _invoice.Title), Strings.Android.PaymentTransactionReview, Strings.Android.OK, Strings.Android.Cancel);
            if (confirm != Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                return;
            }

            IsLoading = true;

            TLInputPaymentCredentialsBase credentials;
            if (_paymentForm.HasSavedCredentials && _paymentForm.SavedCredentials is TLPaymentSavedCredentialsCard savedCard)
            {
                credentials = new TLInputPaymentCredentialsSaved { Id = savedCard.Id, TmpPassword = ApplicationSettings.Current.TmpPassword.TmpPassword };
            }
            else
            {
                credentials = new TLInputPaymentCredentials { Data = new TLDataJSON { Data = _credentials }, IsSave = _save };
            }

            var response = await ProtoService.SendPaymentFormAsync(_message.Id, _requestedInfo?.Id, _shipping?.Id, credentials);
            if (response.IsSucceeded)
            {
                if (response.Result is TLPaymentsPaymentVerficationNeeded verificationNeeded)
                {
                    if (Uri.TryCreate(verificationNeeded.Url, UriKind.Absolute, out Uri uri))
                    {
                        await Launcher.LaunchUriAsync(uri);
                    }
                }

                NavigationService.GoBackAt(1);
            }
            else if (response.Error != null)
            {

            }
        }

        public override void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.RaisePropertyChanged(propertyName);

            if (propertyName.Equals("IsLoading"))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
