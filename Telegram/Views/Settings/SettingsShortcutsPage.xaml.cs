//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Controls;
using Telegram.Services;
using Telegram.ViewModels.Settings;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsShortcutsPage : HostedPage
    {
        public SettingsShortcutsViewModel ViewModel => DataContext as SettingsShortcutsViewModel;

        public SettingsShortcutsPage()
        {
            InitializeComponent();
        }

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as BadgeButton;
            var info = sender.ItemsSourceView.GetAt(args.Index) as ShortcutInfo;
            //var info = button.DataContext as ShortcutInfo;

            button.Content = info.Command;
            //button.Badge = info.Shortcut;
            button.Command = ViewModel.EditCommand;
            button.CommandParameter = info;
        }
    }
}
