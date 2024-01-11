//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordCreatePopup : ContentPopup
    {
        public SettingsPasswordCreatePopup()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Continue;
            SecondaryButtonText = Strings.Cancel;

            Animated.Source = new LocalFileSource("ms-appx:///Assets/Animations/AuthorizationStateWaitPassword.tgs")
            {
                Markers = new Dictionary<string, int>
                {
                    { "Close", 40 },
                    { "CloseToPeek", 40 + 16 },
                }
            };
        }

        public string Password { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var password1 = Field1.Password;
            var password2 = Field2.Password;

            if (string.IsNullOrEmpty(password1))
            {
                VisualUtilities.ShakeView(Field1);
                args.Cancel = true;
                return;
            }

            if (string.IsNullOrEmpty(password2))
            {
                VisualUtilities.ShakeView(Field2);
                args.Cancel = true;
                return;
            }

            if (!string.Equals(password1, password2))
            {
                ToastPopup.Show(Strings.PasswordDoNotMatch);
                args.Cancel = true;
                return;
            }

            Password = password1;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Password_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (sender == Field1)
                {
                    Field2.Focus(FocusState.Keyboard);
                }
                else
                {
                    Hide(ContentDialogResult.Primary);
                }

                e.Handled = true;
            }
        }

        private void Reveal_Click(object sender, RoutedEventArgs e)
        {
            Field1.PasswordRevealMode = Reveal.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
            Field2.PasswordRevealMode = Reveal.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
            Animated.Seek(Reveal.IsChecked == true ? "Close" : "CloseToPeek");
        }
    }
}
