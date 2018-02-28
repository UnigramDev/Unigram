using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core;
using Unigram.Services;
using Windows.UI.Popups;
using Template10.Utils;
using Unigram.Core.Common;
using Template10.Mvvm;
using System.ComponentModel;
using System.Collections.Specialized;
using Windows.Storage;
using System.Runtime.CompilerServices;
using TdWindows;

namespace Unigram.ViewModels.Dialogs
{
    public class DialogStickersViewModel : UnigramViewModelBase, IHandle<UpdateRecentStickers>
    {
        private StickerSetViewModel _recentSet;
        private StickerSetViewModel _favoriteSet;
        private TLChannelStickerSet _groupSet;

        private bool _recentGifs;
        private bool _recentStickers;
        private bool _favedStickers;
        private bool _featured;
        private bool _stickers;

        public DialogStickersViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            _favoriteSet = new StickerSetViewModel(new StickerSetInfo
            {
                Title = Strings.Resources.FavoriteStickers,
                Name = "tg/favedStickers"
            });

            _recentSet = new StickerSetViewModel(new StickerSetInfo
            {
                Title = Strings.Resources.RecentStickers,
                Name = "tg/recentlyUsed"
            });

            //_groupSet = new TLChannelStickerSet
            //{
            //    Set = new TLStickerSet
            //    {
            //        Title = Strings.Resources.GroupStickers,
            //        ShortName = "tg/groupStickers",
            //    },
            //};

            Aggregator.Subscribe(this);

            SavedGifs = new MvxObservableCollection<MosaicMediaRow>();
            FeaturedStickers = new MvxObservableCollection<TLFeaturedStickerSet>();
            SavedStickers = new StickerSetCollection();

            //SyncStickers();
            //SyncGifs();

            InstallCommand = new RelayCommand<TLFeaturedStickerSet>(InstallExecute);
        }

        public void Handle(UpdateFavoriteStickers update)
        {
            ProtoService.Send(new GetFavoriteStickers(), result =>
            {
                if (result is Stickers favorite)
                {
                    BeginOnUIThread(() => _favoriteSet.Update(favorite, true));
                }
            });
        }

        public void Handle(UpdateRecentStickers update)
        {
            if (update.IsAttached)
            {
                return;
            }

            ProtoService.Send(new GetRecentStickers(), result =>
            {
                if (result is Stickers recent)
                {
                    BeginOnUIThread(() => _recentSet.Update(recent, true));
                }
            });
        }

        private void ProcessRecentGifs()
        {
            //var recent = _stickersService.GetRecentGifs();
            //BeginOnUIThread(() =>
            //{
            //    SavedGifs.ReplaceWith(MosaicMedia.Calculate(recent));
            //});
        }

        private void ProcessRecentStickers()
        {
            //var items = _stickersService.GetRecentStickers(StickerType.Image);
            //BeginOnUIThread(() =>
            //{
            //    _recentSet.Documents = new TLVector<TLDocumentBase>(items);
            //    CheckDocuments();

            //    if (_recentSet.Documents.Count > 0)
            //    {
            //        SavedStickers.Add(_recentSet);
            //    }
            //    else
            //    {
            //        SavedStickers.Remove(_recentSet);
            //    }
            //});
        }

        private void ProcessFavedStickers()
        {
            //var items = _stickersService.GetRecentStickers(StickerType.Fave);
            //BeginOnUIThread(() =>
            //{
            //    _favedSet.Documents = new TLVector<TLDocumentBase>(items);
            //    CheckDocuments();

            //    if (_favedSet.Documents.Count > 0)
            //    {
            //        SavedStickers.Add(_favedSet);
            //    }
            //    else
            //    {
            //        SavedStickers.Remove(_favedSet);
            //    }
            //});
        }

        private void ProcessStickers()
        {
            //_stickers = true;

            //var stickers = _stickersService.GetStickerSets(StickerType.Image);
            //BeginOnUIThread(() =>
            //{
            //    SavedStickers.ReplaceWith(stickers);

            //    //if (_groupSet.Documents != null && _groupSet.Documents.Count > 0)
            //    //{
            //    //    SavedStickers.Add(_groupSet);
            //    //}
            //    //else
            //    //{
            //    //    SavedStickers.Remove(_groupSet);
            //    //}

            //    //if (_recentSet.Documents != null && _recentSet.Documents.Count > 0)
            //    //{
            //    //    SavedStickers.Add(_recentSet);
            //    //}
            //    //else
            //    //{
            //    //    SavedStickers.Remove(_recentSet);
            //    //}

            //    //if (_favedSet.Documents != null && _favedSet.Documents.Count > 0)
            //    //{
            //    //    SavedStickers.Add(_favedSet);
            //    //}
            //    //else
            //    //{
            //    //    SavedStickers.Remove(_favedSet);
            //    //}
            //});
        }

