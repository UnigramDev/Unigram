using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        private User _currentInlineBot;
        public User CurrentInlineBot
        {
            get
            {
                return _currentInlineBot;
            }
            set
            {
                Set(ref _currentInlineBot, value);
            }
        }

        private BotResultsCollection _inlineBotResults;
        public BotResultsCollection InlineBotResults
        {
            get
            {
                return _inlineBotResults;
            }
            set
            {
                Set(ref _inlineBotResults, value);
                RaisePropertyChanged(() => InlineBotResultsVisibility);

                _inlineBotResults?.Reset();
            }
        }

        public Visibility InlineBotResultsVisibility
        {
            get
            {
                return _inlineBotResults != null && (!string.IsNullOrEmpty(_inlineBotResults.SwitchPmText) || _inlineBotResults.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public async Task<bool> ResolveInlineBotAsync(string text, CancellationToken token)
        {
            var username = text.TrimStart('@').TrimEnd(' ');
            if (string.IsNullOrEmpty(username))
            {
                return false;
            }

            var chat = Chat;
            if (chat != null && chat.Type is ChatTypeSupergroup super && super.IsChannel)
            {
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return false;
                }
            }

            var response = await ProtoService.SendAsync(new SearchPublicChat(username));
            if (response is Chat result && result.Type is ChatTypePrivate privata)
            {
                if (token.IsCancellationRequested)
                {
                    return true;
                }

                var user = ProtoService.GetUser(privata.UserId);
                if (user.Type is UserTypeBot bot && bot.IsInline)
                {
                    CurrentInlineBot = user;
                    return true;
                }
            }

            return false;
        }


        public async void ResolveInlineBot(string text, string command = null, CancellationToken token = default)
        {
            var username = text.TrimStart('@').TrimEnd(' ');

            var chat = Chat;
            if (chat != null && chat.Type is ChatTypeSupergroup super && super.IsChannel)
            {
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return;
                }
            }

            var response = await ProtoService.SendAsync(new SearchPublicChat(username));
            if (response is Chat result && result.Type is ChatTypePrivate privata)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                CurrentInlineBot = ProtoService.GetUser(privata.UserId);
                GetInlineBotResults(command ?? string.Empty, token);
            }
        }

        public async void GetInlineBotResults(string query, CancellationToken token)
        {
            if (CurrentInlineBot == null)
            {
                InlineBotResults = null;
                return;
            }

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (query != null)
            {
                query = query.Format();
            }

            //Debug.WriteLine($"@{CurrentInlineBot.Username}: {CurrentInlineBot.BotInlinePlaceholder}, {query}");

            // TODO: cache

            if (false)
            {

            }
            else
            {
                var collection = new BotResultsCollection(ProtoService, _currentInlineBot.Id, chat.Id, null, query);
                var result = await collection.LoadMoreItemsAsync(0);

                if (collection.Results != null && !token.IsCancellationRequested)
                {
                    InlineBotResults = collection;
                }

                //var response = await ProtoService.GetInlineBotResultsAsync(CurrentInlineBot.ToInputUser(), Peer, null, query, string.Empty);
                //if (response.IsSucceeded)
                //{
                //    foreach (var item in response.Result.Results)
                //    {
                //        item.QueryId = response.Result.QueryId;
                //    }

                //    InlineBotResults = response.Result;
                //    Debug.WriteLine(response.Result.Results.Count.ToString());
                //}
            }
        }

        public async void SendBotInlineResult(InlineQueryResult queryResult, long queryId)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var currentInlineBot = CurrentInlineBot;
            if (currentInlineBot == null)
            {
                return;
            }

            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            //var channel = With as TLChannel;
            //if (channel != null && channel.HasBannedRights && channel.BannedRights.IsSendGames && result.Type.Equals("game", StringComparison.OrdinalIgnoreCase))
            //{
            //    if (channel.BannedRights.IsForever())
            //    {
            //        await MessagePopup.ShowAsync(Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AppName, Strings.Resources.OK);
            //    }
            //    else
            //    {
            //        await MessagePopup.ShowAsync(string.Format(Strings.Resources.AttachMediaRestricted, BindConvert.Current.BannedUntil(channel.BannedRights.UntilDate)), Strings.Resources.AppName, Strings.Resources.OK);
            //    }

            //    return;
            //}

            SetText(null, false);

            CurrentInlineBot = null;
            InlineBotResults = null;

            var reply = GetReply(true);

            var response = await ProtoService.SendAsync(new SendInlineQueryResultMessage(chat.Id, _threadId, reply, options, queryId, queryResult.GetId(), false));
        }
    }

    public class BotResultsCollection : IncrementalCollection<InlineQueryResult>
    {
        private readonly IProtoService _protoService;

        private readonly Dictionary<InlineQueryResult, long> _queryIds;

        private readonly int _botUserId;
        private readonly long _chatId;
        private readonly Location _location;
        private readonly string _query;

        private InlineQueryResults _results;
        private string _nextOffset;

        public BotResultsCollection(IProtoService protoService, int botUserId, long chatId, Location location, string query)
        {
            _protoService = protoService;

            _queryIds = new Dictionary<InlineQueryResult, long>();

            _botUserId = botUserId;
            _chatId = chatId;
            _location = location;
            _query = query;

            _nextOffset = string.Empty;
        }

        public long GetQueryId(InlineQueryResult result)
        {
            if (_queryIds.TryGetValue(result, out long value))
            {
                return value;
            }

            return 0;
        }

        public InlineQueryResults Results => _results;

        public string Query => _query;

        public long InlineQueryId => _results.InlineQueryId;
        public string NextOffset => _results.NextOffset;

        public string SwitchPmParameter => _results.SwitchPmParameter;
        public string SwitchPmText => _results.SwitchPmText;

        public override async Task<IList<InlineQueryResult>> LoadDataAsync()
        {
            if (_nextOffset != null)
            {
                var response = await _protoService.SendAsync(new GetInlineQueryResults(_botUserId, _chatId, _location, _query, _nextOffset));
                if (response is InlineQueryResults results)
                {
                    _results = results;
                    _nextOffset = string.IsNullOrEmpty(results.NextOffset) ? null : results.NextOffset;

                    foreach (var item in results.Results)
                    {
                        _queryIds[item] = results.InlineQueryId;
                    }

                    return results.Results;
                }
            }

            return new InlineQueryResult[0];
        }
    }
}
