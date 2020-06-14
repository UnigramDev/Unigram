using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Windows.UI.Xaml;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditRestrictedPage : HostedPage, IMemberDelegate
    {
        public SupergroupEditRestrictedViewModel ViewModel => DataContext as SupergroupEditRestrictedViewModel;

        public SupergroupEditRestrictedPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SupergroupEditRestrictedViewModel, IMemberDelegate>(this);
        }

        #region Binding

        private string ConvertUntilDate(int date)
        {
            if (date == 0)
            {
                return Strings.Resources.UserRestrictionsUntilForever;
            }

            var dateTime = BindConvert.Current.DateTime(date);
            return BindConvert.Current.ShortDate.Format(dateTime) + ", " + BindConvert.Current.ShortTime.Format(dateTime);
        }

        #endregion

        #region Delegate

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Title.Text = user.GetFullName();
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
            Photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 64);

            Verified.Visibility = user.IsVerified ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken) { }

        public void UpdateMember(Chat chat, User user, ChatMember member)
        {
            if (member.Status is ChatMemberStatusRestricted restricted)
            {
                DismissPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DismissPanel.Visibility = Visibility.Collapsed;
            }

            PermissionsPanel.Visibility = Visibility.Visible;

            //ChangeInfo.Header = group.IsChannel ? Strings.Resources.EditAdminChangeChannelInfo : Strings.Resources.EditAdminChangeGroupInfo;
            //PostMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            //EditMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            //DeleteMessages.Header = group.IsChannel ? Strings.Resources.EditAdminDeleteMessages : Strings.Resources.EditAdminGroupDeleteMessages;
            //BanUsers.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
            //AddUsers.Header = group.AnyoneCanInvite ? Strings.Resources.EditAdminAddUsersViaLink : Strings.Resources.EditAdminAddUsers;
        }

        #endregion

    }
}
