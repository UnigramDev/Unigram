using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class InteractionsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private MessageReplyTo _replyTo;

        private string _nextOffset;
        private MessageViewer _nextViewers;

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
                    MessageReplyToStory replyToStory => new GetStoryViewers(replyToStory.StoryId, _nextViewers, 50),
                    _ => null
                };

                var response = await ClientService.SendAsync(function);
                if (response is MessageViewers viewers)
                {
                    HasMoreItems = _replyTo is MessageReplyToStory && viewers.Viewers.Count > 0;

                    foreach (var item in viewers.Viewers)
                    {
                        _nextViewers = item;

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

        public async void OpenChat(object clickedItem)
        {
            if (clickedItem is AddedReaction addedReaction)
            {
                if (addedReaction.SenderId is MessageSenderChat senderChat)
                {
                    NavigationService.Navigate(typeof(ProfilePage), senderChat.ChatId);
                }
                else if (addedReaction.SenderId is MessageSenderUser senderUser)
                {
                    var response = await ClientService.SendAsync(new CreatePrivateChat(senderUser.UserId, true));
                    if (response is Chat chat)
                    {
                        NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
            }
            else if (clickedItem is MessageViewer messageViewer)
            {
                var response = await ClientService.SendAsync(new CreatePrivateChat(messageViewer.UserId, true));
                if (response is Chat chat)
                {
                    NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                }
            }
        }
    }
}
