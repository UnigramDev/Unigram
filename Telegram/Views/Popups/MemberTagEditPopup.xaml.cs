//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class MemberTagEditPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly Chat _chat;
        private readonly ChatMember _member;

        public MemberTagEditPopup(IClientService clientService, IEventAggregator aggregator, Chat chat, ChatMember member)
        {
            InitializeComponent();

            _clientService = clientService;
            _aggregator = aggregator;

            _chat = chat;
            _member = member;

            Field.Text = member.Tag;

            Title = Strings.MemberTagTitle;
            SecondaryButtonText = Strings.Cancel;

            UpdatePrimaryButtonText();

            BackgroundControl.Update(clientService, null);
            Message.UpdateMockup(clientService, chat, member.MemberId, member.GetTag(), member.Status switch
            {
                ChatMemberStatusCreator => ChatMemberRank.Owner,
                ChatMemberStatusAdministrator => ChatMemberRank.Admin,
                _ => ChatMemberRank.Other
            });
        }

        private bool _submitted;

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_submitted || _member.MemberId is not MessageSenderUser user)
            {
                return;
            }

            _submitted = true;
            IsPrimaryButtonPending = true;

            var deferral = args.GetDeferral();

            var response = await _clientService.SendAsync(new SetChatMemberTag(_chat.Id, user.UserId, Field.Text));
            if (response is Error error)
            {
                args.Cancel = true;
                ToastPopup.ShowError(XamlRoot, error);

                _submitted = false;
                IsPrimaryButtonPending = false;
            }
            else
            {
                _member.Tag = Field.Text;
                _aggregator.Publish(new UpdateChatMember(_chat.Id, 0, 0, null, false, false, _member, _member));
            }

            deferral.Complete();
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePrimaryButtonText();
        }

        private void UpdatePrimaryButtonText()
        {
            var oldValue = !string.IsNullOrEmpty(_member.Tag);
            var newValue = !string.IsNullOrEmpty(Field.Text);

            if (oldValue && newValue)
            {
                PrimaryButtonText = Strings.MemberTagButtonEdit;
            }
            else if (oldValue && !newValue)
            {
                PrimaryButtonText = Strings.MemberTagButtonRemove;
            }
            else
            {
                PrimaryButtonText = Strings.MemberTagButtonAdd;
            }

            var tag = _member.Status switch
            {
                ChatMemberStatusCreator => string.IsNullOrEmpty(Field.Text) ? Strings.ChatTagOwner : Field.Text,
                ChatMemberStatusAdministrator => string.IsNullOrEmpty(Field.Text) ? Strings.ChatTagAdmin : Field.Text,
                _ => Field.Text
            };

            Message.UpdateMockup(_clientService, _chat, _member.MemberId, tag, _member.Status switch
            {
                ChatMemberStatusCreator => ChatMemberRank.Owner,
                ChatMemberStatusAdministrator => ChatMemberRank.Admin,
                _ => ChatMemberRank.Other
            });
        }
    }
}
