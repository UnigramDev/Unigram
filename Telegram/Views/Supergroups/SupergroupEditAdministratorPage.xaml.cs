//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupEditAdministratorPage : HostedPage, IMemberDelegate
    {
        public SupergroupEditAdministratorViewModel ViewModel => DataContext as SupergroupEditAdministratorViewModel;

        public SupergroupEditAdministratorPage()
        {
            InitializeComponent();
            Title = Strings.EditAdmin;
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

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Cell.UpdateUser(ViewModel.ClientService, user, 64);
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
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

                Done.Visibility = canBeEdited ? Visibility.Visible : Visibility.Collapsed;
                Dismiss.Visibility = member.Status is ChatMemberStatusAdministrator && canBeEdited ? Visibility.Visible : Visibility.Collapsed;
                PermissionsRoot.Footer = canBeEdited ? null : Strings.EditAdminCantEdit;
                EditRankField.PlaceholderText = member.Status is ChatMemberStatusCreator ? Strings.ChannelCreator : Strings.ChannelAdmin;
                EditRankPanel.Footer = string.Format(Strings.EditAdminRankInfo, member.Status is ChatMemberStatusCreator ? Strings.ChannelCreator : Strings.ChannelAdmin);

                ChangeInfo.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                PostMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                EditMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                DeleteMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                BanUsers.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                AddUsers.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                PinMessages.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                ManageVideoChats.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                AddAdmins.IsEnabled = member.Status is ChatMemberStatusAdministrator && canBeEdited;
                IsAnonymous.IsEnabled = canBeEdited;
            }
            else
            {
                Done.Visibility = Visibility.Visible;
                Dismiss.Visibility = Visibility.Collapsed;
                PermissionsRoot.Footer = null;
                EditRankField.PlaceholderText = Strings.ChannelAdmin;
                EditRankPanel.Footer = string.Format(Strings.EditAdminRankInfo, Strings.ChannelAdmin);
            }

            if (chat.Type is ChatTypeSupergroup group)
            {
                PermissionsRoot.Visibility = Visibility.Visible;

                ChangeInfo.Content = group.IsChannel ? Strings.EditAdminChangeChannelInfo : Strings.EditAdminChangeGroupInfo;
                PostMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
                EditMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
                DeleteMessages.Content = group.IsChannel ? Strings.EditAdminDeleteMessages : Strings.EditAdminGroupDeleteMessages;
                BanUsers.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                PinMessages.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                ManageVideoChats.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                IsAnonymous.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                AddUsers.Content = chat.Permissions.CanInviteUsers ? Strings.EditAdminAddUsersViaLink : Strings.EditAdminAddUsers;
            }
            else
            {
                PermissionsRoot.Visibility = Visibility.Collapsed;
            }

            //TransferOwnership.Content = group.IsChannel ? Strings.EditAdminChannelTransfer : Strings.EditAdminGroupTransfer;
        }

        #region Binding

        private Visibility ConvertActionVisibility(Visibility ownership, Visibility dismiss)
        {
            if (ownership == Visibility.Visible)
            {
                return Visibility.Visible;
            }

            return dismiss;
        }

        #endregion

    }
}
