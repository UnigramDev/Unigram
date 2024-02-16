//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Payments;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Payments
{
    public class PaymentFormArgs
    {
        public PaymentFormArgs(InputInvoice inputInvoice, PaymentForm paymentForm)
        {
            InputInvoice = inputInvoice;
            PaymentForm = paymentForm;
        }

        public InputInvoice InputInvoice { get; }

        public PaymentForm PaymentForm { get; }
    }

    public class PaymentFormViewModel : ViewModelBase
    {
        public PaymentFormViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is InputInvoiceMessage invoiceMessage)
            {
                var message = await ClientService.SendAsync(new GetMessage(invoiceMessage.ChatId, invoiceMessage.MessageId)) as Message;
                if (message?.Content is MessageInvoice invoice)
                {
                    if (invoice.ReceiptMessageId == 0)
                    {
                        await InitializeForm(invoiceMessage);
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
            else if (parameter is InputInvoiceName invoiceName)
            {
                await InitializeForm(invoiceName);
            }
            else if (parameter is PaymentFormArgs paymentForm)
            {
                await InitializeForm(paymentForm.InputInvoice, paymentForm.PaymentForm);
            }
        }

        private async Task InitializeForm(InputInvoice invoice)
        {
            var paymentForm = await ClientService.SendAsync(new GetPaymentForm(invoice, Theme.Current.Parameters)) as PaymentForm;
            if (paymentForm == null)
            {
                return;
            }

            await InitializeForm(invoice, paymentForm);
        }

        private async Task InitializeForm(InputInvoice invoice, PaymentForm paymentForm)
        {
            IsReceipt = false;

            InputInvoice = invoice;
            PaymentForm = paymentForm;

            Photo = paymentForm.ProductPhoto;
            Title = paymentForm.ProductTitle;
            Description = paymentForm.ProductDescription;

            Invoice = paymentForm.Invoice;
            Bot = ClientService.GetUser(paymentForm.SellerBotUserId);

            Credentials = paymentForm.SavedCredentials.FirstOrDefault();
            Info = paymentForm.SavedOrderInfo;

            RaisePropertyChanged(nameof(HasSuggestedTipAmounts));

            if (paymentForm.SavedOrderInfo != null)
            {
                var response = await ClientService.SendAsync(new ValidateOrderInfo(invoice, paymentForm.SavedOrderInfo, false));
                if (response is ValidatedOrderInfo validated)
                {
                    ValidatedInfo = validated;
                }
            }
        }

        private async Task InitializeReceipt(Message message, long receiptMessageId)
        {
            IsReceipt = true;

            var paymentReceipt = await ClientService.SendAsync(new GetPaymentReceipt(message.ChatId, receiptMessageId)) as PaymentReceipt;
            if (paymentReceipt == null)
            {
                return;
            }

            Photo = paymentReceipt.Photo;
            Title = paymentReceipt.Title;
            Description = paymentReceipt.Description;

            Invoice = paymentReceipt.Invoice;
            Bot = ClientService.GetUser(paymentReceipt.SellerBotUserId);

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

        private FormattedText _description;
        public FormattedText Description
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
            if (_paymentForm is not PaymentForm form)
            {
                return;
            }

            if (form.SavedCredentials.Count > 0
                || form.AdditionalPaymentOptions.Count > 0)
            {
                var credentials = form.SavedCredentials.Select(x => new ChooseOptionItem(x, x.Title, false));
                var additional = form.AdditionalPaymentOptions.Select(x => new ChooseOptionItem(x, x.Title, false));

                var items = new[] { new ChooseOptionItem(null, Strings.PaymentCheckoutMethodNewCard, false) };

                var choose = new ChooseOptionPopup(items.Union(credentials).Union(additional));
                choose.Title = Strings.PaymentCheckoutMethod;
                choose.PrimaryButtonText = Strings.OK;
                choose.SecondaryButtonText = Strings.Cancel;

                var confirm1 = await ShowPopupAsync(choose);
                if (confirm1 == ContentDialogResult.Primary)
                {
                    if (choose.SelectedIndex is SavedCredentials savedCredentials)
                    {
                        _inputCredentials = new InputCredentialsSaved(savedCredentials.Id);
                        Credentials = savedCredentials;
                        return;
                    }
                    else if (choose.SelectedIndex is PaymentOption paymentOption)
                    {
                        var option = new PaymentCredentialsPopup(form, paymentOption);

                        var confirm2 = await ShowPopupAsync(option);
                        if (confirm2 == ContentDialogResult.Primary && option.Credentials != null)
                        {
                            _inputCredentials = new InputCredentialsNew(option.Credentials.Id, true);
                            Credentials = option.Credentials;
                        }

                        return;
                    }
                }
            }

            var popup = new PaymentCredentialsPopup(_paymentForm);

            var confirm = await ShowPopupAsync(popup);
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

            var confirm = await ShowPopupAsync(popup);
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
                x => new ChooseOptionItem(x, Formatter.ShippingOption(x, _paymentForm.Invoice.Currency), _shipping?.Id == x.Id));

            var popup = new ChooseOptionPopup(items);
            popup.Title = Strings.PaymentCheckoutShippingMethod;
            popup.PrimaryButtonText = Strings.OK;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary && popup.SelectedIndex is ShippingOption index)
            {
                Shipping = index;
            }
        }

        public async void ChooseTipAmount()
        {
            var popup = new InputPopup(InputPopupType.Value);
            popup.Value = Formatter.Amount(_tipAmount, _paymentForm.Invoice.Currency);
            popup.Maximum = Formatter.Amount(_paymentForm.Invoice.MaxTipAmount, _paymentForm.Invoice.Currency);
            popup.Formatter = Locale.GetCurrencyFormatter(_paymentForm.Invoice.Currency);

            popup.Title = Strings.SearchTipToday;
            popup.PrimaryButtonText = Strings.OK;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                TipAmount = Formatter.AmountBack(popup.Value, _paymentForm.Invoice.Currency);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_paymentForm == null)
            {
                await WindowContext.Current.ConsolidateAsync();
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

            var bot = ClientService.GetUser(_paymentForm.SellerBotUserId);
            var provider = ClientService.GetUser(_paymentForm.PaymentProviderUserId);

            var disclaimer = await ShowPopupAsync(string.Format(Strings.PaymentWarningText, bot.FirstName, provider.FirstName), Strings.PaymentWarning, Strings.OK);

            var confirm = await ShowPopupAsync(string.Format(Strings.PaymentTransactionMessage, Locale.FormatCurrency(TotalAmount, _paymentForm.Invoice.Currency), bot.FirstName, _title), Strings.PaymentTransactionReview, Strings.OK, Strings.Cancel);
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
                if (_paymentForm.SavedCredentials.Count > 0)
                {
                    var password = await GetTemporaryPasswordStateAsync();
                    if (password.HasPassword && password.ValidFor > 0)
                    {
                        credentials = new InputCredentialsSaved(_paymentForm.SavedCredentials[0].Id);
                    }
                }
            }

            if (credentials == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new SendPaymentForm(_inputInvoice, formId, infoId, shippingId, credentials, 0));
            if (response is PaymentResult result)
            {
                if (Uri.TryCreate(result.VerificationUrl, UriKind.Absolute, out Uri uri))
                {
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }

                await WindowContext.Current.ConsolidateAsync();
            }
        }

        private async Task<TemporaryPasswordState> GetTemporaryPasswordStateAsync()
        {
            var response = await ClientService.SendAsync(new GetTemporaryPasswordState());
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
            popup.Header = string.Format(Strings.PaymentConfirmationMessage, _paymentForm.SavedCredentials[0].Title);
            popup.PrimaryButtonText = Strings.Continue;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return new TemporaryPasswordState(false, 0);
            }

            var response = await ClientService.SendAsync(new CreateTemporaryPassword(popup.Text, 60 * 30));
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
