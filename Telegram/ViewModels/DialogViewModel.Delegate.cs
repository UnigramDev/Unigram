//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Gallery;
using Telegram.Converters;
using Telegram.Services.Updates;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Gallery;
using Telegram.Views;
using Telegram.Views.Popups;
using Windows.UI.Xaml;

namespace Telegram.ViewModels
{
    public partial class DialogViewModel
    {
        public void ViewVisibleMessages()
        {
            Delegate?.ViewVisibleMessages();
        }

        public void DoubleTapped(MessageViewModel message, bool alternate)
        {
            if (Settings.Appearance.IsQuickReplySelected || alternate)
            {
                if (alternate)
                {
                    ReactToMessage(message, ClientService.DefaultReaction);
                }
                else
                {
                    ReplyToMessage(message);
                }
            }
            else if (alternate)
            {
                ReplyToMessage(message);
            }
            else
            {
                ReactToMessage(message, ClientService.DefaultReaction);
            }
        }

        private void ReactToMessage(MessageViewModel message, ReactionType reaction)
        {
            if (message.InteractionInfo?.Reactions != null && message.InteractionInfo.Reactions.IsChosen(reaction))
            {
                ClientService.Send(new RemoveMessageReaction(message.ChatId, message.Id, reaction));
            }
            else
            {
                ClientService.Send(new AddMessageReaction(message.ChatId, message.Id, reaction, false, false));
            }
        }

