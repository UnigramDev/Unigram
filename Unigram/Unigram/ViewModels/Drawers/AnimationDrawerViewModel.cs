using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels.Drawers
{
    public class AnimationDrawerViewModel : TLViewModelBase, IHandle<UpdateSavedAnimations>
    {
        private bool _updated;

        public AnimationDrawerViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator) : base(protoService, cacheService, settingsService, aggregator)
        {
            SavedItems = new AnimationsCollection();
            TrendingItems = new TrendingAnimationsCollection(protoService, aggregator);

            Sets = new List<AnimationsCollection>();
            Sets.Add(SavedItems);
            Sets.Add(TrendingItems);
            Sets.AddRange(cacheService.AnimationSearchEmojis.Select(x => new SearchAnimationsCollection(protoService, aggregator, x)));

            SelectedSet = Sets[0];

            Aggregator.Subscribe(this);
        }

        private static Dictionary<int, Dictionary<int, AnimationDrawerViewModel>> _windowContext = new Dictionary<int, Dictionary<int, AnimationDrawerViewModel>>();
        public static AnimationDrawerViewModel GetForCurrentView(int sessionId)
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out Dictionary<int, AnimationDrawerViewModel> reference))
            {
                if (reference.TryGetValue(sessionId, out AnimationDrawerViewModel value))
                {
                    return value;
                }
            }
            else
            {
                _windowContext[id] = new Dictionary<int, AnimationDrawerViewModel>();
            }

            var context = TLContainer.Current.Resolve<AnimationDrawerViewModel>();
            _windowContext[id][sessionId] = context;

            return context;
        }

        public void Update()
        {
            if (_updated)
            {
                return;
            }

            _updated = true;

            ProtoService.Send(new GetSavedAnimations(), result =>
            {
                if (result is Animations animation)
                {
                    BeginOnUIThread(() => SavedItems.ReplaceWith(animation.AnimationsValue));
                }
            });
        }

        public void Handle(UpdateSavedAnimations update)
        {
            ProtoService.Send(new GetSavedAnimations(), result =>
            {
                if (result is Animations animation)
                {
                    BeginOnUIThread(() => SavedItems.ReplaceWith(animation.AnimationsValue));
                }
            });
        }

        public AnimationsCollection SavedItems { get; private set; }

        public TrendingAnimationsCollection TrendingItems { get; private set; }

        public void FindAnimations(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SetAnimationSet(_selectedSet, false);
            }
            else
            {
                SetAnimationSet(new SearchAnimationsCollection(ProtoService, Aggregator, query), true);
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
                RaisePropertyChanged(() => Items);
            }
            else
            {
                _searchSet = null;
                Set(() => SelectedSet, ref _selectedSet, collection);
                RaisePropertyChanged(() => Items);
            }

            if (collection is SearchAnimationsCollection search && search.IsEmpty())
            {
                await search.LoadMoreItemsAsync(0);
            }
            else if (collection != null)
            {
                collection.Reset();
            }
        }

    }

    public class AnimationsCollection : MvxObservableCollection<Animation>/*, IKeyIndexMapping*/
    {
        public virtual string Name => "tg/recentlyUsed";
        public virtual string Title => Strings.Resources.RecentStickers;

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

    public class TrendingAnimationsCollection : SearchAnimationsCollection
    {
        public TrendingAnimationsCollection(IProtoService protoService, IEventAggregator aggregator)
            : base(protoService, aggregator, string.Empty)
        {
        }

        public override string Name => "tg/trending";
        public override string Title => Strings.Resources.FeaturedGifs;
    }

    public class SearchAnimationsCollection : AnimationsCollection, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;
        private readonly string _query;

        private int? _userId;
        private string _offset = string.Empty;
        private bool _hasMoreItems = true;

        public SearchAnimationsCollection(IProtoService protoService, IEventAggregator aggregator, string query)
        {
            _protoService = protoService;
            _aggregator = aggregator;
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
                    var bot = await _protoService.SendAsync(new SearchPublicChat(_protoService.Options.AnimationSearchBotUsername));
                    if (bot is Chat chat && chat.Type is ChatTypePrivate privata)
                    {
                        _userId = privata.UserId;
                    }
                }

                if (_userId != null)
                {
                    var response = await _protoService.SendAsync(new GetInlineQueryResults(_userId.Value, 0, null, _query, _offset));
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
