using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Universal.WinSQLite;
using Windows.Storage;

namespace Unigram.Core
{
    public class DatabaseContext
    {
        private static DatabaseContext _current;
        public static DatabaseContext Current
        {
            get
            {
                if (_current == null)
                    _current = new DatabaseContext();

                return _current;
            }
        }

        private string _path;

        #region Queries

        private const string COUNT_TABLE = "SELECT COUNT(*) FROM `{0}`";

        private const string CREATE_TABLE_STICKERSET = "CREATE TABLE IF NOT EXISTS `StickerSets`(`Id` bigint primary key not null, `AccessHash` bigint, `Title` text, `ShortName` text, `Count` int, `Hash` int, `Flags` int, `Order` int)";
        private const string INSERT_TABLE_STICKERSET = "INSERT OR REPLACE INTO `StickerSets` (`Id`,`AccessHash`,`Title`,`ShortName`,`Count`,`Hash`,`Flags`,`Order`) VALUES(?,?,?,?,?,?,?,?)";
        private const string SELECT_TABLE_STICKERSET = "SELECT `Id`,`AccessHash`,`Title`,`ShortName`,`Count`,`Hash`,`Flags`,`Order` FROM `StickerSets` ORDER BY `Order`";
        private const string UPDATE_TABLE_STICKERSET_ORDER = "UPDATE `StickerSets` SET `Order` = ? WHERE `Id` = ?";

        private const string CREATE_TABLE_STICKERPACK = "CREATE TABLE `StickerPacks` (`Id` integer primary key autoincrement, `StickerId` bigint not null, `SetId` bigint not null, `Emoticon` text not null, `Order` int not null)";
        private const string CREATE_INDEX_STICKERPACK = "CREATE INDEX `EmoticonIndex` ON `StickerPacks` (`Emoticon`)";
        private const string INSERT_TABLE_STICKERPACK = "INSERT OR REPLACE INTO `StickerPacks` (`StickerId`,`SetId`,`Emoticon`,`Order`) VALUES(?,?,?,?)";
        private const string SELECT_TABLE_STICKERPACK = "SELECT Stickers.`Id`,Stickers.`AccessHash`,Stickers.`Date`,Stickers.`MimeType`,Stickers.`Size`,Stickers.`Thumb`,Stickers.`DCId`,Stickers.`Version`,Stickers.`Attributes`,Stickers.`Tag` FROM `Stickers` INNER JOIN `StickerPacks` ON Stickers.`Id` = StickerPacks.`StickerId` WHERE StickerPacks.`Emoticon` = '{0}' ORDER BY StickerPacks.`Order`";
        private const string UPDATE_TABLE_STICKERPACK_ORDER = "UPDATE `StickerPacks` SET `Order` = ? WHERE `SetId` = ?";

        private const string CREATE_TABLE_DOCUMENT = "CREATE TABLE IF NOT EXISTS `{0}`(`Id` bigint primary key not null, `AccessHash` bigint, `Date` int, `MimeType` text, `Size` int, `Thumb` string, `DCId` int, `Version` int, `Attributes` string, `Tag` bigint)";
        private const string INSERT_TABLE_DOCUMENT = "INSERT OR REPLACE INTO `{0}` (Id,AccessHash,Date,MimeType,Size,Thumb,DCId,Version,Attributes,Tag) VALUES(?,?,?,?,?,?,?,?,?,?)";
        private const string SELECT_TABLE_DOCUMENT = "SELECT Id,AccessHash,Date,MimeType,Size,Thumb,DCId,Version,Attributes,Tag FROM `{0}`";

        private const string CREATE_TABLE_STORAGEFILE_MAPPING = "CREATE TABLE IF NOT EXISTS `{0}`(`Path` text primary key not null, `DateModified` datetime, `Id` bigint, `AccessHash` bigint)";
        private const string INSERT_TABLE_STORAGEFILE_MAPPING = "INSERT OR REPLACE INTO `{0}` (Path,DateModified,Id,AccessHash) VALUES('{1}','{2}',{3},{4})";
        private const string SELECT_TABLE_STORAGEFILE_MAPPING = "SELECT Path,DateModified,Id,AccessHash FROM `{0}` WHERE Path = '{1}'";