        public async void OpenReply(MessageViewModel message)
        {
            if (message.ReplyToState != MessageReplyToState.Deleted)
            {
                if (message.ReplyTo is MessageReplyToMessage replyToMessage)
                {
                    if (replyToMessage.ChatId != message.ChatId && ClientService.TryGetChat(replyToMessage.ChatId, out Chat replyToChat))
                    {
                        if (ClientService.TryGetSupergroup(replyToChat, out Supergroup supergroup))
                        {
                            if (supergroup.Status is ChatMemberStatusLeft && !supergroup.IsPublic() && !ClientService.IsChatAccessible(replyToChat))
                            {
                                if (supergroup.IsChannel)
                                {
                                    ToastPopup.Show(replyToMessage.Quote != null && replyToMessage.Quote.IsManual
                                        ? Strings.QuotePrivateChannel
                                        : Strings.ReplyPrivateChannel, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                                }
                                else
                                {
                                    ToastPopup.Show(replyToMessage.Quote != null && replyToMessage.Quote.IsManual
                                        ? Strings.QuotePrivateGroup
                                        : Strings.ReplyPrivateGroup, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                                }

                                return;
                            }
                        }
                        else if (replyToMessage.MessageId == 0)
                        {
                            ToastPopup.Show(replyToMessage.Quote != null && replyToMessage.Quote.IsManual
                                        ? Strings.QuotePrivate
                                        : Strings.ReplyPrivate, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                            return;
                        }

                        NavigationService.NavigateToChat(replyToChat, replyToMessage.MessageId);
                    }
                    else if (replyToMessage.Origin != null && replyToMessage.MessageId == 0)
                    {
                        ToastPopup.Show(replyToMessage.Quote != null && replyToMessage.Quote.IsManual
                                        ? Strings.QuotePrivate
                                        : Strings.ReplyPrivate, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                    }
                    else if (replyToMessage.ChatId == message.ChatId || replyToMessage.ChatId == 0)
                    {
                        await LoadMessageSliceAsync(message.Id, replyToMessage.MessageId, highlight: replyToMessage.Quote);
                    }
                }
            }
        }

        public async void OpenThread(MessageViewModel message)
        {
            long chatId = message.ChatId;
            long threadId = message.Id;

            long? messageId = null;

            if (message.ChatId == ClientService.Options.RepliesBotChatId)
            {
                // TODO: 172 is this correct?
                if (message.ForwardInfo?.Origin is MessageOriginUser or MessageOriginChat && message.ForwardInfo?.Source != null)
                {
                    chatId = message.ForwardInfo.Source.ChatId;
                    threadId = message.ForwardInfo.Source.MessageId;

                    messageId = threadId;
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel)
                {
                    chatId = fromChannel.ChatId;
                    threadId = fromChannel.MessageId;

                    messageId = threadId;
                }

                var original = await ClientService.SendAsync(new GetMessage(chatId, threadId)) as Message;
                if (original == null || !original.CanGetMessageThread)
                {
                    NavigationService.NavigateToChat(chatId, threadId);
                    return;
                }
            }

            var response = await ClientService.SendAsync(new GetMessageThread(chatId, threadId));
            if (response is MessageThreadInfo)
            {
                NavigationService.NavigateToChat(chatId, messageId, thread: threadId);
            }
        }



        public void OpenWebPage(WebPage webPage)
        {
            if (webPage.InstantViewVersion != 0)
            {
                NavigationService.NavigateToInstant(webPage.Url);
            }
            else
            {
                MessageHelper.OpenUrl(ClientService, NavigationService, webPage.Url, !webPage.SkipConfirmation, new OpenUrlSourceChat(_chat.Id));
            }
        }

        public async void OpenSticker(Sticker sticker)
        {
            if (sticker.SetId != 0)
            {
                await StickersPopup.ShowAsync(sticker.SetId, Sticker_Click);
            }
        }

        public async void OpenGame(MessageViewModel message)
        {
            if (_chat is not Chat chat)
            {
                return;
            }

            var game = message.Content as MessageGame;
            if (game == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetCallbackQueryAnswer(chat.Id, message.Id, new CallbackQueryPayloadGame(game.Game.ShortName)));
            if (response is CallbackQueryAnswer answer && !string.IsNullOrEmpty(answer.Url))
            {
                ChatActionManager.SetTyping(new ChatActionStartPlayingGame());

                var viaBot = message.GetViaBotUser();
                if (viaBot != null && viaBot.HasActiveUsername(out string username))
                {
                    NavigationService.Navigate(typeof(GamePage), new GameConfiguration(message, answer.Url, game.Game.Title, username));
                }
                else
                {
                    NavigationService.Navigate(typeof(GamePage), new GameConfiguration(message, answer.Url, game.Game.Title, string.Empty));
                }
            }
        }

        public void Call(MessageViewModel message, bool video)
        {
            Call(video);
        }

        public async void VotePoll(MessageViewModel message, IList<int> options)
        {
            var poll = message.Content as MessagePoll;
            if (poll == null || options == null)
            {
                return;
            }

            await ClientService.SendAsync(new SetPollAnswer(message.ChatId, message.Id, options));

            var updated = message.Content as MessagePoll;
            if (updated.Poll.Type is PollTypeQuiz quiz)
            {
                if (quiz.CorrectOptionId == options[0])
                {
                    Aggregator.Publish(new UpdateConfetti());
                }
                else
                {
                    Delegate?.UpdateBubbleWithMessageId(message.Id, bubble => VisualUtilities.ShakeView(bubble));
                }
            }
        }



        public void OpenUser(long userId)
        {
            _messageDelegate.OpenUser(userId);
        }

        public void OpenViaBot(long viaBotUserId)
        {
            var chat = Chat;
            if (chat != null && chat.Type is ChatTypeSupergroup super && super.IsChannel)
            {
                var supergroup = ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return;
                }
            }

            var user = ClientService.GetUser(viaBotUserId);
            if (user != null && user.HasActiveUsername(out string username))
            {
                SetText($"@{username} ");
                ResolveInlineBot(username);
            }
        }

        public void OpenChat(long chatId, bool profile = false)
        {
            var chat = ClientService.GetChat(chatId);
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
            var chat = ClientService.GetChat(chatId);
            if (chat == null)
            {
                return;
            }

            NavigationService.NavigateToChat(chat, message: messageId);
        }

        public void OpenHashtag(string hashtag)
        {
            Search = new ChatSearchViewModel(ClientService, Settings, Aggregator, this, hashtag);
        }

        public void OpenUrl(string url, bool untrust)
        {
            _messageDelegate.OpenUrl(url, untrust);
        }

        public async void OpenMedia(MessageViewModel message, FrameworkElement target, int timestamp = 0)
        {
            if (message.Content is MessageAudio or MessageVoiceNote)
            {
                _playbackService.Play(message, ThreadId, SavedMessagesTopicId);

                if (timestamp > 0)
                {
                    _playbackService.Seek(TimeSpan.FromSeconds(timestamp));
                }
            }
            else if (message.Content is MessagePoll poll)
            {
                await ShowPopupAsync(new PollResultsPopup(ClientService, Settings, Aggregator, _messageDelegate, message.ChatId, message.Id, poll.Poll));
            }
            else if (message.Content is MessageGame game && message.ReplyMarkup is ReplyMarkupInlineKeyboard inline)
            {
                foreach (var row in inline.Rows)
                {
                    foreach (var button in row)
                    {
                        if (button.Type is InlineKeyboardButtonTypeCallbackGame)
                        {
                            OpenInlineButton(message, button);
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
                    viewModel = await InstantGalleryViewModel.CreateAsync(ClientService, StorageService, Aggregator, message, webPage);
                }

                if (viewModel == null && (message.Content is MessageAnimation || webPage?.Animation != null))
                {
                    Delegate?.PlayMessage(message, target);
                }
                else
                {
                    if (viewModel == null)
                    {
                        static bool IsSingle(MessageContent content)
                        {
                            return content switch
                            {
                                MessagePhoto photo => photo.IsSecret,
                                MessageVideo video => video.IsSecret,
                                MessageVideoNote videoNote => videoNote.IsSecret,
                                _ => true
                            };
                        }

                        if (IsSingle(message.Content))
                        {
                            viewModel = new SingleGalleryViewModel(ClientService, _storageService, Aggregator, new GalleryMessage(ClientService, message));
                        }
                        else
                        {
                            viewModel = new ChatGalleryViewModel(ClientService, _storageService, Aggregator, message.ChatId, ThreadId, SavedMessagesTopicId, message);
                        }
                    }

                    viewModel.NavigationService = NavigationService;
                    await GalleryWindow.ShowAsync(viewModel, target != null ? () => target : null, timestamp);
                }

                TextField?.Focus(FocusState.Programmatic);
            }
        }

        public void PlayMessage(MessageViewModel message)
        {
            _playbackService.Play(message, ThreadId, SavedMessagesTopicId);
        }

        public bool RecognizeSpeech(MessageViewModel message)
        {
            if (ClientService.IsPremium)
            {
                _needsUpdateSpeechRecognitionTrial = false;
                ClientService.Send(new RecognizeSpeech(message.ChatId, message.Id));

                return true;
            }
            else if (ClientService.SpeechRecognitionTrial.LeftCount > 0)
            {
                _needsUpdateSpeechRecognitionTrial = true;
                ClientService.Send(new RecognizeSpeech(message.ChatId, message.Id));

                return true;
            }
            else if (ClientService.SpeechRecognitionTrial.WeeklyCount > 0)
            {
                ShowSpeechRecognitionTrial(3);
            }
            else
            {
                ShowSpeechRecognitionTrial(0);
            }

            return false;
        }

        private void ShowSpeechRecognitionTrial(int type)
        {
            _needsUpdateSpeechRecognitionTrial = false;

            var trial = ClientService.SpeechRecognitionTrial;
            var builder = new StringBuilder();

            if (type == 0)
            {
                // TODO: generic error
            }
            else if (type == 1)
            {
                if (trial.NextResetDate > 0)
                {
                    builder.Append(Locale.Declension(Strings.R.TranscriptionTrialLeftUntil, trial.LeftCount, Formatter.DateAt(trial.NextResetDate)));
                }
                else
                {
                    builder.Append(Locale.Declension(Strings.R.TranscriptionTrialLeft, trial.LeftCount));
                }
            }
            else if (type == 2 || type == 3)
            {
                builder.Append(Locale.Declension(Strings.R.TranscriptionTrialEnd, trial.WeeklyCount));

                if (type == 2)
                {
                    builder.Append(" ");
                    builder.Append(Strings.TranscriptionTrialEndBuy);
                }
                else if (trial.NextResetDate > 0)
                {
                    builder.Append(" ");
                    builder.Append(string.Format(Strings.TranscriptionTrialEndWaitOrBuy, Formatter.DateAt(trial.NextResetDate)));
                }

                var text = builder.ToString();
                var markdown = Extensions.ReplacePremiumLink(text, new PremiumFeatureVoiceRecognition());

                ToastPopup.Show(markdown, new LocalFileSource("ms-appx:///Assets/Toasts/Transcribe.tgs"));
            }
        }

        public async void SendBotCommand(string command)
        {
            await SendMessageAsync(null, new InputMessageText(new FormattedText(command, Array.Empty<TextEntity>()), null, false), new MessageSendOptions
            {
                SendingId = int.MaxValue
            });
        }



        public void Select(MessageViewModel message)
        {
            if (message.IsService)
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

            RaisePropertyChanged(nameof(CanForwardSelectedMessages));
            RaisePropertyChanged(nameof(CanDeleteSelectedMessages));
            RaisePropertyChanged(nameof(CanCopySelectedMessage));
            RaisePropertyChanged(nameof(CanReportSelectedMessages));

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

            RaisePropertyChanged(nameof(CanForwardSelectedMessages));
            RaisePropertyChanged(nameof(CanDeleteSelectedMessages));
            RaisePropertyChanged(nameof(CanCopySelectedMessage));
            RaisePropertyChanged(nameof(CanReportSelectedMessages));

            RaisePropertyChanged(nameof(SelectedCount));
        }
    }
}