        private void ProcessFeaturedStickers()
        {
            //_featured = true;
            //var stickers = _stickersService.GetFeaturedStickerSets();
            //var unread = _stickersService.GetUnreadStickerSets();
            //BeginOnUIThread(() =>
            //{
            //    FeaturedUnreadCount = unread.Count;
            //    FeaturedStickers.ReplaceWith(stickers.Select(set => new TLFeaturedStickerSet
            //    {
            //        Set = set.Set,
            //        IsUnread = unread.Contains(set.Set.Id),
            //        Covers = new TLVector<TLDocumentBase>(set.Documents.Take(Math.Min(set.Documents.Count, 5)))
            //    }));
            //});
        }

        private void CheckDocuments()
        {
            //if (_recentSet.Documents == null || _favedSet.Documents == null)
            //{
            //    return;
            //}

            //for (int i = 0; i < _favedSet.Documents.Count; i++)
            //{
            //    var favSticker = _favedSet.Documents[i] as TLDocument;
            //    for (int j = 0; j < _recentSet.Documents.Count; j++)
            //    {
            //        var recSticker = _recentSet.Documents[j] as TLDocument;
            //        if (recSticker.DCId == favSticker.DCId && recSticker.Id == favSticker.Id)
            //        {
            //            _recentSet.Documents.Remove(recSticker);
            //            break;
            //        }
            //    }
            //}
        }

        public MvxObservableCollection<MosaicMediaRow> SavedGifs { get; private set; }

        public MvxObservableCollection<TLFeaturedStickerSet> FeaturedStickers { get; private set; }

        public StickerSetCollection SavedStickers { get; private set; }

        //public void SyncGroup(TLChannelFull channelFull)
        //{
        //    SavedStickers.Remove(_groupSet);

        //    var update = true;

        //    var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
        //    if (appData.Values.TryGetValue("Stickers" + channelFull.Id, out object stickersObj))
        //    {
        //        var stickersId = (long)stickersObj;
        //        if (stickersId == channelFull.StickerSet?.Id)
        //        {
        //            update = false;
        //        }
        //    }

        //    if (channelFull.HasStickerSet && update)
        //    {
        //        _groupSet.With = CacheService.GetChat(channelFull.Id) as TLChannel;
        //        _groupSet.Full = channelFull;

        //        Execute.BeginOnThreadPool(() =>
        //        {
        //            var result = _stickersService.GetGroupStickerSetById(channelFull.StickerSet);
        //            if (result != null)
        //            {
        //                BeginOnUIThread(() =>
        //                {
        //                    _groupSet.Documents = new TLVector<TLDocumentBase>(result.Documents);

        //                    if (_groupSet.Documents != null && _groupSet.Documents.Count > 0)
        //                    {
        //                        SavedStickers.Add(_groupSet);
        //                    }
        //                    else
        //                    {
        //                        SavedStickers.Remove(_groupSet);
        //                    }
        //                });
        //            }
        //        });
        //    }
        //}

        //public void HideGroup(TLChannelFull channelFull)
        //{
        //    var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
        //    appData.Values["Stickers" + channelFull.Id] = channelFull.StickerSet?.Id ?? 0;

        //    SavedStickers.Remove(_groupSet);
        //}

