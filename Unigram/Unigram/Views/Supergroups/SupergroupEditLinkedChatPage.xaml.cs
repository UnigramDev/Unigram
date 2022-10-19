using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditLinkedChatPage : HostedPage, ISupergroupDelegate
    {
        public SupergroupEditLinkedChatViewModel ViewModel => DataContext as SupergroupEditLinkedChatViewModel;

        public SupergroupEditLinkedChatPage()
        {
            InitializeComponent();
            Title = Strings.Resources.Discussion;
        }

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var content = button.Content as Grid;

            var chat = button.DataContext as Chat;

            var title = content.Children[1] as TextBlock;
            title.Text = ViewModel.ClientService.GetTitle(chat);

            if (ViewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                var subtitle = content.Children[2] as TextBlock;
                if (supergroup.HasActiveUsername(out string username))
                {
                    subtitle.Text = $"@{username}";
                }
                else
                {
                    subtitle.Text = Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount);
                }
            }

            var photo = content.Children[0] as ProfilePicture;
            photo.SetChat(ViewModel.ClientService, chat, 36);

            button.Command = ViewModel.LinkCommand;
            button.CommandParameter = chat;
        }

        #region Delegate

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            TextBlockHelper.SetMarkdown(Headline, string.Format(Strings.Resources.DiscussionChannelGroupSetHelp2, chat.Title));

            Create.Visibility = group.HasLinkedChat ? Visibility.Collapsed : Visibility.Visible;
            Unlink.Visibility = group.HasLinkedChat ? Visibility.Visible : Visibility.Collapsed;
            Unlink.Content = group.IsChannel ? Strings.Resources.DiscussionUnlinkGroup : Strings.Resources.DiscussionUnlinkChannel;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            var linkedChat = ViewModel.ClientService.GetChat(fullInfo.LinkedChatId);
            if (linkedChat != null)
            {
                if (group.IsChannel)
                {
                    TextBlockHelper.SetMarkdown(Headline, string.Format(Strings.Resources.DiscussionChannelGroupSetHelp2, linkedChat.Title));
                    LayoutRoot.Footer = Strings.Resources.DiscussionChannelHelp2;
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Headline, string.Format(Strings.Resources.DiscussionGroupHelp, linkedChat.Title));
                    LayoutRoot.Footer = Strings.Resources.DiscussionChannelHelp2;
                }

                JoinToSendMessages.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                JoinToSendMessages.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        #endregion

        #region Binding

        private string ConvertJoinToSendMessages(bool joinToSendMessages)
        {
            return joinToSendMessages ? Strings.Resources.ChannelSettingsJoinRequestInfo : Strings.Resources.ChannelSettingsJoinToSendInfo;
        }

        #endregion
    }
}
