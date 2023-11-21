//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using RLottie;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Common
{
    public static class PlaceholderHelper
    {
        public static ImageSource GetBitmap(IClientService clientService, PhotoSize photoSize)
        {
            return GetBitmap(clientService, photoSize.Photo, photoSize.Width, photoSize.Height);
        }

        public static ImageSource GetBitmap(IClientService clientService, File file, int width, int height)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                return UriEx.ToBitmap(file.Local.Path, width, height);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && clientService != null)
            {
                clientService.DownloadFile(file.Id, 1);
            }

            return null;
        }

        public static async Task<LoadedImageSurface> LoadBitmapAsync(File file)
        {
            try
            {
                var item = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                using (var stream = await item.OpenReadAsync())
                {
                    return LoadedImageSurface.StartLoadFromStream(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        private static readonly DisposableMutex _patternSurfaceLock = new();

        public static async Task<LoadedImageSurface> LoadPatternBitmapAsync(File file, double rasterizationScale)
        {
            using var locked = await _patternSurfaceLock.WaitAsync();

            var bitmap = default(LoadedImageSurface);
            var scale = (int)(rasterizationScale * 100);

            rasterizationScale = 0.25 * rasterizationScale;

            var cache = $"{file.Remote.UniqueId}.scale-{scale}.png";
            var relative = System.IO.Path.Combine("Wallpapers", cache);

            var delete = false;

            var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(relative) as StorageFile;
            if (item == null)
            {
                try
                {
                    item = await ApplicationData.Current.LocalFolder.CreateFileAsync(relative, CreationCollisionOption.ReplaceExisting);

                    using (var stream = await item.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var text = await ProcessSvgXmlAsync(file.Local.Path);
                        await PlaceholderImageHelper.Current.DrawSvgAsync(text, Colors.White, stream, rasterizationScale);

                        bitmap = LoadedImageSurface.StartLoadFromStream(stream, new Size(360, 740));
                    }
                }
                catch
                {
                    delete = true;
                }
            }
            else
            {
                try
                {
                    using (var stream = await item.OpenReadAsync())
                    {
                        bitmap = LoadedImageSurface.StartLoadFromStream(stream, new Size(360, 740));
                    }
                }
                catch
                {
                    delete = true;
                }
            }

            if (item != null && delete)
            {
                try
                {
                    await item.DeleteAsync();
                }
                catch
                {
                    // Shit happens
                }
            }

            return bitmap;
        }

        private static async Task<string> ProcessSvgXmlAsync(string filePath)
        {
            var styles = new Dictionary<string, string>();
            var text = string.Empty;

            using (var source = System.IO.File.OpenRead(filePath))
            using (var decompress = new GZipStream(source, CompressionMode.Decompress))
            using (var reader = new System.IO.StreamReader(decompress))
            {
                text = await reader.ReadToEndAsync();
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

        public static async Task<ImageSource> GetBlurredAsync(string path, float amount = 3)
        {
            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    await Task.Run(() => PlaceholderImageHelper.Current.DrawThumbnailPlaceholder(path, amount, stream));
                    await bitmap.SetSourceAsync(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static async void GetBlurred(BitmapImage bitmap, IList<byte> bytes, float amount = 3)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    await Task.Run(() => PlaceholderImageHelper.Current.DrawThumbnailPlaceholder(bytes, amount, stream));
                    await bitmap.SetSourceAsync(stream);
                }
                catch { }
            }
        }

        public static async Task<ImageSource> GetBlurredAsync(IList<byte> bytes, float amount = 3)
        {
            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    await Task.Run(() => PlaceholderImageHelper.Current.DrawThumbnailPlaceholder(bytes, amount, stream));
                    await bitmap.SetSourceAsync(stream);
                }
                catch { }
            }

            return bitmap;
        }

        public static ImageSource GetWebPFrame(string path, double maxWidth = 512)
        {
            try
            {
                var buffer = PlaceholderImageHelper.DrawWebP(path, (int)maxWidth, out Size size);
                if (size.Width > 0 && size.Height > 0)
                {
                    var bitmap = new WriteableBitmap((int)size.Width, (int)size.Height);
                    BufferSurface.Copy(buffer, bitmap.PixelBuffer);

                    return bitmap;
                }
                else
                {
                    return new BitmapImage(UriEx.ToLocal(path));
                }
            }
            catch { }

            return null;
        }

        public static async Task<ImageSource> GetWebPFrameAsync(string path, double maxWidth = 512)
        {
            try
            {
                maxWidth = maxWidth < 512 ? maxWidth * WindowContext.Current.RasterizationScale : maxWidth;
                maxWidth = Math.Min(maxWidth, 512);

                Size size;

                var buffer = await Task.Run(() => PlaceholderImageHelper.DrawWebP(path, (int)maxWidth, out size));
                if (size.Width > 0 && size.Height > 0)
                {
                    var bitmap = new WriteableBitmap((int)size.Width, (int)size.Height);
                    BufferSurface.Copy(buffer, bitmap.PixelBuffer);

                    return bitmap;
                }
                else
                {
                    return new BitmapImage(UriEx.ToLocal(path));
                }
            }
            catch { }

            return null;
        }

        public static ImageSource GetLottieFrame(string path, int frame, int width, int height, bool webp = true)
        {
            // Frame size affects disk cache, so we always use 256.
            var animation = LottieAnimation.LoadFromFile(path, width, height, false, null);
            if (animation == null)
            {
                if (webp)
                {
                    return GetWebPFrame(path, width);
                }

                return null;
            }

            var bitmap = new WriteableBitmap(width, height);
            animation.RenderSync(bitmap.PixelBuffer, frame);
            animation.Dispose();

            return bitmap;
        }
    }
}
