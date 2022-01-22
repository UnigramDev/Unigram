using System;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Unigram.Views.Popups;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditPage : HostedPage, ISupergroupEditDelegate
    {
        public SupergroupEditViewModel ViewModel => DataContext as SupergroupEditViewModel;

        public SupergroupEditPage()
        {
            InitializeComponent();
        }

        private async void EditPhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.MediaTypes);

                var media = await picker.PickSingleMediaAsync();
                if (media != null)
                {
                    var dialog = new EditMediaPopup(media, ImageCropperMask.Ellipse);

                    var confirm = await dialog.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        ViewModel.EditPhotoCommand.Execute(media);
                    }
                }
            }
            catch { }
        }

        #region Binding

        private string ConvertHistory(bool available)
        {
            return available ? Strings.Resources.ChatHistoryVisible : Strings.Resources.ChatHistoryHidden;
        }

        #endregion

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            //UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

            Reactions.Badge = string.Format("{0}/{1}", chat.AvailableReactions.Count, ViewModel.CacheService.Reactions.Count(x => x.Value.IsActive));
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title.Text = ViewModel.ProtoService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.SetChat(ViewModel.ProtoService, chat, 64);
        }

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Title.PlaceholderText = group.IsChannel ? Strings.Resources.EnterChannelName : Strings.Resources.GroupName;

            Delete.Content = group.IsChannel ? Strings.Resources.ChannelDelete : Strings.Resources.DeleteMega;
            DeletePanel.Footer = group.IsChannel ? Strings.Resources.ChannelDeleteInfo : Strings.Resources.MegaDeleteInfo;

            Members.Content = group.IsChannel ? Strings.Resources.ChannelSubscribers : Strings.Resources.ChannelMembers;

            ViewModel.Title = chat.Title;
            ViewModel.IsSignatures = group.SignMessages;


            Photo.IsEnabled = group.CanChangeInfo();
            Title.IsReadOnly = !group.CanChangeInfo();
            About.IsReadOnly = !group.CanChangeInfo();

            ChatType.Content = group.IsChannel ? Strings.Resources.ChannelType : Strings.Resources.GroupType;
            ChatType.Visibility = Visibility.Collapsed;
            ChatType.Badge = group.Username.Length > 0
                ? group.IsChannel
                    ? Strings.Resources.TypePublic
                    : Strings.Resources.TypePublicGroup
                : group.IsChannel
                    ? chat.HasProtectedContent
                        ? Strings.Resources.TypePrivateRestrictedForwards
                        : Strings.Resources.TypePrivate
                    : chat.HasProtectedContent
                        ? Strings.Resources.TypePrivateGroupRestrictedForwards
                        : Strings.Resources.TypePrivateGroup;

            ChatHistory.Badge = null;
            ChatHistory.Visibility = group.CanChangeInfo() && string.IsNullOrEmpty(group.Username) && !group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            InviteLinkPanel.Visibility = group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;
            ChannelSignMessagesPanel.Visibility = group.CanChangeInfo() && group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            GroupStickersPanel.Visibility = Visibility.Collapsed;

            ChatLinked.Visibility = group.IsChannel ? Visibility.Visible : group.HasLinkedChat ? Visibility.Visible : Visibility.Collapsed;
            ChatLinked.Content = group.IsChannel ? Strings.Resources.Discussion : Strings.Resources.LinkedChannel;
            ChatLinked.Badge = group.HasLinkedChat ? string.Empty : Strings.Resources.DiscussionInfo;

            Permissions.Badge = string.Format("{0}/{1}", chat.Permissions.Count(), chat.Permissions.Total());
            Permissions.Visibility = group.IsChannel || !group.CanRestrictMembers() ? Visibility.Collapsed : Visibility.Visible;
            Blacklist.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            GroupStickersPanel.Visibility = fullInfo.CanSetStickerSet ? Visibility.Visible : Visibility.Collapsed;


            ViewModel.About = fullInfo.Description;
            ViewModel.IsAllHistoryAvailable = fullInfo.IsAllHistoryAvailable;

            ChatType.Visibility = fullInfo.CanSetUsername ? Visibility.Visible : Visibility.Collapsed;
            ChatHistory.Badge = fullInfo.IsAllHistoryAvailable ? Strings.Resources.ChatHistoryVisible : Strings.Resources.ChatHistoryHidden;

            var linkedChat = ViewModel.CacheService.GetChat(fullInfo.LinkedChatId);
            if (linkedChat != null)
            {
                ChatLinked.Badge = linkedChat.Title;
            }
            else
            {
                ChatLinked.Badge = Strings.Resources.DiscussionInfo;
            }

            Admins.Badge = fullInfo.AdministratorCount;
            Members.Badge = fullInfo.MemberCount;
            Blacklist.Badge = fullInfo.BannedCount;

            if (group.CanInviteUsers())
            {
                if (fullInfo.InviteLink == null && string.IsNullOrEmpty(group.Username))
                {
                    InviteLinkPanel.Visibility = Visibility.Collapsed;
                    ViewModel.ProtoService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
                }
                else if (string.IsNullOrEmpty(group.Username))
                {
                    InviteLink.Text = fullInfo.InviteLink?.InviteLink;
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

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

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
            DeletePanel.Footer = Strings.Resources.MegaDeleteInfo;

            Members.Content = Strings.Resources.ChannelMembers;

            ViewModel.Title = chat.Title;
            ViewModel.IsSignatures = false;
            ViewModel.IsAllHistoryAvailable = false;


            //Photo.IsEnabled = group.CanChangeInfo();
            //Title.IsReadOnly = !group.CanChangeInfo();
            //About.IsReadOnly = !group.CanChangeInfo();

            ChatType.Content = Strings.Resources.GroupType;
            ChatType.Badge = Strings.Resources.TypePrivateGroup;
            ChatType.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            ChatHistory.Badge = Strings.Resources.ChatHistoryHidden;
            ChatHistory.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            InviteLinkPanel.Visibility = group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;
            ChatLinked.Visibility = Visibility.Collapsed;
            ChannelSignMessagesPanel.Visibility = Visibility.Collapsed;
            GroupStickersPanel.Visibility = Visibility.Collapsed;

            Permissions.Badge = string.Format("{0}/{1}", chat.Permissions.Count(), chat.Permissions.Total());
            Permissions.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;
            Blacklist.Visibility = Visibility.Collapsed;

            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            GroupStickersPanel.Visibility = Visibility.Collapsed;

            Admins.Badge = fullInfo.Members.Count(x => x.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator);
            Members.Badge = fullInfo.Members.Count;
            Blacklist.Badge = 0;

            if (group.CanInviteUsers())
            {
                if (fullInfo.InviteLink == null)
                {
                    InviteLinkPanel.Visibility = Visibility.Collapsed;
                    ViewModel.ProtoService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
                }
                else
                {
                    InviteLink.Text = fullInfo.InviteLink?.InviteLink;
                    RevokeLink.Visibility = Visibility.Visible;
                    InviteLinkPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                InviteLinkPanel.Visibility = Visibility.Collapsed;
            }

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        #endregion
    }
}
