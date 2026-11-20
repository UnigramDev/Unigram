//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class TransferGiftPopup : ModalPopup
    {
        private readonly UpgradedGift _resale;
        private readonly Chat _chat;

        private bool _gram;

        public string Text { get; set; } = string.Empty;

        public TransferGiftPopup(IClientService clientService, ReceivedGift gift, Chat chat)
        {
            InitializeComponent();

            if (gift.Gift is SentGiftUpgraded upgraded)
            {
                InitializePhotos(clientService, upgraded.Gift, chat);

                if (gift.TransferStarCount > 0)
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, Locale.Declension(Strings.R.Gift2TransferPriceText, gift.TransferStarCount, upgraded.Gift.ToName(), chat.Title));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, string.Format(Strings.Gift2TransferText, upgraded.Gift.ToName(), chat.Title));
                }

                PrimaryButtonContent = Strings.Gift2TransferDo;
            }

            PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            SecondaryButtonContent = Strings.Cancel;
        }

        /// <summary>
        /// Confirms buying a gift on sale, for the current user when chat is null.
        /// </summary>
        public TransferGiftPopup(IClientService clientService, UpgradedGift gift, Chat chat, bool gram)
        {
            InitializeComponent();

            _resale = gift;
            _chat = chat;

            InitializePhotos(clientService, gift, chat);

            PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            SecondaryButtonContent = Strings.Cancel;

            if (gift.ResaleParameters.GramOnly)
            {
                CurrencyInfo.Text = Strings.Gift2BuyPriceOnlyTON;
                CurrencyInfo.Visibility = Visibility.Visible;
                UpdateCurrency(true);
            }
            else
            {
                Currency.Visibility = Visibility.Visible;
                Currency.SelectedIndex = gram ? 1 : 0;
                Currency.SelectionChanged += Currency_SelectionChanged;
                UpdateCurrency(gram);
            }
        }

        public bool IsGram => _gram;

        public GiftResalePrice Price => _gram
            ? new GiftResalePriceGram(_resale.ResaleParameters.GramCentCount)
            : new GiftResalePriceStar(_resale.ResaleParameters.StarCount);

        private void InitializePhotos(IClientService clientService, UpgradedGift gift, Chat chat)
        {
            Animated.Source = new DelayedFileSource(clientService, gift.Model.Sticker);

            Photo1.Update(clientService, gift);

            if (chat != null)
            {
                Photo2.Source = ProfilePictureSource.Chat(clientService, chat);
            }
            else if (clientService.TryGetUser(clientService.Options.MyId, out User user))
            {
                Photo2.Source = ProfilePictureSource.User(clientService, user);
            }
        }

        private void UpdateCurrency(bool gram)
        {
            _gram = gram;

            var parameters = _resale.ResaleParameters;
            var name = _resale.ToName();

            if (gram)
            {
                var amount = FormatGram(parameters.GramCentCount);

                TextBlockHelper.SetMarkdown(MessageLabel, _chat != null
                    ? string.Format(Strings.Gift2BuyPriceTextTON, amount, name, _chat.Title)
                    : string.Format(Strings.Gift2BuyPriceSelfTextTON, amount, name));

                PrimaryButtonText = string.Format(Strings.Gift2BuyDoPrice2TON, amount).ReplaceStar(Icons.Gram14);
            }
            else
            {
                TextBlockHelper.SetMarkdown(MessageLabel, _chat != null
                    ? Locale.Declension(Strings.R.Gift2BuyPriceText, parameters.StarCount, name, _chat.Title)
                    : Locale.Declension(Strings.R.Gift2BuyPriceSelfText, parameters.StarCount, name));

                PrimaryButtonText = Locale.Declension(Strings.R.Gift2BuyDoPrice2, parameters.StarCount).ReplaceStar(Icons.Premium);
            }

            // The hint sells Grams, so it goes once they are picked.
            if (!parameters.GramOnly)
            {
                CurrencyInfo.Text = Strings.Gift2BuyPricePayHintTON;
                CurrencyInfo.Visibility = gram ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void Currency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Currency.SelectedIndex >= 0)
            {
                UpdateCurrency(Currency.SelectedIndex == 1);
            }
        }

        public static string FormatGram(long gramCentCount)
        {
            return (gramCentCount / 100d).ToString("0.##");
        }

        /// <summary>
        /// Confirms and pays for a gift on sale, asking again whenever the seller raised the price meanwhile.
        /// Returns null when the user gave up, otherwise TDLib's answer or <see cref="ErrorStarsNeeded"/>.
        /// </summary>
        public static async Task<Object> BuyAsync(IClientService clientService, INavigationService navigationService, UpgradedGift gift, Chat chat, MessageSender ownerId, FormattedText text, bool isPrivate)
        {
            var gram = gift.ResaleParameters.GramOnly;

            while (true)
            {
                var popup = new TransferGiftPopup(clientService, gift, chat, gram);

                var confirm = await navigationService.ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    return null;
                }

                gram = popup.IsGram;

                var function = new SendResoldGift(gift.Name, ownerId, popup.Price, text, isPrivate);
                Object response;

                if (gram)
                {
                    if (!await HasGramsAsync(clientService, navigationService, gift.ResaleParameters.GramCentCount))
                    {
                        return null;
                    }

                    response = await clientService.SendAsync(function);
                }
                else
                {
                    response = await clientService.SendPaymentAsync(gift.ResaleParameters.StarCount, function);
                }

                if (response is GiftResaleResultPriceIncreased increased)
                {
                    if (increased.Price is GiftResalePriceStar star)
                    {
                        gift.ResaleParameters.StarCount = star.StarCount;
                    }
                    else if (increased.Price is GiftResalePriceGram grams)
                    {
                        gift.ResaleParameters.GramCentCount = grams.GramCentCount;
                    }

                    navigationService.ShowToast(Strings.GiftOnchainErrorPriceChangedFixed, ToastPopupIcon.Info);
                    continue;
                }

                return response;
            }
        }

        /// <summary>
        /// False, after offering to top up, when the balance is short of <paramref name="gramCentCount"/>.
        /// </summary>
        public static async Task<bool> HasGramsAsync(IClientService clientService, INavigationService navigationService, long gramCentCount)
        {
            // The balance is in nanograms, and a gram cent is 10^7 of them.
            var required = gramCentCount * 10_000_000;
            if (clientService.OwnedGramCount < required)
            {
                await clientService.GetTonTransactionsAsync(null, string.Empty, 1);
            }

            var missing = required - clientService.OwnedGramCount;
            if (missing <= 0)
            {
                return true;
            }

            // Rounded up, so that topping up the amount shown is always enough.
            var title = string.Format(Strings.TonNeededTitle, FormatGram((missing + 9_999_999) / 10_000_000));

            var confirm = await navigationService.ShowPopupAsync(Strings.FragmentAddFunds, title, Strings.TopUpViaFragment, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var url = clientService.Options.GramTopUpUrl;
                MessageHelper.OpenUrl(null, null, string.IsNullOrEmpty(url) ? Strings.TopUpViaFragmentLink : url);
            }

            return false;
        }
    }
}
