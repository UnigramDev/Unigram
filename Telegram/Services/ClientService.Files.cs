//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public Task<File> GetFileAsync(int fileId)
        {
            var tsc = new TaskCompletionSource<File>();
            Send(new GetFile(fileId), result =>
            {
                if (result is File file)
                {
                    tsc.SetResult(ProcessFile(file));
                }
                else
                {
                    tsc.SetResult(null);
                }
            });

            return tsc.Task;
        }

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
            switch (target)
            {
                case AlternativeVideo alternativeVideo:
                    if (alternativeVideo.HlsFile != null)
                    {
                        alternativeVideo.HlsFile = ProcessFile(alternativeVideo.HlsFile);
                    }
                    if (alternativeVideo.Video != null)
                    {
                        alternativeVideo.Video = ProcessFile(alternativeVideo.Video);
                    }
                    break;
                case AnimatedChatPhoto animatedChatPhoto:
                    if (animatedChatPhoto.File != null)
                    {
                        animatedChatPhoto.File = ProcessFile(animatedChatPhoto.File);
                    }
                    break;
                case AnimatedEmoji animatedEmoji:
                    if (animatedEmoji.Sound != null)
                    {
                        animatedEmoji.Sound = ProcessFile(animatedEmoji.Sound);
                    }
                    if (animatedEmoji.Sticker != null)
                    {
                        ProcessFiles(animatedEmoji.Sticker);
                    }
                    break;
                case Animation animation:
                    if (animation.AnimationValue != null)
                    {
                        animation.AnimationValue = ProcessFile(animation.AnimationValue);
                    }
                    if (animation.Thumbnail != null)
                    {
                        ProcessFiles(animation.Thumbnail);
                    }
                    break;
                case Animations animations:
                    foreach (var item in animations.AnimationsValue)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case AttachmentMenuBot attachmentMenuBot:
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
                    break;
                case Audio audio:
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
                    break;
                case Background background:
                    if (background.Document != null)
                    {
                        ProcessFiles(background.Document);
                    }
                    break;
                case Backgrounds backgrounds:
                    foreach (var item in backgrounds.BackgroundsValue)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case BasicGroupFullInfo basicGroupFullInfo:
                    if (basicGroupFullInfo.Photo != null)
                    {
                        ProcessFiles(basicGroupFullInfo.Photo);
                    }
                    break;
                case BotInfo botInfo:
                    if (botInfo.Animation != null)
                    {
                        ProcessFiles(botInfo.Animation);
                    }
                    if (botInfo.Photo != null)
                    {
                        ProcessFiles(botInfo.Photo);
                    }
                    break;
                case BotMediaPreview botMediaPreview:
                    if (botMediaPreview.Content != null)
                    {
                        ProcessFiles(botMediaPreview.Content);
                    }
                    break;
                case BotMediaPreviewInfo botMediaPreviewInfo:
                    foreach (var item in botMediaPreviewInfo.Previews)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case BotMediaPreviews botMediaPreviews:
                    foreach (var item in botMediaPreviews.Previews)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case BotTransactionPurposeInvoicePayment botTransactionPurposeInvoicePayment:
                    if (botTransactionPurposeInvoicePayment.ProductInfo != null)
                    {
                        ProcessFiles(botTransactionPurposeInvoicePayment.ProductInfo);
                    }
                    break;
                case BotTransactionPurposePaidMedia botTransactionPurposePaidMedia:
                    foreach (var item in botTransactionPurposePaidMedia.Media)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case BotWriteAccessAllowReasonLaunchedWebApp botWriteAccessAllowReasonLaunchedWebApp:
                    if (botWriteAccessAllowReasonLaunchedWebApp.WebApp != null)
                    {
                        ProcessFiles(botWriteAccessAllowReasonLaunchedWebApp.WebApp);
                    }
                    break;
                case BusinessFeaturePromotionAnimation businessFeaturePromotionAnimation:
                    if (businessFeaturePromotionAnimation.Animation != null)
                    {
                        ProcessFiles(businessFeaturePromotionAnimation.Animation);
                    }
                    break;
                case BusinessInfo businessInfo:
                    if (businessInfo.StartPage != null)
                    {
                        ProcessFiles(businessInfo.StartPage);
                    }
                    break;
                case BusinessMessage businessMessage:
                    if (businessMessage.Message != null)
                    {
                        ProcessFiles(businessMessage.Message);
                    }
                    if (businessMessage.ReplyToMessage != null)
                    {
                        ProcessFiles(businessMessage.ReplyToMessage);
                    }
                    break;
                case BusinessMessages businessMessages:
                    foreach (var item in businessMessages.Messages)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case BusinessStartPage businessStartPage:
                    if (businessStartPage.Sticker != null)
                    {
                        ProcessFiles(businessStartPage.Sticker);
                    }
                    break;
                case Chat chat:
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
                    break;
                case ChatBackground chatBackground:
                    if (chatBackground.Background != null)
                    {
                        ProcessFiles(chatBackground.Background);
                    }
                    break;
                case ChatEvent chatEvent:
                    if (chatEvent.Action != null)
                    {
                        ProcessFiles(chatEvent.Action);
                    }
                    break;
                case ChatEventBackgroundChanged chatEventBackgroundChanged:
                    if (chatEventBackgroundChanged.NewBackground != null)
                    {
                        ProcessFiles(chatEventBackgroundChanged.NewBackground);
                    }
                    if (chatEventBackgroundChanged.OldBackground != null)
                    {
                        ProcessFiles(chatEventBackgroundChanged.OldBackground);
                    }
                    break;
                case ChatEventMessageDeleted chatEventMessageDeleted:
                    if (chatEventMessageDeleted.Message != null)
                    {
                        ProcessFiles(chatEventMessageDeleted.Message);
                    }
                    break;
                case ChatEventMessageEdited chatEventMessageEdited:
                    if (chatEventMessageEdited.NewMessage != null)
                    {
                        ProcessFiles(chatEventMessageEdited.NewMessage);
                    }
                    if (chatEventMessageEdited.OldMessage != null)
                    {
                        ProcessFiles(chatEventMessageEdited.OldMessage);
                    }
                    break;
                case ChatEventMessagePinned chatEventMessagePinned:
                    if (chatEventMessagePinned.Message != null)
                    {
                        ProcessFiles(chatEventMessagePinned.Message);
                    }
                    break;
                case ChatEventMessageUnpinned chatEventMessageUnpinned:
                    if (chatEventMessageUnpinned.Message != null)
                    {
                        ProcessFiles(chatEventMessageUnpinned.Message);
                    }
                    break;
                case ChatEventPhotoChanged chatEventPhotoChanged:
                    if (chatEventPhotoChanged.NewPhoto != null)
                    {
                        ProcessFiles(chatEventPhotoChanged.NewPhoto);
                    }
                    if (chatEventPhotoChanged.OldPhoto != null)
                    {
                        ProcessFiles(chatEventPhotoChanged.OldPhoto);
                    }
                    break;
                case ChatEventPollStopped chatEventPollStopped:
                    if (chatEventPollStopped.Message != null)
                    {
                        ProcessFiles(chatEventPollStopped.Message);
                    }
                    break;
                case ChatEvents chatEvents:
                    foreach (var item in chatEvents.Events)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case ChatInviteLinkInfo chatInviteLinkInfo:
                    if (chatInviteLinkInfo.Photo != null)
                    {
                        ProcessFiles(chatInviteLinkInfo.Photo);
                    }
                    break;
                case ChatPhoto chatPhoto:
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
                    break;
                case ChatPhotoInfo chatPhotoInfo:
                    if (chatPhotoInfo.Big != null)
                    {
                        chatPhotoInfo.Big = ProcessFile(chatPhotoInfo.Big);
                    }
                    if (chatPhotoInfo.Small != null)
                    {
                        chatPhotoInfo.Small = ProcessFile(chatPhotoInfo.Small);
                    }
                    break;
                case ChatPhotos chatPhotos:
                    foreach (var item in chatPhotos.Photos)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case ChatTheme chatTheme:
                    if (chatTheme.DarkSettings != null)
                    {
                        ProcessFiles(chatTheme.DarkSettings);
                    }
                    if (chatTheme.LightSettings != null)
                    {
                        ProcessFiles(chatTheme.LightSettings);
                    }
                    break;
                case ChatTransactionPurposePaidMedia chatTransactionPurposePaidMedia:
                    foreach (var item in chatTransactionPurposePaidMedia.Media)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case DatedFile datedFile:
                    if (datedFile.File != null)
                    {
                        datedFile.File = ProcessFile(datedFile.File);
                    }
                    break;
                case DiceStickersRegular diceStickersRegular:
                    if (diceStickersRegular.Sticker != null)
                    {
                        ProcessFiles(diceStickersRegular.Sticker);
                    }
                    break;
                case DiceStickersSlotMachine diceStickersSlotMachine:
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
                    break;
                case Document document:
                    if (document.DocumentValue != null)
                    {
                        document.DocumentValue = ProcessFile(document.DocumentValue);
                    }
                    if (document.Thumbnail != null)
                    {
                        ProcessFiles(document.Thumbnail);
                    }
                    break;
                case EmojiCategories emojiCategories:
                    foreach (var item in emojiCategories.Categories)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case EmojiCategory emojiCategory:
                    if (emojiCategory.Icon != null)
                    {
                        ProcessFiles(emojiCategory.Icon);
                    }
                    break;
                case EmojiReaction emojiReaction:
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
                    break;
                case EncryptedPassportElement encryptedPassportElement:
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
                    break;
                case FileDownload fileDownload:
                    if (fileDownload.Message != null)
                    {
                        ProcessFiles(fileDownload.Message);
                    }
                    break;
                case ForumTopic forumTopic:
                    if (forumTopic.LastMessage != null)
                    {
                        ProcessFiles(forumTopic.LastMessage);
                    }
                    break;
                case ForumTopics forumTopics:
                    foreach (var item in forumTopics.Topics)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case FoundChatMessages foundChatMessages:
                    foreach (var item in foundChatMessages.Messages)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case FoundFileDownloads foundFileDownloads:
                    foreach (var item in foundFileDownloads.Files)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case FoundMessages foundMessages:
                    foreach (var item in foundMessages.Messages)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case FoundStories foundStories:
                    foreach (var item in foundStories.Stories)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case FoundWebApp foundWebApp:
                    if (foundWebApp.WebApp != null)
                    {
                        ProcessFiles(foundWebApp.WebApp);
                    }
                    break;
                case Game game:
                    if (game.Animation != null)
                    {
                        ProcessFiles(game.Animation);
                    }
                    if (game.Photo != null)
                    {
                        ProcessFiles(game.Photo);
                    }
                    break;
                case Gift gift:
                    if (gift.Sticker != null)
                    {
                        ProcessFiles(gift.Sticker);
                    }
                    break;
                case Gifts gifts:
                    foreach (var item in gifts.GiftsValue)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case IdentityDocument identityDocument:
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
                    break;
                case InlineQueryResultAnimation inlineQueryResultAnimation:
                    if (inlineQueryResultAnimation.Animation != null)
                    {
                        ProcessFiles(inlineQueryResultAnimation.Animation);
                    }
                    break;
                case InlineQueryResultArticle inlineQueryResultArticle:
                    if (inlineQueryResultArticle.Thumbnail != null)
                    {
                        ProcessFiles(inlineQueryResultArticle.Thumbnail);
                    }
                    break;
                case InlineQueryResultAudio inlineQueryResultAudio:
                    if (inlineQueryResultAudio.Audio != null)
                    {
                        ProcessFiles(inlineQueryResultAudio.Audio);
                    }
                    break;
                case InlineQueryResultContact inlineQueryResultContact:
                    if (inlineQueryResultContact.Thumbnail != null)
                    {
                        ProcessFiles(inlineQueryResultContact.Thumbnail);
                    }
                    break;
                case InlineQueryResultDocument inlineQueryResultDocument:
                    if (inlineQueryResultDocument.Document != null)
                    {
                        ProcessFiles(inlineQueryResultDocument.Document);
                    }
                    break;
                case InlineQueryResultGame inlineQueryResultGame:
                    if (inlineQueryResultGame.Game != null)
                    {
                        ProcessFiles(inlineQueryResultGame.Game);
                    }
                    break;
                case InlineQueryResultLocation inlineQueryResultLocation:
                    if (inlineQueryResultLocation.Thumbnail != null)
                    {
                        ProcessFiles(inlineQueryResultLocation.Thumbnail);
                    }
                    break;
                case InlineQueryResultPhoto inlineQueryResultPhoto:
                    if (inlineQueryResultPhoto.Photo != null)
                    {
                        ProcessFiles(inlineQueryResultPhoto.Photo);
                    }
                    break;
                case InlineQueryResults inlineQueryResults:
                    foreach (var item in inlineQueryResults.Results)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case InlineQueryResultSticker inlineQueryResultSticker:
                    if (inlineQueryResultSticker.Sticker != null)
                    {
                        ProcessFiles(inlineQueryResultSticker.Sticker);
                    }
                    break;
                case InlineQueryResultVenue inlineQueryResultVenue:
                    if (inlineQueryResultVenue.Thumbnail != null)
                    {
                        ProcessFiles(inlineQueryResultVenue.Thumbnail);
                    }
                    break;
                case InlineQueryResultVideo inlineQueryResultVideo:
                    if (inlineQueryResultVideo.Video != null)
                    {
                        ProcessFiles(inlineQueryResultVideo.Video);
                    }
                    break;
                case InlineQueryResultVoiceNote inlineQueryResultVoiceNote:
                    if (inlineQueryResultVoiceNote.VoiceNote != null)
                    {
                        ProcessFiles(inlineQueryResultVoiceNote.VoiceNote);
                    }
                    break;
                case LinkPreview linkPreview:
                    if (linkPreview.Type != null)
                    {
                        ProcessFiles(linkPreview.Type);
                    }
                    break;
                case LinkPreviewAlbumMediaPhoto linkPreviewAlbumMediaPhoto:
                    if (linkPreviewAlbumMediaPhoto.Photo != null)
                    {
                        ProcessFiles(linkPreviewAlbumMediaPhoto.Photo);
                    }
                    break;
                case LinkPreviewAlbumMediaVideo linkPreviewAlbumMediaVideo:
                    if (linkPreviewAlbumMediaVideo.Video != null)
                    {
                        ProcessFiles(linkPreviewAlbumMediaVideo.Video);
                    }
                    break;
                case LinkPreviewTypeAlbum linkPreviewTypeAlbum:
                    foreach (var item in linkPreviewTypeAlbum.Media)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case LinkPreviewTypeAnimation linkPreviewTypeAnimation:
                    if (linkPreviewTypeAnimation.Animation != null)
                    {
                        ProcessFiles(linkPreviewTypeAnimation.Animation);
                    }
                    break;
                case LinkPreviewTypeApp linkPreviewTypeApp:
                    if (linkPreviewTypeApp.Photo != null)
                    {
                        ProcessFiles(linkPreviewTypeApp.Photo);
                    }
                    break;
                case LinkPreviewTypeArticle linkPreviewTypeArticle:
                    if (linkPreviewTypeArticle.Photo != null)
                    {
                        ProcessFiles(linkPreviewTypeArticle.Photo);
                    }
                    break;
                case LinkPreviewTypeAudio linkPreviewTypeAudio:
                    if (linkPreviewTypeAudio.Audio != null)
                    {
                        ProcessFiles(linkPreviewTypeAudio.Audio);
                    }
                    break;
                case LinkPreviewTypeBackground linkPreviewTypeBackground:
                    if (linkPreviewTypeBackground.Document != null)
                    {
                        ProcessFiles(linkPreviewTypeBackground.Document);
                    }
                    break;
                case LinkPreviewTypeChannelBoost linkPreviewTypeChannelBoost:
                    if (linkPreviewTypeChannelBoost.Photo != null)
                    {
                        ProcessFiles(linkPreviewTypeChannelBoost.Photo);
                    }
                    break;
                case LinkPreviewTypeChat linkPreviewTypeChat:
                    if (linkPreviewTypeChat.Photo != null)
                    {
                        ProcessFiles(linkPreviewTypeChat.Photo);
                    }
                    break;
                case LinkPreviewTypeDocument linkPreviewTypeDocument:
                    if (linkPreviewTypeDocument.Document != null)
                    {
                        ProcessFiles(linkPreviewTypeDocument.Document);
                    }
                    break;
                case LinkPreviewTypeEmbeddedAnimationPlayer linkPreviewTypeEmbeddedAnimationPlayer:
                    if (linkPreviewTypeEmbeddedAnimationPlayer.Thumbnail != null)
                    {
                        ProcessFiles(linkPreviewTypeEmbeddedAnimationPlayer.Thumbnail);
                    }
                    break;
                case LinkPreviewTypeEmbeddedAudioPlayer linkPreviewTypeEmbeddedAudioPlayer:
                    if (linkPreviewTypeEmbeddedAudioPlayer.Thumbnail != null)
                    {
                        ProcessFiles(linkPreviewTypeEmbeddedAudioPlayer.Thumbnail);
                    }
                    break;
                case LinkPreviewTypeEmbeddedVideoPlayer linkPreviewTypeEmbeddedVideoPlayer:
                    if (linkPreviewTypeEmbeddedVideoPlayer.Thumbnail != null)
                    {
                        ProcessFiles(linkPreviewTypeEmbeddedVideoPlayer.Thumbnail);
                    }
                    break;
                case LinkPreviewTypePhoto linkPreviewTypePhoto:
                    if (linkPreviewTypePhoto.Photo != null)
                    {
                        ProcessFiles(linkPreviewTypePhoto.Photo);
                    }
                    break;
                case LinkPreviewTypeSticker linkPreviewTypeSticker:
                    if (linkPreviewTypeSticker.Sticker != null)
                    {
                        ProcessFiles(linkPreviewTypeSticker.Sticker);
                    }
                    break;
                case LinkPreviewTypeStickerSet linkPreviewTypeStickerSet:
                    foreach (var item in linkPreviewTypeStickerSet.Stickers)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case LinkPreviewTypeSupergroupBoost linkPreviewTypeSupergroupBoost:
                    if (linkPreviewTypeSupergroupBoost.Photo != null)
                    {
                        ProcessFiles(linkPreviewTypeSupergroupBoost.Photo);
                    }
                    break;
                case LinkPreviewTypeTheme linkPreviewTypeTheme:
                    foreach (var item in linkPreviewTypeTheme.Documents)
                    {
                        ProcessFiles(item);
                    }
                    if (linkPreviewTypeTheme.Settings != null)
                    {
                        ProcessFiles(linkPreviewTypeTheme.Settings);
                    }
                    break;
                case LinkPreviewTypeUser linkPreviewTypeUser:
                    if (linkPreviewTypeUser.Photo != null)
                    {
                        ProcessFiles(linkPreviewTypeUser.Photo);
                    }
                    break;
                case LinkPreviewTypeVideo linkPreviewTypeVideo:
                    if (linkPreviewTypeVideo.Video != null)
                    {
                        ProcessFiles(linkPreviewTypeVideo.Video);
                    }
                    break;
                case LinkPreviewTypeVideoChat linkPreviewTypeVideoChat:
                    if (linkPreviewTypeVideoChat.Photo != null)
                    {
                        ProcessFiles(linkPreviewTypeVideoChat.Photo);
                    }
                    break;
                case LinkPreviewTypeVideoNote linkPreviewTypeVideoNote:
                    if (linkPreviewTypeVideoNote.VideoNote != null)
                    {
                        ProcessFiles(linkPreviewTypeVideoNote.VideoNote);
                    }
                    break;
                case LinkPreviewTypeVoiceNote linkPreviewTypeVoiceNote:
                    if (linkPreviewTypeVoiceNote.VoiceNote != null)
                    {
                        ProcessFiles(linkPreviewTypeVoiceNote.VoiceNote);
                    }
                    break;
                case LinkPreviewTypeWebApp linkPreviewTypeWebApp:
                    if (linkPreviewTypeWebApp.Photo != null)
                    {
                        ProcessFiles(linkPreviewTypeWebApp.Photo);
                    }
                    break;
                case Message message:
                    if (message.Content != null)
                    {
                        ProcessFiles(message.Content);
                    }
                    if (message.ReplyTo != null)
                    {
                        ProcessFiles(message.ReplyTo);
                    }
                    break;
                case MessageAnimatedEmoji messageAnimatedEmoji:
                    if (messageAnimatedEmoji.AnimatedEmoji != null)
                    {
                        ProcessFiles(messageAnimatedEmoji.AnimatedEmoji);
                    }
                    break;
                case MessageAnimation messageAnimation:
                    if (messageAnimation.Animation != null)
                    {
                        ProcessFiles(messageAnimation.Animation);
                    }
                    break;
                case MessageAudio messageAudio:
                    if (messageAudio.Audio != null)
                    {
                        ProcessFiles(messageAudio.Audio);
                    }
                    break;
                case MessageBotWriteAccessAllowed messageBotWriteAccessAllowed:
                    if (messageBotWriteAccessAllowed.Reason != null)
                    {
                        ProcessFiles(messageBotWriteAccessAllowed.Reason);
                    }
                    break;
                case MessageCalendar messageCalendar:
                    foreach (var item in messageCalendar.Days)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case MessageCalendarDay messageCalendarDay:
                    if (messageCalendarDay.Message != null)
                    {
                        ProcessFiles(messageCalendarDay.Message);
                    }
                    break;
                case MessageChatChangePhoto messageChatChangePhoto:
                    if (messageChatChangePhoto.Photo != null)
                    {
                        ProcessFiles(messageChatChangePhoto.Photo);
                    }
                    break;
                case MessageChatSetBackground messageChatSetBackground:
                    if (messageChatSetBackground.Background != null)
                    {
                        ProcessFiles(messageChatSetBackground.Background);
                    }
                    break;
                case MessageChatShared messageChatShared:
                    if (messageChatShared.Chat != null)
                    {
                        ProcessFiles(messageChatShared.Chat);
                    }
                    break;
                case MessageDice messageDice:
                    if (messageDice.FinalState != null)
                    {
                        ProcessFiles(messageDice.FinalState);
                    }
                    if (messageDice.InitialState != null)
                    {
                        ProcessFiles(messageDice.InitialState);
                    }
                    break;
                case MessageDocument messageDocument:
                    if (messageDocument.Document != null)
                    {
                        ProcessFiles(messageDocument.Document);
                    }
                    break;
                case MessageEffect messageEffect:
                    if (messageEffect.StaticIcon != null)
                    {
                        ProcessFiles(messageEffect.StaticIcon);
                    }
                    if (messageEffect.Type != null)
                    {
                        ProcessFiles(messageEffect.Type);
                    }
                    break;
                case MessageEffectTypeEmojiReaction messageEffectTypeEmojiReaction:
                    if (messageEffectTypeEmojiReaction.EffectAnimation != null)
                    {
                        ProcessFiles(messageEffectTypeEmojiReaction.EffectAnimation);
                    }
                    if (messageEffectTypeEmojiReaction.SelectAnimation != null)
                    {
                        ProcessFiles(messageEffectTypeEmojiReaction.SelectAnimation);
                    }
                    break;
                case MessageEffectTypePremiumSticker messageEffectTypePremiumSticker:
                    if (messageEffectTypePremiumSticker.Sticker != null)
                    {
                        ProcessFiles(messageEffectTypePremiumSticker.Sticker);
                    }
                    break;
                case MessageGame messageGame:
                    if (messageGame.Game != null)
                    {
                        ProcessFiles(messageGame.Game);
                    }
                    break;
                case MessageGift messageGift:
                    if (messageGift.Gift != null)
                    {
                        ProcessFiles(messageGift.Gift);
                    }
                    break;
                case MessageGiftedPremium messageGiftedPremium:
                    if (messageGiftedPremium.Sticker != null)
                    {
                        ProcessFiles(messageGiftedPremium.Sticker);
                    }
                    break;
                case MessageGiftedStars messageGiftedStars:
                    if (messageGiftedStars.Sticker != null)
                    {
                        ProcessFiles(messageGiftedStars.Sticker);
                    }
                    break;
                case MessageGiveaway messageGiveaway:
                    if (messageGiveaway.Sticker != null)
                    {
                        ProcessFiles(messageGiveaway.Sticker);
                    }
                    break;
                case MessageGiveawayPrizeStars messageGiveawayPrizeStars:
                    if (messageGiveawayPrizeStars.Sticker != null)
                    {
                        ProcessFiles(messageGiveawayPrizeStars.Sticker);
                    }
                    break;
                case MessageInvoice messageInvoice:
                    if (messageInvoice.PaidMedia != null)
                    {
                        ProcessFiles(messageInvoice.PaidMedia);
                    }
                    if (messageInvoice.ProductInfo != null)
                    {
                        ProcessFiles(messageInvoice.ProductInfo);
                    }
                    break;
                case MessageLinkInfo messageLinkInfo:
                    if (messageLinkInfo.Message != null)
                    {
                        ProcessFiles(messageLinkInfo.Message);
                    }
                    break;
                case MessagePaidMedia messagePaidMedia:
                    foreach (var item in messagePaidMedia.Media)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case MessagePassportDataReceived messagePassportDataReceived:
                    foreach (var item in messagePassportDataReceived.Elements)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case MessagePhoto messagePhoto:
                    if (messagePhoto.Photo != null)
                    {
                        ProcessFiles(messagePhoto.Photo);
                    }
                    break;
                case MessagePremiumGiftCode messagePremiumGiftCode:
                    if (messagePremiumGiftCode.Sticker != null)
                    {
                        ProcessFiles(messagePremiumGiftCode.Sticker);
                    }
                    break;
                case MessageReplyToMessage messageReplyToMessage:
                    if (messageReplyToMessage.Content != null)
                    {
                        ProcessFiles(messageReplyToMessage.Content);
                    }
                    break;
                case Messages messages:
                    foreach (var item in messages.MessagesValue)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case MessageSponsor messageSponsor:
                    if (messageSponsor.Photo != null)
                    {
                        ProcessFiles(messageSponsor.Photo);
                    }
                    break;
                case MessageSticker messageSticker:
                    if (messageSticker.Sticker != null)
                    {
                        ProcessFiles(messageSticker.Sticker);
                    }
                    break;
                case MessageSuggestProfilePhoto messageSuggestProfilePhoto:
                    if (messageSuggestProfilePhoto.Photo != null)
                    {
                        ProcessFiles(messageSuggestProfilePhoto.Photo);
                    }
                    break;
                case MessageText messageText:
                    if (messageText.LinkPreview != null)
                    {
                        ProcessFiles(messageText.LinkPreview);
                    }
                    break;
                case MessageThreadInfo messageThreadInfo:
                    foreach (var item in messageThreadInfo.Messages)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case MessageUsersShared messageUsersShared:
                    foreach (var item in messageUsersShared.Users)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case MessageVideo messageVideo:
                    foreach (var item in messageVideo.AlternativeVideos)
                    {
                        ProcessFiles(item);
                    }
                    if (messageVideo.Video != null)
                    {
                        ProcessFiles(messageVideo.Video);
                    }
                    break;
                case MessageVideoNote messageVideoNote:
                    if (messageVideoNote.VideoNote != null)
                    {
                        ProcessFiles(messageVideoNote.VideoNote);
                    }
                    break;
                case MessageVoiceNote messageVoiceNote:
                    if (messageVoiceNote.VoiceNote != null)
                    {
                        ProcessFiles(messageVoiceNote.VoiceNote);
                    }
                    break;
                case Notification notification:
                    if (notification.Type != null)
                    {
                        ProcessFiles(notification.Type);
                    }
                    break;
                case NotificationGroup notificationGroup:
                    foreach (var item in notificationGroup.Notifications)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case NotificationSound notificationSound:
                    if (notificationSound.Sound != null)
                    {
                        notificationSound.Sound = ProcessFile(notificationSound.Sound);
                    }
                    break;
                case NotificationSounds notificationSounds:
                    foreach (var item in notificationSounds.NotificationSoundsValue)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case NotificationTypeNewMessage notificationTypeNewMessage:
                    if (notificationTypeNewMessage.Message != null)
                    {
                        ProcessFiles(notificationTypeNewMessage.Message);
                    }
                    break;
                case NotificationTypeNewPushMessage notificationTypeNewPushMessage:
                    if (notificationTypeNewPushMessage.Content != null)
                    {
                        ProcessFiles(notificationTypeNewPushMessage.Content);
                    }
                    break;
                case PageBlockAnimation pageBlockAnimation:
                    if (pageBlockAnimation.Animation != null)
                    {
                        ProcessFiles(pageBlockAnimation.Animation);
                    }
                    if (pageBlockAnimation.Caption != null)
                    {
                        ProcessFiles(pageBlockAnimation.Caption);
                    }
                    break;
                case PageBlockAudio pageBlockAudio:
                    if (pageBlockAudio.Audio != null)
                    {
                        ProcessFiles(pageBlockAudio.Audio);
                    }
                    if (pageBlockAudio.Caption != null)
                    {
                        ProcessFiles(pageBlockAudio.Caption);
                    }
                    break;
                case PageBlockAuthorDate pageBlockAuthorDate:
                    if (pageBlockAuthorDate.Author != null)
                    {
                        ProcessFiles(pageBlockAuthorDate.Author);
                    }
                    break;
                case PageBlockBlockQuote pageBlockBlockQuote:
                    if (pageBlockBlockQuote.Credit != null)
                    {
                        ProcessFiles(pageBlockBlockQuote.Credit);
                    }
                    if (pageBlockBlockQuote.Text != null)
                    {
                        ProcessFiles(pageBlockBlockQuote.Text);
                    }
                    break;
                case PageBlockCaption pageBlockCaption:
                    if (pageBlockCaption.Credit != null)
                    {
                        ProcessFiles(pageBlockCaption.Credit);
                    }
                    if (pageBlockCaption.Text != null)
                    {
                        ProcessFiles(pageBlockCaption.Text);
                    }
                    break;
                case PageBlockChatLink pageBlockChatLink:
                    if (pageBlockChatLink.Photo != null)
                    {
                        ProcessFiles(pageBlockChatLink.Photo);
                    }
                    break;
                case PageBlockCollage pageBlockCollage:
                    if (pageBlockCollage.Caption != null)
                    {
                        ProcessFiles(pageBlockCollage.Caption);
                    }
                    foreach (var item in pageBlockCollage.PageBlocks)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PageBlockCover pageBlockCover:
                    if (pageBlockCover.Cover != null)
                    {
                        ProcessFiles(pageBlockCover.Cover);
                    }
                    break;
                case PageBlockDetails pageBlockDetails:
                    if (pageBlockDetails.Header != null)
                    {
                        ProcessFiles(pageBlockDetails.Header);
                    }
                    foreach (var item in pageBlockDetails.PageBlocks)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PageBlockEmbedded pageBlockEmbedded:
                    if (pageBlockEmbedded.Caption != null)
                    {
                        ProcessFiles(pageBlockEmbedded.Caption);
                    }
                    if (pageBlockEmbedded.PosterPhoto != null)
                    {
                        ProcessFiles(pageBlockEmbedded.PosterPhoto);
                    }
                    break;
                case PageBlockEmbeddedPost pageBlockEmbeddedPost:
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
                    break;
                case PageBlockFooter pageBlockFooter:
                    if (pageBlockFooter.Footer != null)
                    {
                        ProcessFiles(pageBlockFooter.Footer);
                    }
                    break;
                case PageBlockHeader pageBlockHeader:
                    if (pageBlockHeader.Header != null)
                    {
                        ProcessFiles(pageBlockHeader.Header);
                    }
                    break;
                case PageBlockKicker pageBlockKicker:
                    if (pageBlockKicker.Kicker != null)
                    {
                        ProcessFiles(pageBlockKicker.Kicker);
                    }
                    break;
                case PageBlockList pageBlockList:
                    foreach (var item in pageBlockList.Items)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PageBlockListItem pageBlockListItem:
                    foreach (var item in pageBlockListItem.PageBlocks)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PageBlockMap pageBlockMap:
                    if (pageBlockMap.Caption != null)
                    {
                        ProcessFiles(pageBlockMap.Caption);
                    }
                    break;
                case PageBlockParagraph pageBlockParagraph:
                    if (pageBlockParagraph.Text != null)
                    {
                        ProcessFiles(pageBlockParagraph.Text);
                    }
                    break;
                case PageBlockPhoto pageBlockPhoto:
                    if (pageBlockPhoto.Caption != null)
                    {
                        ProcessFiles(pageBlockPhoto.Caption);
                    }
                    if (pageBlockPhoto.Photo != null)
                    {
                        ProcessFiles(pageBlockPhoto.Photo);
                    }
                    break;
                case PageBlockPreformatted pageBlockPreformatted:
                    if (pageBlockPreformatted.Text != null)
                    {
                        ProcessFiles(pageBlockPreformatted.Text);
                    }
                    break;
                case PageBlockPullQuote pageBlockPullQuote:
                    if (pageBlockPullQuote.Credit != null)
                    {
                        ProcessFiles(pageBlockPullQuote.Credit);
                    }
                    if (pageBlockPullQuote.Text != null)
                    {
                        ProcessFiles(pageBlockPullQuote.Text);
                    }
                    break;
                case PageBlockRelatedArticle pageBlockRelatedArticle:
                    if (pageBlockRelatedArticle.Photo != null)
                    {
                        ProcessFiles(pageBlockRelatedArticle.Photo);
                    }
                    break;
                case PageBlockRelatedArticles pageBlockRelatedArticles:
                    foreach (var item in pageBlockRelatedArticles.Articles)
                    {
                        ProcessFiles(item);
                    }
                    if (pageBlockRelatedArticles.Header != null)
                    {
                        ProcessFiles(pageBlockRelatedArticles.Header);
                    }
                    break;
                case PageBlockSlideshow pageBlockSlideshow:
                    if (pageBlockSlideshow.Caption != null)
                    {
                        ProcessFiles(pageBlockSlideshow.Caption);
                    }
                    foreach (var item in pageBlockSlideshow.PageBlocks)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PageBlockSubheader pageBlockSubheader:
                    if (pageBlockSubheader.Subheader != null)
                    {
                        ProcessFiles(pageBlockSubheader.Subheader);
                    }
                    break;
                case PageBlockSubtitle pageBlockSubtitle:
                    if (pageBlockSubtitle.Subtitle != null)
                    {
                        ProcessFiles(pageBlockSubtitle.Subtitle);
                    }
                    break;
                case PageBlockTable pageBlockTable:
                    if (pageBlockTable.Caption != null)
                    {
                        ProcessFiles(pageBlockTable.Caption);
                    }
                    foreach (var item in pageBlockTable.Cells)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PageBlockTableCell pageBlockTableCell:
                    if (pageBlockTableCell.Text != null)
                    {
                        ProcessFiles(pageBlockTableCell.Text);
                    }
                    break;
                case PageBlockTitle pageBlockTitle:
                    if (pageBlockTitle.Title != null)
                    {
                        ProcessFiles(pageBlockTitle.Title);
                    }
                    break;
                case PageBlockVideo pageBlockVideo:
                    if (pageBlockVideo.Caption != null)
                    {
                        ProcessFiles(pageBlockVideo.Caption);
                    }
                    if (pageBlockVideo.Video != null)
                    {
                        ProcessFiles(pageBlockVideo.Video);
                    }
                    break;
                case PageBlockVoiceNote pageBlockVoiceNote:
                    if (pageBlockVoiceNote.Caption != null)
                    {
                        ProcessFiles(pageBlockVoiceNote.Caption);
                    }
                    if (pageBlockVoiceNote.VoiceNote != null)
                    {
                        ProcessFiles(pageBlockVoiceNote.VoiceNote);
                    }
                    break;
                case PaidMediaPhoto paidMediaPhoto:
                    if (paidMediaPhoto.Photo != null)
                    {
                        ProcessFiles(paidMediaPhoto.Photo);
                    }
                    break;
                case PaidMediaVideo paidMediaVideo:
                    if (paidMediaVideo.Video != null)
                    {
                        ProcessFiles(paidMediaVideo.Video);
                    }
                    break;
                case PassportElementBankStatement passportElementBankStatement:
                    if (passportElementBankStatement.BankStatement != null)
                    {
                        ProcessFiles(passportElementBankStatement.BankStatement);
                    }
                    break;
                case PassportElementDriverLicense passportElementDriverLicense:
                    if (passportElementDriverLicense.DriverLicense != null)
                    {
                        ProcessFiles(passportElementDriverLicense.DriverLicense);
                    }
                    break;
                case PassportElementIdentityCard passportElementIdentityCard:
                    if (passportElementIdentityCard.IdentityCard != null)
                    {
                        ProcessFiles(passportElementIdentityCard.IdentityCard);
                    }
                    break;
                case PassportElementInternalPassport passportElementInternalPassport:
                    if (passportElementInternalPassport.InternalPassport != null)
                    {
                        ProcessFiles(passportElementInternalPassport.InternalPassport);
                    }
                    break;
                case PassportElementPassport passportElementPassport:
                    if (passportElementPassport.Passport != null)
                    {
                        ProcessFiles(passportElementPassport.Passport);
                    }
                    break;
                case PassportElementPassportRegistration passportElementPassportRegistration:
                    if (passportElementPassportRegistration.PassportRegistration != null)
                    {
                        ProcessFiles(passportElementPassportRegistration.PassportRegistration);
                    }
                    break;
                case PassportElementRentalAgreement passportElementRentalAgreement:
                    if (passportElementRentalAgreement.RentalAgreement != null)
                    {
                        ProcessFiles(passportElementRentalAgreement.RentalAgreement);
                    }
                    break;
                case PassportElements passportElements:
                    foreach (var item in passportElements.Elements)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PassportElementsWithErrors passportElementsWithErrors:
                    foreach (var item in passportElementsWithErrors.Elements)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PassportElementTemporaryRegistration passportElementTemporaryRegistration:
                    if (passportElementTemporaryRegistration.TemporaryRegistration != null)
                    {
                        ProcessFiles(passportElementTemporaryRegistration.TemporaryRegistration);
                    }
                    break;
                case PassportElementUtilityBill passportElementUtilityBill:
                    if (passportElementUtilityBill.UtilityBill != null)
                    {
                        ProcessFiles(passportElementUtilityBill.UtilityBill);
                    }
                    break;
                case PaymentForm paymentForm:
                    if (paymentForm.ProductInfo != null)
                    {
                        ProcessFiles(paymentForm.ProductInfo);
                    }
                    break;
                case PaymentReceipt paymentReceipt:
                    if (paymentReceipt.ProductInfo != null)
                    {
                        ProcessFiles(paymentReceipt.ProductInfo);
                    }
                    break;
                case PersonalDocument personalDocument:
                    foreach (var item in personalDocument.Files)
                    {
                        ProcessFiles(item);
                    }
                    foreach (var item in personalDocument.Translation)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case Photo photo:
                    foreach (var item in photo.Sizes)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PhotoSize photoSize:
                    if (photoSize.Photo != null)
                    {
                        photoSize.Photo = ProcessFile(photoSize.Photo);
                    }
                    break;
                case PremiumFeaturePromotionAnimation premiumFeaturePromotionAnimation:
                    if (premiumFeaturePromotionAnimation.Animation != null)
                    {
                        ProcessFiles(premiumFeaturePromotionAnimation.Animation);
                    }
                    break;
                case PremiumState premiumState:
                    foreach (var item in premiumState.Animations)
                    {
                        ProcessFiles(item);
                    }
                    foreach (var item in premiumState.BusinessAnimations)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case ProductInfo productInfo:
                    if (productInfo.Photo != null)
                    {
                        ProcessFiles(productInfo.Photo);
                    }
                    break;
                case ProfilePhoto profilePhoto:
                    if (profilePhoto.Big != null)
                    {
                        profilePhoto.Big = ProcessFile(profilePhoto.Big);
                    }
                    if (profilePhoto.Small != null)
                    {
                        profilePhoto.Small = ProcessFile(profilePhoto.Small);
                    }
                    break;
                case PublicForwardMessage publicForwardMessage:
                    if (publicForwardMessage.Message != null)
                    {
                        ProcessFiles(publicForwardMessage.Message);
                    }
                    break;
                case PublicForwards publicForwards:
                    foreach (var item in publicForwards.Forwards)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case PublicForwardStory publicForwardStory:
                    if (publicForwardStory.Story != null)
                    {
                        ProcessFiles(publicForwardStory.Story);
                    }
                    break;
                case PushMessageContentAnimation pushMessageContentAnimation:
                    if (pushMessageContentAnimation.Animation != null)
                    {
                        ProcessFiles(pushMessageContentAnimation.Animation);
                    }
                    break;
                case PushMessageContentAudio pushMessageContentAudio:
                    if (pushMessageContentAudio.Audio != null)
                    {
                        ProcessFiles(pushMessageContentAudio.Audio);
                    }
                    break;
                case PushMessageContentDocument pushMessageContentDocument:
                    if (pushMessageContentDocument.Document != null)
                    {
                        ProcessFiles(pushMessageContentDocument.Document);
                    }
                    break;
                case PushMessageContentPhoto pushMessageContentPhoto:
                    if (pushMessageContentPhoto.Photo != null)
                    {
                        ProcessFiles(pushMessageContentPhoto.Photo);
                    }
                    break;
                case PushMessageContentSticker pushMessageContentSticker:
                    if (pushMessageContentSticker.Sticker != null)
                    {
                        ProcessFiles(pushMessageContentSticker.Sticker);
                    }
                    break;
                case PushMessageContentVideo pushMessageContentVideo:
                    if (pushMessageContentVideo.Video != null)
                    {
                        ProcessFiles(pushMessageContentVideo.Video);
                    }
                    break;
                case PushMessageContentVideoNote pushMessageContentVideoNote:
                    if (pushMessageContentVideoNote.VideoNote != null)
                    {
                        ProcessFiles(pushMessageContentVideoNote.VideoNote);
                    }
                    break;
                case PushMessageContentVoiceNote pushMessageContentVoiceNote:
                    if (pushMessageContentVoiceNote.VoiceNote != null)
                    {
                        ProcessFiles(pushMessageContentVoiceNote.VoiceNote);
                    }
                    break;
                case QuickReplyMessage quickReplyMessage:
                    if (quickReplyMessage.Content != null)
                    {
                        ProcessFiles(quickReplyMessage.Content);
                    }
                    break;
                case QuickReplyMessages quickReplyMessages:
                    foreach (var item in quickReplyMessages.Messages)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case QuickReplyShortcut quickReplyShortcut:
                    if (quickReplyShortcut.FirstMessage != null)
                    {
                        ProcessFiles(quickReplyShortcut.FirstMessage);
                    }
                    break;
                case RichTextAnchorLink richTextAnchorLink:
                    if (richTextAnchorLink.Text != null)
                    {
                        ProcessFiles(richTextAnchorLink.Text);
                    }
                    break;
                case RichTextBold richTextBold:
                    if (richTextBold.Text != null)
                    {
                        ProcessFiles(richTextBold.Text);
                    }
                    break;
                case RichTextEmailAddress richTextEmailAddress:
                    if (richTextEmailAddress.Text != null)
                    {
                        ProcessFiles(richTextEmailAddress.Text);
                    }
                    break;
                case RichTextFixed richTextFixed:
                    if (richTextFixed.Text != null)
                    {
                        ProcessFiles(richTextFixed.Text);
                    }
                    break;
                case RichTextIcon richTextIcon:
                    if (richTextIcon.Document != null)
                    {
                        ProcessFiles(richTextIcon.Document);
                    }
                    break;
                case RichTextItalic richTextItalic:
                    if (richTextItalic.Text != null)
                    {
                        ProcessFiles(richTextItalic.Text);
                    }
                    break;
                case RichTextMarked richTextMarked:
                    if (richTextMarked.Text != null)
                    {
                        ProcessFiles(richTextMarked.Text);
                    }
                    break;
                case RichTextPhoneNumber richTextPhoneNumber:
                    if (richTextPhoneNumber.Text != null)
                    {
                        ProcessFiles(richTextPhoneNumber.Text);
                    }
                    break;
                case RichTextReference richTextReference:
                    if (richTextReference.Text != null)
                    {
                        ProcessFiles(richTextReference.Text);
                    }
                    break;
                case RichTexts richTexts:
                    foreach (var item in richTexts.Texts)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case RichTextStrikethrough richTextStrikethrough:
                    if (richTextStrikethrough.Text != null)
                    {
                        ProcessFiles(richTextStrikethrough.Text);
                    }
                    break;
                case RichTextSubscript richTextSubscript:
                    if (richTextSubscript.Text != null)
                    {
                        ProcessFiles(richTextSubscript.Text);
                    }
                    break;
                case RichTextSuperscript richTextSuperscript:
                    if (richTextSuperscript.Text != null)
                    {
                        ProcessFiles(richTextSuperscript.Text);
                    }
                    break;
                case RichTextUnderline richTextUnderline:
                    if (richTextUnderline.Text != null)
                    {
                        ProcessFiles(richTextUnderline.Text);
                    }
                    break;
                case RichTextUrl richTextUrl:
                    if (richTextUrl.Text != null)
                    {
                        ProcessFiles(richTextUrl.Text);
                    }
                    break;
                case SavedMessagesTopic savedMessagesTopic:
                    if (savedMessagesTopic.LastMessage != null)
                    {
                        ProcessFiles(savedMessagesTopic.LastMessage);
                    }
                    break;
                case SharedChat sharedChat:
                    if (sharedChat.Photo != null)
                    {
                        ProcessFiles(sharedChat.Photo);
                    }
                    break;
                case SharedUser sharedUser:
                    if (sharedUser.Photo != null)
                    {
                        ProcessFiles(sharedUser.Photo);
                    }
                    break;
                case SponsoredMessage sponsoredMessage:
                    if (sponsoredMessage.Content != null)
                    {
                        ProcessFiles(sponsoredMessage.Content);
                    }
                    if (sponsoredMessage.Sponsor != null)
                    {
                        ProcessFiles(sponsoredMessage.Sponsor);
                    }
                    break;
                case SponsoredMessages sponsoredMessages:
                    foreach (var item in sponsoredMessages.Messages)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case StarTransaction starTransaction:
                    if (starTransaction.Partner != null)
                    {
                        ProcessFiles(starTransaction.Partner);
                    }
                    break;
                case StarTransactionPartnerBot starTransactionPartnerBot:
                    if (starTransactionPartnerBot.Purpose != null)
                    {
                        ProcessFiles(starTransactionPartnerBot.Purpose);
                    }
                    break;
                case StarTransactionPartnerBusiness starTransactionPartnerBusiness:
                    foreach (var item in starTransactionPartnerBusiness.Media)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case StarTransactionPartnerChat starTransactionPartnerChat:
                    if (starTransactionPartnerChat.Purpose != null)
                    {
                        ProcessFiles(starTransactionPartnerChat.Purpose);
                    }
                    break;
                case StarTransactionPartnerUser starTransactionPartnerUser:
                    if (starTransactionPartnerUser.Purpose != null)
                    {
                        ProcessFiles(starTransactionPartnerUser.Purpose);
                    }
                    break;
                case StarTransactions starTransactions:
                    foreach (var item in starTransactions.Transactions)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case Sticker sticker:
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
                    break;
                case StickerFullTypeRegular stickerFullTypeRegular:
                    if (stickerFullTypeRegular.PremiumAnimation != null)
                    {
                        stickerFullTypeRegular.PremiumAnimation = ProcessFile(stickerFullTypeRegular.PremiumAnimation);
                    }
                    break;
                case Stickers stickers:
                    foreach (var item in stickers.StickersValue)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case StickerSet stickerSet:
                    foreach (var item in stickerSet.Stickers)
                    {
                        ProcessFiles(item);
                    }
                    if (stickerSet.Thumbnail != null)
                    {
                        ProcessFiles(stickerSet.Thumbnail);
                    }
                    break;
                case StickerSetInfo stickerSetInfo:
                    foreach (var item in stickerSetInfo.Covers)
                    {
                        ProcessFiles(item);
                    }
                    if (stickerSetInfo.Thumbnail != null)
                    {
                        ProcessFiles(stickerSetInfo.Thumbnail);
                    }
                    break;
                case StickerSets stickerSets:
                    foreach (var item in stickerSets.Sets)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case Stories stories:
                    foreach (var item in stories.StoriesValue)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case Story story:
                    if (story.Content != null)
                    {
                        ProcessFiles(story.Content);
                    }
                    break;
                case StoryContentPhoto storyContentPhoto:
                    if (storyContentPhoto.Photo != null)
                    {
                        ProcessFiles(storyContentPhoto.Photo);
                    }
                    break;
                case StoryContentVideo storyContentVideo:
                    if (storyContentVideo.AlternativeVideo != null)
                    {
                        ProcessFiles(storyContentVideo.AlternativeVideo);
                    }
                    if (storyContentVideo.Video != null)
                    {
                        ProcessFiles(storyContentVideo.Video);
                    }
                    break;
                case StoryInteraction storyInteraction:
                    if (storyInteraction.Type != null)
                    {
                        ProcessFiles(storyInteraction.Type);
                    }
                    break;
                case StoryInteractions storyInteractions:
                    foreach (var item in storyInteractions.Interactions)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case StoryInteractionTypeForward storyInteractionTypeForward:
                    if (storyInteractionTypeForward.Message != null)
                    {
                        ProcessFiles(storyInteractionTypeForward.Message);
                    }
                    break;
                case StoryInteractionTypeRepost storyInteractionTypeRepost:
                    if (storyInteractionTypeRepost.Story != null)
                    {
                        ProcessFiles(storyInteractionTypeRepost.Story);
                    }
                    break;
                case StoryVideo storyVideo:
                    if (storyVideo.Thumbnail != null)
                    {
                        ProcessFiles(storyVideo.Thumbnail);
                    }
                    if (storyVideo.Video != null)
                    {
                        storyVideo.Video = ProcessFile(storyVideo.Video);
                    }
                    break;
                case SupergroupFullInfo supergroupFullInfo:
                    if (supergroupFullInfo.Photo != null)
                    {
                        ProcessFiles(supergroupFullInfo.Photo);
                    }
                    break;
                case ThemeSettings themeSettings:
                    if (themeSettings.Background != null)
                    {
                        ProcessFiles(themeSettings.Background);
                    }
                    break;
                case Thumbnail thumbnail:
                    if (thumbnail.File != null)
                    {
                        thumbnail.File = ProcessFile(thumbnail.File);
                    }
                    break;
                case TMeUrl tMeUrl:
                    if (tMeUrl.Type != null)
                    {
                        ProcessFiles(tMeUrl.Type);
                    }
                    break;
                case TMeUrls tMeUrls:
                    foreach (var item in tMeUrls.Urls)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case TMeUrlTypeChatInvite tMeUrlTypeChatInvite:
                    if (tMeUrlTypeChatInvite.Info != null)
                    {
                        ProcessFiles(tMeUrlTypeChatInvite.Info);
                    }
                    break;
                case TrendingStickerSets trendingStickerSets:
                    foreach (var item in trendingStickerSets.Sets)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case UpdateActiveLiveLocationMessages updateActiveLiveLocationMessages:
                    foreach (var item in updateActiveLiveLocationMessages.Messages)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case UpdateActiveNotifications updateActiveNotifications:
                    foreach (var item in updateActiveNotifications.Groups)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case UpdateAnimatedEmojiMessageClicked updateAnimatedEmojiMessageClicked:
                    if (updateAnimatedEmojiMessageClicked.Sticker != null)
                    {
                        ProcessFiles(updateAnimatedEmojiMessageClicked.Sticker);
                    }
                    break;
                case UpdateAttachmentMenuBots updateAttachmentMenuBots:
                    foreach (var item in updateAttachmentMenuBots.Bots)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case UpdateBasicGroupFullInfo updateBasicGroupFullInfo:
                    if (updateBasicGroupFullInfo.BasicGroupFullInfo != null)
                    {
                        ProcessFiles(updateBasicGroupFullInfo.BasicGroupFullInfo);
                    }
                    break;
                case UpdateBusinessMessageEdited updateBusinessMessageEdited:
                    if (updateBusinessMessageEdited.Message != null)
                    {
                        ProcessFiles(updateBusinessMessageEdited.Message);
                    }
                    break;
                case UpdateChatBackground updateChatBackground:
                    if (updateChatBackground.Background != null)
                    {
                        ProcessFiles(updateChatBackground.Background);
                    }
                    break;
                case UpdateChatLastMessage updateChatLastMessage:
                    if (updateChatLastMessage.LastMessage != null)
                    {
                        ProcessFiles(updateChatLastMessage.LastMessage);
                    }
                    break;
                case UpdateChatPhoto updateChatPhoto:
                    if (updateChatPhoto.Photo != null)
                    {
                        ProcessFiles(updateChatPhoto.Photo);
                    }
                    break;
                case UpdateChatThemes updateChatThemes:
                    foreach (var item in updateChatThemes.ChatThemes)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case UpdateDefaultBackground updateDefaultBackground:
                    if (updateDefaultBackground.Background != null)
                    {
                        ProcessFiles(updateDefaultBackground.Background);
                    }
                    break;
                case UpdateFile updateFile:
                    if (updateFile.File != null)
                    {
                        updateFile.File = ProcessFile(updateFile.File);
                    }
                    break;
                case UpdateFileAddedToDownloads updateFileAddedToDownloads:
                    if (updateFileAddedToDownloads.FileDownload != null)
                    {
                        ProcessFiles(updateFileAddedToDownloads.FileDownload);
                    }
                    break;
                case UpdateMessageContent updateMessageContent:
                    if (updateMessageContent.NewContent != null)
                    {
                        ProcessFiles(updateMessageContent.NewContent);
                    }
                    break;
                case UpdateMessageSendFailed updateMessageSendFailed:
                    if (updateMessageSendFailed.Message != null)
                    {
                        ProcessFiles(updateMessageSendFailed.Message);
                    }
                    break;
                case UpdateMessageSendSucceeded updateMessageSendSucceeded:
                    if (updateMessageSendSucceeded.Message != null)
                    {
                        ProcessFiles(updateMessageSendSucceeded.Message);
                    }
                    break;
                case UpdateNewBusinessCallbackQuery updateNewBusinessCallbackQuery:
                    if (updateNewBusinessCallbackQuery.Message != null)
                    {
                        ProcessFiles(updateNewBusinessCallbackQuery.Message);
                    }
                    break;
                case UpdateNewBusinessMessage updateNewBusinessMessage:
                    if (updateNewBusinessMessage.Message != null)
                    {
                        ProcessFiles(updateNewBusinessMessage.Message);
                    }
                    break;
                case UpdateNewChat updateNewChat:
                    if (updateNewChat.Chat != null)
                    {
                        ProcessFiles(updateNewChat.Chat);
                    }
                    break;
                case UpdateNewMessage updateNewMessage:
                    if (updateNewMessage.Message != null)
                    {
                        ProcessFiles(updateNewMessage.Message);
                    }
                    break;
                case UpdateNotification updateNotification:
                    if (updateNotification.Notification != null)
                    {
                        ProcessFiles(updateNotification.Notification);
                    }
                    break;
                case UpdateNotificationGroup updateNotificationGroup:
                    foreach (var item in updateNotificationGroup.AddedNotifications)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case UpdateQuickReplyShortcut updateQuickReplyShortcut:
                    if (updateQuickReplyShortcut.Shortcut != null)
                    {
                        ProcessFiles(updateQuickReplyShortcut.Shortcut);
                    }
                    break;
                case UpdateQuickReplyShortcutMessages updateQuickReplyShortcutMessages:
                    foreach (var item in updateQuickReplyShortcutMessages.Messages)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case UpdateSavedMessagesTopic updateSavedMessagesTopic:
                    if (updateSavedMessagesTopic.Topic != null)
                    {
                        ProcessFiles(updateSavedMessagesTopic.Topic);
                    }
                    break;
                case UpdateServiceNotification updateServiceNotification:
                    if (updateServiceNotification.Content != null)
                    {
                        ProcessFiles(updateServiceNotification.Content);
                    }
                    break;
                case UpdateStickerSet updateStickerSet:
                    if (updateStickerSet.StickerSet != null)
                    {
                        ProcessFiles(updateStickerSet.StickerSet);
                    }
                    break;
                case UpdateStory updateStory:
                    if (updateStory.Story != null)
                    {
                        ProcessFiles(updateStory.Story);
                    }
                    break;
                case UpdateStorySendFailed updateStorySendFailed:
                    if (updateStorySendFailed.Story != null)
                    {
                        ProcessFiles(updateStorySendFailed.Story);
                    }
                    break;
                case UpdateStorySendSucceeded updateStorySendSucceeded:
                    if (updateStorySendSucceeded.Story != null)
                    {
                        ProcessFiles(updateStorySendSucceeded.Story);
                    }
                    break;
                case UpdateSupergroupFullInfo updateSupergroupFullInfo:
                    if (updateSupergroupFullInfo.SupergroupFullInfo != null)
                    {
                        ProcessFiles(updateSupergroupFullInfo.SupergroupFullInfo);
                    }
                    break;
                case UpdateTrendingStickerSets updateTrendingStickerSets:
                    if (updateTrendingStickerSets.StickerSets != null)
                    {
                        ProcessFiles(updateTrendingStickerSets.StickerSets);
                    }
                    break;
                case UpdateUser updateUser:
                    if (updateUser.User != null)
                    {
                        ProcessFiles(updateUser.User);
                    }
                    break;
                case UpdateUserFullInfo updateUserFullInfo:
                    if (updateUserFullInfo.UserFullInfo != null)
                    {
                        ProcessFiles(updateUserFullInfo.UserFullInfo);
                    }
                    break;
                case User user:
                    if (user.ProfilePhoto != null)
                    {
                        ProcessFiles(user.ProfilePhoto);
                    }
                    break;
                case UserFullInfo userFullInfo:
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
                    break;
                case UserGift userGift:
                    if (userGift.Gift != null)
                    {
                        ProcessFiles(userGift.Gift);
                    }
                    break;
                case UserGifts userGifts:
                    foreach (var item in userGifts.Gifts)
                    {
                        ProcessFiles(item);
                    }
                    break;
                case UserTransactionPurposeGiftedStars userTransactionPurposeGiftedStars:
                    if (userTransactionPurposeGiftedStars.Sticker != null)
                    {
                        ProcessFiles(userTransactionPurposeGiftedStars.Sticker);
                    }
                    break;
                case UserTransactionPurposeGiftSell userTransactionPurposeGiftSell:
                    if (userTransactionPurposeGiftSell.Gift != null)
                    {
                        ProcessFiles(userTransactionPurposeGiftSell.Gift);
                    }
                    break;
                case UserTransactionPurposeGiftSend userTransactionPurposeGiftSend:
                    if (userTransactionPurposeGiftSend.Gift != null)
                    {
                        ProcessFiles(userTransactionPurposeGiftSend.Gift);
                    }
                    break;
                case Video video:
                    if (video.Thumbnail != null)
                    {
                        ProcessFiles(video.Thumbnail);
                    }
                    if (video.VideoValue != null)
                    {
                        video.VideoValue = ProcessFile(video.VideoValue);
                    }
                    break;
                case VideoNote videoNote:
                    if (videoNote.Thumbnail != null)
                    {
                        ProcessFiles(videoNote.Thumbnail);
                    }
                    if (videoNote.Video != null)
                    {
                        videoNote.Video = ProcessFile(videoNote.Video);
                    }
                    break;
                case VoiceNote voiceNote:
                    if (voiceNote.Voice != null)
                    {
                        voiceNote.Voice = ProcessFile(voiceNote.Voice);
                    }
                    break;
                case WebApp webApp:
                    if (webApp.Animation != null)
                    {
                        ProcessFiles(webApp.Animation);
                    }
                    if (webApp.Photo != null)
                    {
                        ProcessFiles(webApp.Photo);
                    }
                    break;
                case WebPageInstantView webPageInstantView:
                    foreach (var item in webPageInstantView.PageBlocks)
                    {
                        ProcessFiles(item);
                    }
                    break;
            }
        }
    }
}
