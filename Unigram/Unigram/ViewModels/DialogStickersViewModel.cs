using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core;
using Unigram.Services;
using Windows.UI.Popups;
using Template10.Utils;
using Unigram.Core.Common;
using Template10.Mvvm;
using System.ComponentModel;
using Telegram.Api.TL.Messages;
using System.Collections.Specialized;
using Windows.Storage;

namespace Unigram.ViewModels
{
    public class DialogStickersViewModel : UnigramViewModelBase, IHandle<RecentsDidLoadedEventArgs>, IHandle<StickersDidLoadedEventArgs>, IHandle<FeaturedStickersDidLoadedEventArgs>, IHandle<GroupStickersDidLoadedEventArgs>
    {
        public readonly IStickersService _stickersService;

        private TLMessagesStickerSet _recentSet;
        private TLMessagesStickerSet _favedSet;
        private TLChannelStickerSet _groupSet;

        private bool _recentGifs;
        private bool _recentStickers;
        private bool _favedStickers;
        private bool _featured;
        private bool _stickers;

        public DialogStickersViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService)
            : base(protoService, cacheService, aggregator)
        {
            _stickersService = stickersService;

            _favedSet = new TLMessagesStickerSet
            {
                Set = new TLStickerSet
                {
                    Title = "Favorites",
                    ShortName = "tg/favedStickers"
                }
            };

            _recentSet = new TLMessagesStickerSet
            {
                Set = new TLStickerSet
                {
                    Title = "Frequently used",
                    ShortName = "tg/recentlyUsed"
                }
            };

            _groupSet = new TLChannelStickerSet
            {
                Set = new TLStickerSet
                {
                    Title = "Group stickers",
                    ShortName = "tg/groupStickers",
                },
            };

            Aggregator.Subscribe(this);

            SavedGifs = new MvxObservableCollection<TLDocument>();
            FeaturedStickers = new MvxObservableCollection<TLFeaturedStickerSet>();
            SavedStickers = new StickerSetCollection();

            SyncStickers();
            SyncGifs();
        }

        public IStickersService StickersService
        {
            get
            {
                return _stickersService;
            }
        }

        public void Handle(RecentsDidLoadedEventArgs e)
        {
            if (e.IsGifs)
            {
                ProcessRecentGifs();
            }
            else if (e.Type == StickerType.Image)
            {
                ProcessRecentStickers();
            }
            else if (e.Type == StickerType.Fave)
            {
                ProcessFavedStickers();
            }
        }

        public void Handle(StickersDidLoadedEventArgs e)
        {
            Debug.WriteLine("StickersDidLoaded");

            if (e.Type == StickerType.Image)
            {
                ProcessStickers();
            }
        }

        public void Handle(FeaturedStickersDidLoadedEventArgs e)
        {
            ProcessFeaturedStickers();
        }

        public void Handle(GroupStickersDidLoadedEventArgs e)
        {
            if (_groupSet.Full?.StickerSet?.Id == e.Id)
            {
                SyncGroup(_groupSet.Full);
            }
        }

        private void ProcessRecentGifs()
        {
            var recent = _stickersService.GetRecentGifs();
            Execute.BeginOnUIThread(() =>
            {
                SavedGifs.ReplaceWith(recent);
            });
        }

        private void ProcessRecentStickers()
        {
            var items = _stickersService.GetRecentStickers(StickerType.Image);
            Execute.BeginOnUIThread(() =>
            {
                _recentSet.Documents = new TLVector<TLDocumentBase>(items);
                CheckDocuments();

                if (_recentSet.Documents.Count > 0)
                {
                    SavedStickers.Add(_recentSet);
                }
                else
                {
                    SavedStickers.Remove(_recentSet);
                }
            });
        }

        private void ProcessFavedStickers()
        {
            var items = _stickersService.GetRecentStickers(StickerType.Fave);
            Execute.BeginOnUIThread(() =>
            {
                _favedSet.Documents = new TLVector<TLDocumentBase>(items);
                CheckDocuments();

                if (_favedSet.Documents.Count > 0)
                {
                    SavedStickers.Add(_favedSet);
                }
                else
                {
                    SavedStickers.Remove(_favedSet);
                }
            });
        }

