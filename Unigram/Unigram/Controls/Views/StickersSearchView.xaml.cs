using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class StickersSearchView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private FileContext<ViewModels.Dialogs.StickerViewModel> _stickers = new FileContext<ViewModels.Dialogs.StickerViewModel>();

        public StickersSearchView()
        {
            InitializeComponent();
        }

        private async void Stickers_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Image;

            if (args.InRecycleQueue)
            {
                content.Source = null;
                return;
            }

            var sticker = args.Item as ViewModels.Dialogs.StickerViewModel;

            if (sticker == null || sticker.Thumbnail == null)
            {
                content.Source = null;
                return;
            }

            args.ItemContainer.Tag = args.Item;
            content.Tag = args.Item;

            //if (args.Phase < 2)
            //{
            //    content.Source = null;
            //    args.RegisterUpdateCallback(Stickers_ContainerContentChanging);
            //}
            //else
            if (args.Phase == 0)
            {
                Debug.WriteLine("Loading sticker " + sticker.StickerValue.Id + " for sticker set id " + sticker.SetId);

                var file = sticker.Thumbnail.Photo;
                if (file.Local.IsDownloadingCompleted)
                {
                    content.Source = await PlaceholderHelper.GetWebpAsync(file.Local.Path);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    DownloadFile(file.Id, sticker);
                }
            }
            else
            {
                throw new System.Exception("We should be in phase 0, but we are not.");
            }

            args.Handled = true;
        }

        private void DownloadFile(int id, ViewModels.Dialogs.StickerViewModel sticker)
        {
            _stickers[id].Add(sticker);
            ViewModel.ProtoService.Send(new DownloadFile(id, 1, 0));
        }

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {

        }

        private void Stickers_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.Stickers.Find(Field.Text);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
