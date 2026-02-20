//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Messages.Content;
using Telegram.Controls.Stories;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Gallery;
using Telegram.ViewModels.Stories;
using Telegram.Views;
using Telegram.Views.Popups;
using Windows.Foundation;
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
            if (message.ReplyToState == MessageReplyToState.Deleted || message.ReplyTo is not MessageReplyToMessage replyToMessage)
            {
                return;
            }

            if (replyToMessage.ChatId != message.ChatId && ClientService.TryGetChat(replyToMessage.ChatId, out Chat replyToChat))
            {
                if (ClientService.TryGetSupergroup(replyToChat, out Supergroup supergroup))
                {
                    if (supergroup.Status is ChatMemberStatusLeft && !supergroup.IsPublic() && !ClientService.IsChatAccessible(replyToChat))
                    {
                        if (supergroup.IsChannel)
                        {
                            ShowToast(replyToMessage.Quote != null && replyToMessage.Quote.IsManual
                                ? Strings.QuotePrivateChannel
                                : Strings.ReplyPrivateChannel, ToastPopupIcon.Info);
                        }
                        else
                        {
                            ShowToast(replyToMessage.Quote != null && replyToMessage.Quote.IsManual
                                ? Strings.QuotePrivateGroup
                                : Strings.ReplyPrivateGroup, ToastPopupIcon.Info);
                        }

                        return;
                    }
                }
                else if (replyToMessage.MessageId == 0)
                {
                    ShowToast(replyToMessage.Quote != null && replyToMessage.Quote.IsManual
                        ? Strings.QuotePrivate
                        : Strings.ReplyPrivate, ToastPopupIcon.Info);
                    return;
                }

                long chatId = replyToChat.Id;
                long messageId = replyToMessage.MessageId;

                MessageTopic messageTopic = null;

                if (message.ChatId == ClientService.Options.RepliesBotChatId)
                {
                    // TODO: 172 is this correct?
                    if (message.ForwardInfo?.Origin is MessageOriginUser or MessageOriginChat && message.ForwardInfo?.Source != null)
                    {
                        chatId = message.ForwardInfo.Source.ChatId;
                        messageTopic = new MessageTopicThread(message.ForwardInfo.Source.MessageId);
                    }
                    else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel)
                    {
                        chatId = fromChannel.ChatId;
                        messageTopic = new MessageTopicThread(fromChannel.MessageId);
                    }

                    if (messageTopic is MessageTopicThread messageTopicThread)
                    {
                        await ClientService.SendAsync(new GetMessage(chatId, messageTopicThread.MessageThreadId));

                        var response = await ClientService.SendAsync(new GetMessageThread(chatId, messageTopicThread.MessageThreadId));
                        if (response is not MessageThreadInfo)
                        {
                            return;
                        }
                    }
                }

                NavigationService.NavigateToChat(chatId, messageId, topic: messageTopic, state: new NavigationState { { "highlight", replyToMessage.Quote }, { "checklist_task_id", replyToMessage.ChecklistTaskId } });
            }
            else if (replyToMessage.Origin != null && replyToMessage.MessageId == 0)
            {
                ShowToast(replyToMessage.Quote != null && replyToMessage.Quote.IsManual
                    ? Strings.QuotePrivate
                    : Strings.ReplyPrivate, ToastPopupIcon.Info);
            }
            else if (replyToMessage.ChatId == message.ChatId || replyToMessage.ChatId == 0)
            {
                await LoadMessageSliceAsync(message.Id, replyToMessage.MessageId, highlight: replyToMessage.Quote, checklistTaskId: replyToMessage.ChecklistTaskId);
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

                await ClientService.SendAsync(new GetMessage(chatId, threadId));

                var properties = await ClientService.SendAsync(new GetMessageProperties(chatId, threadId)) as MessageProperties;
                if (properties == null || !properties.CanGetMessageThread)
                {
                    NavigationService.NavigateToChat(chatId, threadId);
                    return;
                }
            }

            var response = await ClientService.SendAsync(new GetMessageThread(chatId, threadId));
            if (response is MessageThreadInfo)
            {
                NavigationService.NavigateToChat(chatId, messageId, topic: new MessageTopicThread(threadId));
            }
        }

        public bool IsAdministrator(MessageSender memberId) => _messageDelegate.IsAdministrator(memberId);

        public void OpenWebPage(MessageViewModel message)
        {
            if (message.Content is not MessageText text)
            {
                return;
            }

            if (text.LinkPreview?.InstantViewVersion != 0)
            {
                var url = text.LinkPreview.Url;

                foreach (var entity in text.Text.Entities)
                {
                    string compare;

                    if (entity.Type is TextEntityTypeUrl)
                    {
                        compare = text.Text.Text.Substring(entity.Offset, entity.Length);
                    }
                    else if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        compare = textUrl.Url;
                    }
                    else
                    {
                        continue;
                    }

                    if (MessageHelper.AreTheSame(url, compare, out _))
                    {
                        url = compare;
                        break;
                    }
                }

                NavigationService.NavigateToInstant(url);
            }
            else if (text.LinkPreview != null)
            {
                MessageHelper.OpenUrl(ClientService, NavigationService, text.LinkPreview.Url, !text.LinkPreview.SkipConfirmation, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
        }

        public async void OpenSticker(Sticker sticker)
        {
            if (sticker.SetId != 0)
            {
                await StickersPopup.ShowAsync(NavigationService, sticker.SetId);
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
                if (viaBot != null)
                {
                    NavigationService.NavigateToWebApp(viaBot, answer.Url, game.Game.Title, message.ChatId, message.Id);
                }
            }
        }

        public void Call(MessageViewModel message, bool video)
        {
            if (message.Content is MessageGroupCall groupCall)
            {
                _voipService.JoinGroupCall(NavigationService, new InputGroupCallMessage(message.ChatId, message.Id));
            }
            else
            {
                Call(video);
            }
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
                    Delegate?.UpdateBubbleWithMessageId(message.Id, bubble =>
                    {
                        if (bubble.MediaTemplateRoot is PollContent pollContent)
                        {
                            pollContent.ShowExplanation();
                        }

                        VisualUtilities.ShakeView(bubble);
                    });
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
                SetText($"@{username} ", focus: true);
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
            Search = new ChatSearchViewModel(ClientService, NavigationService, Settings, Aggregator, this, hashtag, null);
        }

        public void OpenUrl(string url, bool untrust, OpenUrlSource source = null)
        {
            _messageDelegate.OpenUrl(url, untrust, source);
        }

        public async void OpenMedia(MessageViewModel message, FrameworkElement target, double timestamp = 0)
        {
            if (message.Content is MessageAudio or MessageVoiceNote)
            {
                LifetimeService.Current.Playback.Play(XamlRoot, message, TopicId);

                if (timestamp > 0)
                {
                    LifetimeService.Current.Playback.Seek(TimeSpan.FromSeconds(timestamp));
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
            else if (message.Content is MessageAsyncStory story && story.Story != null)
            {
                Rect GetOrigin(ActiveStoriesViewModel activeStories)
                {
                    var transform = target.TransformToVisual(null);
                    var point = transform.TransformPoint(new Point());

                    return new Rect(point.X, point.Y, target.ActualWidth, target.ActualHeight);
                }

                var transform = target.TransformToVisual(null);

                var point = transform.TransformPoint(new Point());
                var origin = new Rect(point.X, point.Y, target.ActualWidth, target.ActualHeight);

                var activeStories = new ActiveStoriesViewModel(ClientService, Settings, Aggregator, story.Story);
                var viewModel = StoryListViewModel.Create(NavigationService, activeStories);

                var window = new StoriesWindow();
                window.Update(viewModel, activeStories, StoryOpenOrigin.Card, origin, GetOrigin);
                _ = window.ShowAsync(XamlRoot);
            }
            else
            {
                GalleryViewModelBase viewModel = null;

                var linkPreview = message.Content is MessageText text ? text.LinkPreview : null;
                if (linkPreview != null && linkPreview.Type is LinkPreviewTypeAlbum album)
                {
                    viewModel = InstantGalleryViewModel.Create(ClientService, StorageService, Aggregator, message, album);
                }

                if (viewModel == null && (message.Content is MessageAnimation || linkPreview?.Type is LinkPreviewTypeAnimation))
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
                                MessageDocument => false,
                                _ => true
                            };
                        }

                        var properties = await ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id)) as MessageProperties;
                        if (properties == null && Type != DialogType.EventLog)
                        {
                            return;
                        }

                        if (Type == DialogType.EventLog || IsSingle(message.Content))
                        {
                            viewModel = new StandaloneGalleryViewModel(ClientService, _storageService, Aggregator, new GalleryMessage(ClientService, message, properties));
                        }
                        else
                        {
                            viewModel = new ChatGalleryViewModel(ClientService, _storageService, Aggregator, message.ChatId, TopicId, message, properties);
                        }
                    }

                    NavigationService.ShowGallery(viewModel, target, timestamp);
                }

                //TextField?.Focus(FocusState.Programmatic);
            }
        }

        public void OpenPaidMedia(MessageViewModel message, PaidMedia media, FrameworkElement target, double timestamp = 0)
        {
            if (message.Content is MessagePaidAlbum album)
            {
                GalleryMedia item = null;
                GalleryMedia Filter(PaidMedia x)
                {
                    GalleryMedia result = null;
                    if (x is PaidMediaPhoto photo)
                    {
                        result = new GalleryPhoto(ClientService, photo.Photo, null, true);
                    }
                    else if (x is PaidMediaVideo video)
                    {
                        result = new GalleryVideo(ClientService, video.Video, null, true);
                    }

                    if (x == media)
                    {
                        item = result;
                    }

                    return result;
                }

                var items = album.Media
                    .Select(Filter)
                    .Where(x => x is not null)
                    .ToList();

                var viewModel = new StandaloneGalleryViewModel(ClientService, StorageService, Aggregator, items, item);
                NavigationService.ShowGallery(viewModel, target, timestamp);

                //TextField?.Focus(FocusState.Programmatic);
            }
        }

        public void PlayMessage(MessageViewModel message)
        {
            LifetimeService.Current.Playback.Play(XamlRoot, message, TopicId);
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

                ToastPopup.Show(XamlRoot, markdown, ToastPopupIcon.Transcribe);
            }
        }

        public void SendBotCommand(string command)
        {
            _ = SendMessageAsync(null, new InputMessageText(command.AsFormattedText(false), null, false), new MessageSendOptions
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

            UpdateSelectionState();
        }

        public void Unselect(MessageViewModel message, bool updateSelection = false)
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

            if (updateSelection && SelectedItems.Count < 1 && IsReportingMessages == null)
            {
                IsSelectionEnabled = false;
            }

            UpdateSelectionState();
        }

        private async void UpdateSelectionState()
        {
            RaisePropertyChanged(nameof(CanCopySelectedMessage));
            RaisePropertyChanged(nameof(CanReportSelectedMessages));

            RaisePropertyChanged(nameof(SelectedCount));

            if (Type is DialogType.BusinessReplies)
            {
                CanDeleteSelectedMessages = true;
                CanForwardSelectedMessages = false;
            }
            else
            {
                var selectedItems = SelectedItems.Values.ToList();
                var properties = await ClientService.GetMessagePropertiesAsync(selectedItems.Select(x => new MessageId(x)));

                CanDeleteSelectedMessages = properties.Count > 0 && properties.Values.All(x => x.CanBeDeletedForAllUsers || x.CanBeDeletedOnlyForSelf);
                CanForwardSelectedMessages = properties.Count > 0 && properties.Values.All(x => x.CanBeForwarded);
            }
        }
    }
}
