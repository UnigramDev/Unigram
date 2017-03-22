using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Messages;
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

        void CheckStickers(int type);

        void CheckArchivedStickersCount(int type);

        void CheckFeaturedStickers();

        List<TLDocument> GetRecentStickers(int type);

        List<TLDocument> GetRecentStickersNoCopy(int type);

        void AddRecentSticker(int type, TLDocument document, int date);

        List<TLDocument> GetRecentGifs();

        void AddRecentGif(TLDocument document, int date);

        void RemoveRecentGif(TLDocument document);

        bool IsLoadingStickers(int type);

        TLMessagesStickerSet GetStickerSetByName(string name);

        TLMessagesStickerSet GetStickerSetById(long id);

        Dictionary<string, List<TLDocument>> GetAllStickers();

        int GetArchivedStickersCount(int type);

        List<TLMessagesStickerSet> GetStickerSets(int type);

        List<TLStickerSetCoveredBase> GetFeaturedStickerSets();

        List<long> GetUnreadStickerSets();

        bool IsStickerPackInstalled(long id);

        bool IsStickerPackUnread(long id);

        bool IsStickerPackInstalled(string name);

        string GetEmojiForSticker(long id);

        void LoadRecents(int type, bool gif, bool cache);

        void ReorderStickers(int type, List<long> order);

        void CalculateNewHash(int type);

        void AddNewStickerSet(TLMessagesStickerSet set);

        void LoadArchivedStickersCount(int type, bool cache);

        void LoadFeaturedStickers(bool cache, bool force);

        void MarkFeaturedStickersAsRead(bool query);

        void MarkFeaturedStickersByIdAsRead(long id);

        void LoadStickers(int type, bool cache, bool force);

        string GetStickerSetName(long setId);

        long GetStickerSetId(TLDocument document);

        void RemoveStickersSet(TLStickerSet stickerSet, int hide, bool showSettings);

        event NeedReloadArchivedStickersEventHandler NeedReloadArchivedStickers;
        event StickersDidLoadedEventHandler StickersDidLoaded;
        event FeaturedStickersDidLoadedEventHandler FeaturedStickersDidLoaded;
        event RecentsDidLoadedEventHandler RecentsDidLoaded;
        event ArchivedStickersCountDidLoadedEventHandler ArchivedStickersCountDidLoaded;
    }

    public class StickersService : IStickersService
    {
        public static int TYPE_IMAGE = 0;
        public static int TYPE_MASK = 1;

        private List<TLMessagesStickerSet>[] stickerSets = new[] { new List<TLMessagesStickerSet>(), new List<TLMessagesStickerSet>() };
        private Dictionary<long, TLMessagesStickerSet> stickerSetsById = new Dictionary<long, TLMessagesStickerSet>();
        private Dictionary<string, TLMessagesStickerSet> stickerSetsByName = new Dictionary<string, TLMessagesStickerSet>();
        private bool[] loadingStickers = new bool[2];
        private bool[] stickersLoaded = new bool[2];
        private int[] archivedStickersCount = new int[2];
        private int[] loadHash = new int[2];
        private int[] loadDate = new int[2];

        private Dictionary<long, string> stickersByEmoji = new Dictionary<long, string>();
        private Dictionary<string, List<TLDocument>> allStickers = new Dictionary<string, List<TLDocument>>();

        private List<TLDocument>[] recentStickers = new[] { new List<TLDocument>(), new List<TLDocument>() };
        private bool[] loadingRecentStickers = new bool[2];
        private bool[] recentStickersLoaded = new bool[2];

        private List<TLDocument> recentGifs = new List<TLDocument>();
        private bool loadingRecentGifs;
        private bool recentGifsLoaded;

        private int loadFeaturedHash;
        private int loadFeaturedDate;
        private List<TLStickerSetCoveredBase> featuredStickerSets = new List<TLStickerSetCoveredBase>();
        private Dictionary<long, TLStickerSetCoveredBase> featuredStickerSetsById = new Dictionary<long, TLStickerSetCoveredBase>();
        private List<long> unreadStickerSets = new List<long>();
        private List<long> readingStickerSets = new List<long>();
        private bool loadingFeaturedStickers;
        private bool featuredStickersLoaded;

        private readonly IMTProtoService _protoService;
        private readonly ICacheService _cacheService;

        public StickersService(IMTProtoService protoService, ICacheService cacheService)
        {
            _protoService = protoService;
            _cacheService = cacheService;
        }

        public void Cleanup()
        {
            for (int i = 0; i < 2; i++)
            {
                loadHash[i] = 0;
                loadDate[i] = 0;
                stickerSets[i].Clear();
                recentStickers[i].Clear();
                loadingStickers[i] = false;
                stickersLoaded[i] = false;
                loadingRecentStickers[i] = false;
                recentStickersLoaded[i] = false;
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

        public void CheckStickers(int type)
        {
            if (!loadingStickers[type] && (!stickersLoaded[type] || Math.Abs(Utils.CurrentTimestamp / 1000 - loadDate[type]) >= 60 * 60))
            {
                LoadStickers(type, true, false);
            }
        }

        public void CheckArchivedStickersCount(int type)
        {
            //if (!loadingStickers[type] && (!stickersLoaded[type] || Math.Abs(Utils.CurrentTimestamp / 1000 - loadDate[type]) >= 60 * 60))
            {
                LoadArchivedStickersCount(type, true);
            }
        }

        public void CheckFeaturedStickers()
        {
            if (!loadingFeaturedStickers && (!featuredStickersLoaded || Math.Abs(Utils.CurrentTimestamp / 1000 - loadFeaturedDate) >= 60 * 60))
            {
                LoadFeaturedStickers(true, false);
            }
        }

        public List<TLDocument> GetRecentStickers(int type)
        {
            return new List<TLDocument>(recentStickers[type]);
        }

        public List<TLDocument> GetRecentStickersNoCopy(int type)
        {
            return recentStickers[type];
        }

        public void AddRecentSticker(int type, TLDocument document, int date)
        {
            bool found = false;
            for (int i = 0; i < recentStickers[type].Count; i++)
            {
                TLDocument image = recentStickers[type][i];
                if (image.Id == document.Id)
                {
                    recentStickers[type].RemoveAt(i);
                    recentStickers[type].Insert(0, image);
                    found = true;
                }
            }
            if (!found)
            {
                recentStickers[type].Insert(0, document);
            }
            if (recentStickers[type].Count > _cacheService.Config.StickersRecentLimit)
            {
                TLDocument old = recentStickers[type][recentStickers[type].Count - 1];
                recentStickers[type].RemoveAt(recentStickers[type].Count - 1);
                try
                {
                    Database database;
                    DatabaseContext.Current.OpenDatabase(out database);
                    DatabaseContext.Current.Execute(database, "DELETE FROM web_recent_v3 WHERE Id = " + old.Id);
                    Sqlite3.sqlite3_close(database);
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
            }
            List<TLDocument> arrayList = new List<TLDocument>();
            arrayList.Add(document);
            ProcessLoadedRecentDocuments(type, arrayList, false, date);
        }

        public List<TLDocument> GetRecentGifs()
        {
            return new List<TLDocument>(recentGifs);
        }

        public void RemoveRecentGif(TLDocument document)
        {
            recentGifs.Remove(document);
            _protoService.SaveGifCallback(new TLInputDocument { Id = document.Id, AccessHash = document.AccessHash }, true, null);

            try
            {
                Database database;
                DatabaseContext.Current.OpenDatabase(out database);
                DatabaseContext.Current.Execute(database, "DELETE FROM web_recent_v3 WHERE Id = " + document.Id);
                Sqlite3.sqlite3_close(database);
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
                    DatabaseContext.Current.OpenDatabase(out database);
                    DatabaseContext.Current.Execute(database, "DELETE FROM web_recent_v3 WHERE Id = " + old.Id);
                    Sqlite3.sqlite3_close(database);
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

        public bool IsLoadingStickers(int type)
        {
            return loadingStickers[type];
        }

        public TLMessagesStickerSet GetStickerSetByName(string name)
        {
            return stickerSetsByName[name];
        }

        public TLMessagesStickerSet GetStickerSetById(long id)
        {
            return stickerSetsById[id];
        }

        public Dictionary<string, List<TLDocument>> GetAllStickers()
        {
            return allStickers;
        }

        public int GetArchivedStickersCount(int type)
        {
            return archivedStickersCount[type];
        }

        public List<TLMessagesStickerSet> GetStickerSets(int type)
        {
            return stickerSets[type];
        }

        public List<TLStickerSetCoveredBase> GetFeaturedStickerSets()
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

        public string GetEmojiForSticker(long id)
        {
            string value = stickersByEmoji[id];
            return value != null ? value : string.Empty;
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

        public void LoadRecents(int type, bool gif, bool cache)
        {
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
                    DatabaseContext.Current.OpenDatabase(out database);

                    List<TLDocument> arrayList = new List<TLDocument>();

                    Sqlite3.sqlite3_prepare_v2(database, "SELECT Id,AccessHash,Date,MimeType,Size,Thumb,DCId,Version,Attributes,MetaType,MetaDate FROM web_recent_v3 WHERE MetaType = " + (gif ? 2 : (type == TYPE_IMAGE ? 3 : 4)) + " ORDER BY MetaDate DESC", out statement);

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
                    Sqlite3.sqlite3_close(database);

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
                    RecentsDidLoaded?.Invoke(this, new RecentsDidLoadedEventArgs(gif, type));
                    LoadRecents(type, gif, false);
                }
                catch (Exception e)
                {
                    //FileLog.e("tmessages", e);
                }
            }
            else
            {
                long lastLoadTime;
                if (gif)
                {
                    lastLoadTime = ApplicationSettings.Current.GetValueOrDefault<long>("lastGifLoadTime", 0);
                }
                else
                {
                    lastLoadTime = ApplicationSettings.Current.GetValueOrDefault<long>("lastStickersLoadTime", 0);
                }
                if (Math.Abs(Utils.CurrentTimestamp - lastLoadTime) < 60 * 60 * 1000)
                {
                    return;
                }
                if (gif)
                {
                    var hash = CalculateDocumentsHash(recentGifs);
                    _protoService.GetSavedGifsCallback(hash, result =>
                    {
                        List<TLDocument> arrayList = null;
                        if (result is TLMessagesSavedGifs)
                        {
                            TLMessagesSavedGifs res = (TLMessagesSavedGifs)result;
                            arrayList = res.Gifs.OfType<TLDocument>().ToList();
                        }

                        ProcessLoadedRecentDocuments(type, arrayList, gif, 0);
                    });

                }
                else
                {
                    var hash = CalculateDocumentsHash(recentStickers[type]);
                    var attached = type == TYPE_MASK;
                    _protoService.GetRecentStickersCallback(attached, hash, result =>
                    {
                        List<TLDocument> arrayList = null;
                        if (result is TLMessagesRecentStickers)
                        {
                            TLMessagesRecentStickers res = (TLMessagesRecentStickers)result;
                            arrayList = res.Stickers.OfType<TLDocument>().ToList();
                        }

                        ProcessLoadedRecentDocuments(type, arrayList, gif, 0);
                    });
                }
            }
        }

        private void ProcessLoadedRecentDocuments(int type, List<TLDocument> documents, bool gif, int date)
        {
            if (documents != null)
            {
                try
                {
                    var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

                    //SQLiteDatabase database = MessagesStorage.getInstance().getDatabase();
                    //int maxCount = gif ? MessagesController.getInstance().maxRecentGifsCount : MessagesController.getInstance().maxRecentStickersCount;
                    int maxCount = gif ? _cacheService.Config.SavedGifsLimit : _cacheService.Config.StickersRecentLimit;
                    //database.beginTransaction();
                    //SQLitePreparedStatement state = database.executeFast("REPLACE INTO web_recent_v3 VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)");


                    Database database;
                    Statement statement;
                    DatabaseContext.Current.OpenDatabase(out database);

                    DatabaseContext.Current.Execute(database, "CREATE TABLE IF NOT EXISTS `web_recent_v3`(`Id` bigint primary key not null, `AccessHash` bigint, `Date` int, `MimeType` text, `Size` int, `Thumb` string, `DCId` int, `Version` int, `Attributes` string, `MetaType` int, `MetaDate` int)");
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
                        Sqlite3.sqlite3_bind_int(statement, 10, gif ? 2 : (type == TYPE_IMAGE ? 3 : 4));
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
                            DatabaseContext.Current.Execute(database, "DELETE FROM web_recent_v3 WHERE Id = " + documents[i].Id);
                        }
                        DatabaseContext.Current.Execute(database, "COMMIT TRANSACTION");
                    }

                    Sqlite3.sqlite3_close(database);
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
                    ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTime", Utils.CurrentTimestamp);
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
                    RecentsDidLoaded?.Invoke(this, new RecentsDidLoadedEventArgs(gif, type));
                }
            }
        }

        public void ReorderStickers(int type, List<long> order)
        {
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
            StickersDidLoaded?.Invoke(this, new StickersDidLoadedEventArgs(type));
            LoadStickers(type, false, true);
        }

        public void CalculateNewHash(int type)
        {
            loadHash[type] = CalculateStickersHash(stickerSets[type]);
        }

        public void AddNewStickerSet(TLMessagesStickerSet set)
        {
            if (stickerSetsById.ContainsKey(set.Set.Id) || stickerSetsByName.ContainsKey(set.Set.ShortName))
            {
                return;
            }
            int type = set.Set.IsMasks ? TYPE_MASK : TYPE_IMAGE;
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
                List<TLDocument> arrayList = allStickers[stickerPack.Emoticon];
                if (arrayList == null)
                {
                    arrayList = new List<TLDocument>();
                    allStickers[stickerPack.Emoticon] = arrayList;
                }
                for (int k = 0; k < stickerPack.Documents.Count; k++)
                {
                    long id = stickerPack.Documents[k];
                    if (!stickersByEmoji.ContainsKey(id))
                    {
                        stickersByEmoji[id] = stickerPack.Emoticon;
                    }
                    TLDocument sticker = stickersById[id];
                    if (sticker != null)
                    {
                        arrayList.Add(sticker);
                    }
                }
            }
            loadHash[type] = CalculateStickersHash(stickerSets[type]);
            //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
            StickersDidLoaded?.Invoke(this, new StickersDidLoadedEventArgs(type));
            LoadStickers(type, false, true);
        }

        public void LoadArchivedStickersCount(int type, bool cache)
        {
            if (cache)
            {
                int count = ApplicationSettings.Current.GetValueOrDefault("archivedStickersCount" + type, -1);
                if (count == -1)
                {
                    LoadArchivedStickersCount(type, false);
                    return;
                }

                archivedStickersCount[type] = count;
                //NotificationCenter.getInstance().postNotificationName(NotificationCenter.archivedStickersCountDidLoaded, new Object[] { Integer.valueOf(type) });
                ArchivedStickersCountDidLoaded?.Invoke(this, new ArchivedStickersCountDidLoadedEventArgs(type));
            }
            else
            {
                var req = new TLMessagesGetArchivedStickers();
                req.Limit = 0;
                req.IsMasks = type == TYPE_MASK;
                _protoService.SendRequestCallback<TLMessagesArchivedStickers>(req, result =>
                {
                    archivedStickersCount[type] = result.Count;
                    ApplicationSettings.Current.AddOrUpdateValue("archivedStickersCount" + type, result.Count);
                    //NotificationCenter.getInstance().postNotificationName(NotificationCenter.archivedStickersCountDidLoaded, new Object[] { Integer.valueOf(StickersQuery.19.this.val$type) });
                    ArchivedStickersCountDidLoaded?.Invoke(this, new ArchivedStickersCountDidLoadedEventArgs(type));
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
                List<TLStickerSetCoveredBase> newStickerArray = null;
                List<long> unread = null;
                int date = 0;
                int hash = 0;

                Database database;
                Statement statement;
                DatabaseContext.Current.OpenDatabase(out database);
                try
                {
                    Sqlite3.sqlite3_prepare_v2(database, "SELECT data, unread, date, hash FROM stickers_featured WHERE 1", out statement);

                    if (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
                    {
                        var data = Sqlite3.sqlite3_column_blob(statement, 0);
                        if (data != null)
                        {
                            newStickerArray = TLFactory.From<TLVector<TLStickerSetCoveredBase>>(data).ToList();
                        }

                        var data2 = Sqlite3.sqlite3_column_blob(statement, 1);
                        if (data2 != null)
                        {
                            unread = TLFactory.From<TLVector<long>>(data2).ToList();
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
                    Sqlite3.sqlite3_close(database);
                }

                ProcessLoadedFeaturedStickers(newStickerArray, unread, true, date, hash);
            }
            else
            {
                TLMessagesGetFeaturedStickers req = new TLMessagesGetFeaturedStickers();
                req.Hash = force ? 0 : loadFeaturedHash;
                _protoService.SendRequestCallback<TLMessagesFeaturedStickersBase>(req, result =>
                {
                    if (result is TLMessagesFeaturedStickers res)
                    {
                        ProcessLoadedFeaturedStickers(res.Sets, res.Unread, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                    }
                    else
                    {
                        ProcessLoadedFeaturedStickers(null, null, false, (int)(Utils.CurrentTimestamp / 1000), req.Hash);
                    }
                });
            }
        }

        private void ProcessLoadedFeaturedStickers(IList<TLStickerSetCoveredBase> res, IList<long> unreadStickers, bool cache, int date, int hash)
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
                    List<TLStickerSetCoveredBase> stickerSetsNew = new List<TLStickerSetCoveredBase>();
                    Dictionary<long, TLStickerSetCoveredBase> stickerSetsByIdNew = new Dictionary<long, TLStickerSetCoveredBase>();

                    for (int i = 0; i < res.Count; i++)
                    {
                        TLStickerSetCoveredBase stickerSet = res[i];
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
                    FeaturedStickersDidLoaded?.Invoke(this, new FeaturedStickersDidLoadedEventArgs());
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

        private void PutFeaturedStickersToCache(IList<TLStickerSetCoveredBase> stickers, IList<long> unreadStickers, int date, int hash)
        {
            TLVector<TLStickerSetCoveredBase> stickersFinal = stickers != null ? new TLVector<TLStickerSetCoveredBase>(stickers) : null;

            try
            {
                Database database;
                Statement statement;
                DatabaseContext.Current.OpenDatabase(out database);
                DatabaseContext.Current.Execute(database, "CREATE TABLE IF NOT EXISTS stickers_featured(id INTEGER PRIMARY KEY, data BLOB, unread BLOB, date INTEGER, hash TEXT);");

                if (stickersFinal != null)
                {
                    Sqlite3.sqlite3_prepare_v2(database, "REPLACE INTO stickers_featured VALUES(?, ?, ?, ?, ?)", out statement);

                    using (var data = new MemoryStream())
                    using (var data2 = new MemoryStream())
                    {
                        using (var to = new TLBinaryWriter(data))
                        {
                            stickersFinal.Write(to);
                        }

                        using (var to = new TLBinaryWriter(data2))
                        {
                            new TLVector<long>(unreadStickers).Write(to);
                        }

                        Sqlite3.sqlite3_reset(statement);
                        Sqlite3.sqlite3_bind_int(statement, 1, 1);
                        Sqlite3.sqlite3_bind_blob(statement, 2, data.ToArray(), -1);
                        Sqlite3.sqlite3_bind_blob(statement, 3, data2.ToArray(), -1);
                        Sqlite3.sqlite3_bind_int(statement, 4, date);
                        Sqlite3.sqlite3_bind_int(statement, 5, hash);
                        Sqlite3.sqlite3_step(statement);
                    }

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

                Sqlite3.sqlite3_close(database);
            }
            catch (Exception e)
            {
                //FileLog.e("tmessages", e);
            }
        }

        private int CalculateFeaturedStickersHash(IList<TLStickerSetCoveredBase> sets)
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
            FeaturedStickersDidLoaded?.Invoke(this, new FeaturedStickersDidLoadedEventArgs());
            PutFeaturedStickersToCache(featuredStickerSets, unreadStickerSets, loadFeaturedDate, loadFeaturedHash);
            if (query)
            {

                TLMessagesReadFeaturedStickers req = new TLMessagesReadFeaturedStickers();
                _protoService.SendRequestCallback<bool>(req, null);
            }
        }

        public void MarkFeaturedStickersByIdAsRead(long id)
        {
            if (!unreadStickerSets.Contains(id) || readingStickerSets.Contains(id))
            {
                return;
            }
            readingStickerSets.Add(id);
            TLMessagesReadFeaturedStickers req = new TLMessagesReadFeaturedStickers();
            req.Id = new TLVector<long>();
            req.Id.Add(id);
            _protoService.SendRequestCallback<bool>(req, null);

            //AndroidUtilities.runOnUIThread(new Runnable()
            //{
            //    @Override
            //    public void run()
            //    {
            unreadStickerSets.Remove(id);
            readingStickerSets.Remove(id);
            loadFeaturedHash = CalculateFeaturedStickersHash(featuredStickerSets);
            //NotificationCenter.getInstance().postNotificationName(NotificationCenter.featuredStickersDidLoaded);
            FeaturedStickersDidLoaded?.Invoke(this, new FeaturedStickersDidLoadedEventArgs());
            PutFeaturedStickersToCache(featuredStickerSets, unreadStickerSets, loadFeaturedDate, loadFeaturedHash);
            //    }
            //}, 1000);
        }

        public void LoadStickers(int type, bool cache, bool force)
        {
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
                DatabaseContext.Current.OpenDatabase(out database);
                try
                {
                    Sqlite3.sqlite3_prepare_v2(database, "SELECT data, date, hash FROM stickers_v2 WHERE id = " + (type + 1), out statement);

                    if (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
                    {
                        var data = Sqlite3.sqlite3_column_blob(statement, 0);
                        if (data != null)
                        {
                            newStickerArray = TLFactory.From<TLVector<TLMessagesStickerSet>>(data).ToList();
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
                    Sqlite3.sqlite3_close(database);
                }
                ProcessLoadedStickers(type, newStickerArray, true, date, hash);
            }
            else
            {
                TLObject req;
                int hash;
                if (type == TYPE_IMAGE)
                {
                    req = new TLMessagesGetAllStickers();
                    hash = ((TLMessagesGetAllStickers)req).Hash = force ? 0 : loadHash[type];
                }
                else
                {
                    req = new TLMessagesGetMaskStickers();
                    hash = ((TLMessagesGetMaskStickers)req).Hash = force ? 0 : loadHash[type];
                }

                _protoService.SendRequestCallback<TLMessagesAllStickersBase>(req, async result =>
                {
                    if (result is TLMessagesAllStickers)
                    {
                        TLMessagesAllStickers res = (TLMessagesAllStickers)result;
                        List<TLMessagesStickerSet> newStickerArray = new List<TLMessagesStickerSet>();
                        if (res.Sets.Count == 0)
                        {
                            ProcessLoadedStickers(type, newStickerArray, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                        }
                        else
                        {
                            Dictionary<long, TLMessagesStickerSet> newStickerSets = new Dictionary<long, TLMessagesStickerSet>();
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
                                        ProcessLoadedStickers(type, newStickerArray, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                                    }
                                    continue;
                                }

                                newStickerArray.Add(null);
                                int index = i;

                                var response = await _protoService.GetStickerSetAsync(new TLInputStickerSetID { Id = stickerSet.Id, AccessHash = stickerSet.AccessHash });
                                if (response.IsSucceeded)
                                {
                                    TLMessagesStickerSet res1 = (TLMessagesStickerSet)response.Result;
                                    newStickerArray[index] = res1;
                                    newStickerSets[stickerSet.Id] = res1;
                                    if (newStickerSets.Count == res.Sets.Count)
                                    {
                                        for (int j = 0; j < newStickerArray.Count; j++)
                                        {
                                            if (newStickerArray[j] == null)
                                            {
                                                newStickerArray.RemoveAt(j);
                                            }
                                        }
                                        ProcessLoadedStickers(type, newStickerArray, false, (int)(Utils.CurrentTimestamp / 1000), res.Hash);
                                    }

                                }

                                //_protoService.GetStickerSetCallback(new TLInputStickerSetID { Id = stickerSet.Id, AccessHash = stickerSet.AccessHash }, callback =>
                                //{
                                //});

                            }
                        }
                    }
                    else
                    {
                        ProcessLoadedStickers(type, null, false, (int)(Utils.CurrentTimestamp / 1000), hash);
                    }
                });
            }
        }

        private void PutStickersToCache(int type, List<TLMessagesStickerSet> stickers, int date, int hash)
        {
            TLVector<TLMessagesStickerSet> stickersFinal = stickers != null ? new TLVector<TLMessagesStickerSet>(stickers) : null;

            try
            {
                Database database;
                Statement statement;
                DatabaseContext.Current.OpenDatabase(out database);
                DatabaseContext.Current.Execute(database, "CREATE TABLE IF NOT EXISTS stickers_v2(id INTEGER PRIMARY KEY, data BLOB, date INTEGER, hash TEXT);");

                if (stickersFinal != null)
                {
                    Sqlite3.sqlite3_prepare_v2(database, "REPLACE INTO stickers_v2 VALUES(?, ?, ?, ?)", out statement);

                    using (var data = new MemoryStream())
                    {
                        using (var to = new TLBinaryWriter(data))
                        {
                            stickersFinal.Write(to);
                        }

                        Sqlite3.sqlite3_reset(statement);
                        Sqlite3.sqlite3_bind_int(statement, 1, type == TYPE_IMAGE ? 1 : 2);
                        Sqlite3.sqlite3_bind_blob(statement, 2, data.ToArray(), -1);
                        Sqlite3.sqlite3_bind_int(statement, 3, date);
                        Sqlite3.sqlite3_bind_int(statement, 4, hash);
                        Sqlite3.sqlite3_step(statement);
                    }

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

                Sqlite3.sqlite3_close(database);
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

        private void ProcessLoadedStickers(int type, List<TLMessagesStickerSet> res, bool cache, int date, int hash)
        {
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
                LoadStickers(type, false, false);
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
                    Dictionary<long, string> stickersByEmojiNew = new Dictionary<long, string>();
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
                                List<TLDocument> arrayList = null;
                                allStickersNew.TryGetValue(stickerPack.Emoticon, out arrayList);
                                if (arrayList == null)
                                {
                                    arrayList = new List<TLDocument>();
                                    allStickersNew[stickerPack.Emoticon] = arrayList;
                                }
                                for (int k = 0; k < stickerPack.Documents.Count; k++)
                                {
                                    long id = stickerPack.Documents[k];
                                    if (!stickersByEmojiNew.ContainsKey(id))
                                    {
                                        stickersByEmojiNew[id] = stickerPack.Emoticon;
                                    }
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
                        PutStickersToCache(type, stickerSetsNew, date, hash);
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
                    if (type == TYPE_IMAGE)
                    {
                        allStickers = allStickersNew;
                        stickersByEmoji = stickersByEmojiNew;
                    }
                    //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
                    StickersDidLoaded?.Invoke(this, new StickersDidLoadedEventArgs(type));
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
                PutStickersToCache(type, null, date, 0);
            }
            //    }
            //});        
        }

        public void RemoveStickersSet(TLStickerSet stickerSet, int hide, bool showSettings)
        {
            int type = stickerSet.IsMasks ? TYPE_MASK : TYPE_IMAGE;
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
                PutStickersToCache(type, stickerSets[type], loadDate[type], loadHash[type]);
                //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
                StickersDidLoaded?.Invoke(this, new StickersDidLoadedEventArgs(type));
                TLMessagesInstallStickerSet req = new TLMessagesInstallStickerSet();
                req.StickerSet = stickerSetID;
                req.Archived = hide == 1;
                _protoService.SendRequestCallback<TLMessagesStickerSetInstallResultBase>(req, result =>
                {
                    if (result is TLMessagesStickerSetInstallResultArchive)
                    {
                        //NotificationCenter.getInstance().postNotificationName(NotificationCenter.needReloadArchivedStickers, type);
                        NeedReloadArchivedStickers?.Invoke(this, new NeedReloadArchivedStickersEventArgs(type));
                        //if (hide != 1 && baseFragment != null && baseFragment.getParentActivity() != null)
                        //{
                        //    StickersArchiveAlert alert = new StickersArchiveAlert(baseFragment.getParentActivity(), showSettings ? baseFragment : null, ((TLRPC.TL_messages_stickerSetInstallResultArchive)response).sets);
                        //    baseFragment.showDialog(alert.create());
                        //}
                    }
                    LoadStickers(type, false, false);
                });


            }
            else
            {
                TLMessagesUninstallStickerSet req = new TLMessagesUninstallStickerSet();
                req.StickerSet = stickerSetID;
                _protoService.SendRequestCallback<bool>(req, result =>
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
                    LoadStickers(type, false, true);

                }, fault =>
                {
                    LoadStickers(type, false, true);
                });
            }
        }

        public event NeedReloadArchivedStickersEventHandler NeedReloadArchivedStickers;
        public event StickersDidLoadedEventHandler StickersDidLoaded;
        public event FeaturedStickersDidLoadedEventHandler FeaturedStickersDidLoaded;
        public event RecentsDidLoadedEventHandler RecentsDidLoaded;
        public event ArchivedStickersCountDidLoadedEventHandler ArchivedStickersCountDidLoaded;
    }

    public enum StickerSetType : int
    {
        Image = 0,
        Mask = 1
    }

    public delegate void NeedReloadArchivedStickersEventHandler(object sender, NeedReloadArchivedStickersEventArgs e);
    public class NeedReloadArchivedStickersEventArgs : EventArgs
    {
        public int Type { get; private set; }

        public NeedReloadArchivedStickersEventArgs(int type)
        {
            Type = type;
        }
    }

    public delegate void StickersDidLoadedEventHandler(object sender, StickersDidLoadedEventArgs e);
    public class StickersDidLoadedEventArgs : EventArgs
    {
        public int Type { get; private set; }

        public StickersDidLoadedEventArgs(int type)
        {
            Type = type;
        }
    }

    public delegate void FeaturedStickersDidLoadedEventHandler(object sender, FeaturedStickersDidLoadedEventArgs e);
    public class FeaturedStickersDidLoadedEventArgs : EventArgs
    {
        public FeaturedStickersDidLoadedEventArgs()
        {
        }
    }

    public delegate void RecentsDidLoadedEventHandler(object sender, RecentsDidLoadedEventArgs e);
    public class RecentsDidLoadedEventArgs : EventArgs
    {
        public bool IsGifs { get; private set; }

        public int Type { get; private set; }

        public RecentsDidLoadedEventArgs(bool gif, int type)
        {
            IsGifs = gif;
            Type = type;
        }
    }

    public delegate void ArchivedStickersCountDidLoadedEventHandler(object sender, ArchivedStickersCountDidLoadedEventArgs e);
    public class ArchivedStickersCountDidLoadedEventArgs : EventArgs
    {
        public int Type { get; private set; }

        public ArchivedStickersCountDidLoadedEventArgs(int type)
        {
            Type = type;
        }
    }
}