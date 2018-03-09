using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsStorageOptimizationPage : Page
    {
        private readonly ContentDialogBase _dialog;

        public SettingsStorageOptimizationPage(IProtoService protoService, ContentDialogBase dialog, StorageStatisticsByChat statistics)
        {
            InitializeComponent();

            _dialog = dialog;

            var chat = protoService.GetChat(statistics.ChatId);

            Title.Text = chat == null ? "Other Chats" : protoService.GetTitle(chat);
            Subtitle.Text = FileSizeConverter.Convert(statistics.Size);

            Photo.Source = chat == null ? null : PlaceholderHelper.GetChat(protoService, chat, 36, 36);
            Photo.Visibility = chat == null ? Visibility.Collapsed : Visibility.Visible;

            List.ItemsSource = statistics.ByFileType.OrderByDescending(x => x.Size).ToList();
            
            foreach (var fileType in statistics.ByFileType)
            {
                switch (fileType.FileType)
                {
                    case FileTypeAnimation animation:
                    case FileTypeAudio audio:
                    case FileTypeDocument document:
                    case FileTypeNone none:
                    case FileTypePhoto photo:
                    case FileTypeUnknown unknown:
                    case FileTypeVideo video:
                    case FileTypeVideoNote videoNote:
                    case FileTypeVoiceNote voiceNote:
                        List.SelectedItems.Add(fileType);
                        break;
                }
            }
        }

        public IList<FileType> SelectedItems { get; private set; }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var fileType = args.Item as StorageStatisticsByFileType;

            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;

            switch (fileType.FileType)
            {
                case FileTypeAnimation animation:
                    title.Text = string.Format("{0} {1}", fileType.Count, "GIFs");
                    break;
                case FileTypeAudio audio:
                    title.Text = string.Format("{0} {1}", fileType.Count, "music files");
                    break;
                case FileTypeDocument document:
                    title.Text = string.Format("{0} {1}", fileType.Count, "files");
                    break;
                case FileTypeNone none:
                    title.Text = string.Format("{0} {1}", fileType.Count, "none");
                    break;
                case FileTypePhoto photo:
                    title.Text = string.Format("{0} {1}", fileType.Count, "photos");
                    break;
                case FileTypeProfilePhoto profilePhoto:
                    title.Text = string.Format("{0} {1}", fileType.Count, "profile photos");
                    break;
                case FileTypeSecret secret:
                    title.Text = string.Format("{0} {1}", fileType.Count, "secret");
                    break;
                case FileTypeSecretThumbnail secret:
                    title.Text = string.Format("{0} {1}", fileType.Count, "secret thumbnails");
                    break;
                case FileTypeSticker stickers:
                    title.Text = string.Format("{0} {1}", fileType.Count, "stickers");
                    break;
                case FileTypeThumbnail thumbnail:
                    title.Text = string.Format("{0} {1}", fileType.Count, "thumbnails");
                    break;
                case FileTypeUnknown unknown:
                    title.Text = string.Format("{0} {1}", fileType.Count, "unknown");
                    break;
                case FileTypeVideo video:
                    title.Text = string.Format("{0} {1}", fileType.Count, "videos");
                    break;
                case FileTypeVideoNote videoNote:
                    title.Text = string.Format("{0} {1}", fileType.Count, "video messages");
                    break;
                case FileTypeVoiceNote voiceNote:
                    title.Text = string.Format("{0} {1}", fileType.Count, "voice messages");
                    break;
                case FileTypeWallpaper wallpaper:
                    title.Text = string.Format("{0} {1}", fileType.Count, "wallapers");
                    break;
                default:
                    break;
            }

            subtitle.Text = FileSizeConverter.Convert(fileType.Size);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            var items = List.ItemsSource as IList<StorageStatisticsByFileType>;
            if (items != null)
            {
                SelectedItems = List.SelectedItems.Cast<StorageStatisticsByFileType>().Select(x => x.FileType).ToList();
                _dialog.Hide(ContentDialogBaseResult.OK);
            }
            else
            {
                _dialog.Hide(ContentDialogBaseResult.Cancel);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _dialog.Hide(ContentDialogBaseResult.Cancel);
        }
    }
}
