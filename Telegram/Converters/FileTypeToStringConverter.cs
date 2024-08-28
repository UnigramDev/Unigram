//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Data;
using System;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;

namespace Telegram.Converters
{
    public partial class FileTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch (value)
            {
                case FileTypeNotes:
                    return Strings.LocalAudioCache;
                case FileTypeOther:
                    return Strings.MessagesDataUsage;
                case FileTypeTotal:
                    return Strings.TotalDataUsage;
                case FileTypeAnimation:
                    return Strings.LocalGifCache;
                case FileTypeAudio:
                    return Strings.LocalMusicCache;
                case FileTypeDocument:
                    return Strings.FilesDataUsage;
                case FileTypePhoto:
                    return Strings.LocalPhotoCache;
                case FileTypeVideo:
                    return Strings.LocalVideoCache;
                case FileTypeVideoNote:
                    return Strings.VideoMessagesAutodownload;
                case FileTypeVoiceNote:
                    return Strings.AudioAutodownload;
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
