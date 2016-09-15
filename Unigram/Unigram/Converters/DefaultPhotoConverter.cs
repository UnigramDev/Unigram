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
using Unigram.Core.Dependency;
using Unigram.WebP;
using Windows.Graphics.Imaging;
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
            return Convert(value, parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public static object Convert(object value)
        {
            return Convert(value, null);
        }

        public static object Convert(object value, object parameter)
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

            var userProfilePhoto = value as TLUserProfilePhoto;
            if (userProfilePhoto != null)
            {
                var fileLocation = userProfilePhoto.PhotoSmall as TLFileLocation;
                if (fileLocation != null)
                {
                    return ReturnOrEnqueueProfileImage(fileLocation, userProfilePhoto, 0);
                }
            }

            var chatPhoto = value as TLChatPhoto;
            if (chatPhoto != null)
            {
                var fileLocation = chatPhoto.PhotoSmall as TLFileLocation;
                if (fileLocation != null)
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
                        return ReturnOrEnqueueImage(false, fileLocation, photo, photoSize.Size, photoMedia);
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
                if (TLMessage.IsSticker(tLDocument3))
                {
                    if (parameter != null && string.Equals(parameter.ToString(), "ignoreStickers", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    return DefaultPhotoConverter.ReturnOrEnqueueSticker(tLDocument3, null);
                }
                else
                {
                    var photoSize = tLDocument3.Thumb as TLPhotoSize;
                    if (photoSize != null)
                    {
                        var fileLocation = photoSize.Location as TLFileLocation;
                        if (fileLocation != null)
                        {
                            return ReturnOrEnqueueImage(false, fileLocation, tLDocument3, photoSize.Size, null);
                        }
                    }

                    var photoCachedSize = tLDocument3.Thumb as TLPhotoCachedSize;
                    if (photoCachedSize != null)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.SetByteSource(photoCachedSize.Bytes);
                        return bitmap;
                    }
                }
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
                            return DefaultPhotoConverter.ReturnOrEnqueueImage(false, fileLocation, webpage, photoSize.Size, null);
                        }
                    }
                }
            }

            return null;
        }

        private static ImageSource DecodeWebPImage(string cacheKey, byte[] buffer, Action faultCallback = null)
        {
            try
            {
                WeakReference<WriteableBitmap> weakReference;
                WriteableBitmap writeableBitmap;
                if (_cachedWebPImages.TryGetValue(cacheKey, out weakReference) && weakReference.TryGetTarget(out writeableBitmap))
                {
                    return writeableBitmap;
                }

                try
                {
                    var result = WebPImage.DecodeFromByteArray(buffer);
                    _cachedWebPImages[cacheKey] = new WeakReference<WriteableBitmap>(result);

                    GC.Collect();

                    return result;
                }
                catch
                {
                    faultCallback?.Invoke();
                }
            }
            catch (Exception e)
            {
                TLUtils.WriteException("WebPDecode ex ", e);
            }

            return null;
        }

        public static BitmapImage ReturnImage(TLFileLocation location)
        {
            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName)))
            {
                var bitmap = new BitmapImage();
                bitmap.SetUriSource(new Uri("ms-appdata:///local/" + fileName));
                return bitmap;
            }

            return null;
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

        public static BitmapImage ReturnOrEnqueueImage(bool checkChatSettings, TLFileLocation location, TLObject owner, int fileSize, TLMessageMediaPhoto mediaPhoto)
        {
            string fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName)))
            {
                var bitmap = new BitmapImage();
                bitmap.SetUriSource(new Uri("ms-appdata:///local/" + fileName));
                return bitmap;
            }

            if (fileSize >= 0)
            {
                var manager = UnigramContainer.Instance.ResolverType<IDownloadFileManager>();
                var bitmap = new BitmapImage();

                //Execute.BeginOnThreadPool(() => manager.DownloadFile(location, owner, fileSize));
                Execute.BeginOnThreadPool(async () =>
                {
                    await manager.DownloadFileAsync(location, owner, fileSize);
                    Execute.BeginOnUIThread(() =>
                    {
                        bitmap.SetUriSource(new Uri("ms-appdata:///local/" + fileName));
                    });
                });

                return bitmap;
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

        public static BitmapSource ReturnOrEnqueueProfileImage(TLFileLocation location, TLObject owner, int fileSize)
        {
            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            WeakReference weakReference;
            if (_cachedSources.TryGetValue(fileName, out weakReference) && weakReference.IsAlive)
            {
                return weakReference.Target as BitmapSource;
            }

            if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName)))
            {
                var bitmap = new BitmapImage();
                bitmap.SetUriSource(new Uri("ms-appdata:///local/" + fileName));
                _cachedSources[fileName] = new WeakReference(bitmap);

                return bitmap;
            }

            if (fileSize >= 0)
            {
                var manager = UnigramContainer.Instance.ResolverType<IDownloadFileManager>();
                var bitmap = new BitmapImage();
                _cachedSources[fileName] = new WeakReference(bitmap);

                Execute.BeginOnThreadPool(async () =>
                {
                    await manager.DownloadFileAsync(location, owner, fileSize);
                    Execute.BeginOnUIThread(() =>
                    {
                        bitmap.SetUriSource(new Uri("ms-appdata:///local/" + fileName));
                    });
                });

                return bitmap;
            }

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

        public static ImageSource EnqueueStickerPreview(TLFileLocation location, TLObject owner, TLPhotoSize photoSize, BitmapImage bitmap)
        {
            string filename = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, filename)))
            {
                byte[] array = File.ReadAllBytes(Path.Combine(ApplicationData.Current.LocalFolder.Path, filename));

                return DecodeWebPImage(filename, array, delegate
                {
                    File.Delete(Path.Combine(ApplicationData.Current.LocalFolder.Path, filename));
                });
            }

            if (photoSize != null)
            {
                Debug.WriteLine("Download");

                var manager = UnigramContainer.Instance.ResolverType<IDownloadFileManager>();
                Execute.BeginOnThreadPool(async () =>
                {
                    await manager.DownloadFileAsync(location, owner, photoSize.Size);
                    var buffer = WebPImage.Encode(File.ReadAllBytes(Path.Combine(ApplicationData.Current.LocalFolder.Path, filename)));
                    Execute.BeginOnUIThread(() =>
                    {
                        bitmap.SetSource(buffer);
                    });
                });

                return bitmap;
            }

            return null;
        }

        public static ImageSource ReturnOrEnqueueSticker(TLDocument document, TLObject sticker)
        {
            if (document == null)
            {
                return null;
            }

            var filename = document.GetFileName();

            if (!File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, filename)))
            {
                TLObject owner = document;
                if (sticker != null)
                {
                    owner = sticker;
                }

                Debug.WriteLine("Download");

                var bitmap = new BitmapImage();
                var manager = UnigramContainer.Instance.ResolverType<IDownloadDocumentFileManager>();
                Execute.BeginOnThreadPool(async () =>
                {
                    await manager.DownloadFileAsync(document.FileName, document.DCId, document.ToInputFileLocation(), owner, document.Size);
                    var buffer = WebPImage.Encode(File.ReadAllBytes(Path.Combine(ApplicationData.Current.LocalFolder.Path, filename)));
                    Execute.BeginOnUIThread(() =>
                    {
                        bitmap.SetSource(buffer);
                    });
                });

                var cachedSize = document.Thumb as TLPhotoCachedSize;
                if (cachedSize != null)
                {
                    var cacheKey = "cached" + document.GetFileName();
                    var data = cachedSize.Bytes;
                    if (data == null)
                    {
                        return null;
                    }

                    return DecodeWebPImage(cacheKey, data, () => { });
                }
                else
                {
                    var photoSize = document.Thumb as TLPhotoSize;
                    if (photoSize != null)
                    {
                        var location = photoSize.Location as TLFileLocation;
                        if (location != null)
                        {
                            return EnqueueStickerPreview(location, sticker, photoSize, bitmap);
                        }
                    }
                }
            }
            else if (document.Size > 0 && document.Size < 262144)
            {
                var array = File.ReadAllBytes(Path.Combine(ApplicationData.Current.LocalFolder.Path, filename));
                return DecodeWebPImage(filename, array, delegate
                {
                    File.Delete(Path.Combine(ApplicationData.Current.LocalFolder.Path, filename));
                });
            }

            return null;

            //if (document == null)
            //{
            //    return null;
            //}
            //string documentLocalFileName = document.GetFileName();
            //using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
            //{
            //    if (!userStoreForApplication.FileExists(documentLocalFileName))
            //    {
            //        TLObject owner = document;
            //        if (sticker != null)
            //        {
            //            owner = sticker;
            //        }
            //        UnigramContainer.Instance.ResolverType<IDownloadDocumentFileManager>().DownloadFileAsync(document.FileName, document.DCId, document.ToInputFileLocation(), owner, document.Size, delegate (double progress)
            //        {
            //        }, null);

            //        UnigramContainer.Instance.ResolverType<IDownloadDocumentFileManager>()
            //        TLPhotoCachedSize tLPhotoCachedSize = document.Thumb as TLPhotoCachedSize;
            //        if (tLPhotoCachedSize != null)
            //        {
            //            string cacheKey = "cached" + document.GetFileName();
            //            byte[] data = tLPhotoCachedSize.Bytes.Data;
            //            ImageSource result;
            //            if (data == null)
            //            {
            //                result = null;
            //                return result;
            //            }
            //            result = DefaultPhotoConverter.DecodeWebPImage(cacheKey, data, delegate
            //            {
            //            });
            //            return result;
            //        }
            //        else
            //        {
            //            var photoSize = document.Thumb as TLPhotoSize;
            //            if (photoSize != null)
            //            {
            //                var fileLocation = photoSize.Location as TLFileLocation;
            //                if (fileLocation != null)
            //                {
            //                    return DefaultPhotoConverter.ReturnOrEnqueueStickerPreview(fileLocation, sticker, photoSize.Size);
            //                }
            //            }
            //        }
            //    }
            //    else if (document.DocumentSize > 0 && document.DocumentSize < 262144)
            //    {
            //        byte[] array;
            //        using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(documentLocalFileName, 3))
            //        {
            //            array = new byte[isolatedStorageFileStream.get_Length()];
            //            isolatedStorageFileStream.Read(array, 0, array.Length);
            //        }
            //        ImageSource result = DefaultPhotoConverter.DecodeWebPImage(documentLocalFileName, array, delegate
            //        {
            //            using (IsolatedStorageFile userStoreForApplication2 = IsolatedStorageFile.GetUserStoreForApplication())
            //            {
            //                userStoreForApplication2.DeleteFile(documentLocalFileName);
            //            }
            //        });
            //        return result;
            //    }
            //}
            //return null;
        }

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
