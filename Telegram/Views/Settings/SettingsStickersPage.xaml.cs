//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsStickersPage : HostedPage
    {
        public SettingsStickersViewModel ViewModel => DataContext as SettingsStickersViewModel;

        private readonly AnimatedListHandler _handler;

        public SettingsStickersPage()
        {
            InitializeComponent();

            // TODO: this might need to change depending on context
            _handler = new AnimatedListHandler(ScrollingHost, AnimatedListType.Stickers);
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Open(e.ClickedItem as StickerSetInfo);
        }

        private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move && args.Items.Count > 0)
            {
                ViewModel.Reorder(args.Items[0] as StickerSetInfo);
            }
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                var item = new TableListViewItem();
                item.ContentTemplate = sender.ItemTemplate;
                item.Style = sender.ItemContainerStyle;
                item.ContextRequested += StickerSet_ContextRequested;
                args.ItemContainer = item;
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

            var title = content.Children[1] as TextBlock;
            title.Text = stickerSet.Title;

            var subtitle = content.Children[2] as TextBlock;
            subtitle.Text = Locale.Declension(Strings.R.Stickers, stickerSet.Size);

            var cover = stickerSet.GetThumbnail();
            if (cover == null)
            {
                return;
            }

            var file = cover.StickerValue;

            var animated = content.Children[0] as AnimatedImage;
            animated.Source = new DelayedFileSource(ViewModel.ClientService, file);

            if (file.Local.IsDownloadingCompleted)
            {
            }
            else
            {
                CompositionPathParser.ParseThumbnail(cover, out ShapeVisual visual, false);
                ElementCompositionPreview.SetElementChildVisual(content.Children[0], visual);
            }

            args.Handled = true;
        }

        #endregion

        #region Binding

        private bool IsType(StickersType x, StickersType y)
        {
            return x == y;
        }

        #endregion

        #region Context menu

        private void StickerSet_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (ViewModel.Type is not StickersType.Installed and not StickersType.Masks and not StickersType.Emoji)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var stickerSet = ScrollingHost.ItemFromContainer(element) as StickerSetInfo;

            if (stickerSet == null || stickerSet.Id == 0)
            {
                return;
            }

            if (stickerSet.IsOfficial)
            {
                flyout.CreateFlyoutItem(ViewModel.Archive, stickerSet, Strings.StickersHide, Icons.Archive);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.Archive, stickerSet, Strings.StickersHide, Icons.Archive);
                flyout.CreateFlyoutItem(ViewModel.Remove, stickerSet, Strings.StickersRemove, Icons.Delete, destructive: true);
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetShareCommand, stickerSet, Strings.StickersShare);
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetCopyCommand, stickerSet, Strings.StickersCopy);
            }

            args.ShowAt(flyout, element);
        }

        #endregion
    }
}
