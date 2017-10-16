using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class AutoDownloadConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var text = string.Empty;
            var flags = (AutoDownloadType)value;
            if (flags.HasFlag(AutoDownloadType.Photo))
            {
                text += Strings.AppResources.AutoDownload_Photo;
            }

            if (flags.HasFlag(AutoDownloadType.Audio))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }

                text += Strings.AppResources.AutoDownload_Audio;
            }

            if (flags.HasFlag(AutoDownloadType.Round))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }

                text += Strings.AppResources.AutoDownload_Round;
            }

            if (flags.HasFlag(AutoDownloadType.Video))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }

                text += Strings.AppResources.AutoDownload_Video;
            }

            if (flags.HasFlag(AutoDownloadType.Document))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }
                text += Strings.AppResources.AutoDownload_Document;
            }

            if (flags.HasFlag(AutoDownloadType.Music))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }
                text += Strings.AppResources.AutoDownload_Music;
            }

            if (flags.HasFlag(AutoDownloadType.GIF))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }
                text += Strings.AppResources.AutoDownload_GIF;
            }

            if (string.IsNullOrEmpty(text))
            {
                text = Strings.AppResources.AutoDownload_None;
            }

            return text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
