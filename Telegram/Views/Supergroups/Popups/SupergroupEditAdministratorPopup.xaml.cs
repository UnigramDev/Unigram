//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Supergroups.Popups
{
    public partial class SupergroupEditMemberArgs
    {
        public long ChatId { get; }

        public MessageSender MemberId { get; }

        public ChatAdministratorRights DefaultRights { get; }

        public SupergroupEditMemberArgs(long chatId, MessageSender memberId)
        {
            ChatId = chatId;
            MemberId = memberId;
        }

        public SupergroupEditMemberArgs(long chatId, MessageSender memberId, ChatAdministratorRights defaultRights)
        {
            ChatId = chatId;
            MemberId = memberId;
            DefaultRights = defaultRights;
        }
    }

    public sealed partial class SupergroupEditAdministratorPopup : ContentPopup, IMemberPopupDelegate
    {
        public SupergroupEditAdministratorViewModel ViewModel => DataContext as SupergroupEditAdministratorViewModel;

        public SupergroupEditAdministratorPopup()
        {
            InitializeComponent();
            Title = Strings.EditAdmin;

            SecondaryButtonText = Strings.Cancel;
        }

        public void UpdateChat(Chat chat)
        {
        }

        public void UpdateChatTitle(Chat chat)
        {
        }

        public void UpdateChatPhoto(Chat chat)
        {
        }

        public void UpdateUser(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            Cell.UpdateUser(ViewModel.ClientService, user, 64);
            Cell.Height = double.NaN;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Cell.Subtitle = LastSeenConverter.GetLabel(user, true);
        }

        public void UpdateMember(Chat chat, User user, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
            {
                var canBeEdited = (member.Status is ChatMemberStatusCreator && member.MemberId.IsUser(ViewModel.ClientService.Options.MyId)) || (member.Status is ChatMemberStatusAdministrator administrator && administrator.CanBeEdited);

                PrimaryButtonText = canBeEdited ? Strings.Done : string.Empty;
                Dismiss.Visibility = member.Status is ChatMemberStatusAdministrator && canBeEdited ? Visibility.Visible : Visibility.Collapsed;
                EditRankFooter.Text = string.Format(Strings.EditAdminRankInfo, member.Status is ChatMemberStatusCreator ? Strings.ChatTagOwner : Strings.ChatTagAdmin);

                if (user.Type is UserTypeBot { IsGuard: true })
                {
                    ProcessJoinRequests.Visibility = Visibility.Visible;
                    PermissionsFooter.Visibility = Visibility.Visible;
                    PermissionsFooter.Text = Strings.EditAdminProcessJoinRequestsInfo;
                }
                else
                {
                    ProcessJoinRequests.Visibility = Visibility.Collapsed;
                    PermissionsFooter.Visibility = canBeEdited ? Visibility.Collapsed : Visibility.Visible;
                }

                ChangeInfo.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited && !chat.Permissions.CanChangeInfo;
                CanManageMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                CanManageStories.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                CanPostMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                CanEditMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                CanDeleteMessages2.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                CanPostStories.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                CanEditStories.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                CanDeleteStories.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                DeleteMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                BanUsers.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                ManageDirectMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                AddUsers.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                PinMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited && !chat.Permissions.CanPinMessages;
                ManageTags.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited && !chat.Permissions.CanEditTag;
                ManageTopics.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                ManageVideoChats.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                AddAdmins.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                IsAnonymous.IsEnabled = canBeEdited;
                ProcessJoinRequests.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                EditRankField.IsEnabled = canBeEdited;
            }
            else
            {
                ChangeInfo.IsEnabled = !chat.Permissions.CanChangeInfo;
                PinMessages.IsEnabled = !chat.Permissions.CanPinMessages;
                ManageTags.IsEnabled = !chat.Permissions.CanEditTag;

                PrimaryButtonText = Strings.Done;
                Dismiss.Visibility = Visibility.Collapsed;
                PermissionsFooter.Visibility = Visibility.Collapsed;
                EditRankFooter.Text = string.Format(Strings.EditAdminRankInfo, Strings.ChatTagAdmin);
            }

            if (chat.Type is ChatTypeSupergroup group)
            {
                PermissionsRoot.Visibility = Visibility.Visible;
                //PermissionsFooter.Visibility = Visibility.Collapsed;

                if (group.IsChannel)
                {
                    CanManageMessagesRoot.Visibility = Visibility.Visible;
                    DeleteMessages.Visibility = Visibility.Collapsed;

                    EditRankHeader.Visibility = Visibility.Collapsed;
                    EditRankField.Visibility = Visibility.Collapsed;
                    EditRankFooter.Visibility = Visibility.Collapsed;
                }

                ChangeInfo.Content = group.IsChannel ? Strings.EditAdminChangeChannelInfo : Strings.EditAdminChangeGroupInfo;
                ManageDirectMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
                PinMessages.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                ManageTags.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                IsAnonymous.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                ManageTopics.Visibility = ViewModel.IsForum ? Visibility.Visible : Visibility.Collapsed;
                AddUsers.Content = chat.Permissions.CanInviteUsers ? Strings.EditAdminAddUsersViaLink : Strings.EditAdminAddUsers;
            }
            else
            {
                PermissionsRoot.Visibility = Visibility.Collapsed;
                //PermissionsFooter.Visibility = Visibility.Collapsed;
            }

            UpdatePreview(chat, member);

            BackgroundControl.Update(ViewModel.ClientService, null);
            Message.Margin = new Thickness(8, 12, 12, 12);

            //TransferOwnership.Content = group.IsChannel ? Strings.EditAdminChannelTransfer : Strings.EditAdminGroupTransfer;
        }

        #region Binding

        private string ConvertCanSendCount(int count)
        {
            return $"{count}/3";
        }

        private Visibility ConvertActionVisibility(Visibility ownership, Visibility dismiss)
        {
            if (ownership == Visibility.Visible)
            {
                return Visibility.Visible;
            }

            return dismiss;
        }

        #endregion

        private void EditRankField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel.Member == null)
            {
                return;
            }

            UpdatePreview(ViewModel.Chat, ViewModel.Member);
        }

        private void UpdatePreview(Chat chat, ChatMember member)
        {
            var tag = member.Status switch
            {
                ChatMemberStatusCreator => string.IsNullOrEmpty(EditRankField.Text) ? Strings.ChatTagOwner : EditRankField.Text,
                _ => string.IsNullOrEmpty(EditRankField.Text) ? Strings.ChatTagAdmin : EditRankField.Text,
            };

            Message.UpdateMockup(ViewModel.ClientService, chat, member.MemberId, tag, member.Status switch
            {
                ChatMemberStatusCreator => ChatMemberRank.Owner,
                _ => ChatMemberRank.Admin
            });
        }
    }
}
