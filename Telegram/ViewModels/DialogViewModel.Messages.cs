//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Gallery;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Telegram.Views;
using Telegram.Views.Chats;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static Telegram.Services.GenerationService;

namespace Telegram.ViewModels
{
    public class MessageQuote
    {
        public MessageViewModel Message { get; set; }

        public FormattedText Quote { get; set; }
    }

    public partial class DialogViewModel
    {
        #region Reply

        public async void MessageReplyPrevious()
        {
            MessageViewModel last = null;

            var data = _composerHeader;
            if (data != null && data.ReplyToMessage != null)
            {
                last = Items.Reverse().FirstOrDefault(x => x.Id != 0 && x.Id < data.ReplyToMessage.Id) ?? Items.LastOrDefault();
            }
            else
            {
                last = Items.LastOrDefault();
            }

            if (last != null)
            {
                ReplyToMessage(last);
                await HistoryField?.ScrollToItem(last, VerticalAlignment.Center, new MessageBubbleHighlightOptions());
            }
        }

        public async void MessageReplyNext()
        {
            MessageViewModel last = null;

            var data = _composerHeader;
            if (data != null && data.ReplyToMessage != null)
            {
                last = Items.FirstOrDefault(x => x.Id != 0 && x.Id > data.ReplyToMessage.Id);
            }

            if (last != null)
            {
                ReplyToMessage(last);
                await HistoryField?.ScrollToItem(last, VerticalAlignment.Center, new MessageBubbleHighlightOptions());
            }
            else
            {
                ClearReply();
            }
        }

        public async void ReplyToMessage(MessageViewModel message)
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

