using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Revenue
{
    public sealed partial class StarSubscriptionCell : Grid
    {
        public StarSubscriptionCell()
        {
            InitializeComponent();
        }

        public void UpdateInfo(IClientService clientService, StarSubscription subscription)
        {
            if (subscription == null || !clientService.TryGetChat(subscription.ChatId, out Chat chat))
            {
                return;
            }

            Photo.SetChat(clientService, chat, 36);
            Title.Text = chat.Title;

            if (subscription.IsCanceled)
            {
                Subtitle.Text = string.Format(Strings.StarsSubscriptionExpired, Formatter.Date(subscription.ExpirationDate));
                Date.Text = Strings.StarsSubscriptionStatusCancelled;
                Date.Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;
                Date.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center;

                Stars.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                SetRow(Date, 0);
                SetRowSpan(Date, 2);
            }
            else if (subscription.ExpirationDate < DateTime.Now.ToTimestamp())
            {
                Subtitle.Text = string.Format(Strings.StarsSubscriptionExpired, Formatter.Date(subscription.ExpirationDate));
                Date.Text = Strings.StarsSubscriptionStatusExpired;
                Date.Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;
                Date.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center;

                Stars.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                SetRow(Date, 0);
                SetRowSpan(Date, 2);
            }
            else
            {
                Subtitle.Text = string.Format(Strings.StarsSubscriptionRenews, Formatter.Date(subscription.ExpirationDate));
                StarCount.Text = subscription.Pricing.StarCount.ToString("N0");
                Date.Text = Strings.StarsParticipantSubscriptionPerMonth;
                Date.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                Date.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;

                Stars.Visibility = Windows.UI.Xaml.Visibility.Visible;

                SetRow(Date, 1);
                SetRowSpan(Date, 1);
            }
        }
    }
}
