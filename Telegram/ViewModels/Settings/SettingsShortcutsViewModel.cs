//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Settings
{
    public class SettingsShortcutsViewModel : ViewModelBase
    {
        private readonly IShortcutsService _shortcutsService;

        public SettingsShortcutsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IShortcutsService shortcutsService)
            : base(clientService, settingsService, aggregator)
        {
            _shortcutsService = shortcutsService;

            Items = new MvxObservableCollection<ShortcutList>(shortcutsService.GetShortcuts());

            EditCommand = new RelayCommand<ShortcutInfo>(EditExecute);
        }

        public MvxObservableCollection<ShortcutList> Items { get; private set; }

        public RelayCommand<ShortcutInfo> EditCommand { get; }
        private async void EditExecute(ShortcutInfo info)
        {
            var dialog = new EditShortcutPopup(_shortcutsService, info);

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                Items.ReplaceWith(_shortcutsService.Update(dialog.Shortcut, info.Command));
            }
        }
    }
}
