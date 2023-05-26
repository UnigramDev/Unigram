//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Services;
using Telegram.Services.Factories;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
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

namespace Telegram.ViewModels
{
    public partial class DialogViewModel
    {
        #region Stickers

        public async void SendSticker(Sticker sticker, bool? schedule, bool? silent, string emoji = null, bool reorder = false)
        {
            Delegate?.HideStickers();

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (sticker.FullType is StickerFullTypeRegular regular && regular.PremiumAnimation != null && ClientService.IsPremiumAvailable && !ClientService.IsPremium)
            {
                await ShowPopupAsync(new UniqueStickersPopup(ClientService, sticker));
                return;
            }

            var restricted = await VerifyRightsAsync(chat, x => x.CanSendOtherMessages, Strings.GlobalAttachStickersRestricted, Strings.AttachStickersRestrictedForever, Strings.AttachStickersRestricted);
            if (restricted)
            {
                return;
            }

            var options = await PickMessageSendOptionsAsync(schedule, silent, reorder);
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageSticker(new InputFileId(sticker.StickerValue.Id), sticker.Thumbnail?.ToInput(), sticker.Width, sticker.Height, emoji ?? string.Empty);

            await SendMessageAsync(chat, reply, input, options);
        }

        public void ViewSticker(Sticker sticker)
        {
            Delegate?.HideStickers();

            OpenSticker(sticker);
        }

        public void AddFavoriteSticker(Sticker sticker)
        {
            ClientService.Send(new AddFavoriteSticker(new InputFileId(sticker.StickerValue.Id)));
        }

        public void RemoveFavoriteSticker(Sticker sticker)
        {
            ClientService.Send(new RemoveFavoriteSticker(new InputFileId(sticker.StickerValue.Id)));
        }

        public void RemoveRecentSticker(Sticker sticker)
        {
            ClientService.Send(new RemoveRecentSticker(false, new InputFileId(sticker.StickerValue.Id)));
        }

        #endregion

        #region Animations

        public void SendAnimation(Animation animation)
        {
            SendAnimation(animation, null, null);
        }

        public async void SendAnimation(Animation animation, bool? schedule, bool? silent)
        {
            Delegate?.HideStickers();

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var restricted = await VerifyRightsAsync(chat, x => x.CanSendOtherMessages, Strings.GlobalAttachGifRestricted, Strings.AttachGifRestrictedForever, Strings.AttachGifRestricted);
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
            var input = new InputMessageAnimation(new InputFileId(animation.AnimationValue.Id), animation.Thumbnail?.ToInput(), new int[0], animation.Duration, animation.Width, animation.Height, null, false);

            await SendMessageAsync(chat, reply, input, options);
        }

        public void DeleteAnimation(Animation animation)
        {
            ClientService.Send(new RemoveSavedAnimation(new InputFileId(animation.AnimationValue.Id)));
        }

        public void SaveAnimation(Animation animation)
        {
            ClientService.Send(new AddSavedAnimation(new InputFileId(animation.AnimationValue.Id)));
        }

        #endregion

        public async Task<bool> VerifyRightsAsync(Chat chat, Func<ChatPermissions, bool> permission, string global, string forever, string temporary)
        {
            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return false;
                }

                if (supergroup.Status is ChatMemberStatusRestricted restricted && !permission(restricted.Permissions))
                {
                    if (restricted.IsForever())
                    {
                        await ShowPopupAsync(forever, Strings.AppName, Strings.OK);
                    }
                    else
                    {
                        await ShowPopupAsync(string.Format(temporary, Formatter.BannedUntil(restricted.RestrictedUntilDate)), Strings.AppName, Strings.OK);
                    }

                    return true;
                }
                else if (supergroup.Status is ChatMemberStatusMember)
                {
                    if (!permission(chat.Permissions))
                    {
                        await ShowPopupAsync(global, Strings.AppName, Strings.OK);
                        return true;
                    }
                }
            }
            else
            {
                if (!permission(chat.Permissions))
                {
                    await ShowPopupAsync(global, Strings.AppName, Strings.OK);
                    return true;
                }
            }

