using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Services;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Views.Popups
{
    public sealed partial class SettingsEmojiSetPopup : ContentPopup
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        private readonly ItemsCollection _collection;

        private EmojiSet _selectedSet;

        public SettingsEmojiSetPopup(IProtoService protoService, IEmojiSetService emojiSetService, IEventAggregator aggregator)
        {
            InitializeComponent();

            _protoService = protoService;
            _aggregator = aggregator;

            Title = "Emoji Set";
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            List.ItemsSource = _collection = new ItemsCollection(emojiSetService);
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var emojiSet = _selectedSet;
            if (emojiSet == null)
            {
                args.Cancel = true;
                return;
            }

            var file = emojiSet.Document;
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                args.Cancel = true;
                return;
            }

            switch (emojiSet.Id)
            {
                case "microsoft":
                    Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"XamlAutoFontFamily");
                    break;
                case "apple":
                    Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"ms-appx:///Assets/Emoji/{emojiSet.Id}.ttf#Segoe UI Emoji");
                    break;
                default:
                    Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"ms-appdata:///local/emoji/{emojiSet.Id}.{emojiSet.Version}.ttf#Segoe UI Emoji");
                    break;
            }

            SettingsService.Current.Appearance.EmojiSet = emojiSet.ToInstalled();
            SettingsService.Current.Appearance.UpdateNightMode(true);
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void EmojiSet_Click(object sender, RoutedEventArgs e)
        {
            var radio = sender as RadioButton;
            var emojiSet = radio.Tag as EmojiSet;

            _selectedSet = emojiSet;

            var file = emojiSet.Document;
            if (file.Local.IsDownloadingCompleted)
            {

            }
            else
            {
                UpdateManager.Subscribe(radio, _protoService, file, UpdateFile);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    _protoService.DownloadFile(file.Id, 32);
                }
            }
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var radio = args.ItemContainer.ContentTemplateRoot as RadioButton;
            var content = radio.Content as Grid;
            var emojiPack = args.Item as EmojiSet;

            radio.Click -= EmojiSet_Click;
            radio.Click += EmojiSet_Click;
            radio.IsChecked = SettingsService.Current.Appearance.EmojiSet.Id == emojiPack.Id;

            if (SettingsService.Current.Appearance.EmojiSet.Id == emojiPack.Id)
            {
                _selectedSet = emojiPack;
            }

            radio.Tag = emojiPack;
            content.Tag = emojiPack;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = emojiPack.Title;
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                var file = emojiPack.Document;

                var size = Math.Max(file.Size, file.ExpectedSize);
                if (file.Local.IsDownloadingActive)
                {
                    subtitle.Text = string.Format("{0} {1} / {2}", "Downloading", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                    subtitle.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    subtitle.Text = string.Format("{0} {1}", Strings.Resources.AccActionDownload, FileSizeConverter.Convert(size));
                    subtitle.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                }
                else
                {
                    if (SettingsService.Current.Appearance.EmojiSet.Id == emojiPack.Id)
                    {
                        subtitle.Text = "Current Set";
                        subtitle.Foreground = BootStrapper.Current.Resources["SystemControlForegroundAccentBrush"] as Brush;
                    }
                    else
                    {
                        subtitle.Text = emojiPack.IsDefault ? Strings.Resources.Default : "Downloaded";
                        subtitle.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as Image;

                var file = emojiPack.Thumbnail;
                if (file != null && file.Local.IsDownloadingCompleted)
                {
                    photo.Source = new BitmapImage { UriSource = UriEx.ToLocal(file.Local.Path), DecodePixelWidth = 40, DecodePixelHeight = 40 };
                }
                else if (file != null)
                {
                    photo.Source = null;

                    UpdateManager.Subscribe(photo, _protoService, file, UpdateThumbnail, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        _protoService.DownloadFile(file.Id, 1);
                    }
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        private void UpdateFile(object target, File file)
        {
            if (file.Id == _selectedSet?.Document.Id && !file.Local.IsDownloadingCompleted)
            {
                IsPrimaryButtonEnabled = false;
            }
            else
            {
                IsPrimaryButtonEnabled = true;
            }

            var radio = target as RadioButton;
            if (radio == null)
            {
                return;
            }

            var emojiSet = radio.Tag as EmojiSet;
            if (emojiSet == null)
            {
                return;
            }

            var content = radio.Content as Grid;
            if (content == null)
            {
                return;
            }

            radio.IsChecked = (_selectedSet?.Id ?? SettingsService.Current.Appearance.EmojiSet.Id) == emojiSet.Id;

            var subtitle = content.Children[2] as TextBlock;

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                subtitle.Text = string.Format("{0} {1} / {2}", "Downloading", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                subtitle.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                subtitle.Text = string.Format("{0} {1}", Strings.Resources.AccActionDownload, FileSizeConverter.Convert(size));
                subtitle.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
            }
            else
            {
                if (SettingsService.Current.Appearance.EmojiSet.Id == emojiSet.Id)
                {
                    subtitle.Text = "Current Set";
                    subtitle.Foreground = BootStrapper.Current.Resources["SystemControlForegroundAccentBrush"] as Brush;
                }
                else
                {
                    subtitle.Text = emojiSet.IsDefault ? Strings.Resources.Default : "Downloaded";
                    subtitle.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                }
            }
        }

        private void UpdateThumbnail(object target, File file)
        {
            if (target is Image photo)
            {
                photo.Source = new BitmapImage { UriSource = UriEx.ToLocal(file.Local.Path), DecodePixelWidth = 40, DecodePixelHeight = 40 };
            }
        }

        #endregion

        private class ItemsCollection : MvxObservableCollection<EmojiSet>, ISupportIncrementalLoading
        {
            private readonly IEmojiSetService _emojiSetService;
            private bool _hasMoreItems = true;

            public ItemsCollection(IEmojiSetService emojiSetService)
            {
                _emojiSetService = emojiSetService;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async (token) =>
                {
                    Add(new EmojiSet { Id = "apple", Title = "Apple", IsOfficial = true, IsDefault = true, Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.png") } } });
                    Add(new EmojiSet { Id = "microsoft", Title = "Microsoft", IsOfficial = true, Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.png") } } });

                    var results = await _emojiSetService.GetCloudSetsAsync();

                    foreach (var item in results)
                    {
                        Add(item);
                    }

                    _hasMoreItems = false;
                    return new LoadMoreItemsResult { Count = 0 };
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }
    }
}
