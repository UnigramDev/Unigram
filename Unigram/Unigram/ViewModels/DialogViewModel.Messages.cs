using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Views;
using Unigram.Views.Payments;
using Unigram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        #region Reply

        public RelayCommand MessageReplyPreviousCommand { get; }
        private async void MessageReplyPreviousExecute()
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

        public RelayCommand MessageReplyNextCommand { get; }
        private async void MessageReplyNextExecute()
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
                ClearReplyCommand.Execute();
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

            var response = await ProtoService.SendAsync(new GetMessages(chat.Id, items.Select(x => x.Id).ToArray()));
            if (response is Messages updated)
            {
                for (int i = 0; i < updated.MessagesValue.Count; i++)
                {
                    items[i] = updated.MessagesValue[i];
                }
            }

            var sameUser = messages.All(x => x.SenderUserId == first.SenderUserId);
            var dialog = new DeleteMessagesPopup(CacheService, items.Where(x => x != null).ToArray());

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            SelectionMode = ListViewSelectionMode.None;

            if (dialog.DeleteAll && sameUser)
            {
                ProtoService.Send(new DeleteChatMessagesFromUser(chat.Id, first.SenderUserId));
            }
            else
            {
                ProtoService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), dialog.Revoke));
            }

            if (dialog.BanUser && sameUser)
            {
                ProtoService.Send(new SetChatMemberStatus(chat.Id, first.SenderUserId, new ChatMemberStatusBanned()));
            }

            if (dialog.ReportSpam && sameUser && chat.Type is ChatTypeSupergroup supertype)
            {
                ProtoService.Send(new ReportSupergroupSpam(supertype.SupergroupId, first.SenderUserId, messages.Select(x => x.Id).ToList()));
            }
        }

        #endregion

        #region Forward

        public RelayCommand<MessageViewModel> MessageForwardCommand { get; }
        private async void MessageForwardExecute(MessageViewModel message)
        {
            //if (messageBase is TLMessage message)
            //{
            //    if (message.Media is TLMessageMediaGroup groupMedia)
            //    {
            //        ExpandSelection(new[] { message });
            //        MessagesForwardExecute();
            //        return;
            //    }

            //    Search = null;
            //    SelectionMode = ListViewSelectionMode.None;

            //    await ShareView.GetForCurrentView().ShowAsync(message);
            //}

            DisposeSearch();
            SelectionMode = ListViewSelectionMode.None;

            await SharePopup.GetForCurrentView().ShowAsync(message.Get());

            TextField?.Focus(FocusState.Programmatic);
        }

        #endregion

        #region Share

        public RelayCommand<MessageViewModel> MessageShareCommand { get; }
        private async void MessageShareExecute(MessageViewModel message)
        {
            if (message.Content is MessageAlbum album)
            {
                await SharePopup.GetForCurrentView().ShowAsync(album.Messages.Select(x => x.Get()).ToList());
            }
            else
            {
                await SharePopup.GetForCurrentView().ShowAsync(message.Get());
            }
        }

        #endregion

        #region Multiple Delete

        public RelayCommand MessagesDeleteCommand { get; }
        private void MessagesDeleteExecute()
        {
            var messages = new List<MessageViewModel>(SelectedItems);

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
            return SelectedItems.Count > 0 && SelectedItems.All(x => x.CanBeDeletedForAllUsers || x.CanBeDeletedOnlyForSelf);
        }

        #endregion

        #region Multiple Forward

        public RelayCommand MessagesForwardCommand { get; }
        private async void MessagesForwardExecute()
        {
            var messages = SelectedItems.Where(x => x.CanBeForwarded).OrderBy(x => x.Id).Select(x => x.Get()).ToList();
            if (messages.Count > 0)
            {
                DisposeSearch();
                SelectionMode = ListViewSelectionMode.None;

                await SharePopup.GetForCurrentView().ShowAsync(messages);

                TextField?.Focus(FocusState.Programmatic);
            }
        }

        private bool MessagesForwardCanExecute()
        {
            return SelectedItems.Count > 0 && SelectedItems.All(x => x.CanBeForwarded);
        }

        #endregion

        #region Multiple Copy

        public RelayCommand MessagesCopyCommand { get; }
        private void MessagesCopyExecute()
        {
            var messages = SelectedItems.OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                var builder = new StringBuilder();
                SelectionMode = ListViewSelectionMode.None;

                foreach (var message in messages)
                {
                    var chat = message.GetChat();
                    var title = chat.Title;

                    if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
                    {
                        title = ProtoService.GetTitle(chat);
                    }
                    else
                    {
                        var sender = message.GetSenderUser();
                        if (sender != null)
                        {
                            title = sender.GetFullName();
                        }
                    }

                    var date = BindConvert.Current.DateTime(message.Date);
                    builder.AppendLine(string.Format("{0}, [{1} {2}]", title, BindConvert.Current.ShortDate.Format(date), BindConvert.Current.ShortTime.Format(date)));

                    if (message.ForwardInfo?.Origin is MessageForwardOriginChannel forwardedPost)
                    {
                        var from = ProtoService.GetChat(forwardedPost.ChatId);
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {ProtoService.GetTitle(from)}]");
                    }
                    else if (message.ForwardInfo?.Origin is MessageForwardOriginUser forwardedFromUser)
                    {
                        var from = ProtoService.GetUser(forwardedFromUser.SenderUserId);
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {from.GetFullName()}]");
                    }
                    else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser forwardedFromHiddenUser)
                    {
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {forwardedFromHiddenUser.SenderName}]");
                    }

                    if (message.ReplyToMessage != null)
                    {
                        var replySender = message.ReplyToMessage.GetSenderUser();
                        if (replySender != null)
                        {
                            builder.AppendLine($"[In reply to {replySender.GetFullName()}]");
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

            var myId = CacheService.Options.MyId;
            var messages = SelectedItems.Where(x => x.SenderUserId != myId).OrderBy(x => x.Id).Select(x => x.Id).ToList();
            if (messages.Count < 1)
            {
                return;
            }

            var items = new[]
            {
                new SelectRadioItem(new ChatReportReasonSpam(), Strings.Resources.ReportChatSpam, true),
                new SelectRadioItem(new ChatReportReasonViolence(), Strings.Resources.ReportChatViolence, false),
                new SelectRadioItem(new ChatReportReasonPornography(), Strings.Resources.ReportChatPornography, false),
                new SelectRadioItem(new ChatReportReasonChildAbuse(), Strings.Resources.ReportChatChild, false),
                new SelectRadioItem(new ChatReportReasonCustom(), Strings.Resources.ReportChatOther, false)
            };

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.ReportChat;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var reason = dialog.SelectedIndex as ChatReportReason;
            if (reason == null)
            {
                return;
            }

            if (reason is ChatReportReasonCustom other)
            {
                var input = new InputDialog();
                input.Title = Strings.Resources.ReportChat;
                input.PlaceholderText = Strings.Resources.ReportChatDescription;
                input.IsPrimaryButtonEnabled = true;
                input.IsSecondaryButtonEnabled = true;
                input.PrimaryButtonText = Strings.Resources.OK;
                input.SecondaryButtonText = Strings.Resources.Cancel;

                var inputResult = await input.ShowQueuedAsync();
                if (inputResult == ContentDialogResult.Primary)
                {
                    other.Text = input.Text;
                }
                else
                {
                    return;
                }
            }

            ProtoService.Send(new ReportChat(chat.Id, reason, messages));
        }

        private bool MessagesReportCanExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return false;
            }

            var myId = CacheService.Options.MyId;
            return chat.CanBeReported && SelectedItems.Count > 0 && SelectedItems.All(x => x.SenderUserId != myId);
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

            SelectionMode = ListViewSelectionMode.Multiple;
            ListField?.SelectedItems.Add(message);

            ExpandSelection(new[] { message });
        }

        #endregion

        #region Retry

        public RelayCommand<MessageViewModel> MessageRetryCommand { get; }
        private void MessageRetryExecute(MessageViewModel message)
        {
            ProtoService.Send(new ResendMessages(message.ChatId, new[] { message.Id }));
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
            if (message.Content is MessageText text)
            {
                input = text.Text;
            }
            else if (message.Content is MessageContact contact)
            {
                input = new FormattedText(PhoneNumber.Format(contact.Contact.PhoneNumber), new TextEntity[0]);
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
                                    writer.WriteInt32(mentionName.UserId);
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

            if (big.Photo.Local.IsDownloadingCompleted)
            {
                try
                {
                    var temp = await StorageFile.GetFileFromPathAsync(big.Photo.Local.Path);

                    var dataPackage = new DataPackage();
                    dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(temp));
                    ClipboardEx.TrySetContent(dataPackage);
                }
                catch { }
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

            var response = await ProtoService.SendAsync(new GetMessageLink(chat.Id, message.Id, false, false));
            if (response is MessageLink link)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(link.Link);
                ClipboardEx.TrySetContent(dataPackage);

                if (!link.IsPublic)
                {
                    await MessagePopup.ShowAsync(Strings.Resources.LinkCopiedPrivate, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
        }

        #endregion

        #region Edit

        public RelayCommand MessageEditLastCommand { get; }
        private async void MessageEditLastExecute()
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
            CurrentInlineBot = null;
            DisposeSearch();
            SaveDraft();

            var container = new MessageComposerHeader { EditingMessage = message };
            var input = message.Content.GetCaption();

            if (message.Content is MessageText text)
            {
                input = text.Text;
                container.WebPagePreview = text.WebPage;
                container.WebPageUrl = text.WebPage?.Url;
            }

            ComposerHeader = container;
            SetText(input);

            //if (message?.Media is TLMessageMediaGroup groupMedia)
            //{
            //    message = groupMedia.Layout.Messages.FirstOrDefault();
            //}

            //if (message == null)
            //{
            //    return;
            //}

            //var response = await LegacyService.GetMessageEditDataAsync(Peer, message.Id);
            //if (response.IsSucceeded)
            //{
            //    BeginOnUIThread(() =>
            //    {
            //        var messageEditText = GetMessageEditText(response.Result, message);
            //        StartEditMessage(messageEditText, message);
            //    });
            //}
            //else
            //{
            //    BeginOnUIThread(() =>
            //    {
            //        //this.IsWorking = false;
            //        //if (error.CodeEquals(ErrorCode.BAD_REQUEST) && error.TypeEquals(ErrorType.MESSAGE_ID_INVALID))
            //        //{
            //        //    MessageBox.Show(Strings.Additional.EditMessageError, Strings.Additional.Error, 0);
            //        //    return;
            //        //}
            //        Logs.Log.Write("messages.getMessageEditData error " + response.Error);
            //    });
            //}
        }

        #endregion

        #region View thread

        public RelayCommand<MessageViewModel> MessageThreadCommand { get; }
        private void MessageThreadExecute(MessageViewModel message)
        {
            OpenThread(message);
        }

        #endregion

        #region Pin

        public RelayCommand<MessageViewModel> MessagePinCommand { get; }
        private async void MessagePinExecute(MessageViewModel message)
        {
            var chat = message.GetChat();

            if (chat.PinnedMessageId == message.Id)
            {
                var confirm = await MessagePopup.ShowAsync(Strings.Resources.UnpinMessageAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    ProtoService.Send(new UnpinChatMessage(chat.Id));
                }
            }
            else
            {
                var dialog = new MessagePopup();
                dialog.Title = Strings.Resources.AppName;
                dialog.Message = chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel
                    ? Strings.Resources.PinMessageAlertChannel
                    : chat.Type is ChatTypePrivate privata && privata.UserId == CacheService.Options.MyId
                    ? Strings.Resources.PinMessageAlertChat
                    : Strings.Resources.PinMessageAlert;
                dialog.PrimaryButtonText = Strings.Resources.OK;
                dialog.SecondaryButtonText = Strings.Resources.Cancel;

                if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup super && !super.IsChannel)
                {
                    dialog.CheckBoxLabel = Strings.Resources.PinNotify;
                    dialog.IsChecked = true;
                }

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    ProtoService.Send(new PinChatMessage(chat.Id, message.Id, dialog.IsChecked == false));
                }
            }
        }

        #endregion

        #region Report

        public RelayCommand<MessageViewModel> MessageReportCommand { get; }
        private async void MessageReportExecute(MessageViewModel message)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var items = new[]
            {
                new SelectRadioItem(new ChatReportReasonSpam(), Strings.Resources.ReportChatSpam, true),
                new SelectRadioItem(new ChatReportReasonViolence(), Strings.Resources.ReportChatViolence, false),
                new SelectRadioItem(new ChatReportReasonPornography(), Strings.Resources.ReportChatPornography, false),
                new SelectRadioItem(new ChatReportReasonChildAbuse(), Strings.Resources.ReportChatChild, false),
                new SelectRadioItem(new ChatReportReasonCustom(), Strings.Resources.ReportChatOther, false)
            };

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.ReportChat;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var reason = dialog.SelectedIndex as ChatReportReason;
            if (reason == null)
            {
                return;
            }

            if (reason is ChatReportReasonCustom other)
            {
                var input = new InputDialog();
                input.Title = Strings.Resources.ReportChat;
                input.PlaceholderText = Strings.Resources.ReportChatDescription;
                input.IsPrimaryButtonEnabled = true;
                input.IsSecondaryButtonEnabled = true;
                input.PrimaryButtonText = Strings.Resources.OK;
                input.SecondaryButtonText = Strings.Resources.Cancel;

                var inputResult = await input.ShowQueuedAsync();
                if (inputResult == ContentDialogResult.Primary)
                {
                    other.Text = input.Text;
                }
                else
                {
                    return;
                }
            }

            ProtoService.Send(new ReportChat(chat.Id, reason, new[] { message.Id }));
        }

        #endregion

        #region Send now

        public RelayCommand<MessageViewModel> MessageSendNowCommand { get; }
        private void MessageSendNowExecute(MessageViewModel message)
        {
            ProtoService.Send(new EditMessageSchedulingState(message.ChatId, message.Id, null));
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

            ProtoService.Send(new EditMessageSchedulingState(message.ChatId, message.Id, options.SchedulingState));
        }

        #endregion

        #region Keyboard button

        public async void KeyboardButtonExecute(MessageViewModel message, object button)
        {
            if (button is InlineKeyboardButton inline)
            {
                if (message.SchedulingState != null)
                {
                    await MessagePopup.ShowAsync(Strings.Resources.MessageScheduledBotAction, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }

                var chat = message.GetChat();
                if (chat == null)
                {
                    return;
                }

                if (inline.Type is InlineKeyboardButtonTypeBuy buy)
                {
                    if (message.Content is MessageInvoice invoice && invoice.ReceiptMessageId != 0)
                    {
                        //var response = await ProtoService.SendAsync(new GetPaymentReceipt(chat.Id, invoice.ReceiptMessageId));
                        //if (response is PaymentReceipt receipt)
                        //{
                        //    NavigationService.Navigate(typeof(PaymentReceiptPage), TLTuple.Create(message, receipt));
                        //}

                        NavigationService.Navigate(typeof(PaymentReceiptPage), new ReceiptNavigation(chat.Id, invoice.ReceiptMessageId));
                    }
                    else
                    {
                        // TODO:
                        await MessagePopup.ShowAsync("Payments are coming soon!", Strings.Resources.AppName, "OK");
                        return;

                        var response = await ProtoService.SendAsync(new GetPaymentForm(chat.Id, message.Id));
                        if (response is PaymentForm form)
                        {
                            if (form.Invoice.NeedEmailAddress || form.Invoice.NeedName || form.Invoice.NeedPhoneNumber || form.Invoice.NeedShippingAddress)
                            {
                                NavigationService.NavigateToPaymentFormStep1(message, form);
                            }
                            else if (form.SavedCredentials != null)
                            {
                                //if (ApplicationSettings.Current.TmpPassword != null)
                                //{
                                //    if (ApplicationSettings.Current.TmpPassword.ValidUntil < TLUtils.Now + 60)
                                //    {
                                //        ApplicationSettings.Current.TmpPassword = null;
                                //    }
                                //}

                                //if (ApplicationSettings.Current.TmpPassword != null)
                                //{
                                //    NavigationService.NavigateToPaymentFormStep5(message, form, null, null, null, null, null, true);
                                //}
                                //else
                                //{
                                //    NavigationService.NavigateToPaymentFormStep4(message, form, null, null, null);
                                //}
                            }
                            else
                            {
                                NavigationService.NavigateToPaymentFormStep3(message, form, null, null, null);
                            }
                        }
                    }
                }
                else if (inline.Type is InlineKeyboardButtonTypeLoginUrl loginUrl)
                {
                    var response = await ProtoService.SendAsync(new GetLoginUrlInfo(chat.Id, message.Id, loginUrl.Id));
                    if (response is LoginUrlInfoOpen infoOpen)
                    {
                        OpenUrl(infoOpen.Url, !infoOpen.SkipConfirm);
                    }
                    else if (response is LoginUrlInfoRequestConfirmation requestConfirmation)
                    {
                        var dialog = new LoginUrlInfoPopup(CacheService, requestConfirmation);
                        var confirm = await dialog.ShowQueuedAsync();
                        if (confirm != ContentDialogResult.Primary || !dialog.HasAccepted)
                        {
                            return;
                        }

                        response = await ProtoService.SendAsync(new GetLoginUrl(chat.Id, message.Id, loginUrl.Id, dialog.HasWriteAccess));
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
                    var bot = GetBot(message);
                    if (bot == null)
                    {
                        return;
                    }

                    if (switchInline.InCurrentChat)
                    {
                        SetText(string.Format("@{0} {1}", bot.Username, switchInline.Query), focus: true);
                        ResolveInlineBot(bot.Username, switchInline.Query);
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
                            MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, uri);
                        }
                        else
                        {
                            var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.OpenUrlAlert, urlButton.Url), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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
                    var bot = GetBot(message);
                    if (bot != null)
                    {
                        InformativeMessage = _messageFactory.Create(this, new Message(0, bot.Id, 0, 0, null, null, false, false, false, true, false, false, false, false, false, 0, 0, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText(Strings.Resources.Loading, new TextEntity[0]), null), null));
                    }

                    var response = await ProtoService.SendAsync(new GetCallbackQueryAnswer(chat.Id, message.Id, new CallbackQueryPayloadData(callback.Data)));
                    if (response is CallbackQueryAnswer answer)
                    {
                        if (!string.IsNullOrEmpty(answer.Text))
                        {
                            if (answer.ShowAlert)
                            {
                                await new MessagePopup(answer.Text).ShowQueuedAsync();
                            }
                            else
                            {
                                if (bot == null)
                                {
                                    // TODO:
                                    await new MessagePopup(answer.Text).ShowQueuedAsync();
                                    return;
                                }

                                InformativeMessage = _messageFactory.Create(this, new Message(0, bot.Id, 0, 0, null, null, false, false, false, true, false, false, false, false, false, 0, 0, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText(answer.Text, new TextEntity[0]), null), null));
                            }
                        }
                        else if (!string.IsNullOrEmpty(answer.Url))
                        {
                            if (MessageHelper.TryCreateUri(answer.Url, out Uri uri))
                            {
                                if (MessageHelper.IsTelegramUrl(uri))
                                {
                                    MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, uri);
                                }
                                else
                                {
                                    //var dialog = new MessagePopup(response.Result.Url, "Open this link?");
                                    //dialog.PrimaryButtonText = "OK";
                                    //dialog.SecondaryButtonText = "Cancel";

                                    //var result = await dialog.ShowQueuedAsync();
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
                else if (inline.Type is InlineKeyboardButtonTypeCallbackGame callbackGame)
                {
                    var game = message.Content as MessageGame;
                    if (game == null)
                    {
                        return;
                    }

                    var response = await ProtoService.SendAsync(new GetCallbackQueryAnswer(chat.Id, message.Id, new CallbackQueryPayloadGame(game.Game.ShortName)));
                    if (response is CallbackQueryAnswer answer && !string.IsNullOrEmpty(answer.Url))
                    {
                        var bundle = new Dictionary<string, object>();
                        bundle.Add("title", game.Game.Title);
                        bundle.Add("url", answer.Url);
                        bundle.Add("message", message.Id);
                        bundle.Add("chat", message.ChatId);

                        var viaBot = message.GetViaBotUser();
                        if (viaBot != null)
                        {
                            bundle.Add("username", viaBot.Username);
                        }

                        ChatActionManager.SetTyping(new ChatActionStartPlayingGame());
                        NavigationService.Navigate(typeof(GamePage), bundle);
                    }
                }
            }
            else if (button is KeyboardButton keyboardButton)
            {
                if (keyboardButton.Type is KeyboardButtonTypeRequestPhoneNumber requestPhoneNumber)
                {
                    var response = await ProtoService.SendAsync(new GetMe());
                    if (response is Telegram.Td.Api.User cached)
                    {
                        var chat = Chat;
                        if (chat == null)
                        {
                            return;
                        }

                        var content = Strings.Resources.AreYouSureShareMyContactInfo;
                        if (chat.Type is ChatTypePrivate privata)
                        {
                            var withUser = ProtoService.GetUser(privata.UserId);
                            if (withUser != null)
                            {
                                content = withUser.Type is UserTypeBot ? Strings.Resources.AreYouSureShareMyContactInfoBot : string.Format(Strings.Resources.AreYouSureShareMyContactInfoUser, PhoneNumber.Format(cached.PhoneNumber), withUser.GetFullName());
                            }
                        }

                        var confirm = await MessagePopup.ShowAsync(content, Strings.Resources.ShareYouPhoneNumberTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            await SendContactAsync(new Contact(cached.PhoneNumber, cached.FirstName, cached.LastName, string.Empty, cached.Id), null);
                        }
                    }
                }
                else if (keyboardButton.Type is KeyboardButtonTypeRequestLocation requestLocation)
                {
                    var confirm = await MessagePopup.ShowAsync(Strings.Resources.ShareYouLocationInfo, Strings.Resources.ShareYouLocationTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        var location = await _locationService.GetPositionAsync();
                        if (location != null)
                        {
                            await SendMessageAsync(0, new InputMessageLocation(location, 0), null);
                        }
                    }
                }
                else if (keyboardButton.Type is KeyboardButtonTypeRequestPoll requestPoll)
                {
                    await SendPollAsync(requestPoll.ForceQuiz, requestPoll.ForceRegular, _chat?.Type is ChatTypeSupergroup super && super.IsChannel);
                }
                else if (keyboardButton.Type is KeyboardButtonTypeText textButton)
                {
                    await SendMessageAsync(keyboardButton.Text);
                }
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

            ProtoService.Send(new AddFavoriteSticker(new InputFileId(sticker.Sticker.StickerValue.Id)));
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

            ProtoService.Send(new RemoveFavoriteSticker(new InputFileId(sticker.Sticker.StickerValue.Id)));
        }

        #endregion

        #region Save file as

        public RelayCommand<MessageViewModel> MessageSaveMediaCommand { get; }
        private async void MessageSaveMediaExecute(MessageViewModel message)
        {
            var result = message.Get().GetFileAndName(true);

            var file = result.File;
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var cached = await ProtoService.GetFileAsync(file);
            if (cached == null)
            {
                return;
            }

            var fileName = result.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = Path.GetFileName(file.Local.Path);
            }

            var clean = ProtoService.Execute(new CleanFileName(fileName));
            if (clean is Text text && !string.IsNullOrEmpty(text.TextValue))
            {
                fileName = text.TextValue;
            }

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".dat";
            }

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} File", new[] { extension });
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.SuggestedFileName = fileName;

            var picked = await picker.PickSaveFileAsync();
            if (picked != null)
            {
                try
                {
                    await cached.CopyAndReplaceAsync(picked);
                }
                catch { }
            }
        }

        #endregion

        #region Save to Downloads

        public RelayCommand<MessageViewModel> MessageSaveDownloadCommand { get; }
        private async void MessageSaveDownloadExecute(MessageViewModel message)
        {
            var result = message.Get().GetFileAndName(true);

            var file = result.File;
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var fileName = result.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = Path.GetFileName(file.Local.Path);
            }

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".dat";
            }

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} File", new[] { extension });
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.SuggestedFileName = fileName;

            var picked = await picker.PickSaveFileAsync();
            if (picked != null)
            {
                try
                {
                    var cached = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    await cached.CopyAndReplaceAsync(picked);
                }
                catch { }
            }
        }

        #endregion

        #region Save to GIFs

        public RelayCommand<MessageViewModel> MessageSaveAnimationCommand { get; }
        private void MessageSaveAnimationExecute(MessageViewModel message)
        {
            if (message.Content is MessageAnimation animation)
            {
                ProtoService.Send(new AddSavedAnimation(new InputFileId(animation.Animation.AnimationValue.Id)));
            }
            else if (message.Content is MessageText text && text.WebPage != null && text.WebPage.Animation != null)
            {
                ProtoService.Send(new AddSavedAnimation(new InputFileId(text.WebPage.Animation.AnimationValue.Id)));
            }
        }

        #endregion

        #region Open with

        public RelayCommand<MessageViewModel> MessageOpenWithCommand { get; }
        private async void MessageOpenWithExecute(MessageViewModel message)
        {
            var result = message.Get().GetFileAndName(true);

            var file = result.File;
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var item = await ProtoService.GetFileAsync(file);
            if (item != null)
            {
                var options = new LauncherOptions();
                options.DisplayApplicationPicker = true;

                await Launcher.LaunchFileAsync(item, options);
            }
        }

        #endregion

        #region Show in folder

        public RelayCommand<MessageViewModel> MessageOpenFolderCommand { get; }
        private async void MessageOpenFolderExecute(MessageViewModel message)
        {
            var result = message.Get().GetFileAndName(true);

            var file = result.File;
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var item = await ProtoService.GetFileAsync(file);
            if (item != null)
            {
                var folder = await item.GetParentAsync();

                var options = new FolderLauncherOptions();
                options.ItemsToSelect.Add(item);

                await Launcher.LaunchFolderAsync(folder, options);
            }
        }

        #endregion

        #region Add contact

        public RelayCommand<MessageViewModel> MessageAddContactCommand { get; }
        private async void MessageAddContactExecute(MessageViewModel message)
        {
            var contact = message.Content as MessageContact;
            if (contact == null)
            {
                return;
            }

            var user = CacheService.GetUser(contact.Contact.UserId);
            if (user == null)
            {
                return;
            }

            var fullInfo = CacheService.GetUserFull(contact.Contact.UserId);
            if (fullInfo == null)
            {
                fullInfo = await ProtoService.SendAsync(new GetUserFullInfo(contact.Contact.UserId)) as UserFullInfo;
            }

            if (fullInfo == null)
            {
                return;
            }

            var dialog = new EditUserNamePopup(user.FirstName, user.LastName, fullInfo.NeedPhoneNumberPrivacyException);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new AddContact(new Telegram.Td.Api.Contact(user.PhoneNumber, dialog.FirstName, dialog.LastName, string.Empty, user.Id),
                    fullInfo.NeedPhoneNumberPrivacyException ? dialog.SharePhoneNumber : true));
            }
        }

        #endregion

        #region Service message

        public RelayCommand<MessageViewModel> MessageServiceCommand { get; }
        private async void MessageServiceExecute(MessageViewModel message)
        {
            if (message.Content is MessageHeaderDate)
            {
                var date = BindConvert.Current.DateTime(message.Date);

                var dialog = new CalendarPopup();
                dialog.MaxDate = DateTimeOffset.Now.Date;
                dialog.SelectedDates.Add(date);

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary && dialog.SelectedDates.Count > 0)
                {
                    var first = dialog.SelectedDates.FirstOrDefault();
                    var offset = first.Date.ToTimestamp();
                    await LoadDateSliceAsync(offset);
                }
            }
            else if (message.Content is MessagePinMessage pinMessage && pinMessage.MessageId != 0)
            {
                await LoadMessageSliceAsync(null, pinMessage.MessageId);
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

            ProtoService.Send(new SetPollAnswer(message.ChatId, message.Id, new int[0]));
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

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.StopPollAlertText, Strings.Resources.StopPollAlertTitle, Strings.Resources.Stop, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new StopPoll(message.ChatId, message.Id, null));
        }

        #endregion
    }
}
