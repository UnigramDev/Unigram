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
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Gallery;
using Unigram.Converters;
using Unigram.Entities;
using Telegram.Native;
using Unigram.Services;
using Unigram.ViewModels.Chats;
using Unigram.Views;
using Unigram.Views.Chats;
using Unigram.Views.Popups;
using Unigram.Views.Settings;
using Unigram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static Unigram.Services.GenerationService;

namespace Unigram.ViewModels
{
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
                MessageReplyCommand.Execute(last);
                await ListField?.ScrollToItem(last, VerticalAlignment.Center, true);
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
                MessageReplyCommand.Execute(last);
                await ListField?.ScrollToItem(last, VerticalAlignment.Center, true);
            }
            else
            {
                ClearReply();
            }
        }

        public RelayCommand<MessageViewModel> MessageReplyCommand { get; }
        private void MessageReplyExecute(MessageViewModel message)
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

            ComposerHeader = new MessageComposerHeader { ReplyToMessage = message };
            TextField?.Focus(FocusState.Keyboard);
        }

        #endregion

        #region Delete

        public RelayCommand<MessageViewModel> MessageDeleteCommand { get; }
        private void MessageDeleteExecute(MessageViewModel message)
        {
            if (message == null)
            {
                return;
            }

            var chat = message.GetChat();
            if (chat == null)
            {
                return;
            }

            //if (message != null && message.Media is TLMessageMediaGroup groupMedia)
            //{
            //    ExpandSelection(new[] { message });
            //    MessagesDeleteExecute();
            //    return;
            //}

            MessagesDelete(chat, new[] { message });

            TextField?.Focus(FocusState.Programmatic);
        }

        private async void MessagesDelete(Chat chat, IList<MessageViewModel> messages)
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

        public RelayCommand<MessageViewModel> MessageForwardCommand { get; }
        private async void MessageForwardExecute(MessageViewModel message)
        {
            IsSelectionEnabled = false;

            if (message.Content is MessageAlbum album)
            {
                await SharePopup.GetForCurrentView().ShowAsync(album.Messages.Select(x => x.Get()).ToList());
            }
            else
            {
                await SharePopup.GetForCurrentView().ShowAsync(message.Get());
            }

            TextField?.Focus(FocusState.Programmatic);
        }

        #endregion

        #region Multiple Delete

        public RelayCommand MessagesDeleteCommand { get; }
        private void MessagesDeleteExecute()
        {
            var messages = new List<MessageViewModel>(SelectedItems.Values);

            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var chat = first.GetChat();
            if (chat == null)
            {
                return;
            }

            MessagesDelete(chat, messages);
        }

        private bool MessagesDeleteCanExecute()
        {
            return SelectedItems.Count > 0 && SelectedItems.Values.All(x => x.CanBeDeletedForAllUsers || x.CanBeDeletedOnlyForSelf);
        }

        #endregion

        #region Multiple Forward

        public RelayCommand MessagesForwardCommand { get; }
        private async void MessagesForwardExecute()
        {
            var messages = SelectedItems.Values.Where(x => x.CanBeForwarded).OrderBy(x => x.Id).Select(x => x.Get()).ToList();
            if (messages.Count > 0)
            {
                IsSelectionEnabled = false;

                await SharePopup.GetForCurrentView().ShowAsync(messages);
                TextField?.Focus(FocusState.Programmatic);
            }
        }

        private bool MessagesForwardCanExecute()
        {
            return SelectedItems.Count > 0 && SelectedItems.Values.All(x => x.CanBeForwarded);
        }

        #endregion

        #region Multiple Copy

        public RelayCommand MessagesCopyCommand { get; }
        private void MessagesCopyExecute()
        {
            var messages = SelectedItems.Values.OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                var builder = new StringBuilder();
                IsSelectionEnabled = false;

                foreach (var message in messages)
                {
                    var chat = message.GetChat();
                    var title = chat.Title;

                    if (ClientService.TryGetUser(message.SenderId, out Telegram.Td.Api.User senderUser))
                    {
                        title = senderUser.FullName();
                    }
                    else if (ClientService.TryGetChat(message.SenderId, out Chat senderChat))
                    {
                        title = ClientService.GetTitle(senderChat);
                    }

                    var date = Converter.DateTime(message.Date);
                    builder.AppendLine(string.Format("{0}, [{1} {2}]", title, Converter.ShortDate.Format(date), Converter.ShortTime.Format(date)));

                    if (message.ForwardInfo?.Origin is MessageForwardOriginChat fromChat)
                    {
                        var from = ClientService.GetChat(fromChat.SenderChatId);
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {ClientService.GetTitle(from)}]");
                    }
                    else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel forwardedPost)
                    {
                        var from = ClientService.GetChat(forwardedPost.ChatId);
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {ClientService.GetTitle(from)}]");
                    }
                    else if (message.ForwardInfo?.Origin is MessageForwardOriginUser forwardedFromUser)
                    {
                        var from = ClientService.GetUser(forwardedFromUser.SenderUserId);
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {from.FullName()}]");
                    }
                    else if (message.ForwardInfo?.Origin is MessageForwardOriginMessageImport forwardedFromImport)
                    {
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {forwardedFromImport.SenderName}]");
                    }
                    else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser forwardedFromHiddenUser)
                    {
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {forwardedFromHiddenUser.SenderName}]");
                    }

                    if (message.ReplyToMessage != null)
                    {
                        if (ClientService.TryGetUser(message.ReplyToMessage.SenderId, out Telegram.Td.Api.User replyUser))
                        {
                            builder.AppendLine($"[In reply to {replyUser.FullName()}]");
                        }
                        else if (ClientService.TryGetChat(message.ReplyToMessage.SenderId, out Chat replyChat))
                        {
                            builder.AppendLine($"[In reply to {replyChat.Title}]");
                        }
                    }

                    if (message.Content is MessagePhoto photo)
                    {
                        builder.Append($"[{Strings.Resources.AttachPhoto}]");

                        if (photo.Caption != null && !string.IsNullOrEmpty(photo.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.AppendLine(photo.Caption.Text);
                        }
                    }
                    else if (message.Content is MessageVoiceNote voiceNote)
                    {
                        builder.Append($"[{Strings.Resources.AttachAudio}]");

                        if (voiceNote.Caption != null && !string.IsNullOrEmpty(voiceNote.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.AppendLine(voiceNote.Caption.Text);
                        }
                    }
                    else if (message.Content is MessageVideo video)
                    {
                        builder.Append($"[{Strings.Resources.AttachVideo}]");

                        if (video.Caption != null && !string.IsNullOrEmpty(video.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.AppendLine(video.Caption.Text);
                        }
                    }
                    else if (message.Content is MessageVideoNote)
                    {
                        builder.Append($"[{Strings.Resources.AttachRound}]");
                    }
                    else if (message.Content is MessageAnimation animation)
                    {
                        builder.Append($"[{Strings.Resources.AttachGif}]");

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
                            builder.AppendLine($"[{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}]");
                        }
                        else
                        {
                            builder.AppendLine($"[{Strings.Resources.AttachSticker}]");
                        }
                    }
                    else if (message.Content is MessageAudio audio)
                    {
                        builder.Append($"[{Strings.Resources.AttachMusic}]");

                        if (audio.Caption != null && !string.IsNullOrEmpty(audio.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.AppendLine(audio.Caption.Text);
                        }
                    }
                    else if (message.Content is MessageLocation location)
                    {
                        builder.AppendLine($"[{Strings.Resources.AttachLocation}]");
                        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "https://www.bing.com/maps/?pc=W8AP&FORM=MAPXSH&where1=44.312783,9.33426&locsearch=1", location.Location.Latitude, location.Location.Longitude));
                    }
                    else if (message.Content is MessageVenue venue)
                    {
                        builder.AppendLine($"[{Strings.Resources.AttachLocation}]");
                        builder.AppendLine(venue.Venue.Title);
                        builder.AppendLine(venue.Venue.Address);
                        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "https://www.bing.com/maps/?pc=W8AP&FORM=MAPXSH&where1=44.312783,9.33426&locsearch=1", venue.Venue.Location.Latitude, venue.Venue.Location.Longitude));
                    }
                    else if (message.Content is MessageContact contact)
                    {
                        builder.AppendLine($"[{Strings.Resources.AttachContact}]");
                        builder.AppendLine(contact.Contact.GetFullName());
                        builder.AppendLine(PhoneNumber.Format(contact.Contact.PhoneNumber));
                    }
                    else if (message.Content is MessagePoll poll)
                    {
                        builder.AppendLine($"[{Strings.Resources.Poll}: {poll.Poll.Question}");

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

                var dataPackage = new DataPackage();
                dataPackage.SetText(builder.ToString());
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        private bool MessagesCopyCanExecute()
        {
            return SelectedItems.Count > 0;
        }

        #endregion

        #region Multiple Report

        public RelayCommand MessagesReportCommand { get; }
        private async void MessagesReportExecute()
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

        private bool MessagesReportCanExecute()
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

        #endregion

        #region Select

        public RelayCommand<MessageViewModel> MessageSelectCommand { get; }
        private void MessageSelectExecute(MessageViewModel message)
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

        public RelayCommand MessagesUnselectCommand { get; }
        private void MessagesUnselectExecute()
        {
            IsSelectionEnabled = false;
        }

        #endregion

        #region Statistics

        public RelayCommand<MessageViewModel> MessageStatisticsCommand { get; }
        private void MessageStatisticsExecute(MessageViewModel message)
        {
            NavigationService.Navigate(typeof(MessageStatisticsPage), $"{message.ChatId};{message.Id}");
        }

        #endregion

        #region Retry

        public RelayCommand<MessageViewModel> MessageRetryCommand { get; }
        private void MessageRetryExecute(MessageViewModel message)
        {
            ClientService.Send(new ResendMessages(message.ChatId, new[] { message.Id }));
        }

        #endregion

        #region Copy

        public RelayCommand<MessageViewModel> MessageCopyCommand { get; }
        private async void MessageCopyExecute(MessageViewModel message)
        {
            if (message == null)
            {
                return;
            }

            var input = message.Content.GetCaption();
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
                    var stream = new InMemoryRandomAccessStream();
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

                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        #endregion

        #region Copy media

        public RelayCommand<MessageViewModel> MessageCopyMediaCommand { get; }
        private async void MessageCopyMediaExecute(MessageViewModel message)
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

            var temp = await ClientService.GetFileAsync(big.Photo);
            if (temp != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(temp));
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        #endregion

        #region Copy link

        public RelayCommand<MessageViewModel> MessageCopyLinkCommand { get; }
        private async void MessageCopyLinkExecute(MessageViewModel message)
        {
            if (message == null)
            {
                return;
            }

            var chat = message.GetChat();
            if (chat == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetMessageLink(chat.Id, message.Id, 0, false, _threadId != 0));
            if (response is MessageLink link)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(link.Link);
                ClipboardEx.TrySetContent(dataPackage);

                if (!link.IsPublic)
                {
                    await ShowPopupAsync(Strings.Resources.LinkCopiedPrivate, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
        }

        #endregion

        #region Edit

        public async void MessageEditLast()
        {
            var last = Items.LastOrDefault(x => x.CanBeEdited);
            if (last != null)
            {
                MessageEditCommand.Execute(last);
                await ListField?.ScrollToItem(last, VerticalAlignment.Center, true);
            }
        }

        public RelayCommand<MessageViewModel> MessageEditCommand { get; }
        private void MessageEditExecute(MessageViewModel message)
        {
            if (message.Content is MessageAlbum album)
            {
                if (album.IsMedia)
                {
                    message = null;

                    foreach (var child in album.Messages)
                    {
                        var childCaption = child.Content?.GetCaption();
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

            var container = new MessageComposerHeader { EditingMessage = message };
            var input = message.Content.GetCaption();

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

        public RelayCommand<MessageViewModel> MessageThreadCommand { get; }
        private async void MessageThreadExecute(MessageViewModel message)
        {
            var response = await ClientService.SendAsync(new GetMessageThread(message.ChatId, message.Id));
            if (response is MessageThreadInfo)
            {
                NavigationService.NavigateToThread(message.ChatId, message.Id, message.Id);
            }
        }

        #endregion

        #region Pin

        public RelayCommand<MessageViewModel> MessagePinCommand { get; }
        private async void MessagePinExecute(MessageViewModel message)
        {
            var chat = message.GetChat();
            if (chat == null)
            {
                return;
            }

            if (message.IsPinned)
            {
                var confirm = await ShowPopupAsync(Strings.Resources.UnpinMessageAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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
                dialog.Title = Strings.Resources.PinMessageAlertTitle;

                if (last != null && last.Id > message.Id)
                {
                    dialog.Message = Strings.Resources.PinOldMessageAlert;
                }
                else if (channel)
                {
                    dialog.Message = Strings.Resources.PinMessageAlertChannel;
                }
                else if (chat.Type is ChatTypePrivate)
                {
                    dialog.Message = Strings.Resources.PinMessageAlertChat;
                }
                else
                {
                    dialog.Message = Strings.Resources.PinMessageAlert;
                }

                dialog.PrimaryButtonText = Strings.Resources.OK;
                dialog.SecondaryButtonText = Strings.Resources.Cancel;

                if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup && !channel)
                {
                    dialog.CheckBoxLabel = Strings.Resources.PinNotify;
                    dialog.IsChecked = true;
                }
                else if (chat.Type is ChatTypePrivate && !self)
                {
                    dialog.CheckBoxLabel = string.Format(Strings.Resources.PinAlsoFor, chat.Title);
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

        public RelayCommand<MessageViewModel> MessageReportCommand { get; }
        private async void MessageReportExecute(MessageViewModel message)
        {
            await ReportAsync(new[] { message.Id });
        }

        #endregion

        #region Report false positive

        public RelayCommand<MessageViewModel> MessageReportFalsePositiveCommand { get; }
        private async void MessageReportFalsePositiveExecute(MessageViewModel message)
        {
            if (_chat?.Type is ChatTypeSupergroup supergroup)
            {
                ClientService.Send(new ReportSupergroupAntiSpamFalsePositive(supergroup.SupergroupId, message.Id));
                await ShowPopupAsync(Strings.Resources.ChannelAntiSpamFalsePositiveReported);
            }
        }

        #endregion

        #region Send now

        public RelayCommand<MessageViewModel> MessageSendNowCommand { get; }
        private void MessageSendNowExecute(MessageViewModel message)
        {
            ClientService.Send(new EditMessageSchedulingState(message.ChatId, message.Id, null));
        }

        #endregion

        #region Reschedule

        public RelayCommand<MessageViewModel> MessageRescheduleCommand { get; }
        private async void MessageRescheduleExecute(MessageViewModel message)
        {
            var options = await PickMessageSendOptionsAsync(true);
            if (options?.SchedulingState == null)
            {
                return;
            }

            ClientService.Send(new EditMessageSchedulingState(message.ChatId, message.Id, options.SchedulingState));
        }

        #endregion

        #region Translate

        public RelayCommand<MessageViewModel> MessageTranslateCommand { get; }
        private async void MessageTranslateExecute(MessageViewModel message)
        {
            var caption = message.GetCaption();
            if (string.IsNullOrEmpty(caption?.Text))
            {
                return;
            }

            var language = LanguageIdentification.IdentifyLanguage(caption.Text);
            var popup = new TranslatePopup(_translateService, message.ChatId, message.Id, caption.Text, language, LocaleService.Current.CurrentCulture.TwoLetterISOLanguageName, !message.CanBeSaved);
            await ShowPopupAsync(popup);
        }

        #endregion

        #region Keyboard button

        public async void KeyboardButtonInline(MessageViewModel message, InlineKeyboardButton inline)
        {
            if (_chat is not Chat chat)
            {
                return;
            }

            if (message.SchedulingState != null)
            {
                await ShowPopupAsync(Strings.Resources.MessageScheduledBotAction, Strings.Resources.AppName, Strings.Resources.OK);
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

                if (switchInline.InCurrentChat && bot.HasActiveUsername(out string username))
                {
                    SetText(string.Format("@{0} {1}", username, switchInline.Query), focus: true);
                    ResolveInlineBot(username, switchInline.Query);
                }
                else
                {
                    await SharePopup.GetForCurrentView().ShowAsync(switchInline, bot);
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeUrl urlButton)
            {
                if (MessageHelper.TryCreateUri(urlButton.Url, out Uri uri))
                {
                    if (MessageHelper.IsTelegramUrl(uri))
                    {
                        MessageHelper.OpenTelegramUrl(ClientService, NavigationService, uri);
                    }
                    else
                    {
                        var confirm = await ShowPopupAsync(string.Format(Strings.Resources.OpenUrlAlert, urlButton.Url), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                        if (confirm != ContentDialogResult.Primary)
                        {
                            return;
                        }

                        await Launcher.LaunchUriAsync(uri);
                    }
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeCallback callback)
            {
                var bot = message.GetViaBotUser();
                if (bot != null)
                {
                    InformativeMessage = _messageFactory.Create(this, new Message(0, new MessageSenderUser(bot.Id), 0, null, null, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText(Strings.Resources.Loading, new TextEntity[0]), null), null));
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

                            InformativeMessage = _messageFactory.Create(this, new Message(0, new MessageSenderUser(bot.Id), 0, null, null, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText(answer.Text, new TextEntity[0]), null), null));
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
                    Title = Strings.Resources.BotOwnershipTransfer,
                    Header = Strings.Resources.BotOwnershipTransferReadyAlertText,
                    PlaceholderText = Strings.Resources.LoginPassword,
                    PrimaryButtonText = Strings.Resources.BotOwnershipTransferChangeOwner,
                    SecondaryButtonText = Strings.Resources.Cancel
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
                        var primary = Strings.Resources.OK;

                        var builder = new StringBuilder();
                        builder.AppendLine(Strings.Resources.BotOwnershipTransferAlertText);
                        builder.AppendLine($"\u2022 {Strings.Resources.EditAdminTransferAlertText1}");
                        builder.AppendLine($"\u2022 {Strings.Resources.EditAdminTransferAlertText2}");

                        if (error.Message.Equals("PASSWORD_MISSING"))
                        {
                            primary = Strings.Resources.EditAdminTransferSetPassword;
                        }
                        else
                        {
                            builder.AppendLine();
                            builder.AppendLine(Strings.Resources.EditAdminTransferAlertText3);
                        }

                        var confirm = await ShowPopupAsync(builder.ToString(), Strings.Resources.EditAdminTransferAlertTitle, primary, Strings.Resources.Cancel);
                        if (confirm == ContentDialogResult.Primary && !error.Message.Equals("PASSWORD_MISSING"))
                        {
                            NavigationService.Navigate(typeof(SettingsPasswordPage));
                        }
                    }
                    else if (error.Message.Equals("PASSWORD_HASH_INVALID"))
                    {
                        KeyboardButtonInline(message, inline);
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
                    var bundle = new Dictionary<string, object>();
                    bundle.Add("title", game.Game.Title);
                    bundle.Add("url", answer.Url);
                    bundle.Add("message", message.Id);
                    bundle.Add("chat", message.ChatId);

                    var viaBot = message.GetViaBotUser();
                    if (viaBot != null && viaBot.HasActiveUsername(out string username))
                    {
                        bundle.Add("username", username);
                    }

                    ChatActionManager.SetTyping(new ChatActionStartPlayingGame());
                    NavigationService.Navigate(typeof(GamePage), bundle);
                }
            }
            else if (inline.Type is InlineKeyboardButtonTypeWebApp webApp)
            {
                var bot = message.GetViaBotUser();
                if (bot == null)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new OpenWebApp(chat.Id, bot.Id, webApp.Url, Theme.Current.Parameters, Strings.Resources.AppName, _threadId, 0));
                if (response is WebAppInfo webAppInfo)
                {
                    await ShowPopupAsync(new WebBotPopup(SessionId, webAppInfo, inline.Text));
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
                    var content = Strings.Resources.AreYouSureShareMyContactInfo;
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        var withUser = ClientService.GetUser(privata.UserId);
                        if (withUser != null)
                        {
                            content = withUser.Type is UserTypeBot ? Strings.Resources.AreYouSureShareMyContactInfoBot : string.Format(Strings.Resources.AreYouSureShareMyContactInfoUser, PhoneNumber.Format(cached.PhoneNumber), withUser.FullName());
                        }
                    }

                    var confirm = await ShowPopupAsync(content, Strings.Resources.ShareYouPhoneNumberTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await SendContactAsync(chat, new Contact(cached.PhoneNumber, cached.FirstName, cached.LastName, string.Empty, cached.Id), null);
                    }
                }
            }
            else if (keyboardButton.Type is KeyboardButtonTypeRequestLocation)
            {
                var confirm = await ShowPopupAsync(Strings.Resources.ShareYouLocationInfo, Strings.Resources.ShareYouLocationTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var location = await _locationService.GetPositionAsync();
                    if (location != null)
                    {
                        await SendMessageAsync(chat, 0, new InputMessageLocation(location, 0, 0, 0), null);
                    }
                }
            }
            else if (keyboardButton.Type is KeyboardButtonTypeRequestPoll requestPoll)
            {
                await SendPollAsync(requestPoll.ForceQuiz, requestPoll.ForceRegular, _chat?.Type is ChatTypeSupergroup super && super.IsChannel);
            }
            else if (keyboardButton.Type is KeyboardButtonTypeText)
            {
                var input = new InputMessageText(new FormattedText(keyboardButton.Text, null), false, true);
                await SendMessageAsync(chat, chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup ? message.Id : 0, input, null);
            }
            else if (keyboardButton.Type is KeyboardButtonTypeWebApp webApp && message.SenderId is MessageSenderUser bot)
            {
                var response = await ClientService.SendAsync(new OpenWebApp(chat.Id, bot.UserId, webApp.Url, Theme.Current.Parameters, Strings.Resources.AppName, _threadId, 0));
                if (response is WebAppInfo webAppInfo)
                {
                    await ShowPopupAsync(new WebBotPopup(SessionId, webAppInfo, keyboardButton.Text));
                }
            }
        }

        public async void OpenWebView()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ContentDialogResult.Primary != await ShowPopupAsync(string.Format(Strings.Resources.BotOpenPageMessage, chat.Title), Strings.Resources.BotOpenPageTitle, Strings.Resources.OK, Strings.Resources.Cancel))
            {
                return;
            }
        }

        #endregion

        #region Sticker info

        public RelayCommand<MessageViewModel> MessageAddStickerCommand { get; }
        private void MessageAddStickerExecute(MessageViewModel message)
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

        public RelayCommand<MessageViewModel> MessageFaveStickerCommand { get; }
        private void MessageFaveStickerExecute(MessageViewModel message)
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

        public RelayCommand<MessageViewModel> MessageUnfaveStickerCommand { get; }
        private void MessageUnfaveStickerExecute(MessageViewModel message)
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

        public RelayCommand<MessageViewModel> MessageSaveMediaCommand { get; }
        private async void MessageSaveMediaExecute(MessageViewModel message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.SaveAsAsync(file);
            }
        }

        #endregion

        #region Save to GIFs

        public RelayCommand<MessageViewModel> MessageSaveAnimationCommand { get; }
        private void MessageSaveAnimationExecute(MessageViewModel message)
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

        public RelayCommand<MessageViewModel> MessageSaveSoundCommand { get; }
        private void MessageSaveSoundExecute(MessageViewModel message)
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

        public RelayCommand<MessageViewModel> MessageOpenWithCommand { get; }
        private async void MessageOpenWithExecute(MessageViewModel message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenWithAsync(file);
            }
        }

        #endregion

        #region Show in folder

        public RelayCommand<MessageViewModel> MessageOpenFolderCommand { get; }
        private async void MessageOpenFolderExecute(MessageViewModel message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenFolderAsync(file);
            }
        }

        #endregion

        #region Add contact

        public RelayCommand<MessageViewModel> MessageAddContactCommand { get; }
        private void MessageAddContactExecute(MessageViewModel message)
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

        public RelayCommand<MessageViewModel> MessageServiceCommand { get; }
        public async void MessageServiceExecute(MessageViewModel message)
        {
            if (message.Content is MessageHeaderDate)
            {
                var date = Converter.DateTime(message.Date);

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
                await _groupCallService.JoinAsync(message.ChatId);
            }
            else if (message.Content is MessagePaymentSuccessful)
            {
                NavigationService.NavigateToInvoice(message);
            }
            else if (message.Content is MessageChatSetTheme)
            {
                SetThemeExecute();
            }
            else if (message.Content is MessageChatChangePhoto chatChangePhoto)
            {
                var viewModel = new ChatPhotosViewModel(ClientService, StorageService, Aggregator, Chat, chatChangePhoto.Photo);
                await GalleryView.ShowAsync(viewModel);
            }
            else if (message.Content is MessageSuggestProfilePhoto suggestProfilePhoto)
            {
                if (message.IsOutgoing)
                {
                    var viewModel = new ChatPhotosViewModel(ClientService, StorageService, Aggregator, Chat, suggestProfilePhoto.Photo);
                    await GalleryView.ShowAsync(viewModel);
                }
                else
                {
                    var file = suggestProfilePhoto.Photo.Animation?.File
                        ?? suggestProfilePhoto.Photo.GetBig()?.Photo;

                    var storage = await ClientService.GetFileAsync(file);
                    if (storage == null)
                    {
                        return;
                    }

                    var media = await StorageMedia.CreateAsync(storage);
                    var dialog = new EditMediaPopup(media, ImageCropperMask.Ellipse);

                    var confirm = await dialog.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await EditPhotoAsync(media);
                    }
                }
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

        public RelayCommand<MessageViewModel> MessageUnvotePollCommand { get; }
        private void MessageUnvotePollExecute(MessageViewModel message)
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

        public RelayCommand<MessageViewModel> MessageStopPollCommand { get; }
        private async void MessageStopPollExecute(MessageViewModel message)
        {
            var poll = message.Content as MessagePoll;
            if (poll == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(Strings.Resources.StopPollAlertText, Strings.Resources.StopPollAlertTitle, Strings.Resources.Stop, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ClientService.Send(new StopPoll(message.ChatId, message.Id, null));
        }

        #endregion

        #region Show emoji

        public RelayCommand<MessageViewModel> MessageShowEmojiCommand { get; }
        private async void MessageShowEmojiExecute(MessageViewModel message)
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
