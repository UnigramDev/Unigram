using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep5ViewModel : PaymentFormViewModelBase
    {
        private TLPaymentsValidatedRequestedInfo _requestedInfo;
        private string _credentials;

        public PaymentFormStep5ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var buffer = parameter as byte[];
            if (buffer != null)
            {
                using (var from = new TLBinaryReader(buffer))
                {
                    var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm, TLPaymentRequestedInfo, TLPaymentsValidatedRequestedInfo, TLShippingOption, string, string>(from);

                    Message = tuple.Item1;
                    Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
                    PaymentForm = tuple.Item2;
                    Info = tuple.Item3;
                    Shipping = tuple.Item5;
                    CredentialsTitle = string.IsNullOrEmpty(tuple.Item7) ? _paymentForm.SavedCredentials.Title : tuple.Item6;
                    Bot = tuple.Item2.Users.FirstOrDefault(x => x.Id == tuple.Item2.BotId) as TLUser;

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
                }
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

        private RelayCommand _sendCommand;
        public RelayCommand SendCommand => _sendCommand = _sendCommand ?? new RelayCommand(SendExecute, () => !IsLoading);
        private async void SendExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(string.Format("Do you really want to transfer {0} to the {1} bot for {2}?", BindConvert.Current.FormatAmount(_totalAmount, _paymentForm.Invoice.Currency), _bot.FullName, _invoice.Title), "Transaction review", "OK", "Cancel");
            if (confirm != Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                return;
            }

            IsLoading = true;

            TLInputPaymentCredentialsBase credentials;
            if (string.IsNullOrEmpty(_credentials))
            {
                credentials = new TLInputPaymentCredentialsSaved { Id = _paymentForm.SavedCredentials.Id, TmpPassword = ApplicationSettings.Current.TmpPassword.TmpPassword };
            }
            else
            {
                credentials = new TLInputPaymentCredentials { Data = new TLDataJSON { Data = _credentials } };
            }

            var response = await ProtoService.SendPaymentFormAsync(_message.Id, _requestedInfo?.Id, _shipping?.Id, credentials);
            if (response.IsSucceeded)
            {
                var verificatioNeeded = response.Result as TLPaymentsPaymentVerficationNeeded;
                if (verificatioNeeded != null)
                {
                    
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
