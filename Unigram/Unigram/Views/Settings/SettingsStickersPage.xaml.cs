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
using System.Diagnostics;
using TdWindows;
using Windows.UI.Xaml.Media.Imaging;
using Unigram.Common;
using Unigram.Native;
using Windows.Storage;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsStickersPage : Page
    {
        public SettingsStickersViewModel ViewModel => DataContext as SettingsStickersViewModel;

        public SettingsStickersPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsStickersViewModel>();
        }

        private void FeaturedStickers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStickersFeaturedPage));
        }

        private void ArchivedStickers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStickersArchivedPage));
        }

        private void Masks_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsMasksPage));
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerSetInfo stickerSet)
            {
                await StickerSetView.GetForCurrentView().ShowAsync(stickerSet.Id);
            }
        }

        private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move)
            {
                ViewModel.ReorderCommand.Execute(args.Items.FirstOrDefault());
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

                var cover = stickerSet.Covers.FirstOrDefault();
                if (cover == null || cover.Thumbnail == null)
                {
                    return;
                }

                var file = cover.Thumbnail.Photo;
                if (file.Local.IsDownloadingCompleted)
                {
                    var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    var buffer = await FileIO.ReadBufferAsync(temp);

                    photo.Source = WebPImage.DecodeFromBuffer(buffer);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    photo.Source = null;
                    ViewModel.ProtoService.Send(new DownloadFile(file.Id, 1));
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
