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

namespace Telegram.Services
{
    public interface IStorageService
    {
        Task SaveFileAsAsync(File file);

        Task OpenFileAsync(File file);

        Task OpenFileWithAsync(File file);

        Task OpenFolderAsync(File file);
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
                folder ??= await GetDownloadsFolderAsync();

                if (folder != null && IsRelativePath(folder.Path, permanent.Path, out _))
                {
                    var options = new FolderLauncherOptions();
                    options.ItemsToSelect.Add(permanent);

                    await Launcher.LaunchFolderAsync(folder, options);
                }
            }
            catch { }
        }

        private static IAsyncOperation<StorageFolder> GetDownloadsFolderAsync()
        {
            try
            {
                if (ApiInfo.IsStorageSupported)
                {
                    return KnownFolders.GetFolderAsync(KnownFolderId.DownloadsFolder);
                }
                else
                {
                    return KnownFolders.GetFolderForUserAsync(Windows.System.User.GetDefault(), KnownFolderId.DownloadsFolder);
                }
            }
            catch
            {
                return AsyncInfo.Run<StorageFolder>(task => null);
            }
        }

        private static bool IsRelativePath(string relativeTo, string path, out string relative)
        {
            var relativeFull = Path.GetFullPath(relativeTo);
            var pathFull = Path.GetFullPath(path);

            if (pathFull.Length > relativeFull.Length && pathFull[relativeFull.Length] == '\\')
            {
                if (pathFull.StartsWith(relativeFull, StringComparison.OrdinalIgnoreCase))
                {
                    relative = pathFull.Substring(relativeFull.Length + 1);
                    return true;
                }
            }

            relative = null;
            return false;
        }
    }
}
