using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Settings;
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

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsEmojiPage : Page, IFileDelegate
    {
        public SettingsEmojiViewModel ViewModel => DataContext as SettingsEmojiViewModel;

        public SettingsEmojiPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsEmojiViewModel, IFileDelegate>(this);
        }

        private async void EmojiPack_Click(object sender, RoutedEventArgs e)
        {
            var radio = sender as RadioButton;
            var emojiPack = radio.Tag as EmojiSet;

            var file = emojiPack.Document;
            if (file.Local.IsDownloadingActive)
            {

            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                ViewModel.ProtoService.DownloadFile(file.Id, 32);
            }
            else
            {
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("emoji", CreationCollisionOption.OpenIfExists);
                var result = await folder.TryGetItemAsync($"{emojiPack.Id}.ttf");
                if (result == null)
                {
                    return;
                }

                Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"ms-appdata:///local/emoji/{emojiPack.Id}.ttf#Segoe UI");
                ViewModel.Settings.Appearance.EmojiSet = emojiPack.Title;
                ViewModel.Settings.Appearance.EmojiSetId = emojiPack.Id;

                if (Window.Current.Content is FrameworkElement element)
                {
                    element.RequestedTheme = ElementTheme.Dark;
                    element.RequestedTheme = ElementTheme.Light;
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

            radio.IsChecked = ViewModel.Settings.Appearance.EmojiSetId == emojiPack.Id;

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
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    subtitle.Text = string.Format("{0} {1}", Strings.Resources.AccActionDownload, FileSizeConverter.Convert(size));
                }
                else
                {
                    if (ViewModel.Settings.Appearance.EmojiSetId == emojiPack.Id)
                    {
                        subtitle.Text = "Current Set";
                    }
                    else
                    {
                        subtitle.Text = emojiPack.IsDefault ? Strings.Resources.Default : "Downloaded";
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as Image;

                var file = emojiPack.Thumbnail;
                if (file.Local.IsDownloadingCompleted)
                {
                    photo.Source = new BitmapImage { UriSource = new Uri("file:///" + file.Local.Path), DecodePixelWidth = 48, DecodePixelHeight = 48 };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    photo.Source = null;
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion

        #region Delegate

        public async void UpdateFile(File file)
        {
            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("emoji", CreationCollisionOption.OpenIfExists);

            foreach (EmojiSet emojiPack in ViewModel.Items)
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

                    radio.IsChecked = ViewModel.Settings.Appearance.EmojiSetId == emojiPack.Id;

                    if (file.Id == emojiPack.Document.Id)
                    {
                        var subtitle = content.Children[2] as TextBlock;

                        var size = Math.Max(file.Size, file.ExpectedSize);
                        if (file.Local.IsDownloadingActive)
                        {
                            subtitle.Text = string.Format("{0} {1} / {2}", "Downloading", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                        }
                        else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                        {
                            subtitle.Text = string.Format("{0} {1}", Strings.Resources.AccActionDownload, FileSizeConverter.Convert(size));
                        }
                        else
                        {
                            if (ViewModel.Settings.Appearance.EmojiSetId == emojiPack.Id)
                            {
                                subtitle.Text = "Current Set";
                            }
                            else
                            {
                                subtitle.Text = emojiPack.IsDefault ? Strings.Resources.Default : "Downloaded";
                            }

                            var storage = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                            var result = await storage.CopyAsync(folder, $"{emojiPack.Id}.ttf", NameCollisionOption.ReplaceExisting);
                        }
                    }
                    if (file.Id == emojiPack.Thumbnail.Id)
                    {
                        var photo = content.Children[0] as Image;

                        if (file.Local.IsDownloadingCompleted)
                        {
                            photo.Source = new BitmapImage { UriSource = new Uri("file:///" + file.Local.Path), DecodePixelWidth = 48, DecodePixelHeight = 48 };

                            var storage = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                            var result = await storage.CopyAsync(folder, $"{emojiPack.Id}.png", NameCollisionOption.ReplaceExisting);
                        }
                        else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            photo.Source = null;
                            ViewModel.ProtoService.DownloadFile(file.Id, 1);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
