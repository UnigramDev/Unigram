using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls.Messages;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel :
        IHandle<UpdateChatReplyMarkup>,
        IHandle<UpdateChatUnreadMentionCount>,
        IHandle<UpdateChatReadOutbox>,
        IHandle<UpdateChatReadInbox>,
        IHandle<UpdateChatDraftMessage>,
        IHandle<UpdateChatDefaultDisableNotification>,

        IHandle<UpdateUserChatAction>,

        IHandle<UpdateNewMessage>,
        IHandle<UpdateDeleteMessages>,

        IHandle<UpdateMessageContent>,
        IHandle<UpdateMessageContentOpened>,
        IHandle<UpdateMessageMentionRead>,
        IHandle<UpdateMessageEdited>,
        IHandle<UpdateMessageViews>,
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

        IHandle<UpdateFile>
    {

        public void Handle(UpdateUserChatAction update)
        {
            if (update.ChatId == _chat?.Id)
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
                BeginOnUIThread(() => Delegate?.UpdateNotificationSettings(_chat));
            }
        }

        #endregion

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
                        if (content is Grid grid)
                        {
                            content = grid.FindName("Bubble") as FrameworkElement;
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



        public void Handle(UpdateNewMessage update)
        {
            if (update.Message.ChatId == _chat?.Id)
            {
                var endReached = IsEndReached();
                BeginOnUIThread(() => InsertMessage(update.Message, endReached));
            }
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() =>
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        for (int j = 0; j < update.MessageIds.Count; j++)
                        {
                            var message = Items[i];
                            if (message.Id == update.MessageIds[j])
                            {
                                Items.RemoveAt(i);
                                i--;

                                break;
                            }
                            else if (message.ReplyToMessageId == update.MessageIds[j])
                            {
                                message.ReplyToMessage = null;
                                message.ReplyToMessageState = ReplyToMessageState.Deleted;

                                Handle(message, bubble => bubble.UpdateMessageReply(message), service => service.UpdateMessage(message));
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
            if (update.ChatId == _chat?.Id)
            {
                var supergroup = CacheService.GetSupergroupFull(_chat);

                Handle(update.MessageId, message =>
                {
                    message.Content = update.NewContent;
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

                        if (supergroup != null && supergroup.PinnedMessageId == message.Id)
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
                }, (bubble, message) => { });

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

        public void Handle(UpdateMessageViews update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    message.Views = update.Views;
                }, (bubble, message) => bubble.UpdateMessageViews(message));
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
            if (update.Message.ChatId == _chat?.Id)
            {
                Handle(update.OldMessageId, message =>
                {
                    message.Replace(update.Message);
                    ProcessFiles(_chat, new[] { message });
                }, (bubble, message) => bubble.UpdateMessage(message));
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

        private void Handle(long messageId, Action<MessageViewModel> update, Action<MessageBubble, MessageViewModel> action)
        {
            BeginOnUIThread(() =>
            {
                var message = Items.FirstOrDefault(x => x.Id == messageId);
                if (message == null)
                {
                    return;
                }

                update(message);

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
                if (content is Grid grid)
                {
                    content = grid.FindName("Bubble") as FrameworkElement;
                }

                if (content is MessageBubble bubble)
                {
                    action(bubble, message);
                }
            });
        }

        private void Handle(long messageId, Action<MessageViewModel> update, Action<MessageBubble, MessageViewModel, bool> action)
        {
            BeginOnUIThread(() =>
            {
                var field = ListField;
                if (field == null)
                {
                    return;
                }

                for (int i = 0; i < Items.Count; i++)
                {
                    var message = Items[i];
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
                            return;
                        }

                        var content = container.ContentTemplateRoot as FrameworkElement;
                        if (content is Grid grid)
                        {
                            content = grid.FindName("Bubble") as FrameworkElement;
                        }

                        if (content is MessageBubble bubble)
                        {
                            action(bubble, message, message.ReplyToMessageId == messageId);
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
            if (content is Grid grid)
            {
                content = grid.FindName("Bubble") as FrameworkElement;
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

        //public void Handle(TLUpdateContactLink update)
        //{
        //    if (With is TLUser user && user.Id == update.UserId)
        //    {
        //        BeginOnUIThread(() =>
        //        {
        //            IsShareContactAvailable = user.HasAccessHash && !user.HasPhone && !user.IsSelf && !user.IsContact && !user.IsMutualContact;
        //            IsAddContactAvailable = user.HasAccessHash && user.HasPhone && !user.IsSelf && !user.IsContact && !user.IsMutualContact;

        //            RaisePropertyChanged(() => With);

        //            //this.Subtitle = this.GetSubtitle();
        //            //base.NotifyOfPropertyChange<TLObject>(() => this.With);
        //            //this.ChangeUserAction();
        //        });
        //    }
        //}

        //public void Handle(TLUpdateDraftMessage args)
        //{
        //    var flag = false;

        //    var userBase = With as TLUserBase;
        //    var chatBase = With as TLChatBase;
        //    if (userBase != null && args.Peer is TLPeerUser && userBase.Id == args.Peer.Id)
        //    {
        //        flag = true;
        //    }
        //    else if (chatBase != null && args.Peer is TLPeerChat && chatBase.Id == args.Peer.Id)
        //    {
        //        flag = true;
        //    }
        //    else if (chatBase != null && args.Peer is TLPeerChannel && chatBase.Id == args.Peer.Id)
        //    {
        //        flag = true;
        //    }

        //    if (flag)
        //    {
        //        BeginOnUIThread(() =>
        //        {
        //            if (args.Draft is TLDraftMessage draft)
        //            {
        //                SetText(draft.Message, draft.Entities);
        //            }
        //            else if (args.Draft is TLDraftMessageEmpty emptyDraft)
        //            {
        //                SetText(null);
        //            }
        //        });
        //    }
        //}

        public void Handle(object MessageExpiredEventArgs)
        {
            //if (flag)
            //{
            //    BeginOnUIThread(() =>
            //    {
            //        var index = Items.IndexOf(message);
            //        if (index < 0)
            //        {
            //            return;
            //        }

            //        Items.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, message, index, index));
            //    });
            //}
        }

        private async void InsertMessage(Message message, bool endReached)
        {
            using (await _insertLock.WaitAsync())
            {
                //if (!IsFirstSliceLoaded)
                //{
                //    return;
                //}

                //if (IsEndReached())
                //if (endReached || IsEndReached())
                if (IsFirstSliceLoaded == true)
                {
                    var messageCommon = _messageFactory.Create(this, message);
                    var result = new List<MessageViewModel> { messageCommon };
                    ProcessFiles(_chat, result);
                    ProcessReplies(result);

                    InsertMessageInOrder(Items, messageCommon);
                }
                else if (message.IsOutgoing)
                {
                    var chat = _chat;
                    if (chat == null)
                    {
                        return;
                    }

                    //await LoadMessageSliceAsync(null, chat.LastReadInboxMessageId, VerticalAlignment.Bottom);
                    await LoadMessageSliceAsync(null, message.Id, VerticalAlignment.Bottom, 4);
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
