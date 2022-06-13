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
using Unigram.Views.Payments;
using Unigram.Views.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormViewModel : TLViewModelBase
    {
        private readonly bool _save;

        public PaymentFormViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (state.TryGet("chatId", out long chatId)
                && state.TryGet("messageId", out long messageId))
            {
                var message = await ProtoService.SendAsync(new GetMessage(chatId, messageId)) as Message;
                if (message?.Content is MessageInvoice invoice)
                {
                    if (invoice.ReceiptMessageId == 0)
                    {
                        await InitializeForm(new InputInvoiceMessage(chatId, messageId));
                    }
                    else
                    {
                        await InitializeReceipt(message, invoice.ReceiptMessageId);
                    }
                }
                else if (message?.Content is MessagePaymentSuccessful)
                {
                    await InitializeReceipt(message, message.Id);
                }

            }
            else if (state.TryGet("name", out string name))
            {
                await InitializeForm(new InputInvoiceName(name));
            }
        }

        private async Task InitializeForm(InputInvoice invoice)
        {
            IsReceipt = false;

            var paymentForm = await ProtoService.SendAsync(new GetPaymentForm(invoice, new ThemeParameters())) as PaymentForm;
            if (paymentForm == null)
            {
                return;
            }

            InputInvoice = invoice;
            PaymentForm = paymentForm;

            Photo = paymentForm.ProductPhoto;
            Title = paymentForm.ProductTitle;
            Description = paymentForm.ProductDescription;

            Invoice = paymentForm.Invoice;
            Bot = CacheService.GetUser(paymentForm.SellerBotUserId);

            Credentials = paymentForm.SavedCredentials;
            Info = paymentForm.SavedOrderInfo;

            RaisePropertyChanged(nameof(HasSuggestedTipAmounts));

            if (paymentForm.SavedOrderInfo != null)
            {
                var response = await ProtoService.SendAsync(new ValidateOrderInfo(invoice, paymentForm.SavedOrderInfo, false));
                if (response is ValidatedOrderInfo validated)
                {
                    ValidatedInfo = validated;
                }
            }
        }

        private async Task InitializeReceipt(Message message, long receiptMessageId)
        {
            IsReceipt = true;

            var paymentReceipt = await ProtoService.SendAsync(new GetPaymentReceipt(message.ChatId, receiptMessageId)) as PaymentReceipt;
            if (paymentReceipt == null)
            {
                return;
            }

            Photo = paymentReceipt.Photo;
            Title = paymentReceipt.Title;
            Description = paymentReceipt.Description;

            Invoice = paymentReceipt.Invoice;
            Bot = CacheService.GetUser(paymentReceipt.SellerBotUserId);

            Credentials = new SavedCredentials(string.Empty, paymentReceipt.CredentialsTitle);
            Info = paymentReceipt.OrderInfo;

            Shipping = paymentReceipt.ShippingOption;
            TipAmount = paymentReceipt.TipAmount;
        }

        private User _bot;
        public User Bot
        {
            get => _bot;
            set => Set(ref _bot, value);
        }

        protected InputInvoice _inputInvoice;
        public InputInvoice InputInvoice
        {
            get => _inputInvoice;
            set => Set(ref _inputInvoice, value);
        }

        private bool _isReceipt;
        public bool IsReceipt
        {
            get => _isReceipt;
            set => Set(ref _isReceipt, value);
        }

        private Photo _photo;
        public Photo Photo
        {
            get => _photo;
            set => Set(ref _photo, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => Set(ref _description, value);
        }

        protected Invoice _invoice;
        public Invoice Invoice
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

        private long _tipAmount;
        public long TipAmount
        {
            get => _tipAmount;
            set
            {
                Set(ref _tipAmount, value);
                RaisePropertyChanged(nameof(TipAmountSelection));
                RaisePropertyChanged(nameof(TotalAmount));
            }
        }

        public object TipAmountSelection
        {
            get
            {
                var invoice = _paymentForm?.Invoice;
                if (invoice != null && invoice.SuggestedTipAmounts.Contains(_tipAmount))
                {
                    return _tipAmount;
                }

                return null;
            }
            set
            {
                if (value is long tip)
                {
                    Set(ref _tipAmount, tip, nameof(TipAmount));
                }
            }
        }

        public bool HasSuggestedTipAmounts => _paymentForm?.Invoice.SuggestedTipAmounts.Count > 0;

        public long TotalAmount
        {
            get
            {
                var amount = 0L;
                if (_invoice != null)
                {
                    foreach (var price in _invoice.PriceParts)
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

                return amount + _tipAmount;
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
            if (_paymentForm == null)
            {
                return;
            }

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
            if (_paymentForm == null)
            {
                return;
            }

            var popup = new PaymentAddressPopup(_inputInvoice, _paymentForm.Invoice, _info);

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && popup.ValidatedInfo != null)
            {
                ValidatedInfo = popup.ValidatedInfo;
            }
        }

        public async void ChooseShipping()
        {
            if (_paymentForm == null)
            {
                return;
            }

            var validatedInfo = _validatedInfo;
            if (validatedInfo == null)
            {
                ChooseAddress();
                return;
            }

            var items = validatedInfo.ShippingOptions.Select(
                x => new ChooseOptionItem(x, Converter.ShippingOption(x, _paymentForm.Invoice.Currency), _shipping?.Id == x.Id));

            var popup = new ChooseOptionPopup(items);
            popup.Title = Strings.Resources.PaymentCheckoutShippingMethod;
            popup.PrimaryButtonText = Strings.Resources.OK;
            popup.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && popup.SelectedIndex is ShippingOption index)
            {
                Shipping = index;
            }
        }

        public async void ChooseTipAmount()
        {
            var popup = new InputPopup(InputPopupType.Value);
            popup.Value = Converter.Amount(_tipAmount, _paymentForm.Invoice.Currency);
            popup.Maximum = Converter.Amount(_paymentForm.Invoice.MaxTipAmount, _paymentForm.Invoice.Currency);
            popup.Formatter = Locale.GetCurrencyFormatter(_paymentForm.Invoice.Currency);

            popup.Title = Strings.Resources.SearchTipToday;
            popup.PrimaryButtonText = Strings.Resources.OK;
            popup.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                TipAmount = Converter.AmountBack(popup.Value, _paymentForm.Invoice.Currency);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_paymentForm == null)
            {
                await ApplicationView.GetForCurrentView().ConsolidateAsync();
                return;
            }

            if (_credentials == null)
            {
                ChooseCredentials();
                return;
            }

            if (_info == null && _paymentForm.Invoice.NeedInfo())
            {
                ChooseAddress();
                return;
            }

            if (_shipping == null && _validatedInfo?.ShippingOptions.Count > 0)
            {
                ChooseShipping();
                return;
            }

            var bot = CacheService.GetUser(_paymentForm.SellerBotUserId);
            var provider = CacheService.GetUser(_paymentForm.PaymentProviderUserId);

            var disclaimer = await MessagePopup.ShowAsync(string.Format(Strings.Resources.PaymentWarningText, bot.FirstName, provider.FirstName), Strings.Resources.PaymentWarning, Strings.Resources.OK);

            var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.PaymentTransactionMessage, Locale.FormatCurrency(TotalAmount, _paymentForm.Invoice.Currency), bot.FirstName, _title), Strings.Resources.PaymentTransactionReview, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            IsLoading = true;

            var formId = _paymentForm.Id;
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

            if (credentials == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new SendPaymentForm(_inputInvoice, formId, infoId, shippingId, credentials, 0));
            if (response is PaymentResult result)
            {
                if (Uri.TryCreate(result.VerificationUrl, UriKind.Absolute, out Uri uri))
                {
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }

                await ApplicationView.GetForCurrentView().ConsolidateAsync();
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
            var popup = new InputPopup(InputPopupType.Password);
            popup.Header = string.Format(Strings.Resources.PaymentConfirmationMessage, _paymentForm.SavedCredentials.Title);
            popup.PrimaryButtonText = Strings.Resources.Continue;
            popup.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return new TemporaryPasswordState(false, 0);
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
