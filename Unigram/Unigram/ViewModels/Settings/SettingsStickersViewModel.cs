using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersViewModel : SettingsStickersViewModelBase
    {
        public SettingsStickersViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, false)
        {
            SuggestCommand = new RelayCommand(SuggestExecute);
        }

        public StickersSuggestionMode SuggestStickers
        {
            get
            {
                return Settings.Stickers.SuggestionMode;
            }
            set
            {
                Settings.Stickers.SuggestionMode = value;
                RaisePropertyChanged();
            }
        }

        public bool IsLoopingEnabled
        {
            get
            {
                return Settings.Stickers.IsLoopingEnabled;
            }
            set
            {
                Settings.Stickers.IsLoopingEnabled = value;
                RaisePropertyChanged();
            }
        }

        public RelayCommand SuggestCommand { get; }
        private async void SuggestExecute()
        {
            var items = new[]
            {
                new SelectRadioItem(StickersSuggestionMode.All, Strings.Resources.SuggestStickersAll, SuggestStickers == StickersSuggestionMode.All),
                new SelectRadioItem(StickersSuggestionMode.Installed, Strings.Resources.SuggestStickersInstalled, SuggestStickers == StickersSuggestionMode.Installed),
                new SelectRadioItem(StickersSuggestionMode.None, Strings.Resources.SuggestStickersNone, SuggestStickers == StickersSuggestionMode.None),
            };

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.SuggestStickers;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is StickersSuggestionMode index)
            {
                SuggestStickers = index;
            }
        }
    }
}