        public void SyncStickers()
        {
            if (_stickers)
            {
                return;
            }

            _stickers = true;

            ProtoService.Send(new GetFavoriteStickers(), result1 =>
            {
                ProtoService.Send(new GetRecentStickers(), result2 =>
                {
                    ProtoService.Send(new GetInstalledStickerSets(false), result3 =>
                    {
                        if (result1 is Stickers favorite && result2 is Stickers recent && result3 is StickerSets sets)
                        {
                            _favoriteSet.Update(favorite);
                            _recentSet.Update(recent);

                            for (int i = 0; i < _favoriteSet.Stickers.Count; i++)
                            {
                                var favSticker = _favoriteSet.Stickers[i];
                                for (int j = 0; j < _recentSet.Stickers.Count; j++)
                                {
                                    var recSticker = _recentSet.Stickers[j];
                                    if (recSticker.StickerData.Id == favSticker.StickerData.Id)
                                    {
                                        _recentSet.Stickers.Remove(recSticker);
                                        break;
                                    }
                                }
                            }


                            var stickers = new List<StickerSetViewModel>();
                            if (favorite.StickersData.Count > 0)
                            {
                                stickers.Add(_favoriteSet);
                            }
                            if (recent.StickersData.Count > 0)
                            {
                                stickers.Add(_recentSet);
                            }

                            if (sets.Sets.Count > 0)
                            {
                                ProtoService.Send(new GetStickerSet(sets.Sets[0].Id), result4 =>
                                {
                                    if (result4 is StickerSet set)
                                    {
                                        stickers.Add(new StickerSetViewModel(sets.Sets[0], set));
                                        BeginOnUIThread(() => SavedStickers.ReplaceWith(stickers.Union(sets.Sets.Skip(1).Select(x => new StickerSetViewModel(x)))));
                                    }
                                    else
                                    {
                                        BeginOnUIThread(() => SavedStickers.ReplaceWith(stickers.Union(sets.Sets.Select(x => new StickerSetViewModel(x)))));
                                    }
                                });
                            }
                            else
                            {
                                BeginOnUIThread(() => SavedStickers.ReplaceWith(stickers.Union(sets.Sets.Select(x => new StickerSetViewModel(x)))));
                            }
                        }
                    });
                });
            });

            ProtoService.Send(new GetSavedAnimations(), result =>
            {
                if (result is Animations animation)
                {
                    BeginOnUIThread(() => SavedGifs.ReplaceWith(MosaicMedia.Calculate(animation.AnimationsData.ToList())));
                }
            });



            //ProtoService.Send(new GetSavedAnimations(), result =>
            //{

            //});

            //ProtoService.Send(new GetTrendingStickerSets(), result =>
            //{

            //});
        }

        public void SyncGifs()
        {
            //Execute.BeginOnThreadPool(() =>
            //{
            //    _stickersService.LoadRecents(StickerType.Image, true, true, false);

            //    ProcessRecentGifs();
            //});
        }

        private int _featuredUnreadCount;
        public int FeaturedUnreadCount
        {
            get
            {
                return _featuredUnreadCount;
            }
            set
            {
                Set(ref _featuredUnreadCount, value);
            }
        }

        public RelayCommand<TLFeaturedStickerSet> InstallCommand { get; }
        private async void InstallExecute(TLFeaturedStickerSet featured)
        {
            //if (_stickersService.IsStickerPackInstalled(featured.Set.Id) == false)
            //{
            //    var response = await LegacyService.InstallStickerSetAsync(new TLInputStickerSetID { Id = featured.Set.Id, AccessHash = featured.Set.AccessHash }, false);
            //    if (response.IsSucceeded)
            //    {
            //        _stickersService.LoadStickers(featured.Set.IsMasks ? StickerType.Mask : StickerType.Image, false, true);

            //        featured.Set.IsInstalled = true;
            //        featured.Set.IsArchived = false;
            //    }
            //}
            //else
            //{
            //    _stickersService.RemoveStickersSet(featured.Set, featured.Set.IsOfficial ? 1 : 0, true);

            //    featured.Set.IsInstalled = featured.Set.IsOfficial;
            //    featured.Set.IsArchived = featured.Set.IsOfficial;

            //    NavigationService.GoBack();
            //}
        }

