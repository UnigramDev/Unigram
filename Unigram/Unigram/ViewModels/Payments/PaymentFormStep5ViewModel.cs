using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep5ViewModel : PaymentFormViewModelBase
    {
        private ValidatedOrderInfo _requestedInfo;
        private string _credentials;
        private bool _save;

        public PaymentFormStep5ViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
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

            //using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            //{
            //    var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm, TLPaymentRequestedInfo, TLPaymentsValidatedRequestedInfo, TLShippingOption, string, string, bool>(from);

            //    Message = tuple.Item1;
            //    Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
            //    PaymentForm = tuple.Item2;
            //    Info = tuple.Item3;
            //    Shipping = tuple.Item5;
            //    CredentialsTitle = string.IsNullOrEmpty(tuple.Item6) ? null : tuple.Item6;
            //    Bot = tuple.Item2.Users.FirstOrDefault(x => x.Id == tuple.Item2.BotId) as TLUser;
            //    Provider = tuple.Item2.Users.FirstOrDefault(x => x.Id == tuple.Item2.ProviderId) as TLUser;

            //    if (_paymentForm.HasSavedCredentials && _paymentForm.SavedCredentials is TLPaymentSavedCredentialsCard savedCard && _credentialsTitle == null)
            //    {
            //        CredentialsTitle = savedCard.Title;
            //    }

            //    var amount = 0L;
            //    foreach (var price in _paymentForm.Invoice.Prices)
            //    {
            //        amount += price.Amount;
            //    }

            //    if (_shipping != null)
            //    {
            //        foreach (var price in _shipping.Prices)
            //        {
            //            amount += price.Amount;
            //        }
            //    }

            //    TotalAmount = amount;

            //    _requestedInfo = tuple.Item4;
            //    _credentials = tuple.Item7;
            //    _save = tuple.Item8;
            //}

            return Task.CompletedTask;
        }

        private User _bot;
        public User Bot
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

        private User _provider;
        public User Provider
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

        private OrderInfo _info;
        public OrderInfo Info
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

        public ShippingOption _shipping;
        public ShippingOption Shipping
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
            var disclaimer = await MessagePopup.ShowAsync(string.Format(Strings.Resources.PaymentWarningText, _bot.FirstName, _provider.FirstName), Strings.Resources.PaymentWarning, Strings.Resources.OK);

            var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.PaymentTransactionMessage, Locale.FormatCurrency(_totalAmount, _paymentForm.Invoice.Currency), _bot.FirstName, _invoice.Title), Strings.Resources.PaymentTransactionReview, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                return;
            }

            IsLoading = true;

            //TLInputPaymentCredentialsBase credentials;
            //if (_paymentForm.HasSavedCredentials && _paymentForm.SavedCredentials is TLPaymentSavedCredentialsCard savedCard)
            //{
            //    credentials = new TLInputPaymentCredentialsSaved { Id = savedCard.Id, TmpPassword = ApplicationSettings.Current.TmpPassword.TmpPassword };
            //}
            //else
            //{
            //    credentials = new TLInputPaymentCredentials { Data = new TLDataJSON { Data = _credentials }, IsSave = _save };
            //}

            //var response = await LegacyService.SendPaymentFormAsync(_message.Id, _requestedInfo?.Id, _shipping?.Id, credentials);
            //if (response.IsSucceeded)
            //{
            //    if (response.Result is TLPaymentsPaymentVerficationNeeded verificationNeeded)
            //    {
            //        if (Uri.TryCreate(verificationNeeded.Url, UriKind.Absolute, out Uri uri))
            //        {
            //            await Launcher.LaunchUriAsync(uri);
            //        }
            //    }

            //    NavigationService.GoBackAt(1);
            //}
            //else if (response.Error != null)
            //{

            //}
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
