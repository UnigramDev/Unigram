//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Td.Api;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Path = System.IO.Path;
#if WINUI3
using System.Runtime.InteropServices;
#endif

namespace Telegram.Services
{
    public partial class DownloadFolder
    {
        public string DisplayPath { get; }

        public string Path { get; }

        public bool IsCustom { get; }

        public DownloadFolder(bool custom, FolderItem folder)
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
        Task SaveFileAsAsync(XamlRoot xamlRoot, File file);
        Task SaveFileAsAsync(XamlRoot xamlRoot, File file, long chatId, long messageId);

        Task OpenFileAsync(File file);

        Task OpenFileWithAsync(File file);

        Task CopyFilePathAsync(XamlRoot xamlRoot, File file);

        Task SaveFilesAsync(XamlRoot xamlRoot, IEnumerable<File> files);
        Task SaveFilesAsync(XamlRoot xamlRoot, IEnumerable<(File File, long ChatId, long MessageId)> files);

        Task OpenFolderAsync(File file);

        bool CheckAccessToFolder(File file);

        Task<DownloadFolder> GetDownloadFolderAsync();

        Task<DownloadFolder> SetDownloadFolderAsync(FolderItem folder);

        /// <summary>
        /// The volume the app's own data is on.
        /// </summary>
        Task<DiskSpace> GetDiskSpaceAsync();
    }

    public readonly record struct DiskSpace(ulong FreeSpace, ulong Capacity);

    public partial class StorageService : IStorageService
    {
        private readonly IClientService _clientService;
        private readonly IDownloadFolderService _downloadFolder;

        public StorageService(IClientService clientService, IDownloadFolderService downloadFolder)
        {
            _clientService = clientService;
            _downloadFolder = downloadFolder;
        }

        public async Task SaveFileAsAsync(XamlRoot xamlRoot, File file)
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

                // FileSavePicker doesn't support no exension.
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".dat";
                }

                var displayExtension = extension.TrimStart('.').ToUpper();
                var picker = new FileSavePicker();
                picker.FileTypeChoices.Add($"{displayExtension} File", new[] { extension });
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.SuggestedFileName = text.TextValue;

