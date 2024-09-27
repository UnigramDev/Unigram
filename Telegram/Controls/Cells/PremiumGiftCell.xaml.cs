using Telegram.Common;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class PremiumGiftCell : UserControl
    {
        public PremiumGiftCell()
        {
            InitializeComponent();
        }

        public void UpdatePremiumGift(IClientService clientService, PremiumPaymentOption option)
        {
            Animated.Source = new PremiumInfoFileSource(clientService, option.MonthCount);

            Title.Text = Locale.Declension(Strings.R.GiftMonths, option.MonthCount);
            Subtitle.Text = Strings.TelegramPremiumShort;
            Price.Text = Locale.FormatCurrency(option.Amount, option.Currency);

            if (option.DiscountPercentage > 0)
            {
                RibbonRoot.Visibility = Visibility.Visible;
                Ribbon.Text = string.Format(Strings.GiftPremiumOptionDiscount, option.DiscountPercentage);
            }
            else
            {
                RibbonRoot.Visibility = Visibility.Collapsed;
            }
        }
    }
}
