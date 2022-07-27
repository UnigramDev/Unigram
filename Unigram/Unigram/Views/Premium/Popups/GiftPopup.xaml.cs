using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Premium.Popups
{
    public sealed partial class GiftPopup : ContentPopup
    {
        private readonly IProtoService _protoService;
        private readonly INavigationService _navigationService;

        private PremiumGiftOption _selectedOption;

        public GiftPopup(IProtoService protoService, INavigationService navigationService, User user, IList<PremiumGiftOption> options)
        {
            InitializeComponent();

            _protoService = protoService;
            _navigationService = navigationService;

            _selectedOption = options.FirstOrDefault();

            Title = Strings.Resources.GiftTelegramPremiumTitle;
            TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.GiftTelegramPremiumDescription, user.FirstName));

            ScrollingHost.ItemsSource = options;

            var footer = Strings.Resources.GiftPremiumListFeaturesAndTerms;
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

            var option = args.Item as PremiumGiftOption;
            var content = args.ItemContainer.ContentTemplateRoot as RadioButton;

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var price = content.FindName("Price") as TextBlock;
            var icon = content.FindName("Icon") as TextBlock;
            var iconPanel = content.FindName("IconPanel") as Border;

            var monthlyAmount = option.Amount / option.MonthCount;

            title.Text = Locale.Declension("Months", option.MonthCount);
            subtitle.Text = string.Format(Strings.Resources.PricePerMonth, Locale.FormatCurrency(monthlyAmount, option.Currency));
            price.Text = Locale.FormatCurrency(option.Amount, option.Currency);

            if (option.DiscountPercentage > 0)
            {
                icon.Text = string.Format(Strings.Resources.GiftPremiumOptionDiscount, option.DiscountPercentage);
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
            if (sender is FrameworkElement element && element.Tag is PremiumGiftOption option)
            {
                _selectedOption = option;
                PurchaseCommand.Content = string.Format(Strings.Resources.GiftSubscriptionFor, Locale.FormatCurrency(option.Amount, option.Currency));
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
                MessageHelper.OpenTelegramUrl(_protoService, _navigationService, _selectedOption?.PaymentLink);
            }
        }
    }
}
