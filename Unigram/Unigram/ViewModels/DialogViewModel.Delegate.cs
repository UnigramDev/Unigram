using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Gallery;
using Unigram.Controls.Messages;
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
        public bool CanBeDownloaded(object content, File file)
        {
            var chat = _chat;
            if (chat == null || ProtoService.IsDownloadFileCanceled(file.Id))
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
            else if (content is Sticker)
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
            else if (content is VoiceNote)
            {
                return !Settings.AutoDownload.Disabled;
            }

            return false;
        }

        private AutoDownloadChat GetChatType(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel || chat.Type is ChatTypeBasicGroup)
            {
                return AutoDownloadChat.Group;
            }
            else if (chat.Type is ChatTypePrivate or ChatTypeSecret)
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
            ProtoService.DownloadFile(file.Id, 32);
        }



        public void ViewVisibleMessages(bool intermediate)
        {
            Delegate?.ViewVisibleMessages(intermediate);
        }

        public void ReplyToMessage(MessageViewModel message)
        {
            if (Settings.Appearance.IsQuickReplySelected)
            {
                MessageReplyCommand.Execute(message);
            }
            else
            {
                ProtoService.SendAsync(new SetMessageReaction(message.ChatId, message.Id, CacheService.DefaultReaction, false, false));
            }
        }

        public void ForwardMessage(MessageViewModel message)
        {
            MessageForwardCommand.Execute(message);
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
            long chatId = message.ChatId;
            long threadId = message.Id;

            long? messageId = null;

            if (message.ChatId == CacheService.Options.RepliesBotChatId)
            {
                if (message.ForwardInfo?.Origin is MessageForwardOriginUser or MessageForwardOriginChat)
                {
                    chatId = message.ForwardInfo.FromChatId;
                    threadId = message.ForwardInfo.FromMessageId;

                    messageId = threadId;
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel)
                {
                    chatId = fromChannel.ChatId;
                    threadId = fromChannel.MessageId;

                    messageId = threadId;
                }

                var original = await ProtoService.SendAsync(new GetMessage(chatId, threadId)) as Message;
                if (original == null || !original.CanGetMessageThread)
                {
                    NavigationService.NavigateToChat(chatId, threadId);
                    return;
                }
            }

            var response = await ProtoService.SendAsync(new GetMessageThread(chatId, threadId));
            if (response is MessageThreadInfo)
            {
                NavigationService.NavigateToThread(chatId, threadId, messageId);
            }
        }



        public async void OpenFile(File file)
        {
            var local = await ProtoService.GetFileAsync(file);
            if (local != null)
            {
                if (file.Local.Path.EndsWith(".unigram-theme"))
                {
                    await new ThemePreviewPopup(local).ShowQueuedAsync();
                }
                else
                {
                    await Windows.System.Launcher.LaunchFileAsync(local);
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
                     string.Equals(webPage.Type, "telegram_message", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase)))
            {
                MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, uri);
            }
        }

        public async void OpenSticker(Sticker sticker)
        {
            if (sticker.SetId != 0)
            {
                await StickersPopup.ShowAsync(sticker.SetId, Sticker_Click);
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
            //NavigationService.Navigate(typeof(LiveLocationPage), message.ChatId);
        }

        public void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button)
        {
            KeyboardButtonInline(message, button);
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
                    var root = container?.ContentTemplateRoot as MessageSelector;

                    var bubble = root?.Content as MessageBubble;
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

        public async void OpenUser(long userId)
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

        public void OpenViaBot(long viaBotUserId)
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

        public void OpenChat(long chatId, bool profile = false)
        {
            var chat = ProtoService.GetChat(chatId);
            if (chat == null)
            {
                return;
            }

            if (profile)
            {
                NavigationService.Navigate(typeof(ProfilePage), chat.Id);
            }
            else
            {
                NavigationService.NavigateToChat(chat);
            }
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

        public async void OpenMedia(MessageViewModel message, FrameworkElement target, int timestamp = 0)
        {
            if (message.Content is MessageAudio or MessageVoiceNote)
            {
                _playbackService.Play(message, _threadId);

                if (timestamp > 0)
                {
                    _playbackService.Seek(TimeSpan.FromSeconds(timestamp));
                }
            }
            else if (message.Content is MessagePoll poll)
            {
                await new PollResultsPopup(ProtoService, CacheService, Settings, Aggregator, this, message.ChatId, message.Id, poll.Poll).ShowQueuedAsync();
            }
            else if (message.Content is MessageGame game && message.ReplyMarkup is ReplyMarkupInlineKeyboard inline)
            {
                foreach (var row in inline.Rows)
                {
                    foreach (var button in row)
                    {
                        if (button.Type is InlineKeyboardButtonTypeCallbackGame)
                        {
                            KeyboardButtonInline(message, button);
                        }
                    }
                }
            }
            else
            {
                GalleryViewModelBase viewModel = null;

                var webPage = message.Content is MessageText text ? text.WebPage : null;
                if (webPage != null && webPage.IsInstantGallery())
                {
                    viewModel = await InstantGalleryViewModel.CreateAsync(ProtoService, StorageService, Aggregator, message, webPage);
                }

                if (viewModel == null && (message.Content is MessageAnimation || webPage?.Animation != null))
                {
                    Delegate?.PlayMessage(message, target);
                }
                else
                {
                    if (viewModel == null)
                    {
                        if (message.Content is MessageVideoNote or MessagePhoto or MessageVideo && !message.IsSecret())
                        {
                            viewModel = new ChatGalleryViewModel(ProtoService, _storageService, Aggregator, message.ChatId, _threadId, message.Get());
                        }
                        else
                        {
                            viewModel = new SingleGalleryViewModel(ProtoService, _storageService, Aggregator, new GalleryMessage(ProtoService, message.Get()));
                        }
                    }

                    await GalleryView.ShowAsync(viewModel, target != null ? () => target : null, timestamp);
                }

                TextField?.Focus(FocusState.Programmatic);
            }
        }

        public void PlayMessage(MessageViewModel message)
        {
            _playbackService.Play(message, _threadId);
        }



        public async void SendBotCommand(string command)
        {
            await SendMessageAsync(command);
        }



        public string GetAdminTitle(MessageViewModel message)
        {
            if (message.SenderId is MessageSenderUser senderUser)
            {
                return GetAdminTitle(senderUser.UserId);
            }
            else if (message.SenderId is MessageSenderChat && !message.IsChannelPost)
            {
                return message.AuthorSignature.Length > 0 ? message.AuthorSignature : null;
            }

            return null;
        }

        public string GetAdminTitle(long userId)
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

        public void Select(MessageViewModel message)
        {
            if (message.IsService())
            {
                return;
            }

            if (message.MediaAlbumId != 0)
            {
                if (message.Content is MessageAlbum album)
                {
                    foreach (var child in album.Messages)
                    {
                        _selectedItems[child.Id] = child;
                        child.SelectionChanged();
                    }

                    message.SelectionChanged();
                }
                else if (_groupedMessages.TryGetValue(message.MediaAlbumId, out MessageViewModel group))
                {
                    _selectedItems[message.Id] = message;
                    message.SelectionChanged();
                    group.SelectionChanged();
                }
            }
            else
            {
                _selectedItems[message.Id] = message;
                message.SelectionChanged();
            }

            MessagesForwardCommand.RaiseCanExecuteChanged();
            MessagesDeleteCommand.RaiseCanExecuteChanged();
            MessagesCopyCommand.RaiseCanExecuteChanged();
            MessagesReportCommand.RaiseCanExecuteChanged();

            RaisePropertyChanged(nameof(SelectedCount));
        }

        public void Unselect(MessageViewModel message)
        {
            if (message.MediaAlbumId != 0)
            {
                if (message.Content is MessageAlbum album)
                {
                    foreach (var child in album.Messages)
                    {
                        _selectedItems.TryRemove(child.Id, out _);
                        child.SelectionChanged();
                    }

                    message.SelectionChanged();
                }
                else if (_groupedMessages.TryGetValue(message.MediaAlbumId, out MessageViewModel group))
                {
                    _selectedItems.TryRemove(message.Id, out _);
                    message.SelectionChanged();
                    group.SelectionChanged();
                }
            }
            else
            {
                _selectedItems.TryRemove(message.Id, out _);
                message.SelectionChanged();
            }

            MessagesForwardCommand.RaiseCanExecuteChanged();
            MessagesDeleteCommand.RaiseCanExecuteChanged();
            MessagesCopyCommand.RaiseCanExecuteChanged();
            MessagesReportCommand.RaiseCanExecuteChanged();

            RaisePropertyChanged(nameof(SelectedCount));
        }
    }
}