            return false;
        }

        public bool VerifyRights(Chat chat, Func<ChatPermissions, bool> permission, string global, string forever, string temporary, out string label)
        {
            return VerifyRights(ClientService, chat, permission, global, forever, temporary, out label);
        }

        public static bool VerifyRights(IClientService clientService, Chat chat, Func<ChatPermissions, bool> permission, string global, string forever, string temporary, out string label)
        {
            if (clientService.TryGetSupergroup(chat, out var supergroup))
            {
                if (supergroup.Status is ChatMemberStatusRestricted restricted && !permission(restricted.Permissions))
                {
                    if (restricted.IsForever())
                    {
                        label = forever;
                    }
                    else
                    {
                        label = string.Format(temporary, Formatter.BannedUntil(restricted.RestrictedUntilDate));
                    }

                    return true;
                }
                else if (supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    label = null;
                    return false;
                }
            }
            else if (clientService.TryGetBasicGroup(chat, out var basicGroup))
            {
                if (basicGroup.Status is ChatMemberStatusRestricted restricted && !permission(restricted.Permissions))
                {
                    if (restricted.IsForever())
                    {
                        label = forever;
                    }
                    else
                    {
                        label = string.Format(temporary, Formatter.BannedUntil(restricted.RestrictedUntilDate));
                    }

                    return true;
                }
                else if (basicGroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    label = null;
                    return false;
                }
            }

            if (!permission(chat.Permissions))
            {
                if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
                {
                    label = Strings.ChannelCantSendMessage;
                    return true;
                }

                label = global;
                return true;
            }

            label = null;
            return false;
        }

        public bool VerifyRights(Chat chat, Func<ChatPermissions, bool> permission)
        {
            return VerifyRights(ClientService, chat, permission);
        }

        public static bool VerifyRights(IClientService clientService, Chat chat, Func<ChatPermissions, bool> permission)
        {
            if (clientService.TryGetSupergroup(chat, out var supergroup))
            {
                if (supergroup.Status is ChatMemberStatusRestricted restricted && !permission(restricted.Permissions))
                {
                    return true;
                }
                else if (supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    return false;
                }
            }
            else if (clientService.TryGetBasicGroup(chat, out var basicGroup))
            {
                if (basicGroup.Status is ChatMemberStatusRestricted restricted && !permission(restricted.Permissions))
                {
                    return true;
                }
                else if (basicGroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    return false;
                }
            }

            if (!permission(chat.Permissions))
            {
                if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
                {
                    return true;
                }

                return true;
            }

            return false;
        }

        public ChatPermissions GetPermissions(Chat chat, out bool restricted)
        {
            return GetPermissions(ClientService, chat, out restricted);
        }

        public static ChatPermissions GetPermissions(IClientService clientService, Chat chat, out bool restrict)
        {
            restrict = false;

            if (clientService.TryGetSupergroup(chat, out var supergroup))
            {
                if (supergroup.Status is ChatMemberStatusRestricted restricted)
                {
                    restrict = true;
                    return restricted.Permissions;
                }
                else if (supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    return new ChatPermissions(true, true, true, true, true, true, true, true, true, true, true, true, true, true);
                }
            }
            else if (clientService.TryGetBasicGroup(chat, out var basicGroup))
            {
                if (basicGroup.Status is ChatMemberStatusRestricted restricted)
                {
                    restrict = true;
                    return restricted.Permissions;
                }
                else if (basicGroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    return new ChatPermissions(true, true, true, true, true, true, true, true, true, true, true, true, true, true);
                }
            }

            return chat.Permissions;
        }

        public async void SendDocument()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var restricted = await VerifyRightsAsync(chat, x => x.CanSendDocuments,
                Strings.ErrorSendRestrictedDocumentsAll,
                Strings.ErrorSendRestrictedDocuments,
                Strings.ErrorSendRestrictedDocuments);
            if (restricted)
            {
                return;
            }

            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                try
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
                catch { }
            }
            else
            {

            }
        }

        public async void SendFileExecute(IReadOnlyList<StorageFile> files, FormattedText caption = null, bool media = true)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var permissions = GetPermissions(chat, out bool restricted);

            var items = await StorageMedia.CreateAsync(files);
            if (items.Empty())
            {
                return;
            }

            foreach (var item in items)
            {
                if (item is StoragePhoto && !permissions.CanSendPhotos)
                {
                    await ShowPopupAsync(restricted ? Strings.ErrorSendRestrictedPhoto : Strings.ErrorSendRestrictedPhotoAll, Strings.AppName, Strings.OK);
                    return;
                }
                else if (item is StorageVideo && !permissions.CanSendVideos)
                {
                    await ShowPopupAsync(restricted ? Strings.ErrorSendRestrictedVideo : Strings.ErrorSendRestrictedVideoAll, Strings.AppName, Strings.OK);
                    return;
                }
                else if (item is StorageAudio && !permissions.CanSendAudios)
                {
                    await ShowPopupAsync(restricted ? Strings.ErrorSendRestrictedMusic : Strings.ErrorSendRestrictedMusicAll, Strings.AppName, Strings.OK);
                    return;
                }
                else if (item is StorageDocument && !permissions.CanSendDocuments)
                {
                    await ShowPopupAsync(restricted ? Strings.ErrorSendRestrictedDocuments : Strings.ErrorSendRestrictedDocumentsAll, Strings.AppName, Strings.OK);
                    return;
                }
            }

            FormattedText formattedText = null;
            if (caption == null)
            {
                formattedText = GetFormattedText(true);
                caption = formattedText.Substring(0, ClientService.Options.MessageCaptionLengthMax);
            }

            var self = ClientService.IsSavedMessages(_chat);

            var mediaAllowed = permissions.CanSendPhotos
                ? items.All(x => x is StoragePhoto)
                ? permissions.CanSendVideos
                : items.All(x => x is StoragePhoto or StorageVideo)
                : permissions.CanSendVideos
                ? items.All(x => x is StorageVideo)
                : false;

            var popup = new SendFilesPopup(this, items, media, mediaAllowed, permissions.CanSendDocuments || permissions.CanSendAudios, _chat.Type is ChatTypePrivate && !self, _type == DialogType.History, self);
            popup.Caption = caption;

            var confirm = await popup.OpenAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                if (formattedText != null)
                {
                    TextField?.SetText(formattedText);
                }

                return;
            }

            var options = await PickMessageSendOptionsAsync(popup.Schedule, popup.Silent);
            if (options == null)
            {
                return;
            }

            if (popup.Items.Count == 1)
            {
                popup.Items[0].HasSpoiler = popup.Spoiler && !popup.IsFilesSelected;
                await SendStorageMediaAsync(chat, popup.Items[0], popup.Caption, popup.IsFilesSelected, options);
            }
            else if (popup.Items.Count > 1 && popup.IsAlbum && popup.IsAlbumAvailable)
            {
                var group = new List<StorageMedia>(Math.Min(popup.Items.Count, 10));

                foreach (var item in popup.Items)
                {
                    item.HasSpoiler = popup.Spoiler && !popup.IsFilesSelected;
                    group.Add(item);

                    if (group.Count == 10)
                    {
                        await SendGroupedAsync(group, popup.Caption, options, popup.IsFilesSelected);
                        group = new List<StorageMedia>(Math.Min(popup.Items.Count, 10));
                    }
                }

                if (group.Count > 0)
                {
                    await SendGroupedAsync(group, popup.Caption, options, popup.IsFilesSelected);
                }
            }
            else if (popup.Items.Count > 0)
            {
                if (popup.Caption != null)
                {
                    await SendMessageAsync(popup.Caption, options);
                }

                foreach (var file in popup.Items)
                {
                    file.HasSpoiler = popup.Spoiler && !popup.IsFilesSelected;
                    await SendStorageMediaAsync(chat, file, null, popup.IsFilesSelected, options);
                }
            }
        }

        private async Task SendStorageMediaAsync(Chat chat, StorageMedia storage, FormattedText caption, bool asFile, MessageSendOptions options)
        {
            if (storage is StorageDocument or StorageAudio)
            {
                await SendDocumentAsync(chat, storage.File, caption, options);
            }
            else if (storage is StoragePhoto)
            {
                await SendPhotoAsync(chat, storage.File, caption, asFile, storage.HasSpoiler, storage.Ttl, storage.IsEdited ? storage.EditState : null, options);
            }
            else if (storage is StorageVideo video)
            {
                await SendVideoAsync(chat, storage.File, caption, video.IsMuted, asFile, storage.HasSpoiler, storage.Ttl, await video.GetEncodingAsync(), video.GetTransform(), options);
            }
        }

        private async Task SendDocumentAsync(Chat chat, StorageFile file, FormattedText caption = null, MessageSendOptions options = null)
        {
            var factory = await _messageFactory.CreateDocumentAsync(file, false);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

                await SendMessageAsync(chat, reply, input, options);
            }
        }

        private async Task SendPhotoAsync(Chat chat, StorageFile file, FormattedText caption, bool asFile, bool spoiler = false, int ttl = 0, BitmapEditState editState = null, MessageSendOptions options = null)
        {
            var factory = await _messageFactory.CreatePhotoAsync(file, asFile, spoiler, ttl, editState);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

                await SendMessageAsync(chat, reply, input, options);
            }
        }

        public async Task SendVideoAsync(Chat chat, StorageFile file, FormattedText caption, bool animated, bool asFile, bool spoiler = false, int ttl = 0, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null, MessageSendOptions options = null)
        {
            var factory = await _messageFactory.CreateVideoAsync(file, animated, asFile, spoiler, ttl, profile, transform);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

                await SendMessageAsync(chat, reply, input, options);
            }
        }

        public async Task SendVideoNoteAsync(Chat chat, StorageFile file, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
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

                await SendMessageAsync(chat, reply, input, options);
            }
        }

        public async Task SendVoiceNoteAsync(Chat chat, StorageFile file, int duration, FormattedText caption)
        {
            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageVoiceNote(await file.ToGeneratedAsync(ConversionType.Opus), duration, new byte[0], caption);

            await SendMessageAsync(chat, reply, input, options);
        }

        public async void SendCamera()
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

        public async void SendMedia()
        {
            try
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
            catch { }
        }

        public async void SendContact()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = await ChooseChatsPopup.PickUserAsync(ClientService, Strings.ShareContactTitle, true);
            if (user == null)
            {
                return;
            }

            var vcard = string.Empty;
            var contact = new Contact(user.PhoneNumber, user.FirstName, user.LastName, vcard, user.Id);

            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            await SendContactAsync(chat, contact, options);

