using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.Native;
using Unigram.Services;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Common
{
    public static class PlaceholderHelper
    {
        private static Color[] _colors = new Color[7]
        {
            Color.FromArgb(0xFF, 0xE5, 0x65, 0x55),
            Color.FromArgb(0xFF, 0xF2, 0x8C, 0x48),
            Color.FromArgb(0xFF, 0x8E, 0x85, 0xEE),
            Color.FromArgb(0xFF, 0x76, 0xC8, 0x4D),
            Color.FromArgb(0xFF, 0x5F, 0xBE, 0xD5),
            Color.FromArgb(0xFF, 0x54, 0x9C, 0xDD),
            Color.FromArgb(0xFF, 0xF2, 0x74, 0x9A),
        };

        public static SolidColorBrush GetBrush(int i)
        {
            return new SolidColorBrush(_colors[Math.Abs(i % _colors.Length)]);
        }

        public static ImageSource GetIdenticon(IList<byte> hash)
        {
            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                PlaceholderImageHelper.GetForCurrentView().DrawIdenticon(hash, stream);
                try
                {
                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetChat(Chat chat, int width, int height)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        PlaceholderImageHelper.GetForCurrentView().DrawProfilePlaceholder(_colors[Math.Abs(privata.UserId % _colors.Length)], InitialNameStringConverter.Convert(chat), stream);
                    }
                    else if (chat.Type is ChatTypeSecret secret)
                    {
                        PlaceholderImageHelper.GetForCurrentView().DrawProfilePlaceholder(_colors[Math.Abs(secret.UserId % _colors.Length)], InitialNameStringConverter.Convert(chat), stream);
                    }
                    else if (chat.Type is ChatTypeBasicGroup basic)
                    {
                        PlaceholderImageHelper.GetForCurrentView().DrawProfilePlaceholder(_colors[Math.Abs(basic.BasicGroupId % _colors.Length)], InitialNameStringConverter.Convert(chat), stream);
                    }
                    else if (chat.Type is ChatTypeSupergroup super)
                    {
                        PlaceholderImageHelper.GetForCurrentView().DrawProfilePlaceholder(_colors[Math.Abs(super.SupergroupId % _colors.Length)], InitialNameStringConverter.Convert(chat), stream);
                    }

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetChat(ChatInviteLinkInfo chat, int width, int height)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.GetForCurrentView().DrawProfilePlaceholder(_colors[0], InitialNameStringConverter.Convert(chat), stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetUser(User user, int width, int height)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.GetForCurrentView().DrawProfilePlaceholder(_colors[Math.Abs(user.Id % _colors.Length)], InitialNameStringConverter.Convert(user), stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetChat(IProtoService protoService, Chat chat, int width, int height)
        {
            var file = chat.Photo?.Small;
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    protoService?.Send(new DownloadFile(file.Id, 1));
                }
            }
            else
            {
                return GetChat(chat, width, height);
            }

            return null;
        }

        public static ImageSource GetChat(IProtoService protoService, ChatInviteLinkInfo chat, int width, int height)
        {
            var file = chat.Photo?.Small;
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    protoService?.Send(new DownloadFile(file.Id, 1));
                }
            }
            else
            {
                return GetChat(chat, width, height);
            }

            return null;
        }

        public static ImageSource GetUser(IProtoService protoService, User user, int width, int height)
        {
            var file = user.ProfilePhoto?.Small;
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    protoService?.Send(new DownloadFile(file.Id, 1));
                }
            }
            else
            {
                return GetUser(user, width, height);
            }

            return null;
        }

        public static ImageSource GetBitmap(IProtoService protoService, File file, int width, int height)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                protoService.Send(new DownloadFile(file.Id, 1));
            }

            return null;
        }

        public static ImageSource GetBadge(string text, Color color, int width, int height)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.GetForCurrentView().DrawProfilePlaceholder(color, InitialNameStringConverter.Convert(text), stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetBlurred(string path)
        {
            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.GetForCurrentView().DrawThumbnailPlaceholder(path, 3, stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }
    }
}
