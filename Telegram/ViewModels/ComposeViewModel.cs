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
using Telegram.Controls;
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

    /// <summary>
    /// What the composer contributes to a send, read in one act before any await, so that what
    /// goes out is what the user was looking at when they asked for it.
    /// </summary>
    public class ComposerSnapshot
    {
        /// <summary>
        /// A send that doesn't come from the composer, and so must neither borrow from nor clear it.
        /// </summary>
        public static readonly ComposerSnapshot None = new();

        public InputMessageReplyTo ReplyTo { get; init; }

        public LinkPreviewOptions LinkPreview { get; init; }
    }

    /// <summary>
    /// What the user agreed to when they confirmed a send, and the only thing that drives it
    /// forward from there.
    /// </summary>
    /// <remarks>
    /// Deliberately not a <see cref="MessageSendOptions"/>. TDLib reads its paid_message_star_count
    /// as the total for one request and divides it by the number of messages that request carries,
    /// rejecting a remainder — so a single instance can't be shared across the several requests one
    /// send may issue. <see cref="ToOptions"/> builds a fresh one for each, from a price that is
    /// per message.
    /// </remarks>
    public sealed record SendPlan
    {
        public InputMessageReplyTo ReplyTo { get; init; }

        public LinkPreviewOptions LinkPreview { get; init; }

        public InputSuggestedPostInfo SuggestedPostInfo { get; init; }

        public MessageSchedulingState SchedulingState { get; init; }

        public bool DisableNotification { get; init; }

        public bool UpdateOrderOfInstalledStickerSets { get; init; }

        /// <summary>
        /// Stars the user agreed to pay for each single message.
        /// </summary>
        public long PaidMessageStarCount { get; init; }

        public long EffectId { get; init; }

        public MessageSendOptions ToOptions(int messageCount = 1)
        {
            return new MessageSendOptions(SuggestedPostInfo, DisableNotification, false, PaidMessageStarCount * messageCount, UpdateOrderOfInstalledStickerSets, SchedulingState, EffectId, 0, false);
        }
    }

    public abstract class ComposeViewModel : ViewModelBase
    {
        protected ComposeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public abstract void ViewSticker(Sticker sticker);

        protected abstract void HideStickers();

        /// <summary>
        /// Reads the composer without consuming it. Synchronous by contract: call it before a
        /// send's first await, or it observes a composer the user has since moved on from.
        /// </summary>
        protected abstract ComposerSnapshot PeekComposer();

        /// <summary>
        /// Asks the user for everything the send needs their consent on and, once they have given
        /// it, consumes <paramref name="composer"/>. Returns null if they backed out, leaving the
        /// composer as it was.
        /// </summary>
        protected abstract Task<SendPlan> PrepareSendAsync(ComposerSnapshot composer, int messageCount, SchedulingState schedule, bool? silent, bool reorder, long effectId);

        /// <summary>
        /// Peeks and prepares in one go — for the send paths that have not awaited anything yet.
        /// </summary>
        public Task<SendPlan> PrepareSendAsync(int messageCount = 1, SchedulingState schedule = SchedulingState.Auto, bool? silent = null, bool reorder = false, long effectId = 0)
        {
            return PrepareSendAsync(PeekComposer(), messageCount, schedule, silent, reorder, effectId);
        }

        public abstract FormattedText GetFormattedText(bool clear, bool parseMarkdown);

        protected abstract void SetFormattedText(FormattedText text);

        public abstract Chat Chat { get; set; }

        public abstract MessageTopic TopicId { get; set; }

        public virtual MessageTopic OutgoingTopicId { get; }

        public abstract long ThreadId { get; }

        #region Stickers

        public async void SendSticker(Sticker sticker, SchedulingState schedule, bool? silent, string emoji = null, bool reorder = false)
        {
            var composer = PeekComposer();
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

            var plan = await PrepareSendAsync(composer, 1, schedule, silent, reorder, 0);
            if (plan == null)
            {
                return;
            }

            var input = new InputMessageSticker(new InputSticker(new InputFileId(sticker.StickerValue.Id), sticker.Thumbnail?.ToInput(), sticker.Width, sticker.Height), emoji ?? string.Empty);

            await SendMessageAsync(plan, input);
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
            var composer = PeekComposer();
            HideStickers();

            var restricted = await VerifyRightsAsync(x => x.CanSendOtherMessages, Strings.GlobalAttachGifRestricted, Strings.AttachGifRestrictedForever, Strings.AttachGifRestricted);
            if (restricted)
            {
                return;
            }

            var plan = await PrepareSendAsync(composer, 1, schedule, silent, false, 0);
            if (plan == null)
            {
                return;
            }

            var input = new InputMessageAnimation(new InputAnimation(new InputFileId(animation.AnimationValue.Id), animation.Thumbnail?.ToInput(), Array.Empty<int>(), animation.Duration, animation.Width, animation.Height), null, false, false);

            await SendMessageAsync(plan, input);
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

                var files = await picker.PickMultipleFilesAsync(XamlRoot);
                if (files != null && files.Count > 0)
                {
                    SendFileExecute(files, media: false);
                }
            }
            catch { }
        }

        public async void SendAudio()
        {
            var composer = PeekComposer();
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
                var chat = Chat;

                var operations = new MutableVector<InputMessageContent>();
                var groups = new List<Vector<InputMessageContent>>();

                foreach (var selected in popup.SelectedItems)
                {
                    operations.Add(selected.ToInputMessage());

                    if (operations.Count > 9)
                    {
                        groups.Add(operations);
                        operations = new();
                    }
                }

                if (operations.Count > 0)
                {
                    groups.Add(operations);
                }

                var plan = await PrepareSendAsync(composer, popup.SelectedItems.Count, SchedulingState.Auto, null, false, 0);
                if (plan == null)
                {
                    return;
                }

                if (popup.SelectedItems.Count > 1)
                {
                    foreach (var content in groups)
                    {
                        var function = CreateSendMessageAlbum(chat.Id, OutgoingTopicId, plan.ReplyTo, plan.ToOptions(content.Count), content);
                        if (function == null)
                        {
                            return;
                        }

                        await SendMessageAsync(function);
                    }
                }
                else
                {
                    var function = CreateSendMessage(chat.Id, OutgoingTopicId, plan.ReplyTo, plan.ToOptions(), popup.SelectedItems[0].ToInputMessage());
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

                    var files = await picker.PickMultipleFilesAsync(XamlRoot);
                    if (files != null && files.Count > 0)
                    {
                        SendFileExecute(files, media: false);
                    }
                }
                catch { }
            }
        }

        public void SendFileExecute(IReadOnlyList<StorageFile> files, FormattedText caption = null, bool media = true)
        {
            if (files is { Count: > 0 })
            {
                SendFilesAsync(StorageMediaSource.FromFiles(files), caption, media);
            }
        }

        public void SendFileExecute(IReadOnlyList<StorageMedia> items, FormattedText caption = null, bool media = true)
        {
            if (items is { Count: > 0 })
            {
                SendFilesAsync(StorageMediaSource.FromMedia(items), caption, media);
            }
        }

        private async void SendFilesAsync(StorageMediaSource source, FormattedText caption, bool media)
        {
            if (Chat is not Chat chat)
            {
                return;
            }

            var composer = PeekComposer();

            var permissions = ClientService.GetPermissions(chat, out bool restricted);

            string restrictedError = null;
            var sizeExceeded = false;

            bool Validating(StorageMedia item)
            {
                if (item is StoragePhoto && !permissions.CanSendPhotos)
                {
                    restrictedError = restricted ? Strings.ErrorSendRestrictedPhoto : Strings.ErrorSendRestrictedPhotoAll;
                }
                else if (item is StorageVideo && !permissions.CanSendVideos)
                {
                    restrictedError = restricted ? Strings.ErrorSendRestrictedVideo : Strings.ErrorSendRestrictedVideoAll;
                }
                else if (item is StorageAudio && !permissions.CanSendAudios)
                {
                    restrictedError = restricted ? Strings.ErrorSendRestrictedMusic : Strings.ErrorSendRestrictedMusicAll;
                }
                else if (item is StorageDocument && !permissions.CanSendDocuments)
                {
                    restrictedError = restricted ? Strings.ErrorSendRestrictedDocuments : Strings.ErrorSendRestrictedDocumentsAll;
                }
                else if (item.Size > (4000L << 20) || (item.Size > (2000L << 20) && !IsPremium))
                {
                    sizeExceeded = true;
                }

                return restrictedError == null && !sizeExceeded;
            }

            // Anything already typed is refused before the popup exists, as it always was. Files
            // are refused as they land instead, from inside the popup — Ready is empty for those,
            // so this loop simply does not run.
            foreach (var item in source.Ready)
            {
                if (!Validating(item))
                {
                    break;
                }
            }

            if (restrictedError != null)
            {
                await ShowPopupAsync(restrictedError, Strings.AppName, Strings.OK);
                return;
            }
            else if (sizeExceeded)
            {
                NavigationService.ShowLimitReached(new PremiumLimitTypeFileSize());
                return;
            }

            FormattedText formattedText = null;
            if (caption == null)
            {
                formattedText = GetFormattedText(true, false);
                caption = formattedText.Substring(0, ClientService.Options.MessageCaptionLengthMax);
            }

            var self = ClientService.IsSavedMessages(chat);

            var popup = new SendFilesPopup(this, source, Validating, media, permissions, chat.Type is ChatTypePrivate && !self, CanSchedule, self, false);
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

                // Raised while the popup was up, by an item that only typed itself once probed.
                if (restrictedError != null)
                {
                    await ShowPopupAsync(restrictedError, Strings.AppName, Strings.OK);
                }
                else if (sizeExceeded)
                {
                    NavigationService.ShowLimitReached(new PremiumLimitTypeFileSize());
                }

                return;
            }

            var captionz = popup.Caption;

            var captionAboveMedia = popup.ShowCaptionAboveMedia;
            var hasSpoiler = popup.SendWithSpoiler && !popup.IsFilesSelected;
            var highQuality = popup.SendHighQuality && !popup.IsFilesSelected;

            var itemsView = GetItemsView(popup.Items, popup.IsAlbum, popup.IsFilesSelected, permissions.CanSendPhotos, permissions.CanSendVideos, permissions.CanSendAudios, permissions.CanSendDocuments);

            // If we're sending more than one message, send the caption by itself.
            var captionAlone = itemsView.Count > 1 && captionz != null;

            // What the user is asked to pay for is messages, not the files they picked: every item
            // of an album is one, and a caption that can't ride along with a single item is one more.
            var messageCount = captionAlone ? 1 : 0;

            foreach (var item in itemsView)
            {
                messageCount += item is StorageAlbum album ? album.Media.Count : 1;
            }

            var plan = await PrepareSendAsync(composer, messageCount, popup.Schedule, popup.Silent, false, 0);
            if (plan == null)
            {
                return;
            }

            if (captionAlone)
            {
                await SendTextAsync(captionz, plan);

                captionz = null;
                plan = plan with { ReplyTo = null };
            }

            for (int i = 0; i < itemsView.Count; i++)
            {
                var item = itemsView[i];
                var itemCaption = i < itemsView.Count - 1 ? null : captionz;

                if (item is StorageAlbum album)
                {
                    if (album.Media.Count > 1)
                    {
                        await SendGroupedAsync(album.Media, plan, itemCaption, popup.IsFilesSelected, captionAboveMedia, hasSpoiler, highQuality, popup.StarCount);
                    }
                    else if (album.Media.Count > 0)
                    {
                        await SendStorageMediaAsync(album.Media[0], plan, itemCaption, popup.IsFilesSelected, captionAboveMedia, hasSpoiler, highQuality, popup.StarCount);
                    }
                }
                else
                {
                    await SendStorageMediaAsync(item, plan, itemCaption, popup.IsFilesSelected, captionAboveMedia, hasSpoiler, highQuality, popup.StarCount);
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
                    view.Add(new StorageAlbum(albumType, album));
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

                        if (album.Count >= StorageAlbum.MAX_ITEMS || type == StorageAlbumType.NotSupported || (type != albumType && albumType != StorageAlbumType.None))
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

        private async Task SendStorageMediaAsync(StorageMedia storage, SendPlan plan, FormattedText caption, bool asFile, bool captionAboveMedia, bool spoiler, bool highQuality, long starCount = 0)
        {
            if (storage is StorageDocument or StorageAudio || asFile)
            {
                await SendDocumentAsync(storage, plan, caption);
            }
            else if (storage is StoragePhoto photo)
            {
                await SendPhotoAsync(photo, plan, caption, captionAboveMedia, spoiler, storage.Ttl, highQuality, starCount);
            }
            else if (storage is StorageVideo video)
            {
                await SendVideoAsync(video, plan, caption, video.IsMuted, captionAboveMedia, spoiler, storage.Ttl, starCount);
            }
        }

        private async Task SendDocumentAsync(StorageMedia file, SendPlan plan, FormattedText caption)
        {
            var factory = await MessageFactory.CreateDocumentAsync(file, caption, false);
            if (factory is InputMessageContent input)
            {
                await SendMessageAsync(plan, input);
            }
        }

        private async Task SendPhotoAsync(StoragePhoto file, SendPlan plan, FormattedText caption, bool captionAboveMedia, bool hasSpoiler, MessageSelfDestructType ttl, bool highQuality, long starCount = 0)
        {
            var factory = await MessageFactory.CreatePhotoAsync(file, caption, highQuality, captionAboveMedia, hasSpoiler, ttl, starCount);
            if (factory is InputPaidMedia inputPaidMedia)
            {
                await SendMessageAsync(plan, new InputMessagePaidMedia(starCount, new[] { inputPaidMedia }, caption, captionAboveMedia, string.Empty));
            }
            else if (factory is InputMessageContent input)
            {
                await SendMessageAsync(plan, input);
            }
        }

        public async Task SendVideoAsync(StorageVideo video, SendPlan plan, FormattedText caption, bool animated, bool captionAboveMedia, bool hasSpoiler, MessageSelfDestructType ttl, long starCount = 0)
        {
            var factory = await MessageFactory.CreateVideoAsync(video, caption, animated, captionAboveMedia, hasSpoiler, ttl, starCount);
            if (factory is InputPaidMedia inputPaidMedia)
            {
                await SendMessageAsync(plan, new InputMessagePaidMedia(starCount, new[] { inputPaidMedia }, caption, captionAboveMedia, string.Empty));
            }
            else if (factory is InputMessageContent input)
            {
                await SendMessageAsync(plan, input);
            }
        }

        public async Task SendVideoNoteAsync(StorageVideo video, VideoGeneration generation, MessageSelfDestructType selfDestructType)
        {
            var plan = await PrepareSendAsync();
            if (plan == null)
            {
                return;
            }

            var factory = await MessageFactory.CreateVideoNoteAsync(video, generation, selfDestructType);
            if (factory is InputMessageContent input)
            {
                await SendMessageAsync(plan, input);
            }
        }

        public async Task SendVoiceNoteAsync(StorageFile file, ConversionType conversion, TimeSpan duration, byte[] waveform, FormattedText caption, MessageSelfDestructType selfDestructType)
        {
            var plan = await PrepareSendAsync();
            if (plan == null)
            {
                return;
            }

            // TODO: 172 selfDestructType
            var input = new InputMessageVoiceNote(new InputVoiceNote(await GenerationService.PrepareAsync(file, conversion), (int)Math.Round(duration.TotalSeconds), waveform), caption, selfDestructType);

            await SendMessageAsync(plan, input);
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

                var files = await picker.PickMultipleFilesAsync(XamlRoot);
                if (files != null && files.Count > 0)
                {
                    SendFileExecute(files);
                }
            }
            catch { }
        }

        public async void SendContact()
        {
            var composer = PeekComposer();
            var user = await ChooseChatsPopup.PickUserAsync(ClientService, NavigationService, Strings.ShareContactTitle, true);
            if (user == null)
            {
                return;
            }

            var vcard = string.Empty;
            var contact = new Contact(user.PhoneNumber, user.FirstName, user.LastName, vcard, user.Id);

            var plan = await PrepareSendAsync(composer, 1, SchedulingState.Auto, null, false, 0);
            if (plan == null)
            {
                return;
            }

            await SendContactAsync(contact, plan);

            WatchDog.TrackEvent("SendContact");
        }

        public Task<Object> SendContactAsync(Contact contact, SendPlan plan)
        {
            return SendMessageAsync(plan, new InputMessageContact(contact));
        }

        public void SendContent(InputMessageContent content)
        {
            _ = SendContentAsync(content);
        }

        public async Task<Object> SendContentAsync(InputMessageContent input)
        {
            var plan = await PrepareSendAsync();
            if (plan == null)
            {
                return null;
            }

            return await SendMessageAsync(plan, input);
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

        protected Task<Object> SendMessageAsync(SendPlan plan, InputMessageContent inputMessageContent, int messageCount = 1)
        {
            return SendMessageAsync(plan?.ReplyTo, inputMessageContent, plan?.ToOptions(messageCount));
        }

        /// <summary>
        /// The raw send, for the handful of messages that don't come from the composer and so have
        /// nothing to ask the user about — a bot keyboard's reply, mostly.
        /// </summary>
        protected async Task<Object> SendMessageAsync(InputMessageReplyTo replyTo, InputMessageContent inputMessageContent, MessageSendOptions options)
        {
            if (Chat is not Chat chat)
            {
                return null;
            }

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
            var composer = PeekComposer();
            var popup = new SendLocationPopup(Session);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                var plan = await PrepareSendAsync(composer, 1, SchedulingState.Auto, null, false, 0);
                if (plan == null)
                {
                    return;
                }

                await SendMessageAsync(plan, popup.Media);

                WatchDog.TrackEvent("SendLocation");
            }
        }

        public async void SendPoll()
        {
            await SendPollAsync(true, false, false, Chat?.Type is ChatTypeSupergroup super && super.IsChannel);
        }

        protected async Task SendPollAsync(bool useTextAsQuestion, bool forceQuiz, bool forceRegular, bool channel)
        {
            var composer = PeekComposer();
            var title = GetFormattedText(true, false);
            title = title.Substring(0, ClientService.Options.ChecklistTitleLengthMax);

            var popup = new CreatePollPopup(ClientService, NavigationService, title, forceQuiz, forceRegular, channel);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                SetFormattedText(title);
                return;
            }

            var plan = await PrepareSendAsync(composer, 1, SchedulingState.Auto, null, false, 0);
            if (plan == null)
            {
                return;
            }

            await SendMessageAsync(plan, popup.Input);
        }

        public async void SendChecklist()
        {
            if (IsPremium)
            {
                var composer = PeekComposer();
                var title = GetFormattedText(true, false);
                title = title.Substring(0, ClientService.Options.ChecklistTitleLengthMax);

                var popup = new CreateChecklistPopup(ClientService, title);

                var confirm = await ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    SetFormattedText(title);
                    return;
                }

                var plan = await PrepareSendAsync(composer, 1, SchedulingState.Auto, null, false, 0);
                if (plan == null)
                {
                    return;
                }

                var input = new InputMessageChecklist(new InputChecklist(popup.Title, popup.Tasks, popup.OthersCanAddTasks, popup.OthersCanMarkTasksAsDone));

                await SendMessageAsync(plan, input);
            }
            else
            {
                NavigationService.ShowPromo(new PremiumFeatureChecklists());
            }
        }

        private async Task<Object> SendGroupedAsync(IList<StorageMedia> items, SendPlan plan, FormattedText caption, bool forceDocuments, bool captionAboveMedia, bool hasSpoiler, bool highQuality, long starCount = 0)
        {
            if (Chat is not Chat chat)
            {
                return null;
            }

            var operations = new MutableVector<InputMessageContent>();
            var paidOperations = new MutableVector<InputPaidMedia>();

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
                // Paid media is one message however many items it carries.
                return await SendMessageAsync(plan, new InputMessagePaidMedia(starCount, paidOperations, caption, captionAboveMedia, string.Empty));
            }

            var function = CreateSendMessageAlbum(chat.Id, OutgoingTopicId, plan?.ReplyTo, plan?.ToOptions(operations.Count), operations);
            if (function == null)
            {
                return null;
            }

            return await SendMessageAsync(function);
        }

        protected virtual Function CreateSendMessageAlbum(long chatId, MessageTopic topicId, InputMessageReplyTo replyTo, MessageSendOptions messageSendOptions, Vector<InputMessageContent> inputMessageContent)
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

        public Task<Object> SendTextAsync(string text, SendPlan plan = null)
        {
            return SendTextAsync(GetFormattedText(text ?? string.Empty), plan);
        }

        public async Task<Object> SendTextAsync(FormattedText formattedText, SendPlan plan = null)
        {
            if (Chat == null || formattedText == null)
            {
                return null;
            }

            var composer = plan == null ? PeekComposer() : null;
            var text = formattedText.Text ?? string.Empty;

            // Only text that came out of a composer carries the hint, and only the composer was in a
            // position to work it out. A bot's argument or a sticker's emoji hasn't got one.
            var reorder = formattedText is PreparedText prepared && prepared.UpdateOrderOfInstalledStickerSets;

            // The split is decided before the user is asked, because how many messages it comes to
            // is part of what they're agreeing to; the link preview only exists once they have.
            string dice = null;
            var isDice = (formattedText.Entities?.Count ?? 0) == 0 && ClientService.IsDiceEmoji(text, out dice);

            List<FormattedText> texts = null;
            if (!isDice && text.Length > ClientService.Options.MessageTextLengthMax)
            {
                texts = new List<FormattedText>();

                foreach (var split in formattedText.Split(ClientService.Options.MessageTextLengthMax))
                {
                    texts.Add(split);
                }
            }
            else if (!isDice && text.Length > 0)
            {
                texts = new List<FormattedText> { formattedText };
            }

            var messageCount = isDice ? 1 : texts?.Count ?? 0;
            if (messageCount == 0)
            {
                await AfterSendMessageAsync();
                return null;
            }

            if (plan == null)
            {
                plan = await PrepareSendAsync(composer, messageCount, SchedulingState.Auto, null, reorder, 0);

                if (plan == null)
                {
                    return null;
                }
            }
            else if (reorder && AppSettings.Stickers.DynamicPackOrder)
            {
                // The box reports the raw fact; whether to act on it is the user's setting, and a
                // plan built elsewhere has not seen this text.
                plan = plan with { UpdateOrderOfInstalledStickerSets = true };
            }

            if (isDice)
            {
                return await SendMessageAsync(plan, new InputMessageDice(dice, true));
            }

            Object response = null;
            foreach (var content in texts)
            {
                response = await SendMessageAsync(plan, new InputMessageText(content, plan.LinkPreview, true));
            }

            return response;
        }

        /// <summary>
        /// How many messages a text is going to be sent as — the user is asked to pay for each
        /// piece of a text too long to fit in one. Matches what <see cref="TdExtensions.Split"/> does.
        /// </summary>
        protected int CountMessages(FormattedText formattedText)
        {
            var length = formattedText?.Text?.Length ?? 0;
            if (length == 0)
            {
                return 0;
            }

            var maxLength = ClientService.Options.MessageTextLengthMax;
            return length > maxLength ? (int)Math.Ceiling(length / (double)maxLength) : 1;
        }

        protected virtual Task AfterSendMessageAsync()
        {
            return Task.CompletedTask;
        }
    }
}
