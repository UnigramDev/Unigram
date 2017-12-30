using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Native.TL;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.Context;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Telegram.Api.TL.Messages.Methods;
using Unigram.Common;
using Unigram.Core;
using Unigram.Core.Common;
using Universal.WinSQLite;
using Windows.Storage;

namespace Unigram.Services
{
    public interface IStickersService
    {
        void Cleanup();

        bool CheckStickers(StickerType stickerType);
        bool CheckArchivedStickersCount(StickerType stickerType);
        bool CheckFeaturedStickers();

        List<TLDocument> GetRecentStickers(StickerType stickerType);
        List<TLDocument> GetRecentStickersNoCopy(StickerType stickerType);

        void AddRecentSticker(StickerType stickerType, TLDocument document, int date, bool remove);

        List<TLDocument> GetRecentGifs();
        void AddRecentGif(TLDocument document, int date);
        void RemoveRecentGif(TLDocument document);

        bool IsLoadingStickers(StickerType stickerType);

        TLMessagesStickerSet GetGroupStickerSetById(TLStickerSet stickerSet);

        TLMessagesStickerSet GetStickerSetByName(string name);
        TLMessagesStickerSet GetStickerSetById(long id);

        Dictionary<string, List<TLDocument>> GetAllStickers();

        int GetArchivedStickersCount(StickerType stickerType);

        List<TLMessagesStickerSet> GetStickerSets(StickerType stickerType);

        List<TLMessagesStickerSet> GetFeaturedStickerSets();

        List<long> GetUnreadStickerSets();

        bool IsStickerInFavorites(TLDocument document);

        bool IsStickerPackInstalled(long id);
        bool IsStickerPackUnread(long id);
        bool IsStickerPackInstalled(string name);

        List<string> GetEmojiForSticker(long id);

        void LoadRecents(StickerType stickerType, bool gif, bool cache, bool force);

        void ReorderStickers(StickerType stickerType, IList<long> order);

        void CalculateNewHash(StickerType stickerType);

        void AddNewStickerSet(TLMessagesStickerSet set);

        void LoadArchivedStickersCount(StickerType stickerType, bool cache);

        void LoadFeaturedStickers(bool cache, bool force);

        void MarkFeaturedStickersAsRead(bool query);

        void MarkFeaturedStickersByIdAsRead(long id);

        void LoadStickers(StickerType stickerType, bool cache, bool force);

        string GetStickerSetName(long setId);

        long GetStickerSetId(TLDocument document);

        void RemoveStickersSet(TLStickerSet stickerSet, int hide, bool showSettings);

        List<TLDocument> LoadStickersForEmoji(string emoji);
    }

