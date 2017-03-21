using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
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

        public void cleanup()
        {
            for (int a = 0; a < 2; a++)
            {
                loadHash[a] = 0;
                loadDate[a] = 0;
                stickerSets[a].Clear();
                recentStickers[a].Clear();
                loadingStickers[a] = false;
                stickersLoaded[a] = false;
                loadingRecentStickers[a] = false;
                recentStickersLoaded[a] = false;
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

        public void checkStickers(int type)
        {
            if (!loadingStickers[type] && (!stickersLoaded[type] || Math.Abs(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000 - loadDate[type]) >= 60 * 60))
            {
                loadStickers(type, true, false);
            }
        }

        public void checkFeaturedStickers()
        {
            if (!loadingFeaturedStickers && (!featuredStickersLoaded || Math.Abs(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000 - loadFeaturedDate) >= 60 * 60))
            {
                loadFeaturesStickers(true, false);
            }
        }

        public List<TLDocument> getRecentStickers(int type)
        {
            return new List<TLDocument>(recentStickers[type]);
        }

        public List<TLDocument> getRecentStickersNoCopy(int type)
        {
            return recentStickers[type];
        }

        public void addRecentSticker(int type, TLDocument document, int date)
        {
            bool found = false;
            for (int a = 0; a < recentStickers[type].Count; a++)
            {
                TLDocument image = recentStickers[type][a];
                if (image.Id == document.Id)
                {
                    recentStickers[type].RemoveAt(a);
                    recentStickers[type].Insert(0, image);
                    found = true;
                }
            }
            if (!found)
            {
                recentStickers[type].Insert(0, document);
            }
            if (recentStickers[type].Count > 30)
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
            processLoadedRecentDocuments(type, arrayList, false, date);
        }

        public List<TLDocument> getRecentGifs()
        {
            return new List<TLDocument>(recentGifs);
        }

        public void removeRecentGif(TLDocument document)
        {
            recentGifs.Remove(document);
            MTProtoService.Current.SaveGifCallback(new TLInputDocument { Id = document.Id, AccessHash = document.AccessHash }, true, null);

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

        public void addRecentGif(TLDocument document, int date)
        {
            bool found = false;
            for (int a = 0; a < recentGifs.Count; a++)
            {
                TLDocument image = recentGifs[a];
                if (image.Id == document.Id)
                {
                    recentGifs.RemoveAt(a);
                    recentGifs.Insert(0, image);
                    found = true;
                }
            }
            if (!found)
            {
                recentGifs.Insert(0, document);
            }
            if (recentGifs.Count > 200)
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
            processLoadedRecentDocuments(0, arrayList, true, date);
        }

        public bool isLoadingStickers(int type)
        {
            return loadingStickers[type];
        }

        public TLMessagesStickerSet getStickerSetByName(String name)
        {
            return stickerSetsByName[name];
        }

        public TLMessagesStickerSet getStickerSetById(long id)
        {
            return stickerSetsById[id];
        }

        public Dictionary<string, List<TLDocument>> getAllStickers()
        {
            return allStickers;
        }

        public List<TLMessagesStickerSet> getStickerSets(int type)
        {
            return stickerSets[type];
        }

        public List<TLStickerSetCoveredBase> getFeaturedStickerSets()
        {
            return featuredStickerSets;
        }

        public List<long> getUnreadStickerSets()
        {
            return unreadStickerSets;
        }

        public bool isStickerPackInstalled(long id)
        {
            return stickerSetsById.ContainsKey(id);
        }

        public bool isStickerPackUnread(long id)
        {
            return unreadStickerSets.Contains(id);
        }

        public bool isStickerPackInstalled(string name)
        {
            return stickerSetsByName.ContainsKey(name);
        }

        public string getEmojiForSticker(long id)
        {
            string value = stickersByEmoji[id];
            return value != null ? value : string.Empty;
        }

        private int calcDocumentsHash(List<TLDocument> arrayList)
        {
            if (arrayList == null)
            {
                return 0;
            }

            long acc = 0;
            for (int a = 0; a < Math.Min(200, arrayList.Count); a++)
            {
                TLDocument document = arrayList[a];
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

        public void loadRecents(int type, bool gif, bool cache)
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
                    loadRecents(type, gif, false);
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
                if (Math.Abs(DateTime.Now.TimeOfDay.TotalMilliseconds - lastLoadTime) < 60 * 60 * 1000)
                {
                    return;
                }
                if (gif)
                {
                    var hash = calcDocumentsHash(recentGifs);
                    MTProtoService.Current.GetSavedGifsCallback(hash, result =>
                    {
                        List<TLDocument> arrayList = null;
                        if (result is TLMessagesSavedGifs)
                        {
                            TLMessagesSavedGifs res = (TLMessagesSavedGifs)result;
                            arrayList = res.Gifs.OfType<TLDocument>().ToList();
                        }

                        processLoadedRecentDocuments(type, arrayList, gif, 0);
                    });

                }
                else
                {
                    var hash = calcDocumentsHash(recentStickers[type]);
                    var attached = type == TYPE_MASK;
                    MTProtoService.Current.GetRecentStickersCallback(attached, hash, result =>
                    {
                        List<TLDocument> arrayList = null;
                        if (result is TLMessagesRecentStickers)
                        {
                            TLMessagesRecentStickers res = (TLMessagesRecentStickers)result;
                            arrayList = res.Stickers.OfType<TLDocument>().ToList();
                        }

                        processLoadedRecentDocuments(type, arrayList, gif, 0);
                    });
                }
            }
        }

        private void processLoadedRecentDocuments(int type, List<TLDocument> documents, bool gif, int date)
        {
            if (documents != null)
            {
                try
                {
                    var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

                    //SQLiteDatabase database = MessagesStorage.getInstance().getDatabase();
                    //int maxCount = gif ? MessagesController.getInstance().maxRecentGifsCount : MessagesController.getInstance().maxRecentStickersCount;
                    int maxCount = gif ? 200 : 20;
                    //database.beginTransaction();
                    //SQLitePreparedStatement state = database.executeFast("REPLACE INTO web_recent_v3 VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)");


                    Database database;
                    Statement statement;
                    DatabaseContext.Current.OpenDatabase(out database);

                    DatabaseContext.Current.Execute(database, "CREATE TABLE IF NOT EXISTS `web_recent_v3`(`Id` bigint primary key not null, `AccessHash` bigint, `Date` int, `MimeType` text, `Size` int, `Thumb` string, `DCId` int, `Version` int, `Attributes` string, `MetaType` int, `MetaDate` int)");
                    DatabaseContext.Current.Execute(database, "BEGIN IMMEDIATE TRANSACTION");
                    Sqlite3.sqlite3_prepare_v2(database, "INSERT OR REPLACE INTO `web_recent_v3` (Id,AccessHash,Date,MimeType,Size,Thumb,DCId,Version,Attributes,MetaType,MetaDate) VALUES(?,?,?,?,?,?,?,?,?,?,?)", out statement);

                    int count = documents.Count;
                    for (int a = 0; a < count; a++)
                    {
                        if (a == maxCount)
                        {
                            break;
                        }

                        TLDocument document = documents[a];

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
                        Sqlite3.sqlite3_bind_int(statement, 11, date != 0 ? date : count - a);
                        Sqlite3.sqlite3_step(statement);
                    }

                    Sqlite3.sqlite3_finalize(statement);
                    DatabaseContext.Current.Execute(database, "COMMIT TRANSACTION");

                    if (documents.Count >= maxCount)
                    {
                        DatabaseContext.Current.Execute(database, "BEGIN IMMEDIATE TRANSACTION");
                        for (int a = maxCount; a < documents.Count; a++)
                        {
                            DatabaseContext.Current.Execute(database, "DELETE FROM web_recent_v3 WHERE Id = " + documents[a].Id);
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
                    ApplicationSettings.Current.AddOrUpdateValue("lastGifLoadTime", (long)DateTime.Now.TimeOfDay.TotalMilliseconds);
                }
                else
                {
                    loadingRecentStickers[type] = false;
                    recentStickersLoaded[type] = true;
                    ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTime", (long)DateTime.Now.TimeOfDay.TotalMilliseconds);
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
                }
            }
        }

        public void reorderStickers(int type, List<long> order)
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

            loadHash[type] = calcStickersHash(stickerSets[type]);
            //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
            loadStickers(type, false, true);
        }

        public void calcNewHash(int type)
        {
            loadHash[type] = calcStickersHash(stickerSets[type]);
        }

        public void addNewStickerSet(TLMessagesStickerSet set)
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
            for (int a = 0; a < set.Documents.Count; a++)
            {
                TLDocument document = set.Documents[a] as TLDocument;
                stickersById[document.Id] = document;
            }
            for (int a = 0; a < set.Packs.Count; a++)
            {
                TLStickerPack stickerPack = set.Packs[a];
                stickerPack.Emoticon = stickerPack.Emoticon.Replace("\uFE0F", "");
                List<TLDocument> arrayList = allStickers[stickerPack.Emoticon];
                if (arrayList == null)
                {
                    arrayList = new List<TLDocument>();
                    allStickers[stickerPack.Emoticon] = arrayList;
                }
                for (int c = 0; c < stickerPack.Documents.Count; c++)
                {
                    long id = stickerPack.Documents[c];
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
            loadHash[type] = calcStickersHash(stickerSets[type]);
            //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
            loadStickers(type, false, true);
        }

        #region Featured stickers

        public void loadFeaturesStickers(bool cache, bool force)
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
                        hash = calcFeaturedStickersHash(newStickerArray);
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

                processLoadedFeaturedStickers(newStickerArray, unread, true, date, hash);
            }
            else
            {
                TLMessagesGetFeaturedStickers req = new TLMessagesGetFeaturedStickers();
                req.Hash = force ? 0 : loadFeaturedHash;
                MTProtoService.Current.SendRequestCallback<TLMessagesFeaturedStickersBase>(req, result =>
                {
                    if (result is TLMessagesFeaturedStickers res)
                    {
                        processLoadedFeaturedStickers(res.Sets, res.Unread, false, (int)(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000), res.Hash);
                    }
                    else
                    {
                        processLoadedFeaturedStickers(null, null, false, (int)(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000), req.Hash);
                    }
                });
            }
        }

        private void processLoadedFeaturedStickers(IList<TLStickerSetCoveredBase> res, IList<long> unreadStickers, bool cache, int date, int hash)
        {
            loadingFeaturedStickers = false;
            featuredStickersLoaded = true;
            //Utilities.stageQueue.postRunnable(new Runnable()
            //{
            //    @Override
            //    public void run()
            //    {
            if (cache && (res == null || Math.Abs(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000 - date) >= 60 * 60) || !cache && res == null && hash == 0)
            {
                //AndroidUtilities.runOnUIThread(new Runnable() {
                //    @Override
                //    public void run()
                //{
                if (res != null && hash != 0)
                {
                    loadFeaturedHash = hash;
                }
                loadFeaturesStickers(false, false);
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

                    for (int a = 0; a < res.Count; a++)
                    {
                        TLStickerSetCoveredBase stickerSet = res[a];
                        stickerSetsNew.Add(stickerSet);
                        stickerSetsByIdNew[stickerSet.Set.Id] = stickerSet;
                    }

                    if (!cache)
                    {
                        putFeaturedStickersToCache(stickerSetsNew, unreadStickers, date, hash);
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
                putFeaturedStickersToCache(null, null, date, 0);
            }
            //    }
            //});
        }

        private void putFeaturedStickersToCache(IList<TLStickerSetCoveredBase> stickers, IList<long> unreadStickers, int date, int hash)
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

        private int calcFeaturedStickersHash(IList<TLStickerSetCoveredBase> sets)
        {
            long acc = 0;
            for (int a = 0; a < sets.Count; a++)
            {
                TLStickerSet set = sets[a].Set;
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

        public void markFaturedStickersAsRead(bool query)
        {
            // TODO
        }

        public void markFaturedStickersByIdAsRead(long id)
        {
            // TODO
        }

        #endregion

        public void loadStickers(int type, bool cache, bool force)
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
                        hash = calcStickersHash(newStickerArray);
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
                processLoadedStickers(type, newStickerArray, true, date, hash);
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

                MTProtoService.Current.SendRequestCallback<TLMessagesAllStickersBase>(req, async result =>
                {
                    if (result is TLMessagesAllStickers)
                    {
                        TLMessagesAllStickers res = (TLMessagesAllStickers)result;
                        List<TLMessagesStickerSet> newStickerArray = new List<TLMessagesStickerSet>();
                        if (res.Sets.Count == 0)
                        {
                            processLoadedStickers(type, newStickerArray, false, (int)(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000), res.Hash);
                        }
                        else
                        {
                            Dictionary<long, TLMessagesStickerSet> newStickerSets = new Dictionary<long, TLMessagesStickerSet>();
                            for (int a = 0; a < res.Sets.Count; a++)
                            {
                                TLStickerSet stickerSet = res.Sets[a];

                                if (stickerSetsById.TryGetValue(stickerSet.Id, out TLMessagesStickerSet oldSet) && oldSet.Set.Hash == stickerSet.Hash)
                                {
                                    oldSet.Set.IsArchived = stickerSet.IsArchived;
                                    oldSet.Set.IsInstalled = stickerSet.IsInstalled;
                                    oldSet.Set.IsOfficial = stickerSet.IsOfficial;
                                    newStickerSets[oldSet.Set.Id] = oldSet;
                                    newStickerArray.Add(oldSet);

                                    if (newStickerSets.Count == res.Sets.Count)
                                    {
                                        processLoadedStickers(type, newStickerArray, false, (int)(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000), res.Hash);
                                    }
                                    continue;
                                }

                                newStickerArray.Add(null);
                                int index = a;

                                var response = await MTProtoService.Current.GetStickerSetAsync(new TLInputStickerSetID { Id = stickerSet.Id, AccessHash = stickerSet.AccessHash });
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
                                        processLoadedStickers(type, newStickerArray, false, (int)(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000), res.Hash);
                                    }

                                }

                                //MTProtoService.Current.GetStickerSetCallback(new TLInputStickerSetID { Id = stickerSet.Id, AccessHash = stickerSet.AccessHash }, callback =>
                                //{
                                //});

                            }
                        }
                    }
                    else
                    {
                        processLoadedStickers(type, null, false, (int)(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000), hash);
                    }
                });
            }
        }

        private void putStickersToCache(int type, List<TLMessagesStickerSet> stickers, int date, int hash)
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

        public string getStickerSetName(long setId)
        {
            TLMessagesStickerSet stickerSet = stickerSetsById[setId];
            return stickerSet != null ? stickerSet.Set.ShortName : null;
        }

        public long getStickerSetId(TLDocument document)
        {
            for (int a = 0; a < document.Attributes.Count; a++)
            {
                TLDocumentAttributeBase attribute = document.Attributes[a];
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

        private int calcStickersHash(List<TLMessagesStickerSet> sets)
        {
            long acc = 0;
            for (int a = 0; a < sets.Count; a++)
            {
                TLStickerSet set = sets[a].Set;
                if (set.IsArchived)
                {
                    continue;
                }

                acc = ((acc * 20261) + 0x80000000L + set.Hash) % 0x80000000L;
            }

            return (int)acc;
        }

        private void processLoadedStickers(int type, List<TLMessagesStickerSet> res, bool cache, int date, int hash)
        {
            loadingStickers[type] = false;
            stickersLoaded[type] = true;

            //    Utilities.stageQueue.postRunnable(new Runnable()
            //{
            //    @Override
            //    public void run()
            //    {
            if (cache && (res == null || Math.Abs(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000 - date) >= 60 * 60) || !cache && res == null && hash == 0)
            {
                //AndroidUtilities.runOnUIThread(new Runnable() {
                //    @Override
                //    public void run()
                //{
                if (res != null && hash != 0)
                {
                    loadHash[type] = hash;
                }
                loadStickers(type, false, false);
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

                    for (int a = 0; a < res.Count; a++)
                    {
                        TLMessagesStickerSet stickerSet = res[a];
                        if (stickerSet == null)
                        {
                            continue;
                        }
                        stickerSetsNew.Add(stickerSet);
                        stickerSetsByIdNew[stickerSet.Set.Id] = stickerSet;
                        stickerSetsByNameNew[stickerSet.Set.ShortName] = stickerSet;

                        for (int b = 0; b < stickerSet.Documents.Count; b++)
                        {
                            TLDocumentBase document = stickerSet.Documents[b];
                            if (document == null || document is TLDocumentEmpty)
                            {
                                continue;
                            }
                            stickersByIdNew[document.Id] = document as TLDocument;
                        }
                        if (!stickerSet.Set.IsArchived)
                        {
                            for (int b = 0; b < stickerSet.Packs.Count; b++)
                            {
                                TLStickerPack stickerPack = stickerSet.Packs[b];
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
                                for (int c = 0; c < stickerPack.Documents.Count; c++)
                                {
                                    long id = stickerPack.Documents[c];
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
                        putStickersToCache(type, stickerSetsNew, date, hash);
                    }
                    //                        AndroidUtilities.runOnUIThread(new Runnable()
                    //{
                    //    @Override
                    //                            public void run()
                    //    {
                    for (int a = 0; a < stickerSets[type].Count; a++)
                    {
                        TLStickerSet set = stickerSets[type][a].Set;
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
                putStickersToCache(type, null, date, 0);
            }
            //    }
            //});        
        }

        public void removeStickersSet(TLStickerSet stickerSet, int hide, bool showSettings)
        {
            int type = stickerSet.IsMasks ? TYPE_MASK : TYPE_IMAGE;
            TLInputStickerSetID stickerSetID = new TLInputStickerSetID();
            stickerSetID.AccessHash = stickerSet.AccessHash;
            stickerSetID.Id = stickerSet.Id;
            if (hide != 0)
            {
                stickerSet.IsArchived = hide == 1;
                for (int a = 0; a < stickerSets[type].Count; a++)
                {
                    TLMessagesStickerSet set = stickerSets[type][a];
                    if (set.Set.Id == stickerSet.Id)
                    {
                        stickerSets[type].RemoveAt(a);
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
                loadHash[type] = calcStickersHash(stickerSets[type]);
                putStickersToCache(type, stickerSets[type], loadDate[type], loadHash[type]);
                //NotificationCenter.getInstance().postNotificationName(NotificationCenter.stickersDidLoaded, type);
                TLMessagesInstallStickerSet req = new TLMessagesInstallStickerSet();
                req.StickerSet = stickerSetID;
                req.Archived = hide == 1;
                MTProtoService.Current.SendRequestCallback<TLMessagesStickerSetInstallResultBase>(req, result =>
                {
                    if (result is TLMessagesStickerSetInstallResultArchive)
                    {
                        //NotificationCenter.getInstance().postNotificationName(NotificationCenter.needReloadArchivedStickers, type);
                        //if (hide != 1 && baseFragment != null && baseFragment.getParentActivity() != null)
                        //{
                        //    StickersArchiveAlert alert = new StickersArchiveAlert(baseFragment.getParentActivity(), showSettings ? baseFragment : null, ((TLRPC.TL_messages_stickerSetInstallResultArchive)response).sets);
                        //    baseFragment.showDialog(alert.create());
                        //}
                    }
                    loadStickers(type, false, false);
                });


            }
            else
            {
                TLMessagesUninstallStickerSet req = new TLMessagesUninstallStickerSet();
                req.StickerSet = stickerSetID;
                MTProtoService.Current.SendRequestCallback<bool>(req, result =>
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
                    loadStickers(type, false, true);

                }, fault =>
                {
                    loadStickers(type, false, true);
                });
            }
        }

        public event NeedReloadArchivedStickersEventHandler NeedReloadArchivedStickers;

        public event StickersDidLoadedEventHandler StickersDidLoaded;

        public event RecentDocumentsDidLoadedEventHandler RecentDocumentsDidLoaded;
    }

    public enum StickerSetType : int
    {
        Image = 0,
        Mask = 1
    }

    public delegate void NeedReloadArchivedStickersEventHandler(object sender, NeedReloadArchivedStickersEventHandler e);
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

    public delegate void RecentDocumentsDidLoadedEventHandler(object sender, RecentDocumentsDidLoadedEventArgs e);
    public class RecentDocumentsDidLoadedEventArgs : EventArgs
    {
        public bool IsGifs { get; private set; }

        public int Type { get; private set; }

        public RecentDocumentsDidLoadedEventArgs(bool gif, int type)
        {
            Type = type;
        }
    }
}