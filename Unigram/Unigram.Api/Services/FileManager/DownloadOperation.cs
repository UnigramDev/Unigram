using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Upload.Methods;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Telegram.Api.Services.FileManager
{







    public enum FileType
    {
        Photo,
        Audio,
        Video,
        File
    }

    public delegate void FileDidFinishLoadingFile(FileLoadOperation operation, FileInfo finalFile);
    public delegate void FileDidFailedLoadingFile(FileLoadOperation operation, int state);
    public delegate void FileDidChangedLoadProgress(FileLoadOperation operation, float progress);

    public static class FileExt
    {
        public static T Poll<T>(this List<T> list)
        {
            var item = list[0];
            list.RemoveAt(0);
            return item;
        }

        public static bool RenameTo(this FileInfo original, FileInfo file)
        {
            try
            {
                original.Refresh();
                original.MoveTo(file.FullName);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ImageLoader
    {
        public static String getHttpUrlExtension(String url, String defaultExt)
        {
            String ext = null;
            int idx = url.LastIndexOf('.');
            if (idx != -1)
            {
                ext = url.Substring(idx + 1);
            }
            if (ext == null || ext.Length == 0 || ext.Length > 4)
            {
                ext = defaultExt;
            }
            return ext;
        }
    }
}
