//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Td
{
    public class TdException : Exception
    {
        public TdException(string message)
            : base(message)
        {
            IsUnhandled = true;
        }

        public TdException()
        {
            IsUnhandled = false;
        }

        public bool IsUnhandled { get; }

        public static TdException FromMessage(string message)
        {
            if (IsDatabaseBrokenError(message))
            {
                return new TdDatabaseBrokenException();
            }
            else if (IsDiskFullError(message))
            {
                return new TdDiskFullException();
            }
            else if (IsDiskError(message))
            {
                return new TdDiskException();
            }
            else if (IsBinlogError(message))
            {
                return new TdBinlogReindexException();
            }

            return new TdException(message);
        }

        public static bool IsDatabaseBrokenError(string message)
        {
            return message.Contains("Wrong key or database is corrupted") ||
                message.Contains("SQL logic error or missing database") ||
                message.Contains("database disk image is malformed") ||
                message.Contains("file is encrypted or is not a database") ||
                message.Contains("unsupported file format");
        }

        public static bool IsDiskFullError(string message)
        {
            return message.Contains("There is not enough space on the disk") ||
                message.Contains(": 112 :") ||
                message.Contains("database or disk is full") ||
                message.Contains("out of memory for database");
        }

        public static bool IsDiskError(string message)
        {
            // This is UNIX stuff and has no sense on Windows but I'm lazy to see
            // if there's any equivalents that we need to cover.
            return message.Contains("I/O error") || message.Contains("Structure needs cleaning");
        }

        public static bool IsBinlogError(string message)
        {
            return message.Contains("Failed to rename binlog") ||
                message.Contains("Failed to unlink old binlog") ||
                message.Contains(": 8 :");
        }
    }

    public class TdDatabaseBrokenException : TdException
    {

    }

    public class TdDiskFullException : TdException
    {

    }

    public class TdDiskException : TdException
    {

    }

    public class TdBinlogReindexException : TdException
    {

    }
}