            if (ShouldReplyInAnotherChat(message))
            {
                await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationReplyToMessage(message.Get(), null));
            }
            else
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    ReplyToMessage = message
                };

                TextField?.Focus(FocusState.Keyboard);
            }
        }

        public async void QuoteToMessage(MessageQuote quote)
        {
            DisposeSearch();

            var message = quote?.Message;
            if (message == null)
            {
                return;
            }

            if (message.Content is MessageAlbum album)
            {
                message = album.Messages.FirstOrDefault();
            }

            if (ShouldReplyInAnotherChat(message))
            {
                await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationReplyToMessage(message.Get(), quote.Quote));
            }
            else
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    ReplyToMessage = message,
                    ReplyToQuote = quote.Quote
                };

                TextField?.Focus(FocusState.Keyboard);
            }
        }

        private bool ShouldReplyInAnotherChat(MessageViewModel message)
        {
            if (message.CanBeRepliedInAnotherChat is false)
            {
                return false;
            }

            var chat = message.Chat;
            if (chat != null && ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.IsChannel)
                {
                    return supergroup.Status is not ChatMemberStatusCreator and not ChatMemberStatusAdministrator;
                }
            }

            return false;
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

        private async void DeleteMessages(Chat chat, IList<MessageViewModel> messages)
        {
            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var items = messages.Select(x => x.Get()).ToArray();

            var response = await ClientService.SendAsync(new GetMessages(chat.Id, items.Select(x => x.Id).ToArray()));
            if (response is Messages updated)
            {
                for (int i = 0; i < updated.MessagesValue.Count; i++)
                {
                    items[i] = updated.MessagesValue[i];
                }
            }

            var sameUser = messages.All(x => x.SenderId.AreTheSame(first.SenderId));
            var dialog = new DeleteMessagesPopup(ClientService, items.Where(x => x != null).ToArray());

            var confirm = await ShowPopupAsync(dialog);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            IsSelectionEnabled = false;

            if (dialog.DeleteAll && sameUser)
            {
                ClientService.Send(new DeleteChatMessagesBySender(chat.Id, first.SenderId));
            }
            else
            {
                ClientService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), dialog.Revoke));
            }

            if (dialog.BanUser && sameUser)
            {
                ClientService.Send(new SetChatMemberStatus(chat.Id, first.SenderId, new ChatMemberStatusBanned()));
            }

            if (dialog.ReportSpam && sameUser && chat.Type is ChatTypeSupergroup supertype)
            {
                ClientService.Send(new ReportSupergroupSpam(supertype.SupergroupId, messages.Select(x => x.Id).ToList()));
            }
        }

        #endregion

        #region Forward

        public async void ForwardMessage(MessageViewModel message)
        {
            IsSelectionEnabled = false;

            if (message.Content is MessageAlbum album)
            {
                await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareMessages(album.Messages.Select(x => x.Get()).ToList()));
            }
            else
            {
                await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareMessage(message.Get()));
            }

            TextField?.Focus(FocusState.Programmatic);
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

        public bool CanDeleteSelectedMessages => SelectedItems.Count > 0 && SelectedItems.Values.All(x => x.CanBeDeletedForAllUsers || x.CanBeDeletedOnlyForSelf);

        #endregion

        #region Multiple Forward

        public async void ForwardSelectedMessages()
        {
            var messages = SelectedItems.Values.Where(x => x.CanBeForwarded).OrderBy(x => x.Id).Select(x => x.Get()).ToList();
            if (messages.Count > 0)
            {
                IsSelectionEnabled = false;

                await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareMessages(messages));
                TextField?.Focus(FocusState.Programmatic);
            }
        }

        public bool CanForwardSelectedMessages => SelectedItems.Count > 0 && SelectedItems.Values.All(x => x.CanBeForwarded);

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
                    };
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

                    var date = Formatter.ToLocalTime(message.Date);
                    builder.AppendLine(string.Format("{0}, [{1} {2}]", title, Formatter.ShortDate.Format(date), Formatter.ShortTime.Format(date)));

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
                        if (ClientService.TryGetUser(replyToStory.SenderChatId, out Telegram.Td.Api.User replyUser))
                        {
                            builder.AppendLine($"[In reply to {replyUser.FullName()}]");
                        }
                    }

                    if (message.Content is MessagePhoto photo)
                    {
                        builder.Append($"[{Strings.AttachPhoto}]");

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
                        builder.AppendLine($"[{Strings.Poll}: {poll.Poll.Question}");

                        foreach (var option in poll.Poll.Options)
                        {
                            builder.AppendLine($"- {option.Text}");
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

                MessageHelper.CopyText(builder.ToString());
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
            NavigationService.Navigate(typeof(MessageStatisticsPage), $"{message.ChatId};{message.Id}");
        }

        #endregion

        #region Resend

        public void ResendMessage(MessageViewModel message)
        {
            ClientService.Send(new ResendMessages(message.ChatId, new[] { message.Id }, null));
        }

        #endregion

        #region Copy

        public async void CopyMessage(MessageViewModel message)
        {
            if (message == null)
            {
                return;
            }

            var input = message.GetCaption();
            if (message.Content is MessageContact contact)
            {
                input = new FormattedText(PhoneNumber.Format(contact.Contact.PhoneNumber), new TextEntity[0]);
            }
            else if (message.Content is MessageAnimatedEmoji animatedEmoji)
            {
                input = new FormattedText(animatedEmoji.Emoji, new TextEntity[0]);
            }

            if (input != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(input.Text);

                if (input.Entities.Count > 0)
                {
                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                        {
                            writer.WriteInt32(input.Entities.Count(x => x.IsEditable()));

                            foreach (var entity in input.Entities.Where(x => x.IsEditable()))
                            {
                                writer.WriteInt32(entity.Offset);
                                writer.WriteInt32(entity.Length);

                                switch (entity.Type)
                                {
                                    case TextEntityTypeBold bold:
                                        writer.WriteByte(1);
                                        break;
                                    case TextEntityTypeItalic italic:
                                        writer.WriteByte(2);
                                        break;
                                    case TextEntityTypeCode code:
                                    case TextEntityTypePre pre:
                                    case TextEntityTypePreCode preCode:
                                        writer.WriteByte(3);
                                        break;
                                    case TextEntityTypeTextUrl textUrl:
                                        writer.WriteByte(4);
                                        writer.WriteInt32(textUrl.Url.Length);
                                        writer.WriteString(textUrl.Url);
                                        break;
                                    case TextEntityTypeMentionName mentionName:
                                        writer.WriteByte(5);
                                        writer.WriteInt64(mentionName.UserId);
                                        break;
                                }
                            }

                            await writer.FlushAsync();
                            await writer.StoreAsync();
                        }

                        stream.Seek(0);
                        dataPackage.SetData("application/x-tl-field-tags", stream.CloneStream());
                    }
                }

                ClipboardEx.TrySetContent(dataPackage);

                Window.Current.ShowToast(Strings.TextCopied, new LocalFileSource("ms-appx:///Assets/Toasts/Copied.tgs"));
            }
        }

        public async void CopyMessage(MessageQuote quote)
        {
            var input = quote?.Quote;
            if (input != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(input.Text);

                if (input.Entities.Count > 0)
                {
                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                        {
                            writer.WriteInt32(input.Entities.Count(x => x.IsEditable()));

                            foreach (var entity in input.Entities.Where(x => x.IsEditable()))
                            {
                                writer.WriteInt32(entity.Offset);
                                writer.WriteInt32(entity.Length);

                                switch (entity.Type)
                                {
                                    case TextEntityTypeBold bold:
                                        writer.WriteByte(1);
                                        break;
                                    case TextEntityTypeItalic italic:
                                        writer.WriteByte(2);
                                        break;
                                    case TextEntityTypeCode code:
                                    case TextEntityTypePre pre:
                                    case TextEntityTypePreCode preCode:
                                        writer.WriteByte(3);
                                        break;
                                    case TextEntityTypeTextUrl textUrl:
                                        writer.WriteByte(4);
                                        writer.WriteInt32(textUrl.Url.Length);
                                        writer.WriteString(textUrl.Url);
                                        break;
                                    case TextEntityTypeMentionName mentionName:
                                        writer.WriteByte(5);
                                        writer.WriteInt64(mentionName.UserId);
                                        break;
                                }
                            }

                            await writer.FlushAsync();
                            await writer.StoreAsync();
                        }

                        stream.Seek(0);
                        dataPackage.SetData("application/x-tl-field-tags", stream.CloneStream());
                    }
                }

                ClipboardEx.TrySetContent(dataPackage);

                Window.Current.ShowToast(Strings.TextCopied, new LocalFileSource("ms-appx:///Assets/Toasts/Copied.tgs"));
            }
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

            var response = await ClientService.SendAsync(new GetMessageLink(chat.Id, message.Id, 0, false, ThreadId != 0));
            if (response is MessageLink link)
            {
                MessageHelper.CopyLink(link.Link, link.IsPublic);
            }
        }

        #endregion

        #region Edit

        public async void EditLastMessage()
        {
            var last = Items.LastOrDefault(x => x.CanBeEdited);
            if (last != null)
            {
                EditMessage(last);
                await HistoryField?.ScrollToItem(last, VerticalAlignment.Center, new MessageBubbleHighlightOptions());
            }
        }

        public void EditMessage(MessageViewModel message)
        {
            if (message.Content is MessageAlbum album)
            {
                if (album.IsMedia)
                {
                    message = null;

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

            CurrentInlineBot = null;
            DisposeSearch();
            SaveDraft();

            var input = message.GetCaption();
            var container = new MessageComposerHeader(ClientService)
            {
                EditingMessage = message
            };

            if (message.Content is MessageText text)
            {
                if (text.WebPage != null)
                {
                    container.WebPagePreview = text.WebPage;
                    container.WebPageUrl = text.WebPage.Url;
                }
                else
                {
                    var url = text.Text.Entities.FirstOrDefault(x => x.Type is TextEntityTypeUrl);
                    if (url != null)
                    {
                        container.WebPageUrl = text.Text.Text.Substring(url.Offset, url.Length);
                        container.WebPageDisabled = true;
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
            if (response is MessageThreadInfo)
            {
                NavigationService.NavigateToThread(message.ChatId, message.Id, message.Id);
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
            await ReportAsync(new[] { message.Id });
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

        public void SendNowMessage(MessageViewModel message)
        {
            ClientService.Send(new EditMessageSchedulingState(message.ChatId, message.Id, null));
        }

        #endregion

        #region Reschedule

        public async void RescheduleMessage(MessageViewModel message)
        {
            var options = await PickMessageSendOptionsAsync(true);
            if (options?.SchedulingState == null)
            {
                return;
            }

            ClientService.Send(new EditMessageSchedulingState(message.ChatId, message.Id, options.SchedulingState));
        }

        #endregion

        #region Interactions

        public async void ShowMessageInteractions(MessageViewModel message)
        {
            await ShowPopupAsync(typeof(InteractionsPopup), new MessageReplyToMessage(message.ChatId, message.Id, null, false, null, 0, null));
        }

        #endregion

        #region Translate

        public async void TranslateMessage(MessageViewModel message)
        {
            string text;
            long chatId;
            long messageId;

            if (message.Content is MessagePoll poll)
            {
                var builder = new StringBuilder(poll.Poll.Question);

                foreach (var option in poll.Poll.Options)
                {
                    builder.AppendLine();
                    builder.AppendFormat("\U0001F518 {0}", option.Text);
                }

                text = builder.ToString();
                chatId = 0;
                messageId = 0;
            }
            else
            {
                var caption = message.GetCaption();
                if (string.IsNullOrEmpty(caption?.Text))
                {
                    return;
                }

                text = caption.Text;
                chatId = message.ChatId;
                messageId = message.Id;
            }

            var language = LanguageIdentification.IdentifyLanguage(text);
            var popup = new TranslatePopup(_translateService, chatId, messageId, text, language, LocaleService.Current.CurrentCulture.TwoLetterISOLanguageName, !message.CanBeSaved);
            await ShowPopupAsync(popup);
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
                    var dialog = new LoginUrlInfoPopup(ClientService, requestConfirmation);
                    var confirm = await ShowPopupAsync(dialog);
                    if (confirm != ContentDialogResult.Primary || !dialog.HasAccepted)
                    {
                        return;
                    }

                    response = await ClientService.SendAsync(new GetLoginUrl(chat.Id, message.Id, loginUrl.Id, dialog.HasWriteAccess));
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
                    await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationSwitchInline(switchInline.Query, switchInline.TargetChat, bot));
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeUrl urlButton)
            {
                MessageHelper.OpenUrl(ClientService, NavigationService, urlButton.Url, true, new OpenUrlSourceChat(chat.Id));
            }
            else if (inline.Type is InlineKeyboardButtonTypeCallback callback)
            {
                var bot = message.GetViaBotUser();
                if (bot != null)
                {
                    InformativeMessage = CreateMessage(new Message(0, new MessageSenderUser(bot.Id), 0, null, null, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText(Strings.Loading, new TextEntity[0]), null, null), null));
                }

                var response = await ClientService.SendAsync(new GetCallbackQueryAnswer(chat.Id, message.Id, new CallbackQueryPayloadData(callback.Data)));
                if (response is CallbackQueryAnswer answer)
                {
                    InformativeMessage = null;

                    if (!string.IsNullOrEmpty(answer.Text))
                    {
                        if (answer.ShowAlert)
                        {
                            await ShowPopupAsync(new MessagePopup(answer.Text));
                        }
                        else
                        {
                            if (bot == null)
                            {
                                // TODO:
                                await ShowPopupAsync(new MessagePopup(answer.Text));
                                return;
                            }

                            InformativeMessage = CreateMessage(new Message(0, new MessageSenderUser(bot.Id), 0, null, null, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText(answer.Text, new TextEntity[0]), null, null), null));
                        }
                    }
                    else if (!string.IsNullOrEmpty(answer.Url))
                    {
                        if (MessageHelper.TryCreateUri(answer.Url, out Uri uri))
                        {
                            if (MessageHelper.IsTelegramUrl(uri))
                            {
                                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, uri);
                            }
                            else
                            {
                                //var dialog = new MessagePopup(response.Result.Url, "Open this link?");
                                //dialog.PrimaryButtonText = "OK";
                                //dialog.SecondaryButtonText = "Cancel";

                                //var result = await ShowPopupAsync(dialog);
                                //if (result != ContentDialogResult.Primary)
                                //{
                                //    return;
                                //}

                                await Launcher.LaunchUriAsync(uri);
                            }
                        }
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
                        if (confirm == ContentDialogResult.Primary && !error.Message.Equals("PASSWORD_MISSING"))
                        {
                            NavigationService.Navigate(typeof(SettingsPasswordPage));
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
            else if (inline.Type is InlineKeyboardButtonTypeWebApp webApp)
            {
                var bot = message.GetViaBotUser();
                if (bot == null)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new OpenWebApp(chat.Id, bot.Id, webApp.Url, Theme.Current.Parameters, Strings.AppName, ThreadId, null));
                if (response is WebAppInfo webAppInfo)
                {
                    await ShowPopupAsync(new WebBotPopup(ClientService, NavigationService, bot, webAppInfo, null, chat));
                }
            }
        }

        public async void KeyboardButtonExecute(MessageViewModel message, KeyboardButton keyboardButton)
        {
            if (_chat is not Chat chat)
            {
                return;
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
                    var location = await _locationService.GetPositionAsync();
                    if (location != null)
                    {
                        await SendMessageAsync(null, new InputMessageLocation(location, 0, 0, 0), null);
                    }
                }
            }
            else if (keyboardButton.Type is KeyboardButtonTypeRequestPoll requestPoll)
            {
                await SendPollAsync(requestPoll.ForceQuiz, requestPoll.ForceRegular, _chat?.Type is ChatTypeSupergroup super && super.IsChannel);
            }
            else if (keyboardButton.Type is KeyboardButtonTypeText)
            {
                var input = new InputMessageText(new FormattedText(keyboardButton.Text, null), null, true);
                await SendMessageAsync(chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup ? new InputMessageReplyToMessage(0, message.Id, null) : null, input, null);
            }
            else if (keyboardButton.Type is KeyboardButtonTypeWebApp webApp && message.SenderId is MessageSenderUser bot)
            {
                var user = ClientService.GetUser(bot.UserId);
                if (user == null)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new OpenWebApp(chat.Id, bot.UserId, webApp.Url, Theme.Current.Parameters, Strings.AppName, ThreadId, null));
                if (response is WebAppInfo webAppInfo)
                {
                    await ShowPopupAsync(new WebBotPopup(ClientService, NavigationService, user, webAppInfo, null, chat));
                }
            }
        }

        public async void OpenMiniApp(AttachmentMenuBot bot)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = ClientService.GetUser(bot.BotUserId);
            if (user == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new OpenWebApp(chat.Id, bot.BotUserId, string.Empty, Theme.Current.Parameters, Strings.AppName, ThreadId, null));
            if (response is WebAppInfo webAppInfo)
            {
                await ShowPopupAsync(new WebBotPopup(ClientService, NavigationService, user, webAppInfo, bot, chat));
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
            else if (message.Content is MessageText text && text.WebPage?.Sticker != null && text.WebPage.Sticker.SetId != 0)
            {
                OpenSticker(text.WebPage.Sticker);
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
        }

        #endregion

        #region Save file as

        public async void SaveMessageMedia(MessageViewModel message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.SaveFileAsAsync(file);
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
            else if (message.Content is MessageText text && text.WebPage != null && text.WebPage.Animation != null)
            {
                ClientService.Send(new AddSavedAnimation(new InputFileId(text.WebPage.Animation.AnimationValue.Id)));
            }
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
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    ClientService.Send(new AddSavedNotificationSound(new InputFileId(text.WebPage.Audio.AudioValue.Id)));
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    ClientService.Send(new AddSavedNotificationSound(new InputFileId(text.WebPage.VoiceNote.Voice.Id)));
                }
            }
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
            else if (message.Content is MessageHeaderDate)
            {
                var date = Formatter.ToLocalTime(message.Date);

                var dialog = new CalendarPopup(date);
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
                await LoadMessageSliceAsync(message.Id, pinMessage.MessageId);
            }
            else if (message.Content is MessageGameScore gameScore && gameScore.GameMessageId != 0)
            {
                await LoadMessageSliceAsync(message.Id, gameScore.GameMessageId);
            }
            else if (message.Content is MessageChatEvent chatEvent)
            {
                if (chatEvent.Action is ChatEventStickerSetChanged stickerSetChanged && stickerSetChanged.NewStickerSetId != 0)
                {
                    await StickersPopup.ShowAsync(stickerSetChanged.NewStickerSetId);
                }
            }
            else if (message.Content is MessageVideoChatStarted or MessageVideoChatScheduled)
            {
                await _voipGroupService.JoinAsync(message.ChatId);
            }
            else if (message.Content is MessagePaymentSuccessful)
            {
                NavigationService.NavigateToInvoice(message);
            }
            else if (message.Content is MessageChatSetTheme)
            {
                ChangeTheme();
            }
            else if (message.Content is MessageChatChangePhoto chatChangePhoto)
            {
                var viewModel = new ChatPhotosViewModel(ClientService, StorageService, Aggregator, Chat, chatChangePhoto.Photo);
                viewModel.NavigationService = NavigationService;
                await GalleryWindow.ShowAsync(viewModel);
            }
            else if (message.Content is MessageSuggestProfilePhoto suggestProfilePhoto)
            {
                if (message.IsOutgoing)
                {
                    var viewModel = new ChatPhotosViewModel(ClientService, StorageService, Aggregator, Chat, suggestProfilePhoto.Photo);
                    viewModel.NavigationService = NavigationService;
                    await GalleryWindow.ShowAsync(viewModel);
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

                    var confirm = await popup.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await EditPhotoAsync(media);
                    }
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
                    var confirm = await ShowPopupAsync(typeof(BackgroundPopup), new BackgroundParameters(chatSetBackground.Background.Background, message.ChatId, message.Id));
                    if (confirm == ContentDialogResult.Primary)
                    {
                        // TODO:
                        //ClientService.Send(new SetChatBackground(Chat.Id, ))
                    }
                }
            }
            else if (message.Content is MessagePremiumGiftCode premiumGiftCode)
            {
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, new InternalLinkTypePremiumGiftCode(premiumGiftCode.Code));
            }
        }

        public async Task EditPhotoAsync(StorageMedia file)
        {
            if (file is StorageVideo media)
            {
                var props = await media.File.Properties.GetVideoPropertiesAsync();

                var duration = media.EditState.TrimStopTime - media.EditState.TrimStartTime;
                var seconds = duration.TotalSeconds;

                var conversion = new VideoConversion();
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

                var generated = await media.File.ToGeneratedAsync(ConversionType.Transcode, JsonConvert.SerializeObject(conversion));
                var response = await ClientService.SendAsync(new SetProfilePhoto(new InputChatPhotoAnimation(generated, 0), false));
            }
            else if (file is StoragePhoto photo)
            {
                var generated = await photo.File.ToGeneratedAsync(ConversionType.Compress, JsonConvert.SerializeObject(photo.EditState));
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

            ClientService.Send(new SetPollAnswer(message.ChatId, message.Id, new int[0]));
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

            ClientService.Send(new StopPoll(message.ChatId, message.Id, null));
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

                await StickersPopup.ShowAsync(sets);
            }
        }

        #endregion
    }
}
