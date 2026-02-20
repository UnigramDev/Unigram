//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Converters;
using Telegram.ViewModels.Supergroups;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupDirectMessagesPage : HostedPage
    {
        public SupergroupDirectMessagesViewModel ViewModel => DataContext as SupergroupDirectMessagesViewModel;

        public SupergroupDirectMessagesPage()
        {
            InitializeComponent();
            Title = Strings.PostSuggestions;

            SliderHelper.InitializeTicks(Price, PriceTicks, 2, ConvertPriceTicks);
        }

        private string ConvertPriceValue(int value)
        {
            return Locale.Declension(Strings.R.StarsCount, value);
        }

        private string ConvertPriceTicks(int value)
        {
            return (value == 0 ? 0 : 10000).ToString("N0");
        }

        private string ConvertPriceFee(int value)
        {
            var xtr = value / 1000d;
            var usd = xtr * ViewModel.ClientService.Options.ThousandStarToUsdRate;

            var format = Formatter.FormatAmount((long)usd, "USD");

            return string.Format(Strings.PostSuggestionsPriceInfo, 85, format);
        }
    }
}
