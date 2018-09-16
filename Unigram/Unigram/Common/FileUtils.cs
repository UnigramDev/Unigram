using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.Foundation;
using Windows.Storage.FileProperties;
using Windows.Graphics.Imaging;
using Windows.ApplicationModel;
using Windows.Security.Cryptography;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Unigram.Common
{
    public static class FileUtils
    {
        public static string GetFileName(string fileName)
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, SettingsService.Current.ActiveSession.ToString(), fileName);
        }

        public static string GetTempFileName(string fileName)
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp", fileName);
        }

        public static string GetTempFilePath(string fileName)
        {
            return $"temp\\{fileName}";
        }

        public static string GetFilePath(string fileName)
        {
            return $"{SettingsService.Current.ActiveSession}\\{fileName}";
        }

        public static IAsyncOperation<StorageFile> CreateFileAsync(string fileName, CreationCollisionOption options = CreationCollisionOption.ReplaceExisting)
        {
            return ApplicationData.Current.LocalFolder.CreateFileAsync($"{SettingsService.Current.ActiveSession}\\{fileName}", options);
        }

        public static IAsyncOperation<StorageFile> CreateTempFileAsync(string fileName, CreationCollisionOption options = CreationCollisionOption.ReplaceExisting)
        {
            return ApplicationData.Current.LocalFolder.CreateFileAsync($"temp\\{fileName}", options);
        }

        public static IAsyncOperation<IStorageItem> TryGetItemAsync(string fileName)
        {
            return ApplicationData.Current.LocalFolder.TryGetItemAsync($"{SettingsService.Current.ActiveSession}\\{fileName}");
        }

        public static bool Delete(object syncRoot, string fileName)
        {
            try
            {
                lock (syncRoot)
                {
                    if (File.Exists(GetFileName(fileName)))
                    {
                        File.Delete(GetFileName(fileName));
                    }
                }
                return true;
            }
            catch (Exception e)
            {

            }
            return false;
        }

        public static void Write(object syncRoot, string directoryName, string fileName, string str)
        {
            lock (syncRoot)
            {
                if (!Directory.Exists(GetFileName(directoryName)))
                {
                    Directory.CreateDirectory(GetFileName(directoryName));
                }

                using (var file = File.Open(Path.Combine(GetFileName(directoryName), fileName), FileMode.Append))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(str);
                    file.Write(bytes, 0, bytes.Length);
                }
            }
        }

        public static void SaveWithTempFile<T>(string fileName, T data)
        {
            string text = fileName + ".temp";
            //using (var file = File.Open(GetFileName(text), FileMode.Create))
            //{
            //    using (var to = new TLBinaryWriter(file))
            //    {
            //        data.Write(to);
            //    }

            //}
            //var buffer = TLObjectSerializer.Serialize(data);
            //File.WriteAllBytes(GetFileName(text), buffer.ToArray());

            //File.Copy(GetFileName(text), GetFileName(fileName), true);
        }
    }
}
