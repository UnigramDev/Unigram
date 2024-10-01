//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Telegram.ViewModels.Payments;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Stars
{
    public enum PayResult
    {
        Succeeded,
        StarsNeeded,
        Failed
    }

    public partial class PayViewModel : ViewModelBase, IHandle
    {
        private InputInvoice _inputInvoice;

        public PayViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public string OwnedStarCount => ClientService.OwnedStarCount.ToString("N0");

        public PaymentForm PaymentForm { get; private set; }

        public IList<PaidMedia> Media { get; private set; }

        public long ChatId => _inputInvoice is InputInvoiceMessage message ? message.ChatId : 0;

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is PaymentFormArgs args)
            {
                _inputInvoice = args.InputInvoice;
                PaymentForm = args.PaymentForm;

                if (args.Content is MessagePaidAlbum paidAlbum)
                {
                    Media = paidAlbum.Media.ToList();
                }
                else if (args.Content is MessagePaidMedia paidMedia)
                {
                    Media = paidMedia.Media.ToList();
                }
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateOwnedStarCount>(this, Handle);
        }

        private void Handle(UpdateOwnedStarCount update)
        {
            BeginOnUIThread(() => RaisePropertyChanged(nameof(OwnedStarCount)));
        }

        public async Task<PayResult> SubmitAsync()
        {
            if (PaymentForm?.Type is not PaymentFormTypeStars stars)
            {
                return PayResult.Succeeded;
            }

            if (ClientService.OwnedStarCount < stars.StarCount)
            {
                var updated = await ClientService.GetStarTransactionsAsync(ClientService.MyId, string.Empty, null, string.Empty, 1) as StarTransactions;
                if (updated is null || updated.StarCount < stars.StarCount)
                {
                    return PayResult.StarsNeeded;
                }
            }

            var response = await ClientService.SendAsync(new SendPaymentForm(_inputInvoice, PaymentForm.Id, string.Empty, string.Empty, null, 0));
            if (response is PaymentResult result)
            {
                if (result.Success)
                {
                    var user = ClientService.GetUser(PaymentForm.SellerBotUserId);
                    var extended = Locale.Declension(Strings.R.StarsPurchaseCompletedInfo, stars.StarCount, PaymentForm.ProductInfo.Title, user.FullName());

                    Aggregator.Publish(new UpdateConfetti());
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.StarsPurchaseCompleted, extended), ToastPopupIcon.Success);

                    return PayResult.Succeeded;
                }
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }

            return PayResult.Failed;
        }
    }
}
