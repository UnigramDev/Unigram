//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class SubscriptionPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly StarSubscription _subscription;

        private readonly string _transactionId;

        public SubscriptionPopup(IClientService clientService, INavigationService navigationService, StarSubscription subscription)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _subscription = subscription;
            _transactionId = subscription.Id;

            var chat = clientService.GetChat(subscription.ChatId);

            FromPhoto.SetChat(clientService, chat, 24);
            FromPhoto.Visibility = Visibility.Visible;
            FromTitle.Text = chat.Title;
            FromHeader.Text = Strings.StarsSubscriptionChannel;

            Photo.SetChat(clientService, chat, 96);
            Title.Text = Strings.StarsSubscriptionTitle;

            StarCount.Text = string.Format(Strings.PricePerMonthMe, subscription.Pricing.StarCount.ToString("N0"));

            if (subscription.IsCanceled)
            {
                UntilHeader.Text = Strings.StarsSubscriptionUntilExpired;
                UntilText.Text = Formatter.DateAt(subscription.ExpirationDate);

                CancelInfo.Text = Strings.StarsSubscriptionCancelledText;
                CancelInfo.Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;

                PurchaseCommand.Visibility = Visibility.Visible;
                CancelCommand.Visibility = Visibility.Collapsed;
            }
            else if (subscription.ExpirationDate < DateTime.Now.ToTimestamp())
            {
                UntilHeader.Text = Strings.StarsSubscriptionUntilExpired;
                UntilText.Text = Formatter.DateAt(subscription.ExpirationDate);

                CancelInfo.Text = string.Format(Strings.StarsSubscriptionExpiredInfo, Formatter.Date(subscription.ExpirationDate));

                PurchaseCommand.Visibility = Visibility.Visible;
                CancelCommand.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (subscription.IsExpiring)
                {
                    UntilHeader.Text = Strings.StarsSubscriptionUntilExpires;
                    UntilText.Text = Formatter.DateAt(subscription.ExpirationDate);
                }
                else
                {
                    UntilHeader.Text = Strings.StarsSubscriptionUntilRenews;
                    UntilText.Text = Formatter.DateAt(subscription.ExpirationDate);
                }

                CancelInfo.Text = string.Format(Strings.StarsSubscriptionCancelInfo, Formatter.Date(subscription.ExpirationDate));

                PurchaseCommand.Visibility = Visibility.Collapsed;
                CancelCommand.Visibility = Visibility.Visible;
            }
        }

        public SubscriptionPopup(IClientService clientService, INavigationService navigationService, ChatInviteLinkInfo invite)
        {

        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private async void ShareLink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            await _navigationService.ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new HttpUrl("https://")));
        }

        private void SettingsFooter_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.OpenUrl(null, null, Strings.StarsTOSLink);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateThumbnail(file);
        }

        private void UpdateThumbnail(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Photo.Source = UriEx.ToBitmap(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                _clientService.DownloadFile(file.Id, 1);
            }
        }
    }
}