        private void ProcessStickers()
        {
            _stickers = true;

            var stickers = _stickersService.GetStickerSets(StickerType.Image);
            Execute.BeginOnUIThread(() =>
            {
                SavedStickers.ReplaceWith(stickers);

                //if (_groupSet.Documents != null && _groupSet.Documents.Count > 0)
                //{
                //    SavedStickers.Add(_groupSet);
                //}
                //else
                //{
                //    SavedStickers.Remove(_groupSet);
                //}

                //if (_recentSet.Documents != null && _recentSet.Documents.Count > 0)
                //{
                //    SavedStickers.Add(_recentSet);
                //}
                //else
                //{
                //    SavedStickers.Remove(_recentSet);
                //}

                //if (_favedSet.Documents != null && _favedSet.Documents.Count > 0)
                //{
                //    SavedStickers.Add(_favedSet);
                //}
                //else
                //{
                //    SavedStickers.Remove(_favedSet);
                //}
            });
        }

        private void ProcessFeaturedStickers()
        {
            _featured = true;
            var stickers = _stickersService.GetFeaturedStickerSets();
            var unread = _stickersService.GetUnreadStickerSets();
            Execute.BeginOnUIThread(() =>
            {

                FeaturedUnreadCount = unread.Count;
                FeaturedStickers.ReplaceWith(stickers.Select(set => new TLFeaturedStickerSet
                {
                    Set = set.Set,
                    IsUnread = unread.Contains(set.Set.Id),
                    Covers = new TLVector<TLDocumentBase>(set.Documents.Take(Math.Min(set.Documents.Count, 5)))
                }));
            });
        }

        private void CheckDocuments()
        {
            if (_recentSet.Documents == null || _favedSet.Documents == null)
            {
                return;
            }

            for (int i = 0; i < _favedSet.Documents.Count; i++)
            {
                var favSticker = _favedSet.Documents[i] as TLDocument;
                for (int j = 0; j < _recentSet.Documents.Count; j++)
                {
                    var recSticker = _recentSet.Documents[j] as TLDocument;
                    if (recSticker.DCId == favSticker.DCId && recSticker.Id == favSticker.Id)
                    {
                        _recentSet.Documents.Remove(recSticker);
                        break;
                    }
                }
            }
        }

        public MvxObservableCollection<TLDocument> SavedGifs { get; private set; }

        public MvxObservableCollection<TLFeaturedStickerSet> FeaturedStickers { get; private set; }

        public StickerSetCollection SavedStickers { get; private set; }

        public void SyncGroup(TLChannelFull channelFull)
        {
            SavedStickers.Remove(_groupSet);

            var update = true;

            var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
            if (appData.Values.TryGetValue("Stickers" + channelFull.Id, out object stickersObj))
            {
                var stickersId = (long)stickersObj;
                if (stickersId == channelFull.StickerSet?.Id)
                {
                    update = false;
                }
            }

            if (channelFull.HasStickerSet && update)
            {
                _groupSet.With = CacheService.GetChat(channelFull.Id) as TLChannel;
                _groupSet.Full = channelFull;

                Execute.BeginOnThreadPool(() =>
                {
                    var result = _stickersService.GetGroupStickerSetById(channelFull.StickerSet);
                    if (result != null)
                    {
                        Execute.BeginOnUIThread(() =>
                        {
                            _groupSet.Documents = new TLVector<TLDocumentBase>(result.Documents);

                            if (_groupSet.Documents != null && _groupSet.Documents.Count > 0)
                            {
                                SavedStickers.Add(_groupSet);
                            }
                            else
                            {
                                SavedStickers.Remove(_groupSet);
                            }
                        });
                    }
                });
            }
        }

        public void HideGroup(TLChannelFull channelFull)
        {
            var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
            appData.Values["Stickers" + channelFull.Id] = channelFull.StickerSet?.Id ?? 0;

            SavedStickers.Remove(_groupSet);
        }

