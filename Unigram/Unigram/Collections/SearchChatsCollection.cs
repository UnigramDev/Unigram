using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class SearchChatsCollection : ObservableCollection<KeyedList<string, object>>, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly string _query;
        private readonly ChatList _chatList;
        private readonly SearchChatsType _type;

        private readonly List<long> _chats = new List<long>();
        private readonly List<int> _users = new List<int>();

        private IList<ISearchChatsFilter> _internal;
        private MvxObservableCollection<ISearchChatsFilter> _filters;

        private KeyedList<string, object> _local;
        private KeyedList<string, object> _remote;
        private KeyedList<string, object> _messages;

        public MvxObservableCollection<ISearchChatsFilter> Filters => _filters;

        public bool IsFiltered => _filters.Count > 0;

        public KeyedList<string, object> Local => _local;
        public KeyedList<string, object> Remote => _remote;
        public KeyedList<string, object> Messages => _messages;

        public SearchChatsCollection(IProtoService protoService, string query, IList<ISearchChatsFilter> filters, ChatList chatList = null, SearchChatsType type = SearchChatsType.All)
        {
            _protoService = protoService;
            _query = query;
            _chatList = chatList;
            _type = type;

            _internal = filters;
            _filters = new MvxObservableCollection<ISearchChatsFilter>();

            _local = new KeyedList<string, object>(null as string);
            _remote = new KeyedList<string, object>(Strings.Resources.GlobalSearch);
            _messages = new KeyedList<string, object>(Strings.Resources.SearchMessages);

            Add(_local);
            Add(_remote);
            Add(_messages);
        }

        public string Query => _query;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint phase)
        {
            return AsyncInfo.Run(async token =>
            {
                // If the query string is empty we want to load recent chats only
                var empty = string.IsNullOrEmpty(_query);
                if (empty && phase > 0 && (_internal == null || _internal.IsEmpty()))
                {
                    _filters.ReplaceWith(new[]
                    {
                        new SearchChatsFilterContent(new SearchMessagesFilterPhotoAndVideo()),
                        new SearchChatsFilterContent(new SearchMessagesFilterDocument()),
                        new SearchChatsFilterContent(new SearchMessagesFilterUrl()),
                        new SearchChatsFilterContent(new SearchMessagesFilterAudio()),
                        new SearchChatsFilterContent(new SearchMessagesFilterVoiceNote())
                    });

                    return new LoadMoreItemsResult();
                }

                bool IsFiltered(Chat chat)
                {
                    if (_type == SearchChatsType.Post && !_protoService.CanPostMessages(chat))
                    {
                        return true;
                    }
                    else if (_type == SearchChatsType.BasicAndSupergroups && !(chat.Type is ChatTypeBasicGroup) && !(chat.Type is ChatTypeSupergroup))
                    {
                        return !_protoService.CanPostMessages(chat);
                    }
                    else if (_type == SearchChatsType.PrivateAndGroups)
                    {
                        return !(chat.Type is ChatTypePrivate || chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel);
                    }
                    else if (_type == SearchChatsType.Private)
                    {
                        return !(chat.Type is ChatTypePrivate);
                    }

                    return false;
                }

                var hasRanges = _internal?.Any(x => x is SearchChatsFilterDateRange) ?? false;
                var hasContent = _internal?.Any(x => x is SearchChatsFilterContent) ?? false;
                var hasChat = _internal?.Any(x => x is SearchChatsFilterChat) ?? false;

                if (phase == 0 && (!hasChat || (hasContent && !hasChat && !empty)))
                {
                    if (_type != SearchChatsType.BasicAndSupergroups && _query.Length > 0 && Strings.Resources.SavedMessages.StartsWith(_query, StringComparison.OrdinalIgnoreCase))
                    {
                        var savedMessages = await _protoService.SendAsync(new CreatePrivateChat(_protoService.Options.MyId, false));
                        if (savedMessages is Chat chat)
                        {
                            _chats.Add(chat.Id);

                            if (!hasContent)
                            {
                                _local.Add(new SearchResult(chat, _query, false));
                            }

                            _filters.Add(new SearchChatsFilterChat(_protoService, chat));
                        }
                    }

                    var response = await _protoService.SendAsync(new SearchChats(_query, 100));
                    if (response is Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
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
                                    _local.Add(new SearchResult(chat, _query, false));
                                }

                                _filters.Add(new SearchChatsFilterChat(_protoService, chat));
                            }
                        }
                    }
                }
                else if (phase == 1 && !hasChat && !hasContent && _type != SearchChatsType.BasicAndSupergroups)
                {
                    var response = await _protoService.SendAsync(new SearchContacts(_query, 100));
                    if (response is Users users)
                    {
                        foreach (var id in users.UserIds)
                        {
                            if (_users.Contains(id))
                            {
                                continue;
                            }

                            var user = _protoService.GetUser(id);
                            if (user != null)
                            {
                                _users.Add(id);
                                _local.Add(new SearchResult(user, _query, false));
                            }
                        }
                    }
                }
                else if (phase == 2 && (!hasChat || (hasContent && !hasChat && !empty)))
                {
                    var response = await _protoService.SendAsync(new SearchChatsOnServer(_query, 100));
                    if (response is Chats chats && _local != null)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            if (_chats.Contains(id))
                            {
                                continue;
                            }

                            var chat = _protoService.GetChat(id);
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
                                    _local.Add(new SearchResult(chat, _query, false));
                                }

                                _filters.Add(new SearchChatsFilterChat(_protoService, chat));
                            }
                        }
                    }
                }
                else if (phase == 3 && !hasChat && !hasContent)
                {
                    var response = await _protoService.SendAsync(new SearchPublicChats(_query));
                    if (response is Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
                            if (chat != null)
                            {
                                if (IsFiltered(chat))
                                {
                                    continue;
                                }

                                _remote.Add(new SearchResult(chat, _query, true));
                            }
                        }
                    }
                }
                else if (phase == 4)
                {
                    hasRanges = LoadDateRanges(_query);

                    if (!hasContent && !hasRanges && _filters.IsEmpty())
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

                    var content = _internal.FirstOrDefault(x => x is SearchChatsFilterContent) as SearchChatsFilterContent;
                    var range = _internal.FirstOrDefault(x => x is SearchChatsFilterDateRange) as SearchChatsFilterDateRange;
                    var chat = _internal.FirstOrDefault(x => x is SearchChatsFilterChat) as SearchChatsFilterChat;

                    Function function;
                    if (chat != null)
                    {
                        function = new SearchChatMessages(chat.Id, _query, 0, 0, 0, 100, content?.Filter);
                    }
                    else
                    {
                        function = new SearchMessages(_chatList, _query, int.MaxValue, 0, 0, 100);
                    }

                    var response = await _protoService.SendAsync(function);
                    if (response is Messages messages)
                    {
                        foreach (var message in messages.MessagesValue)
                        {
                            _messages.Add(message);
                        }
                    }
                }

                return new LoadMoreItemsResult();
            });
        }

        private bool LoadDateRanges(String str)
        {
            var ranges = DateTimeParser.Parse(str);

            foreach (var range in ranges)
            {
                _filters.Add(new SearchChatsFilterDateRange(range));
            }

            return ranges.Count > 0;
        }

        public bool HasMoreItems => false;
    }

    public interface ISearchChatsFilter
    {
        public string Text { get; }
        public string Glyph { get; }
    }

    public class SearchChatsFilterContent : ISearchChatsFilter
    {
        private SearchMessagesFilter _filter;

        public SearchChatsFilterContent(SearchMessagesFilter filter)
        {
            _filter = filter;
        }

        public SearchMessagesFilter Filter => _filter;

        public string Text
        {
            get
            {
                switch (_filter)
                {
                    case SearchMessagesFilterPhotoAndVideo photoAndVideo:
                        return Strings.Resources.SharedMediaTab2;
                    case SearchMessagesFilterDocument document:
                        return Strings.Resources.SharedFilesTab2;
                    case SearchMessagesFilterUrl url:
                        return Strings.Resources.SharedLinksTab2;
                    case SearchMessagesFilterAudio audio:
                        return Strings.Resources.SharedMusicTab2;
                    case SearchMessagesFilterVoiceNote voiceNote:
                        return Strings.Resources.SharedVoiceTab2;
                    default:
                        return null;
                }
            }
        }

        public string Glyph
        {
            get
            {
                switch (_filter)
                {
                    case SearchMessagesFilterPhotoAndVideo photoAndVideo:
                        return Icons.Photo;
                    case SearchMessagesFilterDocument document:
                        return Icons.Document;
                    case SearchMessagesFilterUrl url:
                        return Icons.Link;
                    case SearchMessagesFilterAudio audio:
                        return Icons.Music;
                    case SearchMessagesFilterVoiceNote voiceNote:
                        return Icons.Microphone;
                    default:
                        return null;
                }
            }
        }
    }

    public class SearchChatsFilterChat : ISearchChatsFilter
    {
        private readonly IProtoService _protoService;
        private readonly Chat _chat;

        public SearchChatsFilterChat(IProtoService protoService, Chat chat)
        {
            _protoService = protoService;
            _chat = chat;
        }

        public long Id => _chat.Id;

        public string Text => _protoService.GetTitle(_chat);

        public string Glyph
        {
            get
            {
                switch (_chat.Type)
                {
                    case ChatTypePrivate privata:
                        return Icons.Contact;
                    case ChatTypeBasicGroup basicGroup:
                        return Icons.BasicGroup;
                    case ChatTypeSupergroup supergroup:
                        return supergroup.IsChannel ? Icons.Group : Icons.Channel;
                    default:
                        return null;
                }
            }
        }
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
                var start = Utils.UnixTimestampToDateTime(_range.StartDate);
                var end = Utils.UnixTimestampToDateTime(_range.EndDate);

                if (start.DayOfYear == end.DayOfYear)
                {
                    return BindConvert.Current.DayMonthFullYear.Format(start);
                }
                else if (start.Month == end.Month)
                {
                    return BindConvert.Current.MonthAbbreviatedYear.Format(start);
                }

                return start.Year.ToString();
            }
        }

        public string Glyph => "\uE787";
    }

    public enum SearchChatsType
    {
        All,
        Post,
        Private,
        PrivateAndGroups,
        BasicAndSupergroups
    }
}
