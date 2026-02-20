//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Converters;
using Telegram.Td;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Settings.Privacy;
using Windows.UI.Xaml;

namespace Telegram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyNewChatPage : HostedPage
    {
        public SettingsPrivacyNewChatViewModel ViewModel => DataContext as SettingsPrivacyNewChatViewModel;

        public SettingsPrivacyNewChatPage()
        {
            InitializeComponent();
            Title = Strings.PrivacyMessages;

            SliderHelper.InitializeTicks(Price, PriceTicks, 2, ConvertPriceTicks);
        }

        private string ConvertFooter(PrivacyValue value)
        {
            if (value == PrivacyValue.DisallowAll)
            {
                return Strings.PrivateMessagesChargePriceInfo;
            }

            var formatted = Extensions.ReplacePremiumLink(Strings.PrivacyMessagesInfo, null);
            var markdown = ClientEx.GetMarkdownText(formatted);

            return markdown.Text;
        }

        private string ConvertPriceValue(int value)
        {
            return Locale.Declension(Strings.R.StarsCount, value);
        }

        private string ConvertPriceTicks(int value)
        {
            return (value == 0 ? 1 : 10000).ToString("N0");
        }

        private string ConvertPriceFee(int value)
        {
            var xtr = value / 1000d;
            var usd = xtr * ViewModel.ClientService.Options.ThousandStarToUsdRate;

            var format = Formatter.FormatAmount((long)usd, "USD");

            return string.Format(Strings.PrivateMessagesPriceInfo, 85, format);
        }

        private Visibility ConvertFee(PrivacyValue value)
        {
            return value == PrivacyValue.DisallowAll
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
