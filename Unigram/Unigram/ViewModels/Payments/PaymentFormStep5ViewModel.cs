using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep5ViewModel : TLViewModelBase
    {
        private readonly bool _save;

        public PaymentFormStep5ViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            state.TryGet("chatId", out long chatId);
            state.TryGet("messageId", out long messageId);

            var message = await ProtoService.SendAsync(new GetMessage(chatId, messageId)) as Message;
            var paymentForm = await ProtoService.SendAsync(new GetPaymentForm(chatId, messageId)) as PaymentForm;

            if (message == null || paymentForm == null)
            {
                return;
            }

            Message = message;
            Invoice = message.Content as MessageInvoice;
            PaymentForm = paymentForm;

            Credentials = paymentForm.SavedCredentials;
            Info = paymentForm.SavedOrderInfo;

            if (paymentForm.SavedOrderInfo != null)
            {
                var response = await ProtoService.SendAsync(new ValidateOrderInfo(chatId, messageId, paymentForm.SavedOrderInfo, false));
                if (response is ValidatedOrderInfo validated)
                {
                    ValidatedInfo = validated;
                }
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

            //    _requestedInfo = tuple.Item4;
            //    _credentials = tuple.Item7;
            //    _save = tuple.Item8;
            //}
        }

        private User _bot;
        public User Bot
        {
            get => _bot;
            set => Set(ref _bot, value);
        }

        protected Message _message;
        public Message Message
        {
            get => _message;
            set => Set(ref _message, value);
        }

        protected MessageInvoice _invoice;
        public MessageInvoice Invoice
        {
            get => _invoice;
            set => Set(ref _invoice, value);
        }

        protected PaymentForm _paymentForm;
        public PaymentForm PaymentForm
        {
            get => _paymentForm;
            set
            {
                Set(ref _paymentForm, value);
                RaisePropertyChanged(nameof(TotalAmount));
            }
        }

        private ValidatedOrderInfo _validatedInfo;
        public ValidatedOrderInfo ValidatedInfo
        {
            get => _validatedInfo;
            set => Set(ref _validatedInfo, value);
        }

        private InputCredentials _inputCredentials;

        private SavedCredentials _credentials;
        public SavedCredentials Credentials
        {
            get => _credentials;
            set => Set(ref _credentials, value);
        }

        public long TotalAmount
        {
            get
            {
                var amount = 0L;
                if (_paymentForm != null)
                {
                    foreach (var price in _paymentForm.Invoice.PriceParts)
                    {
                        amount += price.Amount;
                    }
                }

                if (_shipping != null)
                {
                    foreach (var price in _shipping.PriceParts)
                    {
                        amount += price.Amount;
                    }
                }

                return amount;
            }
        }

        private OrderInfo _info;
        public OrderInfo Info
        {
            get => _info;
            set => Set(ref _info, value);
        }

        public ShippingOption _shipping;
        public ShippingOption Shipping
        {
            get => _shipping;
            set
            {
                Set(ref _shipping, value);
                RaisePropertyChanged(nameof(TotalAmount));
            }
        }

        public async void ChooseCredentials()
        {
            var popup = new PaymentCredentialsPopup(_paymentForm);
            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && popup.Credentials != null)
            {
                _inputCredentials = new InputCredentialsNew(popup.Credentials.Id, true);
                Credentials = popup.Credentials;
            }
        }

        public async void ChooseAddress()
        {
            var popup = new PaymentAddressPopup(_message.ChatId, _message.Id, _paymentForm.Invoice, _info);
            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && popup.ValidatedInfo != null)
            {
                ValidatedInfo = popup.ValidatedInfo;
            }
        }

        public async void ChooseShipping()
        {
            var items = _validatedInfo.ShippingOptions.Select(
                x => new SelectRadioItem(x, Converter.ShippingOption(x, _paymentForm.Invoice.Currency), _shipping?.Id == x.Id));

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.PaymentCheckoutShippingMethod;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is ShippingOption index)
            {
                Shipping = index;
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_credentials == null)
            {
                ChooseCredentials();
                return;
            }

            if (_info == null)
            {
                ChooseAddress();
                return;
            }

            if (_shipping == null)
            {
                ChooseShipping();
                return;
            }

            var bot = CacheService.GetMessageSender(_message.Sender) as User;
            var provider = CacheService.GetMessageSender(_message.Sender) as User;

            var disclaimer = await MessagePopup.ShowAsync(string.Format(Strings.Resources.PaymentWarningText, bot.FirstName, provider.FirstName), Strings.Resources.PaymentWarning, Strings.Resources.OK);

            var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.PaymentTransactionMessage, Locale.FormatCurrency(TotalAmount, _paymentForm.Invoice.Currency), bot.FirstName, _invoice.Title), Strings.Resources.PaymentTransactionReview, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            IsLoading = true;

            var infoId = _validatedInfo?.OrderInfoId ?? string.Empty;
            var shippingId = _shipping?.Id ?? string.Empty;

            var credentials = _inputCredentials;
            if (credentials == null)
            {
                if (_paymentForm.SavedCredentials != null)
                {
                    var password = await GetTemporaryPasswordStateAsync();
                    if (password.HasPassword && password.ValidFor > 0)
                    {
                        credentials = new InputCredentialsSaved(_paymentForm.SavedCredentials.Id);
                    }
                }
            }

            var response = await ProtoService.SendAsync(new SendPaymentForm(_message.ChatId, _message.Id, infoId, shippingId, credentials));
            if (response is PaymentResult result)
            {
                if (Uri.TryCreate(result.VerificationUrl, UriKind.Absolute, out Uri uri))
                {
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }

                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
        }

        private async Task<TemporaryPasswordState> GetTemporaryPasswordStateAsync()
        {
            var response = await ProtoService.SendAsync(new GetTemporaryPasswordState());
            if (response is TemporaryPasswordState state)
            {
                if (state.HasPassword && state.ValidFor > 0)
                {
                    return state;
                }

                return await CreateTemporaryPasswordAsync();
            }
            else if (response is Error error)
            {
            }

            return new TemporaryPasswordState(false, 0);
        }

        private async Task<TemporaryPasswordState> CreateTemporaryPasswordAsync()
        {
            var popup = new InputPopup(true);
            popup.Header = string.Format(Strings.Resources.PaymentConfirmationMessage, _paymentForm.SavedCredentials.Title);
            popup.PrimaryButtonText = Strings.Resources.Continue;
            popup.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            var response = await ProtoService.SendAsync(new CreateTemporaryPassword(popup.Text, 60 * 30));
            if (response is TemporaryPasswordState state)
            {
                return state;
            }
            else if (response is Error error)
            {

            }

            return new TemporaryPasswordState(false, 0);
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
