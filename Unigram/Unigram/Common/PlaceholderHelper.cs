using RLottie;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Xml.Linq;
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

        public static SolidColorBrush GetBrush(long i)
        {
            return new SolidColorBrush(_colors[Math.Abs(i % _colors.Length)]);
        }

        public static ImageSource GetIdenticon(IList<byte> hash, int side)
        {
            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                PlaceholderImageHelper.Current.DrawIdenticon(hash, side, stream);
                try
                {
                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetChat(Chat chat, int side)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        PlaceholderImageHelper.Current.DrawProfilePlaceholder(_colors[Math.Abs(privata.UserId % _colors.Length)], InitialNameStringConverter.Convert(chat), stream);
                    }
                    else if (chat.Type is ChatTypeSecret secret)
                    {
                        PlaceholderImageHelper.Current.DrawProfilePlaceholder(_colors[Math.Abs(secret.UserId % _colors.Length)], InitialNameStringConverter.Convert(chat), stream);
                    }
                    else if (chat.Type is ChatTypeBasicGroup basic)
                    {
                        PlaceholderImageHelper.Current.DrawProfilePlaceholder(_colors[Math.Abs(basic.BasicGroupId % _colors.Length)], InitialNameStringConverter.Convert(chat), stream);
                    }
                    else if (chat.Type is ChatTypeSupergroup super)
                    {
                        PlaceholderImageHelper.Current.DrawProfilePlaceholder(_colors[Math.Abs(super.SupergroupId % _colors.Length)], InitialNameStringConverter.Convert(chat), stream);
                    }

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetChat(ChatInviteLinkInfo chat, int side)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawProfilePlaceholder(_colors[0], InitialNameStringConverter.Convert(chat), stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetUser(User user, int side)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawProfilePlaceholder(_colors[Math.Abs(user.Id % _colors.Length)], InitialNameStringConverter.Convert(user), stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetNameForUser(string firstName, string lastName, int side, int color = 5)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawProfilePlaceholder(_colors[Math.Abs(color % _colors.Length)], InitialNameStringConverter.Convert(firstName, lastName), stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetNameForUser(string name, int side, int color = 5)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawProfilePlaceholder(_colors[Math.Abs(color % _colors.Length)], InitialNameStringConverter.Convert((object)name), stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetNameForChat(string title, int side, int color = 5)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawProfilePlaceholder(_colors[Math.Abs(color % _colors.Length)], InitialNameStringConverter.Convert(title), stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetSavedMessages(int id, int side)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawSavedMessages(_colors[Math.Abs(id % _colors.Length)], stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetDeletedUser(User user, int side)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawDeletedUser(_colors[Math.Abs(user.Id % _colors.Length)], stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetGlyph(string glyph, int id, int side)
        {
            var bitmap = new BitmapImage { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawGlyph(glyph, _colors[Math.Abs(id % _colors.Length)], stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetChat(IProtoService protoService, Chat chat, int side)
        {
            if (protoService != null)
            {
                if (chat.Type is ChatTypePrivate privata && protoService.IsSavedMessages(chat))
                {
                    return GetSavedMessages(privata.UserId, side);
                }
                else if (protoService.IsRepliesChat(chat))
                {
                    return GetGlyph(Icons.Reply, 5, side);
                }
            }

            var file = chat.Photo?.Small;
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    protoService?.DownloadFile(file.Id, 1);
                }
            }
            else if (protoService.TryGetUser(chat, out User user) && user.Type is UserTypeDeleted)
            {
                return GetDeletedUser(user, side);
            }
            else
            {

                return GetChat(chat, side);
            }

            return null;
        }

        public static ImageSource GetChat(IProtoService protoService, ChatInviteLinkInfo chat, int side)
        {
            var file = chat.Photo?.Small;
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    protoService?.DownloadFile(file.Id, 1);
                }
            }
            else
            {
                return GetChat(chat, side);
            }

            return null;
        }

        public static ImageSource GetUser(IProtoService protoService, User user, int side)
        {
            var file = user.ProfilePhoto?.Small;
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = side, DecodePixelHeight = side, DecodePixelType = DecodePixelType.Logical };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    protoService?.DownloadFile(file.Id, 1);
                }
            }
            else if (user.Type is UserTypeDeleted)
            {
                return GetDeletedUser(user, side);
            }
            else
            {
                return GetUser(user, side);
            }

            return null;
        }

        public static ImageSource GetBitmap(IProtoService protoService, PhotoSize photoSize)
        {
            return GetBitmap(protoService, photoSize.Photo, photoSize.Width, photoSize.Height);
        }

        public static ImageSource GetBitmap(IProtoService protoService, File file, int width, int height)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                protoService.DownloadFile(file.Id, 1);
            }

            return null;
        }

        public static ImageSource GetVector(IProtoService protoService, File file, Color foreground)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                var text = GetSvgXml(file);

                var bitmap = new BitmapImage();
                using (var stream = new InMemoryRandomAccessStream())
                {
                    try
                    {
                        PlaceholderImageHelper.Current.DrawSvg(text, foreground, stream);

                        bitmap.SetSource(stream);
                    }
                    catch { }
                }

                return bitmap;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                protoService.DownloadFile(file.Id, 1);
            }

            return null;
        }

        public static LoadedImageSurface GetVectorSurface(IProtoService protoService, File file, Color foreground)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                var text = GetSvgXml(file);

                var bitmap = default(LoadedImageSurface);
                using (var stream = new InMemoryRandomAccessStream())
                {
                    try
                    {
                        var size = PlaceholderImageHelper.Current.DrawSvg(text, foreground, stream);
                        bitmap = LoadedImageSurface.StartLoadFromStream(stream, new Windows.Foundation.Size(size.Width / 3, size.Height / 3));
                    }
                    catch { }
                }

                return bitmap;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && protoService != null)
            {
                protoService.DownloadFile(file.Id, 1);
            }

            return null;
        }

        private static string GetSvgXml(File file)
        {
            var styles = new Dictionary<string, string>();
            var text = string.Empty;

            using (var source = System.IO.File.OpenRead(file.Local.Path))
            using (var decompress = new GZipStream(source, CompressionMode.Decompress))
            using (var reader = new System.IO.StreamReader(decompress))
            {
                text = reader.ReadToEnd();
            }

            var document = XDocument.Parse(text);
            var svg = XNamespace.Get("http://www.w3.org/2000/svg");

            foreach (var styleNode in document.Root.Descendants(svg + "style"))
            {
                var currentStyleString = styleNode.Value;
                int currentClassNameStartIndex = -1;
                int currentClassContentsStartIndex = -1;

                string currentClassName = null;

                for (int i = 0; i < currentStyleString.Length; i++)
                {
                    var c = currentStyleString[i];
                    if (currentClassNameStartIndex != -1)
                    {
                        if (!char.IsLetterOrDigit(c))
                        {
                            currentClassName = currentStyleString.Substring(currentClassNameStartIndex, i - currentClassNameStartIndex);
                            currentClassNameStartIndex = -1;
                        }
                    }
                    else if (currentClassContentsStartIndex != -1)
                    {
                        if (c == '}')
                        {
                            var classContents = currentStyleString.Substring(currentClassContentsStartIndex, i - currentClassContentsStartIndex);
                            if (currentClassName != null && classContents != null)
                            {
                                styles[currentClassName] = classContents;
                                currentClassName = null;
                            }
                            currentClassContentsStartIndex = -1;
                        }
                    }

                    if (currentClassNameStartIndex == -1 && currentClassContentsStartIndex == -1)
                    {
                        if (c == '.')
                        {
                            currentClassNameStartIndex = i + 1;
                        }
                        else if (c == '{')
                        {
                            currentClassContentsStartIndex = i + 1;
                        }
                    }
                }

            }

            foreach (var styleName in styles)
            {
                text = text.Replace($"class=\"{styleName.Key}\"", $"style=\"{styleName.Value}\"");
            }

            return text;
        }

        public static ImageSource GetBlurred(string path, float amount = 3)
        {
            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawThumbnailPlaceholder(path, amount, stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetQr(string data, Color foreground, Color background)
        {
            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    PlaceholderImageHelper.Current.DrawQr(data, foreground, background, stream);

                    bitmap.SetSource(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetWebPFrame(string path)
        {
            //return null;
            //return new BitmapImage(new Uri("file:///" + path));
            return WebPImage.DecodeFromPath(path);
        }

        public static ImageSource GetLottieFrame(string path, int frame, int width, int height, bool webp = true)
        {
            var animation = LottieAnimation.LoadFromFile(path, false, null);
            if (animation == null)
            {
                if (webp)
                {
                    return GetWebPFrame(path);
                }

                return null;
            }

            var bitmap = new WriteableBitmap(width, height);
            animation.RenderSync(bitmap, frame);
            animation.Dispose();

            return bitmap;
        }
    }
}
