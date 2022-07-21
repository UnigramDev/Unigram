using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.Storage.Pickers;
using Windows.System;
using Path = System.IO.Path;

namespace Unigram.Services
{
    public interface IStorageService
    {
        Task SaveAsAsync(File file);

        Task OpenWithAsync(File file);

        Task OpenFolderAsync(File file);
    }

    public class StorageService : IStorageService
    {
        private readonly IProtoService _protoService;

        public StorageService(IProtoService protoService)
        {
            _protoService = protoService;
        }

        public async Task SaveAsAsync(File file)
        {
            if (file == null || !file.Local.IsFileExisting())
            {
                return;
            }

            var cached = await _protoService.GetFileAsync(file);
            if (cached == null)
            {
                return;
            }

            var response = await _protoService.SendAsync(new GetSuggestedFileName(file.Id, string.Empty));
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
            if (file == null || !file.Local.IsFileExisting())
            {
                return;
            }

            var cached = await _protoService.GetFileAsync(file);
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
            if (file == null || !file.Local.IsFileExisting())
            {
                return;
            }

            var cached = await _protoService.GetFileAsync(file);
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
