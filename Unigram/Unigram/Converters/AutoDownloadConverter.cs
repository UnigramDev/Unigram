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
            var flags = (AutoDownloadType)value;
            if (flags == 0)
            {
                return Strings.Android.NoMediaAutoDownload;
            }

            var text = string.Empty;
            text = AppendFlag(flags, AutoDownloadType.Photo, text, Strings.Android.LocalPhotoCache);
            text = AppendFlag(flags, AutoDownloadType.Audio, text, Strings.Android.AudioAutodownload);
            text = AppendFlag(flags, AutoDownloadType.Round, text, Strings.Android.VideoMessagesAutodownload);
            text = AppendFlag(flags, AutoDownloadType.Video, text, Strings.Android.LocalVideoCache);
            text = AppendFlag(flags, AutoDownloadType.Document, text, Strings.Android.FilesDataUsage);
            text = AppendFlag(flags, AutoDownloadType.Music, text, Strings.Android.AttachMusic);
            text = AppendFlag(flags, AutoDownloadType.GIF, text, Strings.Android.LocalGifCache);

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
