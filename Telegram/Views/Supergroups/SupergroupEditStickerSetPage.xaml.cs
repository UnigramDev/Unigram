//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupEditStickerSetPage : HostedPage
    {
        public SupergroupEditStickerSetViewModel ViewModel => DataContext as SupergroupEditStickerSetViewModel;

        private readonly AnimatedListHandler _handler;

        public SupergroupEditStickerSetPage()
        {
            InitializeComponent();
            Title = Strings.GroupStickers;

            _handler = new AnimatedListHandler(ScrollingHost, AnimatedListType.Stickers);

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => ShortName.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += (s, args) =>
            {
                ViewModel.CheckAvailability(ShortName.Value);
            };
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

            var title = content.Children[1] as TextBlock;
            title.Text = stickerSet.Title;

            var subtitle = content.Children[2] as TextBlock;
            subtitle.Text = Locale.Declension(Strings.R.Stickers, stickerSet.Size);

            var cover = stickerSet.GetThumbnail();
            if (cover == null)
            {
                return;
            }

            var animated = content.Children[0] as AnimatedImage;
            animated.Source = new DelayedFileSource(ViewModel.ClientService, cover);

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

    }
}