    public class StickersService : IStickersService, IHandle,
        IHandle<TLUpdateStickerSets>,
        IHandle<TLUpdateStickerSetsOrder>,
        IHandle<TLUpdateNewStickerSet>,
        IHandle<TLUpdateSavedGifs>,
        IHandle<TLUpdateRecentStickers>,
        IHandle<TLUpdateFavedStickers>,
        IHandle<TLUpdateReadFeaturedStickers>
    {
        private List<TLMessagesStickerSet>[] stickerSets = new[] { new List<TLMessagesStickerSet>(), new List<TLMessagesStickerSet>() };
        private Dictionary<long, TLMessagesStickerSet> stickerSetsById = new Dictionary<long, TLMessagesStickerSet>();
        private Dictionary<string, TLMessagesStickerSet> stickerSetsByName = new Dictionary<string, TLMessagesStickerSet>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<long, TLMessagesStickerSet> groupStickerSets = new Dictionary<long, TLMessagesStickerSet>();
        private bool[] loadingStickers = new bool[2];
        private bool[] stickersLoaded = new bool[2];
        private int[] archivedStickersCount = new int[2];
        private int[] loadHash = new int[2];
        private int[] loadDate = new int[2];

        private Dictionary<long, List<String>> stickersByEmoji = new Dictionary<long, List<String>>();
        private Dictionary<string, List<TLDocument>> allStickers = new Dictionary<string, List<TLDocument>>();

        private List<TLDocument>[] recentStickers = new[] { new List<TLDocument>(), new List<TLDocument>(), new List<TLDocument>() };
        private bool[] loadingRecentStickers = new bool[3];
        private bool[] recentStickersLoaded = new bool[3];

        private List<TLDocument> recentGifs = new List<TLDocument>();
        private bool loadingRecentGifs;
        private bool recentGifsLoaded;

        private int loadFeaturedHash;
        private int loadFeaturedDate;
        private List<TLMessagesStickerSet> featuredStickerSets = new List<TLMessagesStickerSet>();
        private Dictionary<long, TLMessagesStickerSet> featuredStickerSetsById = new Dictionary<long, TLMessagesStickerSet>();
        private List<long> unreadStickerSets = new List<long>();
        private List<long> readingStickerSets = new List<long>();
        private bool loadingFeaturedStickers;
        private bool featuredStickersLoaded;

        private readonly IMTProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly ITelegramEventAggregator _aggregator;

        public StickersService(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _aggregator = aggregator;

            _aggregator.Subscribe(this);

            //ApplicationSettings.Current.AddOrUpdateValue("lastGifLoadTime", (long)0);
            //ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTime", (long)0);
        }

        #region Updates handling

        public void Handle(TLUpdateStickerSets update)
        {
            var mask = false;
            LoadStickers(mask ? StickerType.Mask : StickerType.Image, false, true);
        }

        public void Handle(TLUpdateStickerSetsOrder update)
        {
            ReorderStickers(update.IsMasks ? StickerType.Mask : StickerType.Image, update.Order);
        }

        public void Handle(TLUpdateNewStickerSet update)
        {
            AddNewStickerSet(update.StickerSet);
        }

        public void Handle(TLUpdateSavedGifs update)
        {
            ApplicationSettings.Current.AddOrUpdateValue("lastGifLoadTime", 0L);
        }

        public void Handle(TLUpdateRecentStickers update)
        {
            ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTime", 0L);
            ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeMask", 0L);
        }

        public void Handle(TLUpdateFavedStickers update)
        {
            ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeFavs", 0L);
        }

        public void Handle(TLUpdateReadFeaturedStickers update)
        {
            MarkFeaturedStickersAsRead(false);
        }

        #endregion

        public void Cleanup()
        {
            for (int i = 0; i < 3; i++)
            {
                recentStickers[i].Clear();
                loadingRecentStickers[i] = false;
                recentStickersLoaded[i] = false;
            }

            for (int i = 0; i < 2; i++)
            {
                loadHash[i] = 0;
                loadDate[i] = 0;
                stickerSets[i].Clear();
                loadingStickers[i] = false;
                stickersLoaded[i] = false;
            }

            loadFeaturedDate = 0;
            loadFeaturedHash = 0;
            allStickers.Clear();
            stickersByEmoji.Clear();
            featuredStickerSetsById.Clear();
            featuredStickerSets.Clear();
            unreadStickerSets.Clear();
            recentGifs.Clear();
            stickerSetsById.Clear();
            stickerSetsByName.Clear();
            loadingFeaturedStickers = false;
            featuredStickersLoaded = false;
            loadingRecentGifs = false;
            recentGifsLoaded = false;
        }

        public bool CheckStickers(StickerType stickerType)
        {
            var type = (int)stickerType;
            if (!loadingStickers[type] && (!stickersLoaded[type] || Math.Abs(Utils.CurrentTimestamp / 1000 - loadDate[type]) >= 60 * 60))
            {
                LoadStickers(stickerType, true, false);
                return false;
            }

            return true;
        }

        public bool CheckArchivedStickersCount(StickerType stickerType)
        {
            //if (!loadingStickers[type] && (!stickersLoaded[type] || Math.Abs(Utils.CurrentTimestamp / 1000 - loadDate[type]) >= 60 * 60))
            {
                LoadArchivedStickersCount(stickerType, true);
                return false;
            }

            return true;
        }

        public bool CheckFeaturedStickers()
        {
            if (!loadingFeaturedStickers && (!featuredStickersLoaded || Math.Abs(Utils.CurrentTimestamp / 1000 - loadFeaturedDate) >= 60 * 60))
            {
                LoadFeaturedStickers(true, false);
                return false;
            }

            return true;
        }

        public List<TLDocument> GetRecentStickers(StickerType stickerType)
        {
            var type = (int)stickerType;
            return new List<TLDocument>(recentStickers[type]);
        }

        public List<TLDocument> GetRecentStickersNoCopy(StickerType stickerType)
        {
            var type = (int)stickerType;
            return recentStickers[type];
        }

        public void AddRecentSticker(StickerType stickerType, TLDocument document, int date, bool remove)
        {
            int maxCount;

            var type = (int)stickerType;
            var found = false;
            for (int i = 0; i < recentStickers[type].Count; i++)
            {
                TLDocument image = recentStickers[type][i];
                if (image.Id == document.Id)
                {
                    recentStickers[type].RemoveAt(i);

                    if (!remove)
                    {
                        recentStickers[type].Insert(0, image);
                    }

                    found = true;
                }
            }

            if (!(found || remove))
            {
                recentStickers[type].Insert(0, document);
            }

            if (type == 2)
            {
                _protoService.FaveStickerAsync(document.ToInputDocument(), remove, result => { });
                maxCount = _cacheService.Config.StickersFavedLimit;
            }
            else
            {
                maxCount = _cacheService.Config.StickersRecentLimit;
            }

            if (recentStickers[type].Count > _cacheService.Config.StickersRecentLimit)
            {
                TLDocument old;
                if (remove)
                {
                    old = document;
                }
                else
                {
                    old = recentStickers[type][recentStickers[type].Count - 1];
                    recentStickers[type].RemoveAt(recentStickers[type].Count - 1);
                }

                try
                {
                    int cacheType;
                    if (type == 0)
                    {
                        cacheType = 3;
                    }
                    else if (type == 1)
                    {
                        cacheType = 4;
                    }
                    else
                    {
                        cacheType = 5;
                    }

                    Database database;
                    CreateDatabase.Open(out database);
                    DatabaseContext.Current.Execute(database, "DELETE FROM web_recent_v3 WHERE Id = '" + old.Id + "' AND MetaType = " + cacheType);
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
            }

            if (!remove)
            {
                List<TLDocument> arrayList = new List<TLDocument>();
                arrayList.Add(document);
                ProcessLoadedRecentDocuments(stickerType, arrayList, false, date);
            }

            _aggregator.Publish(new RecentsDidLoadedEventArgs(false, stickerType));
            //NotificationCenter.getInstance().postNotificationName(NotificationCenter.recentDocumentsDidLoaded, Boolean.valueOf(false), Integer.valueOf(type));
        }

        public bool IsStickerInFavorites(TLDocument document)
        {
            for (int i = 0; i < recentStickers[2].Count; i++)
            {
                var d = recentStickers[2][i];
                if (d.Id == document.Id && d.DCId == document.DCId)
                {
                    return true;
                }
            }

            return false;
        }

        public List<TLDocument> GetRecentGifs()
        {
            return new List<TLDocument>(recentGifs);
        }

        public void RemoveRecentGif(TLDocument document)
        {
            recentGifs.Remove(document);
            _protoService.SaveGifAsync(new TLInputDocument { Id = document.Id, AccessHash = document.AccessHash }, true, null);

            try
            {
                Database database;
                CreateDatabase.Open(out database);
                DatabaseContext.Current.Execute(database, "DELETE FROM web_recent_v3 WHERE Id = '" + document.Id + "' AND MetaType = 2");
            }
            catch (Exception e)
            {
                //FileLog.e("tmessages", e);
            }
        }

        public void AddRecentGif(TLDocument document, int date)
        {
            bool found = false;
            for (int i = 0; i < recentGifs.Count; i++)
            {
                TLDocument image = recentGifs[i];
                if (image.Id == document.Id)
                {
                    recentGifs.RemoveAt(i);
                    recentGifs.Insert(0, image);
                    found = true;
                }
            }
            if (!found)
            {
                recentGifs.Insert(0, document);
            }

            if (recentGifs.Count > _cacheService.Config.SavedGifsLimit)
            {
                TLDocument old = recentGifs[recentGifs.Count - 1];
                recentGifs.RemoveAt(recentGifs.Count - 1);
                try
                {
                    Database database;
                    CreateDatabase.Open(out database);
                    DatabaseContext.Current.Execute(database, "DELETE FROM web_recent_v3 WHERE Id = '" + old.Id + "' AND MetaType = 2");
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
            }

            List<TLDocument> arrayList = new List<TLDocument>();
            arrayList.Add(document);
            ProcessLoadedRecentDocuments(0, arrayList, true, date);
        }

        public bool IsLoadingStickers(StickerType stickerType)
        {
            return loadingStickers[(int)stickerType];
        }

        public TLMessagesStickerSet GetStickerSetByName(string name)
        {
            stickerSetsByName.TryGetValue(name, out TLMessagesStickerSet set);
            return set;
        }

        public TLMessagesStickerSet GetStickerSetById(long id)
        {
            stickerSetsById.TryGetValue(id, out TLMessagesStickerSet set);
            return set;
        }

        public Dictionary<string, List<TLDocument>> GetAllStickers()
        {
            return allStickers;
        }

        public int GetArchivedStickersCount(StickerType stickerType)
        {
            return archivedStickersCount[(int)stickerType];
        }

        public List<TLMessagesStickerSet> GetStickerSets(StickerType stickerType)
        {
            return stickerSets[(int)stickerType];
        }

        public List<TLMessagesStickerSet> GetFeaturedStickerSets()
        {
            return featuredStickerSets;
        }

        public List<long> GetUnreadStickerSets()
        {
            return unreadStickerSets;
        }

        public bool IsStickerPackInstalled(long id)
        {
            return stickerSetsById.ContainsKey(id);
        }

        public bool IsStickerPackUnread(long id)
        {
            return unreadStickerSets.Contains(id);
        }

        public bool IsStickerPackInstalled(string name)
        {
            return stickerSetsByName.ContainsKey(name);
        }

        public List<string> GetEmojiForSticker(long id)
        {
            if (stickersByEmoji.TryGetValue(id, out List<string> value))
            {
                return value;
            }

            return new List<string>(0);
        }

        private int CalculateDocumentsHash(List<TLDocument> arrayList)
        {
            if (arrayList == null)
            {
                return 0;
            }

            long acc = 0;
            for (int i = 0; i < Math.Min(_cacheService.Config.SavedGifsLimit, arrayList.Count); i++)
            {
                TLDocument document = arrayList[i];
                if (document == null)
                {
                    continue;
                }
                int high_id = (int)(document.Id >> 32);
                int lower_id = (int)document.Id;
                acc = ((acc * 20261) + 0x80000000L + high_id) % 0x80000000L;
                acc = ((acc * 20261) + 0x80000000L + lower_id) % 0x80000000L;
            }

            return (int)acc;
        }

        public TLMessagesStickerSet GetGroupStickerSetById(TLStickerSet stickerSet)
        {
            if (stickerSet == null)
            {
                return null;
            }

            if (!stickerSetsById.TryGetValue(stickerSet.Id, out TLMessagesStickerSet set))
            {
                if (!groupStickerSets.TryGetValue(stickerSet.Id, out set))
                {
                    LoadGroupStickerSet(stickerSet, true);
                }
                else if (set.Set.Hash != stickerSet.Hash)
                {
                    LoadGroupStickerSet(stickerSet, false);
                }
            }

            return set;
        }

        private void LoadGroupStickerSet(TLStickerSet stickerSet, bool cache)
        {
            if (cache)
            {
                //try
                //{
                //    TL_messages_stickerSet set;
                //    SQLiteCursor cursor = MessagesStorage.getInstance().getDatabase().queryFinalized("SELECT document FROM web_recent_v3 WHERE id = 's_" + stickerSet.id + "'", new Object[0]);
                //    if (!cursor.next() || cursor.isNull(0))
                //    {
                //        set = null;
                //    }
                //    else
                //    {
                //        NativeByteBuffer data = cursor.byteBufferValue(0);
                //        if (data != null)
                //        {
                //            set = TL_messages_stickerSet.TLdeserialize(data, data.readInt32(false), false);
                //            data.reuse();
                //        }
                //        else
                //        {
                //            set = null;
                //        }
                //    }
                //    cursor.dispose();
                //    if (set == null || set.set.hash != stickerSet.hash)
                //    {
                //        StickersQuery.loadGroupStickerSet(stickerSet, false);
                //    }
                //    if (set != null)
                //    {
                //        StickersQuery.groupStickerSets.put(Long.valueOf(set.set.id), set);
                //        NotificationCenter.getInstance().postNotificationName(NotificationCenter.groupStickersDidLoaded, Long.valueOf(set.set.id));
                //    }
                //}
                //catch (Throwable e)
                //{
                //    FileLog.m12e(e);
                //}

                TLMessagesStickerSet set;

                Database database;
                Statement statement;
                CreateDatabase.Open(out database);
                try
                {
                    Sqlite3.sqlite3_prepare_v2(database, "SELECT data, FROM group_stickers_v2 WHERE id = " + stickerSet.Id, out statement);

                    if (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
                    {
                        var data = Sqlite3.sqlite3_column_blob(statement, 0);
                        if (data != null)
                        {
                            using (var from = TLObjectSerializer.CreateReader(data.AsBuffer()))
                            {
                                set = TLFactory.Read<TLMessagesStickerSet>(from);
                            }
                        }
                        else
                        {
                            set = null;
                        }
                    }
                    else
                    {
                        set = null;
                    }

                    Sqlite3.sqlite3_finalize(statement);

                    if (set == null || set.Set.Hash != stickerSet.Hash)
                    {
                        LoadGroupStickerSet(stickerSet, false);
                    }

                    if (set != null)
                    {
                        groupStickerSets[set.Set.Id] = set;
                        _aggregator.Publish(new GroupStickersDidLoadedEventArgs(set.Set.Id));
                    }
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
                finally
                {
                    //Sqlite3.sqlite3_close(database);
                }

                return;
            }

            _protoService.GetStickerSetAsync(new TLInputStickerSetID { Id = stickerSet.Id, AccessHash = stickerSet.AccessHash }, result =>
            {
                try
                {
                    Database database;
                    Statement statement;
                    CreateDatabase.Open(out database);
                    DatabaseContext.Current.Execute(database, "CREATE TABLE IF NOT EXISTS group_stickers_v2(id BIGINT PRIMARY KEY, data BLOB);");

                    Sqlite3.sqlite3_prepare_v2(database, "REPLACE INTO group_stickers_v2 VALUES(?, ?, ?, ?)", out statement);

                    var data = TLObjectSerializer.Serialize(result);

                    Sqlite3.sqlite3_reset(statement);
                    Sqlite3.sqlite3_bind_int64(statement, 1, result.Set.Id);
                    Sqlite3.sqlite3_bind_blob(statement, 2, data.ToArray(), -1);
                    Sqlite3.sqlite3_step(statement);

                    Sqlite3.sqlite3_finalize(statement);
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }

                groupStickerSets[result.Set.Id] = result;
                _aggregator.Publish(new GroupStickersDidLoadedEventArgs(result.Set.Id));
            });
        }

        public void LoadRecents(StickerType stickerType, bool gif, bool cache, bool force)
        {
            var type = (int)stickerType;
            if (gif)
            {
                if (loadingRecentGifs)
                {
                    return;
                }
                loadingRecentGifs = true;
                if (recentGifsLoaded)
                {
                    cache = false;
                }
            }
            else
            {
                if (loadingRecentStickers[type])
                {
                    return;
                }
                loadingRecentStickers[type] = true;
                if (recentStickersLoaded[type])
                {
                    cache = false;
                }
            }
            if (cache)
            {
                try
                {
                    Database database;
                    Statement statement;
                    CreateDatabase.Open(out database);

                    List<TLDocument> arrayList = new List<TLDocument>();

                    int cacheType;
                    if (gif)
                    {
                        cacheType = 2;
                    }
                    else if (type == 0)
                    {
                        cacheType = 3;
                    }
                    else if (type == 1)
                    {
                        cacheType = 4;
                    }
                    else
                    {
                        cacheType = 5;
                    }

                    Sqlite3.sqlite3_prepare_v2(database, "SELECT Id,AccessHash,Date,MimeType,Size,Thumb,DCId,Version,Attributes,MetaType,MetaDate FROM web_recent_v3 WHERE MetaType = " + cacheType + " ORDER BY MetaDate DESC", out statement);

                    var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                    while (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
                    {
                        arrayList.Add(new TLDocument
                        {
                            Id = Sqlite3.sqlite3_column_int64(statement, 0),
                            AccessHash = Sqlite3.sqlite3_column_int64(statement, 1),
                            Date = Sqlite3.sqlite3_column_int(statement, 2),
                            MimeType = Sqlite3.sqlite3_column_text(statement, 3),
                            Size = Sqlite3.sqlite3_column_int(statement, 4),
                            Thumb = JsonConvert.DeserializeObject<TLPhotoSizeBase>(Sqlite3.sqlite3_column_text(statement, 5), settings),
                            DCId = Sqlite3.sqlite3_column_int(statement, 6),
                            Version = Sqlite3.sqlite3_column_int(statement, 7),
                            Attributes = JsonConvert.DeserializeObject<TLVector<TLDocumentAttributeBase>>(Sqlite3.sqlite3_column_text(statement, 8), settings)
                        });
                    }

                    Sqlite3.sqlite3_finalize(statement);

                    if (gif)
                    {
                        recentGifs = arrayList;
                        loadingRecentGifs = false;
                        recentGifsLoaded = true;
                    }
                    else
                    {
                        recentStickers[type] = arrayList;
                        loadingRecentStickers[type] = false;
                        recentStickersLoaded[type] = true;
                    }
                    //NotificationCenter.getInstance().postNotificationName(NotificationCenter.recentDocumentsDidLoaded, gif, type);
                    _aggregator.Publish(new RecentsDidLoadedEventArgs(gif, stickerType));

                    if (arrayList.Count == 0)
                    {
                        if (gif)
                        {
                            ApplicationSettings.Current.AddOrUpdateValue("lastGifLoadTime", 0L);
                        }
                        else if (type == 0)
                        {
                            ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTime", 0L);
                        }
                        else if (type == 1)
                        {
                            ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeMask", 0L);
                        }
                        else
                        {
                            ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeFavs", 0L);
                        }
                    }

                    LoadRecents(stickerType, gif, false, false);
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
            }
            else
            {
                if (!force)
                {
                    long lastLoadTime;
                    if (gif)
                    {
                        lastLoadTime = ApplicationSettings.Current.GetValueOrDefault<long>("lastGifLoadTime", 0);
                    }
                    else if (type == 0)
                    {
                        lastLoadTime = ApplicationSettings.Current.GetValueOrDefault<long>("lastStickersLoadTime", 0);
                    }
                    else if (type == 1)
                    {
                        lastLoadTime = ApplicationSettings.Current.GetValueOrDefault<long>("lastStickersLoadTimeMask", 0);
                    }
                    else
                    {
                        lastLoadTime = ApplicationSettings.Current.GetValueOrDefault<long>("lastStickersLoadTimeFavs", 0);
                    }
                    if (Math.Abs(Utils.CurrentTimestamp - lastLoadTime) < 60 * 60 * 1000)
                    {

                        if (gif)
                        {
                            loadingRecentGifs = false;
                            return;
                        }
                        else
                        {
                            loadingRecentStickers[type] = false;
                            return;
                        }
                    }
                }

                if (gif)
                {
                    var hash = CalculateDocumentsHash(recentGifs);
                    _protoService.GetSavedGifsAsync(hash, result =>
                    {
                        List<TLDocument> arrayList = null;
                        if (result is TLMessagesSavedGifs)
                        {
                            TLMessagesSavedGifs res = (TLMessagesSavedGifs)result;
                            arrayList = res.Gifs.OfType<TLDocument>().ToList();
                        }

                        ProcessLoadedRecentDocuments(stickerType, arrayList, gif, 0);
                    });
                }
                else
                {
                    if (stickerType == StickerType.Fave)
                    {
                        var hash = CalculateDocumentsHash(recentStickers[type]);
                        _protoService.GetFavedStickersAsync(hash, result =>
                        {
                            List<TLDocument> arrayList = null;
                            if (result is TLMessagesFavedStickers res)
                            {
                                arrayList = res.Stickers.OfType<TLDocument>().ToList();
                            }

                            ProcessLoadedRecentDocuments(stickerType, arrayList, gif, 0);
                        });
                    }
                    else
                    {
                        var hash = CalculateDocumentsHash(recentStickers[type]);
                        var attached = stickerType == StickerType.Mask;
                        _protoService.GetRecentStickersAsync(attached, hash, result =>
                        {
                            List<TLDocument> arrayList = null;
                            if (result is TLMessagesRecentStickers res)
                            {
                                arrayList = res.Stickers.OfType<TLDocument>().ToList();
                            }

                            ProcessLoadedRecentDocuments(stickerType, arrayList, gif, 0);
                        });
                    }

                }
            }
        }

        private void ProcessLoadedRecentDocuments(StickerType stickerType, List<TLDocument> documents, bool gif, int date)
        {
            var type = (int)stickerType;
            if (documents != null)
            {
                try
                {
                    var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

                    int maxCount;
                    int cacheType;

                    if (gif)
                    {
                        maxCount = _cacheService.Config.SavedGifsLimit;
                    }
                    else if (type == 2)
                    {
                        maxCount = _cacheService.Config.StickersFavedLimit;
                    }
                    else
                    {
                        maxCount = _cacheService.Config.StickersRecentLimit;
                    }

                    if (gif)
                    {
                        cacheType = 2;
                    }
                    else if (type == 0)
                    {
                        cacheType = 3;
                    }
                    else if (type == 1)
                    {
                        cacheType = 4;
                    }
                    else
                    {
                        cacheType = 5;
                    }
                    //SQLiteDatabase database = MessagesStorage.getInstance().getDatabase();
                    //int maxCount = gif ? MessagesController.getInstance().maxRecentGifsCount : MessagesController.getInstance().maxRecentStickersCount;
                    //database.beginTransaction();
                    //SQLitePreparedStatement state = database.executeFast("REPLACE INTO web_recent_v3 VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)");


                    Database database;
                    Statement statement;
                    CreateDatabase.Open(out database);

                    DatabaseContext.Current.Execute(database, "CREATE TABLE IF NOT EXISTS `web_recent_v3`(`Id` bigint primary key not null, `AccessHash` bigint, `Date` int, `MimeType` text, `Size` int, `Thumb` string, `DCId` int, `Version` int, `Attributes` string, `MetaType` int, `MetaDate` int, PRIMARY KEY (Id, MetaType))");
                    DatabaseContext.Current.Execute(database, "BEGIN IMMEDIATE TRANSACTION");
                    Sqlite3.sqlite3_prepare_v2(database, "INSERT OR REPLACE INTO `web_recent_v3` (Id,AccessHash,Date,MimeType,Size,Thumb,DCId,Version,Attributes,MetaType,MetaDate) VALUES(?,?,?,?,?,?,?,?,?,?,?)", out statement);

                    int count = documents.Count;
                    for (int i = 0; i < count; i++)
                    {
                        if (i == maxCount)
                        {
                            break;
                        }

                        TLDocument document = documents[i];

                        var thumb = JsonConvert.SerializeObject(document.Thumb, settings);
                        var attributes = JsonConvert.SerializeObject(document.Attributes, settings);

                        Sqlite3.sqlite3_reset(statement);
                        Sqlite3.sqlite3_bind_int64(statement, 1, document.Id);
                        Sqlite3.sqlite3_bind_int64(statement, 2, document.AccessHash);
                        Sqlite3.sqlite3_bind_int(statement, 3, document.Date);
                        Sqlite3.sqlite3_bind_text(statement, 4, document.MimeType, -1);
                        Sqlite3.sqlite3_bind_int(statement, 5, document.Size);
                        Sqlite3.sqlite3_bind_text(statement, 6, thumb, -1);
                        Sqlite3.sqlite3_bind_int(statement, 7, document.DCId);
                        Sqlite3.sqlite3_bind_int(statement, 8, document.Version);
                        Sqlite3.sqlite3_bind_text(statement, 9, attributes, -1);
                        Sqlite3.sqlite3_bind_int(statement, 10, cacheType);
                        Sqlite3.sqlite3_bind_int(statement, 11, date != 0 ? date : count - i);
                        Sqlite3.sqlite3_step(statement);
                    }

                    Sqlite3.sqlite3_finalize(statement);
                    DatabaseContext.Current.Execute(database, "COMMIT TRANSACTION");

                    if (documents.Count >= maxCount)
                    {
                        DatabaseContext.Current.Execute(database, "BEGIN IMMEDIATE TRANSACTION");
                        for (int i = maxCount; i < documents.Count; i++)
                        {
                            DatabaseContext.Current.Execute(database, "DELETE FROM web_recent_v3 WHERE Id = '" + documents[i].Id + "' AND MetaType = " + cacheType);
                        }
                        DatabaseContext.Current.Execute(database, "COMMIT TRANSACTION");
                    }
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
            }

            if (date == 0)
            {
                if (gif)
                {
                    loadingRecentGifs = false;
                    recentGifsLoaded = true;
                    ApplicationSettings.Current.AddOrUpdateValue("lastGifLoadTime", Utils.CurrentTimestamp);
                }
                else
                {
                    loadingRecentStickers[type] = false;
                    recentStickersLoaded[type] = true;
                    if (type == 0)
                    {
                        ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTime", Utils.CurrentTimestamp);
                    }
                    else if (type == 1)
                    {
                        ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeMask", Utils.CurrentTimestamp);
                    }
                    else
                    {
                        ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeFavs", Utils.CurrentTimestamp);
                    }
                }

                if (documents != null)
                {
                    if (gif)
                    {
                        recentGifs = documents;
                    }
                    else
                    {
                        recentStickers[type] = documents;
                    }

                    //NotificationCenter.getInstance().postNotificationName(NotificationCenter.recentDocumentsDidLoaded, gif, type);
                    _aggregator.Publish(new RecentsDidLoadedEventArgs(gif, stickerType));
                }
            }
        }

        public void ReorderStickers(StickerType stickerType, IList<long> order)
        {
            var type = (int)stickerType;
            stickerSets[type].Sort((lhs, rhs) =>
            {
                int index1 = order.IndexOf(lhs.Set.Id);
                int index2 = order.IndexOf(rhs.Set.Id);
                if (index1 > index2)
                {
                    return 1;
                }
                else if (index1 < index2)
                {
                    return -1;
                }
                return 0;
            });

            loadHash[type] = CalculateStickersHash(stickerSets[type]);
            //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
            _aggregator.Publish(new StickersDidLoadedEventArgs(stickerType));
            LoadStickers(stickerType, false, true);
        }

        public void CalculateNewHash(StickerType stickerType)
        {
            var type = (int)stickerType;
            loadHash[type] = CalculateStickersHash(stickerSets[type]);
        }

        public void AddNewStickerSet(TLMessagesStickerSet set)
        {
            if (stickerSetsById.ContainsKey(set.Set.Id) || stickerSetsByName.ContainsKey(set.Set.ShortName))
            {
                return;
            }
            StickerType stickerType = set.Set.IsMasks ? StickerType.Mask : StickerType.Image;
            int type = (int)stickerType;
            stickerSets[type].Insert(0, set);
            stickerSetsById[set.Set.Id] = set;
            stickerSetsByName[set.Set.ShortName] = set;
            Dictionary<long, TLDocument> stickersById = new Dictionary<long, TLDocument>();
            for (int i = 0; i < set.Documents.Count; i++)
            {
                TLDocument document = set.Documents[i] as TLDocument;
                stickersById[document.Id] = document;
            }
            for (int i = 0; i < set.Packs.Count; i++)
            {
                TLStickerPack stickerPack = set.Packs[i];
                stickerPack.Emoticon = stickerPack.Emoticon.Replace("\uFE0F", "");
                List<TLDocument> arrayList;
                allStickers.TryGetValue(stickerPack.Emoticon, out arrayList);
                if (arrayList == null)
                {
                    arrayList = new List<TLDocument>();
                    allStickers[stickerPack.Emoticon] = arrayList;
                }
                for (int k = 0; k < stickerPack.Documents.Count; k++)
                {
                    long id = stickerPack.Documents[k];
                    List<string> emojiList;
                    stickersByEmoji.TryGetValue(id, out emojiList);
                    if (emojiList == null)
                    {
                        emojiList = new List<string>();
                        stickersByEmoji[id] = emojiList;
                    }

                    emojiList.Add(stickerPack.Emoticon);

                    if (stickersById.TryGetValue(id, out TLDocument sticker))
                    {
                        arrayList.Add(sticker);
                    }
                }
            }
            loadHash[type] = CalculateStickersHash(stickerSets[type]);
            //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
            _aggregator.Publish(new StickersDidLoadedEventArgs(stickerType));
            LoadStickers(stickerType, false, true);
        }

        public void LoadArchivedStickersCount(StickerType stickerType, bool cache)
        {
            var type = (int)stickerType;
            if (cache)
            {
                int count = ApplicationSettings.Current.GetValueOrDefault("archivedStickersCount" + type, -1);
                if (count == -1)
                {
                    LoadArchivedStickersCount(stickerType, false);
                    return;
                }

                archivedStickersCount[type] = count;
                //NotificationCenter.getInstance().postNotificationName(NotificationCenter.archivedStickersCountDidLoaded, new Object[] { Integer.valueOf(type) });
                _aggregator.Publish(new ArchivedStickersCountDidLoadedEventArgs(stickerType));
                LoadArchivedStickersCount(stickerType, false);
            }
            else
            {
                var req = new TLMessagesGetArchivedStickers();
                req.Limit = 0;
                req.IsMasks = stickerType == StickerType.Mask;
                _protoService.SendRequestAsync<TLMessagesArchivedStickers>("messages.getArchivedStickers", req, result =>
                {
                    archivedStickersCount[type] = result.Count;
                    ApplicationSettings.Current.AddOrUpdateValue("archivedStickersCount" + type, result.Count);
                    //NotificationCenter.getInstance().postNotificationName(NotificationCenter.archivedStickersCountDidLoaded, new Object[] { Integer.valueOf(StickersQuery.19.this.val$type) });
                    _aggregator.Publish(new ArchivedStickersCountDidLoadedEventArgs(stickerType));
                });
            }
        }

        public void LoadFeaturedStickers(bool cache, bool force)
        {
            if (loadingFeaturedStickers)
            {
                return;
            }
            loadingFeaturedStickers = true;
            if (cache)
            {
                List<TLMessagesStickerSet> newStickerArray = null;
                List<long> unread = null;
                int date = 0;
                int hash = 0;

                Database database;
                Statement statement;
                CreateDatabase.Open(out database);
                try
                {
                    Sqlite3.sqlite3_prepare_v2(database, "SELECT data, unread, date, hash FROM stickers_featured WHERE 1", out statement);

                    if (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
                    {
                        var data = Sqlite3.sqlite3_column_blob(statement, 0);
                        if (data != null)
                        {
                            using (var from = TLObjectSerializer.CreateReader(data.AsBuffer()))
                            {
                                newStickerArray = TLFactory.Read<TLVector<TLMessagesStickerSet>>(from).ToList();
                            }
                        }

                        var data2 = Sqlite3.sqlite3_column_blob(statement, 1);
                        if (data2 != null)
                        {
                            using (var from = TLObjectSerializer.CreateReader(data.AsBuffer()))
                            {
                                unread = TLFactory.Read<TLVector<long>>(from).ToList();
                            }
                        }

                        date = Sqlite3.sqlite3_column_int(statement, 2);
                        hash = CalculateFeaturedStickersHash(newStickerArray);
                    }

                    Sqlite3.sqlite3_finalize(statement);
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
                finally
                {
                    //Sqlite3.sqlite3_close(database);
                }

                ProcessLoadedFeaturedStickers(newStickerArray, unread, true, date, hash);
            }
            else
            {
                var hash = force ? 0 : loadFeaturedHash;
                _protoService.GetFeaturedStickersAsync(hash, result =>
                {
                    if (result is TLMessagesFeaturedStickers res)
                    {
                        List<TLMessagesStickerSet> newStickerArray = new List<TLMessagesStickerSet>();
                        if (res.Sets.Count == 0)
                        {
                            ProcessLoadedFeaturedStickers(newStickerArray, res.Unread, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                        }
                        else
                        {
                            ConcurrentDictionary<long, TLMessagesStickerSet> newStickerSets = new ConcurrentDictionary<long, TLMessagesStickerSet>();
                            for (int i = 0; i < res.Sets.Count; i++)
                            {
                                TLStickerSetCoveredBase stickerSet = res.Sets[i];

                                if (featuredStickerSetsById.TryGetValue(stickerSet.Set.Id, out TLMessagesStickerSet oldSet) && oldSet.Set.Hash == stickerSet.Set.Hash)
                                {
                                    oldSet.Set.IsArchived = stickerSet.Set.IsArchived;
                                    oldSet.Set.IsInstalled = stickerSet.Set.IsInstalled;
                                    oldSet.Set.IsOfficial = stickerSet.Set.IsOfficial;
                                    newStickerSets[oldSet.Set.Id] = oldSet;
                                    newStickerArray.Add(oldSet);

                                    if (newStickerSets.Count == res.Sets.Count)
                                    {
                                        ProcessLoadedFeaturedStickers(newStickerArray, res.Unread, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                                    }
                                    continue;
                                }

                                newStickerArray.Add(null);
                                int index = i;

                                _protoService.GetStickerSetAsync(new TLInputStickerSetID { Id = stickerSet.Set.Id, AccessHash = stickerSet.Set.AccessHash }, callback =>
                                {
                                    newStickerArray[index] = callback;
                                    newStickerSets[stickerSet.Set.Id] = callback;
                                    if (newStickerSets.Count == res.Sets.Count)
                                    {
                                        for (int j = 0; j < newStickerArray.Count; j++)
                                        {
                                            if (newStickerArray[j] == null)
                                            {
                                                newStickerArray.RemoveAt(j);
                                            }
                                        }

                                        ProcessLoadedFeaturedStickers(newStickerArray, res.Unread, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                                    }
                                });

                            }
                        }
                    }
                    else
                    {
                        ProcessLoadedFeaturedStickers(null, null, false, (int)(Utils.CurrentTimestamp / 1000), hash);
                    }
                });
            }
        }

        private void ProcessLoadedFeaturedStickers(IList<TLMessagesStickerSet> res, IList<long> unreadStickers, bool cache, int date, int hash)
        {
            loadingFeaturedStickers = false;
            featuredStickersLoaded = true;
            //Utilities.stageQueue.postRunnable(new Runnable()
            //{
            //    @Override
            //    public void run()
            //    {
            if (cache && (res == null || Math.Abs(Utils.CurrentTimestamp / 1000 - date) >= 60 * 60) || !cache && res == null && hash == 0)
            {
                //AndroidUtilities.runOnUIThread(new Runnable() {
                //    @Override
                //    public void run()
                //{
                if (res != null && hash != 0)
                {
                    loadFeaturedHash = hash;
                }
                LoadFeaturedStickers(false, false);
                //    }
                //}, res == null && !cache ? 1000 : 0);
                if (res == null)
                {
                    return;
                }
            }
            if (res != null)
            {
                try
                {
                    List<TLMessagesStickerSet> stickerSetsNew = new List<TLMessagesStickerSet>();
                    Dictionary<long, TLMessagesStickerSet> stickerSetsByIdNew = new Dictionary<long, TLMessagesStickerSet>();

                    for (int i = 0; i < res.Count; i++)
                    {
                        TLMessagesStickerSet stickerSet = res[i];
                        stickerSetsNew.Add(stickerSet);
                        stickerSetsByIdNew[stickerSet.Set.Id] = stickerSet;
                    }

                    if (!cache)
                    {
                        PutFeaturedStickersToCache(stickerSetsNew, unreadStickers, date, hash);
                    }
                    //AndroidUtilities.runOnUIThread(new Runnable()
                    //{
                    //    @Override
                    //                            public void run()
                    //    {
                    unreadStickerSets = unreadStickers.ToList();
                    featuredStickerSetsById = stickerSetsByIdNew;
                    featuredStickerSets = stickerSetsNew;
                    loadFeaturedHash = hash;
                    loadFeaturedDate = date;
                    //NotificationCenter.getInstance().postNotificationName(NotificationCenter.featuredStickersDidLoaded);
                    _aggregator.Publish(new FeaturedStickersDidLoadedEventArgs());
                    //    }
                    //});
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
            }
            else if (!cache)
            {
                //                    AndroidUtilities.runOnUIThread(new Runnable()
                //{
                //    @Override
                //                        public void run()
                //    {
                loadFeaturedDate = date;
                //    }
                //});
                PutFeaturedStickersToCache(null, null, date, 0);
            }
            //    }
            //});
        }

        private void PutFeaturedStickersToCache(IList<TLMessagesStickerSet> stickers, IList<long> unreadStickers, int date, int hash)
        {
            TLVector<TLMessagesStickerSet> stickersFinal = stickers != null ? new TLVector<TLMessagesStickerSet>(stickers) : null;

            try
            {
                Database database;
                Statement statement;
                CreateDatabase.Open(out database);
                DatabaseContext.Current.Execute(database, "CREATE TABLE IF NOT EXISTS stickers_featured(id INTEGER PRIMARY KEY, data BLOB, unread BLOB, date INTEGER, hash TEXT);");

                if (stickersFinal != null)
                {
                    Sqlite3.sqlite3_prepare_v2(database, "REPLACE INTO stickers_featured VALUES(?, ?, ?, ?, ?)", out statement);

                    var data1 = TLObjectSerializer.Serialize(stickersFinal);
                    var data2 = TLObjectSerializer.Serialize(new TLVector<long>(unreadStickers));

                    Sqlite3.sqlite3_reset(statement);
                    Sqlite3.sqlite3_bind_int(statement, 1, 1);
                    Sqlite3.sqlite3_bind_blob(statement, 2, data1.ToArray(), -1);
                    Sqlite3.sqlite3_bind_blob(statement, 3, data2.ToArray(), -1);
                    Sqlite3.sqlite3_bind_int(statement, 4, date);
                    Sqlite3.sqlite3_bind_int(statement, 5, hash);
                    Sqlite3.sqlite3_step(statement);

                    Sqlite3.sqlite3_finalize(statement);
                }
                else
                {
                    Sqlite3.sqlite3_prepare_v2(database, "UPDATE stickers_featured SET date = ?", out statement);
                    Sqlite3.sqlite3_reset(statement);
                    Sqlite3.sqlite3_bind_int(statement, 1, date);
                    Sqlite3.sqlite3_step(statement);
                    Sqlite3.sqlite3_finalize(statement);
                }
            }
            catch (Exception e)
            {
                //FileLog.e("tmessages", e);
            }
        }

        private int CalculateFeaturedStickersHash(IList<TLMessagesStickerSet> sets)
        {
            long acc = 0;
            for (int i = 0; i < sets.Count; i++)
            {
                TLStickerSet set = sets[i].Set;
                if (set.IsArchived)
                {
                    continue;
                }
                int high_id = (int)(set.Id >> 32);
                int lower_id = (int)set.Id;
                acc = ((acc * 20261) + 0x80000000L + high_id) % 0x80000000L;
                acc = ((acc * 20261) + 0x80000000L + lower_id) % 0x80000000L;
                if (unreadStickerSets.Contains(set.Id))
                {
                    acc = ((acc * 20261) + 0x80000000L + 1) % 0x80000000L;
                }
            }
            return (int)acc;
        }

        public void MarkFeaturedStickersAsRead(bool query)
        {
            if (unreadStickerSets.Count == 0)
            {
                return;
            }
            unreadStickerSets.Clear();
            loadFeaturedHash = CalculateFeaturedStickersHash(featuredStickerSets);
            //NotificationCenter.getInstance().postNotificationName(NotificationCenter.featuredStickersDidLoaded);
            _aggregator.Publish(new FeaturedStickersDidLoadedEventArgs());
            PutFeaturedStickersToCache(featuredStickerSets, unreadStickerSets, loadFeaturedDate, loadFeaturedHash);
            if (query)
            {

                _protoService.ReadFeaturedStickersAsync(null, null);
            }
        }

        public void MarkFeaturedStickersByIdAsRead(long id)
        {
            if (!unreadStickerSets.Contains(id) || readingStickerSets.Contains(id))
            {
                return;
            }
            readingStickerSets.Add(id);
            _protoService.ReadFeaturedStickersAsync(new TLVector<long> { id }, null);

            //AndroidUtilities.runOnUIThread(new Runnable()
            //{
            //    @Override
            //    public void run()
            //    {
            unreadStickerSets.Remove(id);
            readingStickerSets.Remove(id);
            loadFeaturedHash = CalculateFeaturedStickersHash(featuredStickerSets);
            //NotificationCenter.getInstance().postNotificationName(NotificationCenter.featuredStickersDidLoaded);
            _aggregator.Publish(new FeaturedStickersDidLoadedEventArgs());
            PutFeaturedStickersToCache(featuredStickerSets, unreadStickerSets, loadFeaturedDate, loadFeaturedHash);
            //    }
            //}, 1000);
        }

        public void LoadStickers(StickerType stickerType, bool cache, bool force)
        {
            var type = (int)stickerType;
            if (loadingStickers[type])
            {
                return;
            }
            loadingStickers[type] = true;
            if (cache)
            {
                List<TLMessagesStickerSet> newStickerArray = null;
                int date = 0;
                int hash = 0;
                Database database;
                Statement statement;
                CreateDatabase.Open(out database);
                try
                {
                    Sqlite3.sqlite3_prepare_v2(database, "SELECT data, date, hash FROM stickers_v2 WHERE id = " + (type + 1), out statement);

                    if (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
                    {
                        var data = Sqlite3.sqlite3_column_blob(statement, 0);
                        if (data != null)
                        {
                            using (var from = TLObjectSerializer.CreateReader(data.AsBuffer()))
                            {
                                newStickerArray = TLFactory.Read<TLVector<TLMessagesStickerSet>>(from).ToList();
                            }
                        }

                        date = Sqlite3.sqlite3_column_int(statement, 1);
                        hash = CalculateStickersHash(newStickerArray);
                    }

                    Sqlite3.sqlite3_finalize(statement);
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
                finally
                {
                    //Sqlite3.sqlite3_close(database);
                }

                ProcessLoadedStickers(stickerType, newStickerArray, true, date, hash);
            }
            else
            {
                TLObject req;
                int hash;
                string caption;
                if (stickerType == StickerType.Image)
                {
                    req = new TLMessagesGetAllStickers();
                    hash = ((TLMessagesGetAllStickers)req).Hash = force ? 0 : loadHash[type];
                    caption = "messages.getAllStickers";
                }
                else
                {
                    req = new TLMessagesGetMaskStickers();
                    hash = ((TLMessagesGetMaskStickers)req).Hash = force ? 0 : loadHash[type];
                    caption = "messages.getMaskStickers";
                }

                _protoService.SendRequestAsync<TLMessagesAllStickersBase>(caption, req, result =>
                {
                    if (result is TLMessagesAllStickers res)
                    {
                        List<TLMessagesStickerSet> newStickerArray = new List<TLMessagesStickerSet>();
                        if (res.Sets.Count == 0)
                        {
                            ProcessLoadedStickers(stickerType, newStickerArray, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                        }
                        else
                        {
                            ConcurrentDictionary<long, TLMessagesStickerSet> newStickerSets = new ConcurrentDictionary<long, TLMessagesStickerSet>();
                            for (int i = 0; i < res.Sets.Count; i++)
                            {
                                TLStickerSet stickerSet = res.Sets[i];

                                if (stickerSetsById.TryGetValue(stickerSet.Id, out TLMessagesStickerSet oldSet) && oldSet.Set.Hash == stickerSet.Hash)
                                {
                                    oldSet.Set.IsArchived = stickerSet.IsArchived;
                                    oldSet.Set.IsInstalled = stickerSet.IsInstalled;
                                    oldSet.Set.IsOfficial = stickerSet.IsOfficial;
                                    newStickerSets[oldSet.Set.Id] = oldSet;
                                    newStickerArray.Add(oldSet);

                                    if (newStickerSets.Count == res.Sets.Count)
                                    {
                                        ProcessLoadedStickers(stickerType, newStickerArray, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                                    }
                                    continue;
                                }

                                newStickerArray.Add(null);
                                int index = i;

                                _protoService.GetStickerSetAsync(new TLInputStickerSetID { Id = stickerSet.Id, AccessHash = stickerSet.AccessHash }, callback =>
                                {
                                    newStickerArray[index] = callback;
                                    newStickerSets[stickerSet.Id] = callback;
                                    if (newStickerSets.Count == res.Sets.Count)
                                    {
                                        for (int j = 0; j < newStickerArray.Count; j++)
                                        {
                                            if (newStickerArray[j] == null)
                                            {
                                                newStickerArray.RemoveAt(j);
                                            }
                                        }

                                        ProcessLoadedStickers(stickerType, newStickerArray, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                                    }
                                });

                            }
                        }
                    }
                    else
                    {
                        ProcessLoadedStickers(stickerType, null, false, (int)(Utils.CurrentTimestamp / 1000), hash);
                    }
                });
            }
        }

        private void PutStickersToCache(StickerType stickerType, List<TLMessagesStickerSet> stickers, int date, int hash)
        {
            TLVector<TLMessagesStickerSet> stickersFinal = stickers != null ? new TLVector<TLMessagesStickerSet>(stickers) : null;

            try
            {
                Database database;
                Statement statement;
                CreateDatabase.Open(out database);
                DatabaseContext.Current.Execute(database, "CREATE TABLE IF NOT EXISTS stickers_v2(id INTEGER PRIMARY KEY, data BLOB, date INTEGER, hash TEXT);");

                if (stickersFinal != null)
                {
                    Sqlite3.sqlite3_prepare_v2(database, "REPLACE INTO stickers_v2 VALUES(?, ?, ?, ?)", out statement);

                    var data = TLObjectSerializer.Serialize(stickersFinal);

                    Sqlite3.sqlite3_reset(statement);
                    Sqlite3.sqlite3_bind_int(statement, 1, stickerType == StickerType.Image ? 1 : 2);
                    Sqlite3.sqlite3_bind_blob(statement, 2, data.ToArray(), -1);
                    Sqlite3.sqlite3_bind_int(statement, 3, date);
                    Sqlite3.sqlite3_bind_int(statement, 4, hash);
                    Sqlite3.sqlite3_step(statement);

                    Sqlite3.sqlite3_finalize(statement);
                }
                else
                {
                    Sqlite3.sqlite3_prepare_v2(database, "UPDATE stickers_v2 SET date = ?", out statement);
                    Sqlite3.sqlite3_reset(statement);
                    Sqlite3.sqlite3_bind_int(statement, 1, date);
                    Sqlite3.sqlite3_step(statement);
                    Sqlite3.sqlite3_finalize(statement);
                }
            }
            catch (Exception e)
            {
                //FileLog.e("tmessages", e);
            }
        }

        public string GetStickerSetName(long setId)
        {
            TLMessagesStickerSet stickerSet = stickerSetsById[setId];
            return stickerSet != null ? stickerSet.Set.ShortName : null;
        }

        public long GetStickerSetId(TLDocument document)
        {
            for (int i = 0; i < document.Attributes.Count; i++)
            {
                TLDocumentAttributeBase attribute = document.Attributes[i];
                if (attribute is TLDocumentAttributeSticker stickerAttribute)
                {
                    if (stickerAttribute.StickerSet is TLInputStickerSetID inputStickerSet)
                    {
                        return inputStickerSet.Id;
                    }

                    break;
                }
            }

            return -1;
        }

        private int CalculateStickersHash(List<TLMessagesStickerSet> sets)
        {
            long acc = 0;
            for (int i = 0; i < sets.Count; i++)
            {
                TLStickerSet set = sets[i].Set;
                if (set.IsArchived)
                {
                    continue;
                }

                acc = ((acc * 20261) + 0x80000000L + set.Hash) % 0x80000000L;
            }

            return (int)acc;
        }

        private void ProcessLoadedStickers(StickerType stickerType, List<TLMessagesStickerSet> res, bool cache, int date, int hash)
        {
            var type = (int)stickerType;
            loadingStickers[type] = false;
            stickersLoaded[type] = true;

            //    Utilities.stageQueue.postRunnable(new Runnable()
            //{
            //    @Override
            //    public void run()
            //    {
            if (cache && (res == null || Math.Abs(Utils.CurrentTimestamp / 1000 - date) >= 60 * 60) || !cache && res == null && hash == 0)
            {
                //AndroidUtilities.runOnUIThread(new Runnable() {
                //    @Override
                //    public void run()
                //{
                if (res != null && hash != 0)
                {
                    loadHash[type] = hash;
                }
                LoadStickers(stickerType, false, false);
                //    }
                //}, res == null && !cache ? 1000 : 0);
                if (res == null)
                {
                    return;
                }
            }
            if (res != null)
            {
                try
                {
                    List<TLMessagesStickerSet> stickerSetsNew = new List<TLMessagesStickerSet>();
                    Dictionary<long, TLMessagesStickerSet> stickerSetsByIdNew = new Dictionary<long, TLMessagesStickerSet>();
                    Dictionary<string, TLMessagesStickerSet> stickerSetsByNameNew = new Dictionary<string, TLMessagesStickerSet>();
                    Dictionary<long, List<string>> stickersByEmojiNew = new Dictionary<long, List<string>>();
                    Dictionary<long, TLDocument> stickersByIdNew = new Dictionary<long, TLDocument>();
                    Dictionary<string, List<TLDocument>> allStickersNew = new Dictionary<string, List<TLDocument>>();

                    for (int i = 0; i < res.Count; i++)
                    {
                        TLMessagesStickerSet stickerSet = res[i];
                        if (stickerSet == null)
                        {
                            continue;
                        }
                        stickerSetsNew.Add(stickerSet);
                        stickerSetsByIdNew[stickerSet.Set.Id] = stickerSet;
                        stickerSetsByNameNew[stickerSet.Set.ShortName] = stickerSet;

                        for (int j = 0; j < stickerSet.Documents.Count; j++)
                        {
                            TLDocumentBase document = stickerSet.Documents[j];
                            if (document == null || document is TLDocumentEmpty)
                            {
                                continue;
                            }
                            stickersByIdNew[document.Id] = document as TLDocument;
                        }
                        if (!stickerSet.Set.IsArchived)
                        {
                            for (int j = 0; j < stickerSet.Packs.Count; j++)
                            {
                                TLStickerPack stickerPack = stickerSet.Packs[j];
                                if (stickerPack == null || stickerPack.Emoticon == null)
                                {
                                    continue;
                                }

                                stickerPack.Emoticon = stickerPack.Emoticon.Replace("\uFE0F", "");
                                allStickersNew.TryGetValue(stickerPack.Emoticon, out List<TLDocument> arrayList);
                                if (arrayList == null)
                                {
                                    arrayList = new List<TLDocument>();
                                    allStickersNew[stickerPack.Emoticon] = arrayList;
                                }

                                for (int k = 0; k < stickerPack.Documents.Count; k++)
                                {
                                    long id = stickerPack.Documents[k];
                                    stickersByEmojiNew.TryGetValue(id, out List<string> emojiList);
                                    if (emojiList == null)
                                    {
                                        emojiList = new List<string>();
                                        stickersByEmojiNew[id] = emojiList;
                                    }

                                    emojiList.Add(stickerPack.Emoticon);

                                    if (stickersByIdNew.TryGetValue(id, out TLDocument sticker))
                                    {
                                        arrayList.Add(sticker);
                                    }
                                }
                            }
                        }
                    }

                    if (!cache)
                    {
                        PutStickersToCache(stickerType, stickerSetsNew, date, hash);
                    }
                    //                        AndroidUtilities.runOnUIThread(new Runnable()
                    //{
                    //    @Override
                    //                            public void run()
                    //    {
                    for (int i = 0; i < stickerSets[type].Count; i++)
                    {
                        TLStickerSet set = stickerSets[type][i].Set;
                        stickerSetsById.Remove(set.Id);
                        stickerSetsByName.Remove(set.ShortName);
                    }
                    stickerSetsById.PutRange(stickerSetsByIdNew);
                    stickerSetsByName.PutRange(stickerSetsByNameNew);
                    stickerSets[type] = stickerSetsNew;
                    loadHash[type] = hash;
                    loadDate[type] = date;
                    if (stickerType == StickerType.Image)
                    {
                        allStickers = allStickersNew;
                        stickersByEmoji = stickersByEmojiNew;
                    }
                    //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
                    _aggregator.Publish(new StickersDidLoadedEventArgs(stickerType));
                    //    }
                    //});
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
            }
            else if (!cache)
            {
                //                    AndroidUtilities.runOnUIThread(new Runnable()
                //{
                //    @Override
                //                        public void run()
                //    {
                loadDate[type] = date;
                //    }
                //});
                PutStickersToCache(stickerType, null, date, 0);
            }
            //    }
            //});        
        }

        public void RemoveStickersSet(TLStickerSet stickerSet, int hide, bool showSettings)
        {
            StickerType stickerType = stickerSet.IsMasks ? StickerType.Mask : StickerType.Image;
            int type = (int)stickerType;
            TLInputStickerSetID stickerSetID = new TLInputStickerSetID();
            stickerSetID.AccessHash = stickerSet.AccessHash;
            stickerSetID.Id = stickerSet.Id;
            if (hide != 0)
            {
                stickerSet.IsArchived = hide == 1;
                for (int i = 0; i < stickerSets[type].Count; i++)
                {
                    TLMessagesStickerSet set = stickerSets[type][i];
                    if (set.Set.Id == stickerSet.Id)
                    {
                        stickerSets[type].RemoveAt(i);
                        if (hide == 2)
                        {
                            stickerSets[type].Insert(0, set);
                        }
                        else
                        {
                            stickerSetsById.Remove(set.Set.Id);
                            stickerSetsByName.Remove(set.Set.ShortName);
                        }
                        break;
                    }
                }
                loadHash[type] = CalculateStickersHash(stickerSets[type]);
                PutStickersToCache(stickerType, stickerSets[type], loadDate[type], loadHash[type]);
                //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
                _aggregator.Publish(new StickersDidLoadedEventArgs(stickerType));
                TLMessagesInstallStickerSet req = new TLMessagesInstallStickerSet();
                req.StickerSet = stickerSetID;
                req.Archived = hide == 1;
                _protoService.SendRequestAsync<TLMessagesStickerSetInstallResultBase>("messages.installStickerSet", req, result =>
                {
                    if (result is TLMessagesStickerSetInstallResultArchive)
                    {
                        //NotificationCenter.getInstance().postNotificationName(NotificationCenter.needReloadArchivedStickers, type);
                        _aggregator.Publish(new NeedReloadArchivedStickersEventArgs(stickerType));
                        //if (hide != 1 && baseFragment != null && baseFragment.getParentActivity() != null)
                        //{
                        //    StickersArchiveAlert alert = new StickersArchiveAlert(baseFragment.getParentActivity(), showSettings ? baseFragment : null, ((TLRPC.TL_messages_stickerSetInstallResultArchive)response).sets);
                        //    baseFragment.showDialog(alert.create());
                        //}
                    }
                    LoadStickers(stickerType, false, false);
                });


            }
            else
            {
                TLMessagesUninstallStickerSet req = new TLMessagesUninstallStickerSet();
                req.StickerSet = stickerSetID;
                _protoService.SendRequestAsync<bool>("messages.uninstallStickerSet", req, result =>
                {
                    //try
                    //{
                    //    if (error == null)
                    //    {
                    //        if (stickerSet.masks)
                    //        {
                    //            Toast.makeText(context, LocaleController.getString("MasksRemoved", R.string.MasksRemoved), Toast.LENGTH_SHORT).show();
                    //        }
                    //        else
                    //        {
                    //            Toast.makeText(context, LocaleController.getString("StickersRemoved", R.string.StickersRemoved), Toast.LENGTH_SHORT).show();
                    //        }
                    //    }
                    //    else
                    //    {
                    //        Toast.makeText(context, LocaleController.getString("ErrorOccurred", R.string.ErrorOccurred), Toast.LENGTH_SHORT).show();
                    //    }
                    //}
                    //catch (Exception e)
                    //{
                    //    FileLog.e("tmessages", e);
                    //}
                    LoadStickers(stickerType, false, true);

                }, fault =>
                {
                    LoadStickers(stickerType, false, true);
                });
            }
        }









        public List<TLDocument> LoadStickersForEmoji(string emoji)
        {
            var search = emoji != null && emoji.Length > 0 && emoji.Length <= 14;
            if (search)
            {
                int length = emoji.Length;
                for (int a = 0; a < length; a++)
                {
                    if (a < length - 1 && (emoji[a] == 0xD83C && emoji[a + 1] >= 0xDFFB && emoji[a + 1] <= 0xDFFF || emoji[a] == 0x200D && (emoji[a + 1] == 0x2640 || emoji[a + 1] == 0x2642)))
                    {
                        emoji = string.Concat(emoji.Substring(0, a), emoji.Substring(a + 2));
                        length -= 2;
                        a--;
                    }
                    else if (emoji[a] == 0xfe0f)
                    {
                        emoji = string.Concat(emoji.Substring(0, a), emoji.Substring(a + 1));
                        length--;
                        a--;
                    }
                }
                string lastSticker = emoji.ToString();
                Dictionary<string, List<TLDocument>> allStickers = GetAllStickers();
                if (allStickers != null)
                {
                    List<TLDocument> newStickers;
                    allStickers.TryGetValue(lastSticker, out newStickers);
                    //if (stickers != null && newStickers == null)
                    //{
                    //    if (visible)
                    //    {
                    //        delegate.needChangePanelVisibility(false);
                    //        visible = false;
                    //    }
                    //}
                    //else
                    {
                        var stickers = newStickers != null && newStickers.Count > 0 ? new List<TLDocument>(newStickers) : null;
                        if (stickers != null)
                        {
#if DEBUG
                            var noDuples = stickers.Distinct(new FunctionalEqualityComparer<TLDocument>((x, y) => x.Id == y.Id, x => x.Id.GetHashCode())).ToList();
                            if (noDuples.Count != stickers.Count)
                            {
                                Debugger.Break();
                            }
#endif
                        }

                        return stickers;

                        if (stickers != null)
                        {
                            List<TLDocument> recentStickers = GetRecentStickersNoCopy(StickerType.Image);
                            if (recentStickers.Count > 0)
                            {
                                stickers.Sort(new Comparison<TLDocument>((lhs, rhs) =>
                                {
                                    int getIndex(long id)
                                    {
                                        for (int a = 0; a < recentStickers.Count; a++)
                                        {
                                            if (recentStickers[a].Id == id)
                                            {
                                                return a;
                                            }
                                        }
                                        return -1;
                                    }

                                    int idx1 = getIndex(lhs.Id);
                                    int idx2 = getIndex(rhs.Id);
                                    if (idx1 > idx2)
                                    {
                                        return -1;
                                    }
                                    else if (idx1 < idx2)
                                    {
                                        return 1;
                                    }

                                    return 0;
                                }));
                            }
                        }
                        //checkStickerFilesExistAndDownload();
                        //delegate.needChangePanelVisibility(stickers != null && !stickers.isEmpty() && stickersToLoad.isEmpty());
                        //notifyDataSetChanged();
                        //visible = true;
                    }
                }
            }
            //if (!search)
            //{
            //    if (visible && stickers != null)
            //    {
            //        visible = false;
            //        delegate.needChangePanelVisibility(false);
            //    }
            //}

            return null;
        }
    }

    public enum StickerType : int
    {
        Image = 0,
        Mask = 1,
        Fave = 2
    }

    public class NeedReloadArchivedStickersEventArgs : EventArgs
    {
        public StickerType Type { get; private set; }

        public NeedReloadArchivedStickersEventArgs(StickerType type)
        {
            Type = type;
        }
    }

    public class StickersDidLoadedEventArgs : EventArgs
    {
        public StickerType Type { get; private set; }

        public StickersDidLoadedEventArgs(StickerType type)
        {
            Type = type;
        }
    }

    public class FeaturedStickersDidLoadedEventArgs : EventArgs
    {
        public FeaturedStickersDidLoadedEventArgs()
        {
        }
    }

    public class RecentsDidLoadedEventArgs : EventArgs
    {
        public bool IsGifs { get; private set; }

        public StickerType Type { get; private set; }

        public RecentsDidLoadedEventArgs(bool gif, StickerType type)
        {
            IsGifs = gif;
            Type = type;
        }
    }

    public class ArchivedStickersCountDidLoadedEventArgs : EventArgs
    {
        public StickerType Type { get; private set; }

        public ArchivedStickersCountDidLoadedEventArgs(StickerType type)
        {
            Type = type;
        }
    }

    public class GroupStickersDidLoadedEventArgs : EventArgs
    {
        public long Id { get; private set; }

        public GroupStickersDidLoadedEventArgs(long id)
        {
            Id = id;
        }
    }
}