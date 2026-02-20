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
using Telegram.Navigation;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public partial class DialogUnreadMessagesViewModel : BindableBase
    {
        private readonly DialogViewModel _viewModel;
        private readonly SearchMessagesFilter _filter;

        private readonly bool _oldToNew;

        private List<long> _messages = new();
        private long _lastMessage;

        public DialogUnreadMessagesViewModel(DialogViewModel viewModel, SearchMessagesFilter filter)
        {
            _viewModel = viewModel;
            _filter = filter;

            _oldToNew = filter is SearchMessagesFilterUnreadMention;
        }

        public void SetLastViewedMessage(long messageId)
        {
            _lastMessage = messageId;
        }

        public void RemoveMessage(long messageId)
        {
            _messages?.Remove(messageId);
        }

        public async void NextMessage()
        {
            var chat = _viewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (_messages != null && _messages.Count > 0)
            {
                await _viewModel.LoadMessageSliceAsync(null, _messages.RemoveLast());
            }
            else
            {
                long fromMessageId;
                if (_lastMessage != 0)
                {
                    fromMessageId = _lastMessage;
                }
                else
                {
                    var first = _viewModel.Items.FirstOrDefault();
                    if (first != null)
                    {
                        fromMessageId = first.Id;
                    }
                    else
                    {
                        return;
                    }
                }

                var response = await _viewModel.ClientService.SendAsync(new SearchChatMessages(chat.Id, _viewModel.TopicId, string.Empty, null, fromMessageId, -9, 10, _filter));
                if (response is FoundChatMessages messages)
                {
                    List<long> stack = null;

                    if (_oldToNew)
                    {
                        foreach (var message in messages.Messages.Reverse())
                        {
                            stack ??= new List<long>();
                            stack.Add(message.Id);
                        }
                    }
                    else
                    {
                        foreach (var message in messages.Messages)
                        {
                            stack ??= new List<long>();
                            stack.Add(message.Id);
                        }
                    }

                    if (stack != null)
                    {
                        _messages = stack;
                        NextMessage();
                    }
                }
            }
        }
    }
}
