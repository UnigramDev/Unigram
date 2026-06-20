//
// Copyright (c) Fela Ameghino 2015-2026
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
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public enum SchedulingState
    {
        None,
        Auto,
        Schedule,
        WhenOnline
    }

    public abstract class ComposeViewModel : ViewModelBase
    {
        protected ComposeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public abstract void ViewSticker(Sticker sticker);

        protected abstract void HideStickers();

        protected abstract InputMessageReplyTo GetReply(bool clear, bool notify = true);

        public abstract FormattedText GetFormattedText(bool clear, bool parseMarkdown);

        protected abstract void SetFormattedText(FormattedText text);

        public abstract Chat Chat { get; set; }

        public abstract MessageTopic TopicId { get; set; }

        public virtual MessageTopic OutgoingTopicId { get; }

        public abstract long ThreadId { get; }

        #region Stickers

        public async void SendSticker(Sticker sticker, SchedulingState schedule, bool? silent, string emoji = null, bool reorder = false)
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

            var options = await PickMessageSendOptionsAsync(1, schedule, silent, reorder);
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
            SendAnimation(animation, SchedulingState.Auto, null);
        }

        public async void SendAnimation(Animation animation, SchedulingState schedule, bool? silent)
        {
            HideStickers();

            var restricted = await VerifyRightsAsync(x => x.CanSendOtherMessages, Strings.GlobalAttachGifRestricted, Strings.AttachGifRestrictedForever, Strings.AttachGifRestricted);
            if (restricted)
            {
                return;
            }

            var options = await PickMessageSendOptionsAsync(1, schedule, silent);
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = new InputMessageAnimation(new InputAnimation(new InputFileId(animation.AnimationValue.Id), animation.Thumbnail?.ToInput(), Array.Empty<int>(), animation.Duration, animation.Width, animation.Height), null, false, false);

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

        public async void SendAudio()
        {
            var restricted = await VerifyRightsAsync(x => x.CanSendAudios,
                Strings.ErrorSendRestrictedMusicAll,
                Strings.ErrorSendRestrictedMusic,
                Strings.ErrorSendRestrictedMusic);
            if (restricted)
            {
                return;
            }

            var popup = new SendAudiosPopup(ClientService, NavigationService);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            if (popup.SelectedItems?.Count > 0)
            {
                var options = await PickMessageSendOptionsAsync();
                if (options == null)
                {
                    return;
                }

                var reply = GetReply(true);
                var chat = Chat;

                if (popup.SelectedItems.Count > 1)
                {
                    var operations = new List<InputMessageContent>();
                    var groups = new List<List<InputMessageContent>>();

                    foreach (var selected in popup.SelectedItems)
                    {
                        operations.Add(selected.ToInputMessage());

                        if (operations.Count > 9)
                        {
                            groups.Add(operations);
                            operations = new();
                        }
                    }

                    groups.Add(operations);

                    foreach (var content in groups)
                    {
                        var function = CreateSendMessageAlbum(chat.Id, OutgoingTopicId, reply, options, content);
                        if (function == null)
                        {
                            return;
                        }

                        await SendMessageAsync(function);
                    }
                }
                else
                {
                    var function = CreateSendMessage(chat.Id, OutgoingTopicId, reply, options, popup.SelectedItems[0].ToInputMessage());
                    if (function == null)
                    {
                        return;
                    }

                    await SendMessageAsync(function);
                }
            }
            else
            {
                try
                {
                    var picker = new FileOpenPicker();
                    picker.ViewMode = PickerViewMode.Thumbnail;
                    picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                    picker.FileTypeFilter.Add("*");

                    var files = await picker.PickMultipleFilesAsync();
                    if (files != null && files.Count > 0)
                    {
                        SendFileExecute(files, media: false);
                    }
                }
                catch { }
            }
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

            var permissions = ClientService.GetPermissions(chat, out bool restricted);

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
                else if (item.Size > (4000L << 20) || (item.Size > (2000L << 20) && !IsPremium))
                {
                    NavigationService.ShowLimitReached(new PremiumLimitTypeFileSize());
                    return;
                }
            }

            FormattedText formattedText = null;
            if (caption == null)
            {
                formattedText = GetFormattedText(true, false);
                caption = formattedText.Substring(0, ClientService.Options.MessageCaptionLengthMax);
            }

            var self = ClientService.IsSavedMessages(chat);

            var popup = new SendFilesPopup(this, items, media, permissions, chat.Type is ChatTypePrivate && !self, CanSchedule, self, false);
            popup.Loaded += (s, args) =>
            {
                popup.Caption = caption;
            };

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

            var options = await PickMessageSendOptionsAsync(popup.Items.Count, popup.Schedule, popup.Silent);
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var captionz = popup.Caption;

            var captionAboveMedia = popup.ShowCaptionAboveMedia;
            var hasSpoiler = popup.SendWithSpoiler && !popup.IsFilesSelected;
            var highQuality = popup.SendHighQuality && !popup.IsFilesSelected;

            var itemsView = GetItemsView(popup.Items, popup.IsAlbum, popup.IsFilesSelected, permissions.CanSendPhotos, permissions.CanSendVideos, permissions.CanSendAudios, permissions.CanSendDocuments);

            // If we're sending more than one message, send the caption by itself.
            if (itemsView.Count > 1 && captionz != null)
            {
                await SendMessageAsync(captionz, null, options, reply);
                captionz = null;
                reply = null;
            }

            for (int i = 0; i < itemsView.Count; i++)
            {
                var item = itemsView[i];
                var itemCaption = i < itemsView.Count - 1 ? null : captionz;

                if (item is StorageAlbum album)
                {
                    if (album.Media.Count > 1)
                    {
                        await SendGroupedAsync(album.Media, reply, itemCaption, options, popup.IsFilesSelected, captionAboveMedia, hasSpoiler, highQuality, popup.StarCount);
                    }
                    else if (album.Media.Count > 0)
                    {
                        await SendStorageMediaAsync(album.Media[0], reply, itemCaption, options, popup.IsFilesSelected, captionAboveMedia, hasSpoiler, highQuality, popup.StarCount);
                    }
                }
                else
                {
                    await SendStorageMediaAsync(item, reply, itemCaption, options, popup.IsFilesSelected, captionAboveMedia, hasSpoiler, highQuality, popup.StarCount);
                }
            }
        }

        public static IList<StorageMedia> GetItemsView(IList<StorageMedia> items, bool albumAllowed, bool forceDocuments, bool photoAllowed, bool videoAllowed, bool audioAllowed, bool documentAllowed)
        {
            var view = new List<StorageMedia>();
            var album = new List<StorageMedia>();
            var albumType = StorageAlbumType.None;

            void AddAlbum()
            {
                if (album.Count > 0)
                {
                    view.Add(new StorageAlbum(album));
                    album = new List<StorageMedia>();
                }
            }

            foreach (var item in items)
            {
                if ((item is StorageDocument && documentAllowed) || (item is StoragePhoto && photoAllowed) || (item is StorageVideo && videoAllowed) || (item is StorageAudio && audioAllowed))
                {
                    if (albumAllowed)
                    {
                        if (item is StorageVideo { IsMuted: true } && !forceDocuments)
                        {
                            AddAlbum();

                            albumType = StorageAlbumType.None;
                            view.Add(item);

                            continue;
                        }
                        // TODO: there's a bug server-side that ignores force_file while processing WEBP documents in a album
                        // this makes the whole album upload to fail. We work this around by always breaking WEBP upload to a single message.
                        else if (item is StorageDocument document && document.File.HasExtension(".webp"))
                        {
                            AddAlbum();

                            albumType = StorageAlbumType.None;
                            view.Add(item);

                            continue;
                        }

                        var type = item switch
                        {
                            StorageDocument => StorageAlbumType.Documents,
                            StorageAudio => StorageAlbumType.Audio,
                            StoragePhoto photo => forceDocuments ? StorageAlbumType.Documents : photo.IsAnimated ? StorageAlbumType.NotSupported : StorageAlbumType.Media,
                            _ => forceDocuments ? StorageAlbumType.Documents : StorageAlbumType.Media
                        };

                        if (album.Count > 9 || type == StorageAlbumType.NotSupported || (type != albumType && albumType != StorageAlbumType.None))
                        {
                            AddAlbum();
                        }

                        albumType = type;
                        album.Add(item);
                    }
                    else
                    {
                        view.Add(item);
                    }
                }
            }

            AddAlbum();
            return view;
        }

        protected abstract bool CanSchedule { get; }

        private async Task SendStorageMediaAsync(StorageMedia storage, InputMessageReplyTo reply, FormattedText caption, MessageSendOptions options, bool asFile, bool captionAboveMedia, bool spoiler, bool highQuality, long starCount = 0)
        {
            if (storage is StorageDocument or StorageAudio || asFile)
            {
                await SendDocumentAsync(storage, reply, caption, options);
            }
            else if (storage is StoragePhoto photo)
            {
                await SendPhotoAsync(photo, reply, caption, captionAboveMedia, spoiler, storage.Ttl, highQuality, options, starCount);
            }
            else if (storage is StorageVideo video)
            {
                await SendVideoAsync(video, reply, caption, video.IsMuted, captionAboveMedia, spoiler, storage.Ttl, options, starCount);
            }
        }

        private async Task SendDocumentAsync(StorageMedia file, InputMessageReplyTo reply, FormattedText caption, MessageSendOptions options)
        {
            var factory = await MessageFactory.CreateDocumentAsync(file, caption, false);
            if (factory is InputMessageContent input)
            {
                await SendMessageAsync(reply, input, options);
            }
        }

        private async Task SendPhotoAsync(StoragePhoto file, InputMessageReplyTo reply, FormattedText caption, bool captionAboveMedia, bool hasSpoiler, MessageSelfDestructType ttl, bool highQuality, MessageSendOptions options, long starCount = 0)
        {
            var factory = await MessageFactory.CreatePhotoAsync(file, caption, highQuality, captionAboveMedia, hasSpoiler, ttl, starCount);
            if (factory is InputPaidMedia inputPaidMedia)
            {
                await SendMessageAsync(reply, new InputMessagePaidMedia(starCount, new[] { inputPaidMedia }, caption, captionAboveMedia, string.Empty), options);
            }
            else if (factory is InputMessageContent input)
            {
                await SendMessageAsync(reply, input, options);
            }
        }

        public async Task SendVideoAsync(StorageVideo video, InputMessageReplyTo reply, FormattedText caption, bool animated, bool captionAboveMedia, bool hasSpoiler, MessageSelfDestructType ttl, MessageSendOptions options, long starCount = 0)
        {
            var factory = await MessageFactory.CreateVideoAsync(video, caption, animated, captionAboveMedia, hasSpoiler, ttl, starCount);
            if (factory is InputPaidMedia inputPaidMedia)
            {
                await SendMessageAsync(reply, new InputMessagePaidMedia(starCount, new[] { inputPaidMedia }, caption, captionAboveMedia, string.Empty), options);
            }
            else if (factory is InputMessageContent input)
            {
                await SendMessageAsync(reply, input, options);
            }
        }

        public async Task SendVideoNoteAsync(StorageVideo video, VideoGeneration generation, MessageSelfDestructType selfDestructType)
        {
            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            var factory = await MessageFactory.CreateVideoNoteAsync(video, generation, selfDestructType);
            if (factory is InputMessageContent input)
            {
                var reply = GetReply(true);

                await SendMessageAsync(reply, input, options);
            }
        }

        public async Task SendVoiceNoteAsync(StorageFile file, int duration, FormattedText caption, MessageSelfDestructType selfDestructType)
        {
            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            // TODO: 172 selfDestructType
            var reply = GetReply(true);
            var input = new InputMessageVoiceNote(await file.ToGeneratedAsync(ConversionType.Opus), duration, Array.Empty<byte>(), caption, selfDestructType);

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

            WatchDog.TrackEvent("SendContact");
        }

        public Task<Object> SendContactAsync(Contact contact, MessageSendOptions options)
        {
            var reply = GetReply(true);
            var input = new InputMessageContact(contact);

            return SendMessageAsync(reply, input, options);
        }

        public void SendContent(InputMessageContent content)
        {
            _ = SendContentAsync(content);
        }

        public async Task<Object> SendContentAsync(InputMessageContent input)
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

        public abstract Task<MessageSendOptions> PickMessageSendOptionsAsync(int messageCount = 1, SchedulingState schedulingState = SchedulingState.None, bool? disableNotification = null, bool reorder = false);

        protected async Task<Object> SendMessageAsync(InputMessageReplyTo replyTo, InputMessageContent inputMessageContent, MessageSendOptions options)
        {
            if (Chat is not Chat chat)
            {
                return null;
            }

            InsertedCustomEmojiIds.Clear();

            options ??= new MessageSendOptions();
            options.SendingId = Math.Max(options.SendingId, 1);

            var function = CreateSendMessage(chat.Id, OutgoingTopicId, replyTo, options, inputMessageContent);
            if (function == null)
            {
                return null;
            }

            return await SendMessageAsync(function);
        }

        protected async Task<Object> SendMessageAsync(Function function)
        {
            var response = await ClientService.SendAsync(function);
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
            else if (function is SendMessage sendMessage)
            {
                ContinueSendMessage(sendMessage.Options);
            }
            else if (function is SendMessageAlbum sendMessageAlbum)
            {
                ContinueSendMessage(sendMessageAlbum.Options);
            }

            return response;
        }

        protected virtual Function CreateSendMessage(long chatId, MessageTopic topicId, InputMessageReplyTo replyTo, MessageSendOptions messageSendOptions, InputMessageContent inputMessageContent)
        {
            if (replyTo is InputMessageReplyToTopicMessage replyToTopicMessage)
            {
                topicId = replyToTopicMessage.TopicId;
                replyTo = new InputMessageReplyToMessage(replyToTopicMessage.MessageId, replyToTopicMessage.Quote, replyToTopicMessage.ChecklistTaskId, replyToTopicMessage.PollOptionId);
            }

            return new SendMessage(chatId, topicId, replyTo, messageSendOptions, inputMessageContent);
        }

        protected virtual void ContinueSendMessage(MessageSendOptions options)
        {

        }

        public async void SendLocation()
        {
            var popup = new SendLocationPopup(Session);

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

                WatchDog.TrackEvent("SendLocation");
            }
        }

        public async void SendPoll()
        {
            await SendPollAsync(true, false, false, Chat?.Type is ChatTypeSupergroup super && super.IsChannel);
        }

        protected async Task SendPollAsync(bool useTextAsQuestion, bool forceQuiz, bool forceRegular, bool channel)
        {
            var title = GetFormattedText(true, false);
            title = title.Substring(0, ClientService.Options.ChecklistTitleLengthMax);

            var popup = new CreatePollPopup(ClientService, NavigationService, title, forceQuiz, forceRegular, channel);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                SetFormattedText(title);
                return;
            }

            var options = await PickMessageSendOptionsAsync();
            if (options == null)
            {
                return;
            }

            var reply = GetReply(true);
            var input = popup.Input;

            await SendMessageAsync(reply, input, options);
        }

        public async void SendChecklist()
        {
            if (IsPremium)
            {
                var title = GetFormattedText(true, false);
                title = title.Substring(0, ClientService.Options.ChecklistTitleLengthMax);

                var popup = new CreateChecklistPopup(ClientService, title);

                var confirm = await ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    SetFormattedText(title);
                    return;
                }

                var options = await PickMessageSendOptionsAsync();
                if (options == null)
                {
                    return;
                }

                var reply = GetReply(true);
                var input = new InputMessageChecklist(new InputChecklist(popup.Title, popup.Tasks, popup.OthersCanAddTasks, popup.OthersCanMarkTasksAsDone));

                await SendMessageAsync(reply, input, options);
            }
            else
            {
                NavigationService.ShowPromo(new PremiumFeatureChecklists());
            }
        }

        private async Task<Object> SendGroupedAsync(IList<StorageMedia> items, InputMessageReplyTo reply, FormattedText caption, MessageSendOptions options, bool forceDocuments, bool captionAboveMedia, bool hasSpoiler, bool highQuality, long starCount = 0)
        {
            if (Chat is not Chat chat)
            {
                return null;
            }

            //var reply = GetReply(true);
            var operations = new List<InputMessageContent>();
            var paidOperations = new List<InputPaidMedia>();

            var audio = items.All(x => x is StorageAudio);

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (forceDocuments || item is StorageAudio)
                {
                    var factory = await MessageFactory.CreateDocumentAsync(item, i == items.Count - 1 ? caption : null, !audio || item is not StorageAudio);
                    if (factory is InputMessageContent input)
                    {
                        operations.Add(input);
                    }
                }
                else if (item is StoragePhoto photo)
                {
                    var factory = await MessageFactory.CreatePhotoAsync(photo, i == 0 ? caption : null, highQuality, captionAboveMedia, hasSpoiler, photo.Ttl, starCount);
                    if (factory is InputPaidMedia inputPaidMedia)
                    {
                        paidOperations.Add(inputPaidMedia);
                    }
                    else if (factory is InputMessageContent input)
                    {
                        operations.Add(input);
                    }
                }
                else if (item is StorageVideo video)
                {
                    var factory = await MessageFactory.CreateVideoAsync(video, i == 0 ? caption : null, video.IsMuted, captionAboveMedia, hasSpoiler, video.Ttl, starCount);
                    if (factory is InputPaidMedia inputPaidMedia)
                    {
                        paidOperations.Add(inputPaidMedia);
                    }
                    else if (factory is InputMessageContent input)
                    {
                        operations.Add(input);
                    }
                }
            }

            if (starCount > 0)
            {
                return await SendMessageAsync(reply, new InputMessagePaidMedia(starCount, paidOperations, caption, captionAboveMedia, string.Empty), options);
            }

            var function = CreateSendMessageAlbum(chat.Id, OutgoingTopicId, reply, options, operations);
            if (function == null)
            {
                return null;
            }

            return await SendMessageAsync(function);
        }

        protected virtual Function CreateSendMessageAlbum(long chatId, MessageTopic topicId, InputMessageReplyTo replyTo, MessageSendOptions messageSendOptions, IList<InputMessageContent> inputMessageContent)
        {
            if (replyTo is InputMessageReplyToTopicMessage replyToTopicMessage)
            {
                topicId = replyToTopicMessage.TopicId;
                replyTo = new InputMessageReplyToMessage(replyToTopicMessage.MessageId, replyToTopicMessage.Quote, replyToTopicMessage.ChecklistTaskId, replyToTopicMessage.PollOptionId);
            }

            return new SendMessageAlbum(chatId, topicId, replyTo, messageSendOptions, inputMessageContent);
        }

        public static FormattedText GetFormattedText(string text)
        {
            if (text == null)
            {
                return new FormattedText();
            }

            return ClientEx.ParseMarkdown(text.Format());
        }

        public HashSet<long> InsertedCustomEmojiIds = new();

        public Task<Object> SendMessageAsync(FormattedText formattedText, LinkPreviewOptions linkPreview = null, MessageSendOptions options = null, InputMessageReplyTo reply = null)
        {
            return SendMessageAsync(formattedText?.Text, formattedText?.Entities, linkPreview, options, reply);
        }

        public async Task<Object> SendMessageAsync(string text, IList<TextEntity> entities = null, LinkPreviewOptions linkPreview = null, MessageSendOptions options = null, InputMessageReplyTo reply = null)
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

            var reorder = TextStillContainsEmojis(formattedText.Entities);
            InsertedCustomEmojiIds.Clear();

            var applied = await BeforeSendMessageAsync(formattedText, linkPreview);
            if (applied || string.IsNullOrEmpty(formattedText.Text))
            {
                return null;
            }

            options ??= await PickMessageSendOptionsAsync(reorder: reorder);

            if (options == null)
            {
                return null;
            }

            options.UpdateOrderOfInstalledStickerSets = reorder;
            reply ??= GetReply(options.OnlyPreview == false, options.SchedulingState != null);

            Object response = null;

            if (formattedText.Entities.Count == 0 && ClientService.IsDiceEmoji(text, out string dice))
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
                        var input = new InputMessageText(split, linkPreview, true);
                        response = await SendMessageAsync(reply, input, options);
                    }
                }
                else if (text.Length > 0)
                {
                    var input = new InputMessageText(formattedText, linkPreview, true);
                    response = await SendMessageAsync(reply, input, options);
                }
                else
                {
                    await AfterSendMessageAsync();
                }
            }

            return response;
        }

        private bool TextStillContainsEmojis(IList<TextEntity> entities)
        {
            if (entities.Count == 0 || !Settings.Stickers.DynamicPackOrder)
            {
                return false;
            }

            foreach (var entity in entities)
            {
                if (entity.Type is TextEntityTypeCustomEmoji customEmoji && InsertedCustomEmojiIds.Contains(customEmoji.CustomEmojiId))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual LinkPreviewOptions GetLinkPreviewOptions()
        {
            return null;
        }

        protected virtual Task<bool> BeforeSendMessageAsync(FormattedText formattedText, LinkPreviewOptions options)
        {
            return Task.FromResult(false);
        }

        protected virtual Task AfterSendMessageAsync()
        {
            return Task.CompletedTask;
        }
    }
}
