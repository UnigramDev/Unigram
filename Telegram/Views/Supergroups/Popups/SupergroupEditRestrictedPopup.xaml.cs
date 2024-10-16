//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;
using Telegram.Views.Popups;

namespace Telegram.Views.Supergroups.Popups
{
    public sealed partial class SupergroupEditRestrictedPopup : ContentPopup, IMemberPopupDelegate
    {
        public SupergroupEditRestrictedViewModel ViewModel => DataContext as SupergroupEditRestrictedViewModel;

        public SupergroupEditRestrictedPopup()
        {
            InitializeComponent();
            Title = Strings.UserRestrictions;

            PrimaryButtonText = Strings.Done;
            SecondaryButtonText = Strings.Cancel;
        }

        #region Binding

        private string ConvertUntilDate(int date)
        {
            if (date == 0)
            {
                return Strings.UserRestrictionsUntilForever;
            }

            return Formatter.DateAt(date);
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
            Cell.Height = double.NaN;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Cell.Subtitle = LastSeenConverter.GetLabel(user, true);
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken) { }

        public void UpdateMember(Chat chat, User user, ChatMember member)
        {
            if (member.Status is ChatMemberStatusRestricted restricted)
            {
                CloseButtonText = Strings.UserRestrictionsBlock;

                if (restricted.RestrictedUntilDate != 0)
                {
                    InsertDuration(restricted.RestrictedUntilDate);
                }
                else
                {
                    InsertDuration(int.MaxValue);
                }
            }
            else
            {
                InsertDuration(int.MaxValue);
            }

            PermissionsPanel.Visibility = Visibility.Visible;

            CanSendBasicMessages.IsEnabled = chat.Permissions.CanSendBasicMessages;
            CanSendPhotos.IsEnabled = chat.Permissions.CanSendPhotos;
            CanSendVideos.IsEnabled = chat.Permissions.CanSendVideos;
            CanSendOtherMessages.IsEnabled = chat.Permissions.CanSendOtherMessages;
            CanSendAudios.IsEnabled = chat.Permissions.CanSendAudios;
            CanSendDocuments.IsEnabled = chat.Permissions.CanSendDocuments;
            CanSendVoiceNotes.IsEnabled = chat.Permissions.CanSendVoiceNotes;
            CanSendVideoNotes.IsEnabled = chat.Permissions.CanSendVideoNotes;
            CanSendPolls.IsEnabled = chat.Permissions.CanSendPolls;
            CanAddLinkPreviews.IsEnabled = chat.Permissions.CanAddLinkPreviews;
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

        private async void Duration_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Duration.SelectedItem is SelectionValue value)
            {
                if (value.Value == int.MinValue)
                {
                    var popup = new ChooseDateTimeToast
                    {
                        Title = Strings.ExpireAfter,
                        //Header = Strings.PaidContentPriceTitle,
                        ActionButtonContent = Strings.OK,
                        ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                        CloseButtonContent = Strings.Cancel,
                        PreferredPlacement = TeachingTipPlacementMode.Center,
                        IsLightDismissEnabled = true,
                        ShouldConstrainToRootBounds = true,
                    };

                    var confirm = await popup.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        InsertDuration((int)popup.Value.ToTimestamp());
                    }
                    else if (e.RemovedItems?.Count > 0)
                    {
                        Duration.SelectedItem = e.RemovedItems[0];
                    }
                }
                else if (value.IsCustom is false)
                {
                    for (int i = 0; i < ViewModel.Duration.Count; i++)
                    {
                        if (ViewModel.Duration[i].IsCustom)
                        {
                            ViewModel.Duration.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private void InsertDuration(int value)
        {
            for (int i = 0; i < ViewModel.Duration.Count; i++)
            {
                if (ViewModel.Duration[i].Value == value)
                {
                    Duration.SelectedIndex = i;
                    break;
                }
                else if (ViewModel.Duration[i].Value > value)
                {
                    ViewModel.Duration.Insert(i, new SelectionValue(value, Formatter.DateAt(value), true));
                    Duration.SelectedIndex = i;
                    break;
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Secondary);
        }
    }
}
