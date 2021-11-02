using Microsoft.UI.Xaml.Data;
using System;
using Telegram.Td.Api;
using Unigram.ViewModels.Settings;

namespace Unigram.Converters
{
    public class FileTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch (value)
            {
                case FileTypeNotes:
                    return Strings.Resources.LocalAudioCache;
                case FileTypeOther:
                    return Strings.Resources.MessagesDataUsage;
                case FileTypeTotal:
                    return Strings.Resources.TotalDataUsage;
                case FileTypeAnimation:
                    return Strings.Resources.LocalGifCache;
                case FileTypeAudio:
                    return Strings.Resources.LocalMusicCache;
                case FileTypeDocument:
                    return Strings.Resources.FilesDataUsage;
                case FileTypePhoto:
                    return Strings.Resources.LocalPhotoCache;
                case FileTypeVideo:
                    return Strings.Resources.LocalVideoCache;
                case FileTypeVideoNote:
                    return Strings.Resources.VideoMessagesAutodownload;
                case FileTypeVoiceNote:
                    return Strings.Resources.AudioAutodownload;
                case FileTypeNone:
                    return "Other";
                case FileTypeProfilePhoto:
                    return "Profile photos";
                case FileTypeSticker:
                    return "Stickers";
                case FileTypeThumbnail:
                    return "Thumbnails";
                case FileTypeSecret:
                case FileTypeSecretThumbnail:
                case FileTypeUnknown:
                case FileTypeWallpaper:
                default:
                    return value?.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
