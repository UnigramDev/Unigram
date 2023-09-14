//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupEditRestrictedPage : HostedPage, IMemberPopupDelegate
    {
        public SupergroupEditRestrictedViewModel ViewModel => DataContext as SupergroupEditRestrictedViewModel;

        public SupergroupEditRestrictedPage()
        {
            InitializeComponent();
            Title = Strings.UserRestrictions;
        }

        #region Binding

        private string ConvertUntilDate(int date)
        {
            if (date == 0)
            {
                return Strings.UserRestrictionsUntilForever;
            }

            var dateTime = Formatter.ToLocalTime(date);
            return Formatter.ShortDate.Format(dateTime) + ", " + Formatter.ShortTime.Format(dateTime);
        }

        private string ConvertCanSendCount(int count)
        {
            return $"{count}/9";
        }

        #endregion

        #region Delegate

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Cell.UpdateUser(ViewModel.ClientService, user, 64);
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Cell.Subtitle = LastSeenConverter.GetLabel(user, true);
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken) { }

        public void UpdateMember(Chat chat, User user, ChatMember member)
        {
            if (member.Status is ChatMemberStatusRestricted)
            {
                DismissPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DismissPanel.Visibility = Visibility.Collapsed;
            }

            PermissionsPanel.Visibility = Visibility.Visible;

            CanSendPhotos.IsEnabled = chat.Permissions.CanSendPhotos;
            CanSendVideos.IsEnabled = chat.Permissions.CanSendVideos;
            CanSendOtherMessages.IsEnabled = chat.Permissions.CanSendOtherMessages;
            CanSendAudios.IsEnabled = chat.Permissions.CanSendAudios;
            CanSendDocuments.IsEnabled = chat.Permissions.CanSendDocuments;
            CanSendVoiceNotes.IsEnabled = chat.Permissions.CanSendVoiceNotes;
            CanSendVideoNotes.IsEnabled = chat.Permissions.CanSendVideoNotes;
            CanSendPolls.IsEnabled = chat.Permissions.CanSendPolls;
            CanAddWebPagePreviews.IsEnabled = chat.Permissions.CanAddWebPagePreviews;
            CanInviteUsers.IsEnabled = chat.Permissions.CanInviteUsers;
            CanPinMessages.IsEnabled = chat.Permissions.CanPinMessages;
            CanChangeInfo.IsEnabled = chat.Permissions.CanChangeInfo;

            //ChangeInfo.Header = group.IsChannel ? Strings.EditAdminChangeChannelInfo : Strings.EditAdminChangeGroupInfo;
            //PostMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            //EditMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            //DeleteMessages.Header = group.IsChannel ? Strings.EditAdminDeleteMessages : Strings.EditAdminGroupDeleteMessages;
            //BanUsers.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
            //AddUsers.Header = group.AnyoneCanInvite ? Strings.EditAdminAddUsersViaLink : Strings.EditAdminAddUsers;
        }

        #endregion

        public void Hide()
        {
            // TODO: move this to a popup too
        }
    }
}
