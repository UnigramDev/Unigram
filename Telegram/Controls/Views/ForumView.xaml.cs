//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Views
{
    public enum ForumViewType
    {
        List,
        Vertical,
        Horizontal
    }

    public partial record ForumViewItemClickEventArgs(object ClickedItem, bool FromSelection);

    public sealed partial class ForumView : UserControl, ITopicListDelegate, IAutomationNameProvider
    {
        public TopicListViewModel ViewModel
        {
            get => DataContext as TopicListViewModel;
            set
            {
                DataContext = value;
                ScrollingHost.ItemsSource = value?.Items;
                ScrollingHost.SelectedItem = value?.Items.GetItem(value?.SelectedItem);
            }
        }

        public ForumView()
        {
            InitializeComponent();
        }

        private ForumViewType _type;

        public void UpdateType(ForumViewType type)
        {
            _type = type;

            ScrollingHost.ItemTemplate = type switch
            {
                ForumViewType.Vertical => VerticalTemplate,
                ForumViewType.Horizontal => HorizontalTemplate,
                _ => ListTemplate
            };

            ScrollingHost.ItemsPanel = type switch
            {
                ForumViewType.Vertical => VerticalPanelTemplate,
                ForumViewType.Horizontal => HorizontalPanelTemplate,
                _ => ListPanelTemplate
            };

            ScrollingHost.Orientation = type == ForumViewType.Horizontal ? Orientation.Horizontal : Orientation.Vertical;

            if (type == ForumViewType.Vertical)
            {
                ScrollingHost.Margin = new Thickness(0, 40, 0, 0);

                BackgroundRoot.Visibility = Visibility.Collapsed;
                VerticalBackgroundRoot.Visibility = Visibility.Visible;
                Header.Visibility = Visibility.Collapsed;

                Width = 72;
            }
            else if (type == ForumViewType.Horizontal)
            {
                ScrollingHost.Margin = new Thickness(72, 0, 0, 0);

                BackgroundRoot.Background = null;
                BackgroundRoot.CornerRadius = new CornerRadius(0);
                BackgroundRoot.BorderThickness = new Thickness(0, 0, 0, 1);
                Header.Visibility = Visibility.Collapsed;

                Height = 40;
            }

            ScrollViewer.SetHorizontalScrollBarVisibility(ScrollingHost, type == ForumViewType.Horizontal ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled);
            ScrollViewer.SetHorizontalScrollMode(ScrollingHost, type == ForumViewType.Horizontal ? ScrollMode.Auto : ScrollMode.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(ScrollingHost, type == ForumViewType.Vertical ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollMode(ScrollingHost, type == ForumViewType.Horizontal ? ScrollMode.Disabled : ScrollMode.Auto);
        }

        public void AnimateWidth(bool collapse, TimeSpan duration)
        {
            var visual = ElementComposition.GetElementVisual(BackgroundRoot);
            visual.Clip ??= visual.Compositor.CreateInsetClip();

            var inset = _type == ForumViewType.Horizontal ? 72 : 40;
            var property = _type == ForumViewType.Horizontal ? "LeftInset" : "TopInset";

            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(collapse ? 1 : 0, inset);
            animation.InsertKeyFrame(collapse ? 0 : 1, 0);
            animation.Duration = duration;

            visual.Clip.StartAnimation(property, animation);
        }

        public void Scroll(int offset, bool navigate)
        {
            int index;
            if (offset == int.MaxValue)
            {
                index = ViewModel.Items.Count - 1;
            }
            else if (offset == int.MinValue)
            {
                index = 0;
            }
            else
            {
                index = ScrollingHost.SelectedIndex + offset;
            }

            if (index >= 0 && index < ViewModel.Items.Count)
            {
                if (navigate)
                {
                    ItemClick?.Invoke(this, new ForumViewItemClickEventArgs(ViewModel.Items[index], false));
                }
            }
            else if (index < 0 && offset == -1 && !navigate)
            {
                Search_Click(null, null);
            }
        }

        public void UpdateChat(Chat chat)
        {
            UpdateChatTitle(chat);
            UpdateChatEmojiStatus(chat);

            if (ViewModel.ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
            {
                Subtitle.Text = Locale.Declension(Strings.R.Members, fullInfo.MemberCount);
            }
            else if (ViewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                Subtitle.Text = Locale.Declension(Strings.R.Members, supergroup.MemberCount);
            }
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (chat != null)
            {
                Title.Text = ViewModel.ClientService.GetTitle(chat);
            }
        }

        public void UpdateChatEmojiStatus(Chat chat)
        {
            if (chat != null)
            {
                Identity.SetStatus(ViewModel.ClientService, chat, BotVerified);
            }
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(ProfilePage), ViewModel.Chat.Id);
        }

        private void Options_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Profile.Padding = new Thickness(52, 0, e.NewSize.Width, 0);
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(Search, Strings.Search, Icons.Search, VirtualKey.E);
            flyout.CreateFlyoutSeparator();

            flyout.CreateFlyoutItem(ViewModel.ViewAsMessages, Strings.TopicViewAsMessages, Icons.ChatEmpty);

            if (ViewModel.Chat.CanCreateTopics(ViewModel.ClientService))
            {
                flyout.CreateFlyoutItem(ViewModel.CreateTopic, Strings.CreateTopic, Icons.Compose);
            }

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void Search()
        {
            // TODO: Use event instead
            this.GetParent<MainPage>()?.Search();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Use event instead
            this.GetParent<MainPage>()?.Search();
        }



        #region Recycle

        private readonly Dictionary<long, SelectorItem> _itemToSelector = new();

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                if (_type == ForumViewType.Vertical)
                {
                    args.ItemContainer = new TopNavViewItem
                    {
                        Style = VerticalListViewItemStyle,
                        MinWidth = 0,
                        MinHeight = 0
                    };
                }
                else if (_type == ForumViewType.Horizontal)
                {
                    args.ItemContainer = new TopNavViewItem
                    {
                        MinWidth = 0,
                        MinHeight = 0
                    };
                }
                else
                {
                    args.ItemContainer = new ChatListListViewItem(null)
                    {
                        MinWidth = 0,
                        MinHeight = 0
                    };
                }

                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Topic_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var forumTopic = args.Item as ForumTopic;
            var directMessagesChatTopic = args.Item as DirectMessagesChatTopic;

            var topicId = forumTopic?.Info.ForumTopicId ?? directMessagesChatTopic.Id;

            if (args.InRecycleQueue)
            {
                _itemToSelector.Remove(topicId);
                return;
            }

            _itemToSelector[topicId] = args.ItemContainer;

            if (args.ItemContainer.ContentTemplateRoot is ForumTopicVerticalCell vertical)
            {
                vertical.UpdateLayout(_type == ForumViewType.Vertical);
            }

            if (forumTopic != null && args.ItemContainer.ContentTemplateRoot is IForumTopicDelegate forumTopicCell)
            {
                forumTopicCell.UpdateForumTopic(ViewModel, forumTopic);
            }
            else if (directMessagesChatTopic != null && args.ItemContainer.ContentTemplateRoot is IDirectMessagesTopicDelegate feedbackTopicCell)
            {
                feedbackTopicCell.UpdateDirectMessagesChatTopic(ViewModel, directMessagesChatTopic);
            }

            args.Handled = true;
        }

        public bool TryGetContainer(long messageThreadId, out SelectorItem container)
        {
            return _itemToSelector.TryGetValue(messageThreadId, out container);
        }

        private bool TryGetTopicAndCell(long messageThreadId, out ForumTopic topic, out IForumTopicDelegate cell)
        {
            if (_itemToSelector.TryGetValue(messageThreadId, out SelectorItem container))
            {
                topic = ScrollingHost.ItemFromContainer(container) as ForumTopic;
                cell = container.ContentTemplateRoot as IForumTopicDelegate;
                return topic != null && cell != null;
            }

            topic = null;
            cell = null;
            return false;
        }

        private bool TryGetTopicAndCell(long topicId, out DirectMessagesChatTopic topic, out IDirectMessagesTopicDelegate cell)
        {
            if (_itemToSelector.TryGetValue(topicId, out SelectorItem container))
            {
                topic = ScrollingHost.ItemFromContainer(container) as DirectMessagesChatTopic;
                cell = container.ContentTemplateRoot as IDirectMessagesTopicDelegate;
                return topic != null && cell != null;
            }

            topic = null;
            cell = null;
            return false;
        }

        private bool TryGetCell(ForumTopic topic, out IForumTopicDelegate cell)
        {
            if (_itemToSelector.TryGetValue(topic.Info.ForumTopicId, out SelectorItem container))
            {
                cell = container.ContentTemplateRoot as IForumTopicDelegate;
                return cell != null;
            }

            cell = null;
            return false;
        }

        private bool TryGetCell(DirectMessagesChatTopic topic, out IDirectMessagesTopicDelegate cell)
        {
            if (_itemToSelector.TryGetValue(topic.Id, out SelectorItem container))
            {
                cell = container.ContentTemplateRoot as IDirectMessagesTopicDelegate;
                return cell != null;
            }

            cell = null;
            return false;
        }

        #region ForumTopic

        public void UpdateForumTopicLastMessage(ForumTopic topic)
        {
            HandleForumTopic(topic, (chatView, chat) =>
            {
                chatView.UpdateForumTopicReadInbox(chat);
                chatView.UpdateForumTopicLastMessage(chat);
            });
        }

        public void HandleForumTopic(int forumTopicId, Action<IForumTopicDelegate, ForumTopic> action)
        {
            if (TryGetTopicAndCell(forumTopicId, out ForumTopic chat, out IForumTopicDelegate cell))
            {
                action(cell, chat);
            }
        }

        public void HandleForumTopic(ForumTopic topic, Action<IForumTopicDelegate, ForumTopic> action)
        {
            if (TryGetCell(topic, out IForumTopicDelegate cell))
            {
                action(cell, topic);
            }
        }

        #endregion

        #region DirectMessagesChatTopic

        public void UpdateDirectMessagesChatTopicLastMessage(DirectMessagesChatTopic topic)
        {
            HandleDirectMessagesChatTopic(topic, (chatView, chat) =>
            {
                chatView.UpdateDirectMessagesChatTopicReadInbox(chat);
                chatView.UpdateDirectMessagesChatTopicLastMessage(chat);
            });
        }

        public void HandleDirectMessagesChatTopic(long topicId, Action<IDirectMessagesTopicDelegate, DirectMessagesChatTopic> action)
        {
            if (TryGetTopicAndCell(topicId, out DirectMessagesChatTopic chat, out IDirectMessagesTopicDelegate cell))
            {
                action(cell, chat);
            }
        }

        public void HandleDirectMessagesChatTopic(DirectMessagesChatTopic topic, Action<IDirectMessagesTopicDelegate, DirectMessagesChatTopic> action)
        {
            if (TryGetCell(topic, out IDirectMessagesTopicDelegate cell))
            {
                action(cell, topic);
            }
        }

        #endregion

        public async void SetSelectedItem(object topic)
        {
            await System.Threading.Tasks.Task.Delay(100);

            if (ViewModel?.SelectionMode != ListViewSelectionMode.Multiple)
            {
                try
                {
                    ScrollingHost.SelectedItem = topic;

                    // TODO: would be great, but doesn't seem to work well enough :(
                    //VisualUtilities.QueueCallbackForCompositionRendered(() => ChatsList.SelectedItem = chat);
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        public void SetSelectedItems(IList<object> topics)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.Multiple)
            {
                try
                {
                    foreach (var item in topics)
                    {
                        if (!ScrollingHost.SelectedItems.Contains(item))
                        {
                            ScrollingHost.SelectedItems.Add(item);
                        }
                    }

                    foreach (ForumTopic item in ScrollingHost.SelectedItems)
                    {
                        if (!topics.Contains(item))
                        {
                            ScrollingHost.SelectedItems.Remove(item);
                        }
                    }
                }
                catch
                {
                    // SelectedItems likes to throw
                }
            }
        }

        #endregion

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                if (e.Items[0] is ForumTopic topic && ViewModel.Chat.CanManageTopics(ViewModel.ClientService))
                {
                    if (!topic.IsPinned || e.Items.Count > 1 || ScrollingHost.SelectionMode == ListViewSelectionMode.Multiple)
                    {
                        ScrollingHost.CanReorderItems = false;
                        e.Cancel = true;
                    }
                    else
                    {
                        ScrollingHost.CanReorderItems = true;
                    }
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

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is ForumTopic topic)
            {
                var items = ViewModel.Items as TopicListViewModel.ForumTopicsCollection;
                if (items.Count == 1)
                {
                    return;
                }

                var first = _type == ForumViewType.List ? 0 : 1;

                var index = items.IndexOf(topic);
                var compare = items[index > first ? index - 1 : index + 1];

                if (compare.IsPinned)
                {
                    var pinned = items.Where(x => x.IsPinned).Select(x => x.Info.ForumTopicId).ToArray();

                    ViewModel.ClientService.SetPinnedForumTopics(ViewModel.Chat.Id, pinned);
                }
                else
                {
                    items.Handle(topic.Info.ForumTopicId, topic.Order);
                }
            }
        }

        #region Context menu

        private void Topic_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            var chat = viewModel?.Chat;

            if (viewModel == null || chat == null)
            {
                return;
            }

            var canManage = false;
            if (viewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                canManage = supergroup.CanManageTopics();
            }
            else if (chat.Type is ChatTypePrivate)
            {
                canManage = true;
            }

            var flyout = new MenuFlyout();

            var topic = ScrollingHost.ItemFromContainer(sender);
            if (topic is ForumTopic forumTopic)
            {
                if (canManage)
                {
                    //Telegram.Td.Api.ToggleForumTopicIsPinned // CanManageTopics
                    flyout.CreateFlyoutItem(viewModel.PinTopic, forumTopic, forumTopic.IsPinned ? Strings.UnpinFromTop : Strings.PinToTop, forumTopic.IsPinned ? Icons.PinOff : Icons.Pin);
                }

                var muted = ViewModel.ClientService.Notifications.IsMuted(chat, forumTopic);
                flyout.CreateFlyoutItem(viewModel.NotifyTopic, forumTopic, muted ? Strings.Unmute : Strings.Mute, forumTopic.IsPinned ? Icons.Alert : Icons.AlertOff);

                if (canManage && chat.Type is ChatTypeSupergroup)
                {
                    //Telegram.Td.Api.ToggleGeneralForumTopicIsHidden // CanManageTopics
                    //Telegram.Td.Api.ToggleForumTopicIsClosed // CanManageTopics
                    flyout.CreateFlyoutItem(viewModel.CloseTopic, forumTopic, forumTopic.Info.IsClosed ? Strings.RestartTopic : Strings.CloseTopic, forumTopic.Info.IsClosed ? Icons.PlayCircle : Icons.HandRight);
                }

                if (forumTopic.UnreadCount > 0)
                {
                    flyout.CreateFlyoutItem(viewModel.MarkTopicAsRead, forumTopic, Strings.MarkAsRead, Icons.MarkAsRead);
                }

                if (canManage)
                {
                    //     Deletes all messages in a forum topic; requires CanDeleteMessages administrator
                    //     right in the supergroup unless the user is creator of the topic, the topic has
                    //     no messages from other users and has at most 11 messages.
                    flyout.CreateFlyoutItem(viewModel.DeleteTopic, forumTopic, Strings.Delete, Icons.Delete, destructive: true);
                }

                if (viewModel.SelectionMode != ListViewSelectionMode.Multiple && chat.Type is ChatTypeSupergroup)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(viewModel.OpenTopic, forumTopic, Strings.OpenInNewWindow, Icons.WindowNew);
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(viewModel.SelectTopic, forumTopic, Strings.Select, Icons.CheckmarkCircle);
                }
            }
            else if (topic is DirectMessagesChatTopic directMessagesChatTopic && supergroup.IsAdministeredDirectMessagesGroup)
            {
                flyout.CreateFlyoutItem(viewModel.ClearTopic, directMessagesChatTopic, Strings.ClearHistory, Icons.Broom);
            }

            flyout.ShowAt(sender, args);
        }

        private bool CanCreateTopics(Chat chat, Supergroup supergroup, ForumTopic topic)
        {
            if (supergroup.Status is ChatMemberStatusCreator || (supergroup.Status is ChatMemberStatusAdministrator admin && (admin.Rights.CanPinMessages || supergroup.IsChannel && admin.Rights.CanEditMessages)))
            {
                return true;
            }
            else if (supergroup.Status is ChatMemberStatusRestricted restricted)
            {
                return restricted.Permissions.CanCreateTopics;
            }

            return chat.Permissions.CanCreateTopics;
        }

        #endregion

        public event EventHandler<ForumViewItemClickEventArgs> ItemClick;

        public string GetAutomationName()
        {
            if (Title == null || Subtitle == null || ChatActionLabel == null)
            {
                return string.Empty;
            }

            var result = Title.Text.TrimEnd('.', ',');
            var identity = Identity.CurrentType switch
            {
                IdentityIconType.Fake => Strings.FakeMessage,
                IdentityIconType.Scam => Strings.ScamMessage,
                IdentityIconType.Premium => Strings.AccDescrPremium,
                IdentityIconType.Verified => Strings.AccDescrVerified,
                _ => null
            };

            if (identity != null)
            {
                result += ", " + identity;
            }

            if (ChatActionLabel.Text.Length > 0)
            {
                result += ", " + ChatActionLabel.Text;
            }
            else if (Subtitle.Text.Length > 0)
            {
                result += ", " + Subtitle.Text;
            }

            return result;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(this, new ForumViewItemClickEventArgs(e.ClickedItem, true));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainPage = this.GetParent<MainPage>();
            mainPage?.HideTopicList();
        }
    }
}
