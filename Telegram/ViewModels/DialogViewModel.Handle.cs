//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Controls.Messages.Content;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Core;
using Microsoft.UI.Xaml;

namespace Telegram.ViewModels
{
    public partial class DialogViewModel : IHandle
    {
        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateChatSwitchInlineQuery>(this, Handle)
                .Subscribe<UpdateChatActiveStories>(Handle)
                .Subscribe<UpdateChatPermissions>(Handle)
                .Subscribe<UpdateChatReplyMarkup>(Handle)
                .Subscribe<UpdateChatUnreadMentionCount>(Handle)
                .Subscribe<UpdateChatUnreadReactionCount>(Handle)
                .Subscribe<UpdateChatReadOutbox>(Handle)
                .Subscribe<UpdateChatReadInbox>(Handle)
                .Subscribe<UpdateChatDraftMessage>(Handle)
                .Subscribe<UpdateChatDefaultDisableNotification>(Handle)
                .Subscribe<UpdateChatMessageSender>(Handle)
                .Subscribe<UpdateChatActionBar>(Handle)
                .Subscribe<UpdateChatIsTranslatable>(Handle)
                .Subscribe<UpdateChatHasScheduledMessages>(Handle)
                .Subscribe<UpdateChatVideoChat>(Handle)
                .Subscribe<UpdateChatPendingJoinRequests>(Handle)
                .Subscribe<UpdateChatAction>(Handle)
                .Subscribe<UpdateChatLastMessage>(Handle)
                .Subscribe<UpdateChatBusinessBotManageBar>(Handle)
                .Subscribe<UpdateNewMessage>(Handle)
                .Subscribe<UpdateDeleteMessages>(Handle)
                .Subscribe<UpdateMessageContent>(Handle)
                .Subscribe<UpdateMessageContentOpened>(Handle)
                .Subscribe<UpdateMessageMentionRead>(Handle)
                .Subscribe<UpdateMessageUnreadReactions>(Handle)
                .Subscribe<UpdateMessageEdited>(Handle)
                .Subscribe<UpdateMessageInteractionInfo>(Handle)
                .Subscribe<UpdateMessageIsPinned>(Handle)
                .Subscribe<UpdateMessageSendFailed>(Handle)
                .Subscribe<UpdateMessageSendSucceeded>(Handle)
                .Subscribe<UpdateMessageTranslatedText>(Handle)
                .Subscribe<UpdateMessageFactCheck>(Handle)
                .Subscribe<UpdateMessageEffect>(Handle)
                .Subscribe<UpdateAnimatedEmojiMessageClicked>(Handle)
                .Subscribe<UpdateUser>(Handle)
                .Subscribe<UpdateUserFullInfo>(Handle)
                .Subscribe<UpdateSecretChat>(Handle)
                .Subscribe<UpdateBasicGroup>(Handle)
                .Subscribe<UpdateBasicGroupFullInfo>(Handle)
                .Subscribe<UpdateSupergroup>(Handle)
                .Subscribe<UpdateSupergroupFullInfo>(Handle)
                .Subscribe<UpdateUserStatus>(Handle)
                .Subscribe<UpdateChatTitle>(Handle)
                .Subscribe<UpdateChatPhoto>(Handle)
                .Subscribe<UpdateChatEmojiStatus>(Handle)
                .Subscribe<UpdateChatTheme>(Handle)
                .Subscribe<UpdateChatBackground>(Handle)
                .Subscribe<UpdateChatNotificationSettings>(Handle)
                .Subscribe<UpdateChatOnlineMemberCount>(Handle)
                .Subscribe<UpdateChatVideoChat>(Handle)
                .Subscribe<UpdateGroupCall>(Handle)
                .Subscribe<UpdateSpeechRecognitionTrial>(Handle)
                .Subscribe<UpdateSavedMessagesTags>(Handle)
                .Subscribe<UpdateGreetingSticker>(Handle)
                .Subscribe<UpdateQuickReplyShortcut>(Handle);
        }

        public void Handle(UpdateQuickReplyShortcut update)
        {
            if (QuickReplyShortcut?.Name == update.Shortcut.Name)
            {
                BeginOnUIThread(() => QuickReplyShortcut = update.Shortcut);
            }
        }

