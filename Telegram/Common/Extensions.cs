//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Entities;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
#if NET9_0_OR_GREATER
using WinRT;
#endif

namespace Telegram.Common
{
    public static partial class Extensions
    {
        public static bool TryGetValue(this CoreWebView2HttpRequestHeaders headers, string key, out string value)
        {
            if (headers.Contains(key))
            {
                value = headers.GetHeader(key);
                return true;
            }

            value = null;
            return false;
        }

        public static async Task<StorageMedia> PickSingleMediaAsync(this FileOpenPicker picker, XamlRoot xamlRoot)
        {
            var file = await picker.PickSingleFileAsync(xamlRoot);
            if (file == null)
            {
                return null;
            }

            var media = await StorageMedia.CreateAsync(file);
            if (media != null)
            {
                return media;
            }

            return new StorageInvalid();
        }

        public static Version ToVersion(this PackageVersion version)
        {
            return new Version(version.Major, version.Minor, version.Build, version.Revision);
        }

        public static IEnumerable<AlternativeVideo> FindAlternatives(this GalleryMedia video, params string[] codecs)
        {
            var playlists = video.AlternativeVideos
                .GroupBy(x => x.Codec)
                .ToDictionary(x => x.Key);

            foreach (var codec in codecs)
            {
                if (playlists.TryGetValue(codec, out var playlist))
                {
                    return playlist;
                }
            }

            return Enumerable.Empty<AlternativeVideo>();
        }

        public static int GetNamedInt32(this JsonObject obj, string name, int defaultValue)
        {
            return (int)obj.GetNamedNumber(name, defaultValue);
        }

        public static long GetNamedInt64(this JsonObject obj, string name, long defaultValue)
        {
            var value = obj.GetNamedString(name, string.Empty);
            if (long.TryParse(value, out long result))
            {
                return result;
            }

            return defaultValue;
        }

        public static bool HasExtension(this IStorageFile file, params string[] extensions)
        {
            // A file dragged straight out of a ZIP archive arrives with an empty FileType, though
            // its Name still carries the extension. Falling back rather than always reading Name
            // keeps the exact-match behaviour everywhere FileType is populated.
            var type = file.FileType;

            if (string.IsNullOrEmpty(type))
            {
                return file.Name.HasExtension(extensions);
            }

            foreach (var ext in extensions)
            {
                if (type.Equals(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasExtension(this string path, params string[] extensions)
        {
            foreach (var ext in extensions)
            {
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        class TaskCompletion : TaskCompletionSource<bool>
        {
            public void SetCompleted(object state, bool timedOut)
            {
                TrySetResult(true);
            }
        }

        public static Task WaitOneAsync(this WaitHandle waitHandle)
        {
            var tcs = new TaskCompletion();
            var rwh = ThreadPool.RegisterWaitForSingleObject(waitHandle, tcs.SetCompleted, null, -1, true);
            var t = tcs.Task;
            t.ContinueWith((antecedent) => rwh.Unregister(null));
            return t;
        }

        public static IAsyncOperation<AppServiceResponse> SendMessageAsync(this AppServiceConnection connection, string message, object parameter = null)
        {
            return connection.SendMessageAsync(new ValueSet { { message, parameter ?? true } });
        }

        private const long UnixEpochTicks = 621355968000000000;

        public static int ToUnixTimeSeconds(this DateTime dateTime)
        {
            return (int)((dateTime.ToUniversalTime().Ticks - UnixEpochTicks) / TimeSpan.TicksPerSecond);
        }

        public static uint GetHeight(this VideoProperties props)
        {
            return props.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? props.Height : props.Width;
        }

        public static uint GetWidth(this VideoProperties props)
        {
            return props.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? props.Width : props.Height;
        }

        public static bool IsRelativePath(string relativeTo, string path, out string relative)
        {
            if (string.IsNullOrEmpty(relativeTo) || string.IsNullOrEmpty(path))
            {
                relative = null;
                return false;
            }

            var relativeFull = Path.GetFullPath(relativeTo);
            var pathFull = Path.GetFullPath(path);

            if (pathFull.Length > relativeFull.Length && pathFull[relativeFull.Length] == '\\')
            {
                if (pathFull.StartsWith(relativeFull, StringComparison.OrdinalIgnoreCase))
                {
                    relative = pathFull.Substring(relativeFull.Length + 1);
                    return true;
                }
            }

            relative = null;
            return string.Equals(relativeFull, pathFull, StringComparison.OrdinalIgnoreCase);
        }

        public static unsafe void Buffer(this WriteableBitmap bitmap, out byte* imageBytes)
        {
#if NET9_0_OR_GREATER
            var access = bitmap.PixelBuffer.As<IBufferByteAccess>();
#else
            var access = (IBufferByteAccess)bitmap.PixelBuffer;
#endif
            access.Buffer(out imageBytes);
        }

        public static unsafe Span<byte> Buffer(this WriteableBitmap bitmap)
        {
#if NET9_0_OR_GREATER
            var access = bitmap.PixelBuffer.As<IBufferByteAccess>();
#else
            var access = (IBufferByteAccess)bitmap.PixelBuffer;
#endif
            access.Buffer(out byte* imageBytes);

            return new Span<byte>(imageBytes, bitmap.PixelWidth * bitmap.PixelHeight * 4);
        }

        public static unsafe void Buffer(this IMemoryBufferReference reference, out byte* buffer, out uint capacity)
        {
#if NET9_0_OR_GREATER
            var access = reference.As<IMemoryBufferByteAccess>();
#else
            var access = (IMemoryBufferByteAccess)reference;
#endif
            access.GetBuffer(out buffer, out capacity);
        }

        public static bool TypeEquals(this object o1, object o2)
        {
            if (o1 == null || o2 == null)
            {
                return false;
            }

            return Equals(o1.GetType(), o2.GetType());
        }

        public static bool IsBetween(this TimeSpan value, TimeSpan minimum, TimeSpan maximum)
        {
            // see if start comes before end
            if (minimum < maximum)
            {
                return minimum <= value && value <= maximum;
            }

            // start is after end, so do the inverse comparison
            return !(maximum < value && value < minimum);
        }
    }
}
