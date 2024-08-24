//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;

namespace Telegram.ViewModels
{
    public partial class DialogViewModel
    {
        private User _currentInlineBot;
        public User CurrentInlineBot
        {
            get => _currentInlineBot;
            set => Set(ref _currentInlineBot, value);
        }

        private BotResultsCollection _inlineBotResults;
        public BotResultsCollection InlineBotResults
        {
            get => _inlineBotResults;
            set
            {
                if (Set(ref _inlineBotResults, value))
                {
                    RaisePropertyChanged(nameof(IsInlineBotResultsVisible));

                    _inlineBotResults?.Reset();
                }
            }
        }

        public bool IsInlineBotResultsVisible => _inlineBotResults != null && (_inlineBotResults.Button != null || _inlineBotResults.Count > 0);

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
                var supergroup = ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return false;
                }
            }

            var response = await ClientService.SendAsync(new SearchPublicChat(username));
            if (response is Chat result && result.Type is ChatTypePrivate privata)
            {
                if (token.IsCancellationRequested)
                {
                    return true;
                }

                var user = ClientService.GetUser(privata.UserId);
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
                var supergroup = ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return;
                }
            }

            var response = await ClientService.SendAsync(new SearchPublicChat(username));
            if (response is Chat result && result.Type is ChatTypePrivate privata)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                CurrentInlineBot = ClientService.GetUser(privata.UserId);
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

            if (false && string.IsNullOrEmpty(query))
            {
                InlineBotResults = null;
            }
            else
            {
                var collection = new BotResultsCollection(ClientService, _currentInlineBot.Id, chat.Id, null, query, token);
                await collection.LoadMoreItemsAsync(0);

                if (collection.Results != null && !token.IsCancellationRequested)
                {
                    InlineBotResults = collection;
                }

                //var response = await ClientService.GetInlineBotResultsAsync(CurrentInlineBot.ToInputUser(), Peer, null, query, string.Empty);
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
            //        await ShowPopupAsync(Strings.AttachMediaRestrictedForever, Strings.AppName, Strings.OK);
            //    }
            //    else
            //    {
            //        await ShowPopupAsync(string.Format(Strings.AttachMediaRestricted, BindConvert.Current.BannedUntil(channel.BannedRights.UntilDate)), Strings.AppName, Strings.OK);
            //    }

            //    return;
            //}

            SetText(null, false);

            CurrentInlineBot = null;
            InlineBotResults = null;

            var reply = GetReply(true);
            Function function;

            if (QuickReplyShortcut != null)
            {
                if (reply is InputMessageReplyToMessage replyToMessage)
                {
                    function = new AddQuickReplyShortcutInlineQueryResultMessage(QuickReplyShortcut.Name, replyToMessage.MessageId, queryId, queryResult.GetId(), false);
                }
                else
                {
                    function = new AddQuickReplyShortcutInlineQueryResultMessage(QuickReplyShortcut.Name, 0, queryId, queryResult.GetId(), false);
                }
            }
            else
            {
                function = new SendInlineQueryResultMessage(chat.Id, ThreadId, reply, options, queryId, queryResult.GetId(), false);
            }

            var response = await ClientService.SendAsync(function);
        }
    }

    public class BotResultsCollection : MvxObservableCollection<InlineQueryResult>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;

        private readonly Dictionary<InlineQueryResult, long> _queryIds;

        private readonly long _botUserId;
        private readonly long _chatId;
        private readonly Location _location;
        private readonly string _query;

        private readonly CancellationToken _cancellationToken;

        private InlineQueryResults _results;
        private string _nextOffset;

        public BotResultsCollection(IClientService clientService, long botUserId, long chatId, Location location, string query, CancellationToken cancellationToken)
        {
            _clientService = clientService;

            _queryIds = new Dictionary<InlineQueryResult, long>();

            _botUserId = botUserId;
            _chatId = chatId;
            _location = location;
            _query = query;
            _cancellationToken = cancellationToken;

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

        public InlineQueryResultsButton Button => _results.Button;
        public string ButtonText => _results.Button?.Text;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                var totalCount = 0u;

                if (_nextOffset != null)
                {
                    if (string.IsNullOrEmpty(_nextOffset))
                    {
                        await Task.Delay(Constants.TypingTimeout);
                    }

                    if (_cancellationToken.IsCancellationRequested)
                    {
                        goto Cleanup;
                    }

                    var response = await _clientService.SendAsync(new GetInlineQueryResults(_botUserId, _chatId, _location, _query, _nextOffset));
                    if (response is InlineQueryResults results)
                    {
                        _results = results;
                        _nextOffset = string.IsNullOrEmpty(results.NextOffset) ? null : results.NextOffset;

                        foreach (var item in results.Results)
                        {
                            _queryIds[item] = results.InlineQueryId;
                            Add(item);
                        }
                    }
                }

            Cleanup:
                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            });
        }

        public bool HasMoreItems => _nextOffset != null;
    }
}
