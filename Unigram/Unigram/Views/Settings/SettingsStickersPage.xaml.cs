using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services.Settings;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsStickersPage : HostedPage
    {
        public SettingsStickersViewModel ViewModel => DataContext as SettingsStickersViewModel;

        public SettingsStickersPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsStickersViewModel>();
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

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.StickerSetOpenCommand.Execute(e.ClickedItem);
        }

        private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move)
            {
                ViewModel.ReorderCommand.Execute(args.Items.FirstOrDefault());
            }
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += StickerSet_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var stickerSet = args.Item as StickerSetInfo;

            content.Tag = stickerSet;

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

        #endregion

        #region Binding

        private string ConvertSuggest(StickersSuggestionMode mode)
        {
            switch (mode)
            {
                case StickersSuggestionMode.All:
                    return Strings.Resources.SuggestStickersAll;
                case StickersSuggestionMode.Installed:
                    return Strings.Resources.SuggestStickersInstalled;
                case StickersSuggestionMode.None:
                    return Strings.Resources.SuggestStickersNone;
            }

            return null;
        }

        #endregion

        #region Context menu

        private void StickerSet_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var stickerSet = List.ItemFromContainer(element) as StickerSetInfo;

            if (stickerSet == null || stickerSet.Id == 0)
            {
                return;
            }

            if (stickerSet.IsOfficial)
            {
                flyout.CreateFlyoutItem(ViewModel.StickerSetHideCommand, stickerSet, Strings.Resources.StickersHide, new FontIcon { Glyph = Icons.Archive });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.StickerSetHideCommand, stickerSet, Strings.Resources.StickersHide, new FontIcon { Glyph = Icons.Archive });
                flyout.CreateFlyoutItem(ViewModel.StickerSetRemoveCommand, stickerSet, Strings.Resources.StickersRemove, new FontIcon { Glyph = Icons.Delete });
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetShareCommand, stickerSet, Strings.Resources.StickersShare);
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetCopyCommand, stickerSet, Strings.Resources.StickersCopy);
            }

            args.ShowAt(flyout, element);
        }

        #endregion

    }
}