        #endregion

        private DatabaseContext()
        {
            _path = FileUtils.GetFileName("database.sqlite");
        }

        private void OpenDatabase(out Database database)
        {
            Sqlite3.sqlite3_open_v2(_path, out database, 2 | 4, string.Empty);
        }

        private void Execute(Database database, string query)
        {
            Statement statement;
            Sqlite3.sqlite3_prepare_v2(database, query, out statement);
            Sqlite3.sqlite3_step(statement);
            Sqlite3.sqlite3_finalize(statement);
        }

        private int ExecuteWithResult(Database database, string query)
        {
            Statement statement;
            Sqlite3.sqlite3_prepare_v2(database, query, out statement);
            Sqlite3.sqlite3_step(statement);
            var result = Sqlite3.sqlite3_column_int(statement, 0);
            Sqlite3.sqlite3_finalize(statement);

            return result;
        }

        private string Escape(string str)
        {
            return str.Replace("'", "''");
        }

        public int Count(string table)
        {
            Database database;
            OpenDatabase(out database);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, table));
            var result = ExecuteWithResult(database, string.Format(COUNT_TABLE, table));
            Sqlite3.sqlite3_close(database);

            return result;
        }

        public void InsertDocuments(string table, IEnumerable<TLDocument> documents, bool delete, long tag = 0)
        {
            Database database;
            Statement statement;
            OpenDatabase(out database);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, table));
            Execute(database, "BEGIN IMMEDIATE TRANSACTION");

            if (delete)
            {
                Execute(database, string.Format("DELETE FROM `{0}`", table));
            }

            Sqlite3.sqlite3_prepare_v2(database, string.Format(INSERT_TABLE_DOCUMENT, table), out statement);

            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            foreach (var item in documents)
            {
                var thumb = JsonConvert.SerializeObject(item.Thumb, settings);
                var attributes = JsonConvert.SerializeObject(item.Attributes, settings);

                Sqlite3.sqlite3_bind_int64(statement, 1, item.Id);
                Sqlite3.sqlite3_bind_int64(statement, 2, item.AccessHash);
                Sqlite3.sqlite3_bind_int(statement, 3, item.Date);
                Sqlite3.sqlite3_bind_text(statement, 4, item.MimeType, -1);
                Sqlite3.sqlite3_bind_int(statement, 5, item.Size);
                Sqlite3.sqlite3_bind_text(statement, 6, thumb, -1);
                Sqlite3.sqlite3_bind_int(statement, 7, item.DCId);
                Sqlite3.sqlite3_bind_int(statement, 8, item.Version);
                Sqlite3.sqlite3_bind_text(statement, 9, attributes, -1);
                Sqlite3.sqlite3_bind_int64(statement, 10, tag);

                Sqlite3.sqlite3_step(statement);
                Sqlite3.sqlite3_reset(statement);
            }

            Sqlite3.sqlite3_finalize(statement);

            Execute(database, "COMMIT TRANSACTION");
            Sqlite3.sqlite3_close(database);
        }

        public List<TLDocument> SelectDocuments(string table)
        {
            return SelectDocuments(table, long.MaxValue);
        }

        public List<TLDocument> SelectDocuments(string table, long tag)
        {
            Database database;
            Statement statement;
            OpenDatabase(out database);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, table));

            var query = string.Format(SELECT_TABLE_DOCUMENT, table);
            if (tag < long.MaxValue)
            {
                query = $"{query} WHERE Tag = {tag}";
            }

            Sqlite3.sqlite3_prepare_v2(database, string.Format(SELECT_TABLE_DOCUMENT, table), out statement);

            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            var result = new List<TLDocument>();
            while (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
            {
                result.Add(new TLDocument
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

            return result;
        }

        public void InsertStickerSets(IList<TLMessagesStickerSet> stickerSets)
        {
            Database database;
            Statement statement;
            OpenDatabase(out database);

            Execute(database, CREATE_TABLE_STICKERSET);
            Execute(database, CREATE_TABLE_STICKERPACK);
            Execute(database, CREATE_INDEX_STICKERPACK);
            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, "Stickers"));
            Execute(database, "BEGIN IMMEDIATE TRANSACTION");

            Sqlite3.sqlite3_prepare_v2(database, INSERT_TABLE_STICKERSET, out statement);

            for (int i = 0; i < stickerSets.Count; i++)
            {
                var stickerSet = stickerSets[i];
                Sqlite3.sqlite3_bind_int64(statement, 1, stickerSet.Set.Id);
                Sqlite3.sqlite3_bind_int64(statement, 2, stickerSet.Set.AccessHash);
                Sqlite3.sqlite3_bind_text(statement, 3, stickerSet.Set.Title, -1);
                Sqlite3.sqlite3_bind_text(statement, 4, stickerSet.Set.ShortName, -1);
                Sqlite3.sqlite3_bind_int(statement, 5, stickerSet.Set.Count);
                Sqlite3.sqlite3_bind_int(statement, 6, stickerSet.Set.Hash);
                Sqlite3.sqlite3_bind_int(statement, 7, (int)stickerSet.Set.Flags);
                Sqlite3.sqlite3_bind_int(statement, 8, i);

                Sqlite3.sqlite3_step(statement);
                Sqlite3.sqlite3_reset(statement);
            }

            Sqlite3.sqlite3_finalize(statement);

            Execute(database, "COMMIT TRANSACTION");
            Execute(database, "BEGIN IMMEDIATE TRANSACTION");

            Sqlite3.sqlite3_prepare_v2(database, INSERT_TABLE_STICKERPACK, out statement);

            for (int i = 0; i < stickerSets.Count; i++)
            {
                var stickerSet = stickerSets[i];
                foreach (var item in stickerSet.Packs)
                {
                    foreach (var document in item.Documents)
                    {
                        Sqlite3.sqlite3_bind_int64(statement, 1, document);
                        Sqlite3.sqlite3_bind_int64(statement, 2, stickerSet.Set.Id);
                        Sqlite3.sqlite3_bind_text(statement, 3, item.Emoticon, -1);
                        Sqlite3.sqlite3_bind_int(statement, 4, i);

                        Sqlite3.sqlite3_step(statement);
                        Sqlite3.sqlite3_reset(statement);
                    }
                }
            }

            Sqlite3.sqlite3_finalize(statement);

            Execute(database, "COMMIT TRANSACTION");
            Execute(database, "BEGIN IMMEDIATE TRANSACTION");

            Sqlite3.sqlite3_prepare_v2(database, string.Format(INSERT_TABLE_DOCUMENT, "Stickers"), out statement);

            foreach (var stickerSet in stickerSets)
            {

                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
                foreach (var item in stickerSet.Documents.OfType<TLDocument>())
                {
                    var thumb = JsonConvert.SerializeObject(item.Thumb, settings);
                    var attributes = JsonConvert.SerializeObject(item.Attributes, settings);

                    Sqlite3.sqlite3_bind_int64(statement, 1, item.Id);
                    Sqlite3.sqlite3_bind_int64(statement, 2, item.AccessHash);
                    Sqlite3.sqlite3_bind_int(statement, 3, item.Date);
                    Sqlite3.sqlite3_bind_text(statement, 4, item.MimeType, -1);
                    Sqlite3.sqlite3_bind_int(statement, 5, item.Size);
                    Sqlite3.sqlite3_bind_text(statement, 6, thumb, -1);
                    Sqlite3.sqlite3_bind_int(statement, 7, item.DCId);
                    Sqlite3.sqlite3_bind_int(statement, 8, item.Version);
                    Sqlite3.sqlite3_bind_text(statement, 9, attributes, -1);
                    Sqlite3.sqlite3_bind_int64(statement, 10, stickerSet.Set.Id);

                    Sqlite3.sqlite3_step(statement);
                    Sqlite3.sqlite3_reset(statement);
                }
            }

            Sqlite3.sqlite3_finalize(statement);

            Execute(database, "COMMIT TRANSACTION");

            Sqlite3.sqlite3_close(database);
        }

        public void UpdateStickerSetsOrder(IList<TLStickerSet> stickerSets)
        {
            Database database;
            Statement statement;
            OpenDatabase(out database);

            Execute(database, CREATE_TABLE_STICKERSET);
            Execute(database, CREATE_TABLE_STICKERPACK);
            Execute(database, CREATE_INDEX_STICKERPACK);
            Execute(database, "BEGIN IMMEDIATE TRANSACTION");

            Sqlite3.sqlite3_prepare_v2(database, UPDATE_TABLE_STICKERSET_ORDER, out statement);

            for (int i = 0; i < stickerSets.Count; i++)
            {
                var stickerSet = stickerSets[i];
                Sqlite3.sqlite3_bind_int64(statement, 1, i);
                Sqlite3.sqlite3_bind_int64(statement, 2, stickerSet.Id);

                Sqlite3.sqlite3_step(statement);
                Sqlite3.sqlite3_reset(statement);
            }

            Sqlite3.sqlite3_finalize(statement);

            Execute(database, "COMMIT TRANSACTION");
            Execute(database, "BEGIN IMMEDIATE TRANSACTION");

            Sqlite3.sqlite3_prepare_v2(database, UPDATE_TABLE_STICKERPACK_ORDER, out statement);

            for (int i = 0; i < stickerSets.Count; i++)
            {
                var stickerSet = stickerSets[i];
                Sqlite3.sqlite3_bind_int64(statement, 1, i);
                Sqlite3.sqlite3_bind_int64(statement, 2, stickerSet.Id);

                Sqlite3.sqlite3_step(statement);
                Sqlite3.sqlite3_reset(statement);
            }

            Sqlite3.sqlite3_finalize(statement);

            Execute(database, "COMMIT TRANSACTION");

            Sqlite3.sqlite3_close(database);
        }


        public void RemoveStickerSets(IEnumerable<TLStickerSet> stickerSets)
        {
            Database database;
            OpenDatabase(out database);

            Execute(database, CREATE_TABLE_STICKERSET);
            Execute(database, "BEGIN IMMEDIATE TRANSACTION");

            foreach (var item in stickerSets)
            {
                Execute(database, string.Format("DELETE FROM `StickerPacks` WHERE SetId = {0}", item.Id));
                Execute(database, string.Format("DELETE FROM `StickerSets` WHERE Id = {0}", item.Id));
                Execute(database, string.Format("DELETE FROM `Stickers` WHERE Tag = {0}", item.Id));
            }

            Execute(database, "COMMIT TRANSACTION");
            Sqlite3.sqlite3_close(database);
        }

        public List<TLStickerSet> SelectStickerSets()
        {
            Database database;
            Statement statement;
            OpenDatabase(out database);

            Execute(database, CREATE_TABLE_STICKERSET);

            Sqlite3.sqlite3_prepare_v2(database, SELECT_TABLE_STICKERSET, out statement);

            var result = new List<TLStickerSet>();
            while (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
            {
                result.Add(new TLStickerSet
                {
                    Id = Sqlite3.sqlite3_column_int64(statement, 0),
                    AccessHash = Sqlite3.sqlite3_column_int64(statement, 1),
                    Title = Sqlite3.sqlite3_column_text(statement, 2),
                    ShortName = Sqlite3.sqlite3_column_text(statement, 3),
                    Count = Sqlite3.sqlite3_column_int(statement, 4),
                    Hash = Sqlite3.sqlite3_column_int(statement, 5),
                    Flags = (TLStickerSet.Flag)Sqlite3.sqlite3_column_int(statement, 6)
                });
            }

            Sqlite3.sqlite3_finalize(statement);
            Sqlite3.sqlite3_close(database);

            return result;
        }

        public TLStickerSet SelectStickerSet(long id)
        {
            Database database;
            Statement statement;
            OpenDatabase(out database);

            Execute(database, CREATE_TABLE_STICKERSET);

            Sqlite3.sqlite3_prepare_v2(database, SELECT_TABLE_STICKERSET + " WHERE `Id` = " + id, out statement);

            TLStickerSet result = null;
            while (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
            {
                result = new TLStickerSet
                {
                    Id = Sqlite3.sqlite3_column_int64(statement, 0),
                    AccessHash = Sqlite3.sqlite3_column_int64(statement, 1),
                    Title = Sqlite3.sqlite3_column_text(statement, 2),
                    ShortName = Sqlite3.sqlite3_column_text(statement, 3),
                    Count = Sqlite3.sqlite3_column_int(statement, 4),
                    Hash = Sqlite3.sqlite3_column_int(statement, 5),
                    Flags = (TLStickerSet.Flag)Sqlite3.sqlite3_column_int(statement, 6)
                };
            }

            Sqlite3.sqlite3_finalize(statement);
            Sqlite3.sqlite3_close(database);

            return result;
        }

        public List<TLStickerSetCovered> SelectStickerSetsAsCovered()
        {
            Database database;
            Statement statement;
            OpenDatabase(out database);

            Execute(database, CREATE_TABLE_STICKERSET);

            Sqlite3.sqlite3_prepare_v2(database, SELECT_TABLE_STICKERSET, out statement);

            var result = new List<TLStickerSetCovered>();
            while (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
            {
                result.Add(new TLStickerSetCovered
                {
                    Set = new TLStickerSet
                    {
                        Id = Sqlite3.sqlite3_column_int64(statement, 0),
                        AccessHash = Sqlite3.sqlite3_column_int64(statement, 1),
                        Title = Sqlite3.sqlite3_column_text(statement, 2),
                        ShortName = Sqlite3.sqlite3_column_text(statement, 3),
                        Count = Sqlite3.sqlite3_column_int(statement, 4),
                        Hash = Sqlite3.sqlite3_column_int(statement, 5),
                        Flags = (TLStickerSet.Flag)Sqlite3.sqlite3_column_int(statement, 6)
                    }
                });
            }

            Sqlite3.sqlite3_finalize(statement);
            Sqlite3.sqlite3_close(database);

            return result;
        }

        public List<TLDocument> SelectStickerPack(string emoticon)
        {
            Database database;
            Statement statement;
            OpenDatabase(out database);

            Execute(database, CREATE_TABLE_STICKERPACK);
            Execute(database, CREATE_INDEX_STICKERPACK);
            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, "Stickers"));

            Sqlite3.sqlite3_prepare_v2(database, string.Format(SELECT_TABLE_STICKERPACK, emoticon), out statement);

            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            var result = new List<TLDocument>();
            while (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
            {
                result.Add(new TLDocument
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

            return result;
        }

        public void InsertStorageFileMapping(string table, string fileName, DateTime dateModified, int id, long accessHash)
        {
            Database database;
            OpenDatabase(out database);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, table));

            var fileNameEscaped = Escape(fileName);
            var dateModifiedEscaped = dateModified.ToString("yyyy-MM-dd HH:mm:ss");

            Execute(database, string.Format(INSERT_TABLE_STORAGEFILE_MAPPING, table, fileNameEscaped, dateModifiedEscaped, id, accessHash));

            Sqlite3.sqlite3_close(database);
        }

        public bool SelectStorageFileMapping(string table, string fileName, out DateTime dateModified, out int id, out long accessHash)
        {
            Database database;
            Statement statement;
            OpenDatabase(out database);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, table));

            Sqlite3.sqlite3_prepare_v2(database, string.Format(SELECT_TABLE_DOCUMENT, table, Escape(fileName)), out statement);

            var result = false;
            if (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
            {
                dateModified = DateTime.Parse(Sqlite3.sqlite3_column_text(statement, 1));
                id = Sqlite3.sqlite3_column_int(statement, 2);
                accessHash = Sqlite3.sqlite3_column_int64(statement, 3);
                result = true;
            }
            else
            {
                dateModified = DateTime.MinValue;
                id = -1;
                accessHash = -1;
            }

            Sqlite3.sqlite3_finalize(statement);
            Sqlite3.sqlite3_close(database);

            return result;
        }
    }
}
