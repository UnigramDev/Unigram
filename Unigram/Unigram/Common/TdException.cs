using System;

namespace Unigram
{
    public class TdException : Exception
    {
        public TdException(string message)
            : base(message)
        {

        }

        public TdException()
        {

        }

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

            return new TdException(message);
        }

        private static bool IsDatabaseBrokenError(string message)
        {
            return message.Contains("Wrong key or database is corrupted") ||
                    message.Contains("SQL logic error or missing database") ||
                    message.Contains("database disk image is malformed") ||
                    message.Contains("file is encrypted or is not a database") ||
                    message.Contains("unsupported file format");
        }

        private static bool IsDiskFullError(string message)
        {
            return message.Contains("There is not enough space on the disk") ||
                    message.Contains("database or disk is full") ||
                    message.Contains("out of memory for database");
        }

        private static bool IsDiskError(string message)
        {
            // This is UNIX stuff and has no sense on Windows but I'm lazy to see
            // if there's any equivalents that we need to cover.
            return message.Contains("I/O error") || message.Contains("Structure needs cleaning");
        }

        private static bool IsExternalError(string message)
        {
            return IsDatabaseBrokenError(message) || IsDiskFullError(message) || IsDiskError(message);
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
}
