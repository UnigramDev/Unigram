using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Chats;
using Unigram.Services;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsBackgroundsPage : HostedPage, IHandle<UpdateFile>
    {
        public SettingsBackgroundsViewModel ViewModel => DataContext as SettingsBackgroundsViewModel;

        private readonly FileContext<Background> _backgrounds = new FileContext<Background>();

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

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var wallpaper = args.Item as Background;
            var root = args.ItemContainer.ContentTemplateRoot as Grid;

            var preview = root.Children[0] as ChatBackgroundPreview;
            var content = root.Children[1] as Image;
            var check = root.Children[2];

            check.Visibility = wallpaper == ViewModel.SelectedItem ? Visibility.Visible : Visibility.Collapsed;

            if (wallpaper.Document != null)
            {
                if (wallpaper.Type is BackgroundTypePattern pattern)
                {
                    content.Opacity = pattern.Intensity / 100d;
                    preview.Fill = pattern.Fill;
                }
                else
                {
                    content.Opacity = 1;
                    preview.Fill = null;
                }

                var small = wallpaper.Document.Thumbnail;
                if (small == null)
                {
                    return;
                }

                var file = small.File;
                if (file.Local.IsDownloadingCompleted)
                {
                    content.Source = UriEx.ToBitmap(file.Local.Path, wallpaper.Document.Thumbnail.Width, wallpaper.Document.Thumbnail.Height);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    _backgrounds[file.Id].Add(wallpaper);
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }
            }
            else if (wallpaper.Type is BackgroundTypeFill fill)
            {
                content.Opacity = 1;
                preview.Fill = fill.Fill;
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

                        var small = item.Document?.Thumbnail;
                        if (small == null)
                        {
                            continue;
                        }

                        var container = List.ContainerFromItem(item) as SelectorItem;
                        var root = container?.ContentTemplateRoot as Grid;
                        var content = root?.Children[1] as Image;

                        if (content == null)
                        {
                            continue;
                        }

                        content.Source = UriEx.ToBitmap(file.Local.Path, small.Width, small.Height);
                    }
                });
            }
        }

        private async void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Background wallpaper)
            {
                await new BackgroundPopup(wallpaper).ShowQueuedAsync();
            }
        }
    }
}
