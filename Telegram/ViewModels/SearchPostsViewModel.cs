//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Stars.Popups;

namespace Telegram.ViewModels
{
    public partial class SearchPostsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private CancellationTokenSource _cancellation = new();

        private string _prevQuery;
        private string _nextOffset;

        private bool _activated;

        public SearchPostsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _offline = new(Constants.LocalTypingTimeout, UpdateQueryOffline);
            _query = new(Constants.TypingTimeout, UpdateQuery, CanUpdateQuery);
            _query.Value = string.Empty;

            Items = new IncrementalCollection<Message>(this);

            // The search itself fetches the first page; there is nothing to continue from until
            // it has, and State has to have reached Results.
            Items.HasMoreItems = false;
        }

        public void Activate()
        {
            if (_activated)
            {
                return;
            }

            _activated = true;
            CanUpdateQuery(string.Empty, default);
        }

        public IncrementalCollection<Message> Items { get; }

        private readonly DebouncedPropertyWithToken<string> _offline;

        private readonly DebouncedPropertyWithToken<string> _query;
        public string Query
        {
            get => _query;
            set
            {
                _cancellation.Cancel();
                _cancellation = new();

                _query.Set(value, _cancellation.Token);
                RaisePropertyChanged(nameof(Query));
            }
        }

        public void SynchronizeQuery(string query)
        {
            _cancellation.Cancel();
            _cancellation = new();
        }

        public async void UpdateQuery(string value, CancellationToken token)
        {
            var query = value ?? string.Empty;

            _query.Value = query;

            //await LoadMessagesAsync(query, token);
        }

        private bool CanUpdateQuery(string value, CancellationToken token)
        {
            if (string.Equals(value, _prevQuery))
            {
                return false;
            }

            _offline.Set(_prevQuery = value, token);
            return value.Length > 0;
        }

        private string _queryString;
        public string QueryString
        {
            get => _queryString;
            set => Set(ref _queryString, value);
        }

        private PublicPostSearchLimits _limits;
        public PublicPostSearchLimits Limits
        {
            get => _limits;
            set => Set(ref _limits, value);
        }

        private SearchPostsState _state;
        public SearchPostsState State
        {
            get => _state;
            set => Set(ref _state, value);
        }

        private async void UpdateQueryOffline(string value, CancellationToken token)
        {
            _nextOffset = null;
            Items.HasMoreItems = false;

            Items.ClearIfNotEmpty();

            var query = value ?? string.Empty;

            State = SearchPostsState.Empty;
            QueryString = query;

            var response = await ClientService.SendAsync(new GetPublicPostSearchLimits(query));
            if (response is PublicPostSearchLimits limits && !token.IsCancellationRequested)
            {
                Limits = limits;

                if (limits.IsCurrentQueryFree && !string.IsNullOrEmpty(query))
                {
                    SearchImpl(token);
                }
            }
        }

        public void Search()
        {
            SearchImpl(_cancellation.Token);
        }

        private async void SearchImpl(CancellationToken token)
        {
            if (IsPremium)
            {
                var limits = Limits;
                if (limits == null)
                {
                    return;
                }

                State = SearchPostsState.Loading;

                Task<Object> request;
                if (limits.RemainingFreeQueryCount > 0 || limits.IsCurrentQueryFree)
                {
                    request = ClientService.SendAsync(new SearchPublicPosts(_prevQuery, string.Empty, 50, 0));
                }
                else
                {
                    request = ClientService.SendPaymentAsync(limits.StarCount, new SearchPublicPosts(_prevQuery, string.Empty, 50, limits.StarCount));
                }

                // The token passed in, not the field: a newer query replaces _cancellation with a source
                // that is not cancelled, so the field cannot tell a stale response from a live one.
                var response = await request;
                if (response is FoundPublicPosts posts && !token.IsCancellationRequested)
                {
                    _nextOffset = string.IsNullOrEmpty(posts.NextOffset) ? null : posts.NextOffset;
                    Limits = posts.SearchLimits;

                    Items.ReplaceWith(posts.Messages);

                    State = posts.Messages.Empty()
                        ? SearchPostsState.NotFound
                        : SearchPostsState.Results;

                    // After State, which is half of the condition.
                    Items.HasMoreItems = State == SearchPostsState.Results && _limits != null && _nextOffset != null;

                    if (!limits.IsCurrentQueryFree && limits.RemainingFreeQueryCount == 0)
                    {
                        ShowToast(Locale.Declension(Strings.R.SearchPaidStars, limits.StarCount), ToastPopupIcon.Premium);
                    }
                }
                else if (response is ErrorStarsNeeded)
                {
                    State = SearchPostsState.Empty;
                    NavigationService.ShowPopup(new BuyPopup(), BuyStarsArgs.ForChannel(limits.StarCount, 0));
                }
            }
            else
            {
                NavigationService.ShowPromo();
            }
        }

        #region ISupportIncrementalLoading

        public async Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
        {
            Logger.Info();

            var limits = Limits;
            if (limits == null)
            {
                return default;
            }

            var totalCount = 0u;

            var response = await ClientService.SendAsync(new SearchPublicPosts(_prevQuery, _nextOffset, 50, limits.StarCount));
            if (response is FoundPublicPosts posts)
            {
                _nextOffset = string.IsNullOrEmpty(posts.NextOffset) ? null : posts.NextOffset;
                Limits = posts.SearchLimits;

                foreach (var message in posts.Messages)
                {
                    Items.Add(message);
                    totalCount++;
                }
            }

            return new IncrementalLoadResult(totalCount, State == SearchPostsState.Results && _limits != null && _nextOffset != null);
        }

        #endregion
    }

    public enum SearchPostsState
    {
        Empty,
        Loading,
        Results,
        NotFound
    }
}
