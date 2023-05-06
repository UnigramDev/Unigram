//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Path = System.IO.Path;
using SAP = Windows.Storage.AccessCache.StorageApplicationPermissions;

namespace Telegram.Services
{
    public class DownloadFolder
    {
        public string DisplayPath { get; }

        public string Path { get; }

        public bool IsCustom { get; }

        public DownloadFolder(bool custom, StorageFolder folder)
        {
            var directoryName = System.IO.Path.GetDirectoryName(folder.Path);
            var path = System.IO.Path.Combine(directoryName, folder.DisplayName);

            DisplayPath = path;
            Path = folder.Path;

            IsCustom = custom;
        }

        public DownloadFolder(bool custom, string path)
        {
            DisplayPath = path;
            Path = path;

            IsCustom = custom;
        }

        public override string ToString()
        {
            return DisplayPath;
        }
    }

    public interface IStorageService
    {
        Task SaveFileAsAsync(File file);

        Task OpenFileAsync(File file);

        Task OpenFileWithAsync(File file);

        Task OpenFolderAsync(File file);

        bool CheckAccessToFolder(File file);

        Task<DownloadFolder> GetDownloadFolderAsync();

        Task<DownloadFolder> SetDownloadFolderAsync(StorageFolder folder);
    }

    public class StorageService : IStorageService
    {
        private readonly IClientService _clientService;

        public StorageService(IClientService clientService)
        {
            _clientService = clientService;
        }

        public async Task SaveFileAsAsync(File file)
        {
            // TODO: the current logic doesn't support Save as... before the file is downloaded
            // This is because to download a file to a specific path we have to create a link
            // in advance, while we don't want files saved as to be linked as permanent files.

            // When saving a file as, we always want to retrieve the cached copy
            var cached = await _clientService.GetFileAsync(file);
            if (cached == null)
            {
                return;
            }

            var response = await _clientService.SendAsync(new GetSuggestedFileName(file.Id, string.Empty));
            if (response is not Text text)
            {
                return;
            }

            try
            {
                var extension = Path.GetExtension(text.TextValue);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".dat";
                }

                var displayExtension = extension.TrimStart('.').ToUpper();
                var picker = new FileSavePicker();
                picker.FileTypeChoices.Add($"{displayExtension} File", new[] { extension });
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.SuggestedFileName = text.TextValue;

                var picked = await picker.PickSaveFileAsync();
                if (picked != null)
                {
                    // Save as copy is never linked back 
                    await cached.CopyAndReplaceAsync(picked);
                }
            }
            catch { }
        }

        public Task OpenFileAsync(File file)
        {
            return OpenFileAsync(file);
        }

        public Task OpenFileWithAsync(File file)
        {
            return OpenFileAsync(file, new LauncherOptions
            {
                DisplayApplicationPicker = true
            });
        }

        private async Task OpenFileAsync(File file, LauncherOptions options = null)
        {
            // When opening a file, we always want to retrieve the permanent copy
            var permanent = await _clientService.GetPermanentFileAsync(file);
            if (permanent == null)
            {
                return;
            }

            try
            {
                var opened = await Launcher.LaunchFileAsync(permanent, options);
                if (opened)
                {
                    return;
                }

                await OpenFolderAsync(permanent);
            }
            catch { }
        }

        public async Task OpenFolderAsync(File file)
        {
            // When opening a file, we always want to retrieve the permanent copy
            var permanent = await _clientService.GetPermanentFileAsync(file);
            if (permanent == null)
            {
                return;
            }

            await OpenFolderAsync(permanent);
        }

        private async Task OpenFolderAsync(StorageFile permanent)
        {
            try
            {
                var folder = await permanent.GetParentAsync();
                folder ??= await GetDownloadFolderAsync2();

                if (folder != null && Extensions.IsRelativePath(folder.Path, permanent.Path, out _))
                {
                    var options = new FolderLauncherOptions();
                    options.ItemsToSelect.Add(permanent);

                    await Launcher.LaunchFolderAsync(folder, options);
                }
            }
            catch { }
        }

        private static IAsyncOperation<StorageFolder> GetDownloadFolderAsync2()
        {
            try
            {
                if (ApiInfo.HasDownloadFolder)
                {
                    return KnownFolders.GetFolderAsync(KnownFolderId.DownloadsFolder);
                }

                return null;
            }
            catch
            {
                return AsyncInfo.Run<StorageFolder>(task => null);
            }
        }

        public bool CheckAccessToFolder(File file)
        {
            if (ApiInfo.HasDownloadFolder)
            {
                return true;
            }

            return Future.Contains(file.Remote.UniqueId)
                || Future.Contains(Future.DownloadFolder);
        }

