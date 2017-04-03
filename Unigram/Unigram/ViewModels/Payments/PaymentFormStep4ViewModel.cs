using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep4ViewModel : PaymentFormViewModelBase
    {
        private TLPaymentRequestedInfo _info;
        private TLPaymentsValidatedRequestedInfo _requestedInfo;
        private TLShippingOption _shipping;

        public PaymentFormStep4ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
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
                    var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm, TLPaymentRequestedInfo, TLPaymentsValidatedRequestedInfo, TLShippingOption>(from);

                    Message = tuple.Item1;
                    Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
                    PaymentForm = tuple.Item2;

                    _info = tuple.Item3;
                    _requestedInfo = tuple.Item4;
                    _shipping = tuple.Item5;
                }
            }

            return Task.CompletedTask;
        }

        private string _password;
        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                Set(ref _password, value);
            }
        }

        private RelayCommand _sendCommand;
        public RelayCommand SendCommand => _sendCommand = _sendCommand ?? new RelayCommand(SendExecute, () => !IsLoading);
        private async void SendExecute()
        {
            IsLoading = true;

            var passwordResponse = await ProtoService.GetPasswordAsync();
            if (passwordResponse.IsSucceeded)
            {
                if (passwordResponse.Result is TLAccountPassword password)
                {
                    var currentSalt = password.CurrentSalt;
                    var hash = TLUtils.Combine(currentSalt, Encoding.UTF8.GetBytes(_password), currentSalt);

                    var input = CryptographicBuffer.CreateFromByteArray(hash);
                    var hasher = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);
                    var hashed = hasher.HashData(input);
                    CryptographicBuffer.CopyToByteArray(hashed, out byte[] data);

                    var response = await ProtoService.GetTmpPasswordAsync(data, 60 * 30);
                    if (response.IsSucceeded)
                    {
                        ApplicationSettings.Current.TmpPassword = response.Result;
                        NavigationService.NavigateToPaymentFormStep5(_message, _paymentForm, _info, _requestedInfo, _shipping, null, null);
                    }
                    else if (response.Error != null)
                    {
                        IsLoading = false;

                        if (response.Error.TypeEquals(TLErrorType.PASSWORD_HASH_INVALID))
                        {
                            Password = string.Empty;
                            RaisePropertyChanged(response.Error.ErrorMessage);
                        }
                        else
                        {

                        }
                    }
                }
                else
                {

                }
            }
            else if (passwordResponse.Error != null)
            {
                IsLoading = false;
            }
        }

        public RelayCommand ChooseCommand => new RelayCommand(ChooseExecute);
        private void ChooseExecute()
        {
            NavigationService.NavigateToPaymentFormStep3(_message, _paymentForm, _info, _requestedInfo, _shipping);
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
