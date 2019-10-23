using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Controls.Views;
using Unigram.Views;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.Storage;
using Unigram.Native;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsMasksArchivedPage : Page
    {
        public SettingsMasksArchivedViewModel ViewModel => DataContext as SettingsMasksArchivedViewModel;

        public SettingsMasksArchivedPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsMasksArchivedViewModel>();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerSetInfo stickerSet)
            {
                await StickerSetView.GetForCurrentView().ShowAsync(stickerSet.Id);
            }
        }

        #region Recycle

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
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
                            bitmap = await PlaceholderHelper.GetWebpAsync(file.Local.Path);
                        }

                        photo.Source = bitmap;
                    }
                    else
                    {
                        photo.Source = await PlaceholderHelper.GetWebpAsync(file.Local.Path);
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

        #endregion

    }
}
