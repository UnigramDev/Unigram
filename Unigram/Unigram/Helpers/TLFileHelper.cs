using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Converters;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Unigram.Helpers
{
    public static class TLFileHelper
    {
        public static async Task SavePhotoAsync(TLPhotoSize photoSize, int date, bool downloads)
        {
            var location = photoSize.Location;
            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {
                var resultName = "photo_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".jpg";

                if (downloads)
                {
                    var folder = await GetDownloadsAsync();
                    if (folder == null)
                    {
                        return;
                    }

                    StorageFile file;
                    if (StorageApplicationPermissions.FutureAccessList.ContainsItem(fileName))
                    {
                        file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(fileName);
                    }
                    else
                    {
                        file = await folder.CreateFileAsync(resultName, CreationCollisionOption.GenerateUniqueName);
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace(fileName, file);

                        var result = await FileUtils.GetTempFileAsync(fileName);
                        await result.CopyAndReplaceAsync(file);
                    }

                    if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                    {
                        var options = new FolderLauncherOptions();
                        options.ItemsToSelect.Add(file);

                        await Launcher.LaunchFolderAsync(folder, options);
                    }
                }
                else
                {
                    var picker = new FileSavePicker();
                    picker.FileTypeChoices.Add("JPEG Image", new[] { ".jpg" });
                    picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    picker.SuggestedFileName = resultName;

                    var file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        var result = await FileUtils.GetTempFileAsync(fileName);
                        await result.CopyAndReplaceAsync(file);
                    }
                }
            }
        }

        public static async Task SaveDocumentAsync(TLDocument document, int date, bool downloads)
        {
            var fileName = document.GetFileName();
            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {

                var extension = document.GetFileExtension();
                var resultName = "document_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + extension;

                var fileNameAttribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                if (fileNameAttribute != null)
                {
                    resultName = fileNameAttribute.FileName;
                }

                if (downloads)
                {
                    var folder = await GetDownloadsAsync();
                    if (folder == null)
                    {
                        return;
                    }

                    StorageFile file;
                    if (StorageApplicationPermissions.FutureAccessList.ContainsItem(fileName))
                    {
                        file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(fileName);
                    }
                    else
                    {
                        file = await folder.CreateFileAsync(resultName, CreationCollisionOption.GenerateUniqueName);
                        StorageApplicationPermissions.FutureAccessList.AddOrReplace(fileName, file);

                        var result = await FileUtils.GetTempFileAsync(fileName);
                        await result.CopyAndReplaceAsync(file);
                    }

                    if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                    {
                        var options = new FolderLauncherOptions();
                        options.ItemsToSelect.Add(file);

                        await Launcher.LaunchFolderAsync(folder, options);
                    }
                }
                else
                {
                    var picker = new FileSavePicker();

                    if (!string.IsNullOrEmpty(extension))
                    {
                        picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} File", new[] { extension });
                    }
                    else
                    {
                        picker.FileTypeChoices.Add("Unknown", new[] { ".dat" });
                    }

                    picker.SuggestedStartLocation = PickerLocationId.Downloads;
                    picker.SuggestedFileName = resultName;

                    var file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        var result = await FileUtils.GetTempFileAsync(fileName);
                        await result.CopyAndReplaceAsync(file);
                    }
                }
            }
        }

        private static async Task<StorageFolder> GetDownloadsAsync()
        {
            StorageFolder folder;
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem("Downloads"))
            {
                folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("Downloads");
            }
            else
            {
                var picker = new FolderPicker();
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.FileTypeFilter.Add("*");

                var picked = await picker.PickSingleFolderAsync();
                if (picked == null)
                {
                    return null;
                }

                folder = picked;
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("Downloads", folder);
            }

            return folder;
        }
    }
}
