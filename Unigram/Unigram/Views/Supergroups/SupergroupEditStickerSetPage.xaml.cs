using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Native;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Supergroups;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditStickerSetPage : Page
    {
        public SupergroupEditStickerSetViewModel ViewModel => DataContext as SupergroupEditStickerSetViewModel;

        public SupergroupEditStickerSetPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SupergroupEditStickerSetViewModel>();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(ShortName, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                ViewModel.CheckAvailability(ShortName.Text);
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

                var file = cover.Photo;
                if (file.Local.IsDownloadingCompleted)
                {
                    if (stickerSet.IsAnimated)
                    {
                        var bitmap = PlaceholderHelper.GetLottieFrame(file.Local.Path, 0, 48, 48);
                        if (bitmap == null)
                        {
                            bitmap = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                        }

                        photo.Source = bitmap;
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

            var file = cover.Photo;
            if (file.Local.IsDownloadingCompleted)
            {
                if (stickerSet.IsAnimated)
                {
                    var bitmap = PlaceholderHelper.GetLottieFrame(file.Local.Path, 0, 48, 48);
                    if (bitmap == null)
                    {
                        bitmap = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                    }

                    photo.Source = bitmap;
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
