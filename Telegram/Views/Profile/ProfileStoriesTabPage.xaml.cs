//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Profile;
using Telegram.ViewModels.Stories;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileStoriesTabPage : ProfileTabPage
    {
        public new ProfileStoriesTabViewModel ViewModel => DataContext as ProfileStoriesTabViewModel;

        public ProfileStoriesTabPage()
        {
            InitializeComponent();

            ElementCompositionPreview.SetIsTranslationEnabled(ManagePanel, true);
            ScrollingHost.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, OnSelectionModeChanged);
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
            var story = ScrollingHost.ItemFromContainer(sender) as StoryViewModel;
            if (story == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (story.CanBeAddedToAlbum && ViewModel.Albums != null)
            {
                var item = new MenuFlyoutSubItem();
                item.Text = Strings.StoriesAlbumAddToAlbum;
                item.Icon = MenuFlyoutHelper.CreateIcon(Icons.FolderAdd);

                foreach (var album in ViewModel.Albums)
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
                    toggle.IsChecked = story.AlbumIds.Contains(album.Id);
                    toggle.CommandParameter = (story, album);
                    toggle.Command = new RelayCommand<(StoryViewModel, StoryAlbumViewModel)>(ViewModel.AddStoryToAlbum);

                    item.Items.Add(toggle);
                }

                if (item.Items.Count < ViewModel.ClientService.Options.StoryAlbumCountMax)
                {
                    item.CreateFlyoutSeparator();
                    //item.CreateFlyoutItem(a => { }, story, Strings.StoriesAlbumNewAlbum, Icons.FolderAdd);

                    var toggle = new ToggleMenuFlyoutItem();
                    toggle.Text = Strings.StoriesAlbumNewAlbum;
                    toggle.Icon = MenuFlyoutHelper.CreateIcon(Icons.FolderAdd);
                    toggle.CommandParameter = story;
                    toggle.Command = new RelayCommand<StoryViewModel>(ViewModel.CreateAlbum);

                    item.Items.Add(toggle);
                }

                flyout.Items.Add(item);
            }

            if (story.CanToggleIsPostedToChatPage)
            {
                flyout.CreateFlyoutItem(ViewModel.ArchiveStory, story, story.IsPostedToChatPage ? Strings.Archive : Strings.Unarchive, story.IsPostedToChatPage ? Icons.Archive : Icons.Unarchive);

                if (ViewModel.IsPinned(story))
                {
                    flyout.CreateFlyoutItem(ViewModel.PinStory, story, Strings.UnpinMessage, Icons.PinOff);
                }
                else
                {
                    flyout.CreateFlyoutItem(ViewModel.PinStory, story, Strings.PinMessage, Icons.Pin);
                }
            }

            if (story.CanBeAddedToAlbum && ViewModel.SelectedAlbum != null && ViewModel.SelectedAlbum.Id != 0)
            {
                flyout.CreateFlyoutItem(ViewModel.AddStoryToAlbum, (story, ViewModel.SelectedAlbum), Strings.StoriesAlbumMenuRemoveFromAlbum, Icons.FolderMove);
            }

            if (story.CanBeDeleted)
            {
                flyout.CreateFlyoutItem(ViewModel.DeleteStory, story, Strings.Delete, Icons.Delete, destructive: true);
            }

            flyout.CreateFlyoutSeparator();

            if (flyout.Items.Count > 0)
            {
                flyout.CreateFlyoutItem(ViewModel.SelectStory, story, Strings.Select, Icons.CheckmarkCircle);
            }

            flyout.ShowAt(sender, args);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is StoryCell content && args.Item is StoryViewModel story)
            {
                AutomationProperties.SetName(args.ItemContainer, story.Content is StoryContentPhoto ? Strings.AttachPhoto : story.Content is StoryContentVideo ? Strings.AttachVideo : Strings.Story);

                content.Update(story, ViewModel.SelectedAlbum.Id == 0 && ViewModel.IsPinned(story));
                args.Handled = true;
            }
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                if (e.Items[0] is StoryViewModel story && (ViewModel.SelectedAlbum.Id != 0 || ViewModel.IsPinned(story)))
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

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is StoryViewModel story)
            {
                var items = ViewModel.Items;
                if (items.Count == 1)
                {
                    return;
                }

                if (ViewModel.SelectedAlbum.Id != 0)
                {
                    ViewModel.SelectedAlbum.ReorderStories();
                }
                else
                {
                    var index = items.IndexOf(story);
                    var compare = items[index > 0 ? index - 1 : index + 1];

                    if (ViewModel.IsPinned(compare))
                    {
                        ViewModel.SetPinnedItems();
                    }
                    else
                    {
                        ViewModel.SetPinnedItem(story);
                    }
                }
            }
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            var container = ScrollingHost.ContainerFromItem(e.ClickedItem) as SelectorItem;
            var transform = container.TransformToVisual(null);

            var point = transform.TransformPoint(new Point());
            var origin = new Rect(point.X, point.Y, container.ActualWidth, container.ActualHeight);

            ViewModel.OpenStory(e.ClickedItem as StoryViewModel, origin, GetOrigin);
        }

        private Rect GetOrigin(ActiveStoriesViewModel activeStories)
        {
            var container = ScrollingHost.ContainerFromItem(activeStories.SelectedItem) as SelectorItem;
            if (container != null)
            {
                var transform = container.TransformToVisual(null);
                var point = transform.TransformPoint(new Point());

                return new Rect(point.X, point.Y, container.ActualWidth, container.ActualHeight);
            }

            return Rect.Empty;
        }

        #region Binding

        private string ConvertSelected(int count)
        {
            return Locale.Declension(Strings.R.StoriesSelected, count);
        }

        private string ConvertToggleIcon(bool pinned)
        {
            return pinned ? Icons.StoriesPinnedOff : Icons.StoriesPinned;
        }

        private string ConvertToggleText(bool pinned, int count)
        {
            if (pinned)
            {
                return Locale.Declension(Strings.R.ArchiveStories, count);
            }

            return Strings.SaveToProfile;
        }

        private string ConvertEmptyTitle(StoryAlbumViewModel album)
        {
            if (album.Id == 0)
            {
                return ViewModel.IsPostedToChatPage ? Strings.NoPublicStoriesTitle : Strings.NoArchivedStoriesTitle;
            }

            return Strings.StoriesAlbumOrganizeTitle;
        }

        private string ConvertEmptySubtitle(StoryAlbumViewModel album)
        {
            if (album.Id == 0)
            {
                return ViewModel.IsPostedToChatPage ? Strings.NoStoriesSubtitle : Strings.NoArchivedStoriesSubtitle;
            }

            return Strings.StoriesAlbumOrganizeDescription;
        }

        private Visibility ConvertEmptyButton(StoryAlbumViewModel album)
        {
            return album.Id == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        #endregion

        #region Selection

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            ShowHideManagePanel(ScrollingHost.SelectionMode == ListViewSelectionMode.Multiple);
        }

        private bool _manageCollapsed = true;

        private void ShowHideManagePanel(bool show)
        {
            if (_manageCollapsed != show)
            {
                return;
            }

            _manageCollapsed = !show;
            ManagePanel.Visibility = Visibility.Visible;

            var manage = ElementComposition.GetElementVisual(ManagePanel);
            ElementCompositionPreview.SetIsTranslationEnabled(ManagePanel, true);
            manage.Opacity = show ? 0 : 1;

            var batch = manage.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ManagePanel.Visibility = _manageCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var offset1 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 48, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3(0, 0, 0));

            var opacity1 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);

            manage.StartAnimation("Translation", offset1);
            manage.StartAnimation("Opacity", opacity1);

            batch.End();
        }

        #endregion

        private void Navigation_ItemContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var album = Navigation.ItemFromContainer(sender) as StoryAlbumViewModel;
            if (album?.Id == 0)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (ViewModel.CanEditStories)
            {
                flyout.CreateFlyoutItem(ViewModel.AddStoriesToAlbum, album, Strings.StoriesAlbumMenuAddStories, Icons.AddCircle);
            }

            if (ViewModel.ClientService.HasActiveUsername(ViewModel.Chat, out _))
            {
                flyout.CreateFlyoutItem(ViewModel.ShareAlbum, album, Strings.StoriesAlbumMenuShareLink, Icons.Share);
            }

            if (ViewModel.CanEditStories)
            {
                flyout.CreateFlyoutItem(ViewModel.RenameAlbum, album, Strings.StoriesAlbumMenuEditName, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.DeleteAlbum, album, Strings.StoriesAlbumMenuDeleteAlbum, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(sender, args);
        }

        private void Navigation_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            var list = sender as TopNavView;
            if (list == null)
            {
                return;
            }

            if (e.Items.Count > 1)
            {
                list.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                var items = ViewModel?.Albums;
                if (items == null || items.Count < 2 || e.Items[0] is StoryAlbumViewModel { Id: not 0 })
                {
                    list.CanReorderItems = false;
                    e.Cancel = true;
                }
                else
                {
                    list.CanReorderItems = true;
                }
            }

        }

        private void Navigation_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            sender.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is StoryAlbumViewModel album)
            {
                var items = ViewModel?.Albums;
                var index = items.IndexOf(album);

                var compare = items[index > 0 ? index - 1 : index + 1];
                if (compare.Id == 0 && index > 0 && index < items.Count - 1)
                {
                    compare = items[index + 1];
                }

                if (compare.Id == 0 || album.Id == 0)
                {
                    //ViewModel.Handle(new UpdateChatFolders(ViewModel.ClientService.ChatFolders, 0, false));

                    //ToastPopup.ShowPromo(ViewModel.NavigationService, string.Format(Strings.LimitReachedReorderFolder, Strings.FilterAllChats), Strings.PremiumMore, new PremiumSourceLimitExceeded(new PremiumLimitTypeChatFolderCount()));
                }
                else
                {
                    var albums = items.Where(x => x.Id != 0).Select(x => x.Id).ToArray();

                    ViewModel.ClientService.Send(new ReorderStoryAlbums(ViewModel.Chat.Id, albums));
                }
            }
        }

        private void AddAlbum_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CreateAlbum(null);
        }

        private void AddToAlbum_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddStoriesToAlbum(ViewModel.SelectedAlbum);
        }

        private void Hint_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                textBlock.Text = ViewModel.Chat.Type is ChatTypePrivate
                    ? Strings.ProfileStoriesArchiveHint
                    : Strings.ProfileStoriesArchiveChannelHint;
            }
        }
    }
}