        public void SyncStickers()
        {
            Execute.BeginOnThreadPool(() =>
            {
                _stickersService.LoadRecents(StickerType.Fave, false, true, false);
                _stickersService.LoadRecents(StickerType.Image, false, true, false);
                var stickers = _stickersService.CheckStickers(StickerType.Image);
                var featured = _stickersService.CheckFeaturedStickers();

                ProcessFavedStickers();
                ProcessRecentStickers();
                if (stickers && !_stickers) ProcessStickers();
                if (featured && !_featured) ProcessFeaturedStickers();

                #region Old
                //var watch = Stopwatch.StartNew();

                //var response = await ProtoService.GetAllStickersAsync(0);
                //if (response.IsSucceeded)
                //{
                //    var old = DatabaseContext.Current.SelectStickerSets();

                //    var allStickers = response.Result as TLMessagesAllStickers;
                //    if (allStickers != null)
                //    {
                //        var needData = new Dictionary<long, TLStickerSet>();
                //        var ready = new Dictionary<long, TLStickerSet>();
                //        var removed = new List<TLStickerSet>();

                //        foreach (var set in allStickers.Sets)
                //        {
                //            var cached = old.FirstOrDefault(x => x.Id == set.Id);
                //            if (cached != null)
                //            {
                //                if (cached.Hash == set.Hash)
                //                {
                //                    ready[set.Id] = cached;
                //                }
                //                else
                //                {
                //                    needData[set.Id] = set;
                //                }
                //            }
                //            else
                //            {
                //                needData[set.Id] = set;
                //            }
                //        }

                //        foreach (var set in old)
                //        {
                //            if (needData.ContainsKey(set.Id) || ready.ContainsKey(set.Id)) { }
                //            else
                //            {
                //                removed.Add(set);
                //            }
                //        }

                //        if (removed.Count > 0)
                //        {
                //            DatabaseContext.Current.RemoveStickerSets(removed);
                //        }

                //        if (needData.Count > 0)
                //        {
                //            var results = new List<TLMessagesStickerSet>();
                //            var resultsSyncRoot = new object();
                //            ProtoService.GetStickerSetsAsync(new TLMessagesAllStickers { Sets = new TLVector<TLStickerSet>(needData.Values) },
                //                result =>
                //                {
                //                    Debugger.Break();
                //                    //DatabaseContext.Current.InsertStickerSets(needData.Values);
                //                },
                //                stickerSetResult =>
                //                {
                //                    var messagesStickerSet = stickerSetResult as TLMessagesStickerSet;
                //                    if (messagesStickerSet != null)
                //                    {
                //                        bool processStickerSets;
                //                        lock (resultsSyncRoot)
                //                        {
                //                            results.Add(messagesStickerSet);
                //                            processStickerSets = results.Count == needData.Values.Count;
                //                        }

                //                        if (processStickerSets)
                //                        {
                //                            DatabaseContext.Current.InsertStickerSets(results);
                //                            DatabaseContext.Current.UpdateStickerSetsOrder(allStickers.Sets);

                //                            //foreach (var item in ready)
                //                            //{
                //                            //    var items = DatabaseContext.Current.SelectDocuments("Stickers", item.Key);
                //                            //}

                //                            watch.Stop();
                //                            Execute.BeginOnUIThread(async () =>
                //                            {
                //                                await new MessageDialog(watch.Elapsed.ToString()).ShowQueuedAsync();
                //                            });
                //                        }
                //                    }
                //                },
                //                failure =>
                //                {
                //                    Debugger.Break();
                //                });
                //        }
                //        else
                //        {
                //            DatabaseContext.Current.UpdateStickerSetsOrder(allStickers.Sets);
                //        }
                //    }
                //}
                #endregion
            });
        }

