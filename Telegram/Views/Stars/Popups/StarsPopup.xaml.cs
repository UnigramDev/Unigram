//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Stars;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class StarsPopup : ContentPopup
    {
        public StarsViewModel ViewModel => DataContext as StarsViewModel;

        public StarsPopup()
        {
            InitializeComponent();
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StarPaymentOption option)
            {
                Hide();
                ViewModel.NavigationService.NavigateToInvoice(new InputInvoiceTelegram(new TelegramPaymentPurposeStars(option.Currency, option.Amount, option.StarCount)));
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var transaction = args.Item as StarTransaction;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var photo = content.FindName("Photo") as ProfilePicture;
            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var starCount = content.FindName("StarCount") as TextBlock;

            if (transaction.Source is StarTransactionSourceTelegram)
            {
                photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                title.Text = Strings.StarsTransactionBot;
            }
            else if (transaction.Source is StarTransactionSourceFragment)
            {
                photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                title.Text = Strings.StarsTransactionFragment;
            }
            else if (transaction.Source is StarTransactionSourceAppStore or StarTransactionSourceGooglePlay)
            {
                photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                title.Text = Strings.StarsTransactionInApp;
            }
            else if (transaction.Source is StarTransactionSourceUser sourceUser && ViewModel.ClientService.TryGetUser(sourceUser.UserId, out User user))
            {
                photo.SetUser(ViewModel.ClientService, user, 36);
                title.Text = user.FullName();
            }
            else
            {
                photo.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
                title.Text = Strings.StarsTransactionUnsupported;
            }

            subtitle.Text = Formatter.DateAt(transaction.Date);

            starCount.Text = (transaction.StarCount < 0 ? string.Empty : "+") + transaction.StarCount.ToString("N0");
            starCount.Foreground = BootStrapper.Current.Resources[transaction.StarCount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;

            args.Handled = true;
        }

        public string ConvertCount(long count)
        {
            return count.ToString("N0");
        }

        private void Buy_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Hide();
            ViewModel.NavigationService.ShowPopupAsync(typeof(BuyPopup));
        }
    }
}
