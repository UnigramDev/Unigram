//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.Views.Popups
{
    public sealed partial class JoinChatPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public JoinChatPopup(IClientService clientService, ChatInviteLinkInfo info)
        {
            InitializeComponent();

            _clientService = clientService;

            Photo.Source = ProfilePictureSource.Chat(clientService, info);

            Identity.SetStatus(clientService, info, BotVerified);

            Title.Text = info.Title;

            if (info.CreatesJoinRequest)
            {
                Subtitle.Text = Locale.Declension(info.Type is InviteLinkChatTypeChannel ? Strings.R.Subscribers : Strings.R.Members, info.MemberCount);
            }
            else
            {
                Subtitle.Text = (info.Type is InviteLinkChatTypeChannel ? info.IsPublic ? Strings.ChannelPublic : Strings.ChannelPrivate : info.IsPublic ? Strings.MegaPublic : Strings.MegaPrivate).ToLower();
            }

            if (string.IsNullOrEmpty(info.Description))
            {
                Description.Visibility = Visibility.Collapsed;
            }
            else
            {
                Description.Text = info.Description;
            }

            PrimaryButtonText = info.CreatesJoinRequest ? Strings.RequestToJoin2 : Strings.ChannelJoin2;
            SecondaryButtonText = Strings.Cancel;

            Participants.ItemSize = 36;
            Participants.ItemOverlap = 16;

            if (info.CreatesJoinRequest)
            {
                Participants.Visibility = Visibility.Collapsed;
                TextBlockHelper.SetMarkdown(JoinRequestInfo, Strings.RequestToJoinChannelDescription);
            }
            else
            {
                var participantIds = info.MemberUserIds.Select(x => (MessageSender)new MessageSenderUser(x)).ToList();
                if (participantIds.Count == 1 && info.MemberCount == 1)
                {
                    var participant1 = clientService.GetTitle(participantIds[0]);

                    Participants.Items.ReplaceDiff(participantIds);
                    TextBlockHelper.SetMarkdown(JoinRequestInfo, string.Format(Strings.GroupJoinLinkText2One, participant1));
                }
                else if (participantIds.Count == 2 && info.MemberCount == 2)
                {
                    var participant1 = clientService.GetTitle(participantIds[0]);
                    var participant2 = clientService.GetTitle(participantIds[1]);

                    Participants.Items.ReplaceDiff(participantIds);
                    TextBlockHelper.SetMarkdown(JoinRequestInfo, string.Format(Strings.GroupJoinLinkText2Two, participant1, participant2));
                }
                else if (participantIds.Count >= 2 && info.MemberCount >= 3)
                {
                    var participant1 = clientService.GetTitle(participantIds[0]);
                    var participant2 = clientService.GetTitle(participantIds[1]);

                    Participants.Items.ReplaceDiff(participantIds);
                    TextBlockHelper.SetMarkdown(JoinRequestInfo, Locale.Declension(Strings.R.GroupJoinLinkText2Many, info.MemberCount - 2, participant1, participant2));
                }
                else if (info.MemberCount > 0)
                {
                    Participants.Visibility = Visibility.Collapsed;
                    TextBlockHelper.SetMarkdown(JoinRequestInfo, Locale.Declension(Strings.R.GroupJoinLinkText2Unknown, info.MemberCount));
                }
                else
                {
                    Participants.Visibility = Visibility.Collapsed;
                    JoinRequestInfo.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void Participants_RecentUserHeadChanged(ProfilePicture sender, MessageSender messageSender)
        {
            sender.Source = ProfilePictureSource.MessageSender(_clientService, messageSender);
        }
    }
}
