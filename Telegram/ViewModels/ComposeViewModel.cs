//
// Copyright Fela Ameghino 2015-2024
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
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Factories;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Windows.Media.Capture;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public abstract class ComposeViewModel : ViewModelBase
    {
        protected ComposeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public abstract void ViewSticker(Sticker sticker);

        protected abstract void HideStickers();

        protected abstract InputMessageReplyTo GetReply(bool clear, bool notify = true);

        public abstract FormattedText GetFormattedText(bool clear);

        protected abstract void SetFormattedText(FormattedText text);

        public abstract Chat Chat { get; set; }

        public abstract long ThreadId { get; }

        #region Stickers

        public async void SendSticker(Sticker sticker, bool? schedule, bool? silent, string emoji = null, bool reorder = false)
        {
            HideStickers();

            if (sticker.FullType is StickerFullTypeRegular regular && regular.PremiumAnimation != null && ClientService.IsPremiumAvailable && !ClientService.IsPremium)
            {
                await ShowPopupAsync(new UniqueStickersPopup(ClientService, sticker));
                return;
            }

            var restricted = await VerifyRightsAsync(x => x.CanSendOtherMessages, Strings.GlobalAttachStickersRestricted, Strings.AttachStickersRestrictedForever, Strings.AttachStickersRestricted);
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

            await SendMessageAsync(reply, input, options);
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
            HideStickers();

            var restricted = await VerifyRightsAsync(x => x.CanSendOtherMessages, Strings.GlobalAttachGifRestricted, Strings.AttachGifRestrictedForever, Strings.AttachGifRestricted);
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
            var input = new InputMessageAnimation(new InputFileId(animation.AnimationValue.Id), animation.Thumbnail?.ToInput(), Array.Empty<int>(), animation.Duration, animation.Width, animation.Height, null, false, false);

            await SendMessageAsync(reply, input, options);
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

        public async Task<bool> VerifyRightsAsync(Func<ChatPermissions, bool> permission, string global, string forever, string temporary)
        {
            if (Chat is not Chat chat)
            {
                return false;
            }

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
            var restricted = await VerifyRightsAsync(x => x.CanSendDocuments,
                Strings.ErrorSendRestrictedDocumentsAll,
                Strings.ErrorSendRestrictedDocuments,
                Strings.ErrorSendRestrictedDocuments);
            if (restricted)
            {
                return;
            }

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

        public async void SendFileExecute(IReadOnlyList<StorageFile> files, FormattedText caption = null, bool media = true)
        {
            var items = await StorageMedia.CreateAsync(files);
            if (items.Count > 0)
            {
                SendFileExecute(items, caption, media);
            }
        }


        public async void SendFileExecute(IList<StorageMedia> items, FormattedText caption = null, bool media = true)
        {
            if (Chat is not Chat chat || items.Empty())
            {
                return;
            }

            var permissions = GetPermissions(chat, out bool restricted);

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

            var self = ClientService.IsSavedMessages(chat);

            bool mediaAllowed;
            if (permissions.CanSendPhotos && permissions.CanSendVideos)
            {
                mediaAllowed = items.All(x => x is StoragePhoto or StorageVideo);
            }
            else if (permissions.CanSendPhotos)
            {
                mediaAllowed = items.All(x => x is StoragePhoto);
            }
            else if (permissions.CanSendVideos)
            {
                mediaAllowed = items.All(x => x is StorageVideo);
            }
            else
            {
                mediaAllowed = false;
            }

            var popup = new SendFilesPopup(this, items, media, mediaAllowed, permissions.CanSendDocuments || permissions.CanSendAudios, chat.Type is ChatTypePrivate && !self, CanSchedule, self, false);
            popup.Caption = caption;

            if (ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
            {
                popup.HasPaidMediaAllowed = fullInfo.HasPaidMediaAllowed;
            }

            var confirm = await popup.OpenAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                if (formattedText != null)
                {
                    SetFormattedText(formattedText);
                }

                return;
            }

            var options = await PickMessageSendOptionsAsync(popup.Schedule, popup.Silent);
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var captionz = popup.Caption;

            var captionAboveMedia = popup.ShowCaptionAboveMedia;
            var hasSpoiler = popup.SendWithSpoiler && !popup.IsFilesSelected;

            if (popup.Items.Count == 1)
            {
                await Task.Run(() => SendStorageMediaAsync(popup.Items[0], reply, captionz, popup.IsFilesSelected, options, captionAboveMedia, hasSpoiler, popup.StarCount));
            }
            else if (popup.Items.Count > 1 && popup.IsAlbum && popup.IsAlbumAvailable)
            {
                var group = new List<StorageMedia>(Math.Min(popup.Items.Count, 10));

                foreach (var item in popup.Items)
                {
                    group.Add(item);

                    if (group.Count == 10)
                    {
                        await SendGroupedAsync(group, reply, captionz, options, popup.IsFilesSelected, captionAboveMedia, hasSpoiler, popup.StarCount);
                        group = new List<StorageMedia>(Math.Min(popup.Items.Count, 10));
                        reply = null;
                    }
                }

                if (group.Count > 0)
                {
                    await SendGroupedAsync(group, reply, captionz, options, popup.IsFilesSelected, captionAboveMedia, hasSpoiler, popup.StarCount);
                }
            }
            else if (popup.Items.Count > 0)
            {
                if (caption != null)
                {
                    await SendMessageAsync(caption, options, reply);
                    reply = null;
                }

                await Task.Run(async () =>
                {
                    foreach (var file in popup.Items)
                    {
                        await SendStorageMediaAsync(file, reply, null, popup.IsFilesSelected, options, captionAboveMedia, hasSpoiler, popup.StarCount);
                        reply = null;
                    }
                });
            }
        }

        protected abstract bool CanSchedule { get; }

        private async Task SendStorageMediaAsync(StorageMedia storage, InputMessageReplyTo reply, FormattedText caption, bool asFile, MessageSendOptions options, bool captionAboveMedia = false, bool spoiler = false, long starCount = 0)
        {
            if (storage is StorageDocument or StorageAudio || asFile)
            {
                await SendDocumentAsync(storage, reply, caption, storage.IsScreenshot, options);
            }
            else if (storage is StoragePhoto photo)
            {
                await SendPhotoAsync(photo, reply, caption, captionAboveMedia, spoiler, storage.Ttl, storage.IsEdited ? storage.EditState : null, options, starCount);
            }
            else if (storage is StorageVideo video)
            {
                await SendVideoAsync(video, reply, caption, video.IsMuted, captionAboveMedia, spoiler, storage.Ttl, video.GetTransform(), options, starCount);
            }
        }

        private async Task SendDocumentAsync(StorageMedia file, InputMessageReplyTo reply, FormattedText caption = null, bool asScreenshot = false, MessageSendOptions options = null)
        {
            var factory = await MessageFactory.CreateDocumentAsync(file, false, asScreenshot);
            if (factory != null)
            {
                //var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, caption);

                await SendMessageAsync(reply, input, options);
            }
        }

        private async Task SendPhotoAsync(StoragePhoto file, InputMessageReplyTo reply, FormattedText caption, bool captionAboveMedia = false, bool spoiler = false, MessageSelfDestructType ttl = null, BitmapEditState editState = null, MessageSendOptions options = null, long starCount = 0)
        {
            var factory = await MessageFactory.CreatePhotoAsync(file, captionAboveMedia, spoiler, starCount > 0 ? new MessageSelfDestructTypeImmediately() : ttl, editState);
            if (factory != null)
            {
                if (starCount > 0)
                {
                    var input = factory.PaidDelegate?.Invoke(factory.InputFile);
                    if (input != null)
                    {
                        await SendMessageAsync(reply, new InputMessagePaidMedia(starCount, new[] { input }, caption, captionAboveMedia, string.Empty), options);
                    }
                }
                else
                {
                    //var reply = GetReply(true);
                    var input = factory.Delegate(factory.InputFile, caption);

                    await SendMessageAsync(reply, input, options);
                }
            }
        }

        public async Task SendVideoAsync(StorageVideo video, InputMessageReplyTo reply, FormattedText caption, bool animated, bool captionAboveMedia = false, bool spoiler = false, MessageSelfDestructType ttl = null, VideoTransformEffectDefinition transform = null, MessageSendOptions options = null, long starCount = 0)
        {
            var factory = await MessageFactory.CreateVideoAsync(video, animated, captionAboveMedia, spoiler, starCount > 0 ? new MessageSelfDestructTypeImmediately() : ttl, transform);
            if (factory != null)
            {
                if (starCount > 0)
                {
                    var input = factory.PaidDelegate?.Invoke(factory.InputFile);
                    if (input != null)
                    {
                        await SendMessageAsync(reply, new InputMessagePaidMedia(starCount, new[] { input }, caption, captionAboveMedia, string.Empty), options);
                    }
                }
                else
                {
                    //var reply = GetReply(true);
                    var input = factory.Delegate(factory.InputFile, caption);

                    await SendMessageAsync(reply, input, options);
                }
            }
        }

        public async Task SendVideoNoteAsync(StorageFile file, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
        {
            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            var factory = await MessageFactory.CreateVideoNoteAsync(file, profile, transform);
            if (factory != null)
            {
                var reply = GetReply(true);
                var input = factory.Delegate(factory.InputFile, null);

                await SendMessageAsync(reply, input, options);
            }
        }

        public async Task SendVoiceNoteAsync(StorageFile file, int duration, FormattedText caption)
        {
            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            // TODO: 172 selfDestructType
            var reply = GetReply(true);
            var input = new InputMessageVoiceNote(await file.ToGeneratedAsync(ConversionType.Opus), duration, Array.Empty<byte>(), caption, null);

            await SendMessageAsync(reply, input, options);
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
            var user = await ChooseChatsPopup.PickUserAsync(ClientService, NavigationService, Strings.ShareContactTitle, true);
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

            await SendContactAsync(contact, options);

#if !DEBUG
            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("SendContact");
#endif
        }

        public Task<BaseObject> SendContactAsync(Contact contact, MessageSendOptions options)
        {
            var reply = GetReply(true);
            var input = new InputMessageContact(contact);

            return SendMessageAsync(reply, input, options);
        }

        public async Task<BaseObject> SendContentAsync(InputMessageContent input)
        {
            var reply = GetReply(true);

            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return null;
            }

            return await SendMessageAsync(reply, input, options);
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

        public abstract Task<MessageSendOptions> PickMessageSendOptionsAsync(bool? schedule = null, bool? silent = null, bool reorder = false);

        protected async Task<BaseObject> SendMessageAsync(InputMessageReplyTo replyTo, InputMessageContent inputMessageContent, MessageSendOptions options)
        {
            if (Chat is not Chat chat)
            {
                return null;
            }

            options ??= new MessageSendOptions();
            options.SendingId = Math.Max(options.SendingId, 1);

            var response = await ClientService.SendAsync(CreateSendMessage(chat.Id, ThreadId, replyTo, options, inputMessageContent));
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
            else
            {
                ContinueSendMessage(options);
            }

            return response;
        }

        protected virtual Function CreateSendMessage(long chatId, long messageThreadId, InputMessageReplyTo replyTo, MessageSendOptions messageSendOptions, InputMessageContent inputMessageContent)
        {
            return new SendMessage(chatId, messageThreadId, replyTo, messageSendOptions, null, inputMessageContent);
        }

        protected virtual void ContinueSendMessage(MessageSendOptions options)
        {

        }

        public async void SendLocation()
        {
            var popup = new SendLocationPopup(SessionId);

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

                await SendMessageAsync(reply, input, options);

#if !DEBUG
                Microsoft.AppCenter.Analytics.Analytics.TrackEvent("SendLocation");
#endif
            }
        }

        public async void SendPoll()
        {
            await SendPollAsync(false, false, Chat?.Type is ChatTypeSupergroup super && super.IsChannel);
        }

        protected async Task SendPollAsync(bool forceQuiz, bool forceRegular, bool forceAnonymous)
        {
            var dialog = new CreatePollPopup(ClientService, forceQuiz, forceRegular, forceAnonymous);

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

            await SendMessageAsync(reply, input, options);
        }

        private async Task<BaseObject> SendGroupedAsync(ICollection<StorageMedia> items, InputMessageReplyTo reply, FormattedText caption, MessageSendOptions options, bool asFile, bool captionAboveMedia = false, bool spoiler = false, long starCount = 0)
        {
            if (Chat is not Chat chat)
            {
                return null;
            }

            //var reply = GetReply(true);
            var operations = new List<InputMessageContent>();
            var paidOperations = new List<InputPaidMedia>();

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

                    var factory = await MessageFactory.CreateDocumentAsync(item, !audio, item.IsScreenshot);
                    if (factory != null)
                    {
                        var input = factory.Delegate(factory.InputFile, firstCaption);

                        operations.Add(input);
                        firstCaption = null;
                    }
                }
                else if (item is StoragePhoto photo)
                {
                    var factory = await MessageFactory.CreatePhotoAsync(photo, captionAboveMedia, spoiler, photo.Ttl, photo.IsEdited ? photo.EditState : null);
                    if (factory != null)
                    {
                        if (starCount > 0)
                        {
                            var input = factory.PaidDelegate?.Invoke(factory.InputFile);
                            if (input != null)
                            {
                                paidOperations.Add(input);
                            }
                            else
                            {
                                // TODO: error
                            }
                        }
                        else
                        {
                            var input = factory.Delegate(factory.InputFile, firstCaption);

                            operations.Add(input);
                            firstCaption = null;
                        }
                    }
                }
                else if (item is StorageVideo video)
                {
                    var factory = await MessageFactory.CreateVideoAsync(video, video.IsMuted, captionAboveMedia, spoiler, video.Ttl, video.GetTransform());
                    if (factory != null)
                    {
                        if (starCount > 0)
                        {
                            var input = factory.PaidDelegate?.Invoke(factory.InputFile);
                            if (input != null)
                            {
                                paidOperations.Add(input);
                            }
                            else
                            {
                                // TODO: error
                            }
                        }
                        else
                        {
                            var input = factory.Delegate(factory.InputFile, firstCaption);

                            operations.Add(input);
                            firstCaption = null;
                        }
                    }
                }
            }

            if (starCount > 0)
            {
                return await SendMessageAsync(reply, new InputMessagePaidMedia(starCount, paidOperations, caption, captionAboveMedia, string.Empty), options);
            }

            return await ClientService.SendAsync(CreateSendMessageAlbum(chat.Id, ThreadId, reply, options, operations));
        }

        protected virtual Function CreateSendMessageAlbum(long chatId, long messageThreadId, InputMessageReplyTo replyTo, MessageSendOptions messageSendOptions, IList<InputMessageContent> inputMessageContent)
        {
            return new SendMessageAlbum(chatId, messageThreadId, replyTo, messageSendOptions, inputMessageContent);
        }

        public static FormattedText GetFormattedText(string text)
        {
            if (text == null)
            {
                return new FormattedText();
            }

            return ClientEx.ParseMarkdown(text.Format());
        }

        public Task<BaseObject> SendMessageAsync(FormattedText formattedText, MessageSendOptions options = null, InputMessageReplyTo reply = null)
        {
            return SendMessageAsync(formattedText?.Text, formattedText?.Entities, options, reply);
        }

        public async Task<BaseObject> SendMessageAsync(string text, IList<TextEntity> entities = null, MessageSendOptions options = null, InputMessageReplyTo reply = null)
        {
            text ??= string.Empty;
            text = text.Replace('\v', '\n').Replace('\r', '\n');

            if (Chat is not Chat chat)
            {
                return null;
            }

            FormattedText formattedText;
            if (entities == null)
            {
                formattedText = GetFormattedText(text);
            }
            else
            {
                formattedText = new FormattedText(text, entities);
            }

            var applied = await BeforeSendMessageAsync(formattedText);
            if (applied || string.IsNullOrEmpty(formattedText.Text))
            {
                return null;
            }

            options ??= await PickMessageSendOptionsAsync();

            if (options == null)
            {
                return null;
            }

            var disablePreview = DisableWebPreview();
            reply ??= GetReply(options.OnlyPreview == false, options.SchedulingState != null);

            BaseObject response = null;

            if (ClientService.IsDiceEmoji(text, out string dice))
            {
                var input = new InputMessageDice(dice, true);
                await SendMessageAsync(reply, input, options);
            }
            else
            {
                if (text.Length > ClientService.Options.MessageTextLengthMax)
                {
                    foreach (var split in formattedText.Split(ClientService.Options.MessageTextLengthMax))
                    {
                        var input = new InputMessageText(split, disablePreview, true);
                        response = await SendMessageAsync(reply, input, options);
                    }
                }
                else if (text.Length > 0)
                {
                    var input = new InputMessageText(formattedText, disablePreview, true);
                    response = await SendMessageAsync(reply, input, options);
                }
                else
                {
                    await AfterSendMessageAsync();
                }
            }

            return response;
        }

        protected virtual LinkPreviewOptions DisableWebPreview()
        {
            return null;
        }

        protected virtual Task<bool> BeforeSendMessageAsync(FormattedText formattedText)
        {
            return Task.FromResult(false);
        }

        protected virtual Task AfterSendMessageAsync()
        {
            return Task.CompletedTask;
        }
    }
}
