using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Core.Dependency;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Common
{
    public static class LazyBitmapImage
    {
        public static async void SetUriSource(this BitmapSource bitmap, Uri uri)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            using (var stream = await file.OpenReadAsync())
            {
                await bitmap.SetSourceAsync(stream);
            }
        }

        public static async void SetByteSource(this BitmapSource bitmap, byte[] data)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(data);
                    await writer.StoreAsync();
                }

                await bitmap.SetSourceAsync(stream);
            }
        }
    }

    public class TLBitmapImage : BitmapSource
    {
        public TLBitmapImage() { }

        public TLBitmapImage(TLObject value, bool thumbnail)
        {
            SetSource(value, false);
        }

        public void SetSource(TLObject value, bool thumbnail)
        {
            var photoMedia = value as TLMessageMediaPhoto;
            if (photoMedia != null)
            {
                value = photoMedia.Photo;
            }

            var photo = value as TLPhoto;
            if (photo != null)
            {
                double num = 400;

                TLPhotoSize photoSize = null;
                foreach (var current in photo.Sizes.OfType<TLPhotoSize>())
                {
                    if (photoSize == null || Math.Abs(num - photoSize.W) > Math.Abs(num - current.W))
                    {
                        photoSize = current;
                    }
                }

                photoSize = photo.Sizes.OfType<TLPhotoSize>().OrderByDescending(x => x.W).FirstOrDefault();

                if (photoSize != null)
                {
                    //if (!string.IsNullOrEmpty(photoSize.TempUrl))
                    //{
                    //    if (photoMedia != null)
                    //    {
                    //        photoMedia.DownloadingProgress = 0.01;
                    //    }
                    //    return photoSize.TempUrl;
                    //}

                    var fileLocation = photoSize.Location as TLFileLocation;
                    if (fileLocation != null)
                    {
                        var fileName = string.Format("{0}_{1}_{2}.jpg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);

                        if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName)))
                        {
                            this.SetUriSource(new Uri("ms-appdata:///local/" + fileName));
                        }
                        else if (photoSize.Size >= 0)
                        {
                            if (thumbnail)
                            {
                                var cachedSize = photo.Sizes.OfType<TLPhotoCachedSize>().FirstOrDefault();
                                if (cachedSize != null)
                                {
                                    this.SetByteSource(cachedSize.Bytes);
                                }
                            }

                            Execute.BeginOnThreadPool(async () =>
                            {
                                var manager = UnigramContainer.Instance.ResolverType<IDownloadFileManager>();
                                await manager.DownloadFileAsync(fileLocation, photoSize.Size);

                                Execute.BeginOnUIThread(() =>
                                {
                                    this.SetUriSource(new Uri("ms-appdata:///local/" + fileName));
                                });
                            });
                        }

                        return;
                    }
                }
            }
        }
    }
}
