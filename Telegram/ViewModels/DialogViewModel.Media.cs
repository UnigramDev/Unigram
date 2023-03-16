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
using Windows.Graphics.Imaging;
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

        public async void StickerSendExecute(Sticker sticker, bool? schedule, bool? silent, string emoji = null, bool reorder = false)
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

            var restricted = await VerifyRightsAsync(chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachStickersRestricted, Strings.Resources.AttachStickersRestrictedForever, Strings.Resources.AttachStickersRestricted);
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

        public RelayCommand<Sticker> StickerViewCommand { get; }
        private void StickerViewExecute(Sticker sticker)
        {
            Delegate?.HideStickers();

            OpenSticker(sticker);
        }

        public RelayCommand<Sticker> StickerFaveCommand { get; }
        private void StickerFaveExecute(Sticker sticker)
        {
            ClientService.Send(new AddFavoriteSticker(new InputFileId(sticker.StickerValue.Id)));
        }

        public RelayCommand<Sticker> StickerUnfaveCommand { get; }
        private void StickerUnfaveExecute(Sticker sticker)
        {
            ClientService.Send(new RemoveFavoriteSticker(new InputFileId(sticker.StickerValue.Id)));
        }

        public RelayCommand<Sticker> StickerDeleteCommand { get; }
        private void StickerDeleteExecute(Sticker sticker)
        {
            ClientService.Send(new RemoveRecentSticker(false, new InputFileId(sticker.StickerValue.Id)));
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
            var input = new InputMessageAnimation(new InputFileId(animation.AnimationValue.Id), animation.Thumbnail?.ToInput(), new int[0], animation.Duration, animation.Width, animation.Height, null, false);

            await SendMessageAsync(chat, reply, input, options);
        }

        public RelayCommand<Animation> AnimationDeleteCommand { get; }
        private void AnimationDeleteExecute(Animation animation)
        {
            ClientService.Send(new RemoveSavedAnimation(new InputFileId(animation.AnimationValue.Id)));
        }

        public RelayCommand<Animation> AnimationSaveCommand { get; }
        private void AnimationSaveExecute(Animation animation)
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
                        await ShowPopupAsync(forever, Strings.Resources.AppName, Strings.Resources.OK);
                    }
                    else
                    {
                        await ShowPopupAsync(string.Format(temporary, Converter.BannedUntil(restricted.RestrictedUntilDate)), Strings.Resources.AppName, Strings.Resources.OK);
                    }

                    return true;
                }
                else if (supergroup.Status is ChatMemberStatusMember)
                {
                    if (!permission(chat.Permissions))
                    {
                        await ShowPopupAsync(global, Strings.Resources.AppName, Strings.Resources.OK);
                        return true;
                    }
                }
            }
            else
            {
                if (!permission(chat.Permissions))
                {
                    await ShowPopupAsync(global, Strings.Resources.AppName, Strings.Resources.OK);
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
                        label = string.Format(temporary, Converter.BannedUntil(restricted.RestrictedUntilDate));
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
                        label = string.Format(temporary, Converter.BannedUntil(restricted.RestrictedUntilDate));
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
                    label = Strings.Resources.ChannelCantSendMessage;
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

        public RelayCommand SendDocumentCommand { get; }
        private async void SendDocumentExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var restricted = await VerifyRightsAsync(chat, x => x.CanSendDocuments,
                Strings.Resources.ErrorSendRestrictedDocumentsAll,
                Strings.Resources.ErrorSendRestrictedDocuments,
                Strings.Resources.ErrorSendRestrictedDocuments);
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
            if (items.IsEmpty())
            {
                return;
            }

            foreach (var item in items)
            {
                if (item is StoragePhoto && !permissions.CanSendPhotos)
                {
                    await ShowPopupAsync(restricted ? Strings.Resources.ErrorSendRestrictedPhoto : Strings.Resources.ErrorSendRestrictedPhotoAll, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }
                else if (item is StorageVideo && !permissions.CanSendVideos)
                {
                    await ShowPopupAsync(restricted ? Strings.Resources.ErrorSendRestrictedVideo : Strings.Resources.ErrorSendRestrictedVideoAll, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }
                else if (item is StorageAudio && !permissions.CanSendAudios)
                {
                    await ShowPopupAsync(restricted ? Strings.Resources.ErrorSendRestrictedMusic : Strings.Resources.ErrorSendRestrictedMusicAll, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }
                else if (item is StorageDocument && !permissions.CanSendDocuments)
                {
                    await ShowPopupAsync(restricted ? Strings.Resources.ErrorSendRestrictedDocuments : Strings.Resources.ErrorSendRestrictedDocumentsAll, Strings.Resources.AppName, Strings.Resources.OK);
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

            var dialog = new SendFilesPopup(SessionId, items, media, mediaAllowed, permissions.CanSendDocuments || permissions.CanSendAudios, _chat.Type is ChatTypePrivate && !self, _type == DialogType.History, self);
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
                dialog.Items[0].HasSpoiler = dialog.Spoiler && !dialog.IsFilesSelected;
                await SendStorageMediaAsync(chat, dialog.Items[0], dialog.Caption, dialog.IsFilesSelected, options);
            }
            else if (dialog.Items.Count > 1 && dialog.IsAlbum && dialog.IsAlbumAvailable)
            {
                var group = new List<StorageMedia>(Math.Min(dialog.Items.Count, 10));

                foreach (var item in dialog.Items)
                {
                    item.HasSpoiler = dialog.Spoiler && !dialog.IsFilesSelected;
                    group.Add(item);

                    if (group.Count == 10)
                    {
                        await SendGroupedAsync(group, dialog.Caption, options, dialog.IsFilesSelected);
                        group = new List<StorageMedia>(Math.Min(dialog.Items.Count, 10));
                    }
                }

                if (group.Count > 0)
                {
                    await SendGroupedAsync(group, dialog.Caption, options, dialog.IsFilesSelected);
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
                    file.HasSpoiler = dialog.Spoiler && !dialog.IsFilesSelected;
                    await SendStorageMediaAsync(chat, file, null, dialog.IsFilesSelected, options);
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

        public RelayCommand SendContactCommand { get; }
        private async void SendContactExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = await SharePopup.PickUserAsync(ClientService, Strings.Resources.ShareContactTitle, true);
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
                        until = Utils.ToDateTime(sendAtDate.SendDate);
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
                if (error.TypeEquals(ErrorType.PEER_FLOOD))
                {

                }
                else if (error.TypeEquals(ErrorType.USER_BANNED_IN_CHANNEL))
                {

                }
                else if (error.TypeEquals(ErrorType.SCHEDULE_TOO_MUCH))
                {
                    await ShowPopupAsync(Strings.Resources.MessageScheduledLimitReached, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }

            return response;
        }

        public RelayCommand SendLocationCommand { get; }
        private async void SendLocationExecute()
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

        public RelayCommand SendPollCommand { get; }
        private async void SendPollExecute()
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

                    var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.png", DateTime.Now);
                    var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                    using (var stream = await bitmap.OpenReadAsync())
                    {
                        var result = await ImageHelper.TranscodeAsync(stream, cache, BitmapEncoder.PngEncoderId);
                        media.Add(result);
                    }

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



        public RelayCommand EditDocumentCommand { get; }
        private async void EditDocumentExecute()
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

        public RelayCommand EditMediaCommand { get; }
        private async void EditMediaExecute()
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

        public RelayCommand EditCurrentCommand { get; }
        private async void EditCurrentExecute()
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

            var storageFile = await ClientService.GetFileAsync(file);
            if (storageFile == null)
            {
                return;
            }

            await EditMediaAsync(storageFile);
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

            var dialog = new SendFilesPopup(SessionId, new[] { storage }, true, false, false, false, false, false);
            dialog.Caption = formattedText
                .Substring(0, ClientService.Options.MessageCaptionLengthMax);

            var confirm = await dialog.OpenAsync();

            TextField?.Focus(FocusState.Programmatic);

            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            TextField?.SetText(dialog.Caption);

            Task<InputMessageFactory> request = null;
            if (storage is StoragePhoto)
            {
                request = _messageFactory.CreatePhotoAsync(storage.File, dialog.IsFilesSelected, storage.HasSpoiler, storage.Ttl, storage.IsEdited ? storage.EditState : null);
            }
            else if (storage is StorageVideo video)
            {
                request = _messageFactory.CreateVideoAsync(storage.File, video.IsMuted, dialog.IsFilesSelected, storage.HasSpoiler, storage.Ttl, await video.GetEncodingAsync(), video.GetTransform());
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
