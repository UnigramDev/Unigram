//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class CreateLinkPopup : ModalPopup
    {
        public CreateLinkPopup()
        {
            InitializeComponent();

            Title = Strings.CreateLink;
            PrimaryButtonContent = Strings.OK;
            SecondaryButtonContent = Strings.Cancel;
        }

        public string Text
        {
            get => TextField.Text;
            set => TextField.Text = value;
        }

        public string Link
        {
            get => LinkField.Text;
            set => LinkField.Text = value;
        }

        public bool IsValid { get; set; }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            if (!Validate())
            {
                args.Cancel = true;
            }
        }

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                VisualUtilities.ShakeView(TextField);
                return false;
            }

            if (IsUrlInvalid(Link))
            {
                VisualUtilities.ShakeView(LinkField);
                return false;
            }

            IsValid = true;
            return true;
        }

        private bool IsUrlInvalid(string url)
        {
            return !url.IsValidUrl();
        }

        private void TextField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                LinkField.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        }

        private void LinkField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (Validate())
                {
                    Hide(ContentDialogResult.Primary);
                }

                e.Handled = true;
            }
        }

        private void TextField_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextField.Text))
            {
                TextField.Focus(FocusState.Keyboard);
            }
            else
            {
                LinkField.Focus(FocusState.Keyboard);
            }
        }

        public async Task<bool> ShowQueuedAsync(XamlRoot xamlRoot)
        {
            await ShowAsync(xamlRoot);
            return IsValid;
        }
    }
}
