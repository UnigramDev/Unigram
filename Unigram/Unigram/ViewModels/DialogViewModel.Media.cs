using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Core.Helpers;
using Unigram.Core.Models;
using Unigram.Models;
using Unigram.Services;
using Unigram.Views;
using Windows.ApplicationModel.Contacts;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        public RelayCommand<TLDocument> SendStickerCommand { get; }
        public void SendStickerExecute(TLDocument document)
        {
            SendDocument(document, null);
            Stickers.StickersService.AddRecentSticker(StickerType.Image, document, (int)(Utils.CurrentTimestamp / 1000), false);
        }

        public RelayCommand<TLDocument> SendGifCommand { get; }
        public void SendGifExecute(TLDocument document)
        {
            SendDocument(document, null);
            Stickers.StickersService.AddRecentGif(document, (int)(Utils.CurrentTimestamp / 1000));
        }

        private void SendDocument(TLDocument document, string caption)
        {
            var media = new TLMessageMediaDocument { Document = document, Caption = caption };
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message, false);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var input = new TLInputMediaDocument
                {
                    Caption = caption,
                    Id = new TLInputDocument
                    {
                        Id = document.Id,
                        AccessHash = document.AccessHash,
                    }
                };

                await ProtoService.SendMediaAsync(Peer, input, message);
            });
        }

        public RelayCommand<StorageFile> SendFileCommand { get; }
        private async void SendFileExecute(StorageFile file)
        {
            if (MediaLibrary.SelectedCount > 0)
            {
                foreach (var storage in MediaLibrary.Where(x => x.IsSelected))
                {
                    await SendFileAsync(storage.File, storage.Caption);
                }

                return;
            }

            ObservableCollection<StorageFile> storages = null;

            if (file == null)
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                var files = await picker.PickMultipleFilesAsync();
                if (files != null)
                {
                    storages = new ObservableCollection<StorageFile>(files);
                }
            }
            else
            {
                storages = new ObservableCollection<StorageFile> { file };
            }

            if (storages != null && storages.Count > 0)
            {
                foreach (var storage in storages)
                {
                    //var props = await storage.Properties.GetVideoPropertiesAsync();
                    //var width = props.Width;
                    //var height = props.Height;
                    //var x = 0d;
                    //var y = 0d;

                    //if (width > height) {
                    //    x = (width - height) / 2;
                    //    width = height;
                    //}

                    //if (height > width)
                    //{
                    //    y = (height - width) / 2;
                    //    height = width;
                    //}

                    //var transform = new VideoTransformEffectDefinition();
                    //transform.CropRectangle = new Windows.Foundation.Rect(x, y, width, height);
                    //transform.OutputSize = new Windows.Foundation.Size(240, 240);

                    //var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
                    //profile.Video.Width = 240;
                    //profile.Video.Height = 240;
                    //profile.Video.Bitrate = 300000;

                    //await SendVideoAsync(storage, null, true, transform, profile);
                    await SendFileAsync(storage, null);
                }
            }
        }

        private async Task SendFileAsync(StorageFile file, string caption = null)
        {
            if (_peer == null)
            {
                return;
            }

            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.dat", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            await file.CopyAndReplaceAsync(fileCache);

            var basicProps = await fileCache.GetBasicPropertiesAsync();
            var thumbnail = await ImageHelper.GetFileThumbnailAsync(file);
            if (thumbnail as TLPhotoSize != null)
            {
                await SendThumbnailFileAsync(file, fileLocation, fileName, basicProps, thumbnail as TLPhotoSize, fileCache, caption);
            }
            else
            {
                var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

                var document = new TLDocument
                {
                    Id = 0,
                    AccessHash = 0,
                    Date = date,
                    Size = (int)basicProps.Size,
                    MimeType = file.ContentType,
                    Attributes = new TLVector<TLDocumentAttributeBase>
                    {
                        new TLDocumentAttributeFilename
                        {
                            FileName = file.Name
                        }
                    }
                };

                var musicProps = await file.Properties.GetMusicPropertiesAsync();
                if (musicProps.Duration > TimeSpan.Zero)
                {
                    document.Attributes.Add(new TLDocumentAttributeAudio
                    {
                        Duration = (int)musicProps.Duration.TotalSeconds,
                        Title = musicProps.Title,
                        Performer = musicProps.Artist,
                        IsVoice = false,
                        HasTitle = musicProps.Title != null,
                        HasPerformer = musicProps.Artist != null,
                        HasWaveform = false
                    });
                }

                var media = new TLMessageMediaDocument
                {
                    Document = document,
                    Caption = caption
                };

                var message = TLUtils.GetMessage(SettingsHelper.UserId, _peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

                if (Reply != null)
                {
                    message.HasReplyToMsgId = true;
                    message.ReplyToMsgId = Reply.Id;
                    message.Reply = Reply;
                    Reply = null;
                }

                var previousMessage = InsertSendingMessage(message);
                CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
                {
                    var fileId = TLLong.Random();
                    var upload = await _uploadDocumentManager.UploadFileAsync(fileId, fileName, false).AsTask(Upload(media.Document as TLDocument, progress => new TLSendMessageUploadDocumentAction { Progress = progress }));
                    if (upload != null)
                    {
                        var inputMedia = new TLInputMediaUploadedDocument
                        {
                            File = upload.ToInputFile(),
                            MimeType = document.MimeType,
                            Caption = media.Caption,
                            Attributes = document.Attributes
                        };

                        var result = await ProtoService.SendMediaAsync(_peer, inputMedia, message);
                        //if (result.IsSucceeded)
                        //{
                        //    var update = result.Result as TLUpdates;
                        //    if (update != null)
                        //    {
                        //        var newMessage = update.Updates.OfType<TLUpdateNewMessage>().FirstOrDefault();
                        //        if (newMessage != null)
                        //        {
                        //            var newM = newMessage.Message as TLMessage;
                        //            if (newM != null)
                        //            {
                        //                message.Media = newM.Media;
                        //                message.RaisePropertyChanged(() => message.Media);
                        //            }
                        //        }
                        //    }
                        //}
                    }
                });
            }
        }

        private Progress<double> Upload(ITLTransferable document, Func<int, TLSendMessageActionBase> action, double delta = 0.0, double divider = 1.0)
        {
            document.IsTransferring = true;

            return new Progress<double>((value) =>
            {
                var local = value / divider;

                document.UploadingProgress = delta + local;
                document.IsTransferring = local < 1 && local > 0;
                Debug.WriteLine(value);

                OutputTypingManager.SetTyping(action((int)local * 100));
            });
        }

        private async Task SendThumbnailFileAsync(StorageFile file, TLFileLocation fileLocation, string fileName, BasicProperties basicProps, TLPhotoSize thumbnail, StorageFile fileCache, string caption)
        {
            if (_peer == null)
            {
                return;
            }

            var desiredName = string.Format("{0}_{1}_{2}.jpg", thumbnail.Location.VolumeId, thumbnail.Location.LocalId, thumbnail.Location.Secret);

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var document = new TLDocument
            {
                Id = 0,
                AccessHash = 0,
                Date = date,
                Size = (int)basicProps.Size,
                MimeType = file.ContentType,
                Thumb = thumbnail,
                Attributes = new TLVector<TLDocumentAttributeBase>
                {
                    new TLDocumentAttributeFilename
                    {
                        FileName = file.Name
                    }
                }
            };

            var musicProps = await file.Properties.GetMusicPropertiesAsync();
            if (musicProps.Duration > TimeSpan.Zero)
            {
                document.Attributes.Add(new TLDocumentAttributeAudio
                {
                    Duration = (int)musicProps.Duration.TotalSeconds,
                    Title = musicProps.Title,
                    Performer = musicProps.Artist,
                    IsVoice = false,
                    HasTitle = musicProps.Title != null,
                    HasPerformer = musicProps.Artist != null,
                    HasWaveform = false
                });
            }

            var media = new TLMessageMediaDocument
            {
                Document = document,
                Caption = caption
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, _peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var fileId = TLLong.Random();
                var upload = await _uploadDocumentManager.UploadFileAsync(fileId, fileCache.Name, false).AsTask(Upload(media.Document as TLDocument, progress => new TLSendMessageUploadDocumentAction { Progress = progress }));
                if (upload != null)
                {
                    var thumbFileId = TLLong.Random();
                    var thumbUpload = await _uploadDocumentManager.UploadFileAsync(thumbFileId, desiredName);
                    if (thumbUpload != null)
                    {
                        var inputMedia = new TLInputMediaUploadedDocument
                        {
                            File = upload.ToInputFile(),
                            Thumb = thumbUpload.ToInputFile(),
                            MimeType = document.MimeType,
                            Caption = media.Caption,
                            Attributes = document.Attributes
                        };

                        var result = await ProtoService.SendMediaAsync(_peer, inputMedia, message);
                    }
                    //if (result.IsSucceeded)
                    //{
                    //    var update = result.Result as TLUpdates;
                    //    if (update != null)
                    //    {
                    //        var newMessage = update.Updates.OfType<TLUpdateNewMessage>().FirstOrDefault();
                    //        if (newMessage != null)
                    //        {
                    //            var newM = newMessage.Message as TLMessage;
                    //            if (newM != null)
                    //            {
                    //                message.Media = newM.Media;
                    //                message.RaisePropertyChanged(() => message.Media);
                    //            }
                    //        }
                    //    }
                    //}
                }
            });
        }

        public async Task SendVideoAsync(StorageFile file, string caption, bool round, int? ttlSeconds = null, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
        {
            if (_peer == null)
            {
                return;
            }

            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.mp4", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            await file.CopyAndReplaceAsync(fileCache);

            var basicProps = await fileCache.GetBasicPropertiesAsync();
            var videoProps = await fileCache.Properties.GetVideoPropertiesAsync();

            var thumbnail = await ImageHelper.GetVideoThumbnailAsync(file, videoProps, transform) as TLPhotoSize;
            if (thumbnail == null)
            {
                return;
            }

            var desiredName = string.Format("{0}_{1}_{2}.jpg", thumbnail.Location.VolumeId, thumbnail.Location.LocalId, thumbnail.Location.Secret);

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var videoWidth = (int)videoProps.Width;
            var videoHeight = (int)videoProps.Height;

            if (profile != null)
            {
                videoWidth = (int)profile.Video.Width;
                videoHeight = (int)profile.Video.Height;
            }

            var document = new TLDocument
            {
                Id = 0,
                AccessHash = 0,
                Date = date,
                Size = (int)basicProps.Size,
                MimeType = fileCache.ContentType,
                Thumb = thumbnail,
                Attributes = new TLVector<TLDocumentAttributeBase>
                {
                    new TLDocumentAttributeFilename
                    {
                        FileName = file.Name
                    },
                    new TLDocumentAttributeVideo
                    {
                        Duration = (int)videoProps.Duration.TotalSeconds,
                        W = videoWidth,
                        H = videoHeight,
                        IsRoundMessage = round
                    }
                }
            };

            if (profile != null && profile.Audio == null)
            {
                document.Attributes.Add(new TLDocumentAttributeAnimated());
            }

            var media = new TLMessageMediaDocument
            {
                Document = document,
                HasDocument = document != null,
                Caption = caption,
                HasCaption = caption != null,
                TTLSeconds = ttlSeconds,
                HasTTLSeconds = ttlSeconds != null
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, _peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                if (profile != null)
                {
                    await fileCache.RenameAsync(fileName + ".temp.mp4");
                    var fileResult = await FileUtils.CreateTempFileAsync(fileName);

                    var transcoder = new MediaTranscoder();
                    if (transform != null)
                    {
                        transcoder.AddVideoEffect(transform.ActivatableClassId, true, transform.Properties);
                    }

                    var prepare = await transcoder.PrepareFileTranscodeAsync(fileCache, fileResult, profile);
                    if (prepare.CanTranscode)
                    {
                        await prepare.TranscodeAsync().AsTask(Upload(media.Document as TLDocument, progress => new TLSendMessageUploadVideoAction { Progress = progress }, 0, 200.0));

                        if (prepare.FailureReason == TranscodeFailureReason.None)
                        {
                            //await fileCache.DeleteAsync();
                            fileCache = fileResult;
                        }
                    }
                }

                var fileId = TLLong.Random();
                var upload = await _uploadVideoManager.UploadFileAsync(fileId, fileCache.Name, false).AsTask(Upload(media.Document as TLDocument, progress => new TLSendMessageUploadVideoAction { Progress = progress }, 0.5, 2.0));
                if (upload != null)
                {
                    var thumbFileId = TLLong.Random();
                    var thumbUpload = await _uploadDocumentManager.UploadFileAsync(thumbFileId, desiredName);
                    if (thumbUpload != null)
                    {
                        var inputMedia = new TLInputMediaUploadedDocument
                        {
                            File = upload.ToInputFile(),
                            Thumb = thumbUpload.ToInputFile(),
                            MimeType = document.MimeType,
                            Caption = media.Caption,
                            Attributes = document.Attributes,
                            TTLSeconds = ttlSeconds
                        };

                        var result = await ProtoService.SendMediaAsync(_peer, inputMedia, message);
                    }
                    //if (result.IsSucceeded)
                    //{
                    //    var update = result.Result as TLUpdates;
                    //    if (update != null)
                    //    {
                    //        var newMessage = update.Updates.OfType<TLUpdateNewMessage>().FirstOrDefault();
                    //        if (newMessage != null)
                    //        {
                    //            var newM = newMessage.Message as TLMessage;
                    //            if (newM != null)
                    //            {
                    //                message.Media = newM.Media;
                    //                message.RaisePropertyChanged(() => message.Media);
                    //            }
                    //        }
                    //    }
                    //}
                }
            });
        }

        public RelayCommand<ObservableCollection<StorageMedia>> SendMediaCommand { get; }
        private async void SendMediaExecute(ObservableCollection<StorageMedia> media)
        {
            if (MediaLibrary.SelectedCount > 0)
            {
                foreach (var storage in MediaLibrary.Where(x => x.IsSelected))
                {
                    if (storage is StoragePhoto photo)
                    {
                        var storageFile = await photo.GetFileAsync();
                        await SendPhotoAsync(storageFile, storage.Caption, storage.TTLSeconds);
                    }
                    else if (storage is StorageVideo video)
                    {
                        await SendVideoAsync(storage.File, storage.Caption, false, storage.TTLSeconds, await video.GetEncodingAsync(), video.GetTransform());
                    }
                }

                return;
            }

            ObservableCollection<StorageMedia> storages = media;

            if (media == null)
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.MediaTypes);

                var files = await picker.PickMultipleFilesAsync();
                if (files != null)
                {
                    storages = new ObservableCollection<StorageMedia>();

                    foreach (var file in files)
                    {
                        var storage = await StorageMedia.CreateAsync(file, true);
                        if (storage != null)
                        {
                            storages.Add(storage);
                        }
                    }
                }
            }
            //else
            //{
            //    storages = new ObservableCollection<StorageMedia>(media);
            //}

            if (storages != null && storages.Count > 0)
            {
                var dialog = new SendMediaView { ViewModel = this, Items = storages, SelectedItem = storages[0], IsTTLEnabled = _peer is TLInputPeerUser };
                var dialogResult = await dialog.ShowAsync();

                TextField.FocusMaybe(FocusState.Keyboard);

                if (dialogResult == ContentDialogBaseResult.OK)
                {
                    foreach (var storage in dialog.Items.Where(x => x.IsSelected))
                    {
                        if (storage is StoragePhoto photo)
                        {
                            var storageFile = await photo.GetFileAsync();
                            await SendPhotoAsync(storageFile, storage.Caption, storage.TTLSeconds);
                        }
                        else if (storage is StorageVideo video)
                        {
                            await SendVideoAsync(storage.File, storage.Caption, false, storage.TTLSeconds, await video.GetEncodingAsync(), video.GetTransform());
                        }
                    }
                }
            }
        }

        public async void SendMediaExecute(ObservableCollection<StorageMedia> media, StorageMedia selectedItem)
        {
            var storages = media;
            if (!media.Contains(selectedItem))
            {
                storages.Insert(0, selectedItem);
            }

            if (storages != null && storages.Count > 0)
            {
                var dialog = new SendMediaView { ViewModel = this, Items = storages, SelectedItem = selectedItem, IsTTLEnabled = _peer is TLInputPeerUser };
                var dialogResult = await dialog.ShowAsync();

                TextField.FocusMaybe(FocusState.Keyboard);

                if (dialogResult == ContentDialogBaseResult.OK)
                {
                    foreach (var storage in dialog.Items.Where(x => x.IsSelected))
                    {
                        if (storage is StoragePhoto photo)
                        {
                            var storageFile = await photo.GetFileAsync();
                            await SendPhotoAsync(storageFile, storage.Caption, storage.TTLSeconds);
                        }
                        else if (storage is StorageVideo video)
                        {
                            await SendVideoAsync(storage.File, storage.Caption, false, storage.TTLSeconds, await video.GetEncodingAsync(), video.GetTransform());
                        }
                    }
                }
            }
        }

        private async Task SendPhotoAsync(StorageFile file, string caption, int? ttlSeconds = null)
        {
            var originalProps = await file.Properties.GetImagePropertiesAsync();

            var imageWidth = originalProps.Width;
            var imageHeight = originalProps.Height;
            if (imageWidth >= 20 * imageHeight || imageHeight >= 20 * imageWidth)
            {
                await SendFileAsync(file, caption);
                return;
            }

            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.jpg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            StorageFile fileScale;
            try
            {
                fileScale = await ImageHelper.ScaleJpegAsync(file, fileCache, 1280, 0.77);
            }
            catch (InvalidCastException)
            {
                await fileCache.DeleteAsync();
                await SendGifAsync(file, caption);
                return;
            }

            var basicProps = await fileScale.GetBasicPropertiesAsync();
            var imageProps = await fileScale.Properties.GetImagePropertiesAsync();

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var photoSize = new TLPhotoSize
            {
                Type = "y",
                W = (int)imageProps.Width,
                H = (int)imageProps.Height,
                Location = fileLocation,
                Size = (int)basicProps.Size
            };

            var photo = new TLPhoto
            {
                Id = 0,
                AccessHash = 0,
                Date = date,
                Sizes = new TLVector<TLPhotoSizeBase> { photoSize },
            };

            var media = new TLMessageMediaPhoto
            {
                Photo = photo,
                Caption = caption,
                TTLSeconds = ttlSeconds,
                HasPhoto = true,
                HasCaption = caption != null,
                HasTTLSeconds = ttlSeconds.HasValue
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var fileId = TLLong.Random();
                var upload = await _uploadFileManager.UploadFileAsync(fileId, fileCache.Name, false).AsTask(Upload(photo, progress => new TLSendMessageUploadPhotoAction { Progress = progress }));
                if (upload != null)
                {
                    var inputMedia = new TLInputMediaUploadedPhoto
                    {
                        File = upload.ToInputFile(),
                        Caption = media.Caption,
                        TTLSeconds = ttlSeconds
                    };

                    var response = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                    //if (response.IsSucceeded && response.Result is TLUpdates updates)
                    //{
                    //    TLPhoto newPhoto = null;

                    //    var newMessageUpdate = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                    //    if (newMessageUpdate != null && newMessageUpdate.Message is TLMessage newMessage && newMessage.Media is TLMessageMediaPhoto newPhotoMedia)
                    //    {
                    //        newPhoto = newPhotoMedia.Photo as TLPhoto;
                    //    }

                    //    var newChannelMessageUpdate = updates.Updates.FirstOrDefault(x => x is TLUpdateNewChannelMessage) as TLUpdateNewChannelMessage;
                    //    if (newChannelMessageUpdate != null && newMessageUpdate.Message is TLMessage newChannelMessage && newChannelMessage.Media is TLMessageMediaPhoto newChannelPhotoMedia)
                    //    {
                    //        newPhoto = newChannelPhotoMedia.Photo as TLPhoto;
                    //    }

                    //    if (newPhoto != null && newPhoto.Full is TLPhotoSize newFull && newFull.Location is TLFileLocation newLocation)
                    //    {
                    //        var newFileName = string.Format("{0}_{1}_{2}.jpg", newLocation.VolumeId, newLocation.LocalId, newLocation.Secret);
                    //        var newFile = await FileUtils.CreateTempFileAsync(newFileName);
                    //        await fileCache.CopyAndReplaceAsync(newFile);
                    //    }
                    //}
                }
            });
        }

        private async Task SendGifAsync(StorageFile file, string caption)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.gif", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            await file.CopyAndReplaceAsync(fileCache);

            var basicProps = await fileCache.GetBasicPropertiesAsync();
            var imageProps = await fileCache.Properties.GetImagePropertiesAsync();
            var thumbnailBase = await ImageHelper.GetFileThumbnailAsync(file);
            var thumbnail = thumbnailBase as TLPhotoSize;

            var desiredName = string.Format("{0}_{1}_{2}.jpg", thumbnail.Location.VolumeId, thumbnail.Location.LocalId, thumbnail.Location.Secret);

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var document = new TLDocument
            {
                Id = 0,
                AccessHash = 0,
                Date = date,
                Size = (int)basicProps.Size,
                MimeType = fileCache.ContentType,
                Thumb = thumbnail,
                Attributes = new TLVector<TLDocumentAttributeBase>
                {
                    new TLDocumentAttributeAnimated(),
                    new TLDocumentAttributeFilename
                    {
                        FileName = file.Name
                    },
                    new TLDocumentAttributeImageSize
                    {
                        W = (int)imageProps.Width,
                        H = (int)imageProps.Height
                    },
                    new TLDocumentAttributeVideo
                    {
                        W = (int)imageProps.Width,
                        H = (int)imageProps.Height,
                    }
                }
            };

            var media = new TLMessageMediaDocument
            {
                Caption = caption,
                Document = document
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var fileId = TLLong.Random();
                var upload = await _uploadDocumentManager.UploadFileAsync(fileId, fileName, false).AsTask(media.Document.Upload());
                if (upload != null)
                {
                    var thumbFileId = TLLong.Random();
                    var thumbUpload = await _uploadDocumentManager.UploadFileAsync(thumbFileId, desiredName);
                    if (thumbUpload != null)
                    {
                        var inputMedia = new TLInputMediaUploadedDocument
                        {
                            File = upload.ToInputFile(),
                            Thumb = thumbUpload.ToInputFile(),
                            MimeType = document.MimeType,
                            Caption = media.Caption,
                            Attributes = document.Attributes
                        };

                        var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                    }
                }
            });
        }

        public async Task SendAudioAsync(StorageFile file, int duration, bool voice, string title, string performer, string caption)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.ogg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            await file.CopyAndReplaceAsync(fileCache);

            var basicProps = await fileCache.GetBasicPropertiesAsync();
            var imageProps = await fileCache.Properties.GetImagePropertiesAsync();

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var media = new TLMessageMediaDocument
            {
                Caption = caption,
                Document = new TLDocument
                {
                    Id = TLLong.Random(),
                    AccessHash = TLLong.Random(),
                    Date = date,
                    MimeType = "audio/ogg",
                    Size = (int)basicProps.Size,
                    Thumb = new TLPhotoSizeEmpty
                    {
                        Type = string.Empty
                    },
                    Version = 0,
                    DCId = 0,
                    Attributes = new TLVector<TLDocumentAttributeBase>
                    {
                        new TLDocumentAttributeAudio
                        {
                            IsVoice = voice,
                            Duration = duration,
                            Title = title,
                            Performer = performer
                        }
                    }
                }
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var fileId = TLLong.Random();
                var upload = await _uploadAudioManager.UploadFileAsync(fileId, fileName, false).AsTask(Upload(media.Document as TLDocument, progress => new TLSendMessageUploadAudioAction { Progress = progress }));
                if (upload != null)
                {
                    var inputMedia = new TLInputMediaUploadedDocument
                    {
                        File = upload.ToInputFile(),
                        MimeType = "audio/ogg",
                        Caption = media.Caption,
                        Attributes = new TLVector<TLDocumentAttributeBase>
                        {
                            new TLDocumentAttributeAudio
                            {
                                IsVoice = voice,
                                Duration = duration,
                                Title = title,
                                Performer = performer
                            }
                        }
                    };

                    var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                }
            });
        }

        public RelayCommand SendContactCommand { get; }
        private async void SendContactExecute()
        {
            var picker = new ContactPicker();
            picker.SelectionMode = ContactSelectionMode.Fields;
            picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.PhoneNumber);

            var contact = await picker.PickContactAsync();
            if (contact != null)
            {
                TLUser user = null;

                var annotationStore = await ContactManager.RequestAnnotationStoreAsync(ContactAnnotationStoreAccessType.AppAnnotationsReadWrite);
                var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);
                if (store != null && annotationStore != null)
                {
                    var full = await store.GetContactAsync(contact.Id);
                    if (full != null)
                    {
                        var annotations = await annotationStore.FindAnnotationsForContactAsync(full);

                        var first = annotations.FirstOrDefault();
                        if (first != null)
                        {
                            var remote = first.RemoteId;
                            if (int.TryParse(remote.Substring(1), out int userId))
                            {
                                user = CacheService.GetUser(userId) as TLUser;
                            }
                        }

                        //contact = full;
                    }
                }

                if (user == null)
                {
                    var phone = contact.Phones.FirstOrDefault();
                    if (phone == null)
                    {
                        return;
                    }

                    user = new TLUser();
                    user.FirstName = contact.FirstName;
                    user.LastName = contact.LastName;
                    user.Phone = phone.Number;
                }

                if (user != null)
                {
                    await SendContactAsync(user);
                }
            }
        }

        public Task<bool> SendContactAsync(TLUser user)
        {
            var tsc = new TaskCompletionSource<bool>();
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var media = new TLMessageMediaContact
            {
                PhoneNumber = user.Phone,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserId = user.Id,
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var inputMedia = new TLInputMediaContact
                {
                    PhoneNumber = user.Phone,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                };

                var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                if (result.IsSucceeded)
                {
                    tsc.SetResult(true);
                }
                else
                {
                    tsc.SetResult(false);
                }
            });

            return tsc.Task;
        }

        public RelayCommand SendLocationCommand { get; }
        private async void SendLocationExecute()
        {
            var page = new DialogSendLocationPage();

            var dialog = new ContentDialogBase();
            dialog.Content = page;

            page.Dialog = dialog;
            page.LiveLocation = !_liveLocationService.IsTracking(Peer.ToPeer());

            var confirm = await dialog.ShowAsync();
            if (confirm == ContentDialogBaseResult.OK)
            {
                if (page.Media is TLMessageMediaVenue venue)
                {
                    await SendGeoAsync(venue);
                }
                else if (page.Media is TLMessageMediaGeoLive geoLive)
                {
                    if (geoLive.Geo == null || geoLive.Period == 0 || _liveLocationService.IsTracking(Peer.ToPeer()))
                    {
                        _liveLocationService.StopTracking(Peer.ToPeer());
                    }
                    else
                    {
                        await SendGeoAsync(geoLive);
                    }
                }
                else if (page.Media is TLMessageMediaGeo geo && geo.Geo is TLGeoPoint geoPoint)
                {
                    await SendGeoAsync(geoPoint.Lat, geoPoint.Long);
                }
            }

            //NavigationService.Navigate(typeof(DialogSendLocationPage));
        }

        public Task<bool> SendGeoAsync(double latitude, double longitude)
        {
            var tsc = new TaskCompletionSource<bool>();
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var media = new TLMessageMediaGeo
            {
                Geo = new TLGeoPoint
                {
                    Lat = latitude,
                    Long = longitude
                }
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var inputMedia = new TLInputMediaGeoPoint
                {
                    GeoPoint = new TLInputGeoPoint
                    {
                        Lat = latitude,
                        Long = longitude
                    }
                };

                var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                if (result.IsSucceeded)
                {
                    tsc.SetResult(true);
                }
                else
                {
                    tsc.SetResult(false);
                }
            });

            return tsc.Task;
        }

        public Task<bool> SendGeoAsync(TLMessageMediaGeoLive media)
        {
            var tsc = new TaskCompletionSource<bool>();
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var inputMedia = media.ToInputMedia();

                var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                if (result.IsSucceeded)
                {
                    tsc.SetResult(true);
                    await _liveLocationService.TrackAsync(message);
                }
                else
                {
                    tsc.SetResult(false);
                }
            });

            return tsc.Task;
        }

        public Task<bool> SendGeoAsync(TLMessageMediaVenue media)
        {
            var tsc = new TaskCompletionSource<bool>();
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var inputMedia = media.ToInputMedia();

                var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                if (result.IsSucceeded)
                {
                    tsc.SetResult(true);
                }
                else
                {
                    tsc.SetResult(false);
                }
            });

            return tsc.Task;
        }
    }
}
