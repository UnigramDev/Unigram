//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Profile;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileGiftsTabPage : ProfileTabPage
    {
        public new ProfileGiftsTabViewModel ViewModel => DataContext as ProfileGiftsTabViewModel;

        public ProfileGiftsTabPage()
        {
            InitializeComponent();
        }

        private new void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
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
            var gift = ScrollingHost.ItemFromContainer(sender) as ReceivedGift;

            var flyout = new MenuFlyout();

            if (ViewModel.IsOwned && ViewModel.Collections != null)
            {
                var item = new MenuFlyoutSubItem();
                item.Text = Strings.GiftsCollectionAddToCollection;
                item.Icon = MenuFlyoutHelper.CreateIcon(Icons.FolderAdd);

                foreach (var album in ViewModel.Collections)
                {
                    //// Skip current folder from "Add to folder" list to avoid confusion
                    //if (chatList.AreTheSame(viewModel.Items.ChatList))
                    //{
                    //    continue;
                    //}

                    if (album.Id == 0)
                    {
                        continue;
                    }

                    //var icon = Icons.ParseFolder(folder.Icon);
                    //var glyph = Icons.FolderToGlyph(icon);

                    var toggle = new ToggleMenuFlyoutItem();
                    toggle.Text = album.Name;
                    toggle.Icon = MenuFlyoutHelper.CreateIcon(Icons.Folder);
                    toggle.IsChecked = gift.CollectionIds.Contains(album.Id);
                    toggle.CommandParameter = (gift, album);
                    toggle.Command = new RelayCommand<(ReceivedGift, GiftCollectionViewModel)>(ViewModel.AddGiftToCollection);

                    item.Items.Add(toggle);
                }

                if (item.Items.Count < ViewModel.ClientService.Options.GiftCollectionCountMax)
                {
                    item.CreateFlyoutSeparator();
                    //item.CreateFlyoutItem(a => { }, story, Strings.StoriesAlbumNewAlbum, Icons.FolderAdd);

                    var toggle = new ToggleMenuFlyoutItem();
                    toggle.Text = Strings.GiftsCollectionNewCollection;
                    toggle.Icon = MenuFlyoutHelper.CreateIcon(Icons.FolderAdd);
                    toggle.CommandParameter = gift;
                    toggle.Command = new RelayCommand<ReceivedGift>(ViewModel.CreateCollection);

                    item.Items.Add(toggle);
                }

                flyout.Items.Add(item);
            }

            if (gift.Gift is SentGiftUpgraded)
            {
                if (ViewModel.IsOwned)
                {
                    flyout.CreateFlyoutItem(ViewModel.PinGift, gift, gift.IsPinned ? Strings.Gift2Unpin : Strings.Gift2Pin, gift.IsPinned ? Icons.PinOff : Icons.Pin);
                }

                flyout.CreateFlyoutItem(ViewModel.CopyGift, gift, Strings.CopyLink, Icons.Link);
                flyout.CreateFlyoutItem(ViewModel.ShareGift, gift, Strings.ShareFile, Icons.Share);
            }

            if (ViewModel.IsOwned)
            {
                flyout.CreateFlyoutItem(ViewModel.ToggleGift, gift, gift.IsSaved ? Strings.Gift2HideGift : Strings.Gift2ShowGift, gift.IsSaved ? Icons.EyeOff : Icons.Eye);
            }

            if (gift.CanBeTransferred)
            {
                flyout.CreateFlyoutItem(ViewModel.TransferGift, gift, Strings.Gift2TransferOption, Icons.ArrowExit);
            }

            if (ViewModel.IsOwned && ViewModel.SelectedCollection != null && ViewModel.SelectedCollection.Id != 0)
            {
                flyout.CreateFlyoutItem(ViewModel.AddGiftToCollection, (gift, ViewModel.SelectedCollection), Strings.GiftsCollectionMenuRemoveFromCollection, Icons.FolderMove);
            }

            flyout.ShowAt(sender, args);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ReceivedGiftCell content && args.Item is ReceivedGift gift)
            {
                content.UpdateGift(ViewModel.ClientService, gift);
            }

            args.Handled = true;
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                if (e.Items[0] is ReceivedGift gift && gift.IsPinned && ViewModel.IsOwned)
                {
                    ScrollingHost.CanReorderItems = true;
                }
                else
                {
                    ScrollingHost.CanReorderItems = false;
                    e.Cancel = true;
                }
            }
            catch
            {
                ScrollingHost.CanReorderItems = false;
                e.Cancel = true;
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ScrollingHost.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is ReceivedGift gift)
            {
                var items = ViewModel.Items;
                if (items.Count == 1)
                {
                    return;
                }

                var index = items.IndexOf(gift);
                var compare = items[index > 0 ? index - 1 : index + 1];

                if (compare.IsPinned)
                {
                    ViewModel.SetPinnedItems();
                }
                else
                {
                    ViewModel.SetPinnedItem(gift);
                }
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.OpenGift(e.ClickedItem as ReceivedGift);
        }

        private void Navigation_ItemContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var collection = Navigation.ItemFromContainer(sender) as GiftCollectionViewModel;
            if (collection?.Id == 0)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (ViewModel.IsOwned)
            {
                flyout.CreateFlyoutItem(ViewModel.AddGiftsToCollection, collection, Strings.GiftsCollectionMenuAddGifts, Icons.AddCircle);
            }

            if (ViewModel.ClientService.HasActiveUsername(ViewModel.OwnerId, out _))
            {
                flyout.CreateFlyoutItem(ViewModel.ShareCollection, collection, Strings.GiftsCollectionMenuShareLink, Icons.Share);
            }

            if (ViewModel.IsOwned)
            {
                flyout.CreateFlyoutItem(ViewModel.RenameCollection, collection, Strings.GiftsCollectionMenuEditName, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.DeleteCollection, collection, Strings.GiftsCollectionMenuDeleteCollection, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(sender, args);
        }

        private void Navigation_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {

        }

        private void Navigation_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {

        }

        private void AddCollection_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CreateCollection(null);
        }

        private void AddToCollection_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddGiftsToCollection(ViewModel.SelectedCollection);
        }
    }
}
