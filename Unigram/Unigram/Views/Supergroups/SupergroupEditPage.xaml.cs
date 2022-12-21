using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels;
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
            Title = Strings.Resources.ChannelEdit;
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

        private async void CreatePhoto_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.NavigationService.ShowAsync(typeof(CreateChatPhotoPopup), new CreateChatPhotoParameters(ViewModel.Chat?.Id, false));
        }

        #region Binding

        private string ConvertHistory(int available)
        {
            return ViewModel.AllHistoryAvailableOptions[available].Value
                ? Strings.Resources.ChatHistoryVisibleInfo
                : Strings.Resources.ChatHistoryHiddenInfo;
        }

        #endregion

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            //UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

            if (chat.AvailableReactions is ChatAvailableReactionsAll)
            {
                Reactions.Badge = Strings.Resources.AllReactions;
            }
            else if (chat.AvailableReactions is ChatAvailableReactionsSome some)
            {
                if (some.Reactions.Count > 0)
                {
                    Reactions.Badge = some.Reactions.Count.ToString("N0");
                }
                else
                {
                    Reactions.Badge = Strings.Resources.ReactionsOff;
                }
            }
        }

        public void UpdateChatTitle(Chat chat)
        {
            TitleLabel.Text = ViewModel.ClientService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.SetChat(ViewModel.ClientService, chat, 64);
        }

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            TitleLabel.PlaceholderText = group.IsChannel ? Strings.Resources.EnterChannelName : Strings.Resources.GroupName;

            Delete.Content = group.IsChannel ? Strings.Resources.ChannelDelete : Strings.Resources.DeleteMega;
            DeletePanel.Footer = group.IsChannel ? Strings.Resources.ChannelDeleteInfo : Strings.Resources.MegaDeleteInfo;

            Members.Content = group.IsChannel ? Strings.Resources.ChannelSubscribers : Strings.Resources.ChannelMembers;

            ViewModel.Title = chat.Title;
            ViewModel.IsSignatures = group.SignMessages;

            ChatType.Glyph = group.IsChannel ? Icons.Megaphone : Icons.People;

            Photo.IsEnabled = group.CanChangeInfo();
            TitleLabel.IsReadOnly = !group.CanChangeInfo();
            About.IsReadOnly = !group.CanChangeInfo();

            ChatType.Content = group.IsChannel ? Strings.Resources.ChannelType : Strings.Resources.GroupType;
            ChatType.Glyph = group.IsChannel ? Icons.Megaphone : Icons.People;
            ChatType.Visibility = Visibility.Collapsed;
            ChatType.Badge = group.HasActiveUsername()
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

            ChatHistory.Visibility = group.CanChangeInfo() && !group.HasActiveUsername() && !group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            InviteLinkPanel.Visibility = group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;
            ChannelSignMessagesPanel.Visibility = group.CanChangeInfo() && group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            GroupStickersPanel.Visibility = Visibility.Collapsed;

            ChatLinked.Visibility = group.IsChannel ? Visibility.Visible : group.HasLinkedChat ? Visibility.Visible : Visibility.Collapsed;
            ChatLinked.Content = group.IsChannel ? Strings.Resources.Discussion : Strings.Resources.LinkedChannel;
            ChatLinked.Glyph = group.IsChannel ? Icons.Comment : Icons.Megaphone;
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
            ViewModel.IsAllHistoryAvailable = fullInfo.IsAllHistoryAvailable ? 0 : 1;

            ChatType.Visibility = fullInfo.CanSetUsername ? Visibility.Visible : Visibility.Collapsed;

            var linkedChat = ViewModel.ClientService.GetChat(fullInfo.LinkedChatId);
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
                if (group.HasActiveUsername(out string username))
                {
                    InviteLink.Text = MeUrlPrefixConverter.Convert(ViewModel.ClientService, group.ActiveUsername());
                    RevokeLink.Visibility = Visibility.Collapsed;
                    InviteLinkPanel.Visibility = Visibility.Visible;
                }
                else if (fullInfo.InviteLink != null)
                {
                    InviteLink.Text = fullInfo.InviteLink?.InviteLink;
                    RevokeLink.Visibility = Visibility.Visible;
                    InviteLinkPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    InviteLinkPanel.Visibility = Visibility.Collapsed;
                    ViewModel.ClientService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
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

            ViewModel.ClientService.Send(new GetStickerSet(fullInfo.StickerSetId), result =>
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
            TitleLabel.PlaceholderText = Strings.Resources.GroupName;

            Delete.Content = Strings.Resources.DeleteMega;
            DeletePanel.Footer = Strings.Resources.MegaDeleteInfo;

            Members.Content = Strings.Resources.ChannelMembers;

            ViewModel.Title = chat.Title;
            ViewModel.IsSignatures = false;
            ViewModel.IsAllHistoryAvailable = 1;


            //Photo.IsEnabled = group.CanChangeInfo();
            //Title.IsReadOnly = !group.CanChangeInfo();
            //About.IsReadOnly = !group.CanChangeInfo();

            ChatType.Content = Strings.Resources.GroupType;
            ChatType.Badge = Strings.Resources.TypePrivateGroup;
            ChatType.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

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
                    ViewModel.ClientService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
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
