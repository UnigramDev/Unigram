//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseSoundPopup : ContentPopup
    {
        public ChooseSoundViewModel ViewModel => DataContext as ChooseSoundViewModel;

        public ChooseSoundPopup(TaskCompletionSource<object> completion)
        {
            InitializeComponent();

            _completion = completion;

            Title = Strings.Sound;

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        private readonly TaskCompletionSource<object> _completion;

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (SoundDefault.IsChecked == true)
            {
                _completion.TrySetResult(new NotificationSound { Id = -1 });
            }
            else if (NoSound.IsChecked == true)
            {
                _completion.TrySetResult(new NotificationSound { Id = 0 });
            }
            else
            {
                var selected = ViewModel.Items.FirstOrDefault(x => x.IsSelected);
                if (selected == null)
                {
                    args.Cancel = true;
                    return;
                }

                _completion.TrySetResult(selected.Get());
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
