using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Strings;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class AutoDownloadConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var text = string.Empty;
            var flags = (AutoDownloadType)value;

            text = AppendFlag(flags, AutoDownloadType.Photo, text, AppResources.AutoDownload_Photo);
            text = AppendFlag(flags, AutoDownloadType.Audio, text, AppResources.AutoDownload_Audio);
            text = AppendFlag(flags, AutoDownloadType.Round, text, AppResources.AutoDownload_Round);
            text = AppendFlag(flags, AutoDownloadType.Video, text, AppResources.AutoDownload_Video);
            text = AppendFlag(flags, AutoDownloadType.Document, text, AppResources.AutoDownload_Document);
            text = AppendFlag(flags, AutoDownloadType.GIF, text, AppResources.AutoDownload_GIF);
            text = AppendFlag(flags, AutoDownloadType.Photo, text, AppResources.AutoDownload_Photo);
            text = AppendFlag(flags, AutoDownloadType.Photo, text, AppResources.AutoDownload_Photo);

            if (string.IsNullOrEmpty(text))
            {
                text = AppResources.AutoDownload_None;
            }

            return text;
        }

        private string AppendFlag(AutoDownloadType flags, AutoDownloadType value, string text, string label)
        {
            if (flags.HasFlag(value))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }

                text += label;
            }

            return text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