#if !DEBUG
            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("SendContact");
#endif
        }

        public async Task<BaseObject> SendContactAsync(Chat chat, Telegram.Td.Api.Contact contact, MessageSendOptions options)
        {
            var reply = GetReply(true);
            var input = new InputMessageContact(contact);

            return await SendMessageAsync(chat, reply, input, options);
        }

        //private async Task<BaseObject> SendMessageAsync(long replyToMessageId, InputMessageContent inputMessageContent)
        //{
        //    var options = new MessageSendOptions(false, false, null);
        //    if (_isSchedule)
        //    {
        //        var dialog = new SupergroupEditRestrictedUntilView(DateTime.Now.ToTimestamp());
        //        var confirm = await ShowPopupAsync(dialog);
        //        if (confirm != ContentDialogResult.Primary)
        //        {
        //            return null;
        //        }

        //        options.SchedulingState = new MessageSchedulingStateSendAtDate(dialog.Value.ToTimestamp());
        //    }

        //    return await SendMessageAsync(replyToMessageId, inputMessageContent, options);
        //}

        public async Task<MessageSendOptions> PickMessageSendOptionsAsync(bool? schedule = null, bool? silent = null, bool reorder = false)
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            if (schedule == true || (_type == DialogType.ScheduledMessages && schedule == null))
            {
                var user = ClientService.GetUser(chat);
                var until = DateTime.Now;

                if (_type == DialogType.ScheduledMessages)
                {
                    var last = Items.LastOrDefault();
                    if (last != null && last.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
                    {
                        until = Formatter.ToLocalTime(sendAtDate.SendDate);
                    }
                }

                var dialog = new ScheduleMessagePopup(user, until.AddMinutes(1), ClientService.IsSavedMessages(chat));
                var confirm = await ShowPopupAsync(dialog);

                if (confirm != ContentDialogResult.Primary)
                {
                    return null;
                }

                if (dialog.IsUntilOnline)
                {
                    return new MessageSendOptions(false, false, false, reorder, new MessageSchedulingStateSendWhenOnline(), 0);
                }
                else
                {
                    return new MessageSendOptions(false, false, false, reorder, new MessageSchedulingStateSendAtDate(dialog.Value.ToTimestamp()), 0);
                }
            }
            else
            {
                return new MessageSendOptions(silent ?? false, false, false, reorder, null, 0);
            }
        }

        private async Task<BaseObject> SendMessageAsync(Chat chat, long replyToMessageId, InputMessageContent inputMessageContent, MessageSendOptions options)
        {
            var response = await ClientService.SendAsync(new SendMessage(chat.Id, _threadId, replyToMessageId, options, null, inputMessageContent));
            if (response is Error error)
            {
                if (error.MessageEquals(ErrorType.PEER_FLOOD))
                {

                }
                else if (error.MessageEquals(ErrorType.USER_BANNED_IN_CHANNEL))
                {

                }
                else if (error.MessageEquals(ErrorType.SCHEDULE_TOO_MUCH))
                {
                    await ShowPopupAsync(Strings.MessageScheduledLimitReached, Strings.AppName, Strings.OK);
                }
            }
            else if (options?.SchedulingState != null && Type != DialogType.ScheduledMessages)
            {
                NavigationService.NavigateToChat(chat, scheduled: true);
            }

            return response;
        }

        public async void SendLocation()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var popup = new SendLocationPopup();
            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                var options = await PickMessageSendOptionsAsync();
                if (options == null)
                {
                    return;
                }

                var reply = GetReply(true);
                var input = popup.Media;

                await SendMessageAsync(chat, reply, input, options);

