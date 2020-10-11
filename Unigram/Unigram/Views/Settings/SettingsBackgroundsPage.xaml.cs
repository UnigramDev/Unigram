using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Settings;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsBackgroundsPage : HostedPage, IHandle<UpdateFile>
    {
        public SettingsBackgroundsViewModel ViewModel => DataContext as SettingsBackgroundsViewModel;

        private FileContext<Background> _backgrounds = new FileContext<Background>();

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
            else if (wallpaper.Id == Constants.WallpaperLocalId && StorageApplicationPermissions.FutureAccessList.ContainsItem(wallpaper.Name))
            {
                //var content = root.Children[0] as Image;
                //content.Source = new BitmapImage(new Uri($"ms-appdata:///local/{ViewModel.SessionId}/{Constants.WallpaperLocalFileName}"));

                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(wallpaper.Name);
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
                var file = small.File;
                if (file.Local.IsDownloadingCompleted)
                {
                    content.Source = new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = wallpaper.Document.Thumbnail.Width, DecodePixelHeight = wallpaper.Document.Thumbnail.Height, DecodePixelType = DecodePixelType.Logical };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    _backgrounds[file.Id].Add(wallpaper);
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }

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
            var file = update.File;
            if (!file.Local.IsDownloadingCompleted)
            {
                return;
            }

            if (_backgrounds.TryGetValue(update.File.Id, out List<Background> items))
            {
                this.BeginOnUIThread(() =>
                {
                    foreach (var item in items)
                    {
                        item.UpdateFile(update.File);

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
                            continue;
                        }

                        var small = item.Document?.Thumbnail;
                        if (small == null)
                        {
                            continue;
                        }

                        content.Source = new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = small.Width, DecodePixelHeight = small.Height, DecodePixelType = DecodePixelType.Logical };
                    }
                });
            }
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
