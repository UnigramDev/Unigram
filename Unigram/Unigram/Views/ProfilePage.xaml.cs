//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.ComponentModel;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Chats;
using Unigram.Views.Users;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ProfilePage : HostedPage, IProfileDelegate
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        public ProfilePage()
        {
            InitializeComponent();
            Title = Strings.Resources.Info;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SharedCount"))
            {
                if (ViewModel.HasSharedMembers)
                {
                    MediaFrame.Navigate(typeof(ChatSharedMembersPage), null, new SuppressNavigationTransitionInfo());
                    return;
                }

                var sharedCount = ViewModel.SharedCount;
                if (sharedCount[0] > 0)
                {
                    MediaFrame.Navigate(typeof(ChatSharedMediaPage), null, new SuppressNavigationTransitionInfo());
                }
                else if (sharedCount[1] > 0)
                {
                    MediaFrame.Navigate(typeof(ChatSharedFilesPage), null, new SuppressNavigationTransitionInfo());
                }
                else if (sharedCount[2] > 0)
                {
                    MediaFrame.Navigate(typeof(ChatSharedLinksPage), null, new SuppressNavigationTransitionInfo());
                }
                else if (sharedCount[3] > 0)
                {
                    MediaFrame.Navigate(typeof(ChatSharedMusicPage), null, new SuppressNavigationTransitionInfo());
                }
                else if (sharedCount[4] > 0)
                {
                    MediaFrame.Navigate(typeof(ChatSharedVoicePage), null, new SuppressNavigationTransitionInfo());
                }
                else if (ViewModel.HasSharedGroups)
                {
                    MediaFrame.Navigate(typeof(UserCommonChatsPage), null, new SuppressNavigationTransitionInfo());
                }
            }
        }

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            ProfileHeader?.UpdateChat(chat);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateChat(chat);
            }

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title = ViewModel.ClientService.GetTitle(chat);

            ProfileHeader?.UpdateChatTitle(chat);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateChatTitle(chat);
            }
        }

        public void UpdateChatPhoto(Chat chat)
        {
            ProfileHeader?.UpdateChatPhoto(chat);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateChatPhoto(chat);
            }
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            ProfileHeader?.UpdateChatNotificationSettings(chat);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateChatNotificationSettings(chat);
            }
        }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            ProfileHeader?.UpdateUser(chat, user, secret);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateUser(chat, user, secret);
            }
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            ProfileHeader?.UpdateUserFullInfo(chat, user, fullInfo, secret, accessToken);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateUserFullInfo(chat, user, fullInfo, secret, accessToken);
            }
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            ProfileHeader?.UpdateUserStatus(chat, user);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateUserStatus(chat, user);
            }
        }



        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            ProfileHeader?.UpdateSecretChat(chat, secretChat);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateSecretChat(chat, secretChat);
            }
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            ProfileHeader?.UpdateBasicGroup(chat, group);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateBasicGroup(chat, group);
            }
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ProfileHeader?.UpdateBasicGroupFullInfo(chat, group, fullInfo);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateBasicGroupFullInfo(chat, group, fullInfo);
            }

            ViewModel.Members = new SortedObservableCollection<ChatMember>(new ChatMemberComparer(ViewModel.ClientService, true), fullInfo.Members);
        }



        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            ProfileHeader?.UpdateSupergroup(chat, group);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateSupergroup(chat, group);
            }

            if (!group.IsChannel && (ViewModel.Members == null || group.MemberCount < 200 && group.MemberCount != ViewModel.Members.Count))
            {
                ViewModel.Members = ViewModel.CreateMembers(group.Id);
            }
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ProfileHeader?.UpdateSupergroupFullInfo(chat, group, fullInfo);

            if (MediaFrame.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.UpdateSupergroupFullInfo(chat, group, fullInfo);
            }
        }

        #endregion

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is ChatSharedMediaPageBase sharedMedia)
            {
                sharedMedia.Header.DataContext = ViewModel;
            }

            UnloadObject(ProfileHeader);
        }
    }
}
