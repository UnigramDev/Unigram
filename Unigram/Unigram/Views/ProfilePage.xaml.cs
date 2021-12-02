using System;
using System.ComponentModel;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls.Gallery;
using Unigram.Converters;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Users;
using Unigram.Views.Chats;
using Unigram.Views.Users;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
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
            DataContext = TLContainer.Current.Resolve<ProfileViewModel, IProfileDelegate>(this);
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
                    return;
                }

                if (sharedCount[1] > 0)
                {
                    MediaFrame.Navigate(typeof(ChatSharedFilesPage), null, new SuppressNavigationTransitionInfo());
                    return;
                }

                else if (sharedCount[2] > 0)
                {
                    MediaFrame.Navigate(typeof(ChatSharedLinksPage), null, new SuppressNavigationTransitionInfo());
                    return;
                }

                else if (sharedCount[3] > 0)
                {
                    MediaFrame.Navigate(typeof(ChatSharedMusicPage), null, new SuppressNavigationTransitionInfo());
                    return;
                }

                else if (sharedCount[4] > 0)
                {
                    MediaFrame.Navigate(typeof(ChatSharedVoicePage), null, new SuppressNavigationTransitionInfo());
                    return;
                }

                if (ViewModel.HasSharedGroups)
                {
                    MediaFrame.Navigate(typeof(UserCommonChatsPage), null, new SuppressNavigationTransitionInfo());
                }
            }
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = ViewModel.ProtoService.GetUser(chat);
                if (user == null || user.ProfilePhoto == null)
                {
                    return;
                }

                var userFull = ViewModel.ProtoService.GetUserFull(user.Id);
                if (userFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, user, userFull);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => PhotoInfo);
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                var basicGroupFull = ViewModel.ProtoService.GetBasicGroupFull(chat);
                if (basicGroupFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, chat, basicGroupFull.Photo);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => PhotoInfo);
            }
            else if (chat.Type is ChatTypeSupergroup)
            {
                var supergroupFull = ViewModel.ProtoService.GetSupergroupFull(chat);
                if (supergroupFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, chat, supergroupFull.Photo);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => PhotoInfo);
            }
        }

        private void Notifications_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (toggle.FocusState != FocusState.Unfocused)
            {
                ViewModel.ToggleMuteCommand.Execute(toggle.IsOn);
            }
        }

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            ProfileHeader?.UpdateChat(chat);

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;
        }

        public void UpdateChatTitle(Chat chat)
        {
            ProfileHeader?.UpdateChatTitle(chat);
            TitleInfo.Text = ViewModel.ProtoService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            ProfileHeader?.UpdateChatPhoto(chat);
            PhotoInfo.SetChat(ViewModel.ProtoService, chat, 64);
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            ProfileHeader?.UpdateChatNotificationSettings(chat);
        }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            ProfileHeader?.UpdateUser(chat, user, secret);

            SubtitleInfo.Text = LastSeenConverter.GetLabel(user, true);

            // Unused:
            GroupInvite.Visibility = Visibility.Collapsed;
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            ProfileHeader?.UpdateUserFullInfo(chat, user, fullInfo, secret, accessToken);

            //UserCommonChats.Badge = fullInfo.GroupInCommonCount;
            //UserCommonChats.Visibility = fullInfo.GroupInCommonCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            Call.Visibility = fullInfo.CanBeCalled ? Visibility.Visible : Visibility.Collapsed;
            VideoCall.Visibility = fullInfo.CanBeCalled && fullInfo.SupportsVideoCalls ? Visibility.Visible : Visibility.Collapsed;

            Edit.Visibility = Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            ProfileHeader?.UpdateUserStatus(chat, user);
            SubtitleInfo.Text = LastSeenConverter.GetLabel(user, true);
        }



        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            ProfileHeader?.UpdateSecretChat(chat, secretChat);
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            ProfileHeader?.UpdateBasicGroup(chat, group);

            SubtitleInfo.Text = Locale.Declension("Members", group.MemberCount);

            GroupInvite.Visibility = group.Status is ChatMemberStatusCreator || (group.Status is ChatMemberStatusAdministrator administrator && administrator.CanInviteUsers) || chat.Permissions.CanInviteUsers ? Visibility.Visible : Visibility.Collapsed;

            Edit.Visibility = chat.Permissions.CanChangeInfo || group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator ? Visibility.Visible : Visibility.Collapsed;
            Edit.Glyph = Icons.Edit;

            ToolTipService.SetToolTip(Edit, Strings.Resources.ChannelEdit);

            // Unused:
            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ProfileHeader?.UpdateBasicGroupFullInfo(chat, group, fullInfo);
            ViewModel.Members = new SortedObservableCollection<ChatMember>(new ChatMemberComparer(ViewModel.ProtoService, true), fullInfo.Members);
        }



        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            ProfileHeader?.UpdateSupergroup(chat, group);

            SubtitleInfo.Text = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", group.MemberCount);

            Automation.SetToolTip(Edit, group.IsChannel ? Strings.Resources.ManageChannelMenu : Strings.Resources.ManageGroupMenu);

            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;

            Edit.Visibility = group.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator ? Visibility.Visible : Visibility.Collapsed;
            Edit.Glyph = Icons.Edit;

            GroupInvite.Visibility = !group.IsChannel && (group.Status is ChatMemberStatusCreator || (group.Status is ChatMemberStatusAdministrator administrator && administrator.CanInviteUsers) || chat.Permissions.CanInviteUsers) ? Visibility.Visible : Visibility.Collapsed;

            if (!group.IsChannel && (ViewModel.Members == null || group.MemberCount < 200 && group.MemberCount != ViewModel.Members.Count))
            {
                ViewModel.Members = ViewModel.CreateMembers(group.Id);
            }
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ProfileHeader?.UpdateSupergroupFullInfo(chat, group, fullInfo);
        }

        #endregion

        #region Context menu

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var userId = chat.Type is ChatTypePrivate privata ? privata.UserId : chat.Type is ChatTypeSecret secret ? secret.UserId : 0;
                if (userId != ViewModel.CacheService.Options.MyId)
                {
                    var user = ViewModel.CacheService.GetUser(userId);
                    if (user == null)
                    {
                        return;
                    }

                    var fullInfo = ViewModel.CacheService.GetUserFull(userId);
                    if (fullInfo == null)
                    {
                        return;
                    }

                    //if (fullInfo.CanBeCalled)
                    //{
                    //    callItem = menu.addItem(call_item, R.drawable.ic_call_white_24dp);
                    //}
                    if (user.IsContact)
                    {
                        flyout.CreateFlyoutItem(ViewModel.ShareCommand, Strings.Resources.ShareContact, new FontIcon { Glyph = Icons.Share });
                        flyout.CreateFlyoutItem(chat.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, chat.IsBlocked ? Strings.Resources.Unblock : Strings.Resources.BlockContact, new FontIcon { Glyph = chat.IsBlocked ? Icons.Block : Icons.Block });
                        flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.Resources.EditContact, new FontIcon { Glyph = Icons.Edit });
                        flyout.CreateFlyoutItem(ViewModel.DeleteCommand, Strings.Resources.DeleteContact, new FontIcon { Glyph = Icons.Delete });
                    }
                    else
                    {
                        if (user.Type is UserTypeBot bot)
                        {
                            if (bot.CanJoinGroups)
                            {
                                flyout.CreateFlyoutItem(ViewModel.InviteCommand, Strings.Resources.BotInvite, new FontIcon { Glyph = Icons.PersonAdd });
                            }

                            flyout.CreateFlyoutItem(() => { }, Strings.Resources.BotShare, new FontIcon { Glyph = Icons.Share });
                        }
                        else
                        {
                            flyout.CreateFlyoutItem(ViewModel.AddCommand, Strings.Resources.AddContact, new FontIcon { Glyph = Icons.PersonAdd });
                        }

                        if (user.PhoneNumber.Length > 0)
                        {
                            flyout.CreateFlyoutItem(ViewModel.ShareCommand, Strings.Resources.ShareContact, new FontIcon { Glyph = Icons.Share });
                            flyout.CreateFlyoutItem(chat.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, chat.IsBlocked ? Strings.Resources.Unblock : Strings.Resources.BlockContact, new FontIcon { Glyph = chat.IsBlocked ? Icons.Block : Icons.Block });
                        }
                        else
                        {
                            if (user.Type is UserTypeBot)
                            {
                                flyout.CreateFlyoutItem(chat.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, chat.IsBlocked ? Strings.Resources.BotRestart : Strings.Resources.BotStop, new FontIcon { Glyph = chat.IsBlocked ? Icons.Block : Icons.Block });
                            }
                            else
                            {
                                flyout.CreateFlyoutItem(chat.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, chat.IsBlocked ? Strings.Resources.Unblock : Strings.Resources.BlockContact, new FontIcon { Glyph = chat.IsBlocked ? Icons.Block : Icons.Block });
                            }
                        }
                    }
                }
                else
                {
                    flyout.CreateFlyoutItem(ViewModel.ShareCommand, Strings.Resources.ShareContact, new FontIcon { Glyph = Icons.Share });
                }
            }
            //if (writeButton != null)
            //{
            //    boolean isChannel = ChatObject.isChannel(currentChat);
            //    if (isChannel && !ChatObject.canChangeChatInfo(currentChat) || !isChannel && !currentChat.admin && !currentChat.creator && currentChat.admins_enabled)
            //    {
            //        writeButton.setImageResource(R.drawable.floating_message);
            //        writeButton.setPadding(0, AndroidUtilities.dp(3), 0, 0);
            //    }
            //    else
            //    {
            //        writeButton.setImageResource(R.drawable.floating_camera);
            //        writeButton.setPadding(0, 0, 0, 0);
            //    }
            //}
            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                var fullInfo = ViewModel.ProtoService.GetSupergroupFull(super.SupergroupId);

                if (supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    if (chat.VideoChat.GroupCallId == 0 && supergroup.CanManageVideoChats())
                    {
                        flyout.CreateFlyoutItem(ViewModel.CallCommand, false, supergroup.IsChannel ? Strings.Resources.StartVoipChannel : Strings.Resources.StartVoipChat, new FontIcon { Glyph = Icons.VideoChat });
                    }

                    if (supergroup.IsChannel)
                    {
                        //flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.Resources.ManageChannelMenu, new FontIcon { Glyph = Icons.Edit });
                    }
                    else
                    {
                        flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.Resources.ManageGroupMenu, new FontIcon { Glyph = Icons.Edit });
                    }
                }

                if (fullInfo != null && fullInfo.CanGetStatistics)
                {
                    flyout.CreateFlyoutItem(ViewModel.StatisticsCommand, Strings.Resources.Statistics, new FontIcon { Glyph = Icons.DataUsage });
                }

                if (!super.IsChannel)
                {
                    flyout.CreateFlyoutItem(ViewModel.MembersCommand, Strings.Resources.SearchMembers, new FontIcon { Glyph = Icons.Search });

                    if (supergroup.Status is not ChatMemberStatusCreator and not ChatMemberStatusLeft and not ChatMemberStatusBanned)
                    {
                        flyout.CreateFlyoutItem(ViewModel.DeleteCommand, Strings.Resources.LeaveMegaMenu, new FontIcon { Glyph = Icons.Delete });
                    }
                }
                else if (supergroup.HasLinkedChat)
                {
                    flyout.CreateFlyoutItem(ViewModel.DiscussCommand, Strings.Resources.ViewDiscussion, new FontIcon { Glyph = Icons.Comment });
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var basicGroup = ViewModel.ProtoService.GetBasicGroup(basic.BasicGroupId);
                if (basicGroup == null)
                {
                    return;
                }

                if (chat.VideoChat.GroupCallId == 0 && basicGroup.CanManageVideoChats())
                {
                    flyout.CreateFlyoutItem(ViewModel.CallCommand, false, Strings.Resources.StartVoipChat, new FontIcon { Glyph = Icons.VideoChat });
                }

                if (chat.Permissions.CanChangeInfo || basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator)
                {
                    flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.Resources.ChannelEdit, new FontIcon { Glyph = Icons.Edit });
                }

                flyout.CreateFlyoutItem(ViewModel.MembersCommand, Strings.Resources.SearchMembers, new FontIcon { Glyph = Icons.Search });

                flyout.CreateFlyoutItem(ViewModel.DeleteCommand, Strings.Resources.DeleteAndExit, new FontIcon { Glyph = Icons.Delete });
            }

            //flyout.CreateFlyoutItem(null, Strings.Resources.AddShortcut, new FontIcon { Glyph = Icons.Pin });

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(sender as Button, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
            }
        }

        #endregion

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (ProfileHeader != null)
            {
                UnloadObject(ProfileHeader);
            }
        }
    }
}
