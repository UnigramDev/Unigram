using System;
using System.Collections.Generic;
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

        private const string CREATE_TABLE_DOCUMENT = "CREATE TABLE IF NOT EXISTS `{0}`('Id' bigint primary key not null, 'AccessHash' bigint, 'Date' int, 'MimeType' text, 'Size' int, 'Thumb' string, 'DCId' int, 'Version' int, 'Attributes' string)";
        private const string INSERT_TABLE_DOCUMENT = "INSERT OR REPLACE INTO `{0}` (Id,AccessHash,Date,MimeType,Size,Thumb,DCId,Version,Attributes) VALUES({1},{2},{3},'{4}',{5},'{6}',{7},{8},'{9}');";
        private const string SELECT_TABLE_DOCUMENT = "SELECT Id,AccessHash,Date,MimeType,Size,Thumb,DCId,Version,Attributes FROM `{0}`";

        private const string CREATE_TABLE_STORAGEFILE_MAPPING = "CREATE TABLE IF NOT EXISTS `{0}`('Path' text primary key not null, 'DateModified' datetime, 'Id' bigint, 'AccessHash' bigint)";
        private const string INSERT_TABLE_STORAGEFILE_MAPPING = "INSERT OR REPLACE INTO `{0}` (Path,DateModified,Id,AccessHash) VALUES('{1}','{2}',{3},{4});";
        private const string SELECT_TABLE_STORAGEFILE_MAPPING = "SELECT Path,DateModified,Id,AccessHash FROM `{0}` WHERE Path = '{1}'";

        #endregion

        private DatabaseContext()
        {
            _path = FileUtils.GetFileName("database.sqlite");
        }

        private void Execute(Database database, string query)
        {
            Statement statement = null;
            Sqlite3.sqlite3_prepare_v2(database, query, out statement);
            Sqlite3.sqlite3_step(statement);
            Sqlite3.sqlite3_finalize(statement);
        }

        private int ExecuteWithResult(Database database, string query)
        {
            Statement statement = null;
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
            Sqlite3.sqlite3_open_v2(_path, out database, 2 | 4, string.Empty);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, table));
            var result = ExecuteWithResult(database, string.Format(COUNT_TABLE, table));
            Sqlite3.sqlite3_close(database);

            return result;
        }

        public void InsertDocuments(string table, IEnumerable<TLDocument> documents, bool delete)
        {
            Database database;
            Sqlite3.sqlite3_open_v2(_path, out database, 2 | 4, string.Empty);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, table));
            Execute(database, "BEGIN IMMEDIATE TRANSACTION");

            if (delete)
            {
                Execute(database, string.Format("DELETE FROM `{0}`", table));
            }

            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            foreach (var item in documents)
            {
                var thumb = JsonConvert.SerializeObject(item.Thumb, settings);
                var attributes = JsonConvert.SerializeObject(item.Attributes, settings);

                Execute(database, string.Format(INSERT_TABLE_DOCUMENT, table, item.Id, item.AccessHash, item.Date, Escape(item.MimeType), item.Size, Escape(thumb), item.DCId, item.Version, Escape(attributes)));
            }

            Execute(database, "COMMIT TRANSACTION");
            Sqlite3.sqlite3_close(database);
        }

        public List<TLDocument> SelectDocuments(string table)
        {
            Database database;
            Statement statement;
            Sqlite3.sqlite3_open_v2(_path, out database, 2 | 4, string.Empty);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, table));

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

        public void InsertStorageFileMapping(string table, string fileName, DateTime dateModified, int id, long accessHash)
        {
            Database database;
            Sqlite3.sqlite3_open_v2(_path, out database, 2 | 4, string.Empty);

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
            Sqlite3.sqlite3_open_v2(_path, out database, 2 | 4, string.Empty);

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
