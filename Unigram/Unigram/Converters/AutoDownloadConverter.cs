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
                text += "Photos";
            }

            if (flags.HasFlag(AutoDownloadType.Audio))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }

                text += "Voice messages";
            }

            if (flags.HasFlag(AutoDownloadType.Round))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }

                text += "Video messages";
            }

            if (flags.HasFlag(AutoDownloadType.Video))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }

                text += "Videos";
            }

            if (flags.HasFlag(AutoDownloadType.Document))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }
                text += "Files";
            }

            if (flags.HasFlag(AutoDownloadType.Music))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }
                text += "Music";
            }

            if (flags.HasFlag(AutoDownloadType.GIF))
            {
                if (text.Length > 0)
                {
                    text += ", ";
                }
                text += "GIFs";
            }

            if (string.IsNullOrEmpty(text))
            {
                text = "No media";
            }

            return text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
