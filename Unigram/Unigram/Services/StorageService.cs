//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Entities;
using Unigram.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;
using Path = System.IO.Path;

namespace Unigram.Services
{
    public interface IStorageService
    {
        Task<StorageMedia> PickSingleMediaAsync(XamlRoot xamlRoot, PickerLocationId location, params string[] filters);
        Task<StorageFile> PickSingleFileAsync(XamlRoot xamlRoot, PickerLocationId location, params string[] filters);
        Task<IReadOnlyList<StorageFile>> PickMultipleFilesAsync(XamlRoot xamlRoot, PickerLocationId location, params string[] filters);

        Task SaveAsAsync(File file);

        Task OpenWithAsync(File file);

        Task OpenFolderAsync(File file);
    }

    public class StorageService : IStorageService
    {
        private readonly IClientService _clientService;

        public StorageService(IClientService clientService)
        {
            _clientService = clientService;
        }

        public async Task<StorageMedia> PickSingleMediaAsync(XamlRoot xamlRoot, PickerLocationId location, params string[] filters)
        {
            var file = await PickSingleFileAsync(xamlRoot, location, filters);
            if (file == null)
            {
                return null;
            }

            return await StorageMedia.CreateAsync(file);
        }

        public async Task<StorageFile> PickSingleFileAsync(XamlRoot xamlRoot, PickerLocationId location, params string[] filters)
        {
            try
            {
                var picker = new FileOpenPicker();
                var handle = WindowNative.GetWindowHandle(xamlRoot);

                InitializeWithWindow.Initialize(picker, handle);

                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = location;
                picker.FileTypeFilter.AddRange(filters);

                return await picker.PickSingleFileAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<IReadOnlyList<StorageFile>> PickMultipleFilesAsync(XamlRoot xamlRoot, PickerLocationId location, params string[] filters)
        {
            try
            {
                var picker = new FileOpenPicker();

                var window = WindowContext.ForXamlRoot(xamlRoot);
                var handle = WindowNative.GetWindowHandle(window.Window);

                InitializeWithWindow.Initialize(picker, handle);

                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = location;
                picker.FileTypeFilter.AddRange(filters);

                return await picker.PickMultipleFilesAsync();
            }
            catch
            {
                return Array.Empty<StorageFile>();
            }
        }

        public async Task SaveAsAsync(File file)
        {
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var cached = await _clientService.GetFileAsync(file);
            if (cached == null)
            {
                return;
            }

            var response = await _clientService.SendAsync(new GetSuggestedFileName(file.Id, string.Empty));
            if (response is Text text)
            {
                var extension = Path.GetExtension(text.TextValue);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".dat";
                }

                var displayExtension = extension.TrimStart('.').ToUpper();

                try
                {
                    var picker = new FileSavePicker();
                    picker.FileTypeChoices.Add($"{displayExtension} File", new[] { extension });
                    picker.SuggestedStartLocation = PickerLocationId.Downloads;
                    picker.SuggestedFileName = text.TextValue;

                    var picked = await picker.PickSaveFileAsync();
                    if (picked != null)
                    {
                        await cached.CopyAndReplaceAsync(picked);
                    }
                }
                catch { }
            }
        }

        public async Task OpenWithAsync(File file)
        {
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var cached = await _clientService.GetFileAsync(file);
            if (cached == null)
            {
                return;
            }

            try
            {
                var options = new LauncherOptions();
                options.DisplayApplicationPicker = true;

                await Launcher.LaunchFileAsync(cached, options);
            }
            catch { }
        }

        public async Task OpenFolderAsync(File file)
        {
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var cached = await _clientService.GetFileAsync(file);
            if (cached == null)
            {
                return;
            }

            try
            {
                var folder = await cached.GetParentAsync();

                var options = new FolderLauncherOptions();
                options.ItemsToSelect.Add(cached);

                await Launcher.LaunchFolderAsync(folder, options);
            }
            catch { }
        }
    }
}
