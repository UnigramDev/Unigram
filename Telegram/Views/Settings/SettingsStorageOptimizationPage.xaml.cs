//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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

            TitleLabel.Text = chat == null
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
            }.Where(x => x != null).OrderByDescending(x => x.TotalBytes).ToList();

            ScrollingHost.ItemsSource = items;
            Chart.Items = items;
        }

        public Vector<FileType> SelectedItems { get; private set; }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null && args.Item is StorageChartItem item)
            {
                args.ItemContainer = new ListViewItem
                {
                    Style = sender.ItemContainerStyle,
                    ContentTemplate = sender.ItemTemplate,
                    Resources = new CheckBoxResources
                    {
                        Color = item.Stroke
                    }
                };
            }

            args.IsContainerPrepared = true;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var items = ScrollingHost.ItemsSource as IList<StorageChartItem>;
            if (items != null)
            {
                SelectedItems = items.Where(x => x.IsVisible).SelectMany(x => x.Types).ToVector();
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

        private void StorageChartItem_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox check || check.DataContext is not StorageChartItem item)
            {
                return;
            }

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
        }
    }
}
