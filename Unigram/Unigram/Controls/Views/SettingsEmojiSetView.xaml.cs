using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels.Settings;
using Unigram.Views;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class SettingsEmojiSetView : TLContentDialog, IHandle<UpdateFile>
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        private EmojiSet _selectedSet;

        public SettingsEmojiSetView(IProtoService protoService, IEventAggregator aggregator)
        {
            this.InitializeComponent();

            _protoService = protoService;
            _aggregator = aggregator;

            Title = "Emoji Set";
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

#if DEBUG
            CloseButtonText = "Reset";
            CloseButtonClick += (s, args) =>
            {
                args.Cancel = true;

                foreach (var item in List.ItemsSource as ItemsCollection)
                {
                    if (item.IsOfficial)
                    {
                        continue;
                    }

                    _protoService.Send(new DeleteFileW(item.Document.Id));
                    _protoService.Send(new DeleteFileW(item.Thumbnail.Id));
                }
            };
#endif

            //var items = new List<EmojiSet>();
            //items.Add(new EmojiSet { Id = "apple", Title = "Apple", IsDefault = true, Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.png") } } });
            //items.Add(new EmojiSet { Id = "microsoft", Title = "Microsoft", Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.png") } } });

            _aggregator.Subscribe(this);
            List.ItemsSource = new ItemsCollection(protoService);
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

            switch (emojiSet.Id)
            {
                case "microsoft":
                    Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"XamlAutoFontFamily");
                    break;
                case "apple":
                    Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"ms-appx:///Assets/Emoji/{emojiSet.Id}.ttf#Segoe UI Emoji");
                    break;
                default:
                    Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"ms-appdata:///local/emoji/{emojiSet.Id}.ttf#Segoe UI Emoji");
                    break;
            }

            SettingsService.Current.Appearance.EmojiSet = emojiSet.Title;
            SettingsService.Current.Appearance.EmojiSetId = emojiSet.Id;

            TLContainer.Current.Resolve<IThemeService>().Refresh();
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

            radio.IsChecked = SettingsService.Current.Appearance.EmojiSetId == emojiPack.Id;

            if (SettingsService.Current.Appearance.EmojiSetId == emojiPack.Id)
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
                    if (SettingsService.Current.Appearance.EmojiSetId == emojiPack.Id)
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
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => UpdateFile(update.File));
        }

        private async void UpdateFile(File file)
        {
            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("emoji", CreationCollisionOption.OpenIfExists);

            if (file.Id == _selectedSet?.Document.Id && !file.Local.IsDownloadingCompleted)
            {
                IsPrimaryButtonEnabled = false;
            }
            else
            {
                IsPrimaryButtonEnabled = true;
            }

            foreach (EmojiSet emojiPack in List.ItemsSource as ItemsCollection)
            {
                if (emojiPack.UpdateFile(file))
                {
                    var container = List.ContainerFromItem(emojiPack) as SelectorItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var radio = container.ContentTemplateRoot as RadioButton;
                    if (radio == null)
                    {
                        continue;
                    }

                    var content = radio.Content as Grid;
                    if (content == null)
                    {
                        continue;
                    }

                    radio.IsChecked = (_selectedSet?.Id ?? SettingsService.Current.Appearance.EmojiSetId) == emojiPack.Id;

                    if (file.Id == emojiPack.Document.Id)
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
                            if (SettingsService.Current.Appearance.EmojiSetId == emojiPack.Id)
                            {
                                subtitle.Text = "Current Set";
                                subtitle.Foreground = App.Current.Resources["SystemControlForegroundAccentBrush"] as Brush;
                            }
                            else
                            {
                                subtitle.Text = emojiPack.IsDefault ? Strings.Resources.Default : "Downloaded";
                                subtitle.Foreground = App.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                            }

                            await ItemsCollection.TryCopyPartLocally(folder, file.Local.Path, emojiPack.Id, ".ttf");
                        }
                    }
                    if (file.Id == emojiPack.Thumbnail.Id)
                    {
                        var photo = content.Children[0] as Image;

                        if (file.Local.IsDownloadingCompleted)
                        {
                            photo.Source = new BitmapImage { UriSource = new Uri("file:///" + file.Local.Path), DecodePixelWidth = 40, DecodePixelHeight = 40 };
                            await ItemsCollection.TryCopyPartLocally(folder, file.Local.Path, emojiPack.Id, ".png");
                        }
                        else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            photo.Source = null;
                            _protoService.DownloadFile(file.Id, 1);
                        }
                    }
                }
            }
        }

        #endregion

        class ItemsCollection : MvxObservableCollection<EmojiSet>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private long? _chatId;
            private long _fromMessageId;

            private Dictionary<string, EmojiSet> _dict;

            private bool _hasMoreItems = true;

            public ItemsCollection(IProtoService protoService)
            {
                _protoService = protoService;
                _dict = new Dictionary<string, EmojiSet>();
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async (token) =>
                {
                    if (_chatId == null)
                    {
                        Add(new EmojiSet { Id = "apple", Title = "Apple", IsOfficial = true, IsDefault = true, Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.png") } } });
                        Add(new EmojiSet { Id = "microsoft", Title = "Microsoft", IsOfficial = true, Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.png") } } });

                        var responseChat = await _protoService.SendAsync(new SearchPublicChat("cGFnbGlhY2Npb19kaV9naGlhY2Npbw"));
                        if (responseChat is Chat chat)
                        {
                            _chatId = chat.Id;

                            await _protoService.SendAsync(new OpenChat(chat.Id));
                        }
                        else
                        {
                            _hasMoreItems = false;
                            return new LoadMoreItemsResult { Count = 2 };
                        }
                    }

                    var response = await _protoService.SendAsync(new SearchChatMessages(_chatId.Value, string.Empty, 0, _fromMessageId, 0, 100, new SearchMessagesFilterDocument()));
                    if (response is Telegram.Td.Api.Messages messages)
                    {
                        _hasMoreItems = messages.MessagesValue.Count > 0;

                        var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("emoji", CreationCollisionOption.OpenIfExists);

                        foreach (var message in messages.MessagesValue)
                        {
                            _fromMessageId = message.Id;

                            var document = message.Content as MessageDocument;
                            if (document == null)
                            {
                                continue;
                            }

                            var hashtags = new List<string>();
                            var title = string.Empty;

                            foreach (var entity in document.Caption.Entities)
                            {
                                if (entity.Type is TextEntityTypeHashtag)
                                {
                                    hashtags.Add(document.Caption.Text.Substring(entity.Offset, entity.Length));
                                }
                                else if (entity.Type is TextEntityTypeCode)
                                {
                                    title = document.Caption.Text.Substring(entity.Offset, entity.Length);
                                }
                            }

                            if (!hashtags.Contains("#emoji"))
                            {
                                continue;
                            }

                            if (hashtags.Contains("#preview"))
                            {
                                var file = document.Document.DocumentValue;
                                if (file.Local.IsDownloadingCompleted)
                                {
                                    await TryCopyPartLocally(folder, file.Local.Path, document.Document.FileName, ".png");
                                }

                                if (_dict.TryGetValue(document.Document.FileName, out EmojiSet pack))
                                {
                                    _dict.Remove(document.Document.FileName);

                                    pack.Thumbnail = document.Document.DocumentValue;
                                    Add(pack);
                                }
                                else
                                {
                                    _dict[document.Document.FileName] = new EmojiSet
                                    {
                                        Thumbnail = document.Document.DocumentValue
                                    };
                                }
                            }
                            else
                            {
                                var file = document.Document.DocumentValue;
                                if (file.Local.IsDownloadingCompleted)
                                {
                                    await TryCopyPartLocally(folder, file.Local.Path, document.Document.FileName, ".ttf");
                                }

                                if (_dict.TryGetValue(document.Document.FileName, out EmojiSet pack))
                                {
                                    _dict.Remove(document.Document.FileName);

                                    pack.Id = document.Document.FileName;
                                    pack.Title = title;
                                    pack.Document = document.Document.DocumentValue;
                                    Add(pack);
                                }
                                else
                                {
                                    _dict[document.Document.FileName] = new EmojiSet
                                    {
                                        Id = document.Document.FileName,
                                        Title = title,
                                        Document = document.Document.DocumentValue
                                    };
                                }
                            }
                        }

                        return new LoadMoreItemsResult { Count = (uint)messages.MessagesValue.Count };
                    }
                    else
                    {
                        _hasMoreItems = false;
                        return new LoadMoreItemsResult { Count = 0 };
                    }
                });
            }

            public bool HasMoreItems => _hasMoreItems;

            public static async Task TryCopyPartLocally(StorageFolder folder, string path, string id, string extension)
            {
                var cache = await folder.TryGetItemAsync($"{id}{extension}");
                if (cache == null)
                {
                    try
                    {
                        var result = await StorageFile.GetFileFromPathAsync(path);
                        await result.CopyAsync(folder, $"{id}{extension}", NameCollisionOption.FailIfExists);
                    }
                    catch { }
                }
            }
        }
    }
}
