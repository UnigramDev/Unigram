using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Entities;
using Unigram.Services;
using Unigram.Services.Factories;
using Unigram.Views.Popups;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Capture;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        #region Stickers

        public RelayCommand<Sticker> StickerSendCommand { get; }
        public void StickerSendExecute(Sticker sticker)
        {
            StickerSendExecute(sticker, null, null);
        }

        public async void StickerSendExecute(Sticker sticker, bool? schedule, bool? silent)
        {
            Delegate?.HideStickers();

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var restricted = await VerifyRightsAsync(chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachStickersRestricted, Strings.Resources.AttachStickersRestrictedForever, Strings.Resources.AttachStickersRestricted);
            if (restricted)
            {
                return;
            }

            var options = await PickMessageSendOptionsAsync(schedule, silent);
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageSticker(new InputFileId(sticker.StickerValue.Id), sticker.Thumbnail?.ToInput(), sticker.Width, sticker.Height);

            await SendMessageAsync(reply, input, options);
        }

        public RelayCommand<Sticker> StickerViewCommand { get; }
        private void StickerViewExecute(Sticker sticker)
        {
            Delegate?.HideStickers();

            OpenSticker(sticker);
        }

        public RelayCommand<Sticker> StickerFaveCommand { get; }
        private void StickerFaveExecute(Sticker sticker)
        {
            ProtoService.Send(new AddFavoriteSticker(new InputFileId(sticker.StickerValue.Id)));
        }

        public RelayCommand<Sticker> StickerUnfaveCommand { get; }
        private void StickerUnfaveExecute(Sticker sticker)
        {
            ProtoService.Send(new RemoveFavoriteSticker(new InputFileId(sticker.StickerValue.Id)));
        }

        #endregion

        #region Animations

        public RelayCommand<Animation> AnimationSendCommand { get; }
        public void AnimationSendExecute(Animation animation)
        {
            AnimationSendExecute(animation, null, null);
        }

        public async void AnimationSendExecute(Animation animation, bool? schedule, bool? silent)
        {
            Delegate?.HideStickers();

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var restricted = await VerifyRightsAsync(chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachGifRestricted, Strings.Resources.AttachGifRestrictedForever, Strings.Resources.AttachGifRestricted);
            if (restricted)
            {
                return;
            }

            var options = await PickMessageSendOptionsAsync(schedule, silent);
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageAnimation(new InputFileId(animation.AnimationValue.Id), animation.Thumbnail?.ToInput(), new int[0], animation.Duration, animation.Width, animation.Height, null);

            await SendMessageAsync(reply, input, options);
        }

        public RelayCommand<Animation> AnimationDeleteCommand { get; }
        private void AnimationDeleteExecute(Animation animation)
        {
            ProtoService.Send(new RemoveSavedAnimation(new InputFileId(animation.AnimationValue.Id)));
        }

        public RelayCommand<Animation> AnimationSaveCommand { get; }
        private void AnimationSaveExecute(Animation animation)
        {
            ProtoService.Send(new AddSavedAnimation(new InputFileId(animation.AnimationValue.Id)));
        }

        #endregion

        public async Task<bool> VerifyRightsAsync(Chat chat, Func<ChatPermissions, bool> permission, string global, string forever, string temporary)
        {
            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return false;
                }

                if (supergroup.Status is ChatMemberStatusRestricted restricted && !permission(restricted.Permissions))
                {
                    if (restricted.IsForever())
                    {
                        await MessagePopup.ShowAsync(forever, Strings.Resources.AppName, Strings.Resources.OK);
                    }
                    else
                    {
                        await MessagePopup.ShowAsync(string.Format(temporary, BindConvert.Current.BannedUntil(restricted.RestrictedUntilDate)), Strings.Resources.AppName, Strings.Resources.OK);
                    }

                    return true;
                }
                else if (supergroup.Status is ChatMemberStatusMember)
                {
                    if (!permission(chat.Permissions))
                    {
                        await MessagePopup.ShowAsync(global, Strings.Resources.AppName, Strings.Resources.OK);
                        return true;
                    }
                }
            }
            else
            {
                if (!permission(chat.Permissions))
                {
                    await MessagePopup.ShowAsync(global, Strings.Resources.AppName, Strings.Resources.OK);
                    return true;
                }
            }

            return false;
        }

        public bool VerifyRights(Chat chat, Func<ChatPermissions, bool> permission, string global, string forever, string temporary, out string label)
        {
            return VerifyRights(CacheService, chat, permission, global, forever, temporary, out label);
        }

        public static bool VerifyRights(ICacheService cacheService, Chat chat, Func<ChatPermissions, bool> permission, string global, string forever, string temporary, out string label)
        {
            if (cacheService.TryGetSupergroup(chat, out var supergroup))
            {
                if (supergroup.Status is ChatMemberStatusRestricted restricted && !permission(restricted.Permissions))
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
                else if (supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator)
                {
                    label = null;
                    return false;
                }
            }

            if (!permission(chat.Permissions))
            {
                if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
                {
                    label = Strings.Resources.ChannelCantSendMessage;
                    return true;
                }

                label = global;
                return true;
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
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Count > 0)
                {
                    SendFileExecute(files, media: false);
                }
            }
            else
            {

            }
        }

        public async void SendFileExecute(IReadOnlyList<StorageFile> files, FormattedText caption = null, bool media = true)
        {
            var items = await StorageMedia.CreateAsync(files);
            if (items.IsEmpty())
            {
                return;
            }

            FormattedText formattedText = null;
            if (caption == null)
            {
                formattedText = GetFormattedText(true);
                caption = formattedText.Substring(0, CacheService.Options.MessageCaptionLengthMax);
            }

            var self = CacheService.IsSavedMessages(_chat);

            var dialog = new SendFilesPopup(items, media, _chat.Type is ChatTypePrivate && !self, _type == DialogType.History, self);
            dialog.ViewModel = this;
            dialog.Caption = caption;

            var confirm = await dialog.OpenAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                if (formattedText != null)
                {
                    TextField?.SetText(formattedText);
                }

                return;
            }

            var options = await PickMessageSendOptionsAsync(dialog.Schedule, dialog.Silent);
            if (options == null)
            {
                return;
            }

            if (dialog.Items.Count == 1)
            {
                await SendStorageMediaAsync(dialog.Items[0], dialog.Caption, dialog.IsFilesSelected, options);
            }
            else if (dialog.Items.Count > 1 && dialog.IsAlbum && dialog.IsAlbumAvailable)
            {
                var group = new List<StorageMedia>(Math.Min(dialog.Items.Count, 10));

                foreach (var item in dialog.Items)
                {
                    group.Add(item);

                    if (group.Count == 10)
                    {
                        await SendGroupedAsync(group, dialog.Caption, options);
                        group = new List<StorageMedia>(Math.Min(dialog.Items.Count, 10));
                    }
                }

                if (group.Count > 0)
                {
                    await SendGroupedAsync(group, dialog.Caption, options);
                }
            }
            else if (dialog.Items.Count > 0)
            {
                if (dialog.Caption != null)
                {
                    await SendMessageAsync(dialog.Caption, options);
                }

                foreach (var file in dialog.Items)
                {
                    await SendStorageMediaAsync(file, null, dialog.IsFilesSelected, options);
                }
            }
        }

        private async Task SendStorageMediaAsync(StorageMedia storage, FormattedText caption, bool asFile, MessageSendOptions options)
        {
            if (storage is StorageDocument)
            {
                await SendDocumentAsync(storage.File, caption, options);
            }
            else if (storage is StoragePhoto photo)
            {
                await SendPhotoAsync(storage.File, caption, asFile, storage.Ttl, storage.IsEdited ? storage.EditState : null, options);
            }
            else if (storage is StorageVideo video)
            {
                await SendVideoAsync(storage.File, caption, video.IsMuted, asFile, storage.Ttl, await video.GetEncodingAsync(), video.GetTransform(), options);
            }
        }

        private async Task SendDocumentAsync(StorageFile file, FormattedText caption = null, MessageSendOptions options = null)
        {
            var factory = await _messageFactory.CreateDocumentAsync(file);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

                await SendMessageAsync(reply, input, options);
            }
        }

        private async Task SendPhotoAsync(StorageFile file, FormattedText caption, bool asFile, int ttl = 0, BitmapEditState editState = null, MessageSendOptions options = null)
        {
            var factory = await _messageFactory.CreatePhotoAsync(file, asFile, ttl, editState);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

                await SendMessageAsync(reply, input, options);
            }
        }

        public async Task SendVideoAsync(StorageFile file, FormattedText caption, bool animated, bool asFile, int ttl = 0, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null, MessageSendOptions options = null)
        {
            var factory = await _messageFactory.CreateVideoAsync(file, animated, asFile, ttl, profile, transform);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

                await SendMessageAsync(reply, input, options);
            }
        }

        public async Task SendVideoNoteAsync(StorageFile file, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
        {
            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            var factory = await _messageFactory.CreateVideoNoteAsync(file, profile, transform);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, null);

                await SendMessageAsync(reply, input, options);
            }
        }

        public async Task SendVoiceNoteAsync(StorageFile file, int duration, FormattedText caption)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageVoiceNote(await file.ToGeneratedAsync(), duration, new byte[0], caption);

            await SendMessageAsync(reply, input, options);
        }

        public RelayCommand SendCameraCommand { get; }
        private async void SendCameraExecute()
        {
            var capture = new CameraCaptureUI();
            capture.PhotoSettings.AllowCropping = false;
            capture.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
            capture.PhotoSettings.MaxResolution = CameraCaptureUIMaxPhotoResolution.HighestAvailable;
            capture.VideoSettings.Format = CameraCaptureUIVideoFormat.Mp4;
            capture.VideoSettings.MaxResolution = CameraCaptureUIMaxVideoResolution.HighestAvailable;

            var file = await capture.CaptureFileAsync(CameraCaptureUIMode.PhotoOrVideo);
            if (file != null)
            {
                SendFileExecute(new[] { file });
            }
        }

        public RelayCommand SendMediaCommand { get; }
        private async void SendMediaExecute()
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.MediaTypes);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                SendFileExecute(files);
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

                if (contact == null)
                {
                    return;
                }

                var options = await PickMessageSendOptionsAsync();
                if (options == null)
                {
                    return;
                }

                await SendContactAsync(contact, options);
            }
        }

        public async Task<BaseObject> SendContactAsync(Telegram.Td.Api.Contact contact, MessageSendOptions options)
        {
            return await SendMessageAsync(0, new InputMessageContact(contact), options);
        }

        //private async Task<BaseObject> SendMessageAsync(long replyToMessageId, InputMessageContent inputMessageContent)
        //{
        //    var options = new MessageSendOptions(false, false, null);
        //    if (_isSchedule)
        //    {
        //        var dialog = new SupergroupEditRestrictedUntilView(DateTime.Now.ToTimestamp());
        //        var confirm = await dialog.ShowQueuedAsync();
        //        if (confirm != ContentDialogResult.Primary)
        //        {
        //            return null;
        //        }

        //        options.SchedulingState = new MessageSchedulingStateSendAtDate(dialog.Value.ToTimestamp());
        //    }

        //    return await SendMessageAsync(replyToMessageId, inputMessageContent, options);
        //}

        public async Task<MessageSendOptions> PickMessageSendOptionsAsync(bool? schedule = null, bool? silent = null)
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            if (schedule == true || (_type == DialogType.ScheduledMessages && schedule == null))
            {
                var user = CacheService.GetUser(chat);

                var dialog = new ScheduleMessagePopup(user, DateTime.Now.ToTimestamp(), CacheService.IsSavedMessages(chat));
                var confirm = await dialog.ShowQueuedAsync();

                if (confirm != ContentDialogResult.Primary)
                {
                    return null;
                }

                if (dialog.IsUntilOnline)
                {
                    return new MessageSendOptions(false, false, new MessageSchedulingStateSendWhenOnline());
                }
                else
                {
                    return new MessageSendOptions(false, false, new MessageSchedulingStateSendAtDate(dialog.Value.ToTimestamp()));
                }
            }
            else
            {
                return new MessageSendOptions(silent ?? false, false, null);
            }
        }

        private async Task<BaseObject> SendMessageAsync(long replyToMessageId, InputMessageContent inputMessageContent, MessageSendOptions options)
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            if (options == null)
            {
                options = new MessageSendOptions(false, false, null);
            }

            var response = await ProtoService.SendAsync(new SendMessage(chat.Id, _threadId, replyToMessageId, options, null, inputMessageContent));
            if (response is Error error)
            {
                if (error.TypeEquals(ErrorType.PEER_FLOOD))
                {

                }
                else if (error.TypeEquals(ErrorType.USER_BANNED_IN_CHANNEL))
                {

                }
                else if (error.TypeEquals(ErrorType.SCHEDULE_TOO_MUCH))
                {
                    await MessagePopup.ShowAsync(Strings.Resources.MessageScheduledLimitReached, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }

            return response;
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
            var dialog = new SendLocationPopup();
            //page.LiveLocation = !_liveLocationService.IsTracking(Peer.ToPeer());

            var confirm = await dialog.OpenAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var options = await PickMessageSendOptionsAsync();
                if (options == null)
                {
                    return;
                }

                var reply = GetReply(true);
                var input = dialog.Media;

                await SendMessageAsync(reply, input, options);

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
            await SendPollAsync(false, false, _chat?.Type is ChatTypeSupergroup super && super.IsChannel);
        }

        private async Task SendPollAsync(bool forceQuiz, bool forceRegular, bool forceAnonymous)
        {
            var dialog = new CreatePollPopup(forceQuiz, forceRegular, forceAnonymous);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessagePoll(dialog.Question, dialog.Options, dialog.IsAnonymous, dialog.Type, 0, 0, false);

            await SendMessageAsync(reply, input, options);
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

        private async Task<BaseObject> SendGroupedAsync(ICollection<StorageMedia> items, FormattedText caption, MessageSendOptions options)
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            var reply = GetReply(true);
            var operations = new List<InputMessageContent>();

            var firstCaption = caption;

            foreach (var item in items)
            {
                if (item is StoragePhoto photo)
                {
                    var factory = await _messageFactory.CreatePhotoAsync(photo.File, false, photo.Ttl, photo.IsEdited ? photo.EditState : null);
                    if (factory != null)
                    {
                        var input = factory.Delegate(factory.InputFile, firstCaption);

                        operations.Add(input);
                        firstCaption = null;
                    }
                }
                else if (item is StorageVideo video)
                {
                    var factory = await _messageFactory.CreateVideoAsync(video.File, video.IsMuted, false, video.Ttl, await video.GetEncodingAsync(), video.GetTransform());
                    if (factory != null)
                    {
                        var input = factory.Delegate(factory.InputFile, firstCaption);

                        operations.Add(input);
                        firstCaption = null;
                    }
                }
            }

            return await ProtoService.SendAsync(new SendMessageAlbum(chat.Id, _threadId, reply, options, operations));
        }

        private FormattedText GetFormattedText(string text)
        {
            if (text == null)
            {
                return new FormattedText();
            }

            text = text.Format();
            return Client.Execute(new ParseMarkdown(new FormattedText(text, new TextEntity[0]))) as FormattedText;
        }

        public async Task HandlePackageAsync(DataPackageView package)
        {
            try
            {
                if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
                {
                    var bitmap = await package.GetBitmapAsync();
                    var media = new List<StorageFile>();

                    var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.bmp", DateTime.Now);
                    var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                    using (var stream = await bitmap.OpenReadAsync())
                    using (var reader = new DataReader(stream.GetInputStreamAt(0)))
                    using (var output = await cache.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await reader.LoadAsync((uint)stream.Size);
                        var buffer = reader.ReadBuffer(reader.UnconsumedBufferLength);
                        await output.WriteAsync(buffer);
                    }

                    media.Add(cache);

                    var captionElements = new List<string>();

                    if (package.AvailableFormats.Contains(StandardDataFormats.Text))
                    {
                        var text = await package.GetTextAsync();
                        captionElements.Add(text);
                    }
                    if (package.AvailableFormats.Contains(StandardDataFormats.WebLink))
                    {
                        try
                        {
                            var webLink = await package.GetWebLinkAsync();
                            captionElements.Add(webLink.AbsoluteUri);
                        }
                        catch { }
                    }

                    FormattedText caption = null;
                    if (captionElements.Count > 0)
                    {
                        var resultCaption = string.Join(Environment.NewLine, captionElements);
                        caption = new FormattedText(resultCaption, new TextEntity[0])
                            .Substring(0, CacheService.Options.MessageCaptionLengthMax);
                    }

                    SendFileExecute(media, caption);
                }
                else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await package.GetStorageItemsAsync();
                    var files = new List<StorageFile>(items.Count);

                    foreach (var file in items.OfType<StorageFile>())
                    {
                        files.Add(file);
                    }

                    SendFileExecute(files);
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
                        var link = await package.GetWebLinkAsync();
                        text += Environment.NewLine + link.AbsoluteUri;
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

                    var link = await package.GetWebLinkAsync();
                    field.Document.GetRange(field.Document.Selection.EndPosition, field.Document.Selection.EndPosition).SetText(TextSetOptions.None, link.AbsoluteUri);
                }
            }
            catch { }
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

            var file = await ProtoService.GetFileAsync(fileInfo.File);
            if (file == null)
            {
                return;
            }

            await EditMediaAsync(file);
        }

        public async Task EditMediaAsync(StorageFile file)
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            var storage = await StorageMedia.CreateAsync(file);
            if (storage == null)
            {
                return;
            }

            var formattedText = GetFormattedText(true);

            var dialog = new SendFilesPopup(new[] { storage }, true, false, false, false);
            dialog.Caption = formattedText
                .Substring(0, CacheService.Options.MessageCaptionLengthMax);

            var confirm = await dialog.OpenAsync();

            TextField?.Focus(FocusState.Programmatic);

            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            TextField?.SetText(dialog.Caption);

            Task<InputMessageFactory> request = null;
            if (storage is StoragePhoto photo)
            {
                request = _messageFactory.CreatePhotoAsync(storage.File, dialog.IsFilesSelected, storage.Ttl, storage.IsEdited ? storage.EditState : null);
            }
            else if (storage is StorageVideo video)
            {
                request = _messageFactory.CreateVideoAsync(storage.File, video.IsMuted, dialog.IsFilesSelected, storage.Ttl, await video.GetEncodingAsync(), video.GetTransform());
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
