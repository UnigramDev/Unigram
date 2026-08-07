//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class FormulaPopup : ContentPopup
    {
        public FormulaPopup()
        {
            InitializeComponent();

            IsPrimaryButtonEnabled = false;

            PrimaryButtonText = Strings.Done;
            SecondaryButtonText = Strings.Cancel;
        }

        public FormulaPopup(string formula)
        {
            InitializeComponent();

            Input.Text = formula;

            IsPrimaryButtonEnabled = !string.IsNullOrEmpty(Input.Text);

            try
            {
                Result.Source = Input.Text;
            }
            catch
            {

            }

            PrimaryButtonText = Strings.Done;
            SecondaryButtonText = Strings.Cancel;
        }

        public string Source
        {
            get => Input.Text;
            set => Input.Text = value;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = !string.IsNullOrEmpty(Input.Text);

            try
            {
                Result.Source = Input.Text;
            }
            catch
            {

            }
        }
    }
}
