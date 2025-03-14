//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Telegram.Controls;
using Telegram.Services;

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

        private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
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
