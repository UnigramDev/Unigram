//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using System.Linq;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupEditPage : HostedPage, ISupergroupEditDelegate
    {
        public SupergroupEditViewModel ViewModel => DataContext as SupergroupEditViewModel;

        public SupergroupEditPage()
        {
            InitializeComponent();
            Title = Strings.ChannelEdit;
        }

        #region Binding

        private string ConvertHistory(int available)
        {
            return ViewModel.AllHistoryAvailableOptions[available].Value
                ? Strings.ChatHistoryVisibleInfo
                : Strings.ChatHistoryHiddenInfo;
        }

        #endregion

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            //UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

            if (chat.AvailableReactions is ChatAvailableReactionsAll)
            {
                Reactions.Badge = Strings.AllReactions;
            }
            else if (chat.AvailableReactions is ChatAvailableReactionsSome some)
            {
                if (some.Reactions.Count > 0)
                {
                    Reactions.Badge = some.Reactions.Count.ToString("N0");
                }
                else
                {
                    Reactions.Badge = Strings.ReactionsOff;
                }
            }
        }

        public void UpdateChatTitle(Chat chat)
        {
            TitleLabel.Text = ViewModel.ClientService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.SetChat(ViewModel.ClientService, chat, 96);
        }

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            TitleLabel.PlaceholderText = group.IsChannel ? Strings.EnterChannelName : Strings.GroupName;

            Delete.Content = group.IsChannel ? Strings.ChannelDelete : Strings.DeleteMega;
            DeletePanel.Footer = group.IsChannel ? Strings.ChannelDeleteInfo : Strings.MegaDeleteInfo;

            Members.Content = group.IsChannel ? Strings.ChannelSubscribers : Strings.ChannelMembers;
            //Members.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            EventLog.Visibility = Visibility.Visible;

            ViewModel.Title = chat.Title;

            var canChangeInfo = group.CanChangeInfo(chat);
            var canInviteUsers = group.CanInviteUsers();
            var canRestrictMembers = group.CanRestrictMembers();
            var hasActiveUsername = group.HasActiveUsername();

            TitleLabel.IsReadOnly = !canChangeInfo;
            About.IsReadOnly = !canChangeInfo;
            SetNewPhoto.Visibility = canChangeInfo
                ? Visibility.Visible
                : Visibility.Collapsed;

            ChatType.Content = group.IsChannel ? Strings.ChannelType : Strings.GroupType;
            ChatType.Glyph = group.IsChannel ? Icons.Megaphone : Icons.People;
            ChatType.Badge = hasActiveUsername
                ? group.IsChannel
                    ? Strings.TypePublic
                    : Strings.TypePublicGroup
                : group.IsChannel
                    ? chat.HasProtectedContent
                        ? Strings.TypePrivateRestrictedForwards
                        : Strings.TypePrivate
                    : chat.HasProtectedContent
                        ? Strings.TypePrivateGroupRestrictedForwards
                        : Strings.TypePrivateGroup;

            ChatHistory.Visibility = canChangeInfo && !hasActiveUsername && !group.IsChannel
                ? Visibility.Visible
                : Visibility.Collapsed;

            InviteLinks.Visibility = canInviteUsers && !hasActiveUsername
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (canChangeInfo)
            {
                if (ViewModel.IsPremiumAvailable)
                {
                    ChannelColor.Visibility = Visibility.Visible;
                    ProfileColor.SetChat(ViewModel.ClientService, chat);
                }
                else
                {
                    ChannelColor.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ChannelColor.Visibility = Visibility.Collapsed;
            }

            ChatLinked.Visibility = group.IsChannel ? Visibility.Visible : group.HasLinkedChat ? Visibility.Visible : Visibility.Collapsed;
            ChatLinked.Content = group.IsChannel ? Strings.Discussion : Strings.LinkedChannel;
            ChatLinked.Glyph = group.IsChannel ? Icons.ChatEmpty : Icons.Megaphone;
            ChatLinked.Badge = group.HasLinkedChat ? string.Empty : Strings.DiscussionInfo;

            Permissions.Badge = string.Format("{0}/{1}", chat.Permissions.Count(), chat.Permissions.Total());
            Permissions.Visibility = group.IsChannel || !canRestrictMembers ? Visibility.Collapsed : Visibility.Visible;

            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ViewModel.About = fullInfo.Description;
            ViewModel.IsAllHistoryAvailable = fullInfo.IsAllHistoryAvailable ? 0 : 1;

            var linkedChat = ViewModel.ClientService.GetChat(fullInfo.LinkedChatId);
            if (linkedChat != null && ViewModel.ClientService.TryGetSupergroup(linkedChat, out Supergroup linkedSupergroup))
            {
                if (linkedSupergroup.HasActiveUsername(out string username))
                {
                    ChatLinked.Badge = $"@{username}";
                }
                else
                {
                    ChatLinked.Badge = linkedChat.Title;
                }
            }
            else
            {
                ChatLinked.Badge = Strings.DiscussionInfoShort;
            }

            Admins.Badge = fullInfo.AdministratorCount;
            Members.Badge = fullInfo.MemberCount;
            Blacklist.Badge = fullInfo.BannedCount;

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            Statistics.Visibility = fullInfo.CanGetStatistics
                ? Visibility.Visible
                : Visibility.Collapsed;
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            TitleLabel.PlaceholderText = Strings.GroupName;

            Delete.Content = Strings.DeleteMega;
            DeletePanel.Footer = Strings.MegaDeleteInfo;

            Members.Content = Strings.ChannelMembers;

            EventLog.Visibility = Visibility.Collapsed;

            ViewModel.Title = chat.Title;
            ViewModel.IsAllHistoryAvailable = 1;

            var canChangeInfo = group.CanChangeInfo(chat);
            var canInviteUsers = group.CanInviteUsers();

            TitleLabel.IsReadOnly = !canChangeInfo;
            About.IsReadOnly = !canChangeInfo;
            SetNewPhoto.Visibility = canChangeInfo
                ? Visibility.Visible
                : Visibility.Collapsed;

            ChatType.Glyph = Icons.People;
            ChatType.Content = Strings.GroupType;
            ChatType.Badge = Strings.TypePrivateGroup;
            ChatType.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            ChatHistory.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            InviteLinks.Visibility = canInviteUsers
                ? Visibility.Visible
                : Visibility.Collapsed;
            ChatLinked.Visibility = Visibility.Collapsed;
            ChannelColor.Visibility = Visibility.Collapsed;

            Permissions.Badge = string.Format("{0}/{1}", chat.Permissions.Count(), chat.Permissions.Total());
            Permissions.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;
            Blacklist.Visibility = Visibility.Collapsed;

            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            Admins.Badge = fullInfo.Members.Count(x => x.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator);
            Members.Badge = fullInfo.Members.Count;
            Blacklist.Badge = 0;

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        #endregion
    }
}