        public void Handle(UpdateGreetingSticker update)
        {
            if (_greetingSticker == null)
            {
                BeginOnUIThread(() => GreetingSticker = update.Sticker);
            }
        }

        public void Handle(UpdateSpeechRecognitionTrial update)
        {
            if (_needsUpdateSpeechRecognitionTrial)
            {
                _needsUpdateSpeechRecognitionTrial = false;
                BeginOnUIThread(() => ShowSpeechRecognitionTrial(update.LeftCount > 0 ? 1 : 2));
            }
        }

        public void Handle(UpdateSavedMessagesTags update)
        {
            if (_chat?.Id == ClientService.Options.MyId)
            {
                //if (update.SavedMessagesTopic.AreTheSame(_savedMessagesTopic))
                //{
                //    BeginOnUIThread(() => SavedMessagesTags = update.Tags);
                //}
                //else
                {
                    ClientService.Send(new GetSavedMessagesTags(SavedMessagesTopicId), result =>
                    {
                        if (result is SavedMessagesTags tags)
                        {
                            BeginOnUIThread(() => SavedMessagesTags = tags);
                        }
                    });
                }
            }
        }

        public void Handle(UpdateChatAction update)
        {
            if (update.ChatId == _chat?.Id && update.MessageThreadId == ThreadId && (_type == DialogType.History || _type == DialogType.Thread))
            {
                BeginOnUIThread(() => Delegate?.UpdateChatActions(_chat, ClientService.GetChatActions(update.ChatId)));
            }
        }

