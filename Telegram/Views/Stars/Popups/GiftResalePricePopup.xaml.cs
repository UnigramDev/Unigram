//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Globalization.NumberFormatting;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Stars.Popups
{
    /// <summary>
    /// The price a gift is put for sale at: Stars, or 1/100 of a Gram when only TON is accepted.
    /// </summary>
    public sealed partial class GiftResalePricePopup : ModalPopup
    {
        private readonly IClientService _clientService;
        private readonly INumberFormatter2 _formatter;

        private bool _gram;
        private long _value;

        public GiftResalePricePopup(IClientService clientService, GiftResalePrice price)
        {
            InitializeComponent();

            _clientService = clientService;
            _formatter = CreateFormatter();

            PrimaryButtonText = Strings.ResellGiftButton;
            SecondaryButtonText = Strings.Cancel;

            var scope = new InputScope();
            var name = new InputScopeName();

            name.NameValue = InputScopeNameValue.Number;

            scope.Names.Add(name);
            Label.InputScope = scope;
            Label.Padding = new Thickness(36, Label.Padding.Top, Label.Padding.Right, Label.Padding.Bottom);

            switch (price)
            {
                case GiftResalePriceGram gram:
                    SetCurrency(true, gram.GramCentCount, false);
                    break;
                case GiftResalePriceStar star:
                    SetCurrency(false, star.StarCount, false);
                    break;
                default:
                    SetCurrency(false, 0, false);
                    break;
            }

            Label.Focus(FocusState.Keyboard);
        }

        public GiftResalePrice Price => _gram
            ? new GiftResalePriceGram(_value)
            : new GiftResalePriceStar(_value);

        /// <summary>
        /// TDLib reports a listing in both currencies. Until the gift is loaded again, the one that
        /// was not entered is converted at the current rates, or 0 when a rate is missing.
        /// </summary>
        public GiftResaleParameters Parameters
        {
            get
            {
                // USD cents per 1000 Stars, and per million 1/100 of a Gram.
                double stars = _clientService.Options.ThousandStarToUsdRate;
                double grams = _clientService.Options.MillionGramToUsdRate;

                if (_gram)
                {
                    var starCount = stars > 0 ? (long)Math.Round(_value * grams * 1000 / 1000000 / stars) : 0;
                    return new GiftResaleParameters(starCount, _value, true);
                }

                var gramCentCount = grams > 0 ? (long)Math.Round(_value * stars / 1000 * 1000000 / grams) : 0;
                return new GiftResaleParameters(_value, gramCentCount, false);
            }
        }

        // TDLib takes any positive Gram amount, but a price under a cent cannot be typed.
        private long Minimum => _gram
            ? Math.Max(_clientService.Options.GiftResaleGramCentCountMin, 1)
            : _clientService.Options.GiftResaleStarCountMin;

        private long Maximum => _gram
            ? _clientService.Options.GiftResaleGramCentCountMax
            : _clientService.Options.GiftResaleStarCountMax;

        private void SetCurrency(bool gram, long value, bool animated)
        {
            _gram = gram;

            Title = gram ? Strings.ResellGiftTitleTON : Strings.ResellGiftTitle;
            GramOnly.IsChecked = gram;

            if (animated)
            {
                AnimateIcon(StarCount, !gram);
                AnimateIcon(TonCount, gram);
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

            Label.Text = value > 0
                ? gram ? _formatter.FormatDouble(value / 100d) : _formatter.FormatInt(value)
                : string.Empty;
            Label.SelectionStart = Label.Text.Length;

            UpdateValue(value);
        }

        internal static void AnimateIcon(UIElement element, bool show)
        {
            var visual = ElementComposition.GetElementVisual(element);
            visual.CenterPoint = new Vector3(10, 10, 0);

            var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, show ? Vector3.Zero : Vector3.One);
            scale.InsertKeyFrame(1, show ? Vector3.One : Vector3.Zero);
            scale.Duration = Constants.FastAnimation;

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);
            opacity.Duration = Constants.FastAnimation;

            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Opacity", opacity);
        }

        private void UpdateValue(long value)
        {
            _value = value;

            var tooBig = value > Maximum;
            var tooSmall = value > 0 && value < Minimum;

            if (tooBig || tooSmall)
            {
                ErrorText.Text = string.Format(tooBig ? Strings.ResellGiftPriceTooMuch : Strings.ResellGiftPriceTooSmall, FormatAmount(tooBig ? Maximum : Minimum));
                ErrorText.Visibility = Visibility.Visible;
                HeaderText.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeaderText.Text = _gram ? Strings.ResellGiftPriceTitleTON : Strings.ResellGiftPriceTitle;
                HeaderText.Visibility = Visibility.Visible;
                ErrorText.Visibility = Visibility.Collapsed;
            }

            IsPrimaryButtonEnabled = value > 0 && !tooBig && !tooSmall;

            double usd;

            if (_gram)
            {
                var earnings = value * _clientService.Options.GiftResaleGramEarningsPerMille / 1000;
                TextBlockHelper.SetMarkdown(FooterText, string.Format(Strings.ResellGiftInfoTON, FormatGram(earnings)));

                usd = value / 1000000d * _clientService.Options.MillionGramToUsdRate;
            }
            else
            {
                var earnings = value * _clientService.Options.GiftResaleStarEarningsPerMille / 1000;
                TextBlockHelper.SetMarkdown(FooterText, Locale.Declension(Strings.R.ResellGiftInfo, earnings));

                usd = value / 1000d * _clientService.Options.ThousandStarToUsdRate;
            }

            UsdEstimate.Text = "~" + Formatter.FormatAmount((long)usd, "USD");
        }

        private string FormatAmount(long value)
        {
            return _gram
                ? string.Format(Strings.TonCountX, FormatGram(value))
                : Locale.Declension(Strings.R.StarsCount, value);
        }

        private static string FormatGram(long cents)
        {
            return (cents / 100d).ToString("0.##");
        }

        /// <summary>
        /// Stars, or 1/100 of a Gram when <paramref name="gram"/>; null for text that is neither.
        /// </summary>
        internal static long? Parse(INumberFormatter2 formatter, string text, bool gram)
        {
            if (formatter is not INumberParser parser)
            {
                return null;
            }

            if (!gram)
            {
                return parser.ParseInt(text) is long stars && stars >= 0 ? stars : null;
            }

            // A separator just typed is not a number yet, but the next digit will make one.
            var amount = parser.ParseDouble(text);
            if (amount == null && text.Length > 1 && text[text.Length - 1] is '.' or ',')
            {
                amount = parser.ParseDouble(text.Substring(0, text.Length - 1));
            }

            if (amount is not double grams || grams < 0)
            {
                return null;
            }

            // Rounded first, because 1.15 * 100 is 114.99999999999999.
            var cents = Math.Round(grams * 100, 6);
            return cents == Math.Floor(cents) ? (long)cents : null;
        }

        private void Label_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (args.NewText.Length == 0)
            {
                UpdateValue(0);
            }
            else if (Parse(_formatter, args.NewText, _gram) is long value)
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

        private void GramOnly_Click(object sender, RoutedEventArgs e)
        {
            SetCurrency(GramOnly.IsChecked is true, 0, true);
            Label.Focus(FocusState.Keyboard);
        }

        // This was largely copied from Calculator's GetRegionalSettingsAwareDecimalFormatter()
        internal static DecimalFormatter CreateFormatter()
        {
            DecimalFormatter formatter = null;

            var currentLocale = Locale.GetUserDefaultLocaleName();
            if (currentLocale != null)
            {
                // GetUserDefaultLocaleName may return an invalid bcp47 language tag with trailing non-BCP47 friendly characters,
                // which if present would start with an underscore, for example sort order.
                var underscore = currentLocale.IndexOf('_');
                if (underscore != -1)
                {
                    currentLocale = currentLocale.Substring(0, underscore);
                }

                if (Windows.Globalization.Language.IsWellFormed(currentLocale))
                {
                    formatter = new DecimalFormatter(new[] { currentLocale }, GlobalizationPreferences.HomeGeographicRegion);
                }
            }

            formatter ??= new DecimalFormatter();
            formatter.IntegerDigits = 1;
            formatter.FractionDigits = 0;

            return formatter;
        }
    }
}
