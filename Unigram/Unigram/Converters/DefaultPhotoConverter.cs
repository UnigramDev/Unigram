﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Converters
{
    // TEMP
    public class DefaultPhotoConverter : IValueConverter
    {
        private static readonly Dictionary<string, WeakReference> _cachedSources = new Dictionary<string, WeakReference>();

        private static readonly Dictionary<string, WeakReference<WriteableBitmap>> _cachedWebPImages = new Dictionary<string, WeakReference<WriteableBitmap>>();

        public bool CheckChatSettings
        {
            get;
            set;
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return null;
            }

            return null;

            Stopwatch timer = Stopwatch.StartNew();

            //var encryptedFile = value as TLEncryptedFile;
            //if (encryptedFile != null)
            //{
            //    return DefaultPhotoConverter.ReturnOrEnqueueImage(timer, this.CheckChatSettings, encryptedFile, encryptedFile, null);
            //}

            var userProfilePhoto = value as TLUserProfilePhoto;
            if (userProfilePhoto != null)
            {
                var fileLocation = userProfilePhoto.PhotoSmall as TLFileLocation;
                if (fileLocation != null)
                {
                    return ReturnOrEnqueueProfileImage(timer, fileLocation, userProfilePhoto, 0);
                }
            }

            var chatPhoto = value as TLChatPhoto;
            if (chatPhoto != null)
            {
                var fileLocation = chatPhoto.PhotoSmall as TLFileLocation;
                if (fileLocation != null)
                {
                    return ReturnOrEnqueueProfileImage(timer, fileLocation, chatPhoto, 0);
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
                double num = 311.0;
                double num2;
                if (double.TryParse((string)parameter, out num2))
                {
                    num = num2;
                }

                TLPhotoSize photoSize = null;
                foreach (var current in photo.Sizes.OfType<TLPhotoSize>())
                {
                    if (photoSize == null || Math.Abs(num - photoSize.W) > Math.Abs(num - current.W))
                    {
                        photoSize = current;
                    }
                }

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
                    if (fileLocation != null && (photoMedia == null /*|| !photoMedia.IsCanceled*/))
                    {
                        return ReturnOrEnqueueImage(timer, CheckChatSettings, fileLocation, photo, photoSize.Size, photoMedia);
                    }
                }
            }

            var botInlineMediaResult = value as TLBotInlineMediaResult;
            if (botInlineMediaResult != null && botInlineMediaResult.Type.Equals("sticker", StringComparison.OrdinalIgnoreCase))
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

            var tLDocument3 = value as TLDocument;
            if (tLDocument3 != null)
            {
                //if (TLMessageBase.IsSticker(tLDocument3))
                //{
                //    if (parameter != null && string.Equals(parameter.ToString(), "ignoreStickers", StringComparison.OrdinalIgnoreCase))
                //    {
                //        return null;
                //    }

                //    return DefaultPhotoConverter.ReturnOrEnqueueSticker((TLDocument22)tLDocument3, null);
                //}
                //else
                //{
                //    var photoSize = tLDocument3.Thumb as TLPhotoSize;
                //    if (photoSize != null)
                //    {
                //        var fileLocation = photoSize.Location as TLFileLocation;
                //        if (fileLocation != null)
                //        {
                //            return ReturnOrEnqueueImage(timer, false, fileLocation, tLDocument3, photoSize.Size, null);
                //        }
                //    }

                //    var photoCachedSize = tLDocument3.Thumb as TLPhotoCachedSize;
                //    if (photoCachedSize != null)
                //    {
                //        byte[] data5 = photoCachedSize.Bytes.Data;
                //        return ImageUtils.CreateImage(data5);
                //    }
                //}
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

            var webpageMedia = value as TLMessageMediaWebPage;
            if (webpageMedia != null)
            {
                value = webpageMedia.Webpage;
            }

            //var decryptedWebpageMedia = value as TLDecryptedMessageMediaWebPage;
            //if (decryptedWebpageMedia != null)
            //{
            //    value = decryptedWebpageMedia.WebPage;
            //}

            var webpage = value as TLWebPage;
            if (webpage != null)
            {
                var tLPhoto2 = webpage.Photo as TLPhoto;
                if (tLPhoto2 != null)
                {
                    double num3 = 311.0;
                    double num4;
                    if (double.TryParse((string)parameter, out num4))
                    {
                        num3 = num4;
                    }

                    TLPhotoSize photoSize = null;
                    foreach (var current in tLPhoto2.Sizes.OfType<TLPhotoSize>())
                    {
                        if (photoSize == null || Math.Abs(num3 - (double)photoSize.W) > Math.Abs(num3 - (double)current.W))
                        {
                            photoSize = current;
                        }
                    }

                    if (photoSize != null)
                    {
                        var fileLocation = photoSize.Location as TLFileLocation;
                        if (fileLocation != null)
                        {
                            return DefaultPhotoConverter.ReturnOrEnqueueImage(timer, false, fileLocation, webpage, photoSize.Size, null);
                        }
                    }
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        //private static ImageSource DecodeWebPImage(string cacheKey, byte[] buffer, System.Action faultCallback = null)
        //{
        //    try
        //    {
        //        WeakReference<WriteableBitmap> weakReference;
        //        WriteableBitmap writeableBitmap;
        //        ImageSource result;
        //        if (DefaultPhotoConverter._cachedWebPImages.TryGetValue(cacheKey, ref weakReference) && weakReference.TryGetTarget(ref writeableBitmap))
        //        {
        //            result = writeableBitmap;
        //            return result;
        //        }
        //        WebPDecoder webPDecoder = new WebPDecoder();
        //        int num = 0;
        //        int num2 = 0;
        //        byte[] array = null;
        //        if (buffer == null)
        //        {
        //            Log.Write("DefaultPhotoConverter.DecodeWebPImage buffer=null", null);
        //        }
        //        else
        //        {
        //            array = webPDecoder.DecodeRgbA(buffer, out num, out num2);
        //        }
        //        if (array == null)
        //        {
        //            result = null;
        //            return result;
        //        }
        //        WriteableBitmap writeableBitmap2 = new WriteableBitmap(num, num2);
        //        for (int i = 0; i < array.Length / 4; i++)
        //        {
        //            int num3 = (int)array[4 * i];
        //            int num4 = (int)array[4 * i + 1];
        //            int num5 = (int)array[4 * i + 2];
        //            int num6 = (int)array[4 * i + 3];
        //            num6 <<= 24;
        //            num3 <<= 16;
        //            num4 <<= 8;
        //            int num7 = num6 | num3 | num4 | num5;
        //            writeableBitmap2.get_Pixels()[i] = num7;
        //        }
        //        DefaultPhotoConverter._cachedWebPImages.set_Item(cacheKey, new WeakReference<WriteableBitmap>(writeableBitmap2));
        //        result = writeableBitmap2;
        //        return result;
        //    }
        //    catch (Exception e)
        //    {
        //        TLUtils.WriteException("WebPDecode ex ", e);
        //    }
        //    return null;
        //}

        public static BitmapImage ReturnImage(Stopwatch timer, TLFileLocation location)
        {
            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName)))
            {
                return new BitmapImage(new Uri("ms-appdata:///local/" + fileName));
            }

            //using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
            //{
            //    if (userStoreForApplication.FileExists(fileName))
            //    {
            //        BitmapImage bitmapImage2;
            //        BitmapImage result;
            //        try
            //        {
            //            using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(fileName, 3, 1))
            //            {
            //                isolatedStorageFileStream.Seek(0L, 0);
            //                BitmapImage bitmapImage = new BitmapImage();
            //                bitmapImage.SetSource(isolatedStorageFileStream);
            //                bitmapImage2 = bitmapImage;
            //            }
            //        }
            //        catch (Exception)
            //        {
            //            result = null;
            //            return result;
            //        }
            //        result = bitmapImage2;
            //        return result;
            //    }
            //}
            return null;
        }

        private static async Task<BitmapImage> LoadBitmapAsync(Uri uri)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            using (var stream = await file.OpenReadAsync())
            {
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);

                return bitmap;
            }
        }

        //public static BitmapImage ReturnOrEnqueueImage(Stopwatch timer, bool checkChatSettings, TLEncryptedFile location, TLObject owner, TLDecryptedMessageMediaBase mediaPhoto)
        //{
        //    string text = string.Format("{0}_{1}_{2}.jpg", location.Id, location.DCId, location.AccessHash);
        //    using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
        //    {
        //        if (userStoreForApplication.FileExists(text))
        //        {
        //            BitmapImage bitmapImage2;
        //            BitmapImage result;
        //            try
        //            {
        //                using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(text, 3, 1))
        //                {
        //                    isolatedStorageFileStream.Seek(0L, 0);
        //                    BitmapImage bitmapImage = new BitmapImage();
        //                    bitmapImage.SetSource(isolatedStorageFileStream);
        //                    bitmapImage2 = bitmapImage;
        //                }
        //            }
        //            catch (Exception)
        //            {
        //                result = null;
        //                return result;
        //            }
        //            result = bitmapImage2;
        //            return result;
        //        }
        //        TLObject tLObject = null;
        //        if (checkChatSettings)
        //        {
        //            INavigationService navigationService = IoC.Get<INavigationService>(null);
        //            SecretDialogDetailsView secretDialogDetailsView = navigationService.CurrentContent as SecretDialogDetailsView;
        //            if (secretDialogDetailsView != null)
        //            {
        //                SecretDialogDetailsViewModel secretDialogDetailsViewModel = secretDialogDetailsView.get_DataContext() as SecretDialogDetailsViewModel;
        //                if (secretDialogDetailsViewModel != null)
        //                {
        //                    tLObject = secretDialogDetailsViewModel.With;
        //                }
        //            }
        //        }
        //        IStateService stateService = IoC.Get<IStateService>(null);
        //        TLChatSettings chatSettings = stateService.GetChatSettings();
        //        if (chatSettings != null)
        //        {
        //            if (tLObject is TLUserBase && !chatSettings.AutoDownloadPhotoPrivateChats)
        //            {
        //                BitmapImage result = null;
        //                return result;
        //            }
        //            if (tLObject is TLChatBase && !chatSettings.AutoDownloadPhotoGroups)
        //            {
        //                BitmapImage result = null;
        //                return result;
        //            }
        //        }
        //        if (mediaPhoto != null)
        //        {
        //            mediaPhoto.DownloadingProgress = 0.01;
        //        }
        //        Telegram.Api.Helpers.Execute.BeginOnThreadPool(delegate
        //        {
        //            IEncryptedFileManager encryptedFileManager = IoC.Get<IEncryptedFileManager>(null);
        //            encryptedFileManager.DownloadFile(location, owner, null);
        //        });
        //    }
        //    return null;
        //}

        public static BitmapImage ReturnOrEnqueueImage(Stopwatch timer, bool checkChatSettings, TLFileLocation location, TLObject owner, int fileSize, TLMessageMediaPhoto mediaPhoto)
        {
            string fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName)))
            {
                var bitmap = new BitmapImage(new Uri("ms-appdata:///local/" + fileName));
                return bitmap;
            }

            if (fileSize >= 0)
            {
                var manager = UnigramContainer.Instance.ResolverType<IDownloadFileManager>();
                Execute.BeginOnThreadPool(() => manager.DownloadFile(location, owner, fileSize));
            }

            //using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
            //{
            //    if (userStoreForApplication.FileExists(text))
            //    {
            //        BitmapImage bitmapImage2;
            //        BitmapImage result;
            //        try
            //        {
            //            using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(text, 3, 1))
            //            {
            //                isolatedStorageFileStream.Seek(0L, 0);
            //                BitmapImage bitmapImage = new BitmapImage();
            //                bitmapImage.SetSource(isolatedStorageFileStream);
            //                bitmapImage2 = bitmapImage;
            //            }
            //        }
            //        catch (Exception)
            //        {
            //            result = null;
            //            return result;
            //        }
            //        result = bitmapImage2;
            //        return result;
            //    }
            //    if (fileSize != null)
            //    {
            //        TLObject tLObject = null;
            //        if (checkChatSettings)
            //        {
            //            INavigationService navigationService = IoC.Get<INavigationService>(null);
            //            DialogDetailsView dialogDetailsView = navigationService.CurrentContent as DialogDetailsView;
            //            if (dialogDetailsView != null)
            //            {
            //                DialogDetailsViewModel dialogDetailsViewModel = dialogDetailsView.get_DataContext() as DialogDetailsViewModel;
            //                if (dialogDetailsViewModel != null)
            //                {
            //                    tLObject = dialogDetailsViewModel.With;
            //                }
            //            }
            //        }
            //        IStateService stateService = IoC.Get<IStateService>(null);
            //        TLChatSettings chatSettings = stateService.GetChatSettings();
            //        if (chatSettings != null)
            //        {
            //            if (tLObject is TLUserBase && !chatSettings.AutoDownloadPhotoPrivateChats)
            //            {
            //                BitmapImage result = null;
            //                return result;
            //            }
            //            if (tLObject is TLChatBase && !chatSettings.AutoDownloadPhotoGroups)
            //            {
            //                BitmapImage result = null;
            //                return result;
            //            }
            //        }
            //        if (mediaPhoto != null)
            //        {
            //            mediaPhoto.DownloadingProgress = 0.01;
            //        }
            //        Telegram.Api.Helpers.Execute.BeginOnThreadPool(delegate
            //        {
            //            IFileManager fileManager = IoC.Get<IFileManager>(null);
            //            fileManager.DownloadFile(location, owner, fileSize);
            //        });
            //    }
            //}
            return null;
        }

        public static BitmapSource ReturnOrEnqueueProfileImage(Stopwatch timer, TLFileLocation location, TLObject owner, int fileSize)
        {
            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            WeakReference weakReference;
            if (_cachedSources.TryGetValue(fileName, out weakReference) && weakReference.IsAlive)
            {
                return weakReference.Target as BitmapSource;
            }

            if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName)))
            {
                //var bytes = File.ReadAllBytes(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName));
                //var stream = new MemoryStream(bytes);
                //var bitmap = new BitmapImage();
                //bitmap.SetSource(stream.AsRandomAccessStream());

                var bitmap = new BitmapImage(new Uri("ms-appdata:///local/" + fileName));
                _cachedSources[fileName] = new WeakReference(bitmap);

                return bitmap;
            }

            if (fileSize >= 0)
            {
                var manager = UnigramContainer.Instance.ResolverType<IDownloadFileManager>();
                Execute.BeginOnThreadPool(() => manager.DownloadFile(location, owner, fileSize));
            }

            //using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
            //{
            //    if (userStoreForApplication.FileExists(fileName))
            //    {
            //        BitmapSource bitmapSource;
            //        BitmapSource result;
            //        try
            //        {
            //            using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(fileName, 3, 1))
            //            {
            //                isolatedStorageFileStream.Seek(0L, 0);
            //                BitmapImage bitmapImage = new BitmapImage();
            //                bitmapImage.SetSource(isolatedStorageFileStream);
            //                bitmapSource = bitmapImage;
            //            }
            //            DefaultPhotoConverter._cachedSources.set_Item(fileName, new WeakReference(bitmapSource));
            //        }
            //        catch (Exception)
            //        {
            //            result = null;
            //            return result;
            //        }
            //        result = bitmapSource;
            //        return result;
            //    }
            //    if (fileSize != null)
            //    {
            //        IFileManager fileManager = IoC.Get<IFileManager>(null);
            //        Telegram.Api.Helpers.Execute.BeginOnThreadPool(delegate
            //        {
            //            fileManager.DownloadFile(location, owner, fileSize);
            //        });
            //    }
            //}
            return null;
        }

        //private static ImageSource ReturnOrEnqueueSticker(TLDecryptedMessageMediaExternalDocument document, TLDecryptedMessage owner)
        //{
        //    if (document == null)
        //    {
        //        return null;
        //    }
        //    string documentLocalFileName = document.GetFileName();
        //    using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
        //    {
        //        if (!userStoreForApplication.FileExists(documentLocalFileName))
        //        {
        //            IoC.Get<IDocumentFileManager>(null).DownloadFileAsync(document.FileName, document.DCId, document.ToInputFileLocation(), owner, document.Size, delegate (double progress)
        //            {
        //            }, null);
        //            TLPhotoCachedSize tLPhotoCachedSize = document.Thumb as TLPhotoCachedSize;
        //            if (tLPhotoCachedSize != null)
        //            {
        //                string cacheKey = "cached" + document.GetFileName();
        //                byte[] data = tLPhotoCachedSize.Bytes.Data;
        //                ImageSource result;
        //                if (data == null)
        //                {
        //                    result = null;
        //                    return result;
        //                }
        //                result = DefaultPhotoConverter.DecodeWebPImage(cacheKey, data, delegate
        //                {
        //                });
        //                return result;
        //            }
        //            else
        //            {
        //                TLPhotoSize tLPhotoSize = document.Thumb as TLPhotoSize;
        //                if (tLPhotoSize != null)
        //                {
        //                    TLFileLocation tLFileLocation = tLPhotoSize.Location as TLFileLocation;
        //                    if (tLFileLocation != null)
        //                    {
        //                        ImageSource result = DefaultPhotoConverter.ReturnOrEnqueueStickerPreview(tLFileLocation, owner, tLPhotoSize.Size);
        //                        return result;
        //                    }
        //                }
        //            }
        //        }
        //        else if (document.Size.Value > 0 && document.Size.Value < 262144)
        //        {
        //            byte[] array;
        //            using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(documentLocalFileName, 3))
        //            {
        //                array = new byte[isolatedStorageFileStream.get_Length()];
        //                isolatedStorageFileStream.Read(array, 0, array.Length);
        //            }
        //            ImageSource result = DefaultPhotoConverter.DecodeWebPImage(documentLocalFileName, array, delegate
        //            {
        //                using (IsolatedStorageFile userStoreForApplication2 = IsolatedStorageFile.GetUserStoreForApplication())
        //                {
        //                    userStoreForApplication2.DeleteFile(documentLocalFileName);
        //                }
        //            });
        //            return result;
        //        }
        //    }
        //    return null;
        //}

        //public static ImageSource ReturnOrEnqueueSticker(TLDocument22 document, TLObject sticker)
        //{
        //    if (document == null)
        //    {
        //        return null;
        //    }
        //    string documentLocalFileName = document.GetFileName();
        //    using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
        //    {
        //        if (!userStoreForApplication.FileExists(documentLocalFileName))
        //        {
        //            TLObject owner = document;
        //            if (sticker != null)
        //            {
        //                owner = sticker;
        //            }
        //            IoC.Get<IDocumentFileManager>(null).DownloadFileAsync(document.FileName, document.DCId, document.ToInputFileLocation(), owner, document.Size, delegate (double progress)
        //            {
        //            }, null);
        //            TLPhotoCachedSize tLPhotoCachedSize = document.Thumb as TLPhotoCachedSize;
        //            if (tLPhotoCachedSize != null)
        //            {
        //                string cacheKey = "cached" + document.GetFileName();
        //                byte[] data = tLPhotoCachedSize.Bytes.Data;
        //                ImageSource result;
        //                if (data == null)
        //                {
        //                    result = null;
        //                    return result;
        //                }
        //                result = DefaultPhotoConverter.DecodeWebPImage(cacheKey, data, delegate
        //                {
        //                });
        //                return result;
        //            }
        //            else
        //            {
        //                TLPhotoSize tLPhotoSize = document.Thumb as TLPhotoSize;
        //                if (tLPhotoSize != null)
        //                {
        //                    TLFileLocation tLFileLocation = tLPhotoSize.Location as TLFileLocation;
        //                    if (tLFileLocation != null)
        //                    {
        //                        ImageSource result = DefaultPhotoConverter.ReturnOrEnqueueStickerPreview(tLFileLocation, sticker, tLPhotoSize.Size);
        //                        return result;
        //                    }
        //                }
        //            }
        //        }
        //        else if (document.DocumentSize > 0 && document.DocumentSize < 262144)
        //        {
        //            byte[] array;
        //            using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(documentLocalFileName, 3))
        //            {
        //                array = new byte[isolatedStorageFileStream.get_Length()];
        //                isolatedStorageFileStream.Read(array, 0, array.Length);
        //            }
        //            ImageSource result = DefaultPhotoConverter.DecodeWebPImage(documentLocalFileName, array, delegate
        //            {
        //                using (IsolatedStorageFile userStoreForApplication2 = IsolatedStorageFile.GetUserStoreForApplication())
        //                {
        //                    userStoreForApplication2.DeleteFile(documentLocalFileName);
        //                }
        //            });
        //            return result;
        //        }
        //    }
        //    return null;
        //}

        //public static ImageSource ReturnOrEnqueueStickerPreview(TLFileLocation location, TLObject owner, TLInt fileSize)
        //{
        //    string fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
        //    using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
        //    {
        //        if (userStoreForApplication.FileExists(fileName))
        //        {
        //            byte[] array;
        //            using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(fileName, 3))
        //            {
        //                array = new byte[isolatedStorageFileStream.get_Length()];
        //                isolatedStorageFileStream.Read(array, 0, array.Length);
        //            }
        //            return DefaultPhotoConverter.DecodeWebPImage(fileName, array, delegate
        //            {
        //                using (IsolatedStorageFile userStoreForApplication2 = IsolatedStorageFile.GetUserStoreForApplication())
        //                {
        //                    userStoreForApplication2.DeleteFile(fileName);
        //                }
        //            });
        //        }
        //        if (fileSize != null)
        //        {
        //            IFileManager fileManager = IoC.Get<IFileManager>(null);
        //            Telegram.Api.Helpers.Execute.BeginOnThreadPool(delegate
        //            {
        //                fileManager.DownloadFile(location, owner, fileSize);
        //            });
        //        }
        //    }
        //    return null;
        //}
    }
}
