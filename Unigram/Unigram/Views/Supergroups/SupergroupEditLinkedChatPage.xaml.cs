using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditLinkedChatPage : Page, ISupergroupDelegate
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
            var chat = sender.ItemsSourceView.GetAt(args.Index) as Chat;

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
            TextBlockHelper.SetMarkdown(Headline, string.Format(Strings.Resources.DiscussionChannelGroupSetHelp, chat.Title));

            Create.Visibility = group.HasLinkedChat ? Visibility.Collapsed : Visibility.Visible;
            Unlink.Visibility = group.HasLinkedChat ? Visibility.Visible : Visibility.Collapsed;
            Unlink.Content = group.IsChannel ? Strings.Resources.DiscussionUnlinkGroup : Strings.Resources.DiscussionUnlinkChannel;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {

        }

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        #endregion
    }
}
