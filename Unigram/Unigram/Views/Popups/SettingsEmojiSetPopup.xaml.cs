using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Views.Popups
{
    public sealed partial class SettingsEmojiSetPopup : ContentPopup, IHandle<UpdateFile>
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        private readonly ItemsCollection _collection;

        private EmojiSet _selectedSet;

        public SettingsEmojiSetPopup(IProtoService protoService, IEmojiSetService emojiSetService, IEventAggregator aggregator)
        {
            this.InitializeComponent();

            _protoService = protoService;
            _aggregator = aggregator;

            Title = "Emoji Set";
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            _aggregator.Subscribe(this);
            List.ItemsSource = _collection = new ItemsCollection(emojiSetService);
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            _aggregator.Unsubscribe(this);
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
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                _protoService.DownloadFile(file.Id, 32);
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
                    subtitle.Foreground = App.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    subtitle.Text = string.Format("{0} {1}", Strings.Resources.AccActionDownload, FileSizeConverter.Convert(size));
                    subtitle.Foreground = App.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                }
                else
                {
                    if (SettingsService.Current.Appearance.EmojiSet.Id == emojiPack.Id)
                    {
                        subtitle.Text = "Current Set";
                        subtitle.Foreground = App.Current.Resources["SystemControlForegroundAccentBrush"] as Brush;
                    }
                    else
                    {
                        subtitle.Text = emojiPack.IsDefault ? Strings.Resources.Default : "Downloaded";
                        subtitle.Foreground = App.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as Image;

                var file = emojiPack.Thumbnail;
                if (file != null && file.Local.IsDownloadingCompleted)
                {
                    photo.Source = new BitmapImage { UriSource = new Uri("file:///" + file.Local.Path), DecodePixelWidth = 40, DecodePixelHeight = 40 };
                }
                else if (file != null && file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    photo.Source = null;
                    _protoService.DownloadFile(file.Id, 1);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        public async void Handle(UpdateFile update)
        {
            var emojiSet = _collection.FirstOrDefault(x => x.UpdateFile(update.File));
            if (emojiSet == null)
            {
                return;
            }

            var file = update.File;
            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("emoji", CreationCollisionOption.OpenIfExists);

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (file.Id == _selectedSet?.Document.Id && !file.Local.IsDownloadingCompleted)
                {
                    IsPrimaryButtonEnabled = false;
                }
                else
                {
                    IsPrimaryButtonEnabled = true;
                }

                var container = List.ContainerFromItem(emojiSet) as SelectorItem;
                if (container == null)
                {
                    return;
                }

                var radio = container.ContentTemplateRoot as RadioButton;
                if (radio == null)
                {
                    return;
                }

                var content = radio.Content as Grid;
                if (content == null)
                {
                    return;
                }

                radio.IsChecked = (_selectedSet?.Id ?? SettingsService.Current.Appearance.EmojiSet.Id) == emojiSet.Id;

                if (file.Id == emojiSet.Document.Id)
                {
                    var subtitle = content.Children[2] as TextBlock;

                    var size = Math.Max(file.Size, file.ExpectedSize);
                    if (file.Local.IsDownloadingActive)
                    {
                        subtitle.Text = string.Format("{0} {1} / {2}", "Downloading", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                        subtitle.Foreground = App.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                    }
                    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                    {
                        subtitle.Text = string.Format("{0} {1}", Strings.Resources.AccActionDownload, FileSizeConverter.Convert(size));
                        subtitle.Foreground = App.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                    }
                    else
                    {
                        if (SettingsService.Current.Appearance.EmojiSet.Id == emojiSet.Id)
                        {
                            subtitle.Text = "Current Set";
                            subtitle.Foreground = App.Current.Resources["SystemControlForegroundAccentBrush"] as Brush;
                        }
                        else
                        {
                            subtitle.Text = emojiSet.IsDefault ? Strings.Resources.Default : "Downloaded";
                            subtitle.Foreground = App.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                        }
                    }
                }
                if (file.Id == emojiSet.Thumbnail.Id)
                {
                    var photo = content.Children[0] as Image;

                    if (file.Local.IsDownloadingCompleted)
                    {
                        photo.Source = new BitmapImage { UriSource = new Uri("file:///" + file.Local.Path), DecodePixelWidth = 40, DecodePixelHeight = 40 };
                    }
                    else
                    {
                        photo.Source = null;
                    }
                }
            });
        }

        #endregion

        class ItemsCollection : MvxObservableCollection<EmojiSet>, ISupportIncrementalLoading
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
