﻿//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class InteractionsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private MessageReplyTo _replyTo;

        private string _nextOffset;

        private readonly HashSet<long> _users = new();

        public InteractionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<object>(this);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is MessageReplyTo message)
            {
                _replyTo = message;
                _nextOffset = string.Empty;
            }

            return Task.CompletedTask;
        }

        public string Title => _replyTo is MessageReplyToMessage
            ? Strings.Reactions
            : Strings.StatisticViews;

        public IncrementalCollection<object> Items { get; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            if (_nextOffset != null && _replyTo is MessageReplyToMessage message)
            {
                var response = await ClientService.SendAsync(new GetMessageAddedReactions(message.ChatId, message.MessageId, null, _nextOffset, 50));
                if (response is AddedReactions addedReactions)
                {
                    _nextOffset = addedReactions.NextOffset.Length > 0 ? addedReactions.NextOffset : null;

                    foreach (var item in addedReactions.Reactions)
                    {
                        if (item.SenderId is MessageSenderUser senderUser)
                        {
                            _users.Add(senderUser.UserId);
                        }

                        totalCount++;
                        Items.Add(item);
                    }
                }
                else
                {
                    _nextOffset = null;
                }
            }
            else
            {
                Function function = _replyTo switch
                {
                    MessageReplyToMessage replyToMessage => new GetMessageViewers(replyToMessage.ChatId, replyToMessage.MessageId),
                    _ => null
                };

                var response = await ClientService.SendAsync(function);
                if (response is MessageViewers viewers)
                {
                    HasMoreItems = _replyTo is MessageReplyToStory && viewers.Viewers.Count > 0;

                    foreach (var item in viewers.Viewers)
                    {
                        if (_users.Contains(item.UserId))
                        {
                            continue;
                        }

                        totalCount++;
                        Items.Add(item);
                    }
                }
            }

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        public void OpenChat(object clickedItem)
        {
            if (clickedItem is AddedReaction addedReaction)
            {
                NavigationService.NavigateToSender(addedReaction.SenderId);
            }
            else if (clickedItem is MessageViewer messageViewer)
            {
                NavigationService.NavigateToUser(messageViewer.UserId);
            }
        }
    }
}
