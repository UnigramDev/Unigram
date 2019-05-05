using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels.Settings;
using Unigram.Views;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class SettingsEmojiSetView : TLContentDialog
    {
        private EmojiSet _selectedSet;

        public SettingsEmojiSetView()
        {
            this.InitializeComponent();

            Title = "Emoji Set";
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            var items = new List<EmojiSet>();
            items.Add(new EmojiSet { Id = "apple", Title = "Apple", IsDefault = true, Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.png") } } });
            items.Add(new EmojiSet { Id = "microsoft", Title = "Microsoft", Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.png") } } });

            List.ItemsSource = items;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var emojiSet = _selectedSet;
            if (emojiSet == null)
            {
                args.Cancel = true;
                return;
            }

            Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"ms-appx:///Assets/Emoji/{emojiSet.Id}.ttf#Segoe UI Emoji");
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
                photo.Source = new BitmapImage { UriSource = new Uri("file:///" + file.Local.Path), DecodePixelWidth = 40, DecodePixelHeight = 40 };
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion
    }
}
