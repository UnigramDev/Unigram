//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Windows.Foundation;

namespace Telegram.ViewModels.Drawers
{
    public partial class AnimationDrawerViewModel : ViewModelBase
    {
        private bool _updated;

        public AnimationDrawerViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SavedItems = new AnimationsCollection();
            TrendingItems = new TrendingAnimationsCollection(clientService);

            Sets = new List<AnimationsCollection>();
            Sets.Add(SavedItems);
            Sets.Add(TrendingItems);
            Sets.AddRange(clientService.AnimationSearchEmojis.Select(x => new SearchAnimationsCollection(clientService, x)));

            SelectedSet = Sets[0];

            Subscribe();
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateSavedAnimations>(this, Handle);
        }

        public static AnimationDrawerViewModel Create(int sessionId)
        {
            var context = TypeResolver.Current.Resolve<AnimationDrawerViewModel>(sessionId);
            context.Dispatcher = WindowContext.Current.Dispatcher;
            return context;
        }

        public void Update()
        {
            if (_updated)
            {
                return;
            }

            _updated = true;

            ClientService.Send(new GetSavedAnimations(), result =>
            {
                if (result is Animations animation)
                {
                    BeginOnUIThread(() => Merge(SavedItems, animation.AnimationsValue));
                }
            });
        }

        public void Handle(UpdateSavedAnimations update)
        {
            ClientService.Send(new GetSavedAnimations(), result =>
            {
                if (result is Animations animation)
                {
                    BeginOnUIThread(() => Merge(SavedItems, animation.AnimationsValue));
                }
            });
        }

        private void Merge(IList<Animation> destination, IList<Animation> origin)
        {
            if (destination.Count > 0)
            {
                for (int i = 0; i < destination.Count; i++)
                {
                    var item = destination[i];
                    var index = -1;

                    for (int j = 0; j < origin.Count; j++)
                    {
                        if (origin[j].AnimationValue.Id == item.AnimationValue.Id)
                        {
                            index = j;
                            break;
                        }
                    }

                    if (index == -1)
                    {
                        destination.Remove(item);
                        i--;
                    }
                }

                for (int i = 0; i < origin.Count; i++)
                {
                    var item = origin[i];
                    var index = -1;

                    for (int j = 0; j < destination.Count; j++)
                    {
                        if (destination[j].AnimationValue.Id == item.AnimationValue.Id)
                        {
                            //destination[j].Update(filter);

                            index = j;
                            break;
                        }
                    }

                    if (index > -1 && index != i)
                    {
                        destination.RemoveAt(index);
                        destination.Insert(Math.Min(i, destination.Count), item);
                    }
                    else if (index == -1)
                    {
                        destination.Insert(Math.Min(i, destination.Count), item);
                    }
                }
            }
            else
            {
                destination.Clear();
                destination.AddRange(origin);
            }
        }

        public AnimationsCollection SavedItems { get; private set; }

        public TrendingAnimationsCollection TrendingItems { get; private set; }

        public void Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SetAnimationSet(_selectedSet, false);
            }
            else
            {
                SetAnimationSet(new SearchAnimationsCollection(ClientService, query), true);
            }
        }

        private AnimationsCollection _searchSet;

        private AnimationsCollection _selectedSet;
        public AnimationsCollection SelectedSet
        {
            get => _selectedSet;
            set => SetAnimationSet(value, false);
        }

        public AnimationsCollection Items => _searchSet ?? _selectedSet;

        public List<AnimationsCollection> Sets { get; private set; }

        private async void SetAnimationSet(AnimationsCollection collection, bool searching)
        {
            if ((collection == _selectedSet && !searching && _searchSet == null) || (collection == _searchSet && searching))
            {
                return;
            }

            if (searching)
            {
                _searchSet = collection;
                RaisePropertyChanged(nameof(Items));
            }
            else
            {
                _searchSet = null;
                Set(ref _selectedSet, collection, nameof(SelectedSet));
                RaisePropertyChanged(nameof(Items));
            }

            if (collection is SearchAnimationsCollection search && search.Empty())
            {
                await search.LoadMoreItemsAsync(0);
            }
            else
            {
                collection?.Reset();
            }
        }

    }

    public partial class AnimationsCollection : MvxObservableCollection<Animation>/*, IKeyIndexMapping*/
    {
        public virtual string Name => "tg/recentlyUsed";
        public virtual string Title => Strings.RecentStickers;

        public string KeyFromIndex(int index)
        {
            return this[index].AnimationValue.Id.ToString();
        }

        public int IndexFromKey(string key)
        {
            return IndexOf(this.FirstOrDefault(x => key == x.AnimationValue.Id.ToString()));
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }
    }

    public partial class TrendingAnimationsCollection : SearchAnimationsCollection
    {
        public TrendingAnimationsCollection(IClientService clientService)
            : base(clientService, string.Empty)
        {
        }

        public override string Name => "tg/trending";
        public override string Title => Strings.FeaturedGifs;
    }

    public partial class SearchAnimationsCollection : AnimationsCollection, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly string _query;

        private long? _userId;
        private string _offset = string.Empty;
        private bool _hasMoreItems = true;

        public SearchAnimationsCollection(IClientService clientService, string query)
        {
            _clientService = clientService;
            _query = query;
        }

        public string Query => _query;

        public override string Name => _query;
        public override string Title => _query;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint phase)
        {
            return AsyncInfo.Run(async token =>
            {
                IsLoading = true;

                if (_userId == null)
                {
                    var bot = await _clientService.SendAsync(new SearchPublicChat(_clientService.Options.AnimationSearchBotUsername));
                    if (bot is Chat chat && chat.Type is ChatTypePrivate privata)
                    {
                        _userId = privata.UserId;
                    }
                }

                if (_userId != null)
                {
                    var response = await _clientService.SendAsync(new GetInlineQueryResults(_userId.Value, 0, null, _query, _offset));
                    if (response is InlineQueryResults results)
                    {
                        _offset = results.NextOffset;
                        _hasMoreItems = _offset.Length > 0;

                        foreach (var item in results.Results)
                        {
                            if (item is InlineQueryResultAnimation animation)
                            {
                                Add(animation.Animation);
                            }
                        }
                    }
                    else
                    {
                        _hasMoreItems = false;
                    }
                }
                else
                {
                    _hasMoreItems = false;
                }

                IsLoading = false;
                return new LoadMoreItemsResult();
            });
        }

        public bool HasMoreItems => _hasMoreItems;
    }
}
