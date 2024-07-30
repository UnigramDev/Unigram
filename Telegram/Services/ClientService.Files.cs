//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Td.Api;
using Windows.Storage;
using Future = Telegram.Services.StorageService.Future;

namespace Telegram.Services
{
    public partial class ClientService
    {
        /*
         * How does this work?
         * 
         * As a general rule, all files are downloaded by TDLib into the app cache.
         * The goal however, is to make the local cache folder invisible to the user,
         * and to only provide access to the files through the Downloads folder instead.
         * 
         * # Automatic downloads
         * Nothing happens in this case, automatic downloads always end up in cache.
         * 
         * # Manual downloads
         * All the downloads that pass through the download manager (aka manual downloads)
         * are automatically copied to the user Downloads folder as soon as the download is completed.
         * We do this operation in two steps:
         * 
         * 1. AddFileToDownloads
         * - When the download is started, a temporary file is created in the final location.
         * - The file will look something like this: Unconfirmed {fileId}.tdownload
         * - The file is then added to the system FutureAccessList using the file UniqueId+temp as token.
         * Note: this only happens if FutureAccessList doesn't contain any of UniqueId or UniqueId+temp tokens.
         * 
         * 2. TrackDownloadedFile
         * - Whenever an UpdateFile event is received and the download is actually completed,
         * - we check in the FutureAccessList if there's any file belonging to it, by using UniqueId+temp as token.
         * - if this is the case, we retrieve both the file from cache and the temporary file in the Downloads folder.
         * - we then proceed by replacing the latter with a copy with the cache file, that is then renamed with the final name.
         * - finally we can remove UniqueId+temp from FutureAccessList and add the final UniqueId to the list.
         * 
         * # Using the files
         * The app will always rely on TDLib LocalFile to determine a file status.
         * This means that if the user clears the app cache, the link between cached and permanent files will be broken.
         * This considered, the user must be able to perform different actions on the downloaded files, including:
         * 
         * 1. OpenFile(With)Async and OpenFolderAsync (IStorageService)
         * - We make sure that the LocalFile from TDLib reports IsDownloadingCompleted as true
         * - If yes, we try to retrieve the permanent file from FutureAccessList using UniqueId
         *   - If the permanent file doesn't exist or it was edited after being copied, we do nothing
         *   - Otherwise we create a new unique copy of the file in the Downloads folder and we add it to the FutureAccessList
         * - We launch the file
         * 
         * 2. SaveFileAsAsync (IStorageService)
         * - We make sure that the LocalFile from TDLib reports IsDownloadingCompleted as true
         * - If yes, we try to retrieve the cache file
         *   - We save the copy
         * - If not, and the download didn't start yet
         *   - We call AddFileToDownloads passing the custom location
         * 
         * # Other scenarios
         * All the stuff that needs to be also considered:
         * 
         * 1. User manually deletes the permanent file
         * FutureAccessList is not kept synchronized by the system, so it's not enough to call ContainsItem,
         * a try-catch on GetFileAsync is needed to make sure that the file is still accessible.
         * Note: the file will still be visible as "downloaded" within the app.
         * 
         */

        private readonly HashSet<int> _canceledDownloads = new();
        private readonly HashSet<string> _completedDownloads = new();

        public async Task<StorageFile> GetFileAsync(File file, bool completed = true)
        {
            // Extremely important to do this only for completed,
            // as this method is being used by RemoteFileStream as well.
            if (completed)
            {
                await SendAsync(new DownloadFile(file.Id, 16, 0, 0, false));
            }

            if (file.Local.IsDownloadingCompleted || !completed)
            {
                try
                {
                    return await StorageFile.GetFileFromPathAsync(file.Local.Path);
                }
                catch (System.IO.FileNotFoundException)
                {
                    Send(new DeleteFileW(file.Id));
                }
                catch { }

                return null;
            }

            return null;
        }

        public async Task<StorageFile> GetPermanentFileAsync(File file)
        {
            if (file == null)
            {
                return null;
            }
            else if (ApiInfo.HasCacheOnly || !SettingsService.Current.IsDownloadFolderEnabled)
            {
                return await GetFileAsync(file, true);
            }

            // Let's TDLib check the file integrity
            if (file.Local.IsDownloadingCompleted)
            {
                await SendAsync(new DownloadFile(file.Id, 16, 0, 0, false));
            }

            // If it's still valid, we can proceed with the operation
            if (file.Local.IsDownloadingCompleted && file.Remote.UniqueId.Length > 0)
            {
                try
                {
                    var permanent = await Future.GetFileAsync(file.Remote.UniqueId);
                    if (permanent == null)
                    {
                        _completedDownloads.Add(file.Remote.UniqueId);

                        var source = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                        if (Future.CheckAccess(source))
                        {
                            return source;
                        }
                        else
                        {
                            var destination = await Future.CreateFileAsync(source.Name);

                            await source.CopyAndReplaceAsync(destination);
                            Future.AddOrReplace(file.Remote.UniqueId, destination);

                            return destination;
                        }
                    }

                    return permanent;
                }
                catch
                {
                    Future.Remove(file.Remote.UniqueId);
                }
            }

            return null;
        }

        public async void AddFileToDownloads(File file, long chatId, long messageId, int priority = 30)
        {
            Send(new AddFileToDownloads(file.Id, chatId, messageId, priority));

            if (ApiInfo.HasCacheOnly || !SettingsService.Current.IsDownloadFolderEnabled || Future.Contains(file.Remote.UniqueId, true) || await Future.ContainsAsync(file.Remote.UniqueId))
            {
                return;
            }

            try
            {
                StorageFile destination = await Future.CreateFileAsync($"Unconfirmed {file.Id}.tdownload");
                Future.AddOrReplace(file.Remote.UniqueId, destination, true);
            }
            catch
            {
                Future.Remove(file.Remote.UniqueId, true);
            }
        }

        private async void TrackDownloadedFile(File file)
        {
            if (ApiInfo.HasDownloadFolder
                && SettingsService.Current.IsDownloadFolderEnabled
                && file.Local.IsDownloadingCompleted
                && file.Remote.IsUploadingCompleted
                && Future.Contains(file.Remote.UniqueId, true))
            {
                if (_completedDownloads.Contains(file.Remote.UniqueId))
                {
                    return;
                }

                _completedDownloads.Add(file.Remote.UniqueId);

                try
                {
                    StorageFile source = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    StorageFile destination = await Future.GetFileAsync(file.Remote.UniqueId, true);

                    await source.CopyAndReplaceAsync(destination);
                    await destination.RenameAsync(source.Name, NameCollisionOption.GenerateUniqueName);

                    Future.Remove(file.Remote.UniqueId, true);
                    Future.AddOrReplace(file.Remote.UniqueId, destination);
                }
                catch
                {
                    Future.Remove(file.Remote.UniqueId, true);
                }
            }
        }

