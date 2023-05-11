//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public class SearchChatsCollection : ObservableCollection<object>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly string _query;
        private readonly ChatList _chatList;
        private readonly SearchChatsType _type;
        private readonly Chat _chat;

        private Phase _phase;
        private bool _createLocalHeader = true;
        private bool _createRemoteHeader = true;
        private bool _createForumMessagesHeader = true;
        private bool _createMessagesHeader = true;

        private string _nextOffset = string.Empty;

        private readonly List<long> _chats = new List<long>();
        private readonly List<long> _users = new List<long>();

        private readonly IList<ISearchChatsFilter> _internal;
        private readonly MvxObservableCollection<ISearchChatsFilter> _filters;

        public MvxObservableCollection<ISearchChatsFilter> Filters => _filters;

        public bool IsFiltered => _filters.Count > 0;

        public SearchChatsCollection(IClientService clientService, string query, IList<ISearchChatsFilter> filters/*, ChatList chatList = null*/, SearchChatsType type = SearchChatsType.All, Chat chat = null)
        {
            _clientService = clientService;
            _query = query;
            //_chatList = chatList;
            _chatList = null;
            _type = type;

            _internal = filters;
            _filters = new MvxObservableCollection<ISearchChatsFilter>();

            _chat = chat;
            _phase = chat != null ? Phase.Topics : Phase.Chats;
        }

        public string Query => _query;

        enum Phase
        {
            Topics,
            Chats,
            Contacts,
            ChatsOnServer,
            PublicChats,
            ForumMessages,
            Messages,
            None
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint phase)
        {
            return AsyncInfo.Run(async token =>
            {
                // If the query string is empty we want to load recent chats only
                var empty = string.IsNullOrEmpty(_query);
                //if (empty && phase > 0 && (_internal == null || _internal.IsEmpty()))
                //{
                //    _filters.ReplaceWith(new[]
                //    {
                //        new SearchChatsFilterContent(new SearchMessagesFilterPhotoAndVideo()),
                //        new SearchChatsFilterContent(new SearchMessagesFilterDocument()),
                //        new SearchChatsFilterContent(new SearchMessagesFilterUrl()),
                //        new SearchChatsFilterContent(new SearchMessagesFilterAudio()),
                //        new SearchChatsFilterContent(new SearchMessagesFilterVoiceNote())
                //    });

                //    return new LoadMoreItemsResult();
                //}

                bool IsFiltered(Chat chat)
                {
                    if (_type == SearchChatsType.Post && !_clientService.CanPostMessages(chat))
                    {
                        return true;
                    }
                    else if (_type == SearchChatsType.BasicAndSupergroups && chat.Type is not ChatTypeBasicGroup && chat.Type is not ChatTypeSupergroup)
                    {
                        return !_clientService.CanPostMessages(chat);
                    }
                    else if (_type == SearchChatsType.PrivateAndGroups)
                    {
                        return !(chat.Type is ChatTypePrivate || chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel);
                    }
                    else if (_type == SearchChatsType.Private)
                    {
                        return chat.Type is not ChatTypePrivate;
                    }
                    else if (_type == SearchChatsType.Contacts)
                    {
                        if (_clientService.TryGetUser(chat, out User user))
                        {
                            return string.IsNullOrEmpty(user.PhoneNumber);
                        }

                        return true;
                    }

                    return false;
                }

                var hasRanges = _internal?.Any(x => x is SearchChatsFilterDateRange) ?? false;
                var hasContent = _internal?.Any(x => x is SearchChatsFilterContent) ?? false;
                var hasChat = _internal?.Any(x => x is SearchChatsFilterChat) ?? false;

                var count = 0u;

                void Increase(object item)
                {
                    Add(item);
                    count++;
                }

                if (_phase == Phase.Topics)
                {
                    var response = await _clientService.SendAsync(new GetForumTopics(_chat.Id, _query, 0, 0, 0, 100));
                    if (response is ForumTopics topics)
                    {
                        Increase(new Header(Strings.Topics));

                        foreach (var topic in topics.Topics)
                        {
                            Increase(topic);
                        }
                    }

                    _phase = !hasChat || (hasContent && !hasChat && !empty)
                        ? Phase.Chats
                        : Phase.None;
                }
                else if (_phase == Phase.Chats)
                {
                    if (_type != SearchChatsType.BasicAndSupergroups && _query.Length > 0 && Strings.SavedMessages.StartsWith(_query, StringComparison.OrdinalIgnoreCase))
                    {
                        var savedMessages = await _clientService.SendAsync(new CreatePrivateChat(_clientService.Options.MyId, false));
                        if (savedMessages is Chat chat)
                        {
                            _chats.Add(chat.Id);

                            if (!hasContent)
                            {
                                //if (_createLocalHeader)
                                //{
                                //    _createLocalHeader = false;
                                //    Add(new Header(Strings.SearchAllChatsShort));
                                //}

                                Increase(new SearchResult(chat, _query, false));
                            }

                            _filters.Add(new SearchChatsFilterChat(_clientService, chat));
                        }
                    }

                    var response = await _clientService.SendAsync(new SearchChats(_query, 100));
                    if (response is Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _clientService.GetChat(id);
                            if (chat != null)
                            {
                                if (IsFiltered(chat))
                                {
                                    continue;
                                }

                                if (chat.Type is ChatTypePrivate privata)
                                {
                                    if (_users.Contains(privata.UserId))
                                    {
                                        continue;
                                    }

                                    _users.Add(privata.UserId);
                                }

                                _chats.Add(id);

                                if (!hasContent)
                                {
                                    //if (_createLocalHeader)
                                    //{
                                    //    _createLocalHeader = false;
                                    //    Add(new Header(Strings.SearchAllChatsShort));
                                    //}

                                    Increase(new SearchResult(chat, _query, false));
                                }

                                _filters.Add(new SearchChatsFilterChat(_clientService, chat));
                            }
                        }
                    }

                    _phase = !empty && !hasChat && !hasContent && _type != SearchChatsType.BasicAndSupergroups
                        ? Phase.Contacts
                        : Phase.None;
                }
                else if (_phase == Phase.Contacts)
                {
                    var response = await _clientService.SendAsync(new SearchContacts(_query, 100));
                    if (response is Users users)
                    {
                        foreach (var id in users.UserIds)
                        {
                            if (_users.Contains(id))
                            {
                                continue;
                            }

                            var user = _clientService.GetUser(id);
                            if (user != null)
                            {
                                //if (_createLocalHeader)
                                //{
                                //    _createLocalHeader = false;
                                //    Add(new Header(Strings.SearchAllChatsShort));
                                //}

                                _users.Add(id);
                                Increase(new SearchResult(user, _query, false));
                            }
                        }
                    }

                    _phase = !hasChat || (hasContent && !hasChat && !empty)
                        ? Phase.ChatsOnServer
                        : Phase.None;
                }
                else if (_phase == Phase.ChatsOnServer)
                {
                    var response = await _clientService.SendAsync(new SearchChatsOnServer(_query, 100));
                    if (response is Chats chats && !_createLocalHeader)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            if (_chats.Contains(id))
                            {
                                continue;
                            }

                            var chat = _clientService.GetChat(id);
                            if (chat != null)
                            {
                                if (IsFiltered(chat))
                                {
                                    continue;
                                }

                                if (chat.Type is ChatTypePrivate privata && _users.Contains(privata.UserId))
                                {
                                    continue;
                                }


                                if (!hasContent)
                                {
                                    //if (_createLocalHeader)
                                    //{
                                    //    _createLocalHeader = false;
                                    //    Add(new Header(Strings.SearchAllChatsShort));
                                    //}

                                    Increase(new SearchResult(chat, _query, false));
                                }

                                _filters.Add(new SearchChatsFilterChat(_clientService, chat));
                            }
                        }
                    }

                    _phase = !hasChat && !hasContent
                        ? Phase.PublicChats
                        : Phase.None;
                }
                else if (_phase == Phase.PublicChats)
                {
                    var response = await _clientService.SendAsync(new SearchPublicChats(_query));
                    if (response is Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _clientService.GetChat(id);
                            if (chat != null)
                            {
                                if (IsFiltered(chat))
                                {
                                    continue;
                                }

                                if (_createRemoteHeader)
                                {
                                    _createRemoteHeader = false;
                                    Increase(new Header(Strings.GlobalSearch));
                                }

                                Increase(new SearchResult(chat, _query, true));
                            }
                        }
                    }

                    _phase = _chat != null
                        ? Phase.ForumMessages
                        : Phase.Messages;
                }
                else if (_phase == Phase.Messages)
                {
                    hasRanges = LoadDateRanges(_query);

                    if (!hasContent && !hasRanges && _filters.Empty())
                    {
                        _filters.ReplaceWith(new[]
                        {
                            new SearchChatsFilterContent(new SearchMessagesFilterPhotoAndVideo()),
                            new SearchChatsFilterContent(new SearchMessagesFilterDocument()),
                            new SearchChatsFilterContent(new SearchMessagesFilterUrl()),
                            new SearchChatsFilterContent(new SearchMessagesFilterAudio()),
                            new SearchChatsFilterContent(new SearchMessagesFilterVoiceNote())
                        });
                    }

                    var content = _internal?.FirstOrDefault(x => x is SearchChatsFilterContent) as SearchChatsFilterContent;
                    var range = _internal?.FirstOrDefault(x => x is SearchChatsFilterDateRange) as SearchChatsFilterDateRange;
                    var chat = _internal?.FirstOrDefault(x => x is SearchChatsFilterChat) as SearchChatsFilterChat;

                    var minDate = range?.StartDate ?? 0;
                    var maxDate = range?.EndDate ?? 0;

                    Function function;
                    if (chat != null)
                    {
                        function = new SearchChatMessages(chat.Id, _query, null, 0, 0, 100, content?.Filter, 0);
                    }
                    else
                    {
                        function = new SearchMessages(_chatList, _query, _nextOffset, 100, content?.Filter, minDate, maxDate);
                    }

                    var response = await _clientService.SendAsync(function);
                    if (response is FoundChatMessages messages)
                    {
                        if (_createMessagesHeader)
                        {
                            _createMessagesHeader = false;
                            Increase(new Header(Strings.SearchMessages));
                        }

                        foreach (var message in messages.Messages)
                        {
                            Increase(message);
                        }
                    }
                    else if (response is FoundMessages foundMessages)
                    {
                        if (_createMessagesHeader)
                        {
                            _createMessagesHeader = false;
                            Increase(new Header(Strings.SearchMessages));
                        }

                        _nextOffset = foundMessages.NextOffset;
                        _hasMoreItems = !string.IsNullOrEmpty(foundMessages.NextOffset);

                        foreach (var message in foundMessages.Messages)
                        {
                            Increase(message);
                        }
                    }
                }

                if (_phase == Phase.None)
                {
                    _hasMoreItems = false;
                }

                return new LoadMoreItemsResult { Count = count };
            });
        }

        private bool LoadDateRanges(string str)
        {
            var ranges = DateTimeParser.Parse(str);

            foreach (var range in ranges)
            {
                _filters.Add(new SearchChatsFilterDateRange(range));
            }

            return ranges.Count > 0;
        }

        private bool _hasMoreItems = true;
        public bool HasMoreItems => _hasMoreItems;
    }

    public class SearchDiffHandler : IDiffHandler<object>
    {
        public bool CompareItems(object oldItem, object newItem)
        {
            if (oldItem is Header oldHeader && newItem is Header newHeader)
            {
                return oldHeader.Title == newHeader.Title;
            }
            else if (oldItem is SearchResult oldResult && newItem is SearchResult newResult)
            {
                if (oldResult.Chat != null && newResult.Chat != null)
                {
                    return oldResult.Chat.Id == newResult.Chat.Id;
                }
                else if (oldResult.User != null && newResult.User != null)
                {
                    return oldResult.User.Id == newResult.User.Id;
                }
                else if (oldResult.Topic != null && newResult.Topic != null)
                {

                }
            }
            else if (oldItem is Message oldMessage && newItem is Message newMessage)
            {
                return oldMessage.Id == newMessage.Id && oldMessage.ChatId == newMessage.ChatId;
            }

            return false;
        }

        public void UpdateItem(object oldItem, object newItem)
        {
            //throw new NotImplementedException();
        }
    }

    public class Header
    {
        public string Title { get; set; }

        public Header(string title)
        {
            Title = title;
        }
    }

    public interface ISearchChatsFilter
    {
        public string Text { get; }
        public string Glyph { get; }
    }

    public class SearchChatsFilterContent : ISearchChatsFilter
    {
        private readonly SearchMessagesFilter _filter;

        public SearchChatsFilterContent(SearchMessagesFilter filter)
        {
            _filter = filter;
        }

        public SearchMessagesFilter Filter => _filter;

        public string Text => _filter switch
        {
            SearchMessagesFilterPhotoAndVideo => Strings.SharedMediaTab2,
            SearchMessagesFilterDocument => Strings.SharedFilesTab2,
            SearchMessagesFilterUrl => Strings.SharedLinksTab2,
            SearchMessagesFilterAudio => Strings.SharedMusicTab2,
            SearchMessagesFilterVoiceNote => Strings.SharedVoiceTab2,
            _ => null,
        };

        public string Glyph => _filter switch
        {
            SearchMessagesFilterPhotoAndVideo => Icons.Image,
            SearchMessagesFilterDocument => Icons.Document,
            SearchMessagesFilterUrl => Icons.Link,
            SearchMessagesFilterAudio => Icons.MusicNote,
            SearchMessagesFilterVoiceNote => Icons.MicOn,
            _ => null,
        };
    }

    public class SearchChatsFilterChat : ISearchChatsFilter
    {
        private readonly IClientService _clientService;
        private readonly Chat _chat;

        public SearchChatsFilterChat(IClientService clientService, Chat chat)
        {
            _clientService = clientService;
            _chat = chat;
        }

        public long Id => _chat.Id;

        public string Text => _clientService.GetTitle(_chat);

        public string Glyph => _chat.Type switch
        {
            ChatTypePrivate => Icons.Person,
            ChatTypeBasicGroup => Icons.People,
            ChatTypeSupergroup supergroup => supergroup.IsChannel ? Icons.People : Icons.Megaphone,
            _ => null,
        };
    }

    public class SearchChatsFilterDateRange : ISearchChatsFilter
    {
        private readonly DateRange _range;

        public SearchChatsFilterDateRange(DateRange range)
        {
            _range = range;
        }

        public int EndDate => _range.EndDate;
        public int StartDate => _range.StartDate;

        public string Text
        {
            get
            {
                var start = Formatter.ToLocalTime(_range.StartDate);
                var end = Formatter.ToLocalTime(_range.EndDate);

                if (start.DayOfYear == end.DayOfYear)
                {
                    return Formatter.DayMonthFullYear.Format(start);
                }
                else if (start.Month == end.Month)
                {
                    return Formatter.MonthAbbreviatedYear.Format(start);
                }

                return start.Year.ToString();
            }
        }

        public string Glyph => Icons.Calendar;
    }

    public enum SearchChatsType
    {
        All,
        Post,
        Private,
        PrivateAndGroups,
        BasicAndSupergroups,
        Contacts
    }
}
