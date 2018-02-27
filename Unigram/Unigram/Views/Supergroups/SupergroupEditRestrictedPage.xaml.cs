using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Supergroups;
using Unigram.ViewModels.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Supergroups
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SupergroupEditRestrictedPage : Page, IMemberDelegate
    {
        public SupergroupEditRestrictedViewModel ViewModel => DataContext as SupergroupEditRestrictedViewModel;

        public SupergroupEditRestrictedPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SupergroupEditRestrictedViewModel>();
            ViewModel.Delegate = this;
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
            Photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 64, 64);

            Verified.Visibility = user.IsVerified ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret)
        {
        }

        public void UpdateMember(Chat chat, Supergroup group, User user, ChatMember member)
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

            //ChangeInfo.Header = group.IsChannel ? Strings.Android.EditAdminChangeChannelInfo : Strings.Android.EditAdminChangeGroupInfo;
            //PostMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            //EditMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            //DeleteMessages.Header = group.IsChannel ? Strings.Android.EditAdminDeleteMessages : Strings.Android.EditAdminGroupDeleteMessages;
            //BanUsers.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
            //AddUsers.Header = group.AnyoneCanInvite ? Strings.Android.EditAdminAddUsersViaLink : Strings.Android.EditAdminAddUsers;
        }
    }
}
