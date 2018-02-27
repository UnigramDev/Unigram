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
using Telegram.Api.TL;
using Windows.Storage.FileProperties;
using Windows.Graphics.Imaging;
using Windows.ApplicationModel;
using Windows.Security.Cryptography;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Telegram.Api.Helpers
{
    public static class FileUtils
    {
        public static string GetFileName(string fileName)
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, SettingsHelper.SelectedAccount.ToString(), fileName);
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
            return $"{SettingsHelper.SelectedAccount}\\{fileName}";
        }

        public static IAsyncOperation<StorageFile> CreateFileAsync(string fileName, CreationCollisionOption options = CreationCollisionOption.ReplaceExisting)
        {
            return ApplicationData.Current.LocalFolder.CreateFileAsync($"{SettingsHelper.SelectedAccount}\\{fileName}", options);
        }

        public static IAsyncOperation<StorageFile> CreateTempFileAsync(string fileName, CreationCollisionOption options = CreationCollisionOption.ReplaceExisting)
        {
            return ApplicationData.Current.LocalFolder.CreateFileAsync($"temp\\{fileName}", options);
        }

        public static IAsyncOperation<IStorageItem> TryGetItemAsync(string fileName)
        {
            return ApplicationData.Current.LocalFolder.TryGetItemAsync($"{SettingsHelper.SelectedAccount}\\{fileName}");
        }

        public static void CreateTemporaryFolder()
        {
            if (!Directory.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp\\parts")))
            {
                Directory.CreateDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp"));
                Directory.CreateDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp\\parts"));
                Directory.CreateDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp\\placeholders"));
            }

            if (Directory.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SettingsHelper.SelectedAccount}\\temp")))
            {
                // Delete old temp folder if it exists
                Directory.Delete(Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SettingsHelper.SelectedAccount}\\temp"), true);
            }

            if (!Directory.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SettingsHelper.SelectedAccount}")))
            {
                // Delete old temp folder if it exists
                Directory.CreateDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SettingsHelper.SelectedAccount}"));
            }

            var folders = Directory.GetDirectories(ApplicationData.Current.LocalFolder.Path);
            foreach (var folder in folders)
            {
                if (folder.EndsWith("\\0"))
                {
                    continue;
                }

                try
                {
                    Directory.Move(folder, Path.Combine(ApplicationData.Current.LocalFolder.Path, "0", Path.GetFileName(folder)));
                }
                catch { }
            }

            var files = Directory.GetFiles(ApplicationData.Current.LocalFolder.Path);
            foreach (var file in files)
            {
                try
                {
                    Directory.Move(file, Path.Combine(ApplicationData.Current.LocalFolder.Path, "0", Path.GetFileName(file)));
                }
                catch { }
            }
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

        public static void SaveWithTempFile<T>(string fileName, T data) where T : TLObject
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
