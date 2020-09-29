using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Controls.Messages;
using Unigram.Services;
using Unigram.Services.Updates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel :
        IHandle<UpdateWindowActivated>,
        IHandle<UpdateChatPermissions>,
        IHandle<UpdateChatReplyMarkup>,
        IHandle<UpdateChatUnreadMentionCount>,
        IHandle<UpdateChatReadOutbox>,
        IHandle<UpdateChatReadInbox>,
        //IHandle<UpdateChatDraftMessage>,
        IHandle<UpdateChatDefaultDisableNotification>,
        IHandle<UpdateChatPinnedMessage>,
        IHandle<UpdateChatActionBar>,
        IHandle<UpdateChatHasScheduledMessages>,

        IHandle<UpdateUserChatAction>,

        IHandle<UpdateChatLastMessage>,
        IHandle<UpdateNewMessage>,
        IHandle<UpdateDeleteMessages>,

        IHandle<UpdateMessageContent>,
        IHandle<UpdateMessageContentOpened>,
        IHandle<UpdateMessageMentionRead>,
        IHandle<UpdateMessageEdited>,
        IHandle<UpdateMessageInteractionInfo>,
        IHandle<UpdateMessageSendFailed>,
        IHandle<UpdateMessageSendSucceeded>,

        IHandle<UpdateUser>,
        IHandle<UpdateUserFullInfo>,
        IHandle<UpdateSecretChat>,
        IHandle<UpdateBasicGroup>,
        IHandle<UpdateBasicGroupFullInfo>,
        IHandle<UpdateSupergroup>,
        IHandle<UpdateSupergroupFullInfo>,
        IHandle<UpdateUserStatus>,
        IHandle<UpdateChatTitle>,
        IHandle<UpdateChatPhoto>,
        IHandle<UpdateChatNotificationSettings>,
        IHandle<UpdateChatOnlineMemberCount>,

        IHandle<UpdateFile>
    {

        public void Handle(UpdateUserChatAction update)
        {
            if (update.ChatId == _chat?.Id && (_type == DialogType.History || (_type == DialogType.Thread && update.MessageThreadId == _threadId)))
            {
                BeginOnUIThread(() => Delegate?.UpdateChatActions(_chat, CacheService.GetChatActions(update.ChatId)));
            }
        }

        #region Generic

        public void Handle(UpdateUser update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata && privata.UserId == update.User.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, update.User, false));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.User.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, update.User, true));
            }
        }

        public void Handle(UpdateUserFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata && privata.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ProtoService.GetUser(update.UserId), update.UserFullInfo, false, _accessToken != null));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ProtoService.GetUser(update.UserId), update.UserFullInfo, true, false));
            }
        }

        public void Handle(UpdateSecretChat update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSecret secret && secret.SecretChatId == update.SecretChat.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateSecretChat(chat, update.SecretChat));
            }
        }



        public void Handle(UpdateBasicGroup update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic && basic.BasicGroupId == update.BasicGroup.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateBasicGroup(chat, update.BasicGroup));
            }
        }

        public void Handle(UpdateBasicGroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic && basic.BasicGroupId == update.BasicGroupId)
            {
                BeginOnUIThread(() => Delegate?.UpdateBasicGroupFullInfo(chat, ProtoService.GetBasicGroup(update.BasicGroupId), update.BasicGroupFullInfo));
            }
        }



        public void Handle(UpdateSupergroup update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.SupergroupId == update.Supergroup.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateSupergroup(chat, update.Supergroup));
            }
        }

        public void Handle(UpdateSupergroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.SupergroupId == update.SupergroupId)
            {
                BeginOnUIThread(() => Delegate?.UpdateSupergroupFullInfo(chat, ProtoService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
                //BeginOnUIThread(() => Stickers?.UpdateSupergroupFullInfo(chat, ProtoService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
            }
        }



        public void Handle(UpdateChatTitle update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatTitle(_chat));
            }
        }

        public void Handle(UpdateChatPhoto update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatPhoto(_chat));
            }
        }

        public void Handle(UpdateUserStatus update)
        {
            if (_chat?.Type is ChatTypePrivate privata && privata.UserId == update.UserId || _chat?.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserStatus(_chat, ProtoService.GetUser(update.UserId)));
            }
        }

        public void Handle(UpdateChatNotificationSettings update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatNotificationSettings(_chat));
            }
        }

        public void Handle(UpdateChatOnlineMemberCount update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatOnlineMemberCount(_chat, update.OnlineMemberCount));
            }
        }

        public void Handle(UpdateChatPinnedMessage update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => ShowPinnedMessage(_chat));
            }
        }

        #endregion

        public void Handle(UpdateChatPermissions update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatPermissions(_chat));
            }
        }

        public void Handle(UpdateChatActionBar update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatActionBar(_chat));
            }
        }

        public void Handle(UpdateChatHasScheduledMessages update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatHasScheduledMessages(_chat));
            }
        }

        public async void Handle(UpdateChatReplyMarkup update)
        {
            if (update.ChatId == _chat?.Id)
            {
                var response = await ProtoService.SendAsync(new GetMessage(update.ChatId, update.ReplyMarkupMessageId));
                if (response is Message message)
                {
                    BeginOnUIThread(() => Delegate?.UpdateChatReplyMarkup(_chat, _messageFactory.Create(this, message)));
                }
                else
                {
                    BeginOnUIThread(() => Delegate?.UpdateChatReplyMarkup(_chat, null));
                }
            }
        }

        public void Handle(UpdateChatUnreadMentionCount update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatUnreadMentionCount(_chat, update.UnreadMentionCount));
            }
        }

        public void Handle(UpdateChatReadOutbox update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() =>
                {
                    var field = ListField;
                    if (field == null)
                    {
                        return;
                    }

                    var panel = field.ItemsPanelRoot as ItemsStackPanel;
                    if (panel == null)
                    {
                        return;
                    }

                    if (panel.FirstCacheIndex < 0)
                    {
                        return;
                    }

                    for (int i = panel.FirstCacheIndex; i <= panel.LastCacheIndex; i++)
                    {
                        var container = field.ContainerFromIndex(i) as ListViewItem;
                        if (container == null)
                        {
                            return;
                        }

                        var content = container.ContentTemplateRoot as FrameworkElement;
                        if (content is MessageBubble == false)
                        {
                            content = content.FindName("Bubble") as FrameworkElement;
                        }

                        if (content is MessageBubble bubble)
                        {
                            bubble.UpdateMessageState(Items[i]);
                        }
                    }
                });
            }
        }

        public void Handle(UpdateChatReadInbox update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() =>
                {
                    RaisePropertyChanged(() => UnreadCount);
                });
            }
        }

        public void Handle(UpdateChatDraftMessage update)
        {
            if (update.ChatId == _chat?.Id)
            {
                var header = _composerHeader;
                if (header?.EditingMessage != null)
                {
                    return;
                }

                BeginOnUIThread(() => ShowDraftMessage(_chat));
            }
        }

        public void Handle(UpdateChatDefaultDisableNotification update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatDefaultDisableNotification(_chat, update.DefaultDisableNotification));
            }
        }



        public void Handle(UpdateChatLastMessage update)
        {
            if (update.ChatId == _chat?.Id && update.LastMessage == null)
            {
                IsFirstSliceLoaded = IsEndReached();
            }

            if (update.ChatId == _chat?.Id && _chat.Type is ChatTypePrivate privata)
            {
                var user = CacheService.GetUser(privata.UserId);
                if (user != null && user.Type is UserTypeBot)
                {
                    var fullInfo = CacheService.GetUserFull(user.Id);
                    if (fullInfo != null)
                    {
                        BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(_chat, user, fullInfo, false, _accessToken != null));
                    }
                }
            }
        }

        private bool CheckSchedulingState(Message message)
        {
            if (_type == DialogType.ScheduledMessages)
            {
                return message.SchedulingState != null;
            }
            else if (_type == DialogType.Thread)
            {
                return message.SchedulingState == null && message.MessageThreadId == _threadId;
            }

            return message.SchedulingState == null && _type == DialogType.History;
        }

        public void Handle(UpdateNewMessage update)
        {
            if (update.Message.ChatId == _chat?.Id && CheckSchedulingState(update.Message))
            {
                var endReached = IsEndReached();
                BeginOnUIThread(() => InsertMessage(update.Message));

                if (!update.Message.IsOutgoing && Settings.Notifications.InAppSounds)
                {
                    _pushService.PlaySound();
                }
            }
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.ChatId == _chat?.Id && !update.FromCache)
            {
                BeginOnUIThread(async () =>
                {
                    using (await _insertLock.WaitAsync())
                    {
                        var table = new HashSet<long>(update.MessageIds);

                        for (int i = 0; i < Items.Count; i++)
                        {
                            var message = Items[i];
                            if (message.MediaAlbumId != 0 && message.Content is MessageAlbum album)
                            {
                                var found = false;
                                var invalidated = true;

                                for (int k = 0; k < album.Messages.Count; k++)
                                {
                                    if (table.Contains(album.Messages[k].Id))
                                    {
                                        album.Messages.RemoveAt(k);
                                        k--;

                                        if (album.Messages.Count > 0)
                                        {
                                            message.UpdateWith(album.Messages[0]);
                                            album.Invalidate();
                                        }
                                        else
                                        {
                                            invalidated = false;

                                            Items.RemoveAt(i);
                                            i--;
                                        }

                                        found = true;
                                        //break;
                                    }
                                }

                                if (found)
                                {
                                    if (invalidated)
                                    {
                                        Handle(new UpdateMessageContent(message.ChatId, message.Id, album));
                                    }

                                    continue;
                                }
                            }

                            if (table.Contains(message.Id))
                            {
                                Items.RemoveAt(i);
                                i--;
                            }
                            else if (table.Contains(message.ReplyToMessageId))
                            {
                                message.ReplyToMessage = null;
                                message.ReplyToMessageState = ReplyToMessageState.Deleted;

                                Handle(message, bubble => bubble.UpdateMessageReply(message), service => service.UpdateMessage(message));
                            }

                            if (i >= 0 && i == Items.Count - 1 && Items[i].Content is MessageHeaderUnread)
                            {
                                Items.RemoveAt(i);
                                i--;
                            }
                        }
                    }

                    foreach (var id in update.MessageIds)
                    {
                        if (_composerHeader != null && _composerHeader.Matches(id))
                        {
                            ClearReplyCommand.Execute();
                            break;
                        }
                    }
                });
            }
        }



        public void Handle(UpdateMessageContent update)
        {
            if (update.ChatId == _chat?.Id || update.ChatId == _migratedChat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    if (update.NewContent is MessageAlbum)
                    {
                    }
                    //else if (update.NewContent is MessageExpiredPhoto || update.NewContent is MessageExpiredVideo)
                    //{
                    //    // Probably not the best way
                    //    Items.Remove(message);
                    //    message.Content = update.NewContent;
                    //    InsertMessageInOrder(Items, message);
                    //}
                    else
                    {
                        message.Content = update.NewContent;
                    }

                    ProcessFiles(_chat, new[] { message });
                }, (bubble, message, reply) =>
                {
                    if (reply)
                    {
                        bubble.UpdateMessageReply(message);
                    }
                    else
                    {
                        bubble.UpdateMessageContent(message);
                        Delegate?.ViewVisibleMessages(false);

                        if (_chat?.PinnedMessageId == message.Id)
                        {
                            Delegate?.UpdatePinnedMessage(_chat, message, false);
                        }
                    }
                });
            }
        }

        public void Handle(UpdateMessageContentOpened update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    message.TtlExpiresIn = message.Ttl;

                    switch (message.Content)
                    {
                        case MessageVideoNote videoNote:
                            videoNote.IsViewed = true;
                            break;
                        case MessageVoiceNote voiceNote:
                            voiceNote.IsListened = true;
                            break;
                    }
                }, (bubble, message) => bubble.UpdateMessageContentOpened(message));
            }
        }

        public void Handle(UpdateMessageMentionRead update)
        {
            if (update.ChatId == _chat?.Id)
            {
                if (_mentions != null && _mentions.Contains(update.MessageId))
                {
                    _mentions.Remove(update.MessageId);
                }

                Handle(update.MessageId, message =>
                {
                    message.ContainsUnreadMention = false;
                });

                BeginOnUIThread(() => Delegate?.UpdateChatUnreadMentionCount(_chat, update.UnreadMentionCount));
            }
        }

        public void Handle(UpdateMessageEdited update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    message.EditDate = update.EditDate;
                    message.ReplyMarkup = update.ReplyMarkup;
                }, (bubble, message) => bubble.UpdateMessageEdited(message));
            }
        }

        public void Handle(UpdateMessageInteractionInfo update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    message.InteractionInfo = update.InteractionInfo;
                }, (bubble, message) => bubble.UpdateMessageInteractionInfo(message));
            }
        }

        public void Handle(UpdateMessageSendFailed update)
        {
            if (update.Message.ChatId == _chat?.Id)
            {
                Handle(update.OldMessageId, message =>
                {
                    message.Replace(update.Message);
                }, (bubble, message) => bubble.UpdateMessage(message));
            }
        }

        public void Handle(UpdateMessageSendSucceeded update)
        {
            if (update.Message.ChatId == _chat?.Id && CheckSchedulingState(update.Message))
            {
                //if (Items.Count > 0 && Items[Items.Count - 1].Id == update.OldMessageId)
                //{
                Handle(update.OldMessageId, message =>
                {
                    message.Replace(update.Message);
                    message.GeneratedContentUnread = true;
                    ProcessFiles(_chat, new[] { message });
                }, (bubble, message) =>
                {
                    bubble.UpdateMessage(message);
                    Delegate?.ViewVisibleMessages(false);
                });
                //}
                //else
                //{
                //    var endReached = IsEndReached();
                //    BeginOnUIThread(() => InsertMessage(update.Message, update.OldMessageId));
                //}

                if (Settings.Notifications.InAppSounds)
                {
                    _pushService.PlaySound();
                }
            }
        }

        public void Handle(UpdateFile update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            BeginOnUIThread(() => Delegate?.UpdateFile(update.File));

            var header = _composerHeader;
            if (header?.EditingMessageMedia != null && header?.EditingMessageFileId == update.File.Id && update.File.Size == update.File.Remote.UploadedSize)
            {
                BeginOnUIThread(() => ComposerHeader = null);
                ProtoService.Send(new EditMessageMedia(chat.Id, header.EditingMessage.Id, null, header.EditingMessageMedia.Delegate(new InputFileId(update.File.Id), header.EditingMessageCaption)));
            }
        }

        private void Handle(long messageId, Action<MessageViewModel> update, Action<MessageBubble, MessageViewModel> action = null)
        {
            BeginOnUIThread(async () =>
            {
                var field = ListField;
                if (field == null)
                {
                    return;
                }

                using (await _insertLock.WaitAsync())
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        var message = Items[i];
                        if (message.Content is MessageAlbum album)
                        {
                            var found = false;

                            if (album.Messages.TryGetValue(messageId, out MessageViewModel child))
                            {
                                update(child);
                                found = true;

                                message.UpdateWith(album.Messages[0]);
                                album.Invalidate();

                                if (action == null)
                                {
                                    break;
                                }

                                var container = field.ContainerFromItem(message) as ListViewItem;
                                if (container == null)
                                {
                                    break;
                                }

                                var content = container.ContentTemplateRoot as FrameworkElement;
                                if (content is MessageBubble == false)
                                {
                                    content = content.FindName("Bubble") as FrameworkElement;
                                }

                                if (content is MessageBubble bubble)
                                {
                                    action(bubble, message);
                                }
                            }

                            if (found)
                            {
                                return;
                            }
                        }

                        if (message.Id == messageId)
                        {
                            update(message);

                            if (action == null)
                            {
                                return;
                            }

                            var container = field.ContainerFromItem(message) as ListViewItem;
                            if (container == null)
                            {
                                return;
                            }

                            var content = container.ContentTemplateRoot as FrameworkElement;
                            if (content is MessageBubble == false)
                            {
                                content = content.FindName("Bubble") as FrameworkElement;
                            }

                            if (content is MessageBubble bubble)
                            {
                                action(bubble, message);
                            }
                        }
                    }
                }
            });
        }

        private void Handle(long messageId, Action<MessageViewModel> update, Action<MessageBubble, MessageViewModel, bool> action)
        {
            BeginOnUIThread(async () =>
            {
                var field = ListField;
                if (field == null)
                {
                    return;
                }

                using (await _insertLock.WaitAsync())
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        var message = Items[i];
                        if (message.Content is MessageAlbum album)
                        {
                            var found = false;

                            foreach (var child in album.Messages)
                            {
                                if (child.Id == messageId)
                                {
                                    update(child);
                                    found = true;

                                    message.UpdateWith(album.Messages[0]);
                                    album.Invalidate();

                                    var container = field.ContainerFromItem(message) as ListViewItem;
                                    if (container == null)
                                    {
                                        break;
                                    }

                                    var content = container.ContentTemplateRoot as FrameworkElement;
                                    if (content is MessageBubble == false)
                                    {
                                        content = content.FindName("Bubble") as FrameworkElement;
                                    }

                                    if (content is MessageBubble bubble)
                                    {
                                        action(bubble, message, false);
                                    }

                                    break;
                                }
                            }

                            if (found)
                            {
                                continue;
                            }
                        }


                        if (message.Id == messageId || (message.ReplyToMessageId == messageId && message.ReplyToMessage != null))
                        {
                            if (message.Id == messageId)
                            {
                                update(message);
                            }
                            else if (message.ReplyToMessageId == messageId)
                            {
                                update(message.ReplyToMessage);
                            }

                            var container = field.ContainerFromItem(message) as ListViewItem;
                            if (container == null)
                            {
                                continue;
                            }

                            var content = container.ContentTemplateRoot as FrameworkElement;
                            if (content is MessageBubble == false)
                            {
                                content = content.FindName("Bubble") as FrameworkElement;
                            }

                            if (content is MessageBubble bubble)
                            {
                                action(bubble, message, message.ReplyToMessageId == messageId);
                            }
                        }
                    }
                }
            });
        }

        private void Handle(MessageViewModel message, Action<MessageBubble> action1, Action<MessageService> action2)
        {
            var field = ListField;
            if (field == null)
            {
                return;
            }

            var container = field.ContainerFromItem(message) as ListViewItem;
            if (container == null)
            {
                return;
            }

            var content = container.ContentTemplateRoot as FrameworkElement;
            if (content is MessageBubble == false)
            {
                content = content.FindName("Bubble") as FrameworkElement;
            }

            if (content is MessageBubble bubble)
            {
                action1(bubble);
            }
            else if (content is MessageService service)
            {
                action2(service);
            }
        }

        private async void InsertMessage(Message message, long? oldMessageId = null)
        {
            using (await _insertLock.WaitAsync())
            {
                //if (!IsFirstSliceLoaded)
                //{
                //    return;
                //}

                //if (IsEndReached())
                //if (endReached || IsEndReached())
                if (IsFirstSliceLoaded == true || Type == DialogType.ScheduledMessages)
                {
                    var messageCommon = _messageFactory.Create(this, message);
                    messageCommon.GeneratedContentUnread = true;

                    var result = new List<MessageViewModel> { messageCommon };
                    await ProcessMessagesAsync(_chat, result);

                    if (result.Count > 0)
                    {
                        if (oldMessageId != null)
                        {
                            var oldMessage = Items.FirstOrDefault(x => x.Id == oldMessageId);
                            if (oldMessage != null)
                            {
                                Items.Remove(oldMessage);
                            }
                        }

                        InsertMessageInOrder(Items, result[0]);
                    }
                }
                else if (message.IsOutgoing)
                {
                    await LoadMessageSliceAsync(null, message.Id, VerticalAlignment.Bottom);
                }
            }
        }

        public static int InsertMessageInOrder(IList<MessageViewModel> messages, MessageViewModel message)
        {
            var position = -1;

            if (messages.Count == 0)
            {
                position = 0;
            }

            for (var i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].Id == 0)
                {
                    if (messages[i].Date < message.Date)
                    {
                        position = i + 1;
                        break;
                    }

                    continue;
                }

                if (messages[i].Id == message.Id)
                {
                    position = -1;
                    break;
                }
                if (messages[i].Id < message.Id)
                {
                    position = i + 1;
                    break;
                }
            }

            if (position != -1)
            {
                messages.Insert(position, message);
            }

            return position;
        }
    }
}
