using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Chats;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsBackgroundsPage : HostedPage
    {
        public SettingsBackgroundsViewModel ViewModel => DataContext as SettingsBackgroundsViewModel;

        public SettingsBackgroundsPage()
        {
            InitializeComponent();
            Title = Strings.Resources.ChatBackground;
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var background = List.ItemFromContainer(sender) as Background;

            if (background == null || background.Id == Constants.WallpaperLocalId)
            {
                return;
            }

            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(ViewModel.ShareCommand, background, Strings.Resources.ShareFile, new FontIcon { Glyph = Icons.Share });
            flyout.CreateFlyoutItem(ViewModel.DeleteCommand, background, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
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
                if (file.Local.IsFileExisting())
                {
                    content.Source = UriEx.ToBitmap(file.Local.Path, wallpaper.Document.Thumbnail.Width, wallpaper.Document.Thumbnail.Height);
                }
                else
                {
                    content.Source = null;
                    root.Tag = wallpaper;

                    UpdateManager.Subscribe(root, ViewModel.ProtoService, file, UpdateFile, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        ViewModel.ProtoService.DownloadFile(file.Id, 1);
                    }
                }
            }
            else if (wallpaper.Type is BackgroundTypeFill fill)
            {
                content.Opacity = 1;
                preview.Fill = fill.Fill;
                content.Source = null;
            }
        }

        private void UpdateFile(object target, File file)
        {
            var root = target as Grid;
            var wallpaper = root.Tag as Background;

            var content = root?.Children[1] as Image;
            if (content == null)
            {
                return;
            }

            var small = wallpaper.Document?.Thumbnail;
            if (small == null)
            {
                return;
            }

            content.Source = UriEx.ToBitmap(file.Local.Path, small.Width, small.Height);
        }

        private async void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Background wallpaper)
            {
                var confirm = await new BackgroundPopup(wallpaper).ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    await ViewModel.NavigatedToAsync(null, NavigationMode.Refresh, null);
                }
            }
        }
    }
}
