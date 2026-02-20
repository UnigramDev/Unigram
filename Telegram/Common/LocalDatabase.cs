//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Telegram.Common
{
    // Courtesy of Microsoft's AppCenter
    public class LocalDatabase
    {
        private sqlite3 _db;

        static LocalDatabase()
        {
            try
            {
                Batteries_V2.Init();
            }
            catch
            {
                Logger.Error("Failed to initialize sqlite3 provider.");
            }
        }

        public void Initialize(string databasePath)
        {
            var result = raw.sqlite3_open(databasePath, out _db);
            if (result != raw.SQLITE_OK)
            {
                throw ToStorageException(result, "Failed to open database connection");
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_db != null)
            {
                _db.Dispose();
                _db = null;
            }
        }

        private void BindParameter(sqlite3_stmt stmt, int index, object value)
        {
            int result;
            if (value is string stringValue)
            {
                result = raw.sqlite3_bind_text(stmt, index, stringValue);
            }
            else if (value is int intValue)
            {
                result = raw.sqlite3_bind_int(stmt, index, intValue);
            }
            else if (value is long longValue)
            {
                result = raw.sqlite3_bind_int64(stmt, index, longValue);
            }
            else if (value == null)
            {
                result = raw.sqlite3_bind_null(stmt, index);
            }
            else
            {
                throw new NotSupportedException($"Type {value.GetType().FullName} not supported.");
            }
            if (result != raw.SQLITE_OK)
            {
                throw ToStorageException(result, $"Failed to bind {index} parameter");
            }
        }

        private void BindParameters(sqlite3_stmt stmt, IList<object> values)
        {
            for (var i = 0; i < values?.Count; i++)
            {
                // Parameters in statement are 1-based. See https://www.sqlite.org/c3ref/bind_blob.html
                BindParameter(stmt, i + 1, values[i]);
            }
        }

        private object GetColumnValue(sqlite3_stmt stmt, int index)
        {
            var columnType = raw.sqlite3_column_type(stmt, index);
            switch (columnType)
            {
                case raw.SQLITE_INTEGER:
                    return raw.sqlite3_column_int64(stmt, index);
                case raw.SQLITE_TEXT:
                    return raw.sqlite3_column_text(stmt, index).utf8_to_string();
                case raw.SQLITE_NULL:
                    return null;
            }
            Logger.Error($"Attempt to get unsupported column value {columnType}.");
            return null;
        }

        private int ExecuteNonSelectionSqlQuery(string query, IList<object> args = null)
        {
            var db = _db ?? throw new StorageException("The database wasn't initialized.");
            var result = raw.sqlite3_prepare_v2(db, query, out var stmt);
            if (result != raw.SQLITE_OK)
            {
                throw ToStorageException(result, "Failed to prepare SQL query");
            }
            try
            {
                BindParameters(stmt, args);
                result = raw.sqlite3_step(stmt);
                if (result != raw.SQLITE_DONE)
                {
                    throw ToStorageException(result, "Failed to run query");
                }
            }
            finally
            {
                result = raw.sqlite3_finalize(stmt);
                if (result != raw.SQLITE_OK)
                {
                    Logger.Error($"Failed to finalize statement, result={result}");
                }
            }
            return result;
        }

        private List<object[]> ExecuteSelectionSqlQuery(string query, IList<object> args = null)
        {
            var db = _db ?? throw new StorageException("The database wasn't initialized.");
            var result = raw.sqlite3_prepare_v2(db, query, out var stmt);
            if (result != raw.SQLITE_OK)
            {
                throw ToStorageException(result, "Failed to prepare SQL query");
            }
            try
            {
                var entries = new List<object[]>();
                BindParameters(stmt, args);
                while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    var count = raw.sqlite3_column_count(stmt);
                    entries.Add(Enumerable.Range(0, count).Select(i => GetColumnValue(stmt, i)).ToArray());
                }
                return entries;
            }
            finally
            {
                result = raw.sqlite3_finalize(stmt);
                if (result != raw.SQLITE_OK)
                {
                    Logger.Error($"Failed to finalize statement, result={result}");
                }
            }
        }

        public void CreateTable(string tableName, string[] columnNames, string[] columnTypes)
        {
            var tableClause = string.Join(",", Enumerable.Range(0, columnNames.Length).Select(i => $"{columnNames[i]} {columnTypes[i]}"));
            ExecuteNonSelectionSqlQuery($"CREATE TABLE IF NOT EXISTS {tableName} ({tableClause});");
        }

        public int Count(string tableName, string columnName, object value)
        {
            var result = ExecuteSelectionSqlQuery($"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = ?;", new[] { value });
            var count = (long)(result.FirstOrDefault()?.FirstOrDefault() ?? 0L);
            return (int)count;
        }

        public IList<object[]> Select(string tableName, string columnName, object value, string excludeColumnName, object[] excludeValues, int? limit = null, string[] orderList = null)
        {
            var whereClause = $"{columnName} = ?";
            var args = new List<object> { value };
            if (excludeValues?.Length > 0)
            {
                whereClause += $" AND {excludeColumnName} NOT IN ({BuildBindingMask(excludeValues.Length)})";
                args.AddRange(excludeValues);
            }
            var limitClause = limit != null ? $" LIMIT {limit}" : string.Empty;
            var orderClause = orderList != null && orderList.Length > 0 ? $" ORDER BY {string.Join(",", orderList)} DESC" : string.Empty;
            var query = $"SELECT * FROM {tableName} WHERE {whereClause}{orderClause}{limitClause};";
            return ExecuteSelectionSqlQuery(query, args);
        }

        public IList<object[]> Select(string tableName, int? limit = null, string[] orderList = null)
        {
            var limitClause = limit != null ? $" LIMIT {limit}" : string.Empty;
            var orderClause = orderList != null && orderList.Length > 0 ? $" ORDER BY {string.Join(",", orderList)} DESC" : string.Empty;
            var query = $"SELECT * FROM {tableName}{orderClause}{limitClause};";
            return ExecuteSelectionSqlQuery(query);
        }

        public void Insert(string tableName, string[] columnNames, ICollection<object[]> values)
        {
            var columnsClause = string.Join(",", columnNames);
            var valueClause = $"({BuildBindingMask(values.First().Length)})";
            var valuesClause = string.Join(",", Enumerable.Repeat(valueClause, values.Count));
            var valuesArray = values.SelectMany(i => i).ToArray();
            ExecuteNonSelectionSqlQuery($"INSERT INTO {tableName}({columnsClause}) VALUES {valuesClause};", valuesArray);
        }

        public void Update(string tableName, string[] columnNames, object[] values, string whereColumnName, object whereValue)
        {
            if (columnNames.Length != values.Length)
            {
                throw new ArgumentException("Column names and values must have the same length");
            }

            var setClause = string.Join(", ", columnNames.Select(col => $"{col} = ?"));
            var query = $"UPDATE {tableName} SET {setClause} WHERE {whereColumnName} = ?;";

            var args = new List<object>(values);
            args.Add(whereValue);

            ExecuteNonSelectionSqlQuery(query, args);
        }

        public void Delete(string tableName, string columnName, params object[] values)
        {
            var whereMask = $"{columnName} IN ({BuildBindingMask(values.Length)})";
            ExecuteNonSelectionSqlQuery($"DELETE FROM {tableName} WHERE {whereMask};", values);
        }

        public long ExecuteScalarQuery(string query, IList<object> args = null)
        {
            var result = ExecuteSelectionSqlQuery(query, args);
            if (result.Count > 0 && result[0].Length > 0)
            {
                return (long)result[0][0];
            }
            return 0;
        }

        public List<object[]> ExecuteSelectionQuery(string query, IList<object> args = null)
        {
            return ExecuteSelectionSqlQuery(query, args);
        }

        public int GetLastInsertRowId()
        {
            return (int)raw.sqlite3_last_insert_rowid(_db);
        }

        private StorageException ToStorageException(int result, string message)
        {
            var errorMessage = raw.sqlite3_errmsg(_db).utf8_to_string();
            var exceptionMessage = $"{message}, result={result}\n\t{errorMessage}";
            switch (result)
            {
                case raw.SQLITE_CORRUPT:
                case raw.SQLITE_NOTADB:
                    return new StorageCorruptedException(exceptionMessage);
                case raw.SQLITE_FULL:
                    return new StorageFullException(exceptionMessage);
                default:
                    return new StorageException(exceptionMessage);
            }
        }

        private static string BuildBindingMask(int amount)
        {
            return string.Join(",", Enumerable.Repeat("?", amount));
        }
    }

    internal class StorageCorruptedException : StorageException
    {
        public StorageCorruptedException(string message) : base(message)
        {
        }
    }

    internal class StorageFullException : StorageException
    {
        public StorageFullException(string message) : base(message)
        {
        }
    }

    internal class StorageException : Exception
    {
        public StorageException(string message) : base(message)
        {
        }
    }
}
