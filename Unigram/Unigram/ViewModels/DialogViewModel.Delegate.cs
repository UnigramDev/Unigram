using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Gallery;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.Services.Updates;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Gallery;
using Unigram.Views;
using Unigram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel : IMessageDelegate
    {
        private FileContext<MessageViewModel> _filesMap = new FileContext<MessageViewModel>();
        private FileContext<MessageViewModel> _photosMap = new FileContext<MessageViewModel>();

        public bool CanBeDownloaded(MessageViewModel message)
        {
            var content = message.Content as object;
            if (content is MessageAnimation animationMessage)
            {
                content = animationMessage.Animation;
            }
            else if (content is MessageAudio audioMessage)
            {
                content = audioMessage.Audio;
            }
            else if (content is MessageDocument documentMessage)
            {
                content = documentMessage.Document;
            }
            else if (content is MessageGame gameMessage)
            {
                if (gameMessage.Game.Animation != null)
                {
                    content = gameMessage.Game.Animation;
                }
                else if (gameMessage.Game.Photo != null)
                {
                    content = gameMessage.Game.Photo;
                }
            }
            else if (content is MessageInvoice invoiceMessage)
            {
                content = invoiceMessage.Photo;
            }
            else if (content is MessageLocation locationMessage)
            {
                content = locationMessage.Location;
            }
            else if (content is MessagePhoto photoMessage)
            {
                content = photoMessage.Photo;
            }
            else if (content is MessageSticker stickerMessage)
            {
                content = stickerMessage.Sticker;
            }
            else if (content is MessageText textMessage)
            {
                if (textMessage?.WebPage?.Animation != null)
                {
                    content = textMessage?.WebPage?.Animation;
                }
                else if (textMessage?.WebPage?.Document != null)
                {
                    content = textMessage?.WebPage?.Document;
                }
                else if (textMessage?.WebPage?.Sticker != null)
                {
                    content = textMessage?.WebPage?.Sticker;
                }
                else if (textMessage?.WebPage?.Video != null)
                {
                    content = textMessage?.WebPage?.Video;
                }
                else if (textMessage?.WebPage?.VideoNote != null)
                {
                    content = textMessage?.WebPage?.VideoNote;
                }
                // PHOTO SHOULD ALWAYS BE AT THE END!
                else if (textMessage?.WebPage?.Photo != null)
                {
                    content = textMessage?.WebPage?.Photo;
                }
            }
            else if (content is MessageVideo videoMessage)
            {
                content = videoMessage.Video;
            }
            else if (content is MessageVideoNote videoNoteMessage)
            {
                content = videoNoteMessage.VideoNote;
            }
            else if (content is MessageVoiceNote voiceNoteMessage)
            {
                content = voiceNoteMessage.VoiceNote;
            }

            var file = message.GetFile();
            if (file != null && ProtoService.IsDownloadFileCanceled(file.Id))
            {
                return false;
            }

            var chat = _chat;
            if (chat == null)
            {
                return false;
            }

            if (content is Animation animation)
            {
                return Settings.AutoDownload.ShouldDownloadVideo(GetChatType(chat), animation.AnimationValue.Size);
            }
            else if (content is Audio audio)
            {
                return Settings.AutoDownload.ShouldDownloadDocument(GetChatType(chat), audio.AudioValue.Size);
            }
            else if (content is Document document)
            {
                return Settings.AutoDownload.ShouldDownloadDocument(GetChatType(chat), document.DocumentValue.Size);
            }
            else if (content is Photo photo)
            {
                var big = photo.GetBig();
                if (big != null && ProtoService.IsDownloadFileCanceled(big.Photo.Id))
                {
                    return false;
                }

                return Settings.AutoDownload.ShouldDownloadPhoto(GetChatType(chat));
            }
            else if (content is Sticker sticker)
            {
                // Stickers aren't part of the deal
                return true;
            }
            else if (content is Video video)
            {
                return Settings.AutoDownload.ShouldDownloadVideo(GetChatType(chat), video.VideoValue.Size);
            }
            else if (content is VideoNote videoNote)
            {
                return Settings.AutoDownload.ShouldDownloadDocument(GetChatType(chat), videoNote.Video.Size);
            }
            else if (content is VoiceNote voiceNote)
            {
                // Voice notes aren't part of the deal
                return true;
            }

            return false;
        }

        private AutoDownloadChat GetChatType(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel || chat.Type is ChatTypeBasicGroup)
            {
                return AutoDownloadChat.Group;
            }
            else if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var user = ProtoService.GetUser(chat);
                if (user == null)
                {
                    return AutoDownloadChat.OtherPrivateChat;
                }
                else if (user.IsContact)
                {
                    return AutoDownloadChat.Contact;
                }
            }

            return AutoDownloadChat.Channel;
        }

        public void DownloadFile(MessageViewModel message, File file)
        {
            ProtoService.DownloadFile(file.Id, 1);
        }

        public bool TryGetMessagesForFileId(int fileId, out IList<MessageViewModel> items)
        {
            if (_filesMap.TryGetValue(fileId, out List<MessageViewModel> messages))
            {
                items = messages;
                return true;
            }

            items = null;
            return false;
        }

        public bool TryGetMessagesForPhotoId(int fileId, out IList<MessageViewModel> items)
        {
            if (_photosMap.TryGetValue(fileId, out List<MessageViewModel> messages))
            {
                items = messages;
                return true;
            }

            items = null;
            return false;
        }



        public void ReplyToMessage(MessageViewModel message)
        {
            MessageReplyCommand.Execute(message);
        }

        public async void OpenReply(MessageViewModel message)
        {
            if (message.ReplyToMessageState == ReplyToMessageState.None)
            {
                if (message.ReplyInChatId == message.ChatId || message.ReplyInChatId == 0)
                {
                    await LoadMessageSliceAsync(message.Id, message.ReplyToMessageId);
                }
                else
                {
                    NavigationService.NavigateToChat(message.ReplyInChatId, message.ReplyToMessageId);
                }
            }
        }

        public async void OpenThread(MessageViewModel message)
        {
            var response = await ProtoService.SendAsync(new GetMessageThread(message.ChatId, message.Id));
            if (response is MessageThreadInfo info)
            {
                NavigationService.NavigateToThread(message.ChatId, message.Id);
            }
        }



        public async void OpenFile(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                if (file.Local.Path.EndsWith(".unigram-theme"))
                {
                    await new ThemePreviewPopup(file.Local.Path).ShowQueuedAsync();
                    return;
                }

                var temp = await ProtoService.GetFileAsync(file);
                if (temp != null)
                {
                    await Windows.System.Launcher.LaunchFileAsync(temp);
                }
            }
        }

        public void OpenWebPage(WebPage webPage)
        {
            if (webPage.InstantViewVersion != 0)
            {
                //if (NavigationService is UnigramNavigationService asdas)
                //{
                //    asdas.NavigateToInstant(webPage.Url);
                //    return;
                //}

                NavigationService.NavigateToInstant(webPage.Url);
            }
            else if (MessageHelper.TryCreateUri(webPage.Url, out Uri uri) &&
                    (string.Equals(webPage.Type, "telegram_megagroup", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(webPage.Type, "telegram_channel", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(webPage.Type, "telegram_message", StringComparison.OrdinalIgnoreCase)))
            {
                MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, uri);
            }
        }

        public async void OpenSticker(Sticker sticker)
        {
            if (sticker.SetId != 0)
            {
                await StickerSetPopup.GetForCurrentView().ShowAsync(sticker.SetId, Sticker_Click);
            }
        }

        public async void OpenLocation(Location location, string title)
        {
            var options = new Windows.System.LauncherOptions();
            options.FallbackUri = new Uri(string.Format(CultureInfo.InvariantCulture, "https://www.google.com/maps/search/?api=1&query={0},{1}", location.Latitude, location.Longitude));

            if (title != null)
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format(CultureInfo.InvariantCulture, "bingmaps:?collection=point.{0}_{1}_{2}", location.Latitude, location.Longitude, WebUtility.UrlEncode(title))), options);
            }
            else
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format(CultureInfo.InvariantCulture, "bingmaps:?collection=point.{0}_{1}", location.Latitude, location.Longitude)), options);
            }
        }

        public void OpenLiveLocation(MessageViewModel message)
        {
            NavigationService.Navigate(typeof(LiveLocationPage), message.ChatId);
        }

        public void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button)
        {
            KeyboardButtonExecute(message, button);
        }

        public void Call(MessageViewModel message, bool video)
        {
            CallCommand.Execute(video);
        }

        public async void VotePoll(MessageViewModel message, IList<PollOption> options)
        {
            var poll = message.Content as MessagePoll;
            if (poll == null)
            {
                return;
            }

            var ids = options.Select(x => poll.Poll.Options.IndexOf(x)).ToArray();
            if (ids.IsEmpty())
            {
                return;
            }

            await ProtoService.SendAsync(new SetPollAnswer(message.ChatId, message.Id, ids));

            var updated = message.Content as MessagePoll;
            if (updated.Poll.Type is PollTypeQuiz quiz)
            {
                if (quiz.CorrectOptionId == ids[0])
                {
                    Aggregator.Publish(new UpdateConfetti());
                }
                else
                {
                    var container = ListField?.ContainerFromItem(message) as SelectorItem;
                    var root = container.ContentTemplateRoot as FrameworkElement;

                    var bubble = root.FindName("Bubble") as FrameworkElement;
                    if (bubble == null)
                    {
                        return;
                    }

                    VisualUtilities.ShakeView(bubble);
                }
            }
        }


        public async void OpenUsername(string username)
        {
            var response = await ProtoService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat)
            {
                if (chat.Type is ChatTypePrivate privata)
                {
                    var user = ProtoService.GetUser(privata.UserId);
                    if (user?.Type is UserTypeBot)
                    {
                        NavigationService.NavigateToChat(chat);
                    }
                    else
                    {
                        NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
                else
                {
                    NavigationService.NavigateToChat(chat);
                }
            }
            else
            {
                await MessagePopup.ShowAsync(Strings.Resources.NoUsernameFound, Strings.Resources.AppName, Strings.Resources.OK);
            }
        }

        public async void OpenUser(int userId)
        {
            var response = await ProtoService.SendAsync(new CreatePrivateChat(userId, false));
            if (response is Chat chat)
            {
                var user = ProtoService.GetUser(userId);
                if (user?.Type is UserTypeBot)
                {
                    NavigationService.NavigateToChat(chat);
                }
                else
                {
                    NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                }
            }
        }

        public void OpenViaBot(int viaBotUserId)
        {
            var chat = Chat;
            if (chat != null && chat.Type is ChatTypeSupergroup super && super.IsChannel)
            {
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return;
                }
            }

            var user = ProtoService.GetUser(viaBotUserId);
            if (user != null)
            {
                SetText($"@{user.Username} ");
                ResolveInlineBot(user.Username);
            }
        }

        public void OpenChat(long chatId)
        {
            var chat = ProtoService.GetChat(chatId);
            if (chat == null)
            {
                return;
            }

            NavigationService.NavigateToChat(chat);
        }

        public void OpenChat(long chatId, long messageId)
        {
            var chat = ProtoService.GetChat(chatId);
            if (chat == null)
            {
                return;
            }

            NavigationService.NavigateToChat(chat, message: messageId);
        }

        public void OpenHashtag(string hashtag)
        {
            var search = Search = new ChatSearchViewModel(ProtoService, CacheService, Settings, Aggregator, this);
            search.Search(hashtag, null, null);
        }

        public async void OpenUrl(string url, bool untrust)
        {
            if (MessageHelper.TryCreateUri(url, out Uri uri))
            {
                if (MessageHelper.IsTelegramUrl(uri))
                {
                    MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, uri);
                }
                else
                {
                    //if (message?.Media is TLMessageMediaWebPage webpageMedia)
                    //{
                    //    if (webpageMedia.WebPage is TLWebPage webpage && webpage.HasCachedPage && webpage.Url.Equals(navigation))
                    //    {
                    //        var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                    //        if (service != null)
                    //        {
                    //            service.Navigate(typeof(InstantPage), webpageMedia);
                    //            return;
                    //        }
                    //    }
                    //}

                    if (untrust)
                    {
                        var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.OpenUrlAlert, url), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                        if (confirm != ContentDialogResult.Primary)
                        {
                            return;
                        }
                    }

                    try
                    {
                        await Windows.System.Launcher.LaunchUriAsync(uri);
                    }
                    catch { }
                }
            }
        }

        public void OpenBankCardNumber(string number)
        {
            //var response = await ProtoService.SendAsync(new GetBankCardInfo(number));
            //if (response is BankCardInfo info)
            //{
            //    var url = info.Actions.FirstOrDefault(x => x.)
            //}
        }

        public async void OpenMedia(MessageViewModel message, FrameworkElement target)
        {
            if (message.Content is MessagePoll poll)
            {
                await new PollResultsPopup(ProtoService, CacheService, Settings, Aggregator, this, message.ChatId, message.Id, poll.Poll).ShowQueuedAsync();
                return;
            }
            else if (message.Content is MessageGame game && message.ReplyMarkup is ReplyMarkupInlineKeyboard inline)
            {
                foreach (var row in inline.Rows)
                    foreach (var button in row)
                    {
                        if (button.Type is InlineKeyboardButtonTypeCallbackGame)
                        {
                            KeyboardButtonExecute(message, button);
                            return;
                        }
                    }
            }

            GalleryViewModelBase viewModel = null;

            var webPage = message.Content is MessageText text ? text.WebPage : null;
            if (webPage != null && webPage.IsInstantGallery())
            {
                viewModel = await InstantGalleryViewModel.CreateAsync(ProtoService, Aggregator, message, webPage);

                if (viewModel.Items.IsEmpty())
                {
                    viewModel = null;
                }
            }

            if (viewModel == null && (message.Content is MessageVideoNote || (webPage != null && webPage.VideoNote != null) || message.Content is MessageAnimation || (webPage != null && webPage.Animation != null)))
            {
                Delegate?.PlayMessage(message, target);
            }
            else
            {
                if (viewModel == null)
                {
                    if ((message.Content is MessageAnimation || message.Content is MessagePhoto || message.Content is MessageVideo) && !message.IsSecret())
                    {
                        viewModel = new ChatGalleryViewModel(ProtoService, Aggregator, message.ChatId, _threadId, message.Get());
                    }
                    else
                    {
                        viewModel = new SingleGalleryViewModel(ProtoService, Aggregator, new GalleryMessage(ProtoService, message.Get()));
                    }
                }

                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => target);
            }

            TextField?.Focus(FocusState.Programmatic);
        }

        public void PlayMessage(MessageViewModel message)
        {
            if ((message.Content is MessageVideoNote videoNote && !videoNote.IsViewed && !message.IsOutgoing) || (message.Content is MessageVoiceNote voiceNote && !voiceNote.IsListened && !message.IsOutgoing))
            {
                ProtoService.Send(new OpenMessageContent(message.ChatId, message.Id));
            }

            _playbackService.Enqueue(message.Get());
        }



        public async void SendBotCommand(string command)
        {
            await SendMessageAsync(command);
        }



        public bool IsAdmin(int userId)
        {
            var chat = _chat;
            if (chat == null)
            {
                return false;
            }

            if (_admins.TryGetValue(chat.Id, out IList<ChatAdministrator> value))
            {
                var admin = value.FirstOrDefault(x => x.UserId == userId);
                return admin != null;
            }

            return false;
        }

        public string GetAdminTitle(int userId)
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            if (_admins.TryGetValue(chat.Id, out IList<ChatAdministrator> value))
            {
                var admin = value.FirstOrDefault(x => x.UserId == userId);
                if (admin != null)
                {
                    if (string.IsNullOrEmpty(admin.CustomTitle))
                    {
                        if (admin.IsOwner)
                        {
                            return Strings.Resources.ChannelCreator;
                        }

                        return Strings.Resources.ChannelAdmin;
                    }

                    return admin.CustomTitle;
                }
            }

            return null;
        }
    }
}
