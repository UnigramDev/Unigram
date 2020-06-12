using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsStorageOptimizationPage : ContentPopup
    {
        public SettingsStorageOptimizationPage(IProtoService protoService, StorageStatisticsByChat statistics)
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Resources.CacheClear;
            SecondaryButtonText = Strings.Resources.Close;

            var chat = protoService.GetChat(statistics.ChatId);

            Title.Text = chat == null ? "Other Chats" : protoService.GetTitle(chat);
            Subtitle.Text = FileSizeConverter.Convert(statistics.Size, true);

            Photo.Source = chat == null ? null : PlaceholderHelper.GetChat(protoService, chat, (int)Photo.Width);
            Photo.Visibility = chat == null ? Visibility.Collapsed : Visibility.Visible;

            StorageChartItem photo = null;
            StorageChartItem video = null;
            StorageChartItem document = null;
            StorageChartItem audio = null;
            StorageChartItem voice = null;
            StorageChartItem stickers = null;
            StorageChartItem local = null;

            foreach (var fileType in statistics.ByFileType)
            {
                switch (fileType.FileType)
                {
                    case FileTypePhoto fileTypePhoto:
                        photo = new StorageChartItem(fileType);
                        break;
                    case FileTypeVideo fileTypeVideo:
                        video = new StorageChartItem(fileType);
                        break;
                    case FileTypeDocument fileTypeDocument:
                        document = new StorageChartItem(fileType);
                        break;
                    case FileTypeAudio fileTypeAudio:
                        audio = new StorageChartItem(fileType);
                        break;
                    case FileTypeVideoNote videoNote:
                    case FileTypeVoiceNote voiceNote:
                        voice = voice?.Add(fileType) ?? new StorageChartItem(fileType);
                        break;
                    case FileTypeSticker fileTypeSticker:
                        stickers = new StorageChartItem(fileType);
                        break;
                    default:
                        local = local?.Add(fileType) ?? new StorageChartItem(fileType);
                        break;
                }
            }

            var items = new[] { photo, video, document, audio, voice, stickers, local }.Where(x => x != null).ToList();
            List.ItemsSource = items;
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

            var check = args.ItemContainer.ContentTemplateRoot as CheckBox;
            var item = args.Item as StorageChartItem;

            if (item == null)
            {
                return;
            }

            var content = check.Content as StackPanel;

            var title = content.Children[0] as TextBlock;
            var subtitle = content.Children[1] as TextBlock;

            check.Click -= CheckBox_Click;
            check.Click += CheckBox_Click;

            check.Background = new SolidColorBrush(item.Stroke);
            check.IsChecked = item.IsVisible;

            check.Tag = item;

            title.Text = item.Name;
            subtitle.Text = FileSizeConverter.Convert(item.Size, true);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var items = List.ItemsSource as IList<StorageChartItem>;
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
