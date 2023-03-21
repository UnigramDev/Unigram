//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class GiftPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private PremiumPaymentOption _selectedOption;

        public GiftPopup(IClientService clientService, INavigationService navigationService, User user, IList<PremiumPaymentOption> options)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _selectedOption = options.FirstOrDefault();

            Title = Strings.GiftTelegramPremiumTitle;
            TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.GiftTelegramPremiumDescription, user.FirstName));

            ScrollingHost.ItemsSource = options;

            var footer = Strings.GiftPremiumListFeaturesAndTerms;
            var hereBegin = footer.IndexOf('*');
            var hereEnd = footer.IndexOf('*', hereBegin + 1);

            var hyperlink = new Hyperlink();
            hyperlink.Inlines.Add(new Run { Text = footer.Substring(hereBegin + 1, hereEnd - hereBegin - 1) });

            Footer.Inlines.Add(new Run { Text = footer.Substring(0, hereBegin) });
            Footer.Inlines.Add(hyperlink);
            Footer.Inlines.Add(new Run { Text = footer.Substring(hereEnd + 1) });
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {

        }

        private readonly Color[] _gradient = new Color[]
        {
            Color.FromArgb(0xFF, 0x6F, 0x91, 0xFF),
            Color.FromArgb(0xFF, 0x8B, 0x7C, 0xFF),
            Color.FromArgb(0xFF, 0xA7, 0x67, 0xFF),
        };

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var option = args.Item as PremiumPaymentOption;
            var content = args.ItemContainer.ContentTemplateRoot as RadioButton;

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var price = content.FindName("Price") as TextBlock;
            var icon = content.FindName("Icon") as TextBlock;
            var iconPanel = content.FindName("IconPanel") as Border;

            var monthlyAmount = option.Amount / option.MonthCount;

            title.Text = Locale.Declension("Months", option.MonthCount);
            subtitle.Text = string.Format(Strings.PricePerMonth, Locale.FormatCurrency(monthlyAmount, option.Currency));
            price.Text = Locale.FormatCurrency(option.Amount, option.Currency);

            if (option.DiscountPercentage > 0)
            {
                icon.Text = string.Format(Strings.GiftPremiumOptionDiscount, option.DiscountPercentage);
                iconPanel.Background = new SolidColorBrush(_gradient[args.ItemIndex]);
                iconPanel.Visibility = Visibility.Visible;
            }
            else
            {
                iconPanel.Visibility = Visibility.Collapsed;
            }

            content.Tag = option;
            content.IsChecked = option == _selectedOption;
            args.Handled = true;
        }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is PremiumPaymentOption option)
            {
                _selectedOption = option;
                PurchaseCommand.Content = string.Format(Strings.GiftSubscriptionFor, Locale.FormatCurrency(option.Amount, option.Currency));
            }
        }

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOption?.PaymentLink != null)
            {
                MessageHelper.OpenTelegramUrl(_clientService, _navigationService, _selectedOption?.PaymentLink);
            }
        }
    }
}