        //protected override void BeginOnUIThread(Action action)
        //{
        //    // This is somehow needed because this viewmodel requires a Dispatcher
        //    // in some situations where base one might be null.
        //    Execute.BeginOnUIThread(action);
        //}
    }

    public class TLChannelStickerSet : TLObject
    {
        //public TLChannel With { get; set; }
        //public TLChannelFull Full { get; set; }
    }

    public class TLFeaturedStickerSet : TLObject
    {
        //public TLStickerSet Set { get; set; }

        //private TLVector<TLDocumentBase> _covers;
        //public TLVector<TLDocumentBase> Covers
        //{
        //    get
        //    {
        //        return _covers;
        //    }
        //    set
        //    {
        //        _covers = new TLVector<TLDocumentBase>();

        //        for (int i = 0; i < 5; i++)
        //        {
        //            if (i < value.Count)
        //            {
        //                _covers.Add(value[i]);
        //            }
        //            else
        //            {
        //                _covers.Add(null);
        //            }
        //        }
        //    }
        //}

        private bool _isUnread;
        public bool IsUnread
        {
            get
            {
                return _isUnread;
            }
            set
            {
                _isUnread = value;
            }
        }

        public string Unread
        {
            get
            {
                return _isUnread ? "\u2022" : string.Empty;
            }
        }
    }

    public class StickerSetViewModel
    {
        private readonly StickerSetInfo _info;
        private StickerSet _set;

        public StickerSetViewModel(StickerSetInfo info)
        {
            _info = info;

            var placeholders = new List<StickerViewModel>();
            for (int i = 0; i < info.Size; i++)
            {
                placeholders.Add(new StickerViewModel(info.Id));
            }

            Stickers = new MvxObservableCollection<StickerViewModel>(placeholders);
            Covers = info.Covers;
        }

        public StickerSetViewModel(StickerSetInfo info, StickerSet set)
            : this(info)
        {
            IsLoaded = true;
            Update(set);
        }

        public void Update(StickerSet set, bool reset = false)
        {
            _set = set;

            for (int i = 0; i < set.Stickers.Count && i < Stickers.Count; i++)
            {
                Stickers[i].Update(set.Stickers[i]);
            }

            if (reset)
            {
                Stickers.Reset();
            }
        }

        public void Update(Stickers stickers, bool raise = false)
        {
            if (raise)
            {
                Stickers.ReplaceWith(stickers.StickersData.Select(x => new StickerViewModel(x)));
            }
            else
            {
                Stickers = new MvxObservableCollection<StickerViewModel>(stickers.StickersData.Select(x => new StickerViewModel(x)));
            }
        }

        public MvxObservableCollection<StickerViewModel> Stickers { get; private set; }

        public bool IsLoaded { get; set; }

        //public IList<StickerEmojis> Emojis { get => _set?.Emojis; set => _set?.Emojis = value; }
        //public IList<Sticker> Stickers { get; set; }
        public bool IsViewed => _set?.IsViewed ?? _info.IsViewed;
        public bool IsMasks => _set?.IsMasks ?? _info.IsMasks;
        public bool IsOfficial => _set?.IsOfficial ?? _info.IsOfficial;
        public bool IsArchived => _set?.IsArchived ?? _info.IsArchived;
        public bool IsInstalled => _set?.IsInstalled ?? _info.IsInstalled;
        public string Name => _set?.Name ?? _info.Name;
        public string Title => _set?.Title ?? _info.Title;
        public long Id => _set?.Id ?? _info.Id;

        public IList<Sticker> Covers { get; private set; }
    }

    public class StickerViewModel
    {
        private Sticker _sticker;
        private long _setId;

        public StickerViewModel(long setId)
        {
            _setId = setId;
        }

        public StickerViewModel(Sticker sticker)
        {
            _sticker = sticker;
        }

        public void Update(Sticker sticker)
        {
            _sticker = sticker;
        }

        public bool UpdateFile(File file)
        {
            if (_sticker == null)
            {
                return false;
            }

            return _sticker.UpdateFile(file);
        }

        public Sticker Get()
        {
            return _sticker;
        }

        public File StickerData => _sticker?.StickerData;
        public PhotoSize Thumbnail => _sticker?.Thumbnail;
        public MaskPosition MaskPosition => _sticker?.MaskPosition;
        public bool IsMask => _sticker?.IsMask ?? false;
        public string Emoji => _sticker?.Emoji;
        public int Height => _sticker?.Height ?? 0;
        public int Width => _sticker?.Width ?? 0;
        public long SetId => _sticker?.SetId ?? _setId;
    }

    public class StickerSetCollection : MvxObservableCollection<StickerSetViewModel>
    {
        private readonly Dictionary<string, int> _indexer = new Dictionary<string, int>
        {
            { "tg/favedStickers", 0 },
            { "tg/recentlyUsed", 1 },
            { "tg/groupStickers", 2 }
        };

        private readonly Dictionary<int, string> _mapper = new Dictionary<int, string>
        {
            { 0, "tg/favedStickers" },
            { 1, "tg/recentlyUsed" },
            { 2, "tg/groupStickers" }
        };

        protected override void InsertItem(int index, StickerSetViewModel item)
        {
            if (_indexer.TryGetValue(item.Name, out int want))
            {
                index = 0;

                for (int i = 0; i < 3; i++)
                {
                    if (Count > i && _mapper[i] == this[i].Name && i < want)
                    {
                        index++;
                    }
                }

                var already = IndexOf(item);
                if (already != index)
                {
                    if (already > -1)
                    {
                        base.RemoveItem(already);
                    }

                    base.InsertItem(index, item);
                }
                else
                {
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, index, index));
                }
            }
            else
            {
                base.InsertItem(index, item);
            }
        }
    }
}
