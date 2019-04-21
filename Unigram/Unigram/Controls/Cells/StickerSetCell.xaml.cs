using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Dialogs;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Cells
{
    public sealed partial class StickerSetCell : StackPanel
    {
        private CancellationTokenSource _loadToken;

        public StickerSetCell()
        {
            this.InitializeComponent();
        }

        public async void Update(IProtoService protoService, StickerSetViewModel stickerSet)
        {
            if (_loadToken != null)
            {
                _loadToken.Cancel();
            }

            Title.Text = stickerSet.Title;
            List.ItemsSource = stickerSet.Stickers;

            if (stickerSet.IsLoaded)
            {
                return;
            }

            _loadToken = new CancellationTokenSource();

            var token = _loadToken.Token;
            var response = await protoService.SendAsync(new GetStickerSet(stickerSet.Id));
            if (response is StickerSet full)
            {
                stickerSet.Update(full);
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            //List.ItemsSource = null;
            //List.ItemsSource = stickerSet.Stickers;
        }

        private async void List_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Image;

            if (args.InRecycleQueue)
            {
                content.Source = null;
                return;
            }

            var sticker = args.Item as StickerViewModel;
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
                    //DownloadFile(file.Id, sticker);
                }
            }
            else
            {
                throw new System.Exception("We should be in phase 0, but we are not.");
            }

            args.Handled = true;
        }
    }
}
