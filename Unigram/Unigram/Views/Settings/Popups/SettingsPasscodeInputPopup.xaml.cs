//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings.Popups
{
    public sealed partial class SettingsPasscodeInputPopup : ContentPopup
    {
        public SettingsPasscodeInputPopup()
        {
            InitializeComponent();

            Title = Strings.Resources.Passcode;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public bool IsSimple
        {
            get => Type.SelectedIndex == 0;
            set => Type.SelectedIndex = value ? 0 : 1;
        }

        public string Passcode { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (IsSimple && (First.Password.Length != 4 || !First.Password.All(x => x is >= '0' and <= '9')))
            {
                VisualUtilities.ShakeView(First);
                args.Cancel = true;
                return;
            }

            if (!First.Password.Equals(Confirm.Password))
            {
                VisualUtilities.ShakeView(Confirm);
                args.Cancel = true;
                return;
            }

            Passcode = First.Password.ToString();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void First_Changed(object sender, RoutedEventArgs e)
        {
            if (IsSimple && First.Password.Length == 4 && First.Password.All(x => x is >= '0' and <= '9'))
            {
                Confirm.Focus(FocusState.Keyboard);
            }
        }

        private void Confirm_Changed(object sender, RoutedEventArgs e)
        {
            if (IsSimple && First.Password.Length == 4 && First.Password.All(x => x is >= '0' and <= '9') && First.Password.Equals(Confirm.Password))
            {
                Hide(ContentDialogResult.Primary);
            }
        }

        private void Password_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key is >= Windows.System.VirtualKey.Number0 and <= Windows.System.VirtualKey.Number9) { }
            else if (e.Key is >= Windows.System.VirtualKey.NumberPad0 and <= Windows.System.VirtualKey.NumberPad9) { }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (sender == First)
                {
                    Confirm.Focus(FocusState.Keyboard);
                }
                else
                {
                    Hide(ContentDialogResult.Primary);
                }

                e.Handled = true;
            }
            else
            {
                e.Handled = IsSimple;
            }
        }

        private void Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var scope = InputScopeNameValue.NumericPin;
            var maxLength = 4;

            switch (Type.SelectedIndex)
            {
                case 1:
                    scope = InputScopeNameValue.Password;
                    maxLength = int.MaxValue;
                    break;
            }

            First.Password = Confirm.Password = string.Empty;
            First.MaxLength = Confirm.MaxLength = maxLength;

            var firstScope = new InputScope();
            firstScope.Names.Add(new InputScopeName(scope));
            First.InputScope = firstScope;

            var confirmScope = new InputScope();
            confirmScope.Names.Add(new InputScopeName(scope));
            Confirm.InputScope = confirmScope;

            First.Focus(FocusState.Keyboard);
        }
    }
}
