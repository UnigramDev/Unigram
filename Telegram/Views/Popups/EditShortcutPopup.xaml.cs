//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Telegram.Views.Popups
{
    public sealed partial class EditShortcutPopup : ContentPopup
    {
        private readonly IShortcutsService _shortcutsService;

        public EditShortcutPopup(IShortcutsService shortcutsService, ShortcutInfo info)
        {
            InitializeComponent();

            _shortcutsService = shortcutsService;

            Title = info.Command;
            TextField.Text = info.Shortcut.ToString();

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        public Shortcut Shortcut { get; private set; }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            WindowContext.Current.InputListener.KeyDown += OnAcceleratorKeyActivated;
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            WindowContext.Current.InputListener.KeyDown -= OnAcceleratorKeyActivated;
        }

        private void OnAcceleratorKeyActivated(Window sender, InputKeyDownEventArgs args)
        {
            if (TextField.FocusState != FocusState.Unfocused && _shortcutsService.TryGetShortcut(args, out Shortcut shortcut))
            {
                args.Handled = true;

                Shortcut = shortcut;
                TextField.Text = shortcut.ToString();
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = Shortcut == null;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void TextField_Loaded(object sender, RoutedEventArgs e)
        {
            TextField.Focus(FocusState.Keyboard);
        }
    }
}