#if !DEBUG
                Microsoft.AppCenter.Analytics.Analytics.TrackEvent("SendLocation");
#endif
            }
        }

        public async void SendPoll()
        {
            await SendPollAsync(false, false, _chat?.Type is ChatTypeSupergroup super && super.IsChannel);
        }

        private async Task SendPollAsync(bool forceQuiz, bool forceRegular, bool forceAnonymous)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var dialog = new CreatePollPopup(forceQuiz, forceRegular, forceAnonymous);

            var confirm = await ShowPopupAsync(dialog);
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

            await SendMessageAsync(chat, reply, input, options);
        }

        //public Task<bool> SendGeoAsync(TLMessageMediaGeoLive media)
        //{
        //    var tsc = new TaskCompletionSource<bool>();
        //    var date = TLUtils.DateToUniversalTimeTLInt(DateTime.Now);

        //    //var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), true, true, date, string.Empty, media, 0L, null);

        //    //var previousMessage = InsertSendingMessage(message);
        //    //ClientService.SyncSendingMessage(message, previousMessage, async (m) =>
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

        private async Task<BaseObject> SendGroupedAsync(ICollection<StorageMedia> items, FormattedText caption, MessageSendOptions options, bool asFile)
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            var reply = GetReply(true);
            var operations = new List<InputMessageContent>();

            var firstCaption = asFile ? null : caption;

            var audio = items.All(x => x is StorageAudio);

            foreach (var item in items)
            {
                if (asFile || audio)
                {
                    if (item == items.Last())
                    {
                        firstCaption = caption;
                    }

                    var factory = await _messageFactory.CreateDocumentAsync(item.File, !audio);
                    if (factory != null)
                    {
                        var input = factory.Delegate(factory.InputFile, firstCaption);

                        operations.Add(input);
                        firstCaption = null;
                    }
                }
                else if (item is StoragePhoto photo)
                {
                    var factory = await _messageFactory.CreatePhotoAsync(photo.File, asFile, photo.HasSpoiler, photo.Ttl, photo.IsEdited ? photo.EditState : null);
                    if (factory != null)
                    {
                        var input = factory.Delegate(factory.InputFile, firstCaption);

                        operations.Add(input);
                        firstCaption = null;
                    }
                }
                else if (item is StorageVideo video)
                {
                    var factory = await _messageFactory.CreateVideoAsync(video.File, video.IsMuted, asFile, video.HasSpoiler, video.Ttl, await video.GetEncodingAsync(), video.GetTransform());
                    if (factory != null)
                    {
                        var input = factory.Delegate(factory.InputFile, firstCaption);

                        operations.Add(input);
                        firstCaption = null;
                    }
                }
            }

            return await ClientService.SendAsync(new SendMessageAlbum(chat.Id, _threadId, reply, options, operations, false));
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
                if (package.AvailableFormats.Contains("application/x-tl-message"))
                {
                    var data = await package.GetDataAsync("application/x-tl-message") as IRandomAccessStream;
                    var reader = new DataReader(data.GetInputStreamAt(0));
                    var length = await reader.LoadAsync((uint)data.Size);

                    var chatId = reader.ReadInt64();
                    var messageId = reader.ReadInt64();

                    if (chatId == _chat?.Id)
                    {
                        return;
                    }
                }

                if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
                {
                    var bitmap = await package.GetBitmapAsync();
                    var media = new List<StorageFile>();

                    var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.bmp", DateTime.Now);
                    var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                    using (var source = await bitmap.OpenReadAsync())
                    using (var destination = await cache.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await RandomAccessStream.CopyAsync(
                            source.GetInputStreamAt(0),
                            destination.GetOutputStreamAt(0));
                    }

                    media.Add(cache);

                    var captionElements = new List<string>();

                    if (package.AvailableFormats.Contains(StandardDataFormats.Text))
                    {
                        var text = await package.GetTextAsync();
                        captionElements.Add(text);
                    }

                    FormattedText caption = null;
                    if (captionElements.Count > 0)
                    {
                        var resultCaption = string.Join(Environment.NewLine, captionElements);
                        caption = new FormattedText(resultCaption, new TextEntity[0])
                            .Substring(0, ClientService.Options.MessageCaptionLengthMax);
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



        public async void EditDocument()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                var file = await picker.PickSingleFileAsync();
                if (file == null)
                {
                    return;
                }

                var factory = await _messageFactory.CreateDocumentAsync(file, false);
                if (factory != null)
                {
                    header.EditingMessageMedia = factory;
                }
            }
            catch { }
        }

        public async void EditMedia()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            try
            {
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
            catch { }
        }

        public async void EditCurrent()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            var file = header.EditingMessage.GetFile();
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var cached = await ClientService.GetFileAsync(file);
            if (cached == null)
            {
                return;
            }

            await EditMediaAsync(cached);
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

            var mediaAllowed = header.EditingMessage.Content is not MessageDocument;

            var items = new[] { storage };
            var popup = new SendFilesPopup(this, items, mediaAllowed, mediaAllowed, true, false, false, false);
            popup.Caption = formattedText
                .Substring(0, ClientService.Options.MessageCaptionLengthMax);

            var confirm = await popup.OpenAsync();

            TextField?.Focus(FocusState.Programmatic);

            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            TextField?.SetText(popup.Caption);

            Task<InputMessageFactory> request = null;
            if (storage is StoragePhoto)
            {
                request = _messageFactory.CreatePhotoAsync(storage.File, popup.IsFilesSelected, storage.HasSpoiler, storage.Ttl, storage.IsEdited ? storage.EditState : null);
            }
            else if (storage is StorageVideo video)
            {
                request = _messageFactory.CreateVideoAsync(storage.File, video.IsMuted, popup.IsFilesSelected, storage.HasSpoiler, storage.Ttl, await video.GetEncodingAsync(), video.GetTransform());
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