        public void Handle(UpdateChatBusinessBotManageBar update)
        {
            if (update.ChatId == _chat?.Id && _type == DialogType.History)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatBusinessBotManageBar(_chat, update.BusinessBotManageBar));
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
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, update.User, ClientService.GetUserFull(update.User.Id), false, _accessToken != null));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.User.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, update.User, ClientService.GetUserFull(update.User.Id), true, false));
            }
        }

        public void Handle(UpdateUserFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata && privata.UserId == update.UserId && ClientService.TryGetUser(update.UserId, out User user))
            {
                UpdateEmptyState(user, update.UserFullInfo, true);
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, user, update.UserFullInfo, false, _accessToken != null));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ClientService.GetUser(update.UserId), update.UserFullInfo, true, false));
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
                BeginOnUIThread(() => Delegate?.UpdateBasicGroupFullInfo(chat, update.BasicGroup, ClientService.GetBasicGroupFull(update.BasicGroup.Id)));
            }
        }

        public void Handle(UpdateBasicGroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic && basic.BasicGroupId == update.BasicGroupId && ClientService.TryGetBasicGroup(update.BasicGroupId, out BasicGroup basicGroup))
            {
                BeginOnUIThread(() => Delegate?.UpdateBasicGroupFullInfo(chat, basicGroup, update.BasicGroupFullInfo));
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
                BeginOnUIThread(() => Delegate?.UpdateSupergroupFullInfo(chat, update.Supergroup, ClientService.GetSupergroupFull(update.Supergroup.Id)));
            }
        }

        public void Handle(UpdateSupergroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.SupergroupId == update.SupergroupId && ClientService.TryGetSupergroup(update.SupergroupId, out Supergroup supergroup))
            {
                BeginOnUIThread(() =>
                {
                    Delegate?.UpdateSupergroupFullInfo(chat, supergroup, update.SupergroupFullInfo);
                });
            }
        }

        public void Handle(UpdateChatVideoChat update)
        {
            if (_chat?.Id == update.ChatId)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatVideoChat(_chat, update.VideoChat));
            }
        }

        public void Handle(UpdateGroupCall update)
        {
            if (_chat?.VideoChat?.GroupCallId == update.GroupCall.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateGroupCall(_chat, update.GroupCall));
            }
        }

        private async void UpdateGroupCall(Chat chat, int groupCallId)
        {
            if (groupCallId == 0)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetGroupCall(groupCallId));
            if (response is GroupCall groupCall)
            {
                BeginOnUIThread(() => Delegate?.UpdateGroupCall(chat, groupCall));
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

        public void Handle(UpdateChatEmojiStatus update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatEmojiStatus(_chat));
            }
        }

        public void Handle(UpdateChatTheme update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatTheme(_chat));
            }
        }

        public void Handle(UpdateChatBackground update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatBackground(_chat));
            }
        }

        public void Handle(UpdateUserStatus update)
        {
            if (_chat?.Type is ChatTypePrivate privata && privata.UserId == update.UserId || _chat?.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserStatus(_chat, ClientService.GetUser(update.UserId)));
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
                BeginOnUIThread(() =>
                {
                    if (update.OnlineMemberCount > 1)
                    {
                        OnlineCount = Locale.Declension(Strings.R.OnlineCount, update.OnlineMemberCount);
                    }
                    else
                    {
                        OnlineCount = null;
                    }
                });
            }
        }

        #endregion

        public void Handle(UpdateChatSwitchInlineQuery update)
        {
            if (update.ChatId == _chat?.Id)
            {
                var bot = ClientService.GetUser(update.BotUserId);
                if (bot == null || !bot.HasActiveUsername(out string username))
                {
                    return;
                }

                SetText(string.Format("@{0} {1}", username, update.Query), focus: true);
                ResolveInlineBot(username, update.Query);
            }
        }

        public void Handle(UpdateChatPendingJoinRequests update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatPendingJoinRequests(_chat));
            }
        }

        public void Handle(UpdateChatActiveStories update)
        {
            if (update.ActiveStories.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatActiveStories(_chat));
            }
        }

        public void Handle(UpdateChatPermissions update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatPermissions(_chat));
            }
        }

        public void Handle(UpdateChatActionBar update)
        {
            if (update.ChatId == _chat?.Id && _type == DialogType.History)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatActionBar(_chat));
            }
        }

        public void Handle(UpdateChatIsTranslatable update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatIsTranslatable(_chat, _languageDetected));
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
                var response = await ClientService.SendAsync(new GetMessage(update.ChatId, update.ReplyMarkupMessageId));
                if (response is Message message)
                {
                    BeginOnUIThread(() => Delegate?.UpdateChatReplyMarkup(_chat, CreateMessage(message)));
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

        public void Handle(UpdateChatUnreadReactionCount update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatUnreadReactionCount(_chat, update.UnreadReactionCount));
            }
        }

        public void Handle(UpdateChatReadOutbox update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() =>
                {
                    Delegate?.ForEach((bubble, message) => bubble.UpdateMessageState(message));
                });
            }
        }

        public void Handle(UpdateChatReadInbox update)
        {
            if (update.ChatId == _chat?.Id && _type == DialogType.History)
            {
                BeginOnUIThread(() =>
                {
                    RaisePropertyChanged(nameof(UnreadCount));
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

                BeginOnUIThread(() => ShowDraftMessage(_chat, false));
            }
        }

        public void Handle(UpdateChatDefaultDisableNotification update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatDefaultDisableNotification(_chat, update.DefaultDisableNotification));
            }
        }

        public void Handle(UpdateChatMessageSender update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatMessageSender(_chat, update.MessageSenderId));
            }
        }



        public void Handle(UpdateChatLastMessage update)
        {
            if (update.ChatId == _chat?.Id && update.LastMessage == null)
            {
                IsFirstSliceLoaded = null;
            }

            if (update.ChatId == _chat?.Id && _chat.Type is ChatTypePrivate privata)
            {
                var user = ClientService.GetUser(privata.UserId);
                if (user == null)
                {
                    return;
                }

                if (user.Type is UserTypeBot && ClientService.TryGetUserFull(user.Id, out UserFullInfo fullInfo))
                {
                    BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(_chat, user, fullInfo, false, _accessToken != null));
                }
                else
                {
                    UpdateEmptyState(user, null, true);
                }
            }
        }

        private void UpdateEmptyState(User user, UserFullInfo fullInfo, bool onlyLocal)
        {
            if (_chat is not Chat chat)
            {
                return;
            }

            var empty = chat.LastMessage == null;
            if (empty && _isChatEmpty)
            {
                return;
            }
            else if (empty == _isChatEmpty && fullInfo == null)
            {
                return;
            }

            _isChatEmpty = empty;

            fullInfo ??= ClientService.GetUserFull(user.Id);

            if (fullInfo == null)
            {
                return;
            }

            if (user.RestrictsNewChats)
            {
                ClientService.Send(new CanSendMessageToUser(user.Id, onlyLocal), result =>
                {
                    BeginOnUIThread(() =>
                    {
                        RestrictsNewChats = result is CanSendMessageToUserResultUserRestrictsNewChats;

                        GreetingSticker ??= ClientService.NextGreetingSticker();
                        Delegate?.UpdateUserRestrictsNewChats(_chat, user, fullInfo, result as CanSendMessageToUserResult);
                    });
                });
            }
            else
            {
                BeginOnUIThread(() =>
                {
                    RestrictsNewChats = false;

                    GreetingSticker ??= ClientService.NextGreetingSticker();
                    Delegate?.UpdateUserRestrictsNewChats(_chat, user, fullInfo, null);
                });
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
                if (_topic != null && _topic.Info.IsGeneral)
                {
                    return message.SchedulingState == null && !message.IsTopicMessage;
                }

                return message.SchedulingState == null && message.MessageThreadId == ThreadId;
            }
            else if (_type == DialogType.SavedMessagesTopic)
            {
                return message.SchedulingState == null && message.SavedMessagesTopicId == SavedMessagesTopicId;
            }
            else if (_type == DialogType.Pinned)
            {
                return message.SchedulingState == null && message.IsPinned;
            }

            return message.SchedulingState == null && _type == DialogType.History;
        }

        public void Handle(UpdateNewMessage update)
        {
            if (update.Message.ChatId == _chat?.Id && CheckSchedulingState(update.Message))
            {
                var message = CreateMessage(update.Message);
                message.GeneratedContentUnread = true;
                message.IsInitial = false;

                BeginOnUIThread(() =>
                {
                    InsertMessage(message);

                    if (!update.Message.IsOutgoing && Settings.Notifications.InAppSounds)
                    {
                        if (WindowContext.Current.ActivationMode == CoreWindowActivationMode.ActivatedInForeground)
                        {
                            _notificationsService.PlaySound();
                        }
                    }
                });
            }
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.ChatId == _chat?.Id && !update.FromCache)
            {
                var table = update.MessageIds.ToHashSet();

                BeginOnUIThread(async () =>
                {
                    using (await _loadMoreLock.WaitAsync())
                    {
                        List<MessageViewModel> toBeDeleted = null;

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

                                            _groupedMessages.TryRemove(message.MediaAlbumId, out _);

                                            toBeDeleted ??= new();
                                            toBeDeleted.Add(message);
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
                                toBeDeleted ??= new();
                                toBeDeleted.Add(message);
                            }
                            else if (message.ReplyTo is MessageReplyToMessage replyToMessage && table.Contains(replyToMessage.MessageId))
                            {
                                message.ReplyToItem = null;
                                message.ReplyToState = MessageReplyToState.Deleted;

                                Handle(message, bubble => bubble.UpdateMessageReply(message), service => service.UpdateMessage(message));
                            }

                            if (i >= 0 && i == Items.Count - 1 && Items[i].Content is MessageHeaderUnread)
                            {
                                toBeDeleted ??= new();
                                toBeDeleted.Add(message);
                            }
                        }

                        if (toBeDeleted != null)
                        {
                            //Delegate?.UpdateDeleteMessages(_chat, toBeDeleted);

                            foreach (var item in toBeDeleted)
                            {
                                Items.Remove(item);
                            }
                        }

                        if (Items.Count > 0 && Items[^1].Content is MessageHeaderUnread)
                        {
                            Items.RemoveAt(Items.Count - 1);
                        }
                    }

                    foreach (var id in update.MessageIds)
                    {
                        if (_composerHeader != null && _composerHeader.Matches(id))
                        {
                            ClearReply();
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
                Handle(update.MessageId, message =>
                {
                    if (update.NewContent is not MessageAlbum and not MessageStory)
                    {
                        message.Reset();
                        message.Content = update.NewContent;

                        if (IsTranslating)
                        {
                            _translateService.Translate(message, Settings.Translate.To);
                        }

                        ProcessEmoji(message);

                        if (update.NewContent is MessageExpiredPhoto or MessageExpiredVideo or MessageExpiredVideoNote or MessageExpiredVoiceNote)
                        {
                            // Probably not the best way
                            Items.Remove(message);
                            InsertMessageInOrder(Items, message);
                        }
                    }
                }, (bubble, message, reply) =>
                {
                    if (reply)
                    {
                        bubble.UpdateMessageReply(message);
                    }
                    else
                    {
                        bubble.UpdateMessageContent(message);
                        Delegate?.ViewVisibleMessages();
                    }
                });

                //BeginOnUIThread(() =>
                //{
                //    for (int i = 0; i < PinnedMessages.Count; i++)
                //    {
                //        if (PinnedMessages[i].Id == update.MessageId)
                //        {
                //            PinnedMessages[i].Content = update.NewContent;
                //            Delegate?.UpdatePinnedMessage();

                //            break;
                //        }
                //    }
                //});
            }
        }

        public void Handle(UpdateMessageContentOpened update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    // TODO: this makes no sense
                    if (message.SelfDestructType is MessageSelfDestructTypeTimer timer)
                    {
                        message.SelfDestructIn = timer.SelfDestructTime;
                    }

                    switch (message.Content)
                    {
                        case MessageVideoNote videoNote:
                            videoNote.IsViewed = true;
                            break;
                        case MessageVoiceNote voiceNote:
                            voiceNote.IsListened = true;
                            break;
                    }

                    return true;
                },
                (bubble, message) => bubble.UpdateMessageContentOpened(message));
            }
        }

        public void Handle(UpdateMessageMentionRead update)
        {
            if (update.ChatId == _chat?.Id)
            {
                _mentions.RemoveMessage(update.MessageId);

                Handle(update.MessageId, message =>
                {
                    message.ContainsUnreadMention = false;
                    return false;
                });

                BeginOnUIThread(() => Delegate?.UpdateChatUnreadMentionCount(_chat, update.UnreadMentionCount));
            }
        }

        public void Handle(UpdateMessageUnreadReactions update)
        {
            if (update.ChatId == _chat?.Id)
            {
                _reactions.RemoveMessage(update.MessageId);

                Handle(update.MessageId, message =>
                {
                    message.UnreadReactions = update.UnreadReactions;
                    return true;
                },
                (bubble, message) =>
                {
                    Delegate?.ViewVisibleMessages();
                });

                BeginOnUIThread(() => Delegate?.UpdateChatUnreadReactionCount(_chat, update.UnreadReactionCount));
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
                    return true;
                },
                (bubble, message) => bubble.UpdateMessageEdited(message));
            }
        }

        public void Handle(UpdateMessageInteractionInfo update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    message.InteractionInfo = update.InteractionInfo;
                    return true;
                },
                (bubble, message) => bubble.UpdateMessageInteractionInfo(message));
            }
        }

        public void Handle(UpdateMessageIsPinned update)
        {
            if (update.ChatId == _chat?.Id)
            {
                if (_type == DialogType.Pinned)
                {
                    if (update.IsPinned)
                    {
                        ClientService.Send(new GetMessage(update.ChatId, update.MessageId), response =>
                        {
                            if (response is Message message)
                            {
                                Handle(new UpdateNewMessage(message));
                            }
                        });
                    }
                    else
                    {
                        Handle(new UpdateDeleteMessages(update.ChatId, new[] { update.MessageId }, true, false));
                    }
                }
                else
                {
                    _hasLoadedLastPinnedMessage = false;
                    BeginOnUIThread(() => LoadPinnedMessagesSliceAsync(update.MessageId));

                    Handle(update.MessageId, message =>
                    {
                        message.IsPinned = update.IsPinned;
                        return true;
                    },
                    (bubble, message) => bubble.UpdateMessageIsPinned(message));
                }
            }
        }

        public void Handle(UpdateMessageSendFailed update)
        {
            if (update.Message.ChatId == _chat?.Id)
            {
                Handle(update.OldMessageId, message =>
                {
                    message.Replace(update.Message);
                    return true;
                },
                (bubble, message) => bubble.UpdateMessage(message));
            }
        }

        public void Handle(UpdateMessageSendSucceeded update)
        {
            if (update.Message.ChatId == _chat?.Id && CheckSchedulingState(update.Message))
            {
                Handle(update.OldMessageId, message =>
                {
                    message.Replace(update.Message);
                    message.GeneratedContentUnread = true;

                    if (message.Content is MessagePaidMedia paidMedia)
                    {
                        message.Content = new MessagePaidAlbum(paidMedia);
                    }

                    return true; //MoveMessageInOrder(Items, message);
                },
                (bubble, message) =>
                {
                    bubble.UpdateMessage(message);
                    Delegate?.ViewVisibleMessages();
                }, update.Message.Id);

                if (Settings.Notifications.InAppSounds)
                {
                    _notificationsService.PlaySound();
                }
            }
        }

        public void Handle(UpdateMessageTranslatedText update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    message.TranslatedText = update.TranslatedText;
                    return true;
                },
                (bubble, message) => bubble.UpdateMessageText(message));
            }
        }

        public void Handle(UpdateMessageEffect update)
        {
            if (_messageEffects.TryGetValue(update.Effect.Id, out var hashSet))
            {
                foreach (var messageId in hashSet)
                {
                    Handle(messageId, message =>
                    {
                        message.Effect = update.Effect;
                        return true;
                    }, (bubble, message) =>
                    {
                        bubble.UpdateMessageEffect(message);
                    });
                }

                hashSet.Clear();
            }
        }

        public void Handle(UpdateMessageFactCheck update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    message.FactCheck = update.FactCheck;
                    return true;
                }, (bubble, message) =>
                {
                    bubble.UpdateMessageFactCheck(message);
                });
            }
        }

        public void Handle(UpdateAnimatedEmojiMessageClicked update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, null, (bubble, message) =>
                {
                    if (bubble.MediaTemplateRoot is StickerContent content && message.Content is MessageText text)
                    {
                        ChatActionManager.SetTyping(new ChatActionWatchingAnimations(text.Text.Text));
                        content.PlayInteraction(message, update.Sticker);
                    }
                });
            }
        }

        private void Handle(long messageId, Func<MessageViewModel, bool> update, Action<MessageBubble, MessageViewModel> action = null, long? newMessageId = null)
        {
            BeginOnUIThread(() =>
            {
                if (Items.TryGetValue(messageId, out var message))
                {
                    if (_groupedMessages.TryGetValue(message.MediaAlbumId, out MessageViewModel albumMessage))
                    {
                        if (albumMessage.Content is MessageAlbum album && album.Messages.TryGetValue(messageId, out MessageViewModel child))
                        {
                            update?.Invoke(child);

                            // UpdateMessageSendSucceeded changes the message id
                            if (messageId != child.Id)
                            {
                                album.Messages.Remove(messageId);
                                album.Messages.Add(child);
                            }

                            message.UpdateWith(album.Messages[0]);
                            album.Invalidate();

                            if (action != null)
                            {
                                Delegate?.UpdateBubbleWithMediaAlbumId(message.MediaAlbumId, bubble => action(bubble, albumMessage));
                            }
                        }
                    }
                    else
                    {
                        // if this is coming from UpdateMessageSendSucceded,
                        // but we already have a message with the new ID there was a race condition:
                        // in this case we just delete the temporary message and that's it.
                        if (newMessageId.HasValue && newMessageId != messageId && Items.TryGetValue(newMessageId.Value, out MessageViewModel duplicate))
                        {
                            Items.Remove(duplicate);
                        }

                        if (update == null || update(message))
                        {
                            // UpdateMessageSendSucceeded changes the message id
                            if (action != null)
                            {
                                Delegate?.UpdateBubbleWithMessageId(messageId, bubble => action(bubble, message));
                            }
                        }
                    }

                    if (messageId != message.Id)
                    {
                        Items.UpdateMessageSendSucceeded(messageId, message);
                        Delegate?.UpdateMessageSendSucceeded(messageId, message);
                    }
                }
            });
        }

        private void Handle(long messageId, Action<MessageViewModel> update, Action<MessageBubble, MessageViewModel, bool> action)
        {
            BeginOnUIThread(() =>
            {
                if (Items.TryGetValue(messageId, out var message))
                {
                    if (_groupedMessages.TryGetValue(message.MediaAlbumId, out MessageViewModel albumMessage))
                    {
                        if (albumMessage.Content is MessageAlbum album && album.Messages.TryGetValue(messageId, out MessageViewModel child))
                        {
                            update(child);

                            message.UpdateWith(album.Messages[0]);
                            album.Invalidate();

                            Delegate?.UpdateBubbleWithMediaAlbumId(message.MediaAlbumId, bubble => action(bubble, albumMessage, false));
                        }
                    }
                    else
                    {
                        update(message);
                        Delegate?.UpdateBubbleWithMessageId(messageId, bubble => action(bubble, message, false));
                    }
                }

                Delegate?.UpdateBubbleWithReplyToMessageId(messageId, (bubble, reply) =>
                {
                    update(reply.ReplyToItem as MessageViewModel);
                    action(bubble, reply, true);
                });
            });
        }

        private void Handle(MessageViewModel message, Action<MessageBubble> action1, Action<MessageService> action2)
        {
            Delegate?.UpdateContainerWithMessageId(message.Id, container =>
            {
                if (container.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                {
                    action1(bubble);
                }
                else if (container.ContentTemplateRoot is MessageService service)
                {
                    action2(service);
                }
            });
        }

        private async void InsertMessage(MessageViewModel message)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                if (IsFirstSliceLoaded == true || Type == DialogType.ScheduledMessages)
                {
                    if (IsTranslating)
                    {
                        _translateService.Translate(message, Settings.Translate.To);
                    }

                    var result = new List<MessageViewModel> { message };
                    ProcessMessages(_chat, result);

                    if (result.Count > 0)
                    {
                        InsertMessageInOrder(Items, result[0]);
                    }
                }
                else if (message.IsOutgoing && message.SendingState is MessageSendingStatePending)
                {
                    if (_composerHeader == null)
                    {
                        ComposerHeader = null;
                    }

                    goto LoadMessage;
                }

                return;
            }

        LoadMessage:
            await LoadMessageSliceAsync(null, message.Id, VerticalAlignment.Top);
        }

        public static int InsertMessageInOrder(IList<MessageViewModel> messages, MessageViewModel message)
        {
            var oldIndex = -1;

            if (messages.Count == 0)
            {
                oldIndex = 0;
            }

            for (var i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].Id == 0)
                {
                    if (messages[i].Date < message.Date)
                    {
                        oldIndex = i + 1;
                        break;
                    }

                    continue;
                }

                if (messages[i].Id == message.Id)
                {
                    oldIndex = -1;
                    break;
                }
                if (messages[i].Id < message.Id)
                {
                    oldIndex = i + 1;
                    break;
                }
            }

            if (oldIndex != -1)
            {
                messages.Insert(oldIndex, message);
            }

            return oldIndex;
        }

        public static bool MoveMessageInOrder(MvxObservableCollection<MessageViewModel> messages, MessageViewModel message)
        {
            var newIndex = -1;
            var oldIndex = -1;

            for (var i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].Id == 0)
                {
                    if (messages[i].Date < message.Date && newIndex == -1)
                    {
                        newIndex = i;
                        break;
                    }

                    continue;
                }

                if (messages[i].Id == message.Id)
                {
                    oldIndex = i;
                    break;
                }
                if (messages[i].Id < message.Id && newIndex == -1)
                {
                    newIndex = i;
                }
            }

            if (newIndex > oldIndex)
            {
                newIndex = Math.Min(newIndex, messages.Count - 1);
                messages.RemoveAt(oldIndex);
                messages.Insert(newIndex, message);
                return false;
            }

            return true;
        }

        public void UpdateQuery(string query)
        {
            Delegate?.ForEach(bubble => bubble.UpdateQuery(query));
        }
    }
}
