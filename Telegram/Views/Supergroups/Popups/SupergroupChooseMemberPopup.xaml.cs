//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Supergroups.Popups
{
    public enum SupergroupChooseMemberMode
    {
        Promote,
        Restrict,
        Block
    }

    public class SupergroupChooseMemberArgs
    {
        public SupergroupChooseMemberArgs(long chatId, SupergroupChooseMemberMode mode)
        {
            ChatId = chatId;
            Mode = mode;
        }

        public long ChatId { get; }

        public SupergroupChooseMemberMode Mode { get; }
    }

    public sealed partial class SupergroupChooseMemberPopup : ContentPopup
    {
        public SupergroupChooseMemberViewModel ViewModel => DataContext as SupergroupChooseMemberViewModel;

        public SupergroupChooseMemberPopup()
        {
            InitializeComponent();

            SecondaryButtonText = Strings.Cancel;

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => SearchField.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += async (s, args) =>
            {
                var items = ViewModel.Search;
                if (items != null && string.Equals(SearchField.Text, items.Query))
                {
                    await items.LoadMoreItemsAsync(0);
                    await items.LoadMoreItemsAsync(1);
                    await items.LoadMoreItemsAsync(2);
                    await items.LoadMoreItemsAsync(3);
                }
            };
        }

        public override void OnNavigatedTo()
        {
            Title = ViewModel.Mode switch
            {
                SupergroupChooseMemberMode.Promote => Strings.ChannelAddAdmin,
                SupergroupChooseMemberMode.Restrict => Strings.ChannelAddException,
                SupergroupChooseMemberMode.Block => Strings.ChannelBlockUser,
                _ => string.Empty
            };
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            var messageSender = GetSender(e.ClickedItem);

            if (chat == null || messageSender == null)
            {
                return;
            }

            if (ViewModel.Mode == SupergroupChooseMemberMode.Promote && ViewModel.ClientService.TryGetUser(messageSender, out User tempUser))
            {
                var response = await ViewModel.ClientService.SendAsync(new CanSendMessageToUser(tempUser.Id, true));
                if (response is CanSendMessageToUserResultUserRestrictsNewChats)
                {
                    var text = string.Format(Strings.MessageLockedPremiumLocked, tempUser.FirstName);
                    var markdown = ClientEx.ParseMarkdown(text);

                    var confirm = await ToastPopup.ShowActionAsync(markdown, Strings.UserBlockedNonPremiumButton, new LocalFileSource("ms-appx:///Assets/Toasts/Premium.tgs"));
                    if (confirm == ContentDialogResult.Primary)
                    {
                        Hide();
                        ViewModel.NavigationService.ShowPromo();
                    }

                    return;
                }
            }

            Hide();

            if (ViewModel.Mode == SupergroupChooseMemberMode.Block)
            {
                ViewModel.ClientService.Send(new SetChatMemberStatus(chat.Id, messageSender, new ChatMemberStatusBanned()));
            }
            else
            {
                var sourcePopupType = ViewModel.Mode switch
                {
                    SupergroupChooseMemberMode.Promote => typeof(SupergroupEditAdministratorPopup),
                    SupergroupChooseMemberMode.Restrict => typeof(SupergroupEditRestrictedPopup),
                    _ => null
                };

                if (sourcePopupType == null)
                {
                    return;
                }

                _ = ViewModel.NavigationService.ShowPopupAsync(sourcePopupType, new SupergroupEditMemberArgs(chat.Id, messageSender));
            }
        }

        private MessageSender GetSender(object item)
        {
            if (item is ChatMember member)
            {
                return member.MemberId;
            }
            else if (item is SearchResult result)
            {
                if (result.User is User user)
                {
                    return new MessageSenderUser(user.Id);
                }
                else if (result.Chat is Chat temp && temp.Type is ChatTypePrivate privata)
                {
                    return new MessageSenderUser(privata.UserId);
                }
            }

            return null;
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                content.UpdateSupergroupMember(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        private void Search_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                if (args.InRecycleQueue)
                {
                    content.RecycleSearchResult();
                }
                else
                {
                    content.UpdateSearchResult(ViewModel.ClientService, args, Search_ContainerContentChanging);
                }
            }
        }

        #endregion

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            //MainHeader.Visibility = Visibility.Collapsed;
            //SearchField.Visibility = Visibility.Visible;

            //SearchField.Focus(FocusState.Keyboard);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            //if (string.IsNullOrEmpty(SearchField.Text))
            //{
            //    MainHeader.Visibility = Visibility.Visible;
            //    SearchField.Visibility = Visibility.Collapsed;

            //    Focus(FocusState.Programmatic);
            //}
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                ContentPanel.Visibility = Visibility.Visible;
                ViewModel.Search = null;
            }
            else
            {
                ContentPanel.Visibility = Visibility.Collapsed;
                ViewModel.UpdateSearch(SearchField.Text);
            }
        }
    }
}