                var picked = await picker.PickSaveFileAsync(xamlRoot);
                if (picked != null)
                {
                    // Save as copy is never linked back 
                    await cached.CopyAndReplaceAsync(picked);
                    _downloadFolder.MarkFromInternet(picked);
                }
            }
            catch { }
        }

        public async Task SaveFileAsAsync(XamlRoot xamlRoot, File file, long chatId, long messageId)
        {
            // TODO: the current logic doesn't support Save as... before the file is downloaded
            // This is because to download a file to a specific path we have to create a link
            // in advance, while we don't want files saved as to be linked as permanent files.

            // When saving a file as, we always want to retrieve the cached copy
            //var cached = await _clientService.GetFileAsync(file);
            //if (cached == null)
            //{
            //    return;
            //}

            var response = await _clientService.SendAsync(new GetSuggestedFileName(file.Id, string.Empty));
            if (response is not Text text)
            {
                return;
            }

            try
            {
                var extension = Path.GetExtension(text.TextValue);

                // FileSavePicker doesn't support no exension.
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".dat";
                }

                var displayExtension = extension.TrimStart('.').ToUpper();
                var picker = new FileSavePicker();
                picker.FileTypeChoices.Add($"{displayExtension} File", new[] { extension });
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.SuggestedFileName = text.TextValue;

                var destination = await picker.PickSaveFileAsync(xamlRoot);
                if (destination != null)
                {
                    var cached = await _clientService.GetFileAsync(file);
                    if (cached != null)
                    {
                        // Save as copy is never linked back 
                        await cached.CopyAndReplaceAsync(destination);
                        _downloadFolder.MarkFromInternet(destination);
                    }
                    else
                    {
                        _clientService.AddFileToDownloads(file, chatId, messageId, destination);
                    }
                }
            }
            catch { }
        }

        public Task OpenFileAsync(File file)
        {
            return OpenFileAsync(file, false);
        }

        public Task OpenFileWithAsync(File file)
        {
            return OpenFileAsync(file, true);
        }

        private async Task OpenFileAsync(File file, bool displayApplicationPicker)
        {
            // When opening a file, we always want to retrieve the permanent copy
            var permanent = await _clientService.GetPermanentFileAsync(file);
            if (permanent == null)
            {
                return;
            }

            try
            {
                LauncherOptions options = null;
                if (displayApplicationPicker)
                {
                    // Even constructing LauncherOptions may throw HRESULT 0x800706BA
                    options = new LauncherOptions
                    {
                        DisplayApplicationPicker = true
                    };
                }

                var opened = options != null
                    ? await Launcher.LaunchFileAsync(await permanent.ToStorageFileAsync(), options)
                    : await Launcher.LaunchFileAsync(await permanent.ToStorageFileAsync());

                if (opened)
                {
                    return;
                }

                await OpenFolderAsync(permanent);
            }
            catch { }
        }

        public async Task CopyFilePathAsync(XamlRoot xamlRoot, File file)
        {
            var cached = await _clientService.GetPermanentFileAsync(file);
            if (cached == null)
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(cached.Path);
            ClipboardEx.TrySetContent(dataPackage);

            ToastPopup.Show(xamlRoot, Strings.PathCopied, ToastPopupIcon.Copied);
        }

        public async Task SaveFilesAsync(XamlRoot xamlRoot, IEnumerable<File> files)
        {
            try
            {
                var picker = new FolderPicker();

                var folder = await picker.PickSingleFolderAsync(xamlRoot);
                if (folder == null)
                {
                    return;
                }

                var options = new FolderLauncherOptions();

                foreach (var file in files)
                {
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

                    var destination = await cached.CopyAsync(folder, text.TextValue, NameCollisionOption.GenerateUniqueName);
                    _downloadFolder.MarkFromInternet(destination);
                    options.ItemsToSelect.Add(await destination.ToStorageFileAsync());
                }

                await Launcher.LaunchFolderAsync(await folder.ToStorageFolderAsync(), options);
            }
            catch { }
        }

        public async Task SaveFilesAsync(XamlRoot xamlRoot, IEnumerable<(File File, long ChatId, long MessageId)> files)
        {
            try
            {
                var picker = new FolderPicker();

                var folder = await picker.PickSingleFolderAsync(xamlRoot);
                if (folder == null)
                {
                    return;
                }

                var options = new FolderLauncherOptions();

                foreach (var file in files)
                {
                    // When saving a file as, we always want to retrieve the cached copy
                    var response = await _clientService.SendAsync(new GetSuggestedFileName(file.File.Id, string.Empty));
                    if (response is not Text text)
                    {
                        return;
                    }

                    var cached = await _clientService.GetFileAsync(file.File);
                    if (cached != null)
                    {
                        var destination = await cached.CopyAsync(folder, text.TextValue, NameCollisionOption.GenerateUniqueName);
                        _downloadFolder.MarkFromInternet(destination);
                        options.ItemsToSelect.Add(await destination.ToStorageFileAsync());
                    }
                    else
                    {
                        var destination = await folder.CreateFileAsync(text.TextValue, CreationCollisionOption.GenerateUniqueName);
                        _clientService.AddFileToDownloads(file.File, file.ChatId, file.MessageId, destination);
                    }
                }

                await Launcher.LaunchFolderAsync(await folder.ToStorageFolderAsync(), options);
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

        private async Task OpenFolderAsync(FileItem permanent)
        {
            try
            {
                var folder = await permanent.GetParentAsync();
                folder ??= await _downloadFolder.GetDefaultFolderAsync();

                if (folder != null && Extensions.IsRelativePath(folder.Path, permanent.Path, out _))
                {
                    var options = new FolderLauncherOptions();
                    options.ItemsToSelect.Add(await permanent.ToStorageFileAsync());

                    await Launcher.LaunchFolderAsync(await folder.ToStorageFolderAsync(), options);
                }
            }
            catch { }
        }

        public bool CheckAccessToFolder(File file)
        {
            if (file != null && file.Local.IsDownloadingCompleted)
            {
                // Either the platform can open the default download folder on its own, or the
                // user picked one and we still hold a grant for it.
                return ApiInfo.HasKnownFolders || _downloadFolder.HasCustomFolder;
            }

            return false;
        }

        public Task<DownloadFolder> GetDownloadFolderAsync()
        {
            return _downloadFolder.GetFolderAsync();
        }

        public Task<DownloadFolder> SetDownloadFolderAsync(FolderItem folder)
        {
            return _downloadFolder.SetFolderAsync(folder);
        }

#if WINUI3
        public Task<DiskSpace> GetDiskSpaceAsync()
        {
            // Free space available to this user, so a disk quota counts against it.
            if (GetDiskFreeSpaceExW(AppPaths.LocalPath, out ulong available, out ulong total, out _))
            {
                return Task.FromResult(new DiskSpace(available, total));
            }

            throw new System.ComponentModel.Win32Exception();
        }

        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetDiskFreeSpaceExW(string lpDirectoryName, out ulong lpFreeBytesAvailableToCaller, out ulong lpTotalNumberOfBytes, out ulong lpTotalNumberOfFreeBytes);
#else
        public async Task<DiskSpace> GetDiskSpaceAsync()
        {
            const string FreeSpace = "System.FreeSpace";
            const string Capacity = "System.Capacity";

            var folder = await AppPaths.GetLocalFolderAsync();
            var properties = await folder.Properties.RetrievePropertiesAsync(new[] { FreeSpace, Capacity });

            return new DiskSpace((ulong)properties[FreeSpace], (ulong)properties[Capacity]);
        }
#endif
    }
}
