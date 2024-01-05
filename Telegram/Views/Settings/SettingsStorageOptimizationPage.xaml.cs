//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsStorageOptimizationPage : ContentPopup
    {
        public SettingsStorageOptimizationPage(IClientService clientService, StorageStatisticsByChat statistics)
        {
            InitializeComponent();

            PrimaryButtonText = Strings.CacheClear;
            SecondaryButtonText = Strings.Close;

            var chat = clientService.GetChat(statistics.ChatId);

            Title = chat == null
                ? Strings.ClearMediaCache
                : clientService.GetTitle(chat);

            StorageChartItem photo = null;
            StorageChartItem video = null;
            StorageChartItem document = null;
            StorageChartItem audio = null;
            StorageChartItem voice = null;
            StorageChartItem stickers = null;
            StorageChartItem stories = null;
            StorageChartItem local = null;

            foreach (var fileType in statistics.ByFileType)
            {
                switch (fileType.FileType)
                {
                    case FileTypePhoto:
                        photo = new StorageChartItem(fileType);
                        break;
                    case FileTypeVideo:
                    case FileTypeAnimation:
                        video = video?.Add(fileType) ?? new StorageChartItem(fileType);
                        break;
                    case FileTypeDocument:
                        document = new StorageChartItem(fileType);
                        break;
                    case FileTypeAudio:
                        audio = new StorageChartItem(fileType);
                        break;
                    case FileTypeVideoNote:
                    case FileTypeVoiceNote:
                        voice = voice?.Add(fileType) ?? new StorageChartItem(fileType);
                        break;
                    case FileTypeSticker:
                        stickers = new StorageChartItem(fileType);
                        break;
                    case FileTypePhotoStory:
                    case FileTypeVideoStory:
                        stories = stories?.Add(fileType) ?? new StorageChartItem(fileType);
                        break;
                    case FileTypeProfilePhoto:
                    case FileTypeWallpaper:
                        break;
                    default:
                        local = local?.Add(fileType) ?? new StorageChartItem(fileType);
                        break;
                }
            }

            var items = new[]
            {
                photo,
                video,
                document,
                audio,
                voice,
                stickers,
                stories,
                local
            }.Where(x => x != null).ToList();

            ScrollingHost.ItemsSource = items;
            Chart.Items = items;

            var size = Chart.Items.Where(x => x.IsVisible).Sum(x => x.Size);
            var readable = FileSizeConverter.Convert(size, true).Split(' ');

            SizeLabel.Text = readable[0];
            UnitLabel.Text = readable[1];
        }

        public IList<FileType> SelectedItems { get; private set; }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is CheckBox check && args.Item is StorageChartItem item)
            {
                var content = check.Content as StackPanel;

                var title = content.Children[0] as TextBlock;
                var subtitle = content.Children[1] as TextBlock;

                check.Click -= CheckBox_Click;
                check.Click += CheckBox_Click;

                check.Background = new SolidColorBrush(item.Stroke);
                check.IsChecked = item.IsVisible;

                // Justified because used in CheckBox_Click
                check.Tag = item;

                title.Text = item.Name;
                subtitle.Text = FileSizeConverter.Convert(item.Size, true);

                args.Handled = true;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var items = ScrollingHost.ItemsSource as IList<StorageChartItem>;
            if (items != null)
            {
                SelectedItems = items.Where(x => x.IsVisible).SelectMany(x => x.Types).ToList();
            }
            else
            {
                SelectedItems = null;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SelectedItems = null;
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var check = sender as CheckBox;
            var item = check.Tag as StorageChartItem;

            var index = Chart.Items.IndexOf(item);
            if (index < 0)
            {
                return;
            }

            if (item.IsVisible && Chart.Items.Except(new[] { item }).Any(x => x.IsVisible))
            {
                item.IsVisible = false;
                check.IsChecked = false;

                Chart.Update(index, item.IsVisible);
            }
            else if (!item.IsVisible)
            {
                item.IsVisible = true;
                check.IsChecked = true;

                Chart.Update(index, item.IsVisible);
            }
            else
            {
                VisualUtilities.ShakeView(check);
            }

            var size = Chart.Items.Where(x => x.IsVisible).Sum(x => x.Size);
            var readable = FileSizeConverter.Convert(size, true).Split(' ');

            SizeLabel.Text = readable[0];
            UnitLabel.Text = readable[1];
        }
    }
}