        public void SyncGifs()
        {
            Execute.BeginOnThreadPool(() =>
            {
                _stickersService.LoadRecents(StickerType.Image, true, true, false);

                ProcessRecentGifs();

                #region Old
                //var gifs = await _gifsService.GetSavedGifs();
                //if (gifs.Key != SavedGifsHash || SavedGifs.Count == 0)
                //{
                //    Execute.BeginOnUIThread(() =>
                //    {
                //        SavedGifsHash = gifs.Key;
                //        //SavedGifs.Clear();
                //        //SavedGifs.AddRange(gifs);

                //        if (SavedGifs.Count > 0)
                //        {
                //            for (int i = 0; i < gifs.Count; i++)
                //            {
                //                var user = gifs[i];
                //                var index = -1;

                //                for (int j = 0; j < SavedGifs.Count; j++)
                //                {
                //                    if (SavedGifs[j].Id == user.Id)
                //                    {
                //                        index = j;
                //                        break;
                //                    }
                //                }

                //                if (index > -1 && index != i)
                //                {
                //                    SavedGifs.RemoveAt(index);
                //                    SavedGifs.Insert(Math.Min(i, SavedGifs.Count), user);
                //                }
                //                else if (index == -1)
                //                {
                //                    SavedGifs.Insert(Math.Min(i, SavedGifs.Count), user);
                //                }
                //            }

                //            for (int i = 0; i < SavedGifs.Count; i++)
                //            {
                //                var user = SavedGifs[i];
                //                var index = -1;

                //                for (int j = 0; j < gifs.Count; j++)
                //                {
                //                    if (gifs[j].Id == user.Id)
                //                    {
                //                        index = j;
                //                        break;
                //                    }
                //                }

                //                if (index == -1)
                //                {
                //                    SavedGifs.Remove(user);
                //                    i--;
                //                }
                //            }
                //        }
                //        else
                //        {
                //            SavedGifs.Clear();
                //            SavedGifs.AddRange(gifs);
                //        }

                //        //var old = SavedGifs.ToArray();
                //        //if (old.Length > 0)
                //        //{
                //        //    var order = new Dictionary<int, int>();
                //        //    for (int i = 0; i < old.Length; i++)
                //        //    {
                //        //        order[i] = -1;

                //        //        for (int j = 0; j < gifs.Count; j++)
                //        //        {
                //        //            if (old[i].Id == gifs[j].Id)
                //        //            {
                //        //                order[i] = j;
                //        //                break;
                //        //            }
                //        //        }
                //        //    }

                //        //    //for (int j = 0; j < order.First().Value; j++)
                //        //    //{
                //        //    //    if (order.ContainsKey(j) == false)
                //        //    //    {
                //        //    //        order[j] = j;
                //        //    //    }
                //        //    //}

                //        //    foreach (var item in order)
                //        //    {
                //        //        if (item.Key != item.Value)
                //        //        {
                //        //            SavedGifs.RemoveAt(item.Key);
                //        //            SavedGifs.Insert(item.Value, gifs[item.Value]);
                //        //        }
                //        //    }

                //        //    //Debugger.Break();
                //        //}
                //        //else
                //        //{
                //        //    SavedGifs.Clear();
                //        //    SavedGifs.AddRange(gifs);
                //        //}
                //    });
                //}
                #endregion
            });
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
    }

    public class TLChannelStickerSet : TLMessagesStickerSet
    {
        public TLChannel With { get; set; }
        public TLChannelFull Full { get; set; }
    }

    public class TLFeaturedStickerSet : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public TLStickerSet Set { get; set; }

        private TLVector<TLDocumentBase> _covers;
        public TLVector<TLDocumentBase> Covers
        {
            get
            {
                return _covers;
            }
            set
            {
                _covers = new TLVector<TLDocumentBase>();

                for (int i = 0; i < 5; i++)
                {
                    if (i < value.Count)
                    {
                        _covers.Add(value[i]);
                    }
                    else
                    {
                        _covers.Add(null);
                    }
                }
            }
        }

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsUnread"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Unread"));
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

    public class StickerSetCollection : MvxObservableCollection<TLMessagesStickerSet>
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

        protected override void InsertItem(int index, TLMessagesStickerSet item)
        {
            if (_indexer.TryGetValue(item.Set.ShortName, out int want))
            {
                index = 0;

                for (int i = 0; i < 3; i++)
                {
                    if (Count > i && _mapper[i] == this[i].Set.ShortName && i < want)
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
