//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChatInviteFallbackPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly ChatInviteLink _inviteLink;

        public ChatInviteFallbackPopup(IClientService clientService, long chatId, IList<FailedToAddMember> members)
        {
            InitializeComponent();

            var chat = clientService.GetChat(chatId);
            var channel = chat.Type is ChatTypeSupergroup { IsChannel: true };
            var users = clientService.GetUsers(members.Select(x => x.UserId)).ToList();

            ScrollingHost.ItemsSource = users;

            _clientService = clientService;
            _inviteLink = GetInviteLink(chat);

            string message;

            if (_inviteLink != null)
            {
                Title = Strings.ChannelInviteViaLink;
                message = users.Count == 1
                    ? string.Format(channel ? Strings.InviteChannelRestrictedUsersOne : Strings.InviteRestrictedUsersOne, users[0].FullName())
                    : Locale.Declension(channel ? Strings.R.InviteChannelRestrictedUsers : Strings.R.InviteRestrictedUsers, users.Count);

                PrimaryButtonText = Strings.SendInviteLink;
                SecondaryButtonText = Strings.ActionSkip;

                ScrollingHost.SelectionMode = ListViewSelectionMode.Multiple;
                ScrollingHost.SelectAll();
            }
            else
            {
                Title = Strings.ChannelInviteViaLinkRestricted;
                message = users.Count == 1
                    ? string.Format(channel ? Strings.InviteChannelRestrictedUsers2One : Strings.InviteRestrictedUsers2One, users[0].FullName())
                    : Locale.Declension(channel ? Strings.R.InviteChannelRestrictedUsers2 : Strings.R.InviteRestrictedUsers2, users.Count);

                PrimaryButtonText = Strings.Close;
                SecondaryButtonText = string.Empty;

                ScrollingHost.SelectionMode = ListViewSelectionMode.None;
            }

            TextBlockHelper.SetMarkdown(MessageLabel, message);
        }

        private ChatInviteLink GetInviteLink(Chat chat)
        {
            if (_clientService.TryGetSupergroupFull(chat, out var supergroup))
            {
                return supergroup.InviteLink;
            }
            else if (_clientService.TryGetBasicGroupFull(chat, out var basicGroup))
            {
                return basicGroup.InviteLink;
            }

            return null;
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_inviteLink != null)
            {
                foreach (var user in ScrollingHost.SelectedItems.OfType<User>())
                {
                    var chat = await _clientService.SendAsync(new CreatePrivateChat(user.Id, false)) as Chat;
                    if (chat != null)
                    {
                        _clientService.Send(new SendMessage(chat.Id, null, null, null, new InputMessageText(_inviteLink.InviteLink.AsFormattedText(), null, false)));
                    }
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = ScrollingHost.SelectionMode == ListViewSelectionMode.Multiple
                && ScrollingHost.SelectedItems.Count > 0;
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new MultipleListViewItem(sender, false);
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatShareCell content)
            {
                content.UpdateState(args.ItemContainer.IsSelected, false, true);
                content.UpdateUser(_clientService, args, OnContainerContentChanging);
            }
        }

        #endregion
    }
}
