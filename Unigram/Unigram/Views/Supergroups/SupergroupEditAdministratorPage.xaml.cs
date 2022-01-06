using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Windows.UI.Xaml;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditAdministratorPage : HostedPage, IMemberDelegate
    {
        public SupergroupEditAdministratorViewModel ViewModel => DataContext as SupergroupEditAdministratorViewModel;

        public SupergroupEditAdministratorPage()
        {
            InitializeComponent();
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
            Title.Text = user.GetFullName();
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
            Photo.SetUser(ViewModel.ProtoService, user, 64);

            Verified.Visibility = user.IsVerified ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
        }

        public void UpdateMember(Chat chat, User user, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
            {
                var canBeEdited = (member.Status is ChatMemberStatusCreator && member.MemberId.IsUser(ViewModel.CacheService.Options.MyId)) || (member.Status is ChatMemberStatusAdministrator administrator && administrator.CanBeEdited);

                Header.CommandVisibility = canBeEdited ? Visibility.Visible : Visibility.Collapsed;
                Dismiss.Visibility = canBeEdited ? Visibility.Visible : Visibility.Collapsed;
                PermissionsRoot.Footer = canBeEdited ? null : Strings.Resources.EditAdminCantEdit;
                PermissionsPanel.IsEnabled = canBeEdited;
                EditRankField.PlaceholderText = member.Status is ChatMemberStatusCreator ? Strings.Resources.ChannelCreator : Strings.Resources.ChannelAdmin;
                EditRankPanel.Footer = string.Format(Strings.Resources.EditAdminRankInfo, member.Status is ChatMemberStatusCreator ? Strings.Resources.ChannelCreator : Strings.Resources.ChannelAdmin);
            }
            else
            {
                Header.CommandVisibility = Visibility.Visible;
                Dismiss.Visibility = Visibility.Collapsed;
                PermissionsRoot.Footer = null;
                PermissionsPanel.IsEnabled = true;
                EditRankField.PlaceholderText = Strings.Resources.ChannelAdmin;
                EditRankPanel.Footer = string.Format(Strings.Resources.EditAdminRankInfo, Strings.Resources.ChannelAdmin);
            }

            if (chat.Type is ChatTypeSupergroup group)
            {
                PermissionsPanel.Visibility = Visibility.Visible;

                ChangeInfo.Content = group.IsChannel ? Strings.Resources.EditAdminChangeChannelInfo : Strings.Resources.EditAdminChangeGroupInfo;
                PostMessages.Visibility = PostMessagesSeparator.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
                EditMessages.Visibility = EditMessagesSeparator.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
                DeleteMessages.Content = group.IsChannel ? Strings.Resources.EditAdminDeleteMessages : Strings.Resources.EditAdminGroupDeleteMessages;
                BanUsers.Visibility = BanUsersSeparator.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                PinMessages.Visibility = PinMessagesSeparator.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                ManageVideoChats.Visibility = ManageVideoChatsSeparator.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                IsAnonymous.Visibility = IsAnonymousSeparator.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
                AddUsers.Content = chat.Permissions.CanInviteUsers ? Strings.Resources.EditAdminAddUsersViaLink : Strings.Resources.EditAdminAddUsers;
            }
            else
            {
                PermissionsPanel.Visibility = Visibility.Collapsed;
            }

            //TransferOwnership.Content = group.IsChannel ? Strings.Resources.EditAdminChannelTransfer : Strings.Resources.EditAdminGroupTransfer;
        }
    }
}
