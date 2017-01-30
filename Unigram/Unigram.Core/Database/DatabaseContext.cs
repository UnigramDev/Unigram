using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private const string CREATE_TABLE_DOCUMENT = "CREATE TABLE `{0}`('Id' bigint primary key not null, 'AccessHash' bigint, 'Date' int, 'MimeType' text, 'Size' int, 'DCId' int, 'Version' int)";
        private const string INSERT_TABLE_DOCUMENT = "INSERT OR REPLACE INTO `{0}` (Id,AccessHash,Date,MimeType,Size,DCId,Version) VALUES({1},{2},{3},'{4}',{5},{6},{7});";

        #endregion

        private DatabaseContext()
        {
            _path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "database.sqlite");
        }

        private void Execute(Database database, string query)
        {
            Statement statement = null;
            Sqlite3.sqlite3_prepare_v2(database, query, out statement);
            Sqlite3.sqlite3_step(statement);
            Sqlite3.sqlite3_finalize(statement);
        }

        private string Escape(string str)
        {
            return str.Replace("'", "''");
        }

        public void InsertGifs(IEnumerable<TLDocument> gifs, bool delete)
        {
            Database database;
            Sqlite3.sqlite3_open_v2(_path, out database, 2 | 4, string.Empty);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, "Gifs"));
            Execute(database, "BEGIN IMMEDIATE TRANSACTION");

            if (delete)
            {
                Execute(database, "DELETE FROM `Gifs`");
            }

            foreach (var item in gifs)
            {
                Execute(database, string.Format(INSERT_TABLE_DOCUMENT, "Gifs", item.Id, item.AccessHash, item.Date, Escape(item.MimeType), item.Size, item.DCId, item.Version));
            }

            Execute(database, "COMMIT TRANSACTION");
            Sqlite3.sqlite3_close(database);
        }

        public List<TLDocument> SelectGifs()
        {
            Database database;
            Statement statement;
            Sqlite3.sqlite3_open_v2(_path, out database, 2 | 4, string.Empty);

            Execute(database, string.Format(CREATE_TABLE_DOCUMENT, "Gifs"));

            Sqlite3.sqlite3_prepare_v2(database, $"SELECT Id,AccessHash,Date,MimeType,Size,DCId,Version FROM `Gifs`", out statement);

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
                    DCId = Sqlite3.sqlite3_column_int(statement, 5),
                    Version = Sqlite3.sqlite3_column_int(statement, 6),
                    Attributes = new TLVector<TLDocumentAttributeBase>(),
                    Thumb = new TLPhotoSizeEmpty()
                });
            }

            Sqlite3.sqlite3_finalize(statement);
            Sqlite3.sqlite3_close(database);

            return result;
        }
    }
}
