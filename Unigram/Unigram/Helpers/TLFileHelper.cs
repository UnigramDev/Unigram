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
using Windows.Storage.Pickers;

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
                var resultName = "photo" + BindConvert.Current.DateTime(date).ToString("_yyyyMMdd_HH_mm_ss") + ".jpg";

                if (downloads)
                {
                    var folder = await DownloadsFolder.CreateFolderAsync("Unigram", CreationCollisionOption.OpenIfExists);
                    var result = await FileUtils.GetTempFileAsync(fileName);

                    await result.CopyAsync(folder, resultName, NameCollisionOption.GenerateUniqueName);
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
                var resultName = "document" + BindConvert.Current.DateTime(date).ToString("_yyyyMMdd_HH_mm_ss") + extension;

                var fileNameAttribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                if (fileNameAttribute != null)
                {
                    resultName = fileNameAttribute.FileName;
                }

                if (downloads)
                {
                    var folder = await DownloadsFolder.CreateFolderAsync("Unigram", CreationCollisionOption.OpenIfExists);
                    var result = await FileUtils.GetTempFileAsync(fileName);

                    await result.CopyAsync(folder, resultName, NameCollisionOption.GenerateUniqueName);
                }
                else
                {
                    var picker = new FileSavePicker();
                    picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} File", new[] { document.GetFileExtension() });
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
    }
}
