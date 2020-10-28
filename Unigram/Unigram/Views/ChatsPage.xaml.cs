using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Views
{
    public sealed partial class ChatsPage : ChatsListView
    {
        public MainViewModel Main { get; set; }

        public ChatsPage()
        {
            InitializeComponent();
        }

        public bool AllowSelection { get; set; }

        private void ChatsList_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ChatsListViewItem();
                args.ItemContainer.Style = ChatsList.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = ChatsList.ItemTemplate;
                args.ItemContainer.ContextRequested += Chat_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        #region Context menu

        private async void Chat_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.Tag as Chat;

            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position == null)
            {
                return;
            }

            var muted = ViewModel.CacheService.GetNotificationSettingsMuteFor(chat) > 0;
            flyout.CreateFlyoutItem(DialogArchive_Loaded, viewModel.ChatArchiveCommand, chat, chat.Positions.Any(x => x.List is ChatListArchive) ? Strings.Resources.Unarchive : Strings.Resources.Archive, new FontIcon { Glyph = Icons.Archive });
            flyout.CreateFlyoutItem(DialogPin_Loaded, viewModel.ChatPinCommand, chat, position.IsPinned ? Strings.Resources.UnpinFromTop : Strings.Resources.PinToTop, new FontIcon { Glyph = position.IsPinned ? Icons.Unpin : Icons.Pin });

            if (viewModel.Items.ChatList is ChatListFilter chatListFilter)
            {
                flyout.CreateFlyoutItem(viewModel.FolderRemoveCommand, (chatListFilter.ChatFilterId, chat), Strings.Resources.FilterRemoveFrom, new FontIcon { Glyph = "\uE92B", FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily });
            }
            else
            {
                var response = await ViewModel.ProtoService.SendAsync(new GetChatListsToAddChat(chat.Id)) as ChatLists;
                if (response != null && response.ChatListsValue.Count > 0)
                {
                    var filters = ViewModel.CacheService.ChatFilters;

                    var item = new MenuFlyoutSubItem();
                    item.Text = Strings.Resources.FilterAddTo;
                    item.Icon = new FontIcon { Glyph = "\uE929", FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily };

                    foreach (var chatList in response.ChatListsValue.OfType<ChatListFilter>())
                    {
                        var filter = filters.FirstOrDefault(x => x.Id == chatList.ChatFilterId);
                        if (filter != null)
                        {
                            item.CreateFlyoutItem(ViewModel.FolderAddCommand, (filter.Id, chat), filter.Title, new FontIcon { Glyph = Icons.FromFilter(Icons.ParseFilter(filter.IconName)), FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily });
                        }
                    }

                    if (filters.Count < 10 && item.Items.Count > 0)
                    {
                        item.CreateFlyoutSeparator();
                        item.CreateFlyoutItem(ViewModel.FolderCreateCommand, chat, Strings.Resources.CreateNewFilter, new FontIcon { Glyph = Icons.Add });

                        flyout.Items.Add(item);
                    }
                }
            }

            flyout.CreateFlyoutItem(DialogNotify_Loaded, viewModel.ChatNotifyCommand, chat, muted ? Strings.Resources.UnmuteNotifications : Strings.Resources.MuteNotifications, new FontIcon { Glyph = muted ? Icons.Unmute : Icons.Mute });
            flyout.CreateFlyoutItem(DialogMark_Loaded, viewModel.ChatMarkCommand, chat, chat.IsUnread() ? Strings.Resources.MarkAsRead : Strings.Resources.MarkAsUnread, new FontIcon { Glyph = chat.IsUnread() ? Icons.MarkAsRead : Icons.MarkAsUnread, FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily });
            flyout.CreateFlyoutItem(DialogClear_Loaded, viewModel.ChatClearCommand, chat, Strings.Resources.ClearHistory, new FontIcon { Glyph = Icons.Clear });
            flyout.CreateFlyoutItem(DialogDelete_Loaded, viewModel.ChatDeleteCommand, chat, DialogDelete_Text(chat), new FontIcon { Glyph = Icons.Delete });

            if (AllowSelection && viewModel.SelectionMode != ListViewSelectionMode.Multiple)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.ChatSelectCommand, chat, Strings.Additional.Select, new FontIcon { Glyph = Icons.Select });
            }

            args.ShowAt(flyout, element);
        }

        private bool DialogMark_Loaded(Chat chat)
        {
            return true;
        }

        private bool DialogPin_Loaded(Chat chat)
        {
            //if (!chat.IsPinned)
            //{
            //    var count = ViewModel.Dialogs.LegacyItems.Where(x => x.IsPinned).Count();
            //    var max = ViewModel.CacheService.Config.PinnedDialogsCountMax;

            //    return count < max ? Visibility.Visible : Visibility.Collapsed;
            //}

            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position?.Source != null)
            {
                return false;
            }

            return true;
        }

        private bool DialogArchive_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (ViewModel.CacheService.IsSavedMessages(chat) || position?.Source != null || chat.Id == 777000)
            {
                return false;
            }

            return true;
        }

        private bool DialogNotify_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (ViewModel.CacheService.IsSavedMessages(chat) || position?.Source is ChatSourcePublicServiceAnnouncement)
            {
                return false;
            }

            return true;
        }

        public bool DialogClear_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position?.Source != null)
            {
                return false;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null)
                {
                    return string.IsNullOrEmpty(supergroup.Username) && !super.IsChannel;
                }
            }

            return true;
        }

        private bool DialogDelete_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position?.Source is ChatSourceMtprotoProxy)
            {
                return false;
            }

            //if (dialog.With is TLChannel channel)
            //{
            //    return Visibility.Visible;
            //}
            //else if (dialog.Peer is TLPeerUser userPeer)
            //{
            //    return Visibility.Visible;
            //}
            //else if (dialog.Peer is TLPeerChat chatPeer)
            //{
            //    return dialog.With is TLChatForbidden || dialog.With is TLChatEmpty ? Visibility.Visible : Visibility.Collapsed;
            //}

            //return Visibility.Collapsed;

            return true;
        }

        private string DialogDelete_Text(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position?.Source is ChatSourcePublicServiceAnnouncement)
            {
                return Strings.Resources.PsaHide;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                return super.IsChannel ? Strings.Resources.LeaveChannelMenu : Strings.Resources.LeaveMegaMenu;
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                return Strings.Resources.DeleteAndExit;
            }

            return Strings.Resources.Delete;
        }

        #endregion

        #region Reorder

        private void Chats_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            var header = Header as FrameworkElement;
            var headerVisibility = header != null ? header.Visibility : Visibility.Visible;

            //if (e.Items.Count > 1 || e.Items[0] is Chat chat && !chat.IsPinned || headerVisibility == Visibility.Visible || ChatsList.SelectionMode2 == ListViewSelectionMode.Multiple)
            //{
            //    ChatsList.CanReorderItems = false;
            //    e.Cancel = true;
            //}
            //else
            //{
            //    ChatsList.CanReorderItems = true;
            //}
        }

        private void Chats_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ChatsList.CanReorderItems = false;

            var chatList = ViewModel?.Items.ChatList;
            if (chatList == null)
            {
                return;
            }

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is Chat chat)
            {
                var items = ViewModel.Items;
                var index = items.IndexOf(chat);

                var compare = items[index > 0 ? index - 1 : index + 1];

                //if (compare.Source != null && index > 0)
                //{
                //    compare = items[index + 1];
                //}

                //if (compare.IsPinned)
                //{
                //    ViewModel.ProtoService.Send(new SetPinnedChats(chatList, items.Where(x => x.IsPinned).Select(x => x.Id).ToList()));
                //}
                //else
                //{
                //    ViewModel.Items.Handle(chat.Id, chat.Order);
                //}
            }
        }

        #endregion

        private string ConvertCount(int count)
        {
            return Locale.Declension("Chats", count);
        }
    }
}
