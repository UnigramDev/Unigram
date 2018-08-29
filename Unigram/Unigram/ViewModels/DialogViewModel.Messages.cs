using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Telegram.Helpers;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Core.Services;
using Unigram.Native;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Payments;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml;
using Template10.Common;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        #region Reply

        public RelayCommand MessageReplyLastCommand { get; }
        private async void MessageReplyLastExecute()
        {
            var last = Items.LastOrDefault();
            if (last != null)
            {
                MessageReplyCommand.Execute(last);
                await ListField?.ScrollToItem(last, SnapPointsAlignment.Far, true, 4);
            }
        }

        public RelayCommand<MessageViewModel> MessageReplyCommand { get; }
        private void MessageReplyExecute(MessageViewModel message)
        {
            Search = null;

            if (message == null)
            {
                return;
            }

            //var serviceMessage = message as TLMessageService;
            //if (serviceMessage != null)
            //{
            //    var action = serviceMessage.Action;
            //    // TODO: 
            //    //if (action is TLMessageActionEmpty || action is TLMessageActionUnreadMessages)
            //    //{
            //    //    return;
            //    //}
            //}

            //var message31 = message as TLMessage;
            //if (message31 != null && message31.Media is TLMessageMediaGroup groupMedia)
            //{
            //    message = groupMedia.Layout.Messages.FirstOrDefault();
            //    message31 = message as TLMessage;
            //}

            //if (message.Id <= 0) return;

            //if (message31 != null && !message31.IsOut && message31.HasFromId)
            //{
            //    var fromId = message31.FromId.Value;
            //    var user = CacheService.GetUser(fromId) as TLUser;
            //    if (user != null && user.IsBot)
            //    {
            //        SetReplyMarkup(message31);
            //    }
            //}

            //Reply = message;
            EmbedData = new MessageEmbedData { ReplyToMessage = message };
            TextField?.Focus(Windows.UI.Xaml.FocusState.Keyboard);
        }

        #endregion

        #region Delete

        public RelayCommand<MessageViewModel> MessageDeleteCommand { get; }
        private async void MessageDeleteExecute(MessageViewModel message)
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

            if (message != null && !message.IsOutgoing && !message.IsChannelPost && chat.Type is ChatTypeSupergroup super && !super.IsChannel)
            {
                var sender = message.GetSenderUser();
                var dialog = new DeleteChannelMessageDialog(1, sender?.GetFullName());

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    if (dialog.DeleteAll)
                    {
                        ProtoService.Send(new DeleteChatMessagesFromUser(chat.Id, message.SenderUserId));
                    }
                    else
                    {
                        ProtoService.Send(new DeleteMessages(chat.Id, new[] { message.Id }, true));
                    }

                    if (dialog.BanUser)
                    {
                        ProtoService.Send(new SetChatMemberStatus(chat.Id, message.SenderUserId, new ChatMemberStatusBanned()));
                    }

                    if (dialog.ReportSpam && chat.Type is ChatTypeSupergroup supertype)
                    {
                        ProtoService.Send(new ReportSupergroupSpam(supertype.SupergroupId, message.SenderUserId, new[] { message.Id }));
                    }
                }
            }
            else
            {
                var dialog = new TLMessageDialog();
                dialog.Title = Strings.Resources.Message;
                dialog.Message = string.Format(Strings.Resources.AreYouSureDeleteMessages, Locale.Declension("Messages", 1));
                dialog.PrimaryButtonText = Strings.Resources.OK;
                dialog.SecondaryButtonText = Strings.Resources.Cancel;

                if (message.CanBeDeletedForAllUsers && message.CanBeDeletedOnlyForSelf)
                {
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        var user = ProtoService.GetUser(privata.UserId);
                        if (user != null && !(user.Type is UserTypeBot))
                        {
                            dialog.CheckBoxLabel = string.Format(Strings.Resources.DeleteForUser, ProtoService.GetTitle(chat));
                        }
                    }
                    else if (chat.Type is ChatTypeBasicGroup)
                    {
                        dialog.CheckBoxLabel = Strings.Resources.DeleteForAll;
                    }
                }

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    ProtoService.Send(new DeleteMessages(chat.Id, new[] { message.Id }, dialog.IsChecked == true));
                }
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

            Search = null;
            SelectionMode = ListViewSelectionMode.None;

            await ShareView.GetForCurrentView().ShowAsync(message.Get());
        }

        #endregion

        #region Share

        public RelayCommand<MessageViewModel> MessageShareCommand { get; }
        private async void MessageShareExecute(MessageViewModel message)
        {
            await ShareView.GetForCurrentView().ShowAsync(message.Get());
        }

        #endregion

        #region Multiple Delete

        public RelayCommand MessagesDeleteCommand { get; }
        private async void MessagesDeleteExecute()
        {
            var messages = new List<MessageViewModel>(SelectedItems);
            var message = messages.FirstOrDefault();
            if (message == null)
            {
                return;
            }

            var chat = message.GetChat();
            if (chat == null)
            {
                return;
            }

            //for (int i = 0; i < messages.Count; i++)
            //{
            //    if (messages[i] is TLMessage message && message.Media is TLMessageMediaGroup groupMedia)
            //    {
            //        messages.RemoveAt(i);

            //        for (int j = 0; j < groupMedia.Layout.Messages.Count; j++)
            //        {
            //            messages.Insert(i, groupMedia.Layout.Messages[j]);
            //            i++;
            //        }

            //        i--;
            //    }
            //}

            //if (messageBase == null) return;

            //var message = messageBase as TLMessage;
            //if (message != null && !message.IsOut && !message.IsPost && Peer is TLInputPeerChannel)
            //{
            //    var dialog = new DeleteChannelMessageDialog();

            //    var result = await dialog.ShowAsync();
            //    if (result == ContentDialogResult.Primary)
            //    {
            //        var channel = With as TLChannel;

            //        if (dialog.DeleteAll)
            //        {
            //            // TODO
            //        }
            //        else
            //        {
            //            var messages = new List<TLMessageBase>() { messageBase };
            //            if (messageBase.Id == 0 && messageBase.RandomId != 0L)
            //            {
            //                DeleteMessagesInternal(null, messages);
            //                return;
            //            }

            //            DeleteMessages(null, null, messages, true, null, DeleteMessagesInternal);
            //        }

            //        if (dialog.BanUser)
            //        {
            //            var response = await ProtoService.KickFromChannelAsync(channel, message.From.ToInputUser(), true);
            //            if (response.IsSucceeded)
            //            {
            //                var updates = response.Result as TLUpdates;
            //                if (updates != null)
            //                {
            //                    var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
            //                    if (newChannelMessageUpdate != null)
            //                    {
            //                        Aggregator.Publish(newChannelMessageUpdate.Message);
            //                    }
            //                }
            //            }
            //        }

            //        if (dialog.ReportSpam)
            //        {
            //            var response = await ProtoService.ReportSpamAsync(channel.ToInputChannel(), message.From.ToInputUser(), new TLVector<int> { message.Id });
            //        }
            //    }
            //}
            //else
            {
                var dialog = new TLMessageDialog();
                dialog.Title = Strings.Resources.Message;
                dialog.Message = string.Format(Strings.Resources.AreYouSureDeleteMessages, Locale.Declension("Messages", messages.Count));
                dialog.PrimaryButtonText = Strings.Resources.OK;
                dialog.SecondaryButtonText = Strings.Resources.Cancel;

                var canBeDeletedForAllUsers = messages.All(x => x.CanBeDeletedForAllUsers);
                var canBeDeletedOnlyForSelf = messages.All(x => x.CanBeDeletedOnlyForSelf);

                if (canBeDeletedForAllUsers && canBeDeletedOnlyForSelf)
                {
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        var user = ProtoService.GetUser(privata.UserId);
                        if (user != null && !(user.Type is UserTypeBot))
                        {
                            dialog.CheckBoxLabel = string.Format(Strings.Resources.DeleteForUser, ProtoService.GetTitle(chat));
                        }
                    }
                    else if (chat.Type is ChatTypeBasicGroup)
                    {
                        dialog.CheckBoxLabel = Strings.Resources.DeleteForAll;
                    }
                }

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    SelectionMode = ListViewSelectionMode.None;

                    ProtoService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), dialog.IsChecked == true));
                }
            }
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
                Search = null;
                SelectionMode = ListViewSelectionMode.None;

                await ShareView.GetForCurrentView().ShowAsync(messages);
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

                    if (message.ForwardInfo is MessageForwardedPost forwardedPost)
                    {
                        var from = ProtoService.GetChat(forwardedPost.ChatId);
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {ProtoService.GetTitle(from)}]");
                    }
                    else if (message.ForwardInfo is MessageForwardedFromUser forwardedFromUser)
                    {
                        var from = ProtoService.GetUser(forwardedFromUser.SenderUserId);
                        builder.AppendLine($"[{Strings.Resources.ForwardedMessage}]");
                        builder.AppendLine($"[{Strings.Resources.From} {from.GetFullName()}]");
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
                            builder.Append(photo.Caption);
                        }
                    }
                    else if (message.Content is MessageVoiceNote voiceNote)
                    {
                        builder.Append($"[{Strings.Resources.AttachAudio}]");

                        if (voiceNote.Caption != null && !string.IsNullOrEmpty(voiceNote.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.Append(voiceNote.Caption);
                        }
                    }
                    else if (message.Content is MessageVideo video)
                    {
                        builder.Append($"[{Strings.Resources.AttachVideo}]");

                        if (video.Caption != null && !string.IsNullOrEmpty(video.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.Append(video.Caption);
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
                            builder.Append(animation.Caption);
                        }
                    }
                    else if (message.Content is MessageSticker sticker)
                    {
                        if (!string.IsNullOrEmpty(sticker.Sticker.Emoji))
                        {
                            builder.Append($"[{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}]");
                        }
                        else
                        {
                            builder.Append($"[{Strings.Resources.AttachSticker}]");
                        }
                    }
                    else if (message.Content is MessageAudio audio)
                    {
                        builder.Append($"[{Strings.Resources.AttachMusic}]");

                        if (audio.Caption != null && !string.IsNullOrEmpty(audio.Caption.Text))
                        {
                            builder.AppendLine();
                            builder.Append(audio.Caption);
                        }
                    }
                    else if (message.Content is MessageLocation location)
                    {
                        builder.AppendLine($"[{Strings.Resources.AttachLocation}]");
                        builder.Append(string.Format(CultureInfo.InvariantCulture, "https://www.bing.com/maps/?pc=W8AP&FORM=MAPXSH&where1=44.312783,9.33426&locsearch=1", location.Location.Latitude, location.Location.Longitude));
                    }
                    else if (message.Content is MessageVenue venue)
                    {
                        builder.AppendLine($"[{Strings.Resources.AttachLocation}]");
                        builder.AppendLine(venue.Venue.Title);
                        builder.AppendLine(venue.Venue.Address);
                        builder.Append(string.Format(CultureInfo.InvariantCulture, "https://www.bing.com/maps/?pc=W8AP&FORM=MAPXSH&where1=44.312783,9.33426&locsearch=1", venue.Venue.Location.Latitude, venue.Venue.Location.Longitude));
                    }
                    else if (message.Content is MessageText text)
                    {
                        builder.Append(text.Text.Text);
                    }

                    if (message != messages.Last())
                    {
                        builder.AppendLine();
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

            var myId = ProtoService.GetMyId();
            var messages = SelectedItems.Where(x => x.SenderUserId != myId).OrderBy(x => x.Id).Select(x => x.Id).ToList();
            if (messages.Count < 1)
            {
                return;
            }

            var opt1 = new RadioButton { Content = Strings.Resources.ReportChatSpam, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt2 = new RadioButton { Content = Strings.Resources.ReportChatViolence, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt3 = new RadioButton { Content = Strings.Resources.ReportChatPornography, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt4 = new RadioButton { Content = Strings.Resources.ReportChatOther, HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = true };
            var stack = new StackPanel();
            stack.Children.Add(opt1);
            stack.Children.Add(opt2);
            stack.Children.Add(opt3);
            stack.Children.Add(opt4);
            stack.Margin = new Thickness(12, 16, 12, 0);

            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            dialog.Content = stack;
            dialog.Title = Strings.Resources.ReportChat;
            dialog.IsPrimaryButtonEnabled = true;
            dialog.IsSecondaryButtonEnabled = true;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var reason = opt1.IsChecked == true
                ? new ChatReportReasonSpam()
                : (opt2.IsChecked == true
                    ? new ChatReportReasonViolence()
                    : (opt3.IsChecked == true
                        ? new ChatReportReasonPornography()
                        : (ChatReportReason)new ChatReportReasonCustom()));

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

            var myId = ProtoService.GetMyId();
            return chat.CanBeReported && SelectedItems.Count > 0 && SelectedItems.All(x => x.SenderUserId != myId);
        }

        #endregion

        #region Select

        public RelayCommand<MessageViewModel> MessageSelectCommand { get; }
        private void MessageSelectExecute(MessageViewModel message)
        {
            Search = null;

            //var messageCommon = message as TLMessageCommonBase;
            //if (messageCommon == null)
            //{
            //    return;
            //}

            SelectionMode = ListViewSelectionMode.Multiple;
            ListField?.SelectedItems.Add(message);

            ExpandSelection(new[] { message });
        }

        #endregion

        #region Copy

        public RelayCommand<MessageViewModel> MessageCopyCommand { get; }
        private void MessageCopyExecute(MessageViewModel message)
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

            if (input != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(input.Text);
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

            var response = await ProtoService.SendAsync(new GetPublicMessageLink(chat.Id, message.Id, false));
            if (response is PublicMessageLink link)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(link.Link);
                ClipboardEx.TrySetContent(dataPackage);
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
                await ListField?.ScrollToItem(last, SnapPointsAlignment.Far, true, 4);
            }
        }

        public RelayCommand<MessageViewModel> MessageEditCommand { get; }
        private void MessageEditExecute(MessageViewModel message)
        {
            Search = null;
            CurrentInlineBot = null;
            EmbedData = new MessageEmbedData { EditingMessage = message };

            var input = message.Content.GetCaption();
            if (message.Content is MessageText text)
            {
                input = text.Text;
            }

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
            //        Execute.ShowDebugMessage("messages.getMessageEditData error " + response.Error);
            //    });
            //}
        }

        #endregion

        #region Pin

        public RelayCommand<MessageViewModel> MessagePinCommand { get; }
        private async void MessagePinExecute(MessageViewModel message)
        {
            var chat = message.GetChat();
            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                var fullInfo = ProtoService.GetSupergroupFull(supergroup.SupergroupId);
                if (fullInfo == null)
                {
                    return;
                }

                if (fullInfo.PinnedMessageId == message.Id)
                {
                    var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.UnpinMessageAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        ProtoService.Send(new UnpinSupergroupMessage(supergroup.SupergroupId));
                    }
                }
                else
                {
                    var dialog = new TLMessageDialog();
                    dialog.Title = Strings.Resources.AppName;
                    dialog.Message = supergroup.IsChannel ? Strings.Resources.PinMessageAlertChannel : Strings.Resources.PinMessageAlert;
                    dialog.PrimaryButtonText = Strings.Resources.OK;
                    dialog.SecondaryButtonText = Strings.Resources.Cancel;

                    if (!supergroup.IsChannel)
                    {
                        dialog.CheckBoxLabel = Strings.Resources.PinNotify;
                        dialog.IsChecked = true;
                    }

                    var confirm = await dialog.ShowQueuedAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        ProtoService.Send(new PinSupergroupMessage(supergroup.SupergroupId, message.Id, dialog.IsChecked == false));
                    }
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

            var opt1 = new RadioButton { Content = Strings.Resources.ReportChatSpam, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt2 = new RadioButton { Content = Strings.Resources.ReportChatViolence, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt3 = new RadioButton { Content = Strings.Resources.ReportChatPornography, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt4 = new RadioButton { Content = Strings.Resources.ReportChatOther, HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = true };
            var stack = new StackPanel();
            stack.Children.Add(opt1);
            stack.Children.Add(opt2);
            stack.Children.Add(opt3);
            stack.Children.Add(opt4);
            stack.Margin = new Thickness(12, 16, 12, 0);

            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            dialog.Content = stack;
            dialog.Title = Strings.Resources.ReportChat;
            dialog.IsPrimaryButtonEnabled = true;
            dialog.IsSecondaryButtonEnabled = true;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var reason = opt1.IsChecked == true
                ? new ChatReportReasonSpam()
                : (opt2.IsChecked == true
                    ? new ChatReportReasonViolence()
                    : (opt3.IsChecked == true
                        ? new ChatReportReasonPornography()
                        : (ChatReportReason)new ChatReportReasonCustom()));

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

        #region Keyboard button

        private Message _replyMarkupMessage;

        public Message EditedMessage
        {
            get
            {
                return null;
            }
        }

        public async void KeyboardButtonExecute(MessageViewModel message, object button)
        {
            if (button is InlineKeyboardButton inline)
            {
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
                        await TLMessageDialog.ShowAsync("Payments are coming soon!", Strings.Resources.AppName, "OK");
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
                else if (inline.Type is InlineKeyboardButtonTypeSwitchInline switchInline)
                {
                    //var bot = GetBot(message);
                    //if (bot != null)
                    //{
                    //    if (switchInline.InCurrentChat)
                    //    {
                    //        SetText(string.Format("@{0} {1}", bot.Username, switchInline.Query), focus: true);
                    //        ResolveInlineBot(bot.Username, switchInline.Query);

                    //        //if (With is TLChatBase)
                    //        //{
                    //        //    Reply = message;
                    //        //}
                    //    }
                    //    else
                    //    {
                    //        await ForwardView.Current.ShowAsync(switchInline, bot);
                    //    }
                    //}
                }
                else if (inline.Type is InlineKeyboardButtonTypeUrl urlButton)
                {
                    if (MessageHelper.TryCreateUri(urlButton.Url, out Uri uri))
                    {
                        if (MessageHelper.IsTelegramUrl(uri))
                        {
                            MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, urlButton.Url);
                        }
                        else
                        {
                            var confirm = await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.OpenUrlAlert, urlButton.Url), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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
                    var response = await ProtoService.SendAsync(new GetCallbackQueryAnswer(chat.Id, message.Id, new CallbackQueryPayloadData(callback.Data)));
                    if (response is CallbackQueryAnswer answer)
                    {
                        if (!string.IsNullOrEmpty(answer.Text))
                        {
                            if (answer.ShowAlert)
                            {
                                await new TLMessageDialog(answer.Text).ShowQueuedAsync();
                            }
                            else
                            {
                                //var bot = GetBot(message);
                                //if (bot == null)
                                //{
                                //    // TODO:
                                //    await new TLMessageDialog(response.Result.Message).ShowQueuedAsync();
                                //    return;
                                //}

                                //InformativeMessage = TLUtils.GetShortMessage(0, bot.Id, Peer.ToPeer(), date, response.Result.Message);
                            }
                        }
                        else if (!string.IsNullOrEmpty(answer.Url))
                        {
                            if (MessageHelper.TryCreateUri(answer.Url, out Uri uri))
                            {
                                if (MessageHelper.IsTelegramUrl(uri))
                                {
                                    MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, answer.Url);
                                }
                                else
                                {
                                    //var dialog = new TLMessageDialog(response.Result.Url, "Open this link?");
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
                        var bundle = new TdBundle();
                        bundle.Add("title", game.Game.Title);
                        bundle.Add("url", answer.Url);
                        bundle.Add("message", message.Id);
                        bundle.Add("chat", message.ChatId);

                        var viaBot = message.GetViaBotUser();
                        if (viaBot != null)
                        {
                            bundle.Add("username", viaBot.Username);
                        }

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

                        var confirm = await TLMessageDialog.ShowAsync(content, Strings.Resources.ShareYouPhoneNumberTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            await SendContactAsync(new Contact(cached.PhoneNumber, cached.FirstName, cached.LastName, string.Empty, cached.Id));
                        }
                    }
                }
                else if (keyboardButton.Type is KeyboardButtonTypeRequestLocation requestLocation)
                {
                    var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.ShareYouLocationInfo, Strings.Resources.ShareYouLocationTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        var location = await _locationService.GetPositionAsync();
                        if (location != null)
                        {
                            await SendMessageAsync(0, new InputMessageLocation(new Location(location.Point.Position.Latitude, location.Point.Position.Longitude), 0));
                        }
                    }
                }
                else if (keyboardButton.Type is KeyboardButtonTypeText textButton)
                {
                    await SendMessageAsync(keyboardButton.Text);
                }
            }
        }

        #endregion

        #region Sticker info

        public RelayCommand<MessageViewModel> MessageStickerPackInfoCommand { get; }
        private async void MessageStickerPackInfoExecute(MessageViewModel message)
        {

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

        #region Save to Downloads

        public RelayCommand<MessageViewModel> MessageSaveDownloadCommand { get; }
        private async void MessageSaveDownloadExecute(MessageViewModel message)
        {
            //if (message.IsSticker())
            //{
            //    MessageSaveStickerExecute(message);
            //    return;
            //}

            //var photo = message.GetPhoto();
            //if (photo?.Full is TLPhotoSize photoSize)
            //{
            //    await TLFileHelper.SavePhotoAsync(photoSize, message.Date, true);
            //}

            //var document = message.GetDocument();
            //if (document != null)
            //{
            //    await TLFileHelper.SaveDocumentAsync(document, message.Date, true);
            //}
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

        #region Add contact

        public RelayCommand<MessageViewModel> MessageAddContactCommand { get; }
        private async void MessageAddContactExecute(MessageViewModel message)
        {
            var contact = message.Content as MessageContact;
            if (contact == null)
            {
                return;
            }

            var user = ProtoService.GetUser(contact.Contact.UserId);
            if (user == null)
            {
                return;
            }

            if (user.OutgoingLink is LinkStateIsContact)
            {
                return;
            }

            var dialog = new EditUserNameView(user.FirstName, user.LastName);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new ImportContacts(new[] { new Contact(contact.Contact.PhoneNumber, dialog.FirstName, dialog.LastName, string.Empty, contact.Contact.UserId) }));
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

                var dialog = new Controls.Views.CalendarView();
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
    }
}
