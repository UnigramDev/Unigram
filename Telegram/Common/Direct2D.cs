//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Storage;
using Windows.UI.Composition;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Common
{
    public static class Direct2D
    {
        [ThreadStatic]
        private static Direct2DDevice _current;

        public static Direct2DDevice Current
        {
            get
            {
                if (_current == null)
                {
                    try
                    {
                        _current = new Direct2DDevice(BootStrapper.Current.Compositor);
                    }
                    catch
                    {
                        Logger.Error(Environment.StackTrace);
                        throw;
                    }
                }

                _current.HandleDeviceLost();
                return _current;
            }
        }

        [ThreadStatic]
        private static CompositionGraphicsDevice _video;

        /// <summary>
        /// The device a call's video is drawn on, apart from the one the window's text is: a sink
        /// draws from its own render queue, and a device takes one drawing session at a time, so
        /// a frame and a line of text drawn at once failed one of the two. One for the window,
        /// as the sinks on one device share one queue.
        /// </summary>
        public static CompositionGraphicsDevice VideoDevice => _video ??= Current.CreateGraphicsDevice();

        public static void Release()
        {
            if (_video != null)
            {
                _current?.ReleaseGraphicsDevice(_video);
                _video = null;
            }

            _current?.Dispose();
            _current = null;
        }

        private static Direct2DDevice _shared;
        private static readonly object _sharedLock = new();

        public static Direct2DDevice Shared
        {
            get
            {
                lock (_sharedLock)
                {
                    if (_shared == null)
                    {
                        try
                        {
                            _shared = new Direct2DDevice(null as Compositor);
                        }
                        catch
                        {
                            Logger.Error(Environment.StackTrace);
                            throw;
                        }
                    }

                    _shared.HandleDeviceLost();
                    return _shared;
                }
            }
        }


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

        public static async Task<ChatBackgroundPattern> LoadBitmapAsync(File file)
        {
            try
            {
                var item = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                using (var stream = await item.OpenReadAsync())
                {
                    var surface = LoadedImageSurface.StartLoadFromStream(stream);
                    return new ChatBackgroundPattern(surface);
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task<ChatBackgroundPattern> LoadPatternBitmapAsync(File file, float intensity, bool negative, double rasterizationScale, float minimumHeight)
        {
            return await Current.DrawSvgAsync(file.Local.Path, 1, false, rasterizationScale, minimumHeight);
        }

        public static async void GetBlurred(SoftwareBitmapSource source, string path, float amount = 3)
        {
            try
            {
                var bitmap = await Task.Run(() => Shared.DrawBlurred(path, amount));
                await source.SetBitmapAsync(bitmap);
            }
            catch { }
        }

        public static async void GetBlurred(SoftwareBitmapSource source, byte[] bytes, float amount = 3)
        {
            try
            {
                var bitmap = await Task.Run(() => Shared.DrawBlurred(bytes, amount));
                await source.SetBitmapAsync(bitmap);
            }
            catch { }
        }
    }
}
