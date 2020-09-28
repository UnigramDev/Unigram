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
            DataContext = TLContainer.Current.Resolve<SupergroupEditLinkedChatViewModel, ISupergroupDelegate>(this);
        }

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var content = button.Content as Grid;

            var chat = button.DataContext as Chat;

            var title = content.Children[1] as TextBlock;
            title.Text = ViewModel.ProtoService.GetTitle(chat);

            if (ViewModel.CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                var subtitle = content.Children[2] as TextBlock;
                if (string.IsNullOrEmpty(supergroup.Username))
                {
                    subtitle.Text = Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount);
                }
                else
                {
                    subtitle.Text = $"@{supergroup.Username}";
                }
            }

            var photo = content.Children[0] as ProfilePicture;
            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);

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
            var linkedChat = ViewModel.CacheService.GetChat(fullInfo.LinkedChatId);
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
            }
            else
            {

            }
        }

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        #endregion
    }
}
