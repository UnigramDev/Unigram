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
using Unigram.Services;
using Unigram.Views;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        public RelayCommand<TLDocument> SendStickerCommand => new RelayCommand<TLDocument>(SendStickerExecute);
        public void SendStickerExecute(TLDocument document)
        {
            SendDocument(document, null);
            Stickers.StickersService.AddRecentSticker(StickerType.Image, document, (int)(Utils.CurrentTimestamp / 1000));
        }

        public RelayCommand<TLDocument> SendGifCommand => new RelayCommand<TLDocument>(SendGifExecute);
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

        public RelayCommand<StorageFile> SendFileCommand => new RelayCommand<StorageFile>(SendFileExecute);
        private async void SendFileExecute(StorageFile file)
        {
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

        private async Task SendFileAsync(StorageFile file, string caption)
        {
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
            var thumbnail = await FileUtils.GetFileThumbnailAsync(file);
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
                    MimeType = fileCache.ContentType,
                    Attributes = new TLVector<TLDocumentAttributeBase>
                {
                    new TLDocumentAttributeFilename
                    {
                        FileName = file.Name
                    }
                }
                };

                var media = new TLMessageMediaDocument
                {
                    Document = document,
                    Caption = caption
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

                        var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
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

                document.IsTransferring = local < 1 && local > 0;
                document.UploadingProgress = delta + local;
                Debug.WriteLine(value);

                OutputTypingManager.SetTyping(action((int)local * 100));
            });
        }

        private async Task SendThumbnailFileAsync(StorageFile file, TLFileLocation fileLocation, string fileName, BasicProperties basicProps, TLPhotoSize thumbnail, StorageFile fileCache, string caption)
        {
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
                    new TLDocumentAttributeFilename
                    {
                        FileName = file.Name
                    }
                }
            };

            var media = new TLMessageMediaDocument
            {
                Document = document,
                Caption = caption
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
                var upload = await _uploadDocumentManager.UploadFileAsync(fileId, fileCache.Name, false).AsTask(Upload(media.Document as TLDocument, progress => new TLSendMessageUploadDocumentAction { Progress = progress }));
                if (upload != null)
                {
                    var thumbFileId = TLLong.Random();
                    var thumbUpload = await _uploadDocumentManager.UploadFileAsync(thumbFileId, desiredName);
                    if (thumbUpload != null)
                    {
                        var inputMedia = new TLInputMediaUploadedThumbDocument
                        {
                            File = upload.ToInputFile(),
                            Thumb = thumbUpload.ToInputFile(),
                            MimeType = document.MimeType,
                            Caption = media.Caption,
                            Attributes = document.Attributes
                        };

                        var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
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

        public async Task SendVideoAsync(StorageFile file, string caption, bool round, VideoTransformEffectDefinition transform = null, MediaEncodingProfile profile = null)
        {
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
            var thumbnailBase = await FileUtils.GetFileThumbnailAsync(file);
            var thumbnail = thumbnailBase as TLPhotoSize;

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

            var media = new TLMessageMediaDocument
            {
                Document = document,
                Caption = caption
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
                if (transform != null && profile != null)
                {
                    await fileCache.RenameAsync(fileName + ".temp.mp4");
                    var fileResult = await FileUtils.CreateTempFileAsync(fileName);

                    var transcoder = new MediaTranscoder();
                    transcoder.AddVideoEffect(transform.ActivatableClassId, true, transform.Properties);

                    var prepare = await transcoder.PrepareFileTranscodeAsync(fileCache, fileResult, profile);
                    await prepare.TranscodeAsync().AsTask(Upload(media.Document as TLDocument, progress => new TLSendMessageUploadDocumentAction { Progress = progress }, 0, 200.0));

                    //await fileCache.DeleteAsync();
                    fileCache = fileResult;

                    thumbnailBase = await FileUtils.GetFileThumbnailAsync(fileCache);
                    thumbnail = thumbnailBase as TLPhotoSize;

                    desiredName = string.Format("{0}_{1}_{2}.jpg", thumbnail.Location.VolumeId, thumbnail.Location.LocalId, thumbnail.Location.Secret);
                    document.Thumb = thumbnail;
                }

                var fileId = TLLong.Random();
                var upload = await _uploadVideoManager.UploadFileAsync(fileId, fileCache.Name, false).AsTask(Upload(media.Document as TLDocument, progress => new TLSendMessageUploadDocumentAction { Progress = progress }, 0.5, 2.0));
                if (upload != null)
                {
                    var thumbFileId = TLLong.Random();
                    var thumbUpload = await _uploadDocumentManager.UploadFileAsync(thumbFileId, desiredName);
                    if (thumbUpload != null)
                    {
                        var inputMedia = new TLInputMediaUploadedThumbDocument
                        {
                            File = upload.ToInputFile(),
                            Thumb = thumbUpload.ToInputFile(),
                            MimeType = document.MimeType,
                            Caption = media.Caption,
                            Attributes = document.Attributes
                        };

                        var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
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

        public RelayCommand<StoragePhoto> SendPhotoCommand => new RelayCommand<StoragePhoto>(SendPhotoExecute);
        private async void SendPhotoExecute(StoragePhoto file)
        {
            ObservableCollection<StorageMedia> storages = null;

            if (file == null)
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.MediaTypes);

                var files = await picker.PickMultipleFilesAsync();
                if (files != null)
                {
                    storages = new ObservableCollection<StorageMedia>(files.Select(x => x.Name.EndsWith(".mp4") ? new StorageVideo(x) : (StorageMedia)new StoragePhoto(x)));
                }
            }
            else
            {
                storages = new ObservableCollection<StorageMedia> { file };
            }

            if (storages != null && storages.Count > 0)
            {
                var dialog = new SendPhotosView { Items = storages, SelectedItem = storages[0] };
                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogBaseResult.OK)
                {
                    foreach (var storage in dialog.Items)
                    {
                        if (storage is StoragePhoto)
                        {
                            await SendPhotoAsync(storage.File, storage.Caption);
                        }
                    }
                }
            }
        }

        public async void SendPhotoDrop(ObservableCollection<StorageFile> files)
        {
            ObservableCollection<StorageMedia> storages = null;

            if (files != null)
            {
                storages = new ObservableCollection<StorageMedia>(files.Select(x => new StoragePhoto(x)));
            }

            if (storages != null && storages.Count > 0)
            {
                var dialog = new SendPhotosView { Items = storages, SelectedItem = storages[0] };
                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogBaseResult.OK)
                {
                    foreach (var storage in dialog.Items)
                    {
                        await SendPhotoAsync(storage.File, storage.Caption);
                    }
                }
            }
        }

        private async Task SendPhotoAsync(StorageFile file, string caption)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.jpg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            var fileScale = await ImageHelper.ScaleJpegAsync(file, fileCache, 1280, 0.77);
            if (fileScale == null && Path.GetExtension(file.Name).Equals(".gif"))
            {
                // TODO: animated gif!
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
                Sizes = new TLVector<TLPhotoSizeBase> { photoSize }
            };

            var media = new TLMessageMediaPhoto
            {
                Photo = photo,
                Caption = caption
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
                        Caption = media.Caption,
                        File = upload.ToInputFile()
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
            var thumbnailBase = await FileUtils.GetFileThumbnailAsync(file);
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
                        var inputMedia = new TLInputMediaUploadedThumbDocument
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

        public RelayCommand SendLocationCommand => new RelayCommand(SendLocationExecute);
        private void SendLocationExecute()
        {
            NavigationService.Navigate(typeof(DialogSendLocationPage));
        }

        public Task<bool> SendGeoPointAsync(double latitude, double longitude)
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
    }
}
