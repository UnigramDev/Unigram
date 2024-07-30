//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Td.Api;
using Telegram.ViewModels.Stars;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Stars.Popups
{
    public class SubtractedStar : Control
    {

    }

    public class BuyStarsArgs
    {
        public long StarCount { get; set; }

        public long SellerBotUserId { get; set; }

        public long ReceiverUserId { get; set; }

        public BuyStarsArgs(long starCount, long sellerBotUserId)
        {
            StarCount = starCount;
            SellerBotUserId = sellerBotUserId;
        }

        public BuyStarsArgs(long receiverUserId)
        {
            ReceiverUserId = receiverUserId;
        }
    }

    public sealed partial class BuyPopup : ContentPopup
    {
        public BuyViewModel ViewModel => DataContext as BuyViewModel;

        public BuyPopup()
        {
            InitializeComponent();
        }

        public override void OnNavigatedTo()
        {
            if (ViewModel.Arguments is BuyStarsArgs args)
            {
                if (ViewModel.ClientService.TryGetUser(args.SellerBotUserId, out User sellerUser))
                {
                    TitleLabel.Text = Locale.Declension(Strings.R.StarsNeededTitle, args.StarCount);
                    TextBlockHelper.SetMarkdown(SubtitleLabel, string.Format(Strings.StarsNeededText, sellerUser.FullName()));
                }
                else if (ViewModel.ClientService.TryGetUser(args.ReceiverUserId, out User receiverUser))
                {
                    TitleLabel.Text = Strings.GiftStarsTitle;
                    TextBlockHelper.SetMarkdown(SubtitleLabel, string.Format(Strings.GiftStarsSubtitle, receiverUser.FirstName));

                    BalancePanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StarPaymentOption option)
            {
                Hide();

                if (ViewModel.Arguments?.ReceiverUserId is long receiverUserId)
                {
                    ViewModel.NavigationService.NavigateToInvoice(new InputInvoiceTelegram(new TelegramPaymentPurposeGiftedStars(receiverUserId, option.Currency, option.Amount, option.StarCount)));
                }
                else
                {
                    ViewModel.NavigationService.NavigateToInvoice(new InputInvoiceTelegram(new TelegramPaymentPurposeStars(option.Currency, option.Amount, option.StarCount)));
                }
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var stars = args.Item as StarPaymentOption;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var subtracted = content.FindName("Subtracted") as StackPanel;

            title.Text = Locale.Declension(Strings.R.StarsCount, stars.StarCount);
            subtitle.Text = Locale.FormatCurrency(stars.Amount, stars.Currency);

            subtracted.Children.Clear();

            var index = ViewModel.IndexOf(stars);

            for (int i = 0; i < index; i++)
            {
                subtracted.Children.Add(new SubtractedStar
                {
                    Template = SubtractedStarTemplate,
                    Margin = new Thickness(i == 0 ? -28 : -26, 0, 0, 0)
                });
            }

            AutomationProperties.SetName(args.ItemContainer, title.Text + ", " + subtitle.Text);

            args.Handled = true;
        }

        public string ConvertCount(long count)
        {
            return count.ToString("N0");
        }

        private void SettingsFooter_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.OpenUrl(null, null, Strings.StarsTOSLink);
        }
    }
}
