using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ChatsPage : ChatsListView
    {
        //public ChatsViewModel ViewModel => DataContext as ChatsViewModel;

        public ChatsPage()
        {
            InitializeComponent();
        }

        public bool AllowSelection { get; set; }

        private void ChatsList_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ChatsListViewItem(ChatsList);
                args.ItemContainer.ContentTemplate = ChatsList.ItemTemplate;
                args.ItemContainer.ContextRequested += Chat_ContextRequested;
            }

            args.ItemContainer.Style = ChatsList.ItemContainerStyleSelector.SelectStyle(args.Item, null);
            args.IsContainerPrepared = true;
        }

        #region Context menu

        private void Chat_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.Tag as Chat;

            var muted = ViewModel.CacheService.GetNotificationSettingsMuteFor(chat) > 0;
            flyout.CreateFlyoutItem(DialogPin_Loaded, viewModel.ChatPinCommand, chat, chat.IsPinned ? Strings.Resources.UnpinFromTop : Strings.Resources.PinToTop, new FontIcon { Glyph = chat.IsPinned ? Icons.Unpin : Icons.Pin });
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
            if (ViewModel.CacheService.IsSavedMessages(chat))
            {
                return false;
            }

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

            if (ViewModel.CacheService.IsChatSponsored(chat))
            {
                return false;
            }

            return true;
        }

        private bool DialogNotify_Loaded(Chat chat)
        {
            if (ViewModel.CacheService.IsSavedMessages(chat))
            {
                return false;
            }

            return true;
        }

        public bool DialogClear_Loaded(Chat chat)
        {
            if (ViewModel.CacheService.IsChatSponsored(chat))
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
            if (ViewModel.CacheService.IsChatSponsored(chat))
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
            if (chat.Type is ChatTypeSupergroup super)
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

            if (e.Items.Count > 1 || e.Items[0] is Chat chat && !chat.IsPinned || headerVisibility == Visibility.Visible || ChatsList.SelectionMode2 == ListViewSelectionMode.Multiple)
            {
                ChatsList.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                ChatsList.CanReorderItems = true;
            }
        }

        private void Chats_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ChatsList.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is Chat chat)
            {
                var items = ViewModel.Items;
                var index = items.IndexOf(chat);

                var compare = items[index > 0 ? index - 1 : index + 1];

                if (compare.IsSponsored && index > 0)
                {
                    compare = items[index + 1];
                }

                if (compare.IsPinned)
                {
                    ViewModel.ProtoService.Send(new SetPinnedChats(items.Where(x => x.IsPinned).Select(x => x.Id).ToList()));
                }
                else
                {
                    ViewModel.Handle(new UpdateChatOrder(chat.Id, chat.Order));
                }
            }
        }

        #endregion
    }
}
