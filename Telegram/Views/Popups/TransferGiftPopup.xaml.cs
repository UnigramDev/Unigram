//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Stars.Popups;
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
                var name = clientService.GetTitle(chat, true);

                InitializePhotos(clientService, upgraded.Gift, chat);
                InitializeAttributes(upgraded.Gift);

                if (gift.TransferStarCount > 0)
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, Locale.Declension(Strings.R.Gift2TransferPriceText, gift.TransferStarCount, upgraded.Gift.ToName(), name));
                    PrimaryButtonText = string.Format(Strings.Gift2TransferDoPrice, gift.TransferStarCount.ToString("N0")).ReplaceStar(Icons.Premium);
                }
                else
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, string.Format(Strings.Gift2TransferText, upgraded.Gift.ToName(), name));
                    PrimaryButtonText = Strings.Gift2TransferDo;
                }
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

        private void InitializeAttributes(UpgradedGift gift)
        {
            Attributes.Visibility = Visibility.Visible;

            ModelText.Text = gift.Model.Name;
            ModelRarity.Glyph = gift.Model.Rarity.ToText();
            gift.Model.Rarity.ToColor(ModelRarity);
            BackdropText.Text = gift.Backdrop.Name;
            BackdropRarity.Glyph = gift.Backdrop.Rarity.ToText();
            gift.Backdrop.Rarity.ToColor(BackdropRarity);
            SymbolText.Text = gift.Symbol.Name;
            SymbolRarity.Glyph = gift.Symbol.Rarity.ToText();
            gift.Symbol.Rarity.ToColor(SymbolRarity);

            if (gift.ValueAmount != 0)
            {
                ValueRow.Content = "~" + Locale.FormatCurrency(gift.ValueAmount, gift.ValueCurrency);
                ValueRow.Visibility = Visibility.Visible;
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
        /// False, after saying when it can, while the gift can't be transferred yet.
        /// </summary>
        public static bool CheckTransferDate(INavigationService navigationService, ReceivedGift gift)
        {
            var now = DateTime.Now.ToUnixTimeSeconds();
            if (now >= gift.NextTransferDate)
            {
                return true;
            }

            var diff = Formatter.ToLocalTime(gift.NextTransferDate) - DateTime.Now;

            var message = diff.TotalDays >= 1
                ? string.Format(Strings.Gift2TransferTimeoutDate, Formatter.DateAt(gift.NextTransferDate))
                : string.Format(Strings.Gift2TransferTimeout, Locale.FormatTtl(gift.NextTransferDate - now));

            navigationService.ShowPopup(message, Strings.Gift2TransferTimeoutTitle, Strings.OK);
            return false;
        }

        /// <summary>
        /// False, after saying why, when the gift can't be transferred yet or the chat takes no
        /// upgraded gifts; otherwise asks to confirm the transfer.
        /// </summary>
        public static async Task<bool> ConfirmTransferAsync(IClientService clientService, INavigationService navigationService, ReceivedGift gift, Chat chat)
        {
            if (!CheckTransferDate(navigationService, gift))
            {
                return false;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                if (!clientService.TryGetSupergroupFull(supergroup.SupergroupId, out SupergroupFullInfo fullInfo))
                {
                    var response = await clientService.SendAsync(new GetSupergroupFullInfo(supergroup.SupergroupId));
                    if (response is Error error)
                    {
                        navigationService.ShowToast(error);
                        return false;
                    }

                    fullInfo = response as SupergroupFullInfo;
                }

                if (fullInfo?.CanSendGift is false)
                {
                    await navigationService.ShowPopupAsync(Strings.Gift2ChannelDoesntSupportGiftsText, Strings.Gift2ChannelDoesntSupportGiftsTitle, Strings.OK);
                    return false;
                }
            }
            else if (chat.Type is ChatTypePrivate privata)
            {
                if (!clientService.TryGetUserFull(privata.UserId, out UserFullInfo fullInfo))
                {
                    var response = await clientService.SendAsync(new GetUserFullInfo(privata.UserId));
                    if (response is Error error)
                    {
                        navigationService.ShowToast(error);
                        return false;
                    }

                    fullInfo = response as UserFullInfo;
                }

                if (fullInfo?.GiftSettings?.AcceptedGiftTypes?.UpgradedGifts is false)
                {
                    navigationService.ShowToast(string.Format(Strings.UserDisallowedGifts, clientService.GetTitle(chat, true)), ToastPopupIcon.Error);
                    return false;
                }
            }

            var confirm = await navigationService.ShowPopupAsync(new TransferGiftPopup(clientService, gift, chat));
            return confirm == ContentDialogResult.Primary;
        }

        /// <summary>
        /// Transfers a gift the user already confirmed, paying its transfer price when it has one.
        /// </summary>
        public static async Task TransferAsync(IClientService clientService, INavigationService navigationService, ReceivedGift gift, Chat chat)
        {
            if (gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var function = new TransferGift(gift.ReceivedGiftId, chat.ToMessageSender(), gift.TransferStarCount);
            var response = gift.TransferStarCount > 0
                ? await clientService.SendPaymentAsync(gift.TransferStarCount, function)
                : await clientService.SendAsync(function);

            if (response is Ok)
            {
                clientService.Session.Resolve<IEventAggregator>().Publish(new UpdateGiftIsSold(gift.ReceivedGiftId));

                // From one user to another the gift arrives as a message, so the chat is where it can be seen.
                if (chat.Type is ChatTypePrivate && upgraded.Gift.OwnerId is MessageSenderUser)
                {
                    navigationService.NavigateToChat(chat);
                }

                var text = string.Format(Strings.Gift2TransferredText, upgraded.Gift.ToName(), clientService.GetTitle(chat, true));
                navigationService.ShowToast(string.Format("**{0}**\n{1}", Strings.Gift2TransferredTitle, text), ToastPopupIcon.Forward);
            }
            else if (response is ErrorStarsNeeded needed)
            {
                await navigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(needed.StarCount, 0));
            }
            else if (response is Error error)
            {
                navigationService.ShowToast(error);
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
