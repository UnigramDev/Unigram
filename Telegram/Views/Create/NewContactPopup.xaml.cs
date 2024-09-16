//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.ViewModels.Create;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Views.Create
{
    public sealed partial class NewContactPopup : ContentPopup
    {
        public NewContactViewModel ViewModel => DataContext as NewContactViewModel;

        public NewContactPopup()
        {
            InitializeComponent();

            Title = Strings.NewContactTitle;

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        private void FirstName_Loaded(object sender, RoutedEventArgs e)
        {
            FirstName.Focus(FocusState.Keyboard);
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.Create();
        }
    }
}
