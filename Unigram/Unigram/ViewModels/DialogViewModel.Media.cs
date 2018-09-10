using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.Core.Helpers;
using Unigram.Core.Models;
using Unigram.Entities;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Dialogs;
using Windows.ApplicationModel.Contacts;
using Windows.Foundation;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        public RelayCommand<Sticker> SendStickerCommand { get; }
        public async void SendStickerExecute(Sticker sticker)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var restricted = await VerifyRightsAsync(chat, x => x.CanSendOtherMessages, Strings.Resources.AttachStickersRestrictedForever, Strings.Resources.AttachStickersRestricted);
            if (restricted)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageSticker(new InputFileId(sticker.StickerValue.Id), null, 0, 0);

            await SendMessageAsync(reply, input);
        }

        public RelayCommand<Animation> SendAnimationCommand { get; }
        public async void SendAnimationExecute(Animation animation)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var restricted = await VerifyRightsAsync(chat, x => x.CanSendOtherMessages, Strings.Resources.AttachStickersRestrictedForever, Strings.Resources.AttachStickersRestricted);
            if (restricted)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageSticker(new InputFileId(animation.AnimationValue.Id), null, 0, 0);

            await SendMessageAsync(reply, input);
        }

        public async Task<bool> VerifyRightsAsync(Chat chat, Func<ChatMemberStatusRestricted, bool> permission, string forever, string temporary)
        {
            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return false;
                }

                if (supergroup.Status is ChatMemberStatusRestricted restricted && !permission(restricted))
                {
                    if (restricted.IsForever())
                    {
                        await TLMessageDialog.ShowAsync(forever, Strings.Resources.AppName, Strings.Resources.OK);
                    }
                    else
                    {
                        await TLMessageDialog.ShowAsync(string.Format(temporary, BindConvert.Current.BannedUntil(restricted.RestrictedUntilDate)), Strings.Resources.AppName, Strings.Resources.OK);
                    }

                    return true;
                }
            }

            return false;
        }

        public RelayCommand SendFileCommand { get; }
        private async void SendFileExecute()
        {
            if (MediaLibrary.SelectedCount > 0)
            {
                foreach (var storage in MediaLibrary.Where(x => x.IsSelected))
                {
                    await SendFileAsync(storage.File, storage.Caption);
                }

                return;
            }

            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                foreach (var storage in files)
                {
                    await SendFileAsync(storage, null);
                }
            }
        }

        public async void SendFileExecute(IList<StorageFile> files)
        {
            foreach (var file in files)
            {
                await SendFileAsync(file, null);
            }
        }

        private async Task SendFileAsync(StorageFile file, string caption = null)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageDocument(await file.ToGeneratedAsync(), null, GetFormattedText(caption));

            await SendMessageAsync(reply, input);
        }

        public class VideoConversion
        {
            public bool Transcode { get; set; }
            public bool Mute { get; set; }
            public uint Width { get; set; }
            public uint Height { get; set; }
            public uint Bitrate { get; set; }

            public bool Transform { get; set; }
            public MediaRotation Rotation { get; set; }
            public Size OutputSize { get; set; }
            public MediaMirroringOptions Mirror { get; set; }
            public Rect CropRectangle { get; set; }
        }

        public async Task SendVideoAsync(StorageFile file, string caption, bool animated, bool asFile, int? ttl = null, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
        {
            var basicProps = await file.GetBasicPropertiesAsync();
            var videoProps = await file.Properties.GetVideoPropertiesAsync();

            //var thumbnail = await ImageHelper.GetVideoThumbnailAsync(file, videoProps, transform);

            var videoWidth = (int)videoProps.GetWidth();
            var videoHeight = (int)videoProps.GetHeight();

            if (profile != null)
            {
                videoWidth = videoProps.Orientation == VideoOrientation.Rotate180 || videoProps.Orientation == VideoOrientation.Normal ? (int)profile.Video.Width : (int)profile.Video.Height;
                videoHeight = videoProps.Orientation == VideoOrientation.Rotate180 || videoProps.Orientation == VideoOrientation.Normal ? (int)profile.Video.Height : (int)profile.Video.Width;
            }

            var conversion = new VideoConversion();
            if (profile != null)
            {
                conversion.Transcode = true;
                conversion.Mute = profile.Audio == null;
                conversion.Width = profile.Video.Width;
                conversion.Height = profile.Video.Height;
                conversion.Bitrate = profile.Video.Bitrate;

                if (transform != null)
                {
                    conversion.Transform = true;
                    conversion.Rotation = transform.Rotation;
                    conversion.OutputSize = transform.OutputSize;
                    conversion.Mirror = transform.Mirror;
                    conversion.CropRectangle = transform.CropRectangle;
                }
            }

            var generated = await file.ToGeneratedAsync("transcode#" + JsonConvert.SerializeObject(conversion));
            var thumbnail = await file.ToThumbnailAsync(conversion, "thumbnail_transcode#" + JsonConvert.SerializeObject(conversion));

            if (asFile)
            {
                var reply = GetReply(true);
                var input = new InputMessageDocument(generated, thumbnail, GetFormattedText(caption));

                await SendMessageAsync(reply, input);
            }
            else
            {
                if (profile != null && profile.Audio == null)
                {
                    var reply = GetReply(true);
                    var input = new InputMessageAnimation(generated, thumbnail, (int)videoProps.Duration.TotalSeconds, videoWidth, videoHeight, GetFormattedText(caption));

                    await SendMessageAsync(reply, input);
                }
                else
                {
                    var reply = GetReply(true);
                    var input = new InputMessageVideo(generated, thumbnail, new int[0], (int)videoProps.Duration.TotalSeconds, videoWidth, videoHeight, true, GetFormattedText(caption), ttl ?? 0);

                    await SendMessageAsync(reply, input);
                }
            }
        }

        public async Task SendVideoNoteAsync(StorageFile file, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
        {
            var basicProps = await file.GetBasicPropertiesAsync();
            var videoProps = await file.Properties.GetVideoPropertiesAsync();

            //var thumbnail = await ImageHelper.GetVideoThumbnailAsync(file, videoProps, transform);

            var videoWidth = (int)videoProps.GetWidth();
            var videoHeight = (int)videoProps.GetHeight();

            if (profile != null)
            {
                videoWidth = videoProps.Orientation == VideoOrientation.Rotate180 || videoProps.Orientation == VideoOrientation.Normal ? (int)profile.Video.Width : (int)profile.Video.Height;
                videoHeight = videoProps.Orientation == VideoOrientation.Rotate180 || videoProps.Orientation == VideoOrientation.Normal ? (int)profile.Video.Height : (int)profile.Video.Width;
            }

            var conversion = new VideoConversion();
            if (profile != null)
            {
                conversion.Transcode = true;
                conversion.Width = profile.Video.Width;
                conversion.Height = profile.Video.Height;
                conversion.Bitrate = profile.Video.Bitrate;

                if (transform != null)
                {
                    conversion.Transform = true;
                    conversion.Rotation = transform.Rotation;
                    conversion.OutputSize = transform.OutputSize;
                    conversion.Mirror = transform.Mirror;
                    conversion.CropRectangle = transform.CropRectangle;
                }
            }

            var generated = await file.ToGeneratedAsync("transcode#" + JsonConvert.SerializeObject(conversion));
            var thumbnail = await file.ToThumbnailAsync(conversion, "thumbnail_transcode#" + JsonConvert.SerializeObject(conversion));

            var reply = GetReply(true);
            var input = new InputMessageVideoNote(generated, thumbnail, (int)videoProps.Duration.TotalSeconds, Math.Min(videoWidth, videoHeight));

            await SendMessageAsync(reply, input);
        }

        public RelayCommand SendMediaCommand { get; }
        private async void SendMediaExecute()
        {
            if (MediaLibrary.SelectedCount > 0)
            {
                if (Settings.IsSendGrouped && MediaLibrary.SelectedCount > 1)
                {
                    var items = MediaLibrary.Where(x => x.IsSelected).ToList();
                    var group = new List<StorageMedia>(Math.Min(items.Count, 10));

                    foreach (var item in items)
                    {
                        group.Add(item);

                        if (group.Count == 10)
                        {
                            await SendGroupedAsync(group);
                            group = new List<StorageMedia>(Math.Min(items.Count, 10));
                        }
                    }

                    if (group.Count > 0)
                    {
                        await SendGroupedAsync(group);
                    }
                }
                else
                {
                    foreach (var storage in MediaLibrary.Where(x => x.IsSelected))
                    {
                        if (storage is StoragePhoto photo)
                        {
                            var storageFile = await photo.GetFileAsync();
                            await SendPhotoAsync(storageFile, storage.Caption, storage.IsForceFile, storage.Ttl);
                        }
                        else if (storage is StorageVideo video)
                        {
                            await SendVideoAsync(storage.File, storage.Caption, video.IsMuted, storage.IsForceFile, storage.Ttl, await video.GetEncodingAsync(), video.GetTransform());
                        }
                    }
                }

                return;
            }

            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.MediaTypes);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                var storages = new ObservableCollection<StorageMedia>();

                foreach (var file in files)
                {
                    var storage = await StorageMedia.CreateAsync(file, true);
                    if (storage != null)
                    {
                        storages.Add(storage);
                    }
                }

                SendMediaExecute(storages, storages[0]);
            }
        }

        public async void SendMediaExecute(ObservableCollection<StorageMedia> media, StorageMedia selectedItem)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (media == null || media.IsEmpty())
            {
                return;
            }

            var dialog = new SendMediaView { ViewModel = this, IsTTLEnabled = chat.Type is ChatTypePrivate };
            dialog.SetItems(media);
            dialog.SelectedItem = selectedItem;

            var dialogResult = await dialog.ShowAsync();

            TextField?.FocusMaybe(FocusState.Keyboard);

            if (dialogResult == ContentDialogBaseResult.OK)
            {
                var items = dialog.SelectedItems.ToList();
                if (items.Count > 1 && dialog.IsGrouped)
                {
                    var group = new List<StorageMedia>(Math.Min(items.Count, 10));

                    foreach (var item in items)
                    {
                        group.Add(item);

                        if (group.Count == 10)
                        {
                            await SendGroupedAsync(group);
                            group = new List<StorageMedia>(Math.Min(items.Count, 10));
                        }
                    }

                    if (group.Count > 0)
                    {
                        await SendGroupedAsync(group);
                    }
                }
                else
                {
                    foreach (var storage in items)
                    {
                        if (storage is StoragePhoto photo)
                        {
                            var storageFile = await photo.GetFileAsync();
                            await SendPhotoAsync(storageFile, storage.Caption, storage.IsForceFile, storage.Ttl);
                        }
                        else if (storage is StorageVideo video)
                        {
                            await SendVideoAsync(storage.File, storage.Caption, video.IsMuted, storage.IsForceFile, storage.Ttl, await video.GetEncodingAsync(), video.GetTransform());
                        }
                    }
                }
            }
        }

        private async Task SendPhotoAsync(StorageFile file, string caption, bool asFile, int? ttl = null)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var size = await ImageHelper.GetScaleAsync(file);

            var generated = await file.ToGeneratedAsync(asFile ? "copy" : "compress");
            var thumbnail = default(InputThumbnail);

            if (asFile)
            {
                var reply = GetReply(true);
                var input = new InputMessageDocument(generated, thumbnail, GetFormattedText(caption));

                await SendMessageAsync(reply, input);
            }
            else
            {
                var reply = GetReply(true);
                var input = new InputMessagePhoto(generated, thumbnail, new int[0], size.Width, size.Height, GetFormattedText(caption), ttl ?? 0);

                await SendMessageAsync(reply, input);
            }
        }

        public async Task SendVoiceNoteAsync(StorageFile file, int duration, string caption)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageVoiceNote(await file.ToGeneratedAsync(), duration, new byte[0], GetFormattedText(caption));

            await SendMessageAsync(reply, input);
        }

        public RelayCommand SendContactCommand { get; }
        private async void SendContactExecute()
        {
            var picker = new ContactPicker();
            //picker.SelectionMode = ContactSelectionMode.Fields;
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.PhoneNumber);

            var picked = await picker.PickContactAsync();
            if (picked != null)
            {
                Telegram.Td.Api.Contact contact = null;
                string vcard = string.Empty;

                var annotationStore = await ContactManager.RequestAnnotationStoreAsync(ContactAnnotationStoreAccessType.AppAnnotationsReadWrite);
                var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);
                if (store != null && annotationStore != null)
                {
                    var full = await store.GetContactAsync(picked.Id);
                    if (full != null)
                    {
                        var annotations = await annotationStore.FindAnnotationsForContactAsync(full);

                        var first = annotations.FirstOrDefault();
                        if (first != null)
                        {
                            var remote = first.RemoteId;
                            if (int.TryParse(remote.Substring(1), out int userId))
                            {
                                var user = ProtoService.GetUser(userId);
                                if (user != null)
                                {
                                    contact = new Telegram.Td.Api.Contact(user.PhoneNumber, user.FirstName, user.LastName, vcard, user.Id);
                                }
                            }
                        }

                        //contact = full;
                    }
                }

                if (contact == null)
                {
                    var phone = picked.Phones.FirstOrDefault();
                    if (phone == null)
                    {
                        return;
                    }

                    contact = new Telegram.Td.Api.Contact(phone.Number, picked.FirstName, picked.LastName, vcard, 0);
                }

                if (contact != null)
                {
                    await SendContactAsync(contact);
                }
            }
        }

        public Task<BaseObject> SendContactAsync(Telegram.Td.Api.Contact contact)
        {
            return SendMessageAsync(0, new InputMessageContact(contact));
        }

        private Task<BaseObject> SendMessageAsync(long replyToMessageId, InputMessageContent inputMessageContent)
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            return ProtoService.SendAsync(new SendMessage(chat.Id, replyToMessageId, false, false, null, inputMessageContent));
        }

        public RelayCommand SendLocationCommand { get; }
        private async void SendLocationExecute()
        {
            var page = new DialogShareLocationPage();

            var dialog = new ContentDialogBase();
            dialog.Content = page;

            page.Dialog = dialog;
            //page.LiveLocation = !_liveLocationService.IsTracking(Peer.ToPeer());

            var confirm = await dialog.ShowAsync();
            if (confirm == ContentDialogBaseResult.OK)
            {
                var reply = GetReply(true);
                var input = page.Media;

                await SendMessageAsync(reply, input);

                //if (page.Media is TLMessageMediaVenue venue)
                //{
                //    await SendGeoAsync(venue);
                //}
                //else if (page.Media is TLMessageMediaGeoLive geoLive)
                //{
                //    if (geoLive.Geo == null || geoLive.Period == 0 || _liveLocationService.IsTracking(Peer.ToPeer()))
                //    {
                //        _liveLocationService.StopTracking(Peer.ToPeer());
                //    }
                //    else
                //    {
                //        await SendGeoAsync(geoLive);
                //    }
                //}
                //else if (page.Media is TLMessageMediaGeo geo && geo.Geo is TLGeoPoint geoPoint)
                //{
                //    await SendGeoAsync(geoPoint.Lat, geoPoint.Long);
                //}
            }

            //NavigationService.Navigate(typeof(DialogSendLocationPage));
        }

        //public Task<bool> SendGeoAsync(TLMessageMediaGeoLive media)
        //{
        //    var tsc = new TaskCompletionSource<bool>();
        //    var date = TLUtils.DateToUniversalTimeTLInt(DateTime.Now);

        //    //var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), true, true, date, string.Empty, media, 0L, null);

        //    //var previousMessage = InsertSendingMessage(message);
        //    //CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
        //    //{
        //    //    var inputMedia = media.ToInputMedia();

        //    //    var result = await LegacyService.SendMediaAsync(Peer, inputMedia, message);
        //    //    if (result.IsSucceeded)
        //    //    {
        //    //        tsc.SetResult(true);
        //    //        await _liveLocationService.TrackAsync(message);
        //    //    }
        //    //    else
        //    //    {
        //    //        tsc.SetResult(false);
        //    //    }
        //    //});

        //    return tsc.Task;
        //}

        private async Task<BaseObject> SendGroupedAsync(ICollection<StorageMedia> items)
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            var reply = GetReply(true);
            var operations = new List<InputMessageContent>();

            foreach (var item in items)
            {
                if (item is StoragePhoto photo)
                {
                    var file = await photo.GetFileAsync();

                    var token = StorageApplicationPermissions.FutureAccessList.Enqueue(file);
                    var props = await file.GetBasicPropertiesAsync();
                    var size = await ImageHelper.GetScaleAsync(file);

                    var input = new InputMessagePhoto(new InputFileGenerated(file.Path, "compress", (int)props.Size), null, new int[0], size.Width, size.Height, GetFormattedText(photo.Caption), photo.Ttl ?? 0);

                    operations.Add(input);
                }
                else if (item is StorageVideo video)
                {
                    //var op = await PrepareVideoAsync(video.File, video.Caption, false, video.IsMuted, groupedId, await video.GetEncodingAsync(), video.GetTransform());
                    //if (op.message != null && op.operation != null)
                    //{
                    //    operations.Add(op);
                    //}

                    var file = video.File;
                    var profile = await video.GetEncodingAsync();
                    var transform = video.GetTransform();

                    var basicProps = await file.GetBasicPropertiesAsync();
                    var videoProps = await file.Properties.GetVideoPropertiesAsync();

                    //var thumbnail = await ImageHelper.GetVideoThumbnailAsync(file, videoProps, transform);

                    var videoWidth = (int)videoProps.GetWidth();
                    var videoHeight = (int)videoProps.GetHeight();

                    if (profile != null)
                    {
                        videoWidth = videoProps.Orientation == VideoOrientation.Rotate180 || videoProps.Orientation == VideoOrientation.Normal ? (int)profile.Video.Width : (int)profile.Video.Height;
                        videoHeight = videoProps.Orientation == VideoOrientation.Rotate180 || videoProps.Orientation == VideoOrientation.Normal ? (int)profile.Video.Height : (int)profile.Video.Width;
                    }

                    var conversion = new VideoConversion();
                    if (profile != null)
                    {
                        conversion.Transcode = true;
                        conversion.Mute = profile.Audio == null;
                        conversion.Width = profile.Video.Width;
                        conversion.Height = profile.Video.Height;
                        conversion.Bitrate = profile.Video.Bitrate;

                        if (transform != null)
                        {
                            conversion.Transform = true;
                            conversion.Rotation = transform.Rotation;
                            conversion.OutputSize = transform.OutputSize;
                            conversion.Mirror = transform.Mirror;
                            conversion.CropRectangle = transform.CropRectangle;
                        }
                    }

                    var generated = await file.ToGeneratedAsync("transcode#" + JsonConvert.SerializeObject(conversion));
                    var thumbnail = await file.ToThumbnailAsync(conversion, "thumbnail_transcode#" + JsonConvert.SerializeObject(conversion));

                    if (profile != null && profile.Audio == null)
                    {
                        var input = new InputMessageAnimation(generated, thumbnail, (int)videoProps.Duration.TotalSeconds, videoWidth, videoHeight, GetFormattedText(video.Caption));

                        operations.Add(input);
                    }
                    else
                    {
                        var input = new InputMessageVideo(generated, thumbnail, new int[0], (int)videoProps.Duration.TotalSeconds, videoWidth, videoHeight, true, GetFormattedText(video.Caption), video.Ttl ?? 0);

                        operations.Add(input);
                    }
                }
            }

            return await ProtoService.SendAsync(new SendMessageAlbum(chat.Id, reply, false, false, operations));
        }

        private FormattedText GetFormattedText(string text)
        {
            if (text == null)
            {
                return new FormattedText();
            }

            text = text.Format();

            var entities = Markdown.Parse(ProtoService, ref text);
            if (entities == null)
            {
                entities = new List<TextEntity>();
            }

            return new FormattedText(text, entities);

            //return ProtoService.Execute(new ParseTextEntities(text.Format(), new TextParseModeMarkdown())) as FormattedText;
        }
    }
}
