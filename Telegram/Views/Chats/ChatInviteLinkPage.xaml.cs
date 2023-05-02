//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Chats
{
    public sealed partial class ChatInviteLinkPage : HostedPage
    {
        public ChatInviteLinkViewModel ViewModel => DataContext as ChatInviteLinkViewModel;

        public ChatInviteLinkPage()
        {
            InitializeComponent();
            Title = Strings.InviteLink;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            DataTransferManager.GetForCurrentView().DataRequested += OnDataRequested;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;
        }

        private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (ViewModel.InviteLink != null)
            {
                args.Request.Data.Properties.Title = ViewModel.Chat.Title;
                args.Request.Data.SetWebLink(new Uri(ViewModel.InviteLink));
            }
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        #region Binding

        private string ConvertType(string broadcast, string mega)
        {
            //if (ViewModel.Item is TLChannel channel)
            //{
            //    return Locale.GetString(channel.IsBroadcast ? broadcast : mega);
            //}

            return Locale.GetString(mega);
        }

        #endregion

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            if (content == null)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;
            content.Tag = args.Item;


            switch (args.Item)
            {
                case ChatInviteLinkMember member:
                    UpdateChatInviteLinkMember(member, content, args.Phase);
                    break;
                case ChatInviteLink link:
                    UpdateChatInviteLink(link, content, args.Phase);
                    break;
                case ChatInviteLinkCount count:
                    UpdateChatInviteLinkCount(count, content, args.Phase);
                    break;
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        private void UpdateChatInviteLink(ChatInviteLink link, Grid content, uint phase)
        {
            if (phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = link.InviteLink;
            }
            else if (phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                //subtitle.Text = Locale.Declension("InviteLinkCount", member.InviteLinkCount);
            }
            else if (phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderImage.GetGlyph(Icons.Link, 5);
            }
        }

        private void UpdateChatInviteLinkMember(ChatInviteLinkMember member, Grid content, uint phase)
        {
            var user = ViewModel.ClientService.GetUser(member.UserId);
            if (user == null)
            {
                return;
            }

            if (phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.FullName();
            }
            else if (phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                //subtitle.Text = Locale.Declension("InviteLinkCount", member.InviteLinkCount);
            }
            else if (phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetUser(ViewModel.ClientService, user, 36);
            }
        }

        private void UpdateChatInviteLinkCount(ChatInviteLinkCount count, Grid content, uint phase)
        {
            var user = ViewModel.ClientService.GetUser(count.UserId);
            if (user == null)
            {
                return;
            }

            if (phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.FullName();
            }
            else if (phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = Locale.Declension(Strings.R.InviteLinkCount, count.InviteLinkCount);
            }
            else if (phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetUser(ViewModel.ClientService, user, 36);
            }
        }
    }
}
