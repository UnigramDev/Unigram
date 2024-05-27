//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class FactCheckPopup : ContentPopup
    {
        public FormattedText Text { get; set; }

        public FactCheckPopup(FormattedText text, long maxLength)
        {
            InitializeComponent();

            Title = Strings.FactCheckDialog;
            SecondaryButtonText = Strings.Cancel;

            Label.PlaceholderText = Strings.FactCheckPlaceholder;
            Label.MaxLength = (int)maxLength;
            Label.SetText(text);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Text = Label.GetFormattedText();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Label_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
            {
                return;
            }

            Hide(ContentDialogResult.Primary);
        }

        private bool _empty = true;

        private void Label_TextChanged(object sender, RoutedEventArgs e)
        {
            if (Label.IsEmpty && !_empty)
            {
                PrimaryButtonText = string.Empty;
                Remove.Visibility = Visibility.Visible;
            }
            else if (_empty && !Label.IsEmpty)
            {
                PrimaryButtonText = Strings.Done;
                Remove.Visibility = Visibility.Collapsed;
            }

            _empty = Label.IsEmpty;
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }
    }
}
