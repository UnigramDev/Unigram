using Template10.Common;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.UI.Xaml;
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
            var dialog = new TLContentDialog();
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = StickersSuggestionMode.All, Content = Strings.Resources.SuggestStickersAll, IsChecked = SuggestStickers == StickersSuggestionMode.All });
            stack.Children.Add(new RadioButton { Tag = StickersSuggestionMode.Installed, Content = Strings.Resources.SuggestStickersInstalled, IsChecked = SuggestStickers == StickersSuggestionMode.Installed });
            stack.Children.Add(new RadioButton { Tag = StickersSuggestionMode.None, Content = Strings.Resources.SuggestStickersNone, IsChecked = SuggestStickers == StickersSuggestionMode.None });

            dialog.Title = Strings.Resources.SuggestStickers;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var mode = StickersSuggestionMode.All;
                foreach (RadioButton current in stack.Children)
                {
                    if (current.IsChecked == true)
                    {
                        mode = (StickersSuggestionMode)current.Tag;
                        break;
                    }
                }

                SuggestStickers = mode;
            }
        }
    }
}
