using System;
using Telegram.Td.Api;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class FileTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch (value)
            {
                case FileTypeNotes notes:
                    return Strings.Resources.LocalAudioCache;
                case FileTypeOther other:
                    return Strings.Resources.MessagesDataUsage;
                case FileTypeTotal total:
                    return Strings.Resources.TotalDataUsage;
                case FileTypeAnimation animation:
                    return Strings.Resources.LocalGifCache;
                case FileTypeAudio audio:
                    return Strings.Resources.LocalMusicCache;
                case FileTypeDocument document:
                    return Strings.Resources.FilesDataUsage;
                case FileTypePhoto photo:
                    return Strings.Resources.LocalPhotoCache;
                case FileTypeVideo video:
                    return Strings.Resources.LocalVideoCache;
                case FileTypeVideoNote videoNote:
                    return Strings.Resources.VideoMessagesAutodownload;
                case FileTypeVoiceNote voiceNote:
                    return Strings.Resources.AudioAutodownload;
                case FileTypeNone none:
                    return "Other";
                case FileTypeProfilePhoto profilePhoto:
                    return "Profile photos";
                case FileTypeSticker sticker:
                    return "Stickers";
                case FileTypeThumbnail thumbnail:
                    return "Thumbnails";
                case FileTypeSecret secret:
                case FileTypeSecretThumbnail secretThumbnail:
                case FileTypeUnknown unknown:
                case FileTypeWallpaper wallpaper:
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
