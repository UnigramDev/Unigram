using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Views;
using Unigram.Native;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;

namespace Unigram.Converters
{
    // TEMP
    public class DefaultPhotoConverter : IValueConverter
    {
        private static readonly Dictionary<string, WeakReference> _cachedSources = new Dictionary<string, WeakReference>();
        private static readonly Dictionary<string, WeakReference<WriteableBitmap>> _cachedWebPImages = new Dictionary<string, WeakReference<WriteableBitmap>>();

        private static readonly Dictionary<Window, TLBitmapContext> _threadContext = new Dictionary<Window, TLBitmapContext>();

        public static TLBitmapContext BitmapContext
        {
            get
            {
                if (_threadContext.TryGetValue(Window.Current, out TLBitmapContext value))
                {
                    return value;
                }

                var context = new TLBitmapContext();
                _threadContext[Window.Current] = context;

                return context;
            }
        }

        private static readonly AnimatedImageSourceRendererFactory _videoFactory = new AnimatedImageSourceRendererFactory();

        public bool CheckChatSettings
        {
            get;
            set;
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var param = parameter?.ToString();
            var thumbnail = string.Equals(param, "thumbnail", StringComparison.OrdinalIgnoreCase);

            return Convert(value, thumbnail);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public static object Convert(object value)
        {
            return Convert(value, false);
        }

        public static object Convert(object value, bool thumbnail)
        {
            if (value == null)
            {
                return null;
            }

            //var encryptedFile = value as TLEncryptedFile;
            //if (encryptedFile != null)
            //{
            //    return DefaultPhotoConverter.ReturnOrEnqueueImage(timer, this.CheckChatSettings, encryptedFile, encryptedFile, null);
            //}

            if (value is TLUser user)
            {
                return BitmapContext[user];
            }

            if (value is TLChat chat)
            {
                return BitmapContext[chat];
            }

            if (value is TLChatForbidden forbiddenChat)
            {
                return BitmapContext[forbiddenChat];
            }

            if (value is TLChannel channel)
            {
                return BitmapContext[channel];
            }

            if (value is TLChannelForbidden forbiddenChannel)
            {
                return BitmapContext[forbiddenChannel];
            }

            if (value is TLUserProfilePhoto userProfilePhoto)
            {
                if (userProfilePhoto.PhotoSmall is TLFileLocation fileLocation)
                {
                    return ReturnOrEnqueueProfileImage(fileLocation, userProfilePhoto, 0);
                }
            }

            if (value is TLChatPhoto chatPhoto)
            {
                if (chatPhoto.PhotoSmall is TLFileLocation fileLocation)
                {
                    return ReturnOrEnqueueProfileImage(fileLocation, chatPhoto, 0);
                }
            }

            //TLDecryptedMessageMediaBase tLDecryptedMessageMediaBase = value as TLDecryptedMessageMediaBase;
            //if (tLDecryptedMessageMediaBase != null)
            //{
            //    TLDecryptedMessageMediaVideo tLDecryptedMessageMediaVideo = value as TLDecryptedMessageMediaVideo;
            //    if (tLDecryptedMessageMediaVideo != null)
            //    {
            //        byte[] data = tLDecryptedMessageMediaVideo.Thumb.Data;
            //        if (data.Length > 0 && tLDecryptedMessageMediaVideo.ThumbW.Value > 0 && tLDecryptedMessageMediaVideo.ThumbH.Value > 0)
            //        {
            //            try
            //            {
            //                MemoryStream memoryStream = new MemoryStream(data);
            //                WriteableBitmap writeableBitmap = PictureDecoder.DecodeJpeg(memoryStream);
            //                writeableBitmap.BoxBlur(37);
            //                MemoryStream memoryStream2 = new MemoryStream();
            //                Extensions.SaveJpeg(writeableBitmap, memoryStream2, tLDecryptedMessageMediaVideo.ThumbW.Value, tLDecryptedMessageMediaVideo.ThumbH.Value, 0, 70);
            //                object result = ImageUtils.CreateImage(memoryStream2.ToArray());
            //                return result;
            //            }
            //            catch (Exception)
            //            {
            //            }
            //        }
            //        return ImageUtils.CreateImage(data);
            //    }
            //    TLDecryptedMessageMediaDocument tLDecryptedMessageMediaDocument = value as TLDecryptedMessageMediaDocument;
            //    if (tLDecryptedMessageMediaDocument != null)
            //    {
            //        TLEncryptedFile tLEncryptedFile2 = tLDecryptedMessageMediaDocument.File as TLEncryptedFile;
            //        if (tLEncryptedFile2 != null)
            //        {
            //            string text = string.Format("{0}_{1}_{2}.jpg", tLEncryptedFile2.Id, tLEncryptedFile2.DCId, tLEncryptedFile2.AccessHash);
            //            using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
            //            {
            //                if (userStoreForApplication.FileExists(text))
            //                {
            //                    object result;
            //                    BitmapImage bitmapImage2;
            //                    try
            //                    {
            //                        using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(text, 3, 1))
            //                        {
            //                            isolatedStorageFileStream.Seek(0L, 0);
            //                            BitmapImage bitmapImage = new BitmapImage();
            //                            bitmapImage.SetSource(isolatedStorageFileStream);
            //                            bitmapImage2 = bitmapImage;
            //                        }
            //                    }
            //                    catch (Exception)
            //                    {
            //                        result = null;
            //                        return result;
            //                    }
            //                    result = bitmapImage2;
            //                    return result;
            //                }
            //            }
            //        }
            //        byte[] data2 = tLDecryptedMessageMediaDocument.Thumb.Data;
            //        return ImageUtils.CreateImage(data2);
            //    }
            //    TLEncryptedFile tLEncryptedFile3 = tLDecryptedMessageMediaBase.File as TLEncryptedFile;
            //    if (tLEncryptedFile3 != null && !tLDecryptedMessageMediaBase.IsCanceled)
            //    {
            //        return DefaultPhotoConverter.ReturnOrEnqueueImage(timer, this.CheckChatSettings, tLEncryptedFile3, tLDecryptedMessageMediaBase, tLDecryptedMessageMediaBase);
            //    }
            //}
            //TLDecryptedMessage tLDecryptedMessage = value as TLDecryptedMessage;
            //if (tLDecryptedMessage != null)
            //{
            //    TLDecryptedMessageMediaExternalDocument tLDecryptedMessageMediaExternalDocument = tLDecryptedMessage.Media as TLDecryptedMessageMediaExternalDocument;
            //    if (tLDecryptedMessageMediaExternalDocument != null)
            //    {
            //        return DefaultPhotoConverter.ReturnOrEnqueueSticker(tLDecryptedMessageMediaExternalDocument, tLDecryptedMessage);
            //    }
            //}

            var photoMedia = value as TLMessageMediaPhoto;
            if (photoMedia != null)
            {
                value = photoMedia.Photo;
            }

            var photo = value as TLPhoto;
            if (photo != null)
            {
                return BitmapContext[photo];
            }

            var photoSizeBase2 = value as TLPhotoSizeBase;
            if (photoSizeBase2 != null)
            {
                var photoSize = photoSizeBase2 as TLPhotoSize;
                if (photoSize != null)
                {
                    var fileLocation = photoSize.Location as TLFileLocation;
                    if (fileLocation != null /*&& (photoMedia == null || !photoMedia.IsCanceled)*/)
                    {
                        return ReturnOrEnqueueImage(false, fileLocation, null, photoSize.Size, photoMedia);
                    }
                }

                var photoCachedSize = photoSizeBase2 as TLPhotoCachedSize;
                if (photoCachedSize != null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.SetSource(photoCachedSize.Bytes);
                    return bitmap;
                }
            }

            if (value is TLBotInlineMediaResult botInlineMediaResult && botInlineMediaResult.Type.Equals("sticker", StringComparison.OrdinalIgnoreCase))
            {
                var document = botInlineMediaResult.Document as TLDocument;
                if (document == null)
                {
                    return null;
                }

                var photoCachedSize = document.Thumb as TLPhotoCachedSize;
                if (photoCachedSize != null)
                {
                    //var cacheKey = "cached" + document.GetFileName();
                    //if (photoCachedSize.Bytes == null)
                    //{
                    //    return null;
                    //}

                    //return DefaultPhotoConverter.DecodeWebPImage(cacheKey, photoCachedSize.Bytes, delegate
                    //{
                    //});
                }
                else
                {
                    var photoSize = document.Thumb as TLPhotoSize;
                    if (photoSize != null)
                    {
                        var fileLocation = photoSize.Location as TLFileLocation;
                        if (fileLocation != null)
                        {
                            //return DefaultPhotoConverter.ReturnOrEnqueueStickerPreview(fileLocation, botInlineMediaResult, photoSize.Size);
                        }
                    }

                    //if (TLMessageBase.IsSticker(document))
                    //{
                    //    return DefaultPhotoConverter.ReturnOrEnqueueSticker(document, botInlineMediaResult);
                    //}
                }
            }

            //var stickerItem = value as TLStickerItem;
            //if (stickerItem != null)
            //{
            //    var document = stickerItem.Document as TLDocument;
            //    if (document == null)
            //    {
            //        return null;
            //    }

            //    var photoCachedSize = document.Thumb as TLPhotoCachedSize;
            //    if (photoCachedSize != null)
            //    {
            //        var cacheKey = "cached" + document.GetFileName();
            //        if (photoCachedSize.Bytes == null)
            //        {
            //            return null;
            //        }

            //        return DefaultPhotoConverter.DecodeWebPImage(cacheKey, data4, delegate
            //        {
            //        });
            //    }
            //    else
            //    {
            //        var photoSize = document.Thumb as TLPhotoSize;
            //        if (photoSize != null)
            //        {
            //            var fileLocation = photoSize.Location as TLFileLocation;
            //            if (fileLocation != null)
            //            {
            //                return DefaultPhotoConverter.ReturnOrEnqueueStickerPreview(fileLocation, stickerItem, photoSize.Size);
            //            }
            //        }

            //        if (TLMessageBase.IsSticker(document))
            //        {
            //            return DefaultPhotoConverter.ReturnOrEnqueueSticker(document, stickerItem);
            //        }
            //    }
            //}

            if (value is TLDocument tLDocument3)
            {
                //if (TLMessage.IsGif(tLDocument3))
                //{
                //    return ReturnOrEnqueueGif(tLDocument3, thumbnail);
                //}

                return BitmapContext[tLDocument3, thumbnail];
            }

            //var videoMedia = value as TLMessageMediaVideo;
            //if (videoMedia != null)
            //{
            //    value = videoMedia.Video;
            //}

            //var video = value as TLVideo;
            //if (video != null)
            //{
            //    var photoSize = video.Thumb as TLPhotoSize;
            //    if (photoSize != null)
            //    {
            //        var fileLocation = photoSize.Location as TLFileLocation;
            //        if (fileLocation != null)
            //        {
            //            return ReturnOrEnqueueImage(timer, false, fileLocation, video, photoSize.Size, null);
            //        }
            //    }

            //    var photoCachedSize = video.Thumb as TLPhotoCachedSize;
            //    if (photoCachedSize != null)
            //    {
            //        byte[] data6 = photoCachedSize.Bytes.Data;
            //        return ImageUtils.CreateImage(data6);
            //    }
            //}

            if (value is TLMessageMediaWebPage webpageMedia)
            {
                value = webpageMedia.WebPage;
            }

            //var decryptedWebpageMedia = value as TLDecryptedMessageMediaWebPage;
            //if (decryptedWebpageMedia != null)
            //{
            //    value = decryptedWebpageMedia.WebPage;
            //}

            if (value is TLWebPage webpage)
            {
                if (webpage.Photo is TLPhoto webpagePhoto)
                {
                    return BitmapContext[webpagePhoto];
                }
            }

            if (value is TLMessageMediaInvoice invoiceMedia)
            {
                value = invoiceMedia.Photo;
            }

            if (value is TLWebDocument webDocument)
            {
                return BitmapContext[webDocument];
            }

            return null;
        }

        public static BitmapSource ReturnOrEnqueueProfileImage(TLFileLocation location, TLObject owner, int fileSize)
        {
            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            if (_cachedSources.TryGetValue(fileName, out WeakReference weakReference) && weakReference.IsAlive)
            {
                return weakReference.Target as BitmapSource;
            }

            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {
                var bitmap = new BitmapImage();
                bitmap.UriSource = FileUtils.GetTempFileUri(fileName);
                _cachedSources[fileName] = new WeakReference(bitmap);

                return bitmap;
            }

            if (fileSize >= 0)
            {
                var manager = UnigramContainer.Current.ResolveType<IDownloadFileManager>();
                var bitmap = new BitmapImage();
                _cachedSources[fileName] = new WeakReference(bitmap);

                Execute.BeginOnThreadPool(async () =>
                {
                    await manager.DownloadFileAsync(location, fileSize);
                    Execute.BeginOnUIThread(() =>
                    {
                        bitmap.UriSource = FileUtils.GetTempFileUri(fileName);
                    });
                });

                return bitmap;
            }

            return null;
        }

        public static BitmapImage ReturnOrEnqueueImage(bool checkChatSettings, TLFileLocation location, TLObject owner, int fileSize, TLMessageMediaPhoto mediaPhoto)
        {
            string fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {
                var bitmap = new BitmapImage();
                bitmap.UriSource = FileUtils.GetTempFileUri(fileName);
                return bitmap;
            }

            if (fileSize >= 0)
            {
                var manager = UnigramContainer.Current.ResolveType<IDownloadFileManager>();
                var bitmap = new BitmapImage();

                //Execute.BeginOnThreadPool(() => manager.DownloadFile(location, owner, fileSize));
                Execute.BeginOnThreadPool(async () =>
                {
                    await manager.DownloadFileAsync(location, fileSize, mediaPhoto?.Photo.Download());
                    Execute.BeginOnUIThread(() =>
                    {
                        bitmap.UriSource = FileUtils.GetTempFileUri(fileName);
                    });
                });

                return bitmap;
            }

            return null;
        }
    }
}
