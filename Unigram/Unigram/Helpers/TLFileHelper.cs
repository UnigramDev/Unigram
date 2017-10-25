using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Converters;
using Windows.Storage.Pickers;

namespace Unigram.Helpers
{
    public static class TLFileHelper
    {
        public static async Task SavePhotoAsync(TLPhotoSize photoSize, int date)
        {
            var location = photoSize.Location;
            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {
                var picker = new FileSavePicker();
                picker.FileTypeChoices.Add("JPEG Image", new[] { ".jpg" });
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.SuggestedFileName = BindConvert.Current.DateTime(date).ToString("photo_yyyyMMdd_HH_mm_ss") + ".jpg";

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    var result = await FileUtils.GetTempFileAsync(fileName);
                    await result.CopyAndReplaceAsync(file);
                }
            }
        }

        public static async Task SaveDocumentAsync(TLDocument document, int date)
        {
            var fileName = document.GetFileName();
            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {
                var extension = document.GetFileExtension();

                var picker = new FileSavePicker();
                picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} File", new[] { document.GetFileExtension() });
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.SuggestedFileName = BindConvert.Current.DateTime(date).ToString("photo_yyyyMMdd_HH_mm_ss") + extension;

                var fileNameAttribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                if (fileNameAttribute != null)
                {
                    picker.SuggestedFileName = fileNameAttribute.FileName;
                }

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
