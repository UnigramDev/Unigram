//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;

namespace Telegram.Views.Stars.Popups
{
    /// <summary>
    /// What an offer to buy a gift costs: the offer itself, the owner's message price, and how long
    /// it stands.
    /// </summary>
    public sealed partial class GiftOfferConfirmPopup : ModalPopup
    {
        public GiftOfferConfirmPopup(UpgradedGift gift, string ownerName, bool gram, long value, long fee, int duration)
        {
            InitializeComponent();

            Title = Strings.GiftOfferConfirmSend;
            SecondaryButtonText = Strings.Cancel;

            var amount = gram ? TransferGiftPopup.FormatGram(value) : value.ToString("N0");

            TextBlockHelper.SetMarkdown(MessageLabel, string.Format(gram ? Strings.GiftOfferTransferInfoTextTON : Strings.GiftOfferTransferInfoTextStars, amount, ownerName, gift.ToName()));

            OfferRow.Content = string.Format(Strings.GiftOfferAmount, amount).ReplaceStar(gram ? Icons.Gram14 : Icons.PremiumStar);
            DurationRow.Content = Locale.Declension(Strings.R.GiftOfferHours, duration / 3600);

            if (fee > 0)
            {
                FeeRow.Content = string.Format(Strings.GiftOfferAmount, fee.ToString("N0")).ReplaceStar(Icons.PremiumStar);
                FeeRow.Visibility = Visibility.Visible;
            }

            // The fee is always in Stars, so a Gram offer with a fee pays in both.
            if (gram && fee > 0)
            {
                var grams = string.Format(Strings.GiftOfferPayMultiPart, amount).ReplaceStar(Icons.Gram14);
                var stars = string.Format(Strings.GiftOfferPayMultiPart, fee.ToString("N0")).ReplaceStar(Icons.Premium);

                PrimaryButtonText = string.Format(Strings.GiftOfferPayMulti, grams, stars);
            }
            else if (gram)
            {
                PrimaryButtonText = string.Format(Strings.GiftOfferPay, amount).ReplaceStar(Icons.Gram14);
            }
            else
            {
                PrimaryButtonText = string.Format(Strings.GiftOfferPay, (value + fee).ToString("N0")).ReplaceStar(Icons.Premium);
            }
        }
    }
}
