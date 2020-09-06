using System;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Settings
{
    public class SettingsShortcutsViewModel : TLViewModelBase
    {
        private readonly IShortcutsService _shortcutsService;

        public SettingsShortcutsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IShortcutsService shortcutsService)
            : base(protoService, cacheService, settingsService, aggregator)
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

            var confirm = await dialog.ShowAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                Items.ReplaceWith(_shortcutsService.Update(dialog.Shortcut, info.Command));
            }
        }
    }
}
