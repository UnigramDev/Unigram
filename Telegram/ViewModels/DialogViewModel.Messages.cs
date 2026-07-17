//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Telegram.Views.Chats;
using Telegram.Views.Create;
using Telegram.Views.Popups;
using Telegram.Views.Settings.Popups;
using Telegram.Views.Stars.Popups;
using Telegram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public partial class MessageQuote
    {
        public MessageViewModel Message { get; set; }

        public FormattedText Quote { get; set; }

        public int Position { get; set; }

        public MessageQuote()
        {

        }

        public MessageQuote(MessageComposerReplyTo reply)
        {
            Message = reply.Message;
            Quote = reply.Quote.Text;
            Position = reply.Quote.Position;
        }

        public InputTextQuote ToInput()
        {
            return new InputTextQuote(Quote, Position);
        }
    }

    public partial class MessageChecklistTask
    {
        public MessageViewModel Message { get; set; }

        public ChecklistTask Task { get; set; }

        public MessageChecklistTask(MessageViewModel message, ChecklistTask task)
        {
            Message = message;
            Task = task;
        }

        public MessageChecklistTask(MessageComposerReplyTo reply)
        {
            Message = reply.Message;

            if (reply.Message.Content is MessageChecklist checklist)
            {
                Task = checklist.List.Tasks.FirstOrDefault(x => x.Id == reply.ChecklistTaskId);
            }
        }
    }

    public partial class MessagePollOption
    {
        public MessageViewModel Message { get; set; }

        public PollOption Option { get; set; }

        public MessagePollOption(MessageViewModel message, PollOption option)
        {
            Message = message;
            Option = option;
        }

        public MessagePollOption(MessageComposerReplyTo reply)
        {
            Message = reply.Message;

            if (reply.Message.Content is MessagePoll poll)
            {
                Option = poll.Poll.Options.FirstOrDefault(x => x.Id == reply.PollOptionId);
            }
        }
    }

    public partial class DialogViewModel
    {
        #region Reply

        public void MessageReplyPrevious()
        {
            MessageViewModel last = null;

            var data = _composerHeader;
            if (data != null && data.ReplyTo != null)
            {
                last = Items.Reverse().FirstOrDefault(x => x.Id != 0 && x.Id < data.ReplyTo.Message.Id) ?? Items.LastOrDefault();
            }
            else
            {
                last = Items.LastOrDefault();
            }

            if (last != null)
            {
                ReplyToMessage(last);
                HistoryField?.ScrollToItem(last, VerticalAlignment.Center, new MessageBubbleHighlightOptions(false));
            }
        }

        public void MessageReplyNext()
        {
            MessageViewModel last = null;

            var data = _composerHeader;
            if (data != null && data.ReplyTo != null)
            {
                last = Items.FirstOrDefault(x => x.Id != 0 && x.Id > data.ReplyTo.Message.Id);
            }

            if (last != null)
            {
                ReplyToMessage(last);
                HistoryField?.ScrollToItem(last, VerticalAlignment.Center, new MessageBubbleHighlightOptions(false));
            }
            else
            {
                ClearReply();
            }
        }

        public void ReplyToMessage(MessageViewModel message)
        {
            ReplyToMessage(message, false);
        }

        public void ReplyToMessageInAnotherChat(MessageViewModel message)
        {
            ReplyToMessage(message, true);
        }

        public async void ReplyToMessage(MessageViewModel message, bool inAnotherChat)
        {
            DisposeSearch();

            if (message == null)
            {
                return;
            }

            if (message.Content is MessageAlbum album)
            {
                message = album.Messages.FirstOrDefault();
            }

            var should = await ShouldReplyInAnotherChatAsync(message);
            if (should == ShouldReplyInAnotherChatResult.True || inAnotherChat)
            {
                var header = ComposerHeader;
                var text = GetFormattedText(true, false);

                GetReply(true);

                var confirm = await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationReplyToMessage(message));
                if (confirm != ContentDialogResult.Primary)
                {
                    ComposerHeader = header;
                    SetFormattedText(text);
                }
            }
            else
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    ReplyTo = new MessageComposerReplyTo(message, null, 0, string.Empty, should != ShouldReplyInAnotherChatResult.NotAvailable)
                };

                TextField?.Focus(FocusState.Keyboard);
            }
        }

        public void QuoteToMessage(MessageQuote quote)
        {
            QuoteToMessage(quote, false);
        }

        public void QuoteToMessageInAnotherChat(MessageQuote quote)
        {
            QuoteToMessage(quote, true);
        }

        public async void QuoteToMessage(MessageQuote quote, bool inAnotherChat)
        {
            DisposeSearch();

            var message = quote?.Message;
            if (message == null)
            {
                return;
            }

            if (message.Content is MessageAlbum album)
            {
                message = album.Messages[album.IsMedia ? 0 : ^1];
            }

            var should = await ShouldReplyInAnotherChatAsync(message);
            if (should == ShouldReplyInAnotherChatResult.True || inAnotherChat)
            {
                var header = ComposerHeader;
                var text = GetFormattedText(true, false);

                GetReply(true);

                var confirm = await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationReplyToMessage(message, quote.ToInput()));
                if (confirm != ContentDialogResult.Primary)
                {
                    ComposerHeader = header;
                    SetFormattedText(text);
                }
            }
            else
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    ReplyTo = new MessageComposerReplyTo(message, quote.ToInput(), 0, string.Empty, should != ShouldReplyInAnotherChatResult.NotAvailable)
                };

                TextField?.Focus(FocusState.Keyboard);
            }
        }

        public void ReplyToChecklistTask(MessageChecklistTask message)
        {
            ReplyToChecklistTask(message, false);
        }

        public void ReplyToChecklistTaskInAnotherChat(MessageChecklistTask message)
        {
            ReplyToChecklistTask(message, true);
        }

        public async void ReplyToChecklistTask(MessageChecklistTask checklistTask, bool inAnotherChat)
        {
            DisposeSearch();

            var message = checklistTask.Message;
            if (message == null)
            {
                return;
            }

            if (message.Content is MessageAlbum album)
            {
                message = album.Messages[album.IsMedia ? 0 : ^1];
            }

            var should = await ShouldReplyInAnotherChatAsync(message);
            if (should == ShouldReplyInAnotherChatResult.True || inAnotherChat)
            {
                var header = ComposerHeader;
                var text = GetFormattedText(true, false);

                GetReply(true);

                var confirm = await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationReplyToMessage(message, checklistTaskId: checklistTask.Task.Id));
                if (confirm != ContentDialogResult.Primary)
                {
                    ComposerHeader = header;
                    SetFormattedText(text);
                }
            }
            else
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    ReplyTo = new MessageComposerReplyTo(message, null, checklistTask.Task.Id, string.Empty, should != ShouldReplyInAnotherChatResult.NotAvailable)
                };

                TextField?.Focus(FocusState.Keyboard);
            }
        }

        public void ReplyToPollOption(MessagePollOption message)
        {
            ReplyToPollOption(message, false);
        }

        public void ReplyToPollOptionInAnotherChat(MessagePollOption message)
        {
            ReplyToPollOption(message, true);
        }

        public async void ReplyToPollOption(MessagePollOption pollOption, bool inAnotherChat)
        {
            DisposeSearch();

            var message = pollOption.Message;
            if (message == null)
            {
                return;
            }

            if (message.Content is MessageAlbum album)
            {
                message = album.Messages[album.IsMedia ? 0 : ^1];
            }

            var should = await ShouldReplyInAnotherChatAsync(message);
            if (should == ShouldReplyInAnotherChatResult.True || inAnotherChat)
            {
                var header = ComposerHeader;
                var text = GetFormattedText(true, false);

                GetReply(true);

                var confirm = await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationReplyToMessage(message, pollOptionId: pollOption.Option.Id));
                if (confirm != ContentDialogResult.Primary)
                {
                    ComposerHeader = header;
                    SetFormattedText(text);
                }
            }
            else
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    ReplyTo = new MessageComposerReplyTo(message, null, 0, pollOption.Option.Id, should != ShouldReplyInAnotherChatResult.NotAvailable)
                };

                TextField?.Focus(FocusState.Keyboard);
            }
        }

        enum ShouldReplyInAnotherChatResult
        {
            True,
            False,
            NotAvailable
        }

        private async Task<ShouldReplyInAnotherChatResult> ShouldReplyInAnotherChatAsync(MessageViewModel message)
        {
            var properties = await ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id)) as MessageProperties;
            if (properties == null || properties.CanBeRepliedInAnotherChat is false)
            {
                return ShouldReplyInAnotherChatResult.NotAvailable;
            }

            var chat = message.Chat;
            if (chat != null && ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.IsChannel)
                {
                    return supergroup.Status is not ChatMemberStatusCreator and not ChatMemberStatusAdministrator
                        ? ShouldReplyInAnotherChatResult.True
                        : ShouldReplyInAnotherChatResult.False;
                }
            }

            return ShouldReplyInAnotherChatResult.False;
        }

        #endregion

        #region Delete

        public void DeleteMessage(MessageViewModel message)
        {
            if (message == null)
            {
                return;
            }

            var chat = message.Chat;
            if (chat == null)
            {
                return;
            }

            if (message.Content is MessageAlbum album)
            {
                DeleteMessages(chat, album.Messages);
            }
            else
            {
                DeleteMessages(chat, new[] { message });
            }

            TextField?.Focus(FocusState.Programmatic);
        }

        public async void TryDeleteMessage(MessageViewModel message)
        {
            if (message == null)
            {
                return;
            }

            var properties = await ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id)) as MessageProperties;
            if (properties == null || (!properties.CanBeDeletedOnlyForSelf && !properties.CanBeDeletedForAllUsers))
            {
                return;
            }

            DeleteMessage(message);
        }

        private async void DeleteMessages(Chat chat, IList<MessageViewModel> messages)
        {
            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var items = messages
                .DistinctBy(x => x.Id)
                .ToList<MessageWithOwner>();

            IDictionary<MessageId, MessageProperties> properties;
            if (Type == DialogType.BusinessReplies)
            {
                properties = items.ToDictionary(x => new MessageId(x), y => new MessageProperties
                {
                    CanBeDeletedForAllUsers = true,
                    CanBeDeletedOnlyForSelf = false
                });
            }
            else
            {
                properties = await ClientService.GetMessagePropertiesAsync(items.Select(x => new MessageId(x)));
            }

            var updated = items
                .Where(x => properties.ContainsKey(new MessageId(x)))
                .ToList();

            if (updated.Empty())
            {
                return;
            }

            var popup = new DeleteMessagesPopup(ClientService, chat, TopicId, updated, properties);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            IsSelectionEnabled = false;

            if (Type == DialogType.BusinessReplies)
            {
                ClientService.Send(new DeleteQuickReplyShortcutMessages(QuickReplyShortcut.Id, messages.Select(x => x.Id).ToList()));
                return;
            }

            ClientService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), popup.Revoke));

            foreach (var sender in popup.DeleteAll)
            {
                ClientService.Send(new DeleteChatMessagesBySender(chat.Id, sender));
            }

            foreach (var sender in popup.BanUser)
            {
                ClientService.Send(new SetChatMemberStatus(chat.Id, sender, popup.SelectedStatus));
            }

            if (chat.Type is ChatTypeSupergroup supertype)
            {
                foreach (var sender in popup.ReportSpam)
                {
                    var messageIds = messages
                        .Where(x => x.SenderId.AreTheSame(sender))
                        .Select(x => x.Id)
                        .ToList();

                    ClientService.Send(new ReportSupergroupSpam(supertype.SupergroupId, messageIds));
                }
            }
        }

        #endregion

        #region Forward

        public void ForwardMessage(MessageViewModel message)
        {
            IsSelectionEnabled = false;

            if (message.Content is MessageAlbum album)
            {
                ForwardMessages(album.Messages.ToDictionary(x => new MessageId(x)));
            }
            else
            {
                ForwardMessages(new[] { message }.ToDictionary(x => new MessageId(x)));
            }
        }

        #endregion

        #region Multiple Delete

        public void DeleteSelectedMessages()
        {
            var messages = new List<MessageViewModel>(SelectedItems.Values);

            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var chat = first.Chat;
            if (chat == null)
            {
                return;
            }

            DeleteMessages(chat, messages);
        }

        private bool _canDeleteSelectedMessages;
        public bool CanDeleteSelectedMessages
        {
            get => _canDeleteSelectedMessages;
            set => Set(ref _canDeleteSelectedMessages, value);
        }

        #endregion

        #region Multiple Forward

        public void ForwardSelectedMessages()
        {
            var selectedItems = SelectedItems.Values
                .DistinctBy(x => x.Id)
                .ToDictionary(x => new MessageId(x));

            IsSelectionEnabled = false;
            ForwardMessages(selectedItems);
        }

        private async void ForwardMessages(Dictionary<MessageId, MessageViewModel> selectedItems)
        {
            var properties = await ClientService.GetMessagePropertiesAsync(selectedItems.Select(x => x.Key));

            var messages = properties.Where(x => x.Value.CanBeForwarded).OrderBy(x => x.Key.Id).ToList();
            if (messages.Count > 0)
            {
                var messagesToShare = new List<MessageToShare>(messages.Count);

                foreach (var property in messages)
                {
                    if (selectedItems.TryGetValue(property.Key, out var message))
                    {
                        messagesToShare.Add(new MessageToShare(message, property.Value));
                    }
                }

                await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationShareMessages(messagesToShare));
                TextField?.Focus(FocusState.Programmatic);
            }
        }

        private bool _canForwardSelectedMessages;
        public bool CanForwardSelectedMessages
        {
            get => _canForwardSelectedMessages;
            set => Set(ref _canForwardSelectedMessages, value);
        }

        #endregion

        #region Multiple Download

        public void DownloadSelectedMessages()
        {
            var messages = SelectedItems.Values.Where(x => x.CanBeAddedToDownloads).ToList();
            if (messages.Count > 0)
            {
                IsSelectionEnabled = false;
                TextField?.Focus(FocusState.Programmatic);

                foreach (var message in messages)
                {
                    switch (message.Content)
                    {
                        case MessageAudio audio:
                            ClientService.Send(new AddFileToDownloads(audio.Audio.AudioValue.Id, message.ChatId, message.Id, 32));
                            break;
                        case MessageDocument document:
                            ClientService.Send(new AddFileToDownloads(document.Document.DocumentValue.Id, message.ChatId, message.Id, 32));
                            break;
                        case MessageVideo video:
                            ClientService.Send(new AddFileToDownloads(video.Video.VideoValue.Id, message.ChatId, message.Id, 32));
                            break;
                    }
                }
            }
        }

        #endregion

        #region Multiple Copy

        public void CopySelectedMessages()
        {
            var messages = SelectedItems.Values.OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                var builder = new StringBuilder();
                IsSelectionEnabled = false;

                foreach (var message in messages)
                {
                    var chat = message.Chat;
                    var title = chat.Title;

                    if (ClientService.TryGetUser(message.SenderId, out Telegram.Td.Api.User senderUser))
                    {
                        title = senderUser.FullName();
                    }
                    else if (ClientService.TryGetChat(message.SenderId, out Chat senderChat))
                    {
                        title = ClientService.GetTitle(senderChat);
                    }

                    builder.AppendLine(string.Format("{0}, [{1} {2}]", title, Formatter.Date(message.Date), Formatter.Time(message.Date)));

                    if (message.ForwardInfo?.Origin is MessageOriginChat fromChat)
                    {
                        var from = ClientService.GetChat(fromChat.SenderChatId);
                        builder.AppendLine($"[{Strings.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.From} {ClientService.GetTitle(from)}]");
                    }
                    else if (message.ForwardInfo?.Origin is MessageOriginChannel forwardedPost)
                    {
                        var from = ClientService.GetChat(forwardedPost.ChatId);
                        builder.AppendLine($"[{Strings.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.From} {ClientService.GetTitle(from)}]");
                    }
                    else if (message.ForwardInfo?.Origin is MessageOriginUser forwardedFromUser)
                    {
                        var from = ClientService.GetUser(forwardedFromUser.SenderUserId);
                        builder.AppendLine($"[{Strings.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.From} {from.FullName()}]");
                    }
                    else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser forwardedFromHiddenUser)
                    {
                        builder.AppendLine($"[{Strings.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.From} {forwardedFromHiddenUser.SenderName}]");
                    }
                    else if (message.ImportInfo != null)
                    {
                        builder.AppendLine($"[{Strings.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.From} {message.ImportInfo.SenderName}]");
                    }

                    if (message.ReplyToItem is MessageViewModel replyToMessage)
                    {
                        if (ClientService.TryGetUser(replyToMessage.SenderId, out Telegram.Td.Api.User replyUser))
                        {
                            builder.AppendLine($"[In reply to {replyUser.FullName()}]");
                        }
                        else if (ClientService.TryGetChat(replyToMessage.SenderId, out Chat replyChat))
                        {
                            builder.AppendLine($"[In reply to {replyChat.Title}]");
                        }
                    }
                    else if (message.ReplyToItem is Story replyToStory)
                    {
                        if (ClientService.TryGetUser(replyToStory.PosterChatId, out Telegram.Td.Api.User replyUser))
                        {
                            builder.AppendLine($"[In reply to {replyUser.FullName()}]");
                        }
                    }

                    if (message.Content is MessagePhoto photo)
                    {
                        builder.Append($"[{(photo.Video != null ? Strings.AttachLivePhoto : Strings.AttachPhoto)}]");

                        if (photo.Caption != null && !string.IsNullOrEmpty(photo.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.AppendLine(photo.Caption.Text);
                        }
                    }
                    else if (message.Content is MessageVoiceNote voiceNote)
                    {
                        builder.Append($"[{Strings.AttachAudio}]");

                        if (voiceNote.Caption != null && !string.IsNullOrEmpty(voiceNote.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.AppendLine(voiceNote.Caption.Text);
                        }
                    }
                    else if (message.Content is MessageVideo video)
                    {
                        builder.Append($"[{Strings.AttachVideo}]");

                        if (video.Caption != null && !string.IsNullOrEmpty(video.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.AppendLine(video.Caption.Text);
                        }
                    }
                    else if (message.Content is MessageVideoNote)
                    {
                        builder.Append($"[{Strings.AttachRound}]");
                    }
                    else if (message.Content is MessageAnimation animation)
                    {
                        builder.Append($"[{Strings.AttachGif}]");

                        if (animation.Caption != null && !string.IsNullOrEmpty(animation.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.AppendLine(animation.Caption.Text);
                        }
                    }
                    else if (message.Content is MessageSticker sticker)
                    {
                        if (!string.IsNullOrEmpty(sticker.Sticker.Emoji))
                        {
                            builder.AppendLine($"[{sticker.Sticker.Emoji} {Strings.AttachSticker}]");
                        }
                        else
                        {
                            builder.AppendLine($"[{Strings.AttachSticker}]");
                        }
                    }
                    else if (message.Content is MessageAudio audio)
                    {
                        builder.Append($"[{Strings.AttachMusic}]");

                        if (audio.Caption != null && !string.IsNullOrEmpty(audio.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.AppendLine(audio.Caption.Text);
                        }
                    }
                    else if (message.Content is MessageLocation location)
                    {
                        builder.AppendLine($"[{Strings.AttachLocation}]");
                        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "https://www.bing.com/maps/?pc=W8AP&FORM=MAPXSH&where1=44.312783,9.33426&locsearch=1", location.Location.Latitude, location.Location.Longitude));
                    }
                    else if (message.Content is MessageVenue venue)
                    {
                        builder.AppendLine($"[{Strings.AttachLocation}]");
                        builder.AppendLine(venue.Venue.Title);
                        builder.AppendLine(venue.Venue.Address);
                        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "https://www.bing.com/maps/?pc=W8AP&FORM=MAPXSH&where1=44.312783,9.33426&locsearch=1", venue.Venue.Location.Latitude, venue.Venue.Location.Longitude));
                    }
                    else if (message.Content is MessageContact contact)
                    {
                        builder.AppendLine($"[{Strings.AttachContact}]");
                        builder.AppendLine(contact.Contact.GetFullName());
                        builder.AppendLine(PhoneNumber.Format(contact.Contact.PhoneNumber));
                    }
                    else if (message.Content is MessagePoll poll)
                    {
                        builder.AppendLine($"[{Strings.Poll}: {poll.Poll.Question.Text}]");

                        foreach (var option in poll.Poll.Options)
                        {
                            builder.AppendLine($"- {option.Text.Text}");
                        }
                    }
                    else if (message.Content is MessageChecklist checklist)
                    {
                        builder.AppendLine($"[{Strings.Todo}: {checklist.List.Title.Text}]");

                        foreach (var task in checklist.List.Tasks)
                        {
                            if (task.CompletionDate != 0)
                            {
                                builder.AppendLine($"\u2611 {task.Text.Text}");
                            }
                            else
                            {
                                builder.AppendLine($"\u2610 {task.Text.Text}");
                            }
                        }
                    }
                    else if (message.Content is MessageText text)
                    {
                        builder.AppendLine(text.Text.Text);
                    }

                    if (message != messages.Last())
                    {
                        builder.AppendLine();
                    }
                }

                MessageHelper.CopyText(XamlRoot, builder.ToString());
            }
        }

        public bool CanCopySelectedMessage => SelectedItems.Count > 0;

        #endregion

        #region Multiple Report

        public async void ReportSelectedMessages()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var myId = ClientService.Options.MyId;
            var messages = SelectedItems.Values
                .Where(x => x.SenderId is MessageSenderChat || (x.SenderId is MessageSenderUser senderUser && senderUser.UserId != myId))
                .OrderBy(x => x.Id).Select(x => x.Id).ToList();
            if (messages.Count < 1)
            {
                return;
            }

            await ReportAsync(messages);
        }

        public bool CanReportSelectedMessages
        {
            get
            {
                var chat = _chat;
                if (chat == null)
                {
                    return false;
                }

                var myId = ClientService.Options.MyId;
                return chat.CanBeReported && SelectedItems.Count > 0
                    && SelectedItems.Values.All(x => x.SenderId is MessageSenderChat || (x.SenderId is MessageSenderUser senderUser && senderUser.UserId != myId));
            }
        }

        #endregion

        #region Select

        public void SelectMessage(MessageViewModel message)
        {
            DisposeSearch();

            if (message.MediaAlbumId != 0 && _groupedMessages.TryGetValue(message.MediaAlbumId, out MessageViewModel group))
            {
                message = group;
            }

            Select(message);
            IsSelectionEnabled = true;
            //ListField?.SelectedItems.Add(message);

            //ExpandSelection(new[] { message });
        }

        #endregion

        #region Unselect

        public void UnselectMessages()
        {
            IsSelectionEnabled = false;
        }

        #endregion

        #region Statistics

        public void OpenMessageStatistics(MessageViewModel message)
        {
            NavigationService.Navigate(typeof(MessageStatisticsPage), new MessageId(message.ChatId, message.Id));
        }

        #endregion

        #region Resend

        public async void ResendMessage(MessageViewModel message)
        {
            var paidMessageStarCount = 0L;

            if (message.SendingState is MessageSendingStateFailed failed)
            {
                paidMessageStarCount = failed.RequiredPaidMessageStarCount;
            }

            var paid = await ShowPaidMessageConfirmationAsync(1, paidMessageStarCount);
            if (paid != ContentDialogResult.Primary)
            {
                return;
            }

            ClientService.Send(new ResendMessages(message.ChatId, new[] { message.Id }, null, paidMessageStarCount));
        }

        #endregion

        #region Copy

        public void CopyMessage(MessageViewModel message)
        {
            if (message == null)
            {
                return;
            }

            var input = message.GetCaption();
            if (message.Content is MessageContact contact)
            {
                input = PhoneNumber.Format(contact.Contact.PhoneNumber).AsFormattedText();
            }
            else if (message.Content is MessageAnimatedEmoji animatedEmoji)
            {
                if (animatedEmoji.AnimatedEmoji.Sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
                {
                    input = ClientEx.CustomEmoji(animatedEmoji.Emoji, customEmoji.CustomEmojiId);
                }
                else
                {
                    input = animatedEmoji.Emoji.AsFormattedText();
                }
            }

            if (input != null)
            {
                MessageHelper.CopyText(XamlRoot, input);
            }
        }

        public void CopyMessage(MessageQuote quote)
        {
            var input = quote?.Quote;
            if (input != null)
            {
                MessageHelper.CopyText(XamlRoot, input);
            }
        }

        public void CopyText(FormattedText text)
        {
            MessageHelper.CopyText(XamlRoot, text);
        }

        #endregion

        #region Copy media

        public async void CopyMessageMedia(MessageViewModel message)
        {
            var photo = message.GetPhoto();
            if (photo == null)
            {
                return;
            }

            var big = photo.GetBig();
            if (big == null)
            {
                return;
            }

            var cached = await ClientService.GetFileAsync(big.Photo);
            if (cached != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(cached));
                ClipboardEx.TrySetContent(dataPackage);

                ToastPopup.Show(XamlRoot, Strings.ImageCopied, ToastPopupIcon.Copied);
            }
        }

        #endregion

        #region Copy link

        public async void CopyMessageLink(MessageViewModel message)
        {
            if (message == null)
            {
                return;
            }

            var chat = message.Chat;
            if (chat == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetMessageLink(chat.Id, message.Id, 0, 0, string.Empty, false, ForumTopic != null || Thread != null));
            if (response is MessageLink link)
            {
                MessageHelper.CopyLink(XamlRoot, link.Link, link.IsPublic);
            }
        }

        #endregion

        #region Copy path

        public async void CopyMessagePath(MessageViewModel message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.CopyFilePathAsync(XamlRoot, file);
            }
        }

        #endregion

        #region Edit

        public async void EditLastMessage()
        {
            foreach (var message in Items.Where(x => x.IsOutgoing).Reverse())
            {
                if (message.SendingState is not null)
                {
                    return;
                }

                var properties = await ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id)) as MessageProperties;
                if (properties != null && properties.CanBeEdited)
                {
                    EditMessage(message);
                    HistoryField?.ScrollToItem(message, VerticalAlignment.Center, new MessageBubbleHighlightOptions(false));

                    return;
                }
            }
        }

        public void EditMessage(MessageViewModel message)
        {
            if (message.Content is MessageChecklist)
            {
                EditChecklist(message);
                return;
            }
            else if (message.Content is MessageAlbum album)
            {
                message = null;

                if (album.IsMedia)
                {
                    foreach (var child in album.Messages)
                    {
                        var childCaption = child.GetCaption();
                        if (childCaption != null && !string.IsNullOrEmpty(childCaption.Text))
                        {
                            message = child;
                        }
                    }
                }

                message ??= album.Messages.LastOrDefault();
            }

            if (message == null)
            {
                return;
            }

            ClearInlineBot();
            DisposeSearch();
            SaveDraft();

            var input = message.GetCaption();
            var container = new MessageComposerHeader(ClientService)
            {
                Editing = new MessageComposerEditing(message, null)
            };

            if (message.Content is MessageText text)
            {
                if (text.LinkPreview != null)
                {
                    container.LinkPreview = text.LinkPreview;
                    container.LinkPreviewUrl = text.LinkPreview.Url;
                    container.LinkPreviewOptions = text.LinkPreviewOptions;
                }
                else
                {
                    var url = text.Text.Entities.FirstOrDefault(x => x.Type is TextEntityTypeUrl);
                    if (url != null)
                    {
                        container.LinkPreviewUrl = text.Text.Text.Substring(url.Offset, url.Length);
                        container.LinkPreviewDisabled = true;
                    }
                }
            }

            ComposerHeader = container;
            SetText(input);
        }

        #endregion

        #region View thread

        public async void OpenMessageThread(MessageViewModel message)
        {
            var response = await ClientService.SendAsync(new GetMessageThread(message.ChatId, message.Id));
            if (response is MessageThreadInfo threadInfo)
            {
                // TODO: should thread be info.MessageThreadId?
                NavigationService.NavigateToChat(message.ChatId, message.Id, new MessageTopicThread(threadInfo.MessageThreadId));
            }
        }

        #endregion

        #region Pin

        public async void PinMessage(MessageViewModel message)
        {
            var chat = message.Chat;
            if (chat == null)
            {
                return;
            }

            if (message.IsPinned)
            {
                var confirm = await ShowPopupAsync(Strings.UnpinMessageAlert, Strings.AppName, Strings.OK, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new UnpinChatMessage(chat.Id, message.Id));
                }
            }
            else
            {
                var channel = chat.Type is ChatTypeSupergroup super && super.IsChannel;
                var self = chat.Type is ChatTypePrivate privata && privata.UserId == ClientService.Options.MyId;

                var last = PinnedMessages.LastOrDefault();

                var dialog = new MessagePopup();
                dialog.Title = Strings.PinMessageAlertTitle;

                if (last != null && last.Id > message.Id)
                {
                    dialog.Message = Strings.PinOldMessageAlert;
                }
                else if (channel)
                {
                    dialog.Message = Strings.PinMessageAlertChannel;
                }
                else if (chat.Type is ChatTypePrivate)
                {
                    dialog.Message = Strings.PinMessageAlertChat;
                }
                else
                {
                    dialog.Message = Strings.PinMessageAlert;
                }

                dialog.PrimaryButtonText = Strings.OK;
                dialog.SecondaryButtonText = Strings.Cancel;

                if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup && !channel)
                {
                    dialog.CheckBoxLabel = Strings.PinNotify;
                    dialog.IsChecked = true;
                }
                else if (chat.Type is ChatTypePrivate && !self)
                {
                    dialog.CheckBoxLabel = string.Format(Strings.PinAlsoFor, chat.Title);
                    dialog.IsChecked = false;
                }

                var confirm = await ShowPopupAsync(dialog);
                if (confirm == ContentDialogResult.Primary)
                {
                    var disableNotification = false;
                    var onlyForSelf = false;

                    if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup && !channel)
                    {
                        disableNotification = dialog.IsChecked == false;
                    }
                    else if (chat.Type is ChatTypePrivate && !self)
                    {
                        onlyForSelf = dialog.IsChecked == false;
                    }

                    ClientService.Send(new PinChatMessage(chat.Id, message.Id, disableNotification, onlyForSelf));
                }
            }
        }

        #endregion

        #region Report

        public async void ReportMessage(MessageViewModel message)
        {
            if (message.Content is MessageSponsored { CanBeReported: true })
            {
                ShowPopup(new ReportAdsPopup(this, ChatId, message.Id, null));
            }
            else
            {
                await ReportAsync(new[] { message.Id });
            }
        }

        #endregion

        #region Fact check

        public async void FactCheckMessage(MessageViewModel message)
        {
            var popup = new FactCheckPopup(message.FactCheck?.Text, ClientService.Options.FactCheckLengthMax);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new SetMessageFactCheck(message.ChatId, message.Id, popup.Text));
                ShowToast(string.IsNullOrEmpty(popup.Text?.Text) ? Strings.FactCheckDeleted : Strings.FactCheckEdited, ToastPopupIcon.Info);
            }
        }

        #endregion

        #region Report false positive

        public async void ReportFalsePositive(MessageViewModel message)
        {
            if (_chat?.Type is ChatTypeSupergroup supergroup)
            {
                ClientService.Send(new ReportSupergroupAntiSpamFalsePositive(supergroup.SupergroupId, message.Id));
                await ShowPopupAsync(Strings.ChannelAntiSpamFalsePositiveReported);
            }
        }

        #endregion

        #region Send now

        public async void SendNowMessage(MessageViewModel message)
        {
            if (message.SchedulingState is MessageSchedulingStateSendWhenVideoProcessed)
            {
                var confirm = await ShowPopupAsync(Strings.VideoConversionNowText, Strings.VideoConversionNowTitle, Strings.VideoConversionNowSend, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            ClientService.Send(new EditMessageSchedulingState(message.ChatId, message.Id, null));
        }

        #endregion

        #region Reschedule

        public async void RescheduleMessage(MessageViewModel message)
        {
            var options = await PickMessageSendOptionsAsync(1, SchedulingState.Schedule);
            if (options?.SchedulingState == null)
            {
                return;
            }

            ClientService.Send(new EditMessageSchedulingState(message.ChatId, message.Id, options.SchedulingState));
        }

        #endregion

        #region Translate

        public void TranslateMessage(MessageQuote message)
        {
            var language = LanguageIdentification.IdentifyLanguage(message.Quote.Text);
            var popup = new TranslatePopup(_translateService, message.Quote, language, Settings.Translate.To, !message.Message.CanBeSaved);
            ShowPopup(popup);
        }

        public void TranslateMessage(MessageViewModel message)
        {
            FormattedText text;
            long chatId;
            long messageId;

            if (message.Content is MessagePoll poll)
            {
                var builder = new FormattedText(poll.Poll.Question.Text, poll.Poll.Question.Entities.ToList());

                foreach (var option in poll.Poll.Options)
                {
                    builder = FormattedText.Concat("\n\U0001F518 ", option.Text);
                }

                text = builder;
                chatId = 0;
                messageId = 0;
            }
            else if (message.Content is MessageChecklist checklist)
            {
                var builder = new FormattedText(checklist.List.Title.Text, checklist.List.Title.Entities.ToList());

                foreach (var task in checklist.List.Tasks)
                {
                    if (task.CompletionDate != 0)
                    {
                        builder = FormattedText.Concat("\n\u2611 ", task.Text);
                    }
                    else
                    {
                        builder = FormattedText.Concat("\n\u2610 ", task.Text);
                    }
                }

                text = builder;
                chatId = 0;
                messageId = 0;
            }
            else
            {
                var caption = message.GetTranslatableText();
                if (string.IsNullOrEmpty(caption?.Text))
                {
                    return;
                }

                text = caption;
                chatId = message.ChatId;
                messageId = message.Id;

                if (message.Content is MessageAlbum album && album.Messages.Count > 0)
                {
                    messageId = album.IsMedia
                        ? album.Messages[0].Id
                        : album.Messages[^1].Id;
                }
            }

            var language = LanguageIdentification.IdentifyLanguage(text.Text);
            var popup = new TranslatePopup(_translateService, /*chatId, messageId,*/ text, language, Settings.Translate.To, !message.CanBeSaved);
            ShowPopup(popup);
        }

        #endregion

        #region Checklists

        public void EditChecklist(MessageViewModel message)
        {
            if (message.Content is MessageChecklist checklist)
            {
                EditChecklist(message, checklist.List, false, null);
            }
        }

        public void EditChecklistTask(MessageChecklistTask task)
        {
            if (task.Message.Content is MessageChecklist checklist)
            {
                EditChecklist(task.Message, checklist.List, false, task.Task);
            }
        }

        public void DeleteChecklistTask(MessageChecklistTask task)
        {
            if (task.Message.Content is MessageChecklist checklist)
            {
                var tasks = new List<InputChecklistTask>();

                foreach (var item in checklist.List.Tasks)
                {
                    if (item.Id != task.Task.Id)
                    {
                        tasks.Add(new InputChecklistTask(item.Id, item.Text));
                    }
                }

                ClientService.Send(new EditMessageChecklist(task.Message.ChatId, task.Message.Id, new InputChecklist(checklist.List.Title, tasks, checklist.List.OthersCanAddTasks, checklist.List.OthersCanMarkTasksAsDone)));
            }
        }

        public void MarkChecklistTask(MessageChecklistTask task)
        {
            if (task.Message.Content is MessageChecklist checklist)
            {
                var markedAsDone = new List<int>();
                var markedAsNotDone = new List<int>();

                if (task.Task.CompletionDate != 0)
                {
                    markedAsNotDone.Add(task.Task.Id);
                }
                else
                {
                    markedAsDone.Add(task.Task.Id);
                }

                ClientService.Send(new MarkChecklistTasksAsDone(task.Message.ChatId, task.Message.Id, markedAsDone, markedAsNotDone));
            }
        }

        public void AddChecklistTask(MessageViewModel message)
        {
            if (message.Content is MessageChecklist checklist)
            {
                EditChecklist(message, checklist.List, true, null);
            }
        }

        public async void CopyChecklistTask(MessageChecklistTask task)
        {
            var response = await ClientService.SendAsync(new GetMessageLink(task.Message.ChatId, task.Message.Id, 0, task.Task.Id, string.Empty, false, ForumTopic != null || Thread != null));
            if (response is MessageLink link)
            {
                MessageHelper.CopyLink(XamlRoot, link.Link, link.IsPublic);
            }
        }

        private async void EditChecklist(MessageViewModel message, Checklist checklist, bool addTask, ChecklistTask taskToEdit)
        {
            var response = await ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id));
            if (response is not MessageProperties properties)
            {
                return;
            }

            var popup = new CreateChecklistPopup(ClientService, checklist, properties.CanBeEdited, addTask, taskToEdit);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            if (properties.CanBeEdited && !addTask)
            {
                ClientService.Send(new EditMessageChecklist(message.ChatId, message.Id, new InputChecklist(popup.Title, popup.Tasks, popup.OthersCanAddTasks, popup.OthersCanMarkTasksAsDone)));
            }
            else
            {
                ClientService.Send(new AddChecklistTasks(message.ChatId, message.Id, popup.AddedTasks));
            }
        }

        #endregion

        #region Keyboard button

        public async void OpenInlineButton(MessageViewModel message, InlineKeyboardButton inline)
        {
            if (_chat is not Chat chat)
            {
                return;
            }

            if (message.SchedulingState != null)
            {
                await ShowPopupAsync(Strings.MessageScheduledBotAction, Strings.AppName, Strings.OK);
                return;
            }

            if (inline.Type is InlineKeyboardButtonTypeBuy)
            {
                NavigationService.NavigateToInvoice(message);
            }
            else if (inline.Type is InlineKeyboardButtonTypeUser user)
            {
                var response = await ClientService.SendAsync(new CreatePrivateChat(user.UserId, false));
                if (response is Chat userChat)
                {
                    NavigationService.NavigateToChat(userChat);
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeLoginUrl loginUrl)
            {
                var response = await ClientService.SendAsync(new GetLoginUrlInfo(chat.Id, message.Id, loginUrl.Id));
                if (response is LoginUrlInfoOpen infoOpen)
                {
                    OpenUrl(infoOpen.Url, !infoOpen.SkipConfirmation);
                }
                else if (response is LoginUrlInfoRequestConfirmation requestConfirmation)
                {
                    var popup = new LoginUrlInfoPopup(ClientService, requestConfirmation);

                    var confirm = await ShowPopupAsync(popup);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    response = await ClientService.SendAsync(new GetLoginUrl(chat.Id, message.Id, loginUrl.Id, popup.AllowWriteAccess));
                    if (response is HttpUrl httpUrl)
                    {
                        if (MessageHelper.TryCreateUri(httpUrl.Url, out Uri uri))
                        {
                            await Launcher.LaunchUriAsync(uri);
                        }
                    }
                    else if (response is Error)
                    {
                        if (MessageHelper.TryCreateUri(loginUrl.Url, out Uri uri))
                        {
                            await Launcher.LaunchUriAsync(uri);
                        }
                    }
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeSwitchInline switchInline)
            {
                var bot = message.GetViaBotUser();
                if (bot == null)
                {
                    return;
                }

                if (switchInline.TargetChat is TargetChatCurrent && bot.HasActiveUsername(out string username))
                {
                    SetText(string.Format("@{0} {1}", username, switchInline.Query), focus: true);
                    ResolveInlineBot(username, switchInline.Query);
                }
                else if (switchInline.TargetChat is TargetChatInternalLink internalLink)
                {
                    // TODO
                }
                else
                {
                    await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationSwitchInline(switchInline.Query, switchInline.TargetChat, bot));
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeUrl urlButton)
            {
                MessageHelper.OpenUrl(ClientService, NavigationService, urlButton.Url, true, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
            else if (inline.Type is InlineKeyboardButtonTypeCallback callback)
            {
                var response = await ClientService.SendAsync(new GetCallbackQueryAnswer(chat.Id, message.Id, new CallbackQueryPayloadData(callback.Data)));
                if (response is CallbackQueryAnswer answer)
                {
                    if (!string.IsNullOrEmpty(answer.Text))
                    {
                        if (answer.ShowAlert)
                        {
                            ShowPopup(new MessagePopup(answer.Text));
                        }
                        else
                        {
                            ShowToast(answer.Text);
                        }
                    }
                    else if (!string.IsNullOrEmpty(answer.Url))
                    {
                        MessageHelper.OpenUrl(ClientService, NavigationService, answer.Url);
                    }
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeCallbackWithPassword callbackWithPassword)
            {
                var popup = new InputPopup(InputPopupType.Password)
                {
                    Title = Strings.BotOwnershipTransfer,
                    Header = Strings.BotOwnershipTransferReadyAlertText,
                    PlaceholderText = Strings.LoginPassword,
                    PrimaryButtonText = Strings.BotOwnershipTransferChangeOwner,
                    SecondaryButtonText = Strings.Cancel
                };

                var result = await ShowPopupAsync(popup);
                if (result != ContentDialogResult.Primary)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new GetCallbackQueryAnswer(chat.Id, message.Id, new CallbackQueryPayloadDataWithPassword(popup.Text, callbackWithPassword.Data)));
                if (response is Error error)
                {
                    if (error.Message.Equals("PASSWORD_MISSING") || error.Message.StartsWith("PASSWORD_TOO_FRESH_") || error.Message.StartsWith("SESSION_TOO_FRESH_"))
                    {
                        var primary = Strings.OK;

                        var builder = new StringBuilder();
                        builder.AppendLine(Strings.BotOwnershipTransferAlertText);
                        builder.AppendLine($"\u2022 {Strings.EditAdminTransferAlertText1}");
                        builder.AppendLine($"\u2022 {Strings.EditAdminTransferAlertText2}");

                        if (error.Message.Equals("PASSWORD_MISSING"))
                        {
                            primary = Strings.EditAdminTransferSetPassword;
                        }
                        else
                        {
                            builder.AppendLine();
                            builder.AppendLine(Strings.EditAdminTransferAlertText3);
                        }

                        var confirm = await ShowPopupAsync(builder.ToString(), Strings.EditAdminTransferAlertTitle, primary, Strings.Cancel);
                        if (confirm == ContentDialogResult.Primary && error.Message.Equals("PASSWORD_MISSING"))
                        {
                            NavigationService.NavigateToPasswordSetup();
                        }
                    }
                    else if (error.Message.Equals("PASSWORD_HASH_INVALID"))
                    {
                        OpenInlineButton(message, inline);
                    }
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeCallbackGame)
            {
                OpenGame(message);
            }
            else if (inline.Type is InlineKeyboardButtonTypeWebApp webApp)
            {
                var botUser = message.GetViaBotUser();
                if (botUser == null)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new OpenWebApp(chat.Id, botUser.Id, webApp.Url, OutgoingTopicId, null, new WebAppOpenParameters(Theme.Current.Parameters, Constants.WebAppHostName, new WebAppOpenModeFullSize())));
                if (response is WebAppInfo webAppInfo)
                {
                    NavigationService.NavigateToWebApp(botUser, webAppInfo.Url, webAppInfo.LaunchId, null, source: new OpenUrlSourceChat(ChatId, null), buttonText: inline.Text);
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeCopyText copyText)
            {
                MessageHelper.CopyText(XamlRoot, copyText.Text);
            }
            else if (inline.Type is InlineKeyboardButtonTypeSuggestionDecline)
            {
                DeclineSuggestedPost(message);
            }
            else if (inline.Type is InlineKeyboardButtonTypeSuggestionApprove)
            {
                ApproveSuggestedPost(message);
            }
            else if (inline.Type is InlineKeyboardButtonTypeSuggestionEdit)
            {

            }
        }

        private async void DeclineSuggestedPost(MessageViewModel message)
        {
            var confirm = await ShowInputAsync(InputPopupType.Text, string.Format(Strings.SuggestedMessageDeclineInfo, ClientService.GetTitle(message.SenderId)), Strings.SuggestedMessageDecline, Strings.SuggestedMessageDeclineReasonHint, Strings.Decline, Strings.Cancel, destructive: true);
            if (confirm.Result == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeclineSuggestedPost(message.ChatId, message.Id, confirm.Text));
            }
        }

        private async void ApproveSuggestedPost(MessageViewModel message)
        {
            var text = new StringBuilder();
            text.AppendLine(string.Format(Strings.SuggestedMessageAcceptInfo, ClientService.GetTitle(message.SenderId)));
            text.AppendLine();

            ToastPopup disclaimer = null;

            if (ClientService.TryGetSupergroup(Chat, out Supergroup supergroup) && supergroup.IsAdministeredDirectMessagesGroup)
            {
                var percent = message.SuggestedPostInfo.Price switch
                {
                    SuggestedPostPriceTon => (ClientService.Options.SuggestedPostToncoinEarningsPerMille / 1000d).ToString("0.##%"),
                    _ => (ClientService.Options.SuggestedPostStarEarningsPerMille / 1000d).ToString("0.##%")
                };

                if (message.SuggestedPostInfo.SendDate == 0)
                {
                    text.AppendLine(string.Format(Strings.SuggestedMessageAcceptInfoAnytimeAdmin2, message.SuggestedPostInfo.Price.ToValue(), percent));
                }
                else
                {
                    text.AppendLine(string.Format(Strings.SuggestedMessageAcceptInfoAdmin2, message.SuggestedPostInfo.Price.ToValue(), Formatter.DateAt(message.SuggestedPostInfo.SendDate), percent));
                }

                disclaimer = ToastPopup.Show(XamlRoot, Strings.SuggestedMessageAcceptStarsDisclaimer.AsFormattedText(), ToastPopupIcon.Info, dismissAfter: TimeSpan.Zero);
                disclaimer.PreferredPlacement = TeachingTipPlacementMode.Top;
            }
            else if (message.SuggestedPostInfo.SendDate == 0)
            {
                text.AppendLine(string.Format(Strings.SuggestedMessageAcceptInfoAnytimeUser2, message.SuggestedPostInfo.Price.ToValue()));
            }
            else
            {
                text.AppendLine(string.Format(Strings.SuggestedMessageAcceptInfoUser2, message.SuggestedPostInfo.Price.ToValue(), Formatter.DateAt(message.SuggestedPostInfo.SendDate)));
            }

            text.Append(string.Format(Strings.SuggestedMessageAcceptInfo3, (ClientService.Options.SuggestedPostLifetimeMin / 3600.0).ToString("N0")));

            var confirm = await ShowPopupAsync(text.ToString(), Strings.SuggestedPostAcceptTitle, message.SuggestedPostInfo.SendDate == 0 ? Strings.Next : Strings.SuggestedPostPublish, Strings.Cancel);

            disclaimer?.IsOpen = false;

            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var sendDate = message.SuggestedPostInfo.SendDate;
            if (sendDate == 0)
            {
                // TODO: "Send Now" option
                var popup = new ChooseDateTimePopup
                {
                    Title = Strings.SuggestedPostAcceptTitle,
                    Header = Strings.PostSuggestionsAddTimeHint,
                    PrimaryButtonText = Strings.OK,
                    SecondaryButtonText = Strings.Cancel,
                };

                confirm = await ShowPopupAsync(popup);

                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                sendDate = popup.Value.ToTimestamp();
            }

            ClientService.Send(new ApproveSuggestedPost(message.ChatId, message.Id, sendDate < DateTime.Now.ToTimestamp() ? 0 : sendDate));
        }

        public async void KeyboardButtonExecute(MessageViewModel message, KeyboardButton keyboardButton)
        {
            if (_chat is not Chat chat)
            {
                return;
            }

            if (message.ReplyMarkup is ReplyMarkupShowKeyboard { OneTime: true })
            {
                ClientService.Send(new DeleteChatReplyMarkup(message.ChatId, message.Id));
            }

            if (keyboardButton.Type is KeyboardButtonTypeRequestPhoneNumber)
            {
                if (ClientService.TryGetUser(ClientService.Options.MyId, out Telegram.Td.Api.User cached))
                {
                    var content = Strings.AreYouSureShareMyContactInfo;
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        var withUser = ClientService.GetUser(privata.UserId);
                        if (withUser != null)
                        {
                            content = withUser.Type is UserTypeBot ? Strings.AreYouSureShareMyContactInfoBot : string.Format(Strings.AreYouSureShareMyContactInfoUser, PhoneNumber.Format(cached.PhoneNumber), withUser.FullName());
                        }
                    }

                    var confirm = await ShowPopupAsync(content, Strings.ShareYouPhoneNumberTitle, Strings.OK, Strings.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await SendContactAsync(new Contact(cached.PhoneNumber, cached.FirstName, cached.LastName, string.Empty, cached.Id), null);
                    }
                }
            }
            else if (keyboardButton.Type is KeyboardButtonTypeRequestLocation)
            {
                var confirm = await ShowPopupAsync(Strings.ShareYouLocationInfo, Strings.ShareYouLocationTitle, Strings.OK, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var location = await _locationService.GetPositionAsync(NavigationService);
                    if (location != null)
                    {
                        await SendMessageAsync(null, new InputMessageLocation(location), null);
                    }
                }
            }
            else if (keyboardButton.Type is KeyboardButtonTypeRequestPoll requestPoll)
            {
                await SendPollAsync(false, requestPoll.ForceQuiz, requestPoll.ForceRegular, _chat?.Type is ChatTypeSupergroup super && super.IsChannel);
            }
            else if (keyboardButton.Type is KeyboardButtonTypeText)
            {
                var input = new InputMessageText(new FormattedText(keyboardButton.Text, null), null, true);
                await SendMessageAsync(chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup ? new InputMessageReplyToMessage(message.Id, null, 0, string.Empty) : null, input, null);
            }
            else if (keyboardButton.Type is KeyboardButtonTypeWebApp webApp)
            {
                if (ClientService.TryGetUser(message.SenderId, out Td.Api.User botUser))
                {
                    var response = await ClientService.SendAsync(new GetWebAppUrl(botUser.Id, webApp.Url, new WebAppOpenParameters(Theme.Current.Parameters, Constants.WebAppHostName, new WebAppOpenModeFullSize())));
                    if (response is WebAppUrl webAppUrl)
                    {
                        NavigationService.NavigateToWebApp(botUser, webAppUrl, 0, null, source: new OpenUrlSourceChat(ChatId, null), buttonText: keyboardButton.Text);
                    }
                }
            }
            else if (keyboardButton.Type is KeyboardButtonTypeRequestUsers requestUsers)
            {
                NavigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationRequestUsers(new KeyboardButtonSourceMessage(message.ChatId, message.Id), requestUsers));
            }
            else if (keyboardButton.Type is KeyboardButtonTypeRequestChat requestChat)
            {
                NavigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationRequestChat(requestChat));
            }
            else if (keyboardButton.Type is KeyboardButtonTypeRequestManagedBot requestManagedBot)
            {
                if (ClientService.TryGetUser(message.SenderId, out Td.Api.User botUser))
                {
                    NavigationService.ShowPopup(new NewBotPopup(), new NewBotArgs(botUser.Id, false, requestManagedBot));
                }
            }
        }

        public async void OpenMiniApp(string url)
        {
            var chat = _chat;
            if (chat == null || !ClientService.TryGetUser(chat, out Td.Api.User botUser))
            {
                return;
            }

            var info = await ClientService.SendAsync(new GetInternalLinkType(url));
            if (info is InternalLinkTypeWebApp webApp)
            {
                MessageHelper.NavigateToWebApp(ClientService, NavigationService, webApp.BotUsername, webApp.StartParameter, webApp.WebAppShortName, webApp.Mode, new OpenUrlSourceChat(chat.Id, null));
            }
            else
            {
                var response = await ClientService.SendAsync(new OpenWebApp(chat.Id, botUser.Id, url, OutgoingTopicId, null, new WebAppOpenParameters(Theme.Current.Parameters, Constants.WebAppHostName, new WebAppOpenModeFullSize())));
                if (response is WebAppInfo webAppInfo)
                {
                    NavigationService.NavigateToWebApp(botUser, webAppInfo.Url, webAppInfo.LaunchId, null, source: new OpenUrlSourceChat(ChatId, null));
                }
            }
        }

        public async void OpenMiniApp(AttachmentMenuBot menuBot)
        {
            var chat = _chat;
            if (chat == null || !ClientService.TryGetUser(menuBot.BotUserId, out Td.Api.User botUser))
            {
                return;
            }

            var response = await ClientService.SendAsync(new OpenWebApp(chat.Id, menuBot.BotUserId, string.Empty, OutgoingTopicId, null, new WebAppOpenParameters(Theme.Current.Parameters, Constants.WebAppHostName, new WebAppOpenModeFullSize())));
            if (response is WebAppInfo webAppInfo)
            {
                NavigationService.NavigateToWebApp(botUser, webAppInfo.Url, webAppInfo.LaunchId, menuBot, source: new OpenUrlSourceChat(ChatId, null));
            }
        }

        public async void RemoveMiniApp(AttachmentMenuBot bot)
        {
            var confirm = await ShowPopupAsync(string.Format(Strings.BotRemoveFromMenu, bot.Name), Strings.BotRemoveFromMenuTitle, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new ToggleBotIsAddedToAttachmentMenu(bot.BotUserId, false, false));
            }
        }

        public async void OpenWebView()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ContentDialogResult.Primary != await ShowPopupAsync(string.Format(Strings.BotOpenPageMessage, chat.Title), Strings.BotOpenPageTitle, Strings.OK, Strings.Cancel))
            {
                return;
            }
        }

        #endregion

        #region Sticker info

        public void AddStickerFromMessage(MessageViewModel message)
        {
            if (message.Content is MessageSticker sticker && sticker.Sticker.SetId != 0)
            {
                OpenSticker(sticker.Sticker);
            }
            else if (message.Content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeSticker previewSticker && previewSticker.Sticker.SetId != 0)
            {
                OpenSticker(previewSticker.Sticker);
            }
        }

        #endregion

        #region Fave sticker

        public void AddFavoriteSticker(MessageViewModel message)
        {
            var sticker = message.Content as MessageSticker;
            if (sticker == null)
            {
                return;
            }

            ClientService.Send(new AddFavoriteSticker(new InputFileId(sticker.Sticker.StickerValue.Id)));
            ShowToast(Strings.AddedToFavorites, ToastPopupIcon.Info);
        }

        #endregion

        #region Unfave sticker

        public void RemoveFavoriteSticker(MessageViewModel message)
        {
            var sticker = message.Content as MessageSticker;
            if (sticker == null)
            {
                return;
            }

            ClientService.Send(new RemoveFavoriteSticker(new InputFileId(sticker.Sticker.StickerValue.Id)));
            ShowToast(Strings.RemovedFromFavorites, ToastPopupIcon.Info);
        }

        #endregion

        #region Save file as

        public async void SaveMessageMedia(MessageViewModel message)
        {
            if (message.Content is MessageAlbum album)
            {
                await _storageService.SaveFilesAsync(album.Messages.Select(x => x.GetFile()));
            }
            else
            {
                var file = message.GetFile();
                if (file != null)
                {
                    await _storageService.SaveFileAsAsync(file);
                }
            }
        }

        #endregion

        #region Save to GIFs

        public void SaveMessageAnimation(MessageViewModel message)
        {
            if (message.Content is MessageAnimation animation)
            {
                ClientService.Send(new AddSavedAnimation(new InputFileId(animation.Animation.AnimationValue.Id)));
            }
            else if (message.Content is MessageText text && text.LinkPreview != null && text.LinkPreview.Type is LinkPreviewTypeAnimation previewAnimation)
            {
                ClientService.Send(new AddSavedAnimation(new InputFileId(previewAnimation.Animation.AnimationValue.Id)));
            }

            ShowToast(Strings.GifSavedHint, ToastPopupIcon.Gif);
        }

        #endregion

        #region Save for Notifications

        public void SaveMessageNotificationSound(MessageViewModel message)
        {
            if (message.Content is MessageAudio audio)
            {
                ClientService.Send(new AddSavedNotificationSound(new InputFileId(audio.Audio.AudioValue.Id)));
            }
            if (message.Content is MessageVoiceNote voiceNote)
            {
                ClientService.Send(new AddSavedNotificationSound(new InputFileId(voiceNote.VoiceNote.Voice.Id)));
            }
            else if (message.Content is MessageText text && text.LinkPreview != null)
            {
                if (text.LinkPreview.Type is LinkPreviewTypeAudio previewAudio)
                {
                    ClientService.Send(new AddSavedNotificationSound(new InputFileId(previewAudio.Audio.AudioValue.Id)));
                }
                else if (text.LinkPreview.Type is LinkPreviewTypeVoiceNote previewVoiceNote)
                {
                    ClientService.Send(new AddSavedNotificationSound(new InputFileId(previewVoiceNote.VoiceNote.Voice.Id)));
                }
            }

            var title = Strings.SoundAdded + Environment.NewLine + Strings.SoundAddedSubtitle;
            var entity = new TextEntity(0, Strings.SoundAdded.Length, new TextEntityTypeBold());

            var temp = new FormattedText(title, new[] { entity });
            var markdown = ClientEx.ParseMarkdown(temp);

            ToastPopup.Show(XamlRoot, markdown, ToastPopupIcon.SoundDownload);
        }

        #endregion

        #region Open with

        public async void OpenMessageWith(MessageViewModel message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenFileWithAsync(file);
            }
        }

        #endregion

        #region View in chat

        public void ViewMessageInChat(MessageViewModel message)
        {
            NavigationService.NavigateToChat(message.Chat, message.Id, TopicId);
        }

        #endregion

        #region Show in folder

        public async void OpenMessageFolder(MessageViewModel message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenFolderAsync(file);
            }
        }

        #endregion

        #region Add contact

        public void AddToContacts(MessageViewModel message)
        {
            var contact = message.Content as MessageContact;
            if (contact == null)
            {
                return;
            }

            var user = ClientService.GetUser(contact.Contact.UserId);
            if (user == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(UserEditPage), user.Id);
        }

        #endregion

        #region Service message

        public async void ExecuteServiceMessage(MessageViewModel message)
        {
            if (message.Content is MessageChatUpgradeFrom chatUpgradeFrom)
            {
                var response = await ClientService.SendAsync(new CreateBasicGroupChat(chatUpgradeFrom.BasicGroupId, false));
                if (response is Chat migratedChat)
                {
                    NavigationService.NavigateToChat(migratedChat);
                }
            }
            if (message.Content is MessageChatUpgradeTo chatUpgradeTo)
            {
                var response = await ClientService.SendAsync(new CreateSupergroupChat(chatUpgradeTo.SupergroupId, false));
                if (response is Chat migratedChat)
                {
                    NavigationService.NavigateToChat(migratedChat);
                }
            }
            else if (message.Content is MessageHeaderDate && Type is DialogType.History or DialogType.Thread)
            {
                var date = Formatter.ToLocalTime(message.Date);

                var dialog = new CalendarPopup(ClientService, ChatId, TopicId, date);
                dialog.MaxDate = DateTimeOffset.Now.Date;

                var confirm = await ShowPopupAsync(dialog);
                if (confirm == ContentDialogResult.Primary && dialog.SelectedDates.Count > 0)
                {
                    var first = dialog.SelectedDates.FirstOrDefault();
                    var offset = first.Date.ToTimestamp();

                    await LoadDateSliceAsync(offset);
                }
            }
            else if (message.Content is MessagePinMessage pinMessage && pinMessage.MessageId != 0)
            {
                if (message.ReplyToState != MessageReplyToState.Deleted)
                {
                    await LoadMessageSliceAsync(message.Id, pinMessage.MessageId);
                }
            }
            else if (message.Content is MessageGameScore gameScore && gameScore.GameMessageId != 0)
            {
                if (message.ReplyToState != MessageReplyToState.Deleted)
                {
                    await LoadMessageSliceAsync(message.Id, gameScore.GameMessageId);
                }
            }
            else if (message.Content is MessageChecklistTasksAdded checklistTasksAdded && checklistTasksAdded.ChecklistMessageId != 0)
            {
                if (message.ReplyToState != MessageReplyToState.Deleted)
                {
                    await LoadMessageSliceAsync(message.Id, checklistTasksAdded.ChecklistMessageId, checklistTaskId: checklistTasksAdded.Tasks[0].Id);
                }
            }
            else if (message.Content is MessageChecklistTasksDone checklistTasksDone && checklistTasksDone.ChecklistMessageId != 0)
            {
                if (message.ReplyToState != MessageReplyToState.Deleted)
                {
                    await LoadMessageSliceAsync(message.Id, checklistTasksDone.ChecklistMessageId, checklistTaskId: checklistTasksDone.MarkedAsDoneTaskIds.Count > 0 ? checklistTasksDone.MarkedAsDoneTaskIds[0] : checklistTasksDone.MarkedAsNotDoneTaskIds[0]);
                }
            }
            else if (message.Content is MessagePollOptionAdded pollOptionAdded && pollOptionAdded.PollMessageId != 0)
            {
                if (message.ReplyToState != MessageReplyToState.Deleted)
                {
                    await LoadMessageSliceAsync(message.Id, pollOptionAdded.PollMessageId, pollOptionId: pollOptionAdded.OptionId);
                }
            }
            else if (message.Content is MessagePollOptionDeleted pollOptionDeleted && pollOptionDeleted.PollMessageId != 0)
            {
                if (message.ReplyToState != MessageReplyToState.Deleted)
                {
                    await LoadMessageSliceAsync(message.Id, pollOptionDeleted.PollMessageId, pollOptionId: pollOptionDeleted.OptionId);
                }
            }
            else if (message.Content is MessageSuggestedPostPaid suggestedPostPaid && suggestedPostPaid.SuggestedPostMessageId != 0)
            {
                if (message.ReplyToState != MessageReplyToState.Deleted)
                {
                    await LoadMessageSliceAsync(message.Id, suggestedPostPaid.SuggestedPostMessageId);
                }
            }
            else if (message.Content is MessageSuggestedPostRefunded suggestedPostRefunded && suggestedPostRefunded.SuggestedPostMessageId != 0)
            {
                if (message.ReplyToState != MessageReplyToState.Deleted)
                {
                    await LoadMessageSliceAsync(message.Id, suggestedPostRefunded.SuggestedPostMessageId);
                }
            }
            else if (message.Content is MessageChatEvent chatEvent)
            {
                if (chatEvent.Action is ChatEventStickerSetChanged stickerSetChanged && stickerSetChanged.NewStickerSetId != 0)
                {
                    await StickersPopup.ShowAsync(NavigationService, stickerSetChanged.NewStickerSetId);
                }
                else if (chatEvent.Action is ChatEventCustomEmojiStickerSetChanged customEmojiStickerSetChanged && customEmojiStickerSetChanged.NewStickerSetId != 0)
                {
                    await StickersPopup.ShowAsync(NavigationService, customEmojiStickerSetChanged.NewStickerSetId);
                }
            }
            else if (message.Content is MessageVideoChatStarted or MessageVideoChatScheduled)
            {
                _voipService.JoinGroupCall(NavigationService, message.ChatId);
            }
            else if (message.Content is MessagePaymentSuccessful)
            {
                NavigationService.NavigateToReceipt(message);
            }
            else if (message.Content is MessageChatSetTheme)
            {
                ChangeTheme();
            }
            else if (message.Content is MessageChatChangePhoto chatChangePhoto)
            {
                NavigationService.ShowGallery(new ChatPhotosViewModel(ClientService, StorageService, Aggregator, Chat, chatChangePhoto.Photo));
            }
            else if (message.Content is MessageSuggestProfilePhoto suggestProfilePhoto)
            {
                if (message.IsOutgoing)
                {
                    NavigationService.ShowGallery(new ChatPhotosViewModel(ClientService, StorageService, Aggregator, Chat, suggestProfilePhoto.Photo));
                }
                else
                {
                    var file = suggestProfilePhoto.Photo.Animation?.File
                        ?? suggestProfilePhoto.Photo.GetBig()?.Photo;

                    var cached = await ClientService.GetFileAsync(file);
                    if (cached == null)
                    {
                        return;
                    }

                    var media = await StorageMedia.CreateAsync(cached);
                    var popup = new EditMediaPopup(media, ImageCropperMask.Ellipse);

                    var confirm = await popup.ShowAsync(XamlRoot);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await EditPhotoAsync(media);
                    }
                }
            }
            else if (message.Content is MessageSuggestBirthdate suggestBirthdate && !message.IsOutgoing)
            {
                var popup = new SettingsBirthdatePopup(suggestBirthdate.Birthdate, suggested: true);

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new SetBirthdate(popup.Value));

                    ShowToast(string.Format("**{0}**\n{1}", Strings.PrivacyBirthdaySetDone, Strings.PrivacyBirthdaySetDoneInfo), ToastPopupIcon.Gift);
                }
            }
            else if (message.Content is MessageChatSetBackground chatSetBackground)
            {
                if (chatSetBackground.OldBackgroundMessageId != 0)
                {
                    await LoadMessageSliceAsync(message.Id, chatSetBackground.OldBackgroundMessageId);
                }
                else if (message.IsOutgoing)
                {
                    ChangeTheme();
                }
                else
                {
                    var userFull = message.ClientService.GetUserFull(message.Chat);
                    var sameBackground = chatSetBackground.Background.Background.Id == message.Chat.Background?.Background.Id;

                    if (sameBackground && (userFull == null || userFull.SetChatBackground))
                    {
                        var confirm = await ShowPopupAsync(Strings.RemoveWallpaperMessage, Strings.RemoveWallpaperTitle, Strings.RemoveWallpaperAction, Strings.Cancel, destructive: true);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            ClientService.Send(new DeleteChatBackground(message.ChatId, true));
                        }
                    }
                    else
                    {
                        await ShowPopupAsync(new BackgroundPopup(), new BackgroundParameters(chatSetBackground.Background.Background, message.ChatId, message.Id));
                    }
                }
            }
            else if (message.Content is MessagePremiumGiftCode premiumGiftCode)
            {
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, new InternalLinkTypePremiumGiftCode(premiumGiftCode.Code), new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
            else if (message.Content is MessageGiveawayCompleted giveawayCompleted)
            {
                await LoadMessageSliceAsync(message.Id, giveawayCompleted.GiveawayMessageId);
            }
            else if (message.Content is MessageGift gift)
            {
                ReceivedGift receivedGift = null;
                if (gift.UpgradedReceivedGiftId.Length > 0)
                {
                    receivedGift = await ClientService.SendAsync(new GetReceivedGift(gift.UpgradedReceivedGiftId)) as ReceivedGift;
                }
                else if (gift.ReceivedGiftId.Length > 0)
                {
                    receivedGift = await ClientService.SendAsync(new GetReceivedGift(gift.ReceivedGiftId)) as ReceivedGift;
                }

                receivedGift ??= new ReceivedGift(gift.ReceivedGiftId, gift.SenderId, gift.Text, gift.UniqueGiftNumber, gift.IsPrivate, gift.IsSaved, false, gift.CanBeUpgraded && !gift.WasUpgraded, false, gift.WasRefunded, message.Date, new SentGiftRegular(gift.Gift), Array.Empty<int>(), gift.SellStarCount, gift.PrepaidUpgradeStarCount, gift.IsUpgradeSeparate, 0, 0, 0, 0, 0, gift.PrepaidUpgradeHash, 0);

                ShowPopup(new ReceivedGiftPopup(ClientService, NavigationService, receivedGift, gift.ReceiverId, null));
            }
            else if (message.Content is MessageUpgradedGift upgradedGift)
            {
                ReceivedGift receivedGift = null;
                if (upgradedGift.ReceivedGiftId.Length > 0)
                {
                    receivedGift = await ClientService.SendAsync(new GetReceivedGift(upgradedGift.ReceivedGiftId)) as ReceivedGift;
                }

                receivedGift ??= new ReceivedGift(upgradedGift.ReceivedGiftId, upgradedGift.SenderId, upgradedGift.Gift.OriginalDetails?.Text ?? string.Empty.AsFormattedText(), 0, true, upgradedGift.IsSaved, false, false, upgradedGift.CanBeTransferred, false, message.Date, new SentGiftUpgraded(upgradedGift.Gift), Array.Empty<int>(), 0, 0, false, upgradedGift.TransferStarCount, upgradedGift.DropOriginalDetailsStarCount, upgradedGift.NextTransferDate, upgradedGift.NextResaleDate, upgradedGift.ExportDate, string.Empty, upgradedGift.CraftDate);

                ShowPopup(new ReceivedGiftPopup(ClientService, NavigationService, receivedGift, upgradedGift.ReceiverId, null));
            }
            else if (message.Content is MessageGiftedStars giftedStars)
            {
                var type = new StarTransactionTypeUserDeposit(giftedStars.GifterUserId, giftedStars.Sticker);
                var amount = new StarAmount(giftedStars.StarCount, 0);
                var transaction = new StarTransaction(giftedStars.TransactionId, amount, false, message.Date, type);

                ShowPopup(new Views.Stars.Popups.ReceiptPopup(ClientService, NavigationService, transaction));
            }
            else if (message.Content is MessageGiftedPremium giftedPremium)
            {
                ShowPopup(new Views.Premium.Popups.PromoPopup(ClientService, giftedPremium));
            }
        }

        public async Task EditPhotoAsync(StorageMedia file)
        {
            if (file is StorageVideo media)
            {
                var props = await media.File.Properties.GetVideoPropertiesAsync();

                var duration = media.EditState.TrimStopTime - media.EditState.TrimStartTime;
                var seconds = duration.TotalSeconds;

                var conversion = new VideoGeneration();
                conversion.Mute = true;
                conversion.TrimStartTime = media.EditState.TrimStartTime;
                conversion.TrimStopTime = media.EditState.TrimStartTime + TimeSpan.FromSeconds(Math.Min(seconds, 9.9));
                conversion.Transcode = true;
                conversion.Transform = true;
                //conversion.Rotation = file.EditState.Rotation;
                conversion.OutputSize = new Size(640, 640);
                //conversion.Mirror = transform.Mirror;
                conversion.CropRectangle = new Rect(
                    media.EditState.Rectangle.X * props.Width,
                    media.EditState.Rectangle.Y * props.Height,
                    media.EditState.Rectangle.Width * props.Width,
                    media.EditState.Rectangle.Height * props.Height);

                var rectangle = conversion.CropRectangle;
                rectangle.Width = Math.Min(conversion.CropRectangle.Width, conversion.CropRectangle.Height);
                rectangle.Height = rectangle.Width;

                conversion.CropRectangle = rectangle;

                var serialized = JsonSerializer.Serialize(conversion, GenerationJsonContext.Default.VideoGeneration);
                var generated = await media.File.ToGeneratedAsync(ConversionType.Transcode, serialized);
                var response = await ClientService.SendAsync(new SetProfilePhoto(new InputChatPhotoAnimation(generated, 0), false));
            }
            else if (file is StoragePhoto photo)
            {
                var serialized = JsonSerializer.Serialize(photo.EditState, GenerationJsonContext.Default.ImageGeneration);
                var generated = await photo.File.ToGeneratedAsync(ConversionType.Compress, serialized);
                var response = await ClientService.SendAsync(new SetProfilePhoto(new InputChatPhotoStatic(generated), false));
            }
        }

        #endregion

        #region Unvote poll

        public void UnvotePoll(MessageViewModel message)
        {
            var poll = message.Content as MessagePoll;
            if (poll == null)
            {
                return;
            }

            ClientService.Send(new SetPollAnswer(message.ChatId, message.Id, Array.Empty<int>()));
        }

        #endregion

        #region Stop poll

        public async void StopPoll(MessageViewModel message)
        {
            var poll = message.Content as MessagePoll;
            if (poll == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(Strings.StopPollAlertText, Strings.StopPollAlertTitle, Strings.Stop, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ClientService.Send(new StopPoll(message.ChatId, message.Id));
        }

        #endregion

        #region Polls

        public void MarkPollOption(MessagePollOption option)
        {
            if (option.Message.Content is MessagePoll poll)
            {
                var votes = new List<int>();

                for (int i = 0; i < poll.Poll.Options.Count; i++)
                {
                    var item = poll.Poll.Options[i];
                    if (item.Id == option.Option.Id)
                    {
                        if (!item.IsChosen)
                        {
                            votes.Add(i);
                        }
                    }
                    else if (item.IsChosen)
                    {
                        votes.Add(i);
                    }
                }

                VotePoll(option.Message, votes);
            }
        }

        public void DeletePollOption(MessagePollOption option)
        {
            ClientService.SendAsync(new DeletePollOption(option.Message.ChatId, option.Message.Id, option.Option.Id));
        }

        public async void CopyPollOption(MessagePollOption option)
        {
            var response = await ClientService.SendAsync(new GetMessageLink(option.Message.ChatId, option.Message.Id, 0, 0, option.Option.Id, false, ForumTopic != null || Thread != null));
            if (response is MessageLink link)
            {
                MessageHelper.CopyLink(XamlRoot, link.Link, link.IsPublic);
            }
        }

        #endregion

        #region Show emoji

        public async void ShowMessageEmoji(MessageViewModel message)
        {
            var caption = message.GetCaption();
            if (caption == null)
            {
                return;
            }

            var emoji = new HashSet<long>();

            foreach (var item in caption.Entities)
            {
                if (item.Type is TextEntityTypeCustomEmoji customEmoji)
                {
                    emoji.Add(customEmoji.CustomEmojiId);
                }
            }

            var response = await ClientService.SendAsync(new GetCustomEmojiStickers(emoji.ToList()));
            if (response is Stickers stickers)
            {
                var sets = new HashSet<long>();

                foreach (var sticker in stickers.StickersValue)
                {
                    sets.Add(sticker.SetId);
                }

                await StickersPopup.ShowAsync(NavigationService, sets);
            }
        }

        #endregion
    }
}
