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
using Unigram.Entities;
using Unigram.Native;
using Unigram.Services;
using Unigram.Services.Factories;
using Unigram.Views;
using Unigram.Views.Dialogs;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static Unigram.Services.GenerationService;

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
            var input = new InputMessageSticker(new InputFileId(sticker.StickerValue.Id), sticker.Thumbnail?.ToInputThumbnail(), sticker.Width, sticker.Height);

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
            var input = new InputMessageAnimation(new InputFileId(animation.AnimationValue.Id), animation.Thumbnail?.ToInputThumbnail(), animation.Duration, animation.Width, animation.Height, null);

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

        public bool VerifyRights(Chat chat, Func<ChatMemberStatusRestricted, bool> permission, string forever, string temporary, out string label)
        {
            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    label = null;
                    return false;
                }

                if (supergroup.Status is ChatMemberStatusRestricted restricted && !permission(restricted))
                {
                    if (restricted.IsForever())
                    {
                        label = forever;
                    }
                    else
                    {
                        label = string.Format(temporary, BindConvert.Current.BannedUntil(restricted.RestrictedUntilDate));
                    }

                    return true;
                }
            }

            label = null;
            return false;
        }

        public RelayCommand SendDocumentCommand { get; }
        private async void SendDocumentExecute()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                if (MediaLibrary.SelectedCount > 0)
                {
                    foreach (var storage in MediaLibrary.Where(x => x.IsSelected))
                    {
                        await SendDocumentAsync(storage.File, storage.Caption);
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
                        await SendDocumentAsync(storage, null);
                    }
                }
            }
            else
            {

            }
        }

        public async void SendFileExecute(IList<StorageFile> files)
        {
            foreach (var file in files)
            {
                await SendDocumentAsync(file, null);
            }
        }

        private async Task SendDocumentAsync(StorageFile file, FormattedText caption = null)
        {
            var factory = await _messageFactory.CreateDocumentAsync(file);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

                await SendMessageAsync(reply, input);
            }
        }

        public async Task SendVideoAsync(StorageFile file, FormattedText caption, bool animated, bool asFile, int ttl = 0, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
        {
            var factory = await _messageFactory.CreateVideoAsync(file, animated, asFile, ttl, profile, transform);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

                await SendMessageAsync(reply, input);
            }
        }

        public async Task SendVideoNoteAsync(StorageFile file, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
        {
            var factory = await _messageFactory.CreateVideoNoteAsync(file, profile, transform);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, null);

                await SendMessageAsync(reply, input);
            }
        }

        private async Task SendPhotoAsync(StorageFile file, FormattedText caption, bool asFile, int ttl = 0, Rect? crop = null)
        {
            var factory = await _messageFactory.CreatePhotoAsync(file, asFile, ttl, crop);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

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
                            await SendPhotoAsync(storage.File, storage.Caption, storage.IsForceFile, storage.Ttl, storage.IsCropped ? storage.CropRectangle : null);
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

            var formattedText = GetFormattedText(true);
            selectedItem.Caption = formattedText
                .Substring(0, CacheService.Options.MessageCaptionLengthMax);

            var dialog = new SendMediaView { ViewModel = this, IsTTLEnabled = chat.Type is ChatTypePrivate };
            dialog.SetItems(media);
            dialog.SelectedItem = selectedItem;

            var dialogResult = await dialog.ShowAsync();

            TextField?.FocusMaybe(FocusState.Keyboard);

            if (dialogResult != ContentDialogResult.Primary)
            {
                TextField?.SetText(formattedText.Text, formattedText.Entities);
                return;
            }

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
                        await SendPhotoAsync(storage.File, storage.Caption, storage.IsForceFile, storage.Ttl, storage.IsCropped ? storage.CropRectangle : null);
                    }
                    else if (storage is StorageVideo video)
                    {
                        await SendVideoAsync(storage.File, storage.Caption, video.IsMuted, storage.IsForceFile, storage.Ttl, await video.GetEncodingAsync(), video.GetTransform());
                    }
                }
            }
        }

        public RelayCommand SendContactCommand { get; }
        private async void SendContactExecute()
        {
            var picker = new ContactPicker();
            //picker.SelectionMode = ContactSelectionMode.Fields;
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.Address);
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.ConnectedServiceAccount);
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.Email);
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.ImportantDate);
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.JobInfo);
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.Notes);
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.PhoneNumber);
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.SignificantOther);
            //picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.Website);

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

                        //var vcardStream = await ContactManager.ConvertContactToVCardAsync(full, 2000);
                        //using (var stream = await vcardStream.OpenReadAsync())
                        //{
                        //    using (var dataReader = new DataReader(stream.GetInputStreamAt(0)))
                        //    {
                        //        await dataReader.LoadAsync((uint)stream.Size);
                        //        vcard = dataReader.ReadString(dataReader.UnconsumedBufferLength);
                        //    }
                        //}

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

                        if (contact == null)
                        {
                            var phone = full.Phones.FirstOrDefault();
                            if (phone == null)
                            {
                                return;
                            }

                            contact = new Telegram.Td.Api.Contact(phone.Number, picked.FirstName, picked.LastName, vcard, 0);
                        }
                    }
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

        private async Task<BaseObject> EditMessageAsync(MessageViewModel message, InputFile inputFile, FileType type, Func<InputFile, InputMessageContent> inputMessageContent)
        {
            var response = await ProtoService.SendAsync(new UploadFile(inputFile, type, 32));
            if (response is Telegram.Td.Api.File file)
            {
                ComposerHeader = new MessageComposerHeader { EditingMessage = message, EditingMessageMedia = null, EditingMessageFileId = file.Id };
            }

            return response;
        }

        public RelayCommand SendLocationCommand { get; }
        private async void SendLocationExecute()
        {
            var page = new DialogShareLocationPage();

            var dialog = new OverlayPage();
            dialog.Content = page;

            page.Dialog = dialog;
            //page.LiveLocation = !_liveLocationService.IsTracking(Peer.ToPeer());

            var confirm = await dialog.ShowAsync();
            if (confirm == ContentDialogResult.Primary)
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

        public RelayCommand SendPollCommand { get; }
        private async void SendPollExecute()
        {
            var dialog = new CreatePollView();

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessagePoll(dialog.Question, dialog.Options);

            await SendMessageAsync(reply, input);
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
                    var file = photo.File;
                    var crop = photo.IsCropped ? photo.CropRectangle : null;

                    var token = StorageApplicationPermissions.FutureAccessList.Enqueue(file);
                    var props = await file.GetBasicPropertiesAsync();
                    var size = await ImageHelper.GetScaleAsync(file, crop: crop);

                    var generated = await file.ToGeneratedAsync("compress" + (crop.HasValue ? "#" + JsonConvert.SerializeObject(crop) : string.Empty));

                    var input = new InputMessagePhoto(generated, null, new int[0], size.Width, size.Height, photo.Caption, photo.Ttl);

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
                        var input = new InputMessageAnimation(generated, thumbnail, (int)videoProps.Duration.TotalSeconds, videoWidth, videoHeight, video.Caption);

                        operations.Add(input);
                    }
                    else
                    {
                        var input = new InputMessageVideo(generated, thumbnail, new int[0], (int)videoProps.Duration.TotalSeconds, videoWidth, videoHeight, true, video.Caption, video.Ttl);

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

            var entities = Markdown.Parse(ref text);
            if (entities == null)
            {
                entities = new List<TextEntity>();
            }

            return new FormattedText(text, entities);

            //return ProtoService.Execute(new ParseTextEntities(text.Format(), new TextParseModeMarkdown())) as FormattedText;
        }

        public async Task HandlePackageAsync(DataPackageView package)
        {
            var boh = string.Join(", ", package.AvailableFormats);

            if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
            {
                var bitmap = await package.GetBitmapAsync();
                var media = new ObservableCollection<StorageMedia>();
                var cache = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\paste.jpg", CreationCollisionOption.ReplaceExisting);

                using (var stream = await bitmap.OpenReadAsync())
                using (var reader = new DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    var buffer = new byte[(int)stream.Size];
                    reader.ReadBytes(buffer);
                    await FileIO.WriteBytesAsync(cache, buffer);

                    var photo = await StoragePhoto.CreateAsync(cache, true);
                    if (photo == null)
                    {
                        return;
                    }

                    media.Add(photo);
                }

                if (package.AvailableFormats.Contains(StandardDataFormats.Text))
                {
                    media[0].Caption = new FormattedText(await package.GetTextAsync(), new TextEntity[0])
                        .Substring(0, CacheService.Options.MessageCaptionLengthMax);
                }

                SendMediaExecute(media, media[0]);
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
            {
                var items = await package.GetStorageItemsAsync();
                var media = new ObservableCollection<StorageMedia>();
                var files = new List<StorageFile>(items.Count);

                foreach (StorageFile file in items.OfType<StorageFile>())
                {
                    if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
                    {
                        var photo = await StoragePhoto.CreateAsync(file, true);
                        if (photo != null)
                        {
                            media.Add(photo);
                        }
                    }
                    else if (file.ContentType == "video/mp4")
                    {
                        var video = await StorageVideo.CreateAsync(file, true);
                        if (video != null)
                        {
                            media.Add(video);
                        }
                    }

                    files.Add(file);
                }

                // Send compressed __only__ if user is dropping photos and videos only
                if (media.Count > 0 && media.Count == files.Count)
                {
                    SendMediaExecute(media, media[0]);
                }
                else if (files.Count > 0)
                {
                    SendFileExecute(files);
                }
            }
            //else if (e.DataView.Contains(StandardDataFormats.WebLink))
            //{
            //    // TODO: Invoke getting a preview of the weblink above the Textbox
            //    var link = await e.DataView.GetWebLinkAsync();
            //    if (TextField.Text == "")
            //    {
            //        TextField.Text = link.AbsolutePath;
            //    }
            //    else
            //    {
            //        TextField.Text = (TextField.Text + " " + link.AbsolutePath);
            //    }
            //
            //    gridLoading.Visibility = Visibility.Collapsed;
            //
            //}
            else if (package.AvailableFormats.Contains(StandardDataFormats.Text))
            {
                var field = TextField;
                if (field == null)
                {
                    return;
                }

                var text = await package.GetTextAsync();

                if (package.Contains(StandardDataFormats.WebLink))
                {
                    text += Environment.NewLine + await package.GetWebLinkAsync();
                }

                field.Document.GetRange(field.Document.Selection.EndPosition, field.Document.Selection.EndPosition).SetText(TextSetOptions.None, text);
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.WebLink))
            {
                var field = TextField;
                if (field == null)
                {
                    return;
                }

                var text = await package.GetWebLinkAsync();
                field.Document.GetRange(field.Document.Selection.EndPosition, field.Document.Selection.EndPosition).SetText(TextSetOptions.None, text.ToString());
            }
        }



        public RelayCommand EditDocumentCommand { get; }
        private async void EditDocumentExecute()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            var factory = await _messageFactory.CreateDocumentAsync(file);
            if (factory != null)
            {
                header.EditingMessageMedia = factory;
            }
        }
        public RelayCommand EditMediaCommand { get; }
        private async void EditMediaExecute()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.MediaTypes);

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            await EditMediaAsync(file);
        }

        public RelayCommand EditCurrentCommand { get; }
        private async void EditCurrentExecute()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            var fileInfo = header.EditingMessage.Get().GetFileAndName(true);
            if (fileInfo.File == null || !fileInfo.File.Local.IsDownloadingCompleted)
            {
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(fileInfo.File.Local.Path);
            if (file == null)
            {
                return;
            }

            await EditMediaAsync(file);
        }

        private async Task EditMediaAsync(StorageFile file)
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            var storage = await StorageMedia.CreateAsync(file, true);
            if (storage == null)
            {
                return;
            }

            var formattedText = GetFormattedText(true);
            storage.Caption = formattedText
                .Substring(0, CacheService.Options.MessageCaptionLengthMax);

            var dialog = new SendMediaView { ViewModel = this, IsTTLEnabled = false, IsMultipleSelection = false };
            dialog.SetItems(new ObservableCollection<StorageMedia> { storage });
            dialog.SelectedItem = storage;

            var confirm = await dialog.ShowAsync();

            TextField?.FocusMaybe(FocusState.Keyboard);

            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            TextField?.SetText(storage.Caption);

            Task<InputMessageFactory> request = null;
            if (storage is StoragePhoto photo)
            {
                request = _messageFactory.CreatePhotoAsync(storage.File, storage.IsForceFile, storage.Ttl, storage.IsCropped ? storage.CropRectangle : null);
            }
            else if (storage is StorageVideo video)
            {
                request = _messageFactory.CreateVideoAsync(storage.File, video.IsMuted, storage.IsForceFile, storage.Ttl, await video.GetEncodingAsync(), video.GetTransform());
            }

            if (request == null)
            {
                return;
            }

            var factory = await request;
            if (factory != null)
            {
                header.EditingMessageMedia = factory;
            }
        }
    }
}
