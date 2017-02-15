using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Common;
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
        public static async void SetSource(this BitmapSource bitmap, Uri uri)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            using (var stream = await file.OpenReadAsync())
            {
                try
                {
                    await bitmap.SetSourceAsync(stream);
                }
                catch
                {
                    Debug.Write("AGGRESSIVE");
                }
            }
        }

        public static async void SetSource(this BitmapSource bitmap, byte[] data)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(data);
                    await writer.StoreAsync();
                }

                try
                {
                    await bitmap.SetSourceAsync(stream);
                }
                catch
                {
                    Debug.Write("AGGRESSIVE");
                }
            }
        }
    }

    public class TLBitmapContext : Dictionary<object, WeakReference<TLBitmapSource>>
    {
        public TLBitmapSource this[TLPhoto photo]
        {
            get
            {
                TLBitmapSource target;
                WeakReference<TLBitmapSource> reference;
                if (TryGetValue(photo, out reference) && reference.TryGetTarget(out target))
                {
                    return target;
                }

                target = new TLBitmapSource(photo);
                this[(object)photo] = new WeakReference<TLBitmapSource>(target);
                return target;
            }

            set
            {
                base[(object)photo] = new WeakReference<TLBitmapSource>(value);
            }
        }

        public TLBitmapSource this[TLDocument document]
        {
            get
            {
                TLBitmapSource target;
                WeakReference<TLBitmapSource> reference;
                if (TryGetValue(document, out reference) && reference.TryGetTarget(out target))
                {
                    return target;
                }

                target = new TLBitmapSource(document);
                this[(object)document] = new WeakReference<TLBitmapSource>(target);
                return target;
            }

            set
            {
                base[(object)document] = new WeakReference<TLBitmapSource>(value);
            }
        }

        public TLBitmapSource this[TLUserProfilePhoto userProfilePhoto]
        {
            get
            {
                TLBitmapSource target;
                WeakReference<TLBitmapSource> reference;
                if (TryGetValue(userProfilePhoto, out reference) && reference.TryGetTarget(out target))
                {
                    return target;
                }

                target = new TLBitmapSource(userProfilePhoto);
                this[(object)userProfilePhoto] = new WeakReference<TLBitmapSource>(target);
                return target;
            }

            set
            {
                base[(object)userProfilePhoto] = new WeakReference<TLBitmapSource>(value);
            }
        }


        public TLBitmapSource this[TLChatPhoto chatPhoto]
        {
            get
            {
                TLBitmapSource target;
                WeakReference<TLBitmapSource> reference;
                if (TryGetValue(chatPhoto, out reference) && reference.TryGetTarget(out target))
                {
                    return target;
                }

                target = new TLBitmapSource(chatPhoto);
                this[(object)chatPhoto] = new WeakReference<TLBitmapSource>(target);
                return target;
            }

            set
            {
                base[(object)chatPhoto] = new WeakReference<TLBitmapSource>(value);
            }
        }
    }

    public class TLBitmapSource : BitmapSource
    {
        public const int PHASE_PLACEHOLDER = 0;
        public const int PHASE_THUMBNAIL = 1;
        public const int PHASE_DEFINITIVE = 2;

        public int Phase { get; private set; }

        public TLBitmapSource() { }

        public TLBitmapSource(TLUserProfilePhoto userProfilePhoto)
        {
            if (TrySetSource(userProfilePhoto.PhotoSmall as TLFileLocation, PHASE_DEFINITIVE) == false)
            {
                this.SetSource(new Uri("ms-appx:///Assets/Images/ProfilePlaceholder0.png"));
                SetSource(userProfilePhoto.PhotoSmall as TLFileLocation, 0, PHASE_DEFINITIVE);
            }
        }

        public TLBitmapSource(TLChatPhoto chatPhoto)
        {
            if (TrySetSource(chatPhoto.PhotoSmall as TLFileLocation, PHASE_DEFINITIVE) == false)
            {
                this.SetSource(new Uri("ms-appx:///Assets/Images/ProfilePlaceholder0.png"));
                SetSource(chatPhoto.PhotoSmall as TLFileLocation, 0, PHASE_DEFINITIVE);
            }
        }

        public TLBitmapSource(TLPhotoBase photoBase)
        {
            var photo = photoBase as TLPhoto;
            if (photo != null)
            {
                if (TrySetSource(photo.Full, PHASE_DEFINITIVE) == false)
                {
                    SetSource(photo.Thumb, PHASE_THUMBNAIL);
                    SetSource(photo.Full, PHASE_DEFINITIVE);
                }
            }
        }

        public TLBitmapSource(TLDocument document)
        {
            SetSource(document.Thumb, PHASE_THUMBNAIL);
        }

        public bool TrySetSource(TLPhotoSizeBase photoSizeBase, int phase)
        {
            var photoSize = photoSizeBase as TLPhotoSize;
            if (photoSize != null)
            {
                return TrySetSource(photoSize.Location as TLFileLocation, phase);
            }

            var photoCachedSize = photoSizeBase as TLPhotoCachedSize;
            if (photoCachedSize != null)
            {
                if (phase >= Phase)
                {
                    Phase = phase;
                    this.SetSource(photoCachedSize.Bytes);
                    return true;
                }
            }

            return false;
        }

        public void SetSource(TLPhotoSizeBase photoSizeBase, int phase)
        {
            var photoSize = photoSizeBase as TLPhotoSize;
            if (photoSize != null)
            {
                SetSource(photoSize.Location as TLFileLocation, photoSize.Size, phase);
            }

            var photoCachedSize = photoSizeBase as TLPhotoCachedSize;
            if (photoCachedSize != null)
            {
                if (phase >= Phase)
                {
                    Phase = phase;
                    this.SetSource(photoCachedSize.Bytes);
                }
            }
        }

        public bool TrySetSource(TLFileLocation location, int phase)
        {
            if (phase >= Phase && location != null)
            {
                Phase = phase;

                var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    this.SetSource(FileUtils.GetTempFileUri(fileName));
                    return true;
                }
            }

            return false;
        }

        public void SetSource(TLFileLocation location, int fileSize, int phase)
        {
            if (phase >= Phase && location != null)
            {
                Phase = phase;

                var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    this.SetSource(FileUtils.GetTempFileUri(fileName));
                }
                else
                {
                    Execute.BeginOnThreadPool(async () =>
                    {
                        var manager = UnigramContainer.Instance.ResolveType<IDownloadFileManager>();
                        await manager.DownloadFileAsync(location, fileSize);

                        Execute.BeginOnUIThread(() =>
                        {
                            this.SetSource(FileUtils.GetTempFileUri(fileName));
                        });
                    });
                }
            }
        }
    }
}