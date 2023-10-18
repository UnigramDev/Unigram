//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Converters;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Settings.Popups
{
    public sealed partial class SettingsSessionPopup : ContentPopup
    {
        public SettingsSessionPopup(Session session)
        {
            InitializeComponent();

            var icon = SessionCell.IconForSession(session);

            IconBackground.Background = new SolidColorBrush(icon.Background);

            if (icon.Animation != null)
            {
                Icon.Source = new LocalFileSource($"ms-appx:///Assets/Animations/Device{icon.Animation}.json")
                {
                    ColorReplacements = new Dictionary<int, int> { { 0x000000, icon.Background.ToValue() } }
                };
            }
            else
            {

            }

            Title.Text = session.DeviceModel;
            Subtitle.Text = Formatter.DateExtended(session.LastActiveDate);

            Application.Badge = string.Format("{0} {1}", session.ApplicationName, session.ApplicationVersion);
            Location.Badge = session.Location;
            Address.Badge = session.IpAddress;

            AcceptCalls.IsChecked = session.CanAcceptCalls;

            AcceptSecretChats.IsChecked = session.CanAcceptSecretChats;
            AcceptSecretChatsPanel.Visibility = session.ApiId == 2040 || session.ApiId == 2496
                ? Visibility.Collapsed
                : Visibility.Visible;

            PrimaryButtonText = Strings.Terminate;
            SecondaryButtonText = Strings.Done;
        }

        public bool CanAcceptCalls => AcceptCalls.IsChecked == true;

        public bool CanAcceptSecretChats => AcceptSecretChats.IsChecked == true;

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            await Task.Delay(1000);
            Icon.Play();
        }

        private void AcceptCallsPanel_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            AcceptCalls.IsChecked = AcceptCalls.IsChecked != true;
        }

        private void AcceptSecretChatsPanel_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            AcceptSecretChats.IsChecked = AcceptSecretChats.IsChecked != true;
        }
    }
}
