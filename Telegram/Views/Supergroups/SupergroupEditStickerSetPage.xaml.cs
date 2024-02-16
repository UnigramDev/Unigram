//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Supergroups.Popups
{
    public sealed partial class SupergroupEditStickerSetPopup : ContentPopup
    {
        public SupergroupEditStickerSetViewModel ViewModel => DataContext as SupergroupEditStickerSetViewModel;

        private readonly TaskCompletionSource<object> _tsc;

        public SupergroupEditStickerSetPopup(TaskCompletionSource<object> tsc)
        {
            InitializeComponent();
            Title = Strings.GroupStickers;

            _tsc = tsc;

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var stickerSet = args.Item as StickerSetInfo;

            var title = content.Children[2] as TextBlock;
            title.Text = stickerSet.Title;

            var subtitle = content.Children[3] as TextBlock;
            subtitle.Text = Locale.Declension(Strings.R.Stickers, stickerSet.Size);

            var animated = content.Children[1] as AnimatedImage;
            var cross = content.Children[0];

            var cover = stickerSet.GetThumbnail();
            if (cover == null)
            {
                animated.Source = null;
                cross.Visibility = Visibility.Visible;
                title.Margin = new Thickness(0, 8, 0, -8);
                subtitle.Text = string.Empty;
            }
            else
            {
                animated.Source = new DelayedFileSource(ViewModel.ClientService, cover);
                cross.Visibility = Visibility.Collapsed;
                title.Margin = new Thickness();
                subtitle.Text = Locale.Declension(Strings.R.Stickers, stickerSet.Size);
            }

            args.Handled = true;
        }

        private void Grid_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var content = sender as Grid;
            var stickerSet = args.NewValue as StickerSetInfo;

            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;
            var photo = content.Children[0] as Image;

            if (stickerSet == null)
            {
                title.Text = Strings.ChooseStickerSetNotFound;
                subtitle.Text = Strings.ChooseStickerSetNotFoundInfo;
                photo.Source = null;
                return;
            }

            title.Text = stickerSet.Title;
            subtitle.Text = Locale.Declension(Strings.R.Stickers, stickerSet.Size);

            var cover = stickerSet.Thumbnail ?? stickerSet.Covers.FirstOrDefault()?.Thumbnail;
            if (cover == null)
            {
                return;
            }

            var file = cover.File;
            if (file.Local.IsDownloadingCompleted)
            {
                if (cover.Format is ThumbnailFormatTgs)
                {
                    photo.Source = PlaceholderHelper.GetLottieFrame(file.Local.Path, 0, 48, 48);
                }
                else if (cover.Format is ThumbnailFormatWebp)
                {
                    photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path, 48);
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                photo.Source = null;
                ViewModel.ClientService.DownloadFile(file.Id, 1);
            }
        }

        #endregion

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = ScrollingHost.SelectedItem != null;
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            _tsc.TrySetResult(ScrollingHost.SelectedItem);
        }
    }
}