        public Task<DownloadFolder> GetDownloadFolderAsync()
        {
            return Future.GetFolderAsync();
        }

        public async Task<DownloadFolder> SetDownloadFolderAsync(StorageFolder folder)
        {
            if (folder == null)
            {
                if (Future.Contains(Future.DownloadFolder))
                {
                    Future.Remove(Future.DownloadFolder);
                }

                return await Future.GetFolderAsync();
            }

            var downloads = await Future.GetDefaultFolderAsync();
            if (downloads == null || Extensions.IsRelativePath(downloads.Path, folder.Path, out _))
            {
                Future.Remove(Future.DownloadFolder);
            }
            else
            {
                Future.AddOrReplace(Future.DownloadFolder, folder);
            }

            return await Future.GetFolderAsync();
        }



        public static class Future
        {
            public const string DownloadFolder = "FilesDirectory";

            public static bool Contains(string token, bool temp = false)
            {
                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                return SAP.FutureAccessList.ContainsItem(temp ? token + "temp" : token);
            }

            public static void Remove(string token, bool temp = false)
            {
                if (SAP.FutureAccessList.ContainsItem(temp ? token + "temp" : token))
                {
                    SAP.FutureAccessList.Remove(temp ? token + "temp" : token);
                }
            }

            public static void AddOrReplace(string token, IStorageItem item, bool temp = false)
            {
                SAP.FutureAccessList.EnqueueOrReplace(temp ? token + "temp" : token, item);
            }

            public static bool CheckAccess(IStorageItem item)
            {
                if (Extensions.IsRelativePath(ApplicationData.Current.LocalFolder.Path, item.Path, out string _))
                {
                    return false;
                }

                return SAP.FutureAccessList.CheckAccess(item);
            }

            public static async Task<bool> ContainsAsync(string token, bool temp = false)
            {
                var destination = await GetFileAsync(token, temp);
                return destination != null;
            }

            public static async Task<StorageFile> GetFileAsync(string token, bool temp = false)
            {
                try
                {
                    if (Contains(token, temp))
                    {
                        return await SAP.FutureAccessList.GetFileAsync(temp ? token + "temp" : token);
                    }

                    return null;
                }
                catch
                {
                    Remove(temp ? token + "temp" : token);
                    return null;
                }
            }

            public static async Task<StorageFile> CreateFileAsync(string tempFileName)
            {
                if (ApiInfo.HasCacheOnly)
                {
                    return null;
                }

                await MigrateDownloadFolderAsync();

                if (SAP.FutureAccessList.ContainsItem(DownloadFolder))
                {
                    try
                    {
                        StorageFolder folder = await SAP.FutureAccessList.GetFolderAsync(DownloadFolder);
                        return await folder.CreateFileAsync(tempFileName, CreationCollisionOption.GenerateUniqueName);
                    }
                    catch
                    {
                        Remove(DownloadFolder);
                    }
                }

                return await DownloadsFolder.CreateFileAsync(tempFileName, CreationCollisionOption.GenerateUniqueName);
            }

            public static async Task<DownloadFolder> GetFolderAsync()
            {
                if (ApiInfo.HasCacheOnly)
                {
                    return null;
                }

                await MigrateDownloadFolderAsync();

                if (SAP.FutureAccessList.ContainsItem(DownloadFolder))
                {
                    try
                    {
                        return new DownloadFolder(true, await SAP.FutureAccessList.GetFolderAsync(DownloadFolder));
                    }
                    catch
                    {
                        Remove(DownloadFolder);
                    }
                }

                if (ApiInfo.HasKnownFolders)
                {
                    return new DownloadFolder(false, await GetDefaultFolderAsync());
                }

                return new DownloadFolder(false, Strings.DownloadFolderDefault);
            }

            public static IAsyncOperation<StorageFolder> GetDefaultFolderAsync()
            {
                if (ApiInfo.HasKnownFolders)
                {
                    return KnownFolders.GetFolderAsync(KnownFolderId.DownloadsFolder);
                }

                return null;
            }

            public static async Task MigrateDownloadFolderAsync()
            {
                if (SAP.MostRecentlyUsedList.ContainsItem(DownloadFolder))
                {
                    try
                    {
                        StorageFolder folder = await SAP.MostRecentlyUsedList.GetFolderAsync(DownloadFolder);
                        SAP.FutureAccessList.EnqueueOrReplace(DownloadFolder, folder);
                    }
                    catch
                    {
                        // The app still remembers about the custom folder
                        // but we have no longer access to it (deleted, or whatever) 
                    }

                    SAP.MostRecentlyUsedList.Remove(DownloadFolder);
                }
            }
        }
    }
}
