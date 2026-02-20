//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class MessageUpgradedGiftPurchaseOfferContent : MessageService
    {
        public MessageUpgradedGiftPurchaseOfferContent()
        {
            InitializeComponent();
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            if (message.Content is not MessageUpgradedGiftPurchaseOffer upgradedGiftPurchaseOffer)
            {
                return;
            }

            Pattern.Update(message.ClientService, upgradedGiftPurchaseOffer.Gift);
            Animation.Source = DelayedFileSource.FromSticker(message.ClientService, upgradedGiftPurchaseOffer.Gift.Model.Sticker);

            var now = DateTime.Now.ToTimestamp();
            if (now < upgradedGiftPurchaseOffer.ExpirationDate && upgradedGiftPurchaseOffer.State is GiftPurchaseOfferStatePending && !message.IsOutgoing)
            {
                Accept.Visibility = Visibility.Visible;
                Reject.Visibility = Visibility.Visible;
                Service.CornerRadius = new CornerRadius(11, 11, 4, 4);
            }
            else
            {
                Accept.Visibility = Visibility.Collapsed;
                Reject.Visibility = Visibility.Collapsed;
                Service.CornerRadius = new CornerRadius(11);
            }
        }

        private async void Reject_Click(object sender, RoutedEventArgs e)
        {
            Message.ClientService.TryGetUser(Message.Chat, out User user);

            var confirm = await Message.Delegate.NavigationService.ShowPopupAsync(string.Format(Strings.GiftOfferRejectConfirmText, user.FullName(true)), Strings.GiftOfferRejectConfirmTitle, Strings.GiftOfferRejectConfirmConfirm, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                Message.ClientService.Send(new ProcessGiftPurchaseOffer(Message.Id, false));
            }
        }

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (Message.Content is not MessageUpgradedGiftPurchaseOffer upgradedGiftPurchaseOffer)
            {
                return;
            }

            Message.ClientService.TryGetUser(Message.Chat, out User user);

            var message = string.Empty;

            if (upgradedGiftPurchaseOffer.Price is GiftResalePriceStar resalePriceStar)
            {
                message = string.Format(Strings.GiftOfferTransferInfoTextSellStars, resalePriceStar.ToCount(), user.FullName(true), upgradedGiftPurchaseOffer.Gift.ToName(), resalePriceStar.ToCount(Message.ClientService.Options.GiftResaleStarEarningsPerMille));
            }
            else if (upgradedGiftPurchaseOffer.Price is GiftResalePriceTon resalePriceTon)
            {
                message = string.Format(Strings.GiftOfferTransferInfoTextSellTON, resalePriceTon.ToCount(), user.FullName(true), upgradedGiftPurchaseOffer.Gift.ToName(), resalePriceTon.ToCount(Message.ClientService.Options.GiftResaleToncoinEarningsPerMille));
            }

            var confirm = await Message.Delegate.NavigationService.ShowPopupAsync(string.Format(message, user.FullName(true)), Strings.GiftOfferRejectConfirmTitle, Strings.GiftOfferSellFor, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Message.ClientService.Send(new ProcessGiftPurchaseOffer(Message.Id, true));
            }
        }
    }
}
