//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using Telegram.Controls;
using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChatNotificationsPopup : ContentPopup
    {
        public ChatNotificationsViewModel ViewModel => DataContext as ChatNotificationsViewModel;

        public ChatNotificationsPopup()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (SoundDefault.IsChecked == true)
            {
                ViewModel.Confirm(-1);
            }
            else if (NoSound.IsChecked == true)
            {
                ViewModel.Confirm(0);
            }
            else
            {
                var selected = ViewModel.Items.FirstOrDefault(x => x.IsSelected);
                if (selected == null)
                {
                    args.Cancel = true;
                    return;
                }

                ViewModel.Confirm(selected.Id);
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