        public async void CancelDownloadFile(File file, bool onlyIfPending = false)
        {
            _canceledDownloads.Add(file.Id);
            _completedDownloads.Remove(file.Remote.UniqueId);

            Send(new CancelDownloadFile(file.Id, onlyIfPending));
            Send(new RemoveFileFromDownloads(file.Id, false));

            if (ApiInfo.HasCacheOnly)
            {
                return;
            }

            try
            {
                var destination = await Future.GetFileAsync(file.Remote.UniqueId, true);

                Future.Remove(file.Remote.UniqueId, true);

                if (destination != null)
                {
                    await destination.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public bool IsDownloadFileCanceled(int fileId)
        {
            return _canceledDownloads.Contains(fileId);
        }

        private File ProcessFile(File file)
        {
            if (_files.TryGetValue(file.Id, out File singleton))
            {
                singleton.Update(file);
                return singleton;
            }
            else
            {
                _files[file.Id] = file;

                if (file.Local.IsDownloadingCompleted && !NativeUtils.FileExists(file.Local.Path))
                {
                    Send(new DeleteFileW(file.Id));
                }

                return file;
            }
        }

        public void ProcessFiles(object target)
        {
            if (target is AnimatedChatPhoto animatedChatPhoto)
            {
                if (animatedChatPhoto.File != null)
                {
                    animatedChatPhoto.File = ProcessFile(animatedChatPhoto.File);
                }
            }
            else if (target is AnimatedEmoji animatedEmoji)
            {
                if (animatedEmoji.Sound != null)
                {
                    animatedEmoji.Sound = ProcessFile(animatedEmoji.Sound);
                }
                if (animatedEmoji.Sticker != null)
                {
                    ProcessFiles(animatedEmoji.Sticker);
                }
            }
            else if (target is Animation animation)
            {
                if (animation.AnimationValue != null)
                {
                    animation.AnimationValue = ProcessFile(animation.AnimationValue);
                }
                if (animation.Thumbnail != null)
                {
                    ProcessFiles(animation.Thumbnail);
                }
            }
            else if (target is Animations animations)
            {
                foreach (var item in animations.AnimationsValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is AttachmentMenuBot attachmentMenuBot)
            {
                if (attachmentMenuBot.AndroidIcon != null)
                {
                    attachmentMenuBot.AndroidIcon = ProcessFile(attachmentMenuBot.AndroidIcon);
                }
                if (attachmentMenuBot.AndroidSideMenuIcon != null)
                {
                    attachmentMenuBot.AndroidSideMenuIcon = ProcessFile(attachmentMenuBot.AndroidSideMenuIcon);
                }
                if (attachmentMenuBot.DefaultIcon != null)
                {
                    attachmentMenuBot.DefaultIcon = ProcessFile(attachmentMenuBot.DefaultIcon);
                }
                if (attachmentMenuBot.IosAnimatedIcon != null)
                {
                    attachmentMenuBot.IosAnimatedIcon = ProcessFile(attachmentMenuBot.IosAnimatedIcon);
                }
                if (attachmentMenuBot.IosSideMenuIcon != null)
                {
                    attachmentMenuBot.IosSideMenuIcon = ProcessFile(attachmentMenuBot.IosSideMenuIcon);
                }
                if (attachmentMenuBot.IosStaticIcon != null)
                {
                    attachmentMenuBot.IosStaticIcon = ProcessFile(attachmentMenuBot.IosStaticIcon);
                }
                if (attachmentMenuBot.MacosIcon != null)
                {
                    attachmentMenuBot.MacosIcon = ProcessFile(attachmentMenuBot.MacosIcon);
                }
                if (attachmentMenuBot.MacosSideMenuIcon != null)
                {
                    attachmentMenuBot.MacosSideMenuIcon = ProcessFile(attachmentMenuBot.MacosSideMenuIcon);
                }
                if (attachmentMenuBot.WebAppPlaceholder != null)
                {
                    attachmentMenuBot.WebAppPlaceholder = ProcessFile(attachmentMenuBot.WebAppPlaceholder);
                }
            }
            else if (target is Audio audio)
            {
                if (audio.AlbumCoverThumbnail != null)
                {
                    ProcessFiles(audio.AlbumCoverThumbnail);
                }
                if (audio.AudioValue != null)
                {
                    audio.AudioValue = ProcessFile(audio.AudioValue);
                }
                foreach (var item in audio.ExternalAlbumCovers)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is Background background)
            {
                if (background.Document != null)
                {
                    ProcessFiles(background.Document);
                }
            }
            else if (target is Backgrounds backgrounds)
            {
                foreach (var item in backgrounds.BackgroundsValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is BasicGroupFullInfo basicGroupFullInfo)
            {
                if (basicGroupFullInfo.Photo != null)
                {
                    ProcessFiles(basicGroupFullInfo.Photo);
                }
            }
            else if (target is BotInfo botInfo)
            {
                if (botInfo.Animation != null)
                {
                    ProcessFiles(botInfo.Animation);
                }
                if (botInfo.Photo != null)
                {
                    ProcessFiles(botInfo.Photo);
                }
            }
            else if (target is BotMediaPreview botMediaPreview)
            {
                if (botMediaPreview.Content != null)
                {
                    ProcessFiles(botMediaPreview.Content);
                }
            }
            else if (target is BotMediaPreviewInfo botMediaPreviewInfo)
            {
                foreach (var item in botMediaPreviewInfo.Previews)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is BotMediaPreviews botMediaPreviews)
            {
                foreach (var item in botMediaPreviews.Previews)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is BotWriteAccessAllowReasonLaunchedWebApp botWriteAccessAllowReasonLaunchedWebApp)
            {
                if (botWriteAccessAllowReasonLaunchedWebApp.WebApp != null)
                {
                    ProcessFiles(botWriteAccessAllowReasonLaunchedWebApp.WebApp);
                }
            }
            else if (target is BusinessFeaturePromotionAnimation businessFeaturePromotionAnimation)
            {
                if (businessFeaturePromotionAnimation.Animation != null)
                {
                    ProcessFiles(businessFeaturePromotionAnimation.Animation);
                }
            }
            else if (target is BusinessInfo businessInfo)
            {
                if (businessInfo.StartPage != null)
                {
                    ProcessFiles(businessInfo.StartPage);
                }
            }
            else if (target is BusinessMessage businessMessage)
            {
                if (businessMessage.Message != null)
                {
                    ProcessFiles(businessMessage.Message);
                }
                if (businessMessage.ReplyToMessage != null)
                {
                    ProcessFiles(businessMessage.ReplyToMessage);
                }
            }
            else if (target is BusinessMessages businessMessages)
            {
                foreach (var item in businessMessages.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is BusinessStartPage businessStartPage)
            {
                if (businessStartPage.Sticker != null)
                {
                    ProcessFiles(businessStartPage.Sticker);
                }
            }
            else if (target is Chat chat)
            {
                if (chat.Background != null)
                {
                    ProcessFiles(chat.Background);
                }
                if (chat.LastMessage != null)
                {
                    ProcessFiles(chat.LastMessage);
                }
                if (chat.Photo != null)
                {
                    ProcessFiles(chat.Photo);
                }
            }
            else if (target is ChatBackground chatBackground)
            {
                if (chatBackground.Background != null)
                {
                    ProcessFiles(chatBackground.Background);
                }
            }
            else if (target is ChatEvent chatEvent)
            {
                if (chatEvent.Action != null)
                {
                    ProcessFiles(chatEvent.Action);
                }
            }
            else if (target is ChatEventBackgroundChanged chatEventBackgroundChanged)
            {
                if (chatEventBackgroundChanged.NewBackground != null)
                {
                    ProcessFiles(chatEventBackgroundChanged.NewBackground);
                }
                if (chatEventBackgroundChanged.OldBackground != null)
                {
                    ProcessFiles(chatEventBackgroundChanged.OldBackground);
                }
            }
            else if (target is ChatEventMessageDeleted chatEventMessageDeleted)
            {
                if (chatEventMessageDeleted.Message != null)
                {
                    ProcessFiles(chatEventMessageDeleted.Message);
                }
            }
            else if (target is ChatEventMessageEdited chatEventMessageEdited)
            {
                if (chatEventMessageEdited.NewMessage != null)
                {
                    ProcessFiles(chatEventMessageEdited.NewMessage);
                }
                if (chatEventMessageEdited.OldMessage != null)
                {
                    ProcessFiles(chatEventMessageEdited.OldMessage);
                }
            }
            else if (target is ChatEventMessagePinned chatEventMessagePinned)
            {
                if (chatEventMessagePinned.Message != null)
                {
                    ProcessFiles(chatEventMessagePinned.Message);
                }
            }
            else if (target is ChatEventMessageUnpinned chatEventMessageUnpinned)
            {
                if (chatEventMessageUnpinned.Message != null)
                {
                    ProcessFiles(chatEventMessageUnpinned.Message);
                }
            }
            else if (target is ChatEventPhotoChanged chatEventPhotoChanged)
            {
                if (chatEventPhotoChanged.NewPhoto != null)
                {
                    ProcessFiles(chatEventPhotoChanged.NewPhoto);
                }
                if (chatEventPhotoChanged.OldPhoto != null)
                {
                    ProcessFiles(chatEventPhotoChanged.OldPhoto);
                }
            }
            else if (target is ChatEventPollStopped chatEventPollStopped)
            {
                if (chatEventPollStopped.Message != null)
                {
                    ProcessFiles(chatEventPollStopped.Message);
                }
            }
            else if (target is ChatEvents chatEvents)
            {
                foreach (var item in chatEvents.Events)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is ChatInviteLinkInfo chatInviteLinkInfo)
            {
                if (chatInviteLinkInfo.Photo != null)
                {
                    ProcessFiles(chatInviteLinkInfo.Photo);
                }
            }
            else if (target is ChatPhoto chatPhoto)
            {
                if (chatPhoto.Animation != null)
                {
                    ProcessFiles(chatPhoto.Animation);
                }
                foreach (var item in chatPhoto.Sizes)
                {
                    ProcessFiles(item);
                }
                if (chatPhoto.SmallAnimation != null)
                {
                    ProcessFiles(chatPhoto.SmallAnimation);
                }
            }
            else if (target is ChatPhotoInfo chatPhotoInfo)
            {
                if (chatPhotoInfo.Big != null)
                {
                    chatPhotoInfo.Big = ProcessFile(chatPhotoInfo.Big);
                }
                if (chatPhotoInfo.Small != null)
                {
                    chatPhotoInfo.Small = ProcessFile(chatPhotoInfo.Small);
                }
            }
            else if (target is ChatPhotos chatPhotos)
            {
                foreach (var item in chatPhotos.Photos)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is ChatTheme chatTheme)
            {
                if (chatTheme.DarkSettings != null)
                {
                    ProcessFiles(chatTheme.DarkSettings);
                }
                if (chatTheme.LightSettings != null)
                {
                    ProcessFiles(chatTheme.LightSettings);
                }
            }
            else if (target is DatedFile datedFile)
            {
                if (datedFile.File != null)
                {
                    datedFile.File = ProcessFile(datedFile.File);
                }
            }
            else if (target is DiceStickersRegular diceStickersRegular)
            {
                if (diceStickersRegular.Sticker != null)
                {
                    ProcessFiles(diceStickersRegular.Sticker);
                }
            }
            else if (target is DiceStickersSlotMachine diceStickersSlotMachine)
            {
                if (diceStickersSlotMachine.Background != null)
                {
                    ProcessFiles(diceStickersSlotMachine.Background);
                }
                if (diceStickersSlotMachine.CenterReel != null)
                {
                    ProcessFiles(diceStickersSlotMachine.CenterReel);
                }
                if (diceStickersSlotMachine.LeftReel != null)
                {
                    ProcessFiles(diceStickersSlotMachine.LeftReel);
                }
                if (diceStickersSlotMachine.Lever != null)
                {
                    ProcessFiles(diceStickersSlotMachine.Lever);
                }
                if (diceStickersSlotMachine.RightReel != null)
                {
                    ProcessFiles(diceStickersSlotMachine.RightReel);
                }
            }
            else if (target is Document document)
            {
                if (document.DocumentValue != null)
                {
                    document.DocumentValue = ProcessFile(document.DocumentValue);
                }
                if (document.Thumbnail != null)
                {
                    ProcessFiles(document.Thumbnail);
                }
            }
            else if (target is EmojiCategories emojiCategories)
            {
                foreach (var item in emojiCategories.Categories)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is EmojiCategory emojiCategory)
            {
                if (emojiCategory.Icon != null)
                {
                    ProcessFiles(emojiCategory.Icon);
                }
            }
            else if (target is EmojiReaction emojiReaction)
            {
                if (emojiReaction.ActivateAnimation != null)
                {
                    ProcessFiles(emojiReaction.ActivateAnimation);
                }
                if (emojiReaction.AppearAnimation != null)
                {
                    ProcessFiles(emojiReaction.AppearAnimation);
                }
                if (emojiReaction.AroundAnimation != null)
                {
                    ProcessFiles(emojiReaction.AroundAnimation);
                }
                if (emojiReaction.CenterAnimation != null)
                {
                    ProcessFiles(emojiReaction.CenterAnimation);
                }
                if (emojiReaction.EffectAnimation != null)
                {
                    ProcessFiles(emojiReaction.EffectAnimation);
                }
                if (emojiReaction.SelectAnimation != null)
                {
                    ProcessFiles(emojiReaction.SelectAnimation);
                }
                if (emojiReaction.StaticIcon != null)
                {
                    ProcessFiles(emojiReaction.StaticIcon);
                }
            }
            else if (target is EncryptedPassportElement encryptedPassportElement)
            {
                foreach (var item in encryptedPassportElement.Files)
                {
                    ProcessFiles(item);
                }
                if (encryptedPassportElement.FrontSide != null)
                {
                    ProcessFiles(encryptedPassportElement.FrontSide);
                }
                if (encryptedPassportElement.ReverseSide != null)
                {
                    ProcessFiles(encryptedPassportElement.ReverseSide);
                }
                if (encryptedPassportElement.Selfie != null)
                {
                    ProcessFiles(encryptedPassportElement.Selfie);
                }
                foreach (var item in encryptedPassportElement.Translation)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is FileDownload fileDownload)
            {
                if (fileDownload.Message != null)
                {
                    ProcessFiles(fileDownload.Message);
                }
            }
            else if (target is ForumTopic forumTopic)
            {
                if (forumTopic.LastMessage != null)
                {
                    ProcessFiles(forumTopic.LastMessage);
                }
            }
            else if (target is ForumTopics forumTopics)
            {
                foreach (var item in forumTopics.Topics)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is FoundChatMessages foundChatMessages)
            {
                foreach (var item in foundChatMessages.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is FoundFileDownloads foundFileDownloads)
            {
                foreach (var item in foundFileDownloads.Files)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is FoundMessages foundMessages)
            {
                foreach (var item in foundMessages.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is FoundStories foundStories)
            {
                foreach (var item in foundStories.Stories)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is FoundWebApp foundWebApp)
            {
                if (foundWebApp.WebApp != null)
                {
                    ProcessFiles(foundWebApp.WebApp);
                }
            }
            else if (target is Game game)
            {
                if (game.Animation != null)
                {
                    ProcessFiles(game.Animation);
                }
                if (game.Photo != null)
                {
                    ProcessFiles(game.Photo);
                }
            }
            else if (target is IdentityDocument identityDocument)
            {
                if (identityDocument.FrontSide != null)
                {
                    ProcessFiles(identityDocument.FrontSide);
                }
                if (identityDocument.ReverseSide != null)
                {
                    ProcessFiles(identityDocument.ReverseSide);
                }
                if (identityDocument.Selfie != null)
                {
                    ProcessFiles(identityDocument.Selfie);
                }
                foreach (var item in identityDocument.Translation)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is InlineQueryResultAnimation inlineQueryResultAnimation)
            {
                if (inlineQueryResultAnimation.Animation != null)
                {
                    ProcessFiles(inlineQueryResultAnimation.Animation);
                }
            }
            else if (target is InlineQueryResultArticle inlineQueryResultArticle)
            {
                if (inlineQueryResultArticle.Thumbnail != null)
                {
                    ProcessFiles(inlineQueryResultArticle.Thumbnail);
                }
            }
            else if (target is InlineQueryResultAudio inlineQueryResultAudio)
            {
                if (inlineQueryResultAudio.Audio != null)
                {
                    ProcessFiles(inlineQueryResultAudio.Audio);
                }
            }
            else if (target is InlineQueryResultContact inlineQueryResultContact)
            {
                if (inlineQueryResultContact.Thumbnail != null)
                {
                    ProcessFiles(inlineQueryResultContact.Thumbnail);
                }
            }
            else if (target is InlineQueryResultDocument inlineQueryResultDocument)
            {
                if (inlineQueryResultDocument.Document != null)
                {
                    ProcessFiles(inlineQueryResultDocument.Document);
                }
            }
            else if (target is InlineQueryResultGame inlineQueryResultGame)
            {
                if (inlineQueryResultGame.Game != null)
                {
                    ProcessFiles(inlineQueryResultGame.Game);
                }
            }
            else if (target is InlineQueryResultLocation inlineQueryResultLocation)
            {
                if (inlineQueryResultLocation.Thumbnail != null)
                {
                    ProcessFiles(inlineQueryResultLocation.Thumbnail);
                }
            }
            else if (target is InlineQueryResultPhoto inlineQueryResultPhoto)
            {
                if (inlineQueryResultPhoto.Photo != null)
                {
                    ProcessFiles(inlineQueryResultPhoto.Photo);
                }
            }
            else if (target is InlineQueryResults inlineQueryResults)
            {
                foreach (var item in inlineQueryResults.Results)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is InlineQueryResultSticker inlineQueryResultSticker)
            {
                if (inlineQueryResultSticker.Sticker != null)
                {
                    ProcessFiles(inlineQueryResultSticker.Sticker);
                }
            }
            else if (target is InlineQueryResultVenue inlineQueryResultVenue)
            {
                if (inlineQueryResultVenue.Thumbnail != null)
                {
                    ProcessFiles(inlineQueryResultVenue.Thumbnail);
                }
            }
            else if (target is InlineQueryResultVideo inlineQueryResultVideo)
            {
                if (inlineQueryResultVideo.Video != null)
                {
                    ProcessFiles(inlineQueryResultVideo.Video);
                }
            }
            else if (target is InlineQueryResultVoiceNote inlineQueryResultVoiceNote)
            {
                if (inlineQueryResultVoiceNote.VoiceNote != null)
                {
                    ProcessFiles(inlineQueryResultVoiceNote.VoiceNote);
                }
            }
            else if (target is LinkPreview linkPreview)
            {
                if (linkPreview.Type != null)
                {
                    ProcessFiles(linkPreview.Type);
                }
            }
            else if (target is LinkPreviewAlbumMediaPhoto linkPreviewAlbumMediaPhoto)
            {
                if (linkPreviewAlbumMediaPhoto.Photo != null)
                {
                    ProcessFiles(linkPreviewAlbumMediaPhoto.Photo);
                }
            }
            else if (target is LinkPreviewAlbumMediaVideo linkPreviewAlbumMediaVideo)
            {
                if (linkPreviewAlbumMediaVideo.Video != null)
                {
                    ProcessFiles(linkPreviewAlbumMediaVideo.Video);
                }
            }
            else if (target is LinkPreviewTypeAlbum linkPreviewTypeAlbum)
            {
                foreach (var item in linkPreviewTypeAlbum.Media)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is LinkPreviewTypeAnimation linkPreviewTypeAnimation)
            {
                if (linkPreviewTypeAnimation.Animation != null)
                {
                    ProcessFiles(linkPreviewTypeAnimation.Animation);
                }
            }
            else if (target is LinkPreviewTypeApp linkPreviewTypeApp)
            {
                if (linkPreviewTypeApp.Photo != null)
                {
                    ProcessFiles(linkPreviewTypeApp.Photo);
                }
            }
            else if (target is LinkPreviewTypeArticle linkPreviewTypeArticle)
            {
                if (linkPreviewTypeArticle.Photo != null)
                {
                    ProcessFiles(linkPreviewTypeArticle.Photo);
                }
            }
            else if (target is LinkPreviewTypeAudio linkPreviewTypeAudio)
            {
                if (linkPreviewTypeAudio.Audio != null)
                {
                    ProcessFiles(linkPreviewTypeAudio.Audio);
                }
            }
            else if (target is LinkPreviewTypeBackground linkPreviewTypeBackground)
            {
                if (linkPreviewTypeBackground.Document != null)
                {
                    ProcessFiles(linkPreviewTypeBackground.Document);
                }
            }
            else if (target is LinkPreviewTypeChannelBoost linkPreviewTypeChannelBoost)
            {
                if (linkPreviewTypeChannelBoost.Photo != null)
                {
                    ProcessFiles(linkPreviewTypeChannelBoost.Photo);
                }
            }
            else if (target is LinkPreviewTypeChat linkPreviewTypeChat)
            {
                if (linkPreviewTypeChat.Photo != null)
                {
                    ProcessFiles(linkPreviewTypeChat.Photo);
                }
            }
            else if (target is LinkPreviewTypeDocument linkPreviewTypeDocument)
            {
                if (linkPreviewTypeDocument.Document != null)
                {
                    ProcessFiles(linkPreviewTypeDocument.Document);
                }
            }
            else if (target is LinkPreviewTypeEmbeddedAnimationPlayer linkPreviewTypeEmbeddedAnimationPlayer)
            {
                if (linkPreviewTypeEmbeddedAnimationPlayer.Thumbnail != null)
                {
                    ProcessFiles(linkPreviewTypeEmbeddedAnimationPlayer.Thumbnail);
                }
            }
            else if (target is LinkPreviewTypeEmbeddedAudioPlayer linkPreviewTypeEmbeddedAudioPlayer)
            {
                if (linkPreviewTypeEmbeddedAudioPlayer.Thumbnail != null)
                {
                    ProcessFiles(linkPreviewTypeEmbeddedAudioPlayer.Thumbnail);
                }
            }
            else if (target is LinkPreviewTypeEmbeddedVideoPlayer linkPreviewTypeEmbeddedVideoPlayer)
            {
                if (linkPreviewTypeEmbeddedVideoPlayer.Thumbnail != null)
                {
                    ProcessFiles(linkPreviewTypeEmbeddedVideoPlayer.Thumbnail);
                }
            }
            else if (target is LinkPreviewTypePhoto linkPreviewTypePhoto)
            {
                if (linkPreviewTypePhoto.Photo != null)
                {
                    ProcessFiles(linkPreviewTypePhoto.Photo);
                }
            }
            else if (target is LinkPreviewTypeSticker linkPreviewTypeSticker)
            {
                if (linkPreviewTypeSticker.Sticker != null)
                {
                    ProcessFiles(linkPreviewTypeSticker.Sticker);
                }
            }
            else if (target is LinkPreviewTypeStickerSet linkPreviewTypeStickerSet)
            {
                foreach (var item in linkPreviewTypeStickerSet.Stickers)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is LinkPreviewTypeSupergroupBoost linkPreviewTypeSupergroupBoost)
            {
                if (linkPreviewTypeSupergroupBoost.Photo != null)
                {
                    ProcessFiles(linkPreviewTypeSupergroupBoost.Photo);
                }
            }
            else if (target is LinkPreviewTypeTheme linkPreviewTypeTheme)
            {
                foreach (var item in linkPreviewTypeTheme.Documents)
                {
                    ProcessFiles(item);
                }
                if (linkPreviewTypeTheme.Settings != null)
                {
                    ProcessFiles(linkPreviewTypeTheme.Settings);
                }
            }
            else if (target is LinkPreviewTypeUser linkPreviewTypeUser)
            {
                if (linkPreviewTypeUser.Photo != null)
                {
                    ProcessFiles(linkPreviewTypeUser.Photo);
                }
            }
            else if (target is LinkPreviewTypeVideo linkPreviewTypeVideo)
            {
                if (linkPreviewTypeVideo.Video != null)
                {
                    ProcessFiles(linkPreviewTypeVideo.Video);
                }
            }
            else if (target is LinkPreviewTypeVideoChat linkPreviewTypeVideoChat)
            {
                if (linkPreviewTypeVideoChat.Photo != null)
                {
                    ProcessFiles(linkPreviewTypeVideoChat.Photo);
                }
            }
            else if (target is LinkPreviewTypeVideoNote linkPreviewTypeVideoNote)
            {
                if (linkPreviewTypeVideoNote.VideoNote != null)
                {
                    ProcessFiles(linkPreviewTypeVideoNote.VideoNote);
                }
            }
            else if (target is LinkPreviewTypeVoiceNote linkPreviewTypeVoiceNote)
            {
                if (linkPreviewTypeVoiceNote.VoiceNote != null)
                {
                    ProcessFiles(linkPreviewTypeVoiceNote.VoiceNote);
                }
            }
            else if (target is LinkPreviewTypeWebApp linkPreviewTypeWebApp)
            {
                if (linkPreviewTypeWebApp.Photo != null)
                {
                    ProcessFiles(linkPreviewTypeWebApp.Photo);
                }
            }
            else if (target is Message message)
            {
                if (message.Content != null)
                {
                    ProcessFiles(message.Content);
                }
                if (message.ReplyTo != null)
                {
                    ProcessFiles(message.ReplyTo);
                }
            }
            else if (target is MessageAnimatedEmoji messageAnimatedEmoji)
            {
                if (messageAnimatedEmoji.AnimatedEmoji != null)
                {
                    ProcessFiles(messageAnimatedEmoji.AnimatedEmoji);
                }
            }
            else if (target is MessageAnimation messageAnimation)
            {
                if (messageAnimation.Animation != null)
                {
                    ProcessFiles(messageAnimation.Animation);
                }
            }
            else if (target is MessageAudio messageAudio)
            {
                if (messageAudio.Audio != null)
                {
                    ProcessFiles(messageAudio.Audio);
                }
            }
            else if (target is MessageBotWriteAccessAllowed messageBotWriteAccessAllowed)
            {
                if (messageBotWriteAccessAllowed.Reason != null)
                {
                    ProcessFiles(messageBotWriteAccessAllowed.Reason);
                }
            }
            else if (target is MessageCalendar messageCalendar)
            {
                foreach (var item in messageCalendar.Days)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessageCalendarDay messageCalendarDay)
            {
                if (messageCalendarDay.Message != null)
                {
                    ProcessFiles(messageCalendarDay.Message);
                }
            }
            else if (target is MessageChatChangePhoto messageChatChangePhoto)
            {
                if (messageChatChangePhoto.Photo != null)
                {
                    ProcessFiles(messageChatChangePhoto.Photo);
                }
            }
            else if (target is MessageChatSetBackground messageChatSetBackground)
            {
                if (messageChatSetBackground.Background != null)
                {
                    ProcessFiles(messageChatSetBackground.Background);
                }
            }
            else if (target is MessageChatShared messageChatShared)
            {
                if (messageChatShared.Chat != null)
                {
                    ProcessFiles(messageChatShared.Chat);
                }
            }
            else if (target is MessageDice messageDice)
            {
                if (messageDice.FinalState != null)
                {
                    ProcessFiles(messageDice.FinalState);
                }
                if (messageDice.InitialState != null)
                {
                    ProcessFiles(messageDice.InitialState);
                }
            }
            else if (target is MessageDocument messageDocument)
            {
                if (messageDocument.Document != null)
                {
                    ProcessFiles(messageDocument.Document);
                }
            }
            else if (target is MessageEffect messageEffect)
            {
                if (messageEffect.StaticIcon != null)
                {
                    ProcessFiles(messageEffect.StaticIcon);
                }
                if (messageEffect.Type != null)
                {
                    ProcessFiles(messageEffect.Type);
                }
            }
            else if (target is MessageEffectTypeEmojiReaction messageEffectTypeEmojiReaction)
            {
                if (messageEffectTypeEmojiReaction.EffectAnimation != null)
                {
                    ProcessFiles(messageEffectTypeEmojiReaction.EffectAnimation);
                }
                if (messageEffectTypeEmojiReaction.SelectAnimation != null)
                {
                    ProcessFiles(messageEffectTypeEmojiReaction.SelectAnimation);
                }
            }
            else if (target is MessageEffectTypePremiumSticker messageEffectTypePremiumSticker)
            {
                if (messageEffectTypePremiumSticker.Sticker != null)
                {
                    ProcessFiles(messageEffectTypePremiumSticker.Sticker);
                }
            }
            else if (target is MessageGame messageGame)
            {
                if (messageGame.Game != null)
                {
                    ProcessFiles(messageGame.Game);
                }
            }
            else if (target is MessageGiftedPremium messageGiftedPremium)
            {
                if (messageGiftedPremium.Sticker != null)
                {
                    ProcessFiles(messageGiftedPremium.Sticker);
                }
            }
            else if (target is MessageGiftedStars messageGiftedStars)
            {
                if (messageGiftedStars.Sticker != null)
                {
                    ProcessFiles(messageGiftedStars.Sticker);
                }
            }
            else if (target is MessageInvoice messageInvoice)
            {
                if (messageInvoice.PaidMedia != null)
                {
                    ProcessFiles(messageInvoice.PaidMedia);
                }
                if (messageInvoice.ProductInfo != null)
                {
                    ProcessFiles(messageInvoice.ProductInfo);
                }
            }
            else if (target is MessageLinkInfo messageLinkInfo)
            {
                if (messageLinkInfo.Message != null)
                {
                    ProcessFiles(messageLinkInfo.Message);
                }
            }
            else if (target is MessagePaidMedia messagePaidMedia)
            {
                foreach (var item in messagePaidMedia.Media)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessagePassportDataReceived messagePassportDataReceived)
            {
                foreach (var item in messagePassportDataReceived.Elements)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessagePhoto messagePhoto)
            {
                if (messagePhoto.Photo != null)
                {
                    ProcessFiles(messagePhoto.Photo);
                }
            }
            else if (target is MessagePremiumGiftCode messagePremiumGiftCode)
            {
                if (messagePremiumGiftCode.Sticker != null)
                {
                    ProcessFiles(messagePremiumGiftCode.Sticker);
                }
            }
            else if (target is MessagePremiumGiveaway messagePremiumGiveaway)
            {
                if (messagePremiumGiveaway.Sticker != null)
                {
                    ProcessFiles(messagePremiumGiveaway.Sticker);
                }
            }
            else if (target is MessageReplyToMessage messageReplyToMessage)
            {
                if (messageReplyToMessage.Content != null)
                {
                    ProcessFiles(messageReplyToMessage.Content);
                }
            }
            else if (target is Messages messages)
            {
                foreach (var item in messages.MessagesValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessageSponsor messageSponsor)
            {
                if (messageSponsor.Photo != null)
                {
                    ProcessFiles(messageSponsor.Photo);
                }
            }
            else if (target is MessageSticker messageSticker)
            {
                if (messageSticker.Sticker != null)
                {
                    ProcessFiles(messageSticker.Sticker);
                }
            }
            else if (target is MessageSuggestProfilePhoto messageSuggestProfilePhoto)
            {
                if (messageSuggestProfilePhoto.Photo != null)
                {
                    ProcessFiles(messageSuggestProfilePhoto.Photo);
                }
            }
            else if (target is MessageText messageText)
            {
                if (messageText.LinkPreview != null)
                {
                    ProcessFiles(messageText.LinkPreview);
                }
            }
            else if (target is MessageThreadInfo messageThreadInfo)
            {
                foreach (var item in messageThreadInfo.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessageUsersShared messageUsersShared)
            {
                foreach (var item in messageUsersShared.Users)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessageVideo messageVideo)
            {
                if (messageVideo.Video != null)
                {
                    ProcessFiles(messageVideo.Video);
                }
            }
            else if (target is MessageVideoNote messageVideoNote)
            {
                if (messageVideoNote.VideoNote != null)
                {
                    ProcessFiles(messageVideoNote.VideoNote);
                }
            }
            else if (target is MessageVoiceNote messageVoiceNote)
            {
                if (messageVoiceNote.VoiceNote != null)
                {
                    ProcessFiles(messageVoiceNote.VoiceNote);
                }
            }
            else if (target is Notification notification)
            {
                if (notification.Type != null)
                {
                    ProcessFiles(notification.Type);
                }
            }
            else if (target is NotificationGroup notificationGroup)
            {
                foreach (var item in notificationGroup.Notifications)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is NotificationSound notificationSound)
            {
                if (notificationSound.Sound != null)
                {
                    notificationSound.Sound = ProcessFile(notificationSound.Sound);
                }
            }
            else if (target is NotificationSounds notificationSounds)
            {
                foreach (var item in notificationSounds.NotificationSoundsValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is NotificationTypeNewMessage notificationTypeNewMessage)
            {
                if (notificationTypeNewMessage.Message != null)
                {
                    ProcessFiles(notificationTypeNewMessage.Message);
                }
            }
            else if (target is NotificationTypeNewPushMessage notificationTypeNewPushMessage)
            {
                if (notificationTypeNewPushMessage.Content != null)
                {
                    ProcessFiles(notificationTypeNewPushMessage.Content);
                }
            }
            else if (target is PageBlockAnimation pageBlockAnimation)
            {
                if (pageBlockAnimation.Animation != null)
                {
                    ProcessFiles(pageBlockAnimation.Animation);
                }
                if (pageBlockAnimation.Caption != null)
                {
                    ProcessFiles(pageBlockAnimation.Caption);
                }
            }
            else if (target is PageBlockAudio pageBlockAudio)
            {
                if (pageBlockAudio.Audio != null)
                {
                    ProcessFiles(pageBlockAudio.Audio);
                }
                if (pageBlockAudio.Caption != null)
                {
                    ProcessFiles(pageBlockAudio.Caption);
                }
            }
            else if (target is PageBlockAuthorDate pageBlockAuthorDate)
            {
                if (pageBlockAuthorDate.Author != null)
                {
                    ProcessFiles(pageBlockAuthorDate.Author);
                }
            }
            else if (target is PageBlockBlockQuote pageBlockBlockQuote)
            {
                if (pageBlockBlockQuote.Credit != null)
                {
                    ProcessFiles(pageBlockBlockQuote.Credit);
                }
                if (pageBlockBlockQuote.Text != null)
                {
                    ProcessFiles(pageBlockBlockQuote.Text);
                }
            }
            else if (target is PageBlockCaption pageBlockCaption)
            {
                if (pageBlockCaption.Credit != null)
                {
                    ProcessFiles(pageBlockCaption.Credit);
                }
                if (pageBlockCaption.Text != null)
                {
                    ProcessFiles(pageBlockCaption.Text);
                }
            }
            else if (target is PageBlockChatLink pageBlockChatLink)
            {
                if (pageBlockChatLink.Photo != null)
                {
                    ProcessFiles(pageBlockChatLink.Photo);
                }
            }
            else if (target is PageBlockCollage pageBlockCollage)
            {
                if (pageBlockCollage.Caption != null)
                {
                    ProcessFiles(pageBlockCollage.Caption);
                }
                foreach (var item in pageBlockCollage.PageBlocks)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockCover pageBlockCover)
            {
                if (pageBlockCover.Cover != null)
                {
                    ProcessFiles(pageBlockCover.Cover);
                }
            }
            else if (target is PageBlockDetails pageBlockDetails)
            {
                if (pageBlockDetails.Header != null)
                {
                    ProcessFiles(pageBlockDetails.Header);
                }
                foreach (var item in pageBlockDetails.PageBlocks)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockEmbedded pageBlockEmbedded)
            {
                if (pageBlockEmbedded.Caption != null)
                {
                    ProcessFiles(pageBlockEmbedded.Caption);
                }
                if (pageBlockEmbedded.PosterPhoto != null)
                {
                    ProcessFiles(pageBlockEmbedded.PosterPhoto);
                }
            }
            else if (target is PageBlockEmbeddedPost pageBlockEmbeddedPost)
            {
                if (pageBlockEmbeddedPost.AuthorPhoto != null)
                {
                    ProcessFiles(pageBlockEmbeddedPost.AuthorPhoto);
                }
                if (pageBlockEmbeddedPost.Caption != null)
                {
                    ProcessFiles(pageBlockEmbeddedPost.Caption);
                }
                foreach (var item in pageBlockEmbeddedPost.PageBlocks)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockFooter pageBlockFooter)
            {
                if (pageBlockFooter.Footer != null)
                {
                    ProcessFiles(pageBlockFooter.Footer);
                }
            }
            else if (target is PageBlockHeader pageBlockHeader)
            {
                if (pageBlockHeader.Header != null)
                {
                    ProcessFiles(pageBlockHeader.Header);
                }
            }
            else if (target is PageBlockKicker pageBlockKicker)
            {
                if (pageBlockKicker.Kicker != null)
                {
                    ProcessFiles(pageBlockKicker.Kicker);
                }
            }
            else if (target is PageBlockList pageBlockList)
            {
                foreach (var item in pageBlockList.Items)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockListItem pageBlockListItem)
            {
                foreach (var item in pageBlockListItem.PageBlocks)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockMap pageBlockMap)
            {
                if (pageBlockMap.Caption != null)
                {
                    ProcessFiles(pageBlockMap.Caption);
                }
            }
            else if (target is PageBlockParagraph pageBlockParagraph)
            {
                if (pageBlockParagraph.Text != null)
                {
                    ProcessFiles(pageBlockParagraph.Text);
                }
            }
            else if (target is PageBlockPhoto pageBlockPhoto)
            {
                if (pageBlockPhoto.Caption != null)
                {
                    ProcessFiles(pageBlockPhoto.Caption);
                }
                if (pageBlockPhoto.Photo != null)
                {
                    ProcessFiles(pageBlockPhoto.Photo);
                }
            }
            else if (target is PageBlockPreformatted pageBlockPreformatted)
            {
                if (pageBlockPreformatted.Text != null)
                {
                    ProcessFiles(pageBlockPreformatted.Text);
                }
            }
            else if (target is PageBlockPullQuote pageBlockPullQuote)
            {
                if (pageBlockPullQuote.Credit != null)
                {
                    ProcessFiles(pageBlockPullQuote.Credit);
                }
                if (pageBlockPullQuote.Text != null)
                {
                    ProcessFiles(pageBlockPullQuote.Text);
                }
            }
            else if (target is PageBlockRelatedArticle pageBlockRelatedArticle)
            {
                if (pageBlockRelatedArticle.Photo != null)
                {
                    ProcessFiles(pageBlockRelatedArticle.Photo);
                }
            }
            else if (target is PageBlockRelatedArticles pageBlockRelatedArticles)
            {
                foreach (var item in pageBlockRelatedArticles.Articles)
                {
                    ProcessFiles(item);
                }
                if (pageBlockRelatedArticles.Header != null)
                {
                    ProcessFiles(pageBlockRelatedArticles.Header);
                }
            }
            else if (target is PageBlockSlideshow pageBlockSlideshow)
            {
                if (pageBlockSlideshow.Caption != null)
                {
                    ProcessFiles(pageBlockSlideshow.Caption);
                }
                foreach (var item in pageBlockSlideshow.PageBlocks)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockSubheader pageBlockSubheader)
            {
                if (pageBlockSubheader.Subheader != null)
                {
                    ProcessFiles(pageBlockSubheader.Subheader);
                }
            }
            else if (target is PageBlockSubtitle pageBlockSubtitle)
            {
                if (pageBlockSubtitle.Subtitle != null)
                {
                    ProcessFiles(pageBlockSubtitle.Subtitle);
                }
            }
            else if (target is PageBlockTable pageBlockTable)
            {
                if (pageBlockTable.Caption != null)
                {
                    ProcessFiles(pageBlockTable.Caption);
                }
            }
            else if (target is PageBlockTableCell pageBlockTableCell)
            {
                if (pageBlockTableCell.Text != null)
                {
                    ProcessFiles(pageBlockTableCell.Text);
                }
            }
            else if (target is PageBlockTitle pageBlockTitle)
            {
                if (pageBlockTitle.Title != null)
                {
                    ProcessFiles(pageBlockTitle.Title);
                }
            }
            else if (target is PageBlockVideo pageBlockVideo)
            {
                if (pageBlockVideo.Caption != null)
                {
                    ProcessFiles(pageBlockVideo.Caption);
                }
                if (pageBlockVideo.Video != null)
                {
                    ProcessFiles(pageBlockVideo.Video);
                }
            }
            else if (target is PageBlockVoiceNote pageBlockVoiceNote)
            {
                if (pageBlockVoiceNote.Caption != null)
                {
                    ProcessFiles(pageBlockVoiceNote.Caption);
                }
                if (pageBlockVoiceNote.VoiceNote != null)
                {
                    ProcessFiles(pageBlockVoiceNote.VoiceNote);
                }
            }
            else if (target is PaidMediaPhoto paidMediaPhoto)
            {
                if (paidMediaPhoto.Photo != null)
                {
                    ProcessFiles(paidMediaPhoto.Photo);
                }
            }
            else if (target is PaidMediaVideo paidMediaVideo)
            {
                if (paidMediaVideo.Video != null)
                {
                    ProcessFiles(paidMediaVideo.Video);
                }
            }
            else if (target is PassportElementBankStatement passportElementBankStatement)
            {
                if (passportElementBankStatement.BankStatement != null)
                {
                    ProcessFiles(passportElementBankStatement.BankStatement);
                }
            }
            else if (target is PassportElementDriverLicense passportElementDriverLicense)
            {
                if (passportElementDriverLicense.DriverLicense != null)
                {
                    ProcessFiles(passportElementDriverLicense.DriverLicense);
                }
            }
            else if (target is PassportElementIdentityCard passportElementIdentityCard)
            {
                if (passportElementIdentityCard.IdentityCard != null)
                {
                    ProcessFiles(passportElementIdentityCard.IdentityCard);
                }
            }
            else if (target is PassportElementInternalPassport passportElementInternalPassport)
            {
                if (passportElementInternalPassport.InternalPassport != null)
                {
                    ProcessFiles(passportElementInternalPassport.InternalPassport);
                }
            }
            else if (target is PassportElementPassport passportElementPassport)
            {
                if (passportElementPassport.Passport != null)
                {
                    ProcessFiles(passportElementPassport.Passport);
                }
            }
            else if (target is PassportElementPassportRegistration passportElementPassportRegistration)
            {
                if (passportElementPassportRegistration.PassportRegistration != null)
                {
                    ProcessFiles(passportElementPassportRegistration.PassportRegistration);
                }
            }
            else if (target is PassportElementRentalAgreement passportElementRentalAgreement)
            {
                if (passportElementRentalAgreement.RentalAgreement != null)
                {
                    ProcessFiles(passportElementRentalAgreement.RentalAgreement);
                }
            }
            else if (target is PassportElements passportElements)
            {
                foreach (var item in passportElements.Elements)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PassportElementsWithErrors passportElementsWithErrors)
            {
                foreach (var item in passportElementsWithErrors.Elements)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PassportElementTemporaryRegistration passportElementTemporaryRegistration)
            {
                if (passportElementTemporaryRegistration.TemporaryRegistration != null)
                {
                    ProcessFiles(passportElementTemporaryRegistration.TemporaryRegistration);
                }
            }
            else if (target is PassportElementUtilityBill passportElementUtilityBill)
            {
                if (passportElementUtilityBill.UtilityBill != null)
                {
                    ProcessFiles(passportElementUtilityBill.UtilityBill);
                }
            }
            else if (target is PaymentForm paymentForm)
            {
                if (paymentForm.ProductInfo != null)
                {
                    ProcessFiles(paymentForm.ProductInfo);
                }
            }
            else if (target is PaymentReceipt paymentReceipt)
            {
                if (paymentReceipt.ProductInfo != null)
                {
                    ProcessFiles(paymentReceipt.ProductInfo);
                }
            }
            else if (target is PersonalDocument personalDocument)
            {
                foreach (var item in personalDocument.Files)
                {
                    ProcessFiles(item);
                }
                foreach (var item in personalDocument.Translation)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is Photo photo)
            {
                foreach (var item in photo.Sizes)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PhotoSize photoSize)
            {
                if (photoSize.Photo != null)
                {
                    photoSize.Photo = ProcessFile(photoSize.Photo);
                }
            }
            else if (target is PremiumFeaturePromotionAnimation premiumFeaturePromotionAnimation)
            {
                if (premiumFeaturePromotionAnimation.Animation != null)
                {
                    ProcessFiles(premiumFeaturePromotionAnimation.Animation);
                }
            }
            else if (target is PremiumState premiumState)
            {
                foreach (var item in premiumState.Animations)
                {
                    ProcessFiles(item);
                }
                foreach (var item in premiumState.BusinessAnimations)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is ProductInfo productInfo)
            {
                if (productInfo.Photo != null)
                {
                    ProcessFiles(productInfo.Photo);
                }
            }
            else if (target is ProfilePhoto profilePhoto)
            {
                if (profilePhoto.Big != null)
                {
                    profilePhoto.Big = ProcessFile(profilePhoto.Big);
                }
                if (profilePhoto.Small != null)
                {
                    profilePhoto.Small = ProcessFile(profilePhoto.Small);
                }
            }
            else if (target is PublicForwardMessage publicForwardMessage)
            {
                if (publicForwardMessage.Message != null)
                {
                    ProcessFiles(publicForwardMessage.Message);
                }
            }
            else if (target is PublicForwards publicForwards)
            {
                foreach (var item in publicForwards.Forwards)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PublicForwardStory publicForwardStory)
            {
                if (publicForwardStory.Story != null)
                {
                    ProcessFiles(publicForwardStory.Story);
                }
            }
            else if (target is PushMessageContentAnimation pushMessageContentAnimation)
            {
                if (pushMessageContentAnimation.Animation != null)
                {
                    ProcessFiles(pushMessageContentAnimation.Animation);
                }
            }
            else if (target is PushMessageContentAudio pushMessageContentAudio)
            {
                if (pushMessageContentAudio.Audio != null)
                {
                    ProcessFiles(pushMessageContentAudio.Audio);
                }
            }
            else if (target is PushMessageContentDocument pushMessageContentDocument)
            {
                if (pushMessageContentDocument.Document != null)
                {
                    ProcessFiles(pushMessageContentDocument.Document);
                }
            }
            else if (target is PushMessageContentPhoto pushMessageContentPhoto)
            {
                if (pushMessageContentPhoto.Photo != null)
                {
                    ProcessFiles(pushMessageContentPhoto.Photo);
                }
            }
            else if (target is PushMessageContentSticker pushMessageContentSticker)
            {
                if (pushMessageContentSticker.Sticker != null)
                {
                    ProcessFiles(pushMessageContentSticker.Sticker);
                }
            }
            else if (target is PushMessageContentVideo pushMessageContentVideo)
            {
                if (pushMessageContentVideo.Video != null)
                {
                    ProcessFiles(pushMessageContentVideo.Video);
                }
            }
            else if (target is PushMessageContentVideoNote pushMessageContentVideoNote)
            {
                if (pushMessageContentVideoNote.VideoNote != null)
                {
                    ProcessFiles(pushMessageContentVideoNote.VideoNote);
                }
            }
            else if (target is PushMessageContentVoiceNote pushMessageContentVoiceNote)
            {
                if (pushMessageContentVoiceNote.VoiceNote != null)
                {
                    ProcessFiles(pushMessageContentVoiceNote.VoiceNote);
                }
            }
            else if (target is QuickReplyMessage quickReplyMessage)
            {
                if (quickReplyMessage.Content != null)
                {
                    ProcessFiles(quickReplyMessage.Content);
                }
            }
            else if (target is QuickReplyMessages quickReplyMessages)
            {
                foreach (var item in quickReplyMessages.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is QuickReplyShortcut quickReplyShortcut)
            {
                if (quickReplyShortcut.FirstMessage != null)
                {
                    ProcessFiles(quickReplyShortcut.FirstMessage);
                }
            }
            else if (target is RichTextAnchorLink richTextAnchorLink)
            {
                if (richTextAnchorLink.Text != null)
                {
                    ProcessFiles(richTextAnchorLink.Text);
                }
            }
            else if (target is RichTextBold richTextBold)
            {
                if (richTextBold.Text != null)
                {
                    ProcessFiles(richTextBold.Text);
                }
            }
            else if (target is RichTextEmailAddress richTextEmailAddress)
            {
                if (richTextEmailAddress.Text != null)
                {
                    ProcessFiles(richTextEmailAddress.Text);
                }
            }
            else if (target is RichTextFixed richTextFixed)
            {
                if (richTextFixed.Text != null)
                {
                    ProcessFiles(richTextFixed.Text);
                }
            }
            else if (target is RichTextIcon richTextIcon)
            {
                if (richTextIcon.Document != null)
                {
                    ProcessFiles(richTextIcon.Document);
                }
            }
            else if (target is RichTextItalic richTextItalic)
            {
                if (richTextItalic.Text != null)
                {
                    ProcessFiles(richTextItalic.Text);
                }
            }
            else if (target is RichTextMarked richTextMarked)
            {
                if (richTextMarked.Text != null)
                {
                    ProcessFiles(richTextMarked.Text);
                }
            }
            else if (target is RichTextPhoneNumber richTextPhoneNumber)
            {
                if (richTextPhoneNumber.Text != null)
                {
                    ProcessFiles(richTextPhoneNumber.Text);
                }
            }
            else if (target is RichTextReference richTextReference)
            {
                if (richTextReference.Text != null)
                {
                    ProcessFiles(richTextReference.Text);
                }
            }
            else if (target is RichTexts richTexts)
            {
                foreach (var item in richTexts.Texts)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is RichTextStrikethrough richTextStrikethrough)
            {
                if (richTextStrikethrough.Text != null)
                {
                    ProcessFiles(richTextStrikethrough.Text);
                }
            }
            else if (target is RichTextSubscript richTextSubscript)
            {
                if (richTextSubscript.Text != null)
                {
                    ProcessFiles(richTextSubscript.Text);
                }
            }
            else if (target is RichTextSuperscript richTextSuperscript)
            {
                if (richTextSuperscript.Text != null)
                {
                    ProcessFiles(richTextSuperscript.Text);
                }
            }
            else if (target is RichTextUnderline richTextUnderline)
            {
                if (richTextUnderline.Text != null)
                {
                    ProcessFiles(richTextUnderline.Text);
                }
            }
            else if (target is RichTextUrl richTextUrl)
            {
                if (richTextUrl.Text != null)
                {
                    ProcessFiles(richTextUrl.Text);
                }
            }
            else if (target is SavedMessagesTopic savedMessagesTopic)
            {
                if (savedMessagesTopic.LastMessage != null)
                {
                    ProcessFiles(savedMessagesTopic.LastMessage);
                }
            }
            else if (target is SharedChat sharedChat)
            {
                if (sharedChat.Photo != null)
                {
                    ProcessFiles(sharedChat.Photo);
                }
            }
            else if (target is SharedUser sharedUser)
            {
                if (sharedUser.Photo != null)
                {
                    ProcessFiles(sharedUser.Photo);
                }
            }
            else if (target is SponsoredMessage sponsoredMessage)
            {
                if (sponsoredMessage.Content != null)
                {
                    ProcessFiles(sponsoredMessage.Content);
                }
                if (sponsoredMessage.Sponsor != null)
                {
                    ProcessFiles(sponsoredMessage.Sponsor);
                }
            }
            else if (target is SponsoredMessages sponsoredMessages)
            {
                foreach (var item in sponsoredMessages.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is StarTransaction starTransaction)
            {
                if (starTransaction.Partner != null)
                {
                    ProcessFiles(starTransaction.Partner);
                }
            }
            else if (target is StarTransactionPartnerBot starTransactionPartnerBot)
            {
                if (starTransactionPartnerBot.ProductInfo != null)
                {
                    ProcessFiles(starTransactionPartnerBot.ProductInfo);
                }
            }
            else if (target is StarTransactionPartnerChannel starTransactionPartnerChannel)
            {
                foreach (var item in starTransactionPartnerChannel.Media)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is StarTransactionPartnerUser starTransactionPartnerUser)
            {
                if (starTransactionPartnerUser.Sticker != null)
                {
                    ProcessFiles(starTransactionPartnerUser.Sticker);
                }
            }
            else if (target is StarTransactions starTransactions)
            {
                foreach (var item in starTransactions.Transactions)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is Sticker sticker)
            {
                if (sticker.FullType != null)
                {
                    ProcessFiles(sticker.FullType);
                }
                if (sticker.StickerValue != null)
                {
                    sticker.StickerValue = ProcessFile(sticker.StickerValue);
                }
                if (sticker.Thumbnail != null)
                {
                    ProcessFiles(sticker.Thumbnail);
                }
            }
            else if (target is StickerFullTypeRegular stickerFullTypeRegular)
            {
                if (stickerFullTypeRegular.PremiumAnimation != null)
                {
                    stickerFullTypeRegular.PremiumAnimation = ProcessFile(stickerFullTypeRegular.PremiumAnimation);
                }
            }
            else if (target is Stickers stickers)
            {
                foreach (var item in stickers.StickersValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is StickerSet stickerSet)
            {
                foreach (var item in stickerSet.Stickers)
                {
                    ProcessFiles(item);
                }
                if (stickerSet.Thumbnail != null)
                {
                    ProcessFiles(stickerSet.Thumbnail);
                }
            }
            else if (target is StickerSetInfo stickerSetInfo)
            {
                foreach (var item in stickerSetInfo.Covers)
                {
                    ProcessFiles(item);
                }
                if (stickerSetInfo.Thumbnail != null)
                {
                    ProcessFiles(stickerSetInfo.Thumbnail);
                }
            }
            else if (target is StickerSets stickerSets)
            {
                foreach (var item in stickerSets.Sets)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is Stories stories)
            {
                foreach (var item in stories.StoriesValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is Story story)
            {
                if (story.Content != null)
                {
                    ProcessFiles(story.Content);
                }
            }
            else if (target is StoryContentPhoto storyContentPhoto)
            {
                if (storyContentPhoto.Photo != null)
                {
                    ProcessFiles(storyContentPhoto.Photo);
                }
            }
            else if (target is StoryContentVideo storyContentVideo)
            {
                if (storyContentVideo.AlternativeVideo != null)
                {
                    ProcessFiles(storyContentVideo.AlternativeVideo);
                }
                if (storyContentVideo.Video != null)
                {
                    ProcessFiles(storyContentVideo.Video);
                }
            }
            else if (target is StoryInteraction storyInteraction)
            {
                if (storyInteraction.Type != null)
                {
                    ProcessFiles(storyInteraction.Type);
                }
            }
            else if (target is StoryInteractions storyInteractions)
            {
                foreach (var item in storyInteractions.Interactions)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is StoryInteractionTypeForward storyInteractionTypeForward)
            {
                if (storyInteractionTypeForward.Message != null)
                {
                    ProcessFiles(storyInteractionTypeForward.Message);
                }
            }
            else if (target is StoryInteractionTypeRepost storyInteractionTypeRepost)
            {
                if (storyInteractionTypeRepost.Story != null)
                {
                    ProcessFiles(storyInteractionTypeRepost.Story);
                }
            }
            else if (target is StoryVideo storyVideo)
            {
                if (storyVideo.Thumbnail != null)
                {
                    ProcessFiles(storyVideo.Thumbnail);
                }
                if (storyVideo.Video != null)
                {
                    storyVideo.Video = ProcessFile(storyVideo.Video);
                }
            }
            else if (target is SupergroupFullInfo supergroupFullInfo)
            {
                if (supergroupFullInfo.Photo != null)
                {
                    ProcessFiles(supergroupFullInfo.Photo);
                }
            }
            else if (target is ThemeSettings themeSettings)
            {
                if (themeSettings.Background != null)
                {
                    ProcessFiles(themeSettings.Background);
                }
            }
            else if (target is Thumbnail thumbnail)
            {
                if (thumbnail.File != null)
                {
                    thumbnail.File = ProcessFile(thumbnail.File);
                }
            }
            else if (target is TMeUrl tMeUrl)
            {
                if (tMeUrl.Type != null)
                {
                    ProcessFiles(tMeUrl.Type);
                }
            }
            else if (target is TMeUrls tMeUrls)
            {
                foreach (var item in tMeUrls.Urls)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is TMeUrlTypeChatInvite tMeUrlTypeChatInvite)
            {
                if (tMeUrlTypeChatInvite.Info != null)
                {
                    ProcessFiles(tMeUrlTypeChatInvite.Info);
                }
            }
            else if (target is TrendingStickerSets trendingStickerSets)
            {
                foreach (var item in trendingStickerSets.Sets)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateActiveNotifications updateActiveNotifications)
            {
                foreach (var item in updateActiveNotifications.Groups)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateAnimatedEmojiMessageClicked updateAnimatedEmojiMessageClicked)
            {
                if (updateAnimatedEmojiMessageClicked.Sticker != null)
                {
                    ProcessFiles(updateAnimatedEmojiMessageClicked.Sticker);
                }
            }
            else if (target is UpdateAttachmentMenuBots updateAttachmentMenuBots)
            {
                foreach (var item in updateAttachmentMenuBots.Bots)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateBasicGroupFullInfo updateBasicGroupFullInfo)
            {
                if (updateBasicGroupFullInfo.BasicGroupFullInfo != null)
                {
                    ProcessFiles(updateBasicGroupFullInfo.BasicGroupFullInfo);
                }
            }
            else if (target is UpdateBusinessMessageEdited updateBusinessMessageEdited)
            {
                if (updateBusinessMessageEdited.Message != null)
                {
                    ProcessFiles(updateBusinessMessageEdited.Message);
                }
            }
            else if (target is UpdateChatBackground updateChatBackground)
            {
                if (updateChatBackground.Background != null)
                {
                    ProcessFiles(updateChatBackground.Background);
                }
            }
            else if (target is UpdateChatLastMessage updateChatLastMessage)
            {
                if (updateChatLastMessage.LastMessage != null)
                {
                    ProcessFiles(updateChatLastMessage.LastMessage);
                }
            }
            else if (target is UpdateChatPhoto updateChatPhoto)
            {
                if (updateChatPhoto.Photo != null)
                {
                    ProcessFiles(updateChatPhoto.Photo);
                }
            }
            else if (target is UpdateChatThemes updateChatThemes)
            {
                foreach (var item in updateChatThemes.ChatThemes)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateDefaultBackground updateDefaultBackground)
            {
                if (updateDefaultBackground.Background != null)
                {
                    ProcessFiles(updateDefaultBackground.Background);
                }
            }
            else if (target is UpdateFile updateFile)
            {
                if (updateFile.File != null)
                {
                    updateFile.File = ProcessFile(updateFile.File);
                }
            }
            else if (target is UpdateFileAddedToDownloads updateFileAddedToDownloads)
            {
                if (updateFileAddedToDownloads.FileDownload != null)
                {
                    ProcessFiles(updateFileAddedToDownloads.FileDownload);
                }
            }
            else if (target is UpdateMessageContent updateMessageContent)
            {
                if (updateMessageContent.NewContent != null)
                {
                    ProcessFiles(updateMessageContent.NewContent);
                }
            }
            else if (target is UpdateMessageSendFailed updateMessageSendFailed)
            {
                if (updateMessageSendFailed.Message != null)
                {
                    ProcessFiles(updateMessageSendFailed.Message);
                }
            }
            else if (target is UpdateMessageSendSucceeded updateMessageSendSucceeded)
            {
                if (updateMessageSendSucceeded.Message != null)
                {
                    ProcessFiles(updateMessageSendSucceeded.Message);
                }
            }
            else if (target is UpdateNewBusinessCallbackQuery updateNewBusinessCallbackQuery)
            {
                if (updateNewBusinessCallbackQuery.Message != null)
                {
                    ProcessFiles(updateNewBusinessCallbackQuery.Message);
                }
            }
            else if (target is UpdateNewBusinessMessage updateNewBusinessMessage)
            {
                if (updateNewBusinessMessage.Message != null)
                {
                    ProcessFiles(updateNewBusinessMessage.Message);
                }
            }
            else if (target is UpdateNewChat updateNewChat)
            {
                if (updateNewChat.Chat != null)
                {
                    ProcessFiles(updateNewChat.Chat);
                }
            }
            else if (target is UpdateNewMessage updateNewMessage)
            {
                if (updateNewMessage.Message != null)
                {
                    ProcessFiles(updateNewMessage.Message);
                }
            }
            else if (target is UpdateNotification updateNotification)
            {
                if (updateNotification.Notification != null)
                {
                    ProcessFiles(updateNotification.Notification);
                }
            }
            else if (target is UpdateNotificationGroup updateNotificationGroup)
            {
                foreach (var item in updateNotificationGroup.AddedNotifications)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateQuickReplyShortcut updateQuickReplyShortcut)
            {
                if (updateQuickReplyShortcut.Shortcut != null)
                {
                    ProcessFiles(updateQuickReplyShortcut.Shortcut);
                }
            }
            else if (target is UpdateQuickReplyShortcutMessages updateQuickReplyShortcutMessages)
            {
                foreach (var item in updateQuickReplyShortcutMessages.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateSavedMessagesTopic updateSavedMessagesTopic)
            {
                if (updateSavedMessagesTopic.Topic != null)
                {
                    ProcessFiles(updateSavedMessagesTopic.Topic);
                }
            }
            else if (target is UpdateServiceNotification updateServiceNotification)
            {
                if (updateServiceNotification.Content != null)
                {
                    ProcessFiles(updateServiceNotification.Content);
                }
            }
            else if (target is UpdateStickerSet updateStickerSet)
            {
                if (updateStickerSet.StickerSet != null)
                {
                    ProcessFiles(updateStickerSet.StickerSet);
                }
            }
            else if (target is UpdateStory updateStory)
            {
                if (updateStory.Story != null)
                {
                    ProcessFiles(updateStory.Story);
                }
            }
            else if (target is UpdateStorySendFailed updateStorySendFailed)
            {
                if (updateStorySendFailed.Story != null)
                {
                    ProcessFiles(updateStorySendFailed.Story);
                }
            }
            else if (target is UpdateStorySendSucceeded updateStorySendSucceeded)
            {
                if (updateStorySendSucceeded.Story != null)
                {
                    ProcessFiles(updateStorySendSucceeded.Story);
                }
            }
            else if (target is UpdateSupergroupFullInfo updateSupergroupFullInfo)
            {
                if (updateSupergroupFullInfo.SupergroupFullInfo != null)
                {
                    ProcessFiles(updateSupergroupFullInfo.SupergroupFullInfo);
                }
            }
            else if (target is UpdateTrendingStickerSets updateTrendingStickerSets)
            {
                if (updateTrendingStickerSets.StickerSets != null)
                {
                    ProcessFiles(updateTrendingStickerSets.StickerSets);
                }
            }
            else if (target is UpdateUser updateUser)
            {
                if (updateUser.User != null)
                {
                    ProcessFiles(updateUser.User);
                }
            }
            else if (target is UpdateUserFullInfo updateUserFullInfo)
            {
                if (updateUserFullInfo.UserFullInfo != null)
                {
                    ProcessFiles(updateUserFullInfo.UserFullInfo);
                }
            }
            else if (target is User user)
            {
                if (user.ProfilePhoto != null)
                {
                    ProcessFiles(user.ProfilePhoto);
                }
            }
            else if (target is UserFullInfo userFullInfo)
            {
                if (userFullInfo.BotInfo != null)
                {
                    ProcessFiles(userFullInfo.BotInfo);
                }
                if (userFullInfo.BusinessInfo != null)
                {
                    ProcessFiles(userFullInfo.BusinessInfo);
                }
                if (userFullInfo.PersonalPhoto != null)
                {
                    ProcessFiles(userFullInfo.PersonalPhoto);
                }
                if (userFullInfo.Photo != null)
                {
                    ProcessFiles(userFullInfo.Photo);
                }
                if (userFullInfo.PublicPhoto != null)
                {
                    ProcessFiles(userFullInfo.PublicPhoto);
                }
            }
            else if (target is Video video)
            {
                if (video.Thumbnail != null)
                {
                    ProcessFiles(video.Thumbnail);
                }
                if (video.VideoValue != null)
                {
                    video.VideoValue = ProcessFile(video.VideoValue);
                }
            }
            else if (target is VideoNote videoNote)
            {
                if (videoNote.Thumbnail != null)
                {
                    ProcessFiles(videoNote.Thumbnail);
                }
                if (videoNote.Video != null)
                {
                    videoNote.Video = ProcessFile(videoNote.Video);
                }
            }
            else if (target is VoiceNote voiceNote)
            {
                if (voiceNote.Voice != null)
                {
                    voiceNote.Voice = ProcessFile(voiceNote.Voice);
                }
            }
            else if (target is WebApp webApp)
            {
                if (webApp.Animation != null)
                {
                    ProcessFiles(webApp.Animation);
                }
                if (webApp.Photo != null)
                {
                    ProcessFiles(webApp.Photo);
                }
            }
            else if (target is WebPageInstantView webPageInstantView)
            {
                foreach (var item in webPageInstantView.PageBlocks)
                {
                    ProcessFiles(item);
                }
            }
        }
    }
}
