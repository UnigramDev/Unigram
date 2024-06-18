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

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StarTransaction transaction)
            {
                Hide();
                await ViewModel.ShowPopupAsync(new ReceiptPopup(ViewModel.ClientService, ViewModel.NavigationService, transaction));
                await this.ShowQueuedAsync();
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
            var date = content.FindName("Date") as TextBlock;
            var starCount = content.FindName("StarCount") as TextBlock;

            if (transaction.Partner is StarTransactionPartnerTelegram)
            {
                photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                title.Text = Strings.StarsTransactionBot;
                subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerFragment)
            {
                photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                title.Text = Strings.StarsTransactionFragment;
                subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerAppStore or StarTransactionPartnerGooglePlay)
            {
                photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                title.Text = Strings.StarsTransactionInApp;
                subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerUser sourceUser && ViewModel.ClientService.TryGetUser(sourceUser.UserId, out User user))
            {
                if (sourceUser.ProductInfo != null)
                {
                    title.Text = sourceUser.ProductInfo.Title;
                    subtitle.Text = user.FullName();
                }
                else
                {
                    title.Text = user.FullName();
                    subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }

                photo.SetUser(ViewModel.ClientService, user, 36);
            }
            else
            {
                photo.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
                title.Text = Strings.StarsTransactionUnsupported;
                subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            date.Text = Formatter.DateAt(transaction.Date);

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
