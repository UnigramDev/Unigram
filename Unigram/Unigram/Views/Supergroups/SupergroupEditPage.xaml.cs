using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.ViewModels;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Media.Capture;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditPage : Page, ISupergroupEditDelegate
    {
        public SupergroupEditViewModel ViewModel => DataContext as SupergroupEditViewModel;

        public SupergroupEditPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SupergroupEditViewModel, ISupergroupEditDelegate>(this);
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            var flyout = FlyoutBase.GetAttachedFlyout(sender as FrameworkElement);
            if (flyout == null)
            {
                return;
            }

            if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutShowOptions"))
            {
                flyout.ShowAt(sender as FrameworkElement, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
            }
            else
            {
                flyout.ShowAt(sender as FrameworkElement);
            }
        }

        private async void EditPhoto_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var dialog = new EditYourPhotoView(file)
                {
                    CroppingProportions = ImageCroppingProportions.Square,
                    IsCropEnabled = false
                };

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    ViewModel.EditPhotoCommand.Execute(dialog.Result);
                }
            }
        }

        private async void EditCamera_Click(object sender, RoutedEventArgs e)
        {
            var capture = new CameraCaptureUI();
            capture.PhotoSettings.AllowCropping = false;
            capture.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
            capture.PhotoSettings.MaxResolution = CameraCaptureUIMaxPhotoResolution.HighestAvailable;

            var file = await capture.CaptureFileAsync(CameraCaptureUIMode.Photo);
            if (file != null)
            {
                var dialog = new EditYourPhotoView(file)
                {
                    CroppingProportions = ImageCroppingProportions.Square,
                    IsCropEnabled = false
                };
                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogResult.Primary)
                {
                    ViewModel.EditPhotoCommand.Execute(dialog.Result);
                }
            }
        }

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            //UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title.Text = ViewModel.ProtoService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 64);
        }

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Title.PlaceholderText = group.IsChannel ? Strings.Resources.EnterChannelName : Strings.Resources.GroupName;

            Delete.Content = group.IsChannel ? Strings.Resources.ChannelDelete : Strings.Resources.DeleteMega;
            DeleteInfo.Text = group.IsChannel ? Strings.Resources.ChannelDeleteInfo : Strings.Resources.MegaDeleteInfo;

            Members.Content = group.IsChannel ? Strings.Resources.ChannelSubscribers : Strings.Resources.ChannelMembers;

            ViewModel.Title = chat.Title;
            ViewModel.IsSignatures = group.SignMessages;


            Photo.IsEnabled = group.CanChangeInfo();
            Title.IsReadOnly = !group.CanChangeInfo();
            About.IsReadOnly = !group.CanChangeInfo();

            ChatType.Content = group.IsChannel ? Strings.Resources.ChannelType : Strings.Resources.GroupType;
            ChatType.Badge = group.Username.Length > 0
                ? group.IsChannel ? Strings.Resources.TypePublic : Strings.Resources.TypePublicGroup
                : group.IsChannel ? Strings.Resources.TypePrivate : Strings.Resources.TypePrivateGroup;
            ChatType.Visibility = Visibility.Collapsed;

            ChatDemocracy.Badge = group.AnyoneCanInvite ? Strings.Resources.WhoCanAddMembersAllMembers : Strings.Resources.WhoCanAddMembersAdmins;
            ChatDemocracy.Visibility = group.CanChangeInfo() && !group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            ChatHistory.Badge = null;
            ChatHistory.Visibility = group.CanChangeInfo() && string.IsNullOrEmpty(group.Username) && !group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            InviteLinkPanel.Visibility = group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;
            ChannelSignMessagesPanel.Visibility = group.CanChangeInfo() && group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            GroupStickersPanel.Visibility = Visibility.Collapsed;

            Permissions.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
            Blacklist.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            GroupStickersPanel.Visibility = fullInfo.CanSetStickerSet ? Visibility.Visible : Visibility.Collapsed;


            ViewModel.About = fullInfo.Description;

            ChatType.Visibility = fullInfo.CanSetUsername ? Visibility.Visible : Visibility.Collapsed;
            ChatHistory.Badge = fullInfo.IsAllHistoryAvailable ? Strings.Resources.ChatHistoryVisible : Strings.Resources.ChatHistoryHidden;


            Admins.Badge = fullInfo.AdministratorCount;
            Members.Badge = fullInfo.MemberCount;
            Blacklist.Badge = fullInfo.BannedCount;

            if (group.CanInviteUsers())
            {
                if (string.IsNullOrEmpty(fullInfo.InviteLink) && string.IsNullOrEmpty(group.Username))
                {
                    InviteLinkPanel.Visibility = Visibility.Collapsed;
                    ViewModel.ProtoService.Send(new GenerateChatInviteLink(chat.Id));
                }
                else if (string.IsNullOrEmpty(group.Username))
                {
                    InviteLink.Text = fullInfo.InviteLink;
                    RevokeLink.Visibility = Visibility.Visible;
                    InviteLinkPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    InviteLink.Text = MeUrlPrefixConverter.Convert(ViewModel.CacheService, group.Username);
                    RevokeLink.Visibility = Visibility.Collapsed;
                    InviteLinkPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                InviteLinkPanel.Visibility = Visibility.Collapsed;
            }

            if (fullInfo.StickerSetId == 0 || !fullInfo.CanSetStickerSet)
            {
                return;
            }

            ViewModel.ProtoService.Send(new GetStickerSet(fullInfo.StickerSetId), result =>
            {
                this.BeginOnUIThread(() =>
                {
                    if (result is StickerSet set && ViewModel.Chat?.Id == chat.Id)
                    {
                        GroupStickers.Badge = set.Title;
                    }
                });
            });
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            Title.PlaceholderText = Strings.Resources.GroupName;

            Delete.Content = Strings.Resources.DeleteMega;
            DeleteInfo.Text = Strings.Resources.MegaDeleteInfo;

            Members.Content = Strings.Resources.ChannelMembers;

            ViewModel.Title = chat.Title;
            ViewModel.IsSignatures = false;


            //Photo.IsEnabled = group.CanChangeInfo();
            //Title.IsReadOnly = !group.CanChangeInfo();
            //About.IsReadOnly = !group.CanChangeInfo();

            ChatType.Content = Strings.Resources.GroupType;
            ChatType.Badge = Strings.Resources.TypePrivateGroup;
            ChatType.Visibility = Visibility.Collapsed;

            //ChatDemocracy.Badge = group.AnyoneCanInvite ? Strings.Resources.WhoCanAddMembersAllMembers : Strings.Resources.WhoCanAddMembersAdmins;
            //ChatDemocracy.Visibility = group.CanChangeInfo() && !group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            ChatHistory.Badge = Strings.Resources.ChatHistoryHidden;
            ChatHistory.Visibility = Visibility.Visible;

            InviteLinkPanel.Visibility = group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;
            ChannelSignMessagesPanel.Visibility = Visibility.Collapsed;
            GroupStickersPanel.Visibility = Visibility.Collapsed;

            Permissions.Visibility = Visibility.Visible;
            Blacklist.Visibility = Visibility.Collapsed;

            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            GroupStickersPanel.Visibility = Visibility.Collapsed;

            //ViewModel.About = fullInfo.Description;

            //ChatType.Visibility = fullInfo.CanSetUsername ? Visibility.Visible : Visibility.Collapsed;
            ChatType.Visibility = Visibility.Visible;


            Admins.Badge = fullInfo.Members.Count(x => x.Status is ChatMemberStatusCreator || x.Status is ChatMemberStatusAdministrator);
            Members.Badge = fullInfo.Members.Count;
            Blacklist.Badge = 0;
        }

        #endregion
    }
}
