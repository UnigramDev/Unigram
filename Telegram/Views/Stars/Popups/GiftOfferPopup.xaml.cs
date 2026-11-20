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
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.Globalization.NumberFormatting;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Stars.Popups
{
    /// <summary>
    /// An offer to buy someone else's upgraded gift: Stars, or 1/100 of a Gram, held until the owner
    /// answers or the offer expires.
    /// </summary>
    public sealed partial class GiftOfferPopup : ModalPopup
    {
        // The only durations sendGiftPurchaseOffer accepts, in seconds.
        private static readonly int[] _durations = { 21600, 43200, 86400, 129600, 172800, 259200 };

        private readonly IClientService _clientService;
        private readonly INumberFormatter2 _formatter;
        private readonly UpgradedGift _gift;
        private readonly string _ownerName;

        private bool _gram;
        private long _value;

        public GiftOfferPopup(IClientService clientService, UpgradedGift gift, string ownerName)
        {
            InitializeComponent();

            _clientService = clientService;
            _formatter = GiftResalePricePopup.CreateFormatter();
            _gift = gift;
            _ownerName = ownerName;

            Title = Strings.GiftOfferToBuyTitle;
            SecondaryButtonText = Strings.Cancel;

            var scope = new InputScope();
            var name = new InputScopeName();

            name.NameValue = InputScopeNameValue.Number;

            scope.Names.Add(name);
            Label.InputScope = scope;
            Label.Padding = new Thickness(36, Label.Padding.Top, Label.Padding.Right, Label.Padding.Bottom);

            foreach (var duration in _durations)
            {
                OfferDuration.Items.Add(Locale.Declension(Strings.R.GiftOfferHours, duration / 3600));
            }

            OfferDuration.SelectedIndex = 2;

            SetCurrency(false, false);
            Currency.SelectedIndex = 0;

            Label.Focus(FocusState.Keyboard);
        }

        public bool IsGram => _gram;

        public long Value => _value;

        public GiftResalePrice Price => _gram
            ? new GiftResalePriceGram(_value)
            : new GiftResalePriceStar(_value);

        public int Duration => _durations[OfferDuration.SelectedIndex];

        private long Minimum => _gram
            ? Math.Max(_clientService.Options.GiftResaleGramCentCountMin, 1)
            : _clientService.Options.GiftResaleStarCountMin;

        private long Maximum => _gram
            ? _clientService.Options.GiftResaleGramCentCountMax
            : _clientService.Options.GiftResaleStarCountMax;

        private void SetCurrency(bool gram, bool animated)
        {
            _gram = gram;

            HeaderText.Text = gram ? Strings.GiftOfferTONToOffer : Strings.GiftOfferStarsToOffer;
            TextBlockHelper.SetMarkdown(DurationInfo, string.Format(gram ? Strings.GiftOfferDurationInfoTON : Strings.GiftOfferDurationInfoStars, _ownerName));

            if (animated)
            {
                GiftResalePricePopup.AnimateIcon(StarCount, !gram);
                GiftResalePricePopup.AnimateIcon(TonCount, gram);
            }
            else
            {
                ElementComposition.GetElementVisual(StarCount).Opacity = gram ? 0 : 1;
                ElementComposition.GetElementVisual(TonCount).Opacity = gram ? 1 : 0;
            }

            // The maximum's digits, plus a separator and two decimals for Grams.
            Label.MaxLength = gram
                ? _formatter.FormatInt(Maximum / 100).Length + 3
                : _formatter.FormatInt(Maximum).Length;

            Label.Text = string.Empty;

            UpdateValue(0);
        }

        private void UpdateValue(long value)
        {
            _value = value;

            var tooBig = value > Maximum;
            var tooSmall = value > 0 && value < Minimum;

            var gift = _gift.ToName();

            if (tooBig || tooSmall)
            {
                var limit = FormatAmount(tooBig ? Maximum : Minimum);
                var text = _gram
                    ? tooBig ? Strings.GiftOfferTONToOfferInfoIsHigh : Strings.GiftOfferTONToOfferInfoIsLow
                    : tooBig ? Strings.GiftOfferStarsToOfferInfoIsHigh : Strings.GiftOfferStarsToOfferInfoIsLow;

                TextBlockHelper.SetMarkdown(ErrorText, string.Format(text, limit, gift));
                ErrorText.Visibility = Visibility.Visible;
                FooterText.Visibility = Visibility.Collapsed;
            }
            else
            {
                TextBlockHelper.SetMarkdown(FooterText, string.Format(_gram ? Strings.GiftOfferTONToOfferInfo : Strings.GiftOfferStarsToOfferInfo, gift));
                FooterText.Visibility = Visibility.Visible;
                ErrorText.Visibility = Visibility.Collapsed;
            }

            IsPrimaryButtonEnabled = value > 0 && !tooBig && !tooSmall;
            PrimaryButtonText = string.Format(Strings.GiftOfferButtonStars, FormatAmount(value)).ReplaceStar(_gram ? Icons.Gram14 : Icons.Premium);

            var usd = _gram
                ? value / 1000000d * _clientService.Options.MillionGramToUsdRate
                : value / 1000d * _clientService.Options.ThousandStarToUsdRate;

            UsdEstimate.Text = "~" + Formatter.FormatAmount((long)usd, "USD");
        }

        private string FormatAmount(long value)
        {
            return _gram ? TransferGiftPopup.FormatGram(value) : value.ToString("N0");
        }

        private void Label_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (args.NewText.Length == 0)
            {
                UpdateValue(0);
            }
            else if (GiftResalePricePopup.Parse(_formatter, args.NewText, _gram) is long value)
            {
                UpdateValue(value);
            }
            else
            {
                args.Cancel = true;
            }
        }

        private void Label_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && IsPrimaryButtonEnabled)
            {
                e.Handled = true;
                Hide(ContentDialogResult.Primary);
            }
        }

        private void Currency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var gram = Currency.SelectedIndex == 1;
            if (gram != _gram)
            {
                SetCurrency(gram, true);
                Label.Focus(FocusState.Keyboard);
            }
        }

        /// <summary>
        /// Asks for the offer, confirms it with the owner's message price added, and sends it.
        /// Returns null when the user gave up, otherwise TDLib's answer or <see cref="ErrorStarsNeeded"/>.
        /// </summary>
        public static async Task<Object> OfferAsync(IClientService clientService, INavigationService navigationService, UpgradedGift gift, string ownerName)
        {
            var popup = new GiftOfferPopup(clientService, gift, ownerName);

            var confirm = await navigationService.ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            // An offer reaches a user as a message, so it costs what their messages cost.
            long fee = 0;

            if (gift.OwnerId is MessageSenderUser user)
            {
                if (clientService.TryGetUserFull(user.UserId, out UserFullInfo cached))
                {
                    fee = cached.OutgoingPaidMessageStarCount;
                }
                else if (await clientService.SendAsync(new GetUserFullInfo(user.UserId)) is UserFullInfo fullInfo)
                {
                    fee = fullInfo.OutgoingPaidMessageStarCount;
                }
            }

            var summary = new GiftOfferConfirmPopup(gift, ownerName, popup.IsGram, popup.Value, fee, popup.Duration);

            confirm = await navigationService.ShowPopupAsync(summary);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            var function = new SendGiftPurchaseOffer(gift.OwnerId, gift.Name, popup.Price, popup.Duration, fee);

            if (popup.IsGram)
            {
                if (!await TransferGiftPopup.HasGramsAsync(clientService, navigationService, popup.Value))
                {
                    return null;
                }

                return await clientService.SendPaymentAsync(fee, function);
            }

            return await clientService.SendPaymentAsync(popup.Value + fee, function);
        }
    }
}
