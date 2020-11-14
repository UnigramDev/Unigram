using System;
using System.Linq;
using System.Reactive.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditStickerSetPage : HostedPage
    {
        public SupergroupEditStickerSetViewModel ViewModel => DataContext as SupergroupEditStickerSetViewModel;

        public SupergroupEditStickerSetPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SupergroupEditStickerSetViewModel>();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(ShortName, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                ViewModel.CheckAvailability(ShortName.Value);
            });
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

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = stickerSet.Title;
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = Locale.Declension("Stickers", stickerSet.Size);
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as Image;

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
                    else
                    {
                        photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                    }
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

        private void Grid_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var content = sender as Grid;
            var stickerSet = args.NewValue as StickerSetInfo;

            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;
            var photo = content.Children[0] as Image;

            if (stickerSet == null)
            {
                title.Text = Strings.Resources.ChooseStickerSetNotFound;
                subtitle.Text = Strings.Resources.ChooseStickerSetNotFoundInfo;
                photo.Source = null;
                return;
            }

            title.Text = stickerSet.Title;
            subtitle.Text = Locale.Declension("Stickers", stickerSet.Size);

            var cover = stickerSet.Thumbnail ?? stickerSet.Covers.FirstOrDefault()?.Thumbnail;
            if (cover == null)
            {
                return;
            }

            var file = cover.File;
            if (file.Local.IsDownloadingCompleted)
            {
                if (stickerSet.IsAnimated)
                {
                    photo.Source = PlaceholderHelper.GetLottieFrame(file.Local.Path, 0, 48, 48);
                }
                else
                {
                    photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                photo.Source = null;
                ViewModel.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        #endregion

    }
}
