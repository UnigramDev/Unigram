using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsBackgroundsPage : Page, IHandle<UpdateFile>
    {
        public SettingsBackgroundsViewModel ViewModel => DataContext as SettingsBackgroundsViewModel;

        public SettingsBackgroundsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsBackgroundsViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Subscribe(this);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Unsubscribe(this);
        }

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var wallpaper = args.Item as Background;
            var root = args.ItemContainer.ContentTemplateRoot as Grid;

            var check = root.Children[1] as UIElement;
            check.Visibility = wallpaper.Id == ViewModel.SelectedItem?.Id ? Visibility.Visible : Visibility.Collapsed;

            if (wallpaper.Id == 1000001)
            {
                return;
            }
            else if (wallpaper.Id == Constants.WallpaperLocalId)
            {
                //var content = root.Children[0] as Image;
                //content.Source = new BitmapImage(new Uri($"ms-appdata:///local/{ViewModel.SessionId}/{Constants.WallpaperLocalFileName}"));

                var file = await ApplicationData.Current.LocalFolder.GetFileAsync($"{ViewModel.SessionId}\\{Constants.WallpaperLocalFileName}");
                using (var stream = await file.OpenReadAsync())
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);

                    var content = root.Children[0] as Image;
                    content.Source = bitmap;
                }
            }
            else if (wallpaper.Document != null)
            {
                var small = wallpaper.Document.Thumbnail;
                if (small == null)
                {
                    return;
                }

                var content = root.Children[0] as Image;
                content.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.Photo, wallpaper.Document.Thumbnail.Width, wallpaper.Document.Thumbnail.Height);     
                
                if (wallpaper.Type is BackgroundTypePattern pattern)
                {
                    content.Opacity = pattern.Intensity / 100d;
                    root.Background = pattern.Fill.ToBrush();
                }
                else
                {
                    content.Opacity = 1;
                    root.Background = null;
                }
            }
            else if (wallpaper.Type is BackgroundTypeFill fill)
            {
                var content = root.Children[0] as Rectangle;
                content.Fill = fill.ToBrush();
            }
        }

        public void Handle(UpdateFile update)
        {
            this.BeginOnUIThread(() =>
            {
                foreach (var item in ViewModel.Items)
                {
                    if (item.UpdateFile(update.File))
                    {
                        var container = List.ContainerFromItem(item) as SelectorItem;
                        if (container == null)
                        {
                            continue;
                        }

                        var root = container.ContentTemplateRoot as Grid;
                        if (root == null)
                        {
                            continue;
                        }

                        var content = root.Children[0] as Image;
                        if (content == null)
                        {
                            return;
                        }

                        var small = item.Document?.Thumbnail;
                        if (small == null)
                        {
                            return;
                        }

                        content.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.Photo, item.Document.Thumbnail.Width, item.Document.Thumbnail.Height);
                    }
                }
            });
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Background wallpaper)
            {
                ViewModel.NavigationService.Navigate(typeof(BackgroundPage), TdBackground.ToString(wallpaper));
            }
        }
    }
}
