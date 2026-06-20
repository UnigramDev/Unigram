//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Messages;
using Telegram.Controls.Messages.Content;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;

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
                .Subscribe<UpdateChatUnreadPollVoteCount>(Handle)
                .Subscribe<UpdateChatReadOutbox>(Handle)
                .Subscribe<UpdateForumTopicReadOutbox>(Handle)
                .Subscribe<UpdateChatReadInbox>(Handle)
                .Subscribe<UpdateChatDraftMessage>(Handle)
                .Subscribe<UpdateForumTopicDraftMessage>(Handle)
                .Subscribe<UpdateDirectMessagesChatDraftMessage>(Handle)
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
                .Subscribe<UpdatePendingMessage>(Handle)
                .Subscribe<UpdateDeleteMessages>(Handle)
                .Subscribe<UpdateMessageContent>(Handle)
                .Subscribe<UpdateMessageContentOpened>(Handle)
                .Subscribe<UpdateMessageMentionRead>(Handle)
                .Subscribe<UpdateMessageUnreadReactions>(Handle)
                .Subscribe<UpdateMessageContainsUnreadPollVotes>(Handle)
                .Subscribe<UpdateMessageEdited>(Handle)
                .Subscribe<UpdateMessageInteractionInfo>(Handle)
                .Subscribe<UpdateMessageIsPinned>(Handle)
                .Subscribe<UpdateMessageSendFailed>(Handle)
                .Subscribe<UpdateMessageSendSucceeded>(Handle)
                .Subscribe<UpdateMessageTranslatedText>(Handle)
                .Subscribe<UpdateMessageSummarizedText>(Handle)
                .Subscribe<UpdateMessageFactCheck>(Handle)
                .Subscribe<UpdateMessageEffect>(Handle)
                .Subscribe<UpdateMessageSuggestedPostInfo>(Handle)
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
                .Subscribe<UpdateQuickReplyShortcut>(Handle)
                .Subscribe<UpdateForumTopicInfo>(Handle);
        }

        public void Handle(UpdateForumTopicInfo update)
        {
            if (_chat?.Id == update.Info.ChatId)
            {
                BeginOnUIThread(() => Delegate?.UpdateServiceWithForumTopic(update.Info.ForumTopicId, service => service.UpdateMessageTopic()));
            }
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
            if (update.ChatId == _chat?.Id && update.TopicId.AreTheSame(TopicId) && Type is DialogType.History or DialogType.Thread)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatActions(_chat, ClientService.GetChatActions(update.ChatId)));
            }
        }

        public void Handle(UpdateChatBusinessBotManageBar update)
        {
            if (update.ChatId == _chat?.Id && Type == DialogType.History)
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
                ClientService.TryGetUserFull(update.User.Id, out UserFullInfo fullInfo);
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, update.User, fullInfo, false, _accessToken != null));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.User.Id)
            {
                ClientService.TryGetUserFull(update.User.Id, out UserFullInfo fullInfo);
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, update.User, fullInfo, true, false));
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
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, user, update.UserFullInfo, false, _accessToken != null));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, ClientService.GetUser(update.UserId), update.UserFullInfo, true, false));
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
                ClientService.TryGetBasicGroupFull(update.BasicGroup.Id, out BasicGroupFullInfo fullInfo);
                BeginOnUIThread(() => Delegate?.UpdateBasicGroup(chat, update.BasicGroup, fullInfo));
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
                BeginOnUIThread(() => Delegate?.UpdateBasicGroup(chat, basicGroup, update.BasicGroupFullInfo));
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
                if (IsDirectMessagesGroup)
                {
                    UpdateEmptyState(update.Supergroup);
                }

                ClientService.TryGetSupergroupFull(update.Supergroup.Id, out SupergroupFullInfo fullInfo);
                BeginOnUIThread(() =>
                {
                    if (_hasAutomaticTranslation != update.Supergroup.HasAutomaticTranslation)
                    {
                        UpdateChatIsTranslatable();
                    }

                    Delegate?.UpdateSupergroup(chat, update.Supergroup, fullInfo);
                });
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
                    Delegate?.UpdateSupergroup(chat, supergroup, update.SupergroupFullInfo);
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

                BeginOnUIThread(() =>
                {
                    SetText(string.Format("@{0} {1}", username, update.Query), focus: true);
                    ResolveInlineBot(username, update.Query);
                });
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
            if (update.ChatId == _chat?.Id && Type == DialogType.History)
            {
                BeginOnUIThread(() => UpdateChatActionBar(_chat));
            }
        }

        private void UpdateChatActionBar(Chat chat)
        {
            if (ClientService.TryGetUser(chat, out User user) && chat.ActionBar is ChatActionBarReportAddBlock { AccountInfo: not null })
            {
                ClientService.Send(new GetGroupsInCommon(user.Id, 0, 3), result =>
                {
                    BeginOnUIThread(() => GroupsInCommon = result as Td.Api.Chats);
                });
            }

            Delegate?.UpdateChatActionBar(chat);
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
                BeginOnUIThread(() => Delegate?.UpdateChatReplyMarkup(_chat, CreateMessage(update.ReplyMarkupMessage)));
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

        public void Handle(UpdateChatUnreadPollVoteCount update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatUnreadPollVoteCount(_chat, update.UnreadPollVoteCount));
            }
        }

        public void Handle(UpdateChatReadOutbox update)
        {
            if (update.ChatId == _chat?.Id && _forumTopic == null)
            {
                BeginOnUIThread(() =>
                {
                    Delegate?.ForEach((bubble, message) => bubble.UpdateMessageState(message));
                });
            }
        }
        public void Handle(UpdateForumTopicReadOutbox update)
        {
            if (update.ChatId == _chat?.Id && update.ForumTopicId == _forumTopic?.Info.ForumTopicId)
            {
                BeginOnUIThread(() =>
                {
                    Delegate?.ForEach((bubble, message) => bubble.UpdateMessageState(message));
                });
            }
        }

        public void Handle(UpdateChatReadInbox update)
        {
            if (update.ChatId == _chat?.Id && Type == DialogType.History)
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
                if (header?.Editing != null || header?.SuggestedPostInfo != null)
                {
                    return;
                }

                BeginOnUIThread(() => ShowDraftMessage(_chat, false));
            }
        }

        public void Handle(UpdateForumTopicDraftMessage update)
        {
            if (update.ChatId == _chat?.Id && TopicId.IsForum(update.ForumTopicId))
            {
                var header = _composerHeader;
                if (header?.Editing != null || header?.SuggestedPostInfo != null)
                {
                    return;
                }

                BeginOnUIThread(() => ShowDraftMessage(_chat, false));
            }
        }

        public void Handle(UpdateDirectMessagesChatDraftMessage update)
        {
            if (update.ChatId == _chat?.Id && TopicId.IsDirectMessagesChat(update.TopicId))
            {
                var header = _composerHeader;
                if (header?.Editing != null || header?.SuggestedPostInfo != null)
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
                this.BeginOnUIThread(() => IsNewestSliceLoaded = null);
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
                    BeginOnUIThread(() => Delegate?.UpdateUser(_chat, user, fullInfo, false, _accessToken != null));
                }
                else
                {
                    UpdateEmptyState(user, null, true);
                }
            }
            else if (update.ChatId == _chat?.Id && _chat.Type is ChatTypeSupergroup)
            {
                if (IsForum)
                {
                    BeginOnUIThread(() => Delegate?.UpdateChatLastMessage(_chat));
                }
                else if (IsDirectMessagesGroup && ClientService.TryGetSupergroup(_chat, out Supergroup supergroup))
                {
                    UpdateEmptyState(supergroup);
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
                        Delegate?.UpdateUserEmptyState(_chat, user, fullInfo, result as CanSendMessageToUserResult);
                    });
                });
            }
            else
            {
                BeginOnUIThread(() =>
                {
                    RestrictsNewChats = false;

                    GreetingSticker ??= ClientService.NextGreetingSticker();
                    Delegate?.UpdateUserEmptyState(_chat, user, fullInfo, null);
                });
            }
        }

        private void UpdateEmptyState(Supergroup supergroup)
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
            else if (empty == _isChatEmpty && supergroup == null)
            {
                return;
            }

            _isChatEmpty = empty;
            BeginOnUIThread(() => Delegate?.UpdateSupergroupEmptyState(chat, supergroup));
        }

        private bool CheckSchedulingState(Message message)
        {
            if (Type == DialogType.ScheduledMessages)
            {
                return message.SchedulingState != null;
            }
            else if (Type == DialogType.Thread)
            {
                return message.SchedulingState == null && message.TopicId.AreTheSame(TopicId);
            }
            else if (Type == DialogType.Pinned)
            {
                return message.SchedulingState == null && message.IsPinned;
            }

            return message.SchedulingState == null && Type == DialogType.History;
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
                    DialogPendingMessage pending = null;

                    if (_chat.Type is ChatTypePrivate privata && update.Message.SenderId.IsUser(privata.UserId))
                    {
                        ulong lastUpdate = 0;

                        foreach (var item in _pendingMessages.Values)
                        {
                            if (item.LastUpdate > lastUpdate)
                            {
                                lastUpdate = item.LastUpdate;
                                pending = item;
                            }
                        }

                        foreach (var item in _pendingMessages.Values)
                        {
                            if (item.DraftId != pending?.DraftId)
                            {
                                item.Stop();

                                item.Updated -= PendingMessage_Updated;
                                item.Completed -= PendingMessage_Completed;

                                if (Items.TryGetValue(item.DraftId, out MessageViewModel old))
                                {
                                    Items.Remove(old);
                                }
                            }
                        }

                        _pendingMessages.Clear();
                    }

                    if (pending != null && Items.ContainsKey(long.MaxValue))
                    {
                        pending.Update(update.Message);
                    }
                    else
                    {
                        InsertMessage(message);
                    }

                    if (!update.Message.IsOutgoing)
                    {
                        PlaySound(false);
                    }
                });
            }
        }

        public void Handle(UpdatePendingMessage update)
        {
            if (_chat?.Id == update.ChatId && (TopicId == null || TopicId.IsForum(update.ForumTopicId)) && ClientService.TryGetUser(Chat, out User user))
            {
                var topicId = new MessageTopicForum(update.ForumTopicId);
                var content = update.Content;
                var message = CreateMessage(new Message(long.MaxValue, new MessageSenderUser(user.Id), update.ChatId, null, null, false, false, false, false, false, false, false, false, false, false, DateTime.Now.ToTimestamp(), 0, null, null, null, null, null, null, null, topicId, null, 0, 0, 0, null, 0, 0, string.Empty, 0, string.Empty, 0, 0, null, string.Empty, content, null));
                message.GeneratedContentUnread = true;
                message.IsInitial = false;

                BeginOnUIThread(() =>
                {
                    if (_pendingMessages.TryGetValue(update.DraftId, out DialogPendingMessage pending))
                    {
                        pending.Update(update);
                    }
                    else
                    {
                        pending = message.Content is MessageText ? new DialogPendingTextMessage2(update, message) : new DialogPendingRichMessage(update, message);
                        pending.Updated += PendingMessage_Updated;
                        pending.Completed += PendingMessage_Completed;

                        _pendingMessages[update.DraftId] = pending;
                    }

                    if (Items.TryGetValue(long.MaxValue, out MessageViewModel already))
                    {
                        return;
                    }

                    InsertMessage(message);
                });
            }
        }

        private void PendingMessage_Updated(DialogPendingMessage sender, MessageViewModel message)
        {
            if (Items.TryGetValue(long.MaxValue, out MessageViewModel already))
            {
                already.Replace(message);
                Delegate?.UpdateBubbleWithMessageId(long.MaxValue, bubble => bubble.UpdateMessageContent(already));
            }
        }

        private void PendingMessage_Completed(DialogPendingMessage sender, Message completed)
        {
            _pendingMessages.Remove(sender.DraftId);

            sender.Updated -= PendingMessage_Updated;
            sender.Completed -= PendingMessage_Completed;

            if (completed != null)
            {
                Handle(long.MaxValue, message =>
                {
                    message.Replace(completed);
                    message.IsInitial = true;
                    message.GeneratedContentUnread = true;

                    if (message.Content is MessagePaidMedia paidMedia)
                    {
                        message.Content = new MessagePaidAlbum(paidMedia);
                    }

                    InsertMessage(message, long.MaxValue);

                    return true;
                },
                (bubble, message) =>
                {
                    if (bubble.Parent is MessageSelector selector)
                    {
                        selector.PrepareForItemOverride(message, true);
                    }

                    bubble.UpdateMessage(message);
                    Delegate?.ViewVisibleMessages();
                }, newMessageId: completed.Id);
            }
            else
            {
                if (Items.TryGetValue(sender.DraftId, out MessageViewModel already))
                {
                    Items.Remove(already);
                }
            }
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.ChatId == _chat?.Id && !update.FromCache)
            {
                var table = update.MessageIds.ToHashSet();

                BeginOnUIThread(() =>
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
                                        message.UpdateAlbum(album.Messages[0]);
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
                        foreach (var item in toBeDeleted)
                        {
                            Items.Remove(item);
                        }
                    }

                    if (Items.Count > 0 && Items[^1].Content is MessageHeaderUnread)
                    {
                        Items.RemoveAt(Items.Count - 1);
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
                            // Probably not the best way but replacing content template is not supported
                            InsertMessageInOrder(message, 0, true);
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
                }, (service, message) =>
                {
                    service.UpdateMessage(message);
                });

                PinnedMessages.UpdateMessageContent(update.MessageId, update.NewContent);

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
                Mentions.RemoveMessage(update.MessageId);

                Handle(update.MessageId, message =>
                {
                    message.ContainsUnreadMention = false;
                    return false;
                });

                BeginOnUIThread(() => Delegate?.UpdateChatUnreadMentionCount(_chat, update.UnreadMentionCount));
            }
        }

        public void Handle(UpdateMessageContainsUnreadPollVotes update)
        {
            if (update.ChatId == _chat?.Id)
            {
                if (update.ContainsUnreadPollVotes)
                {
                    PollVotes.AddMessage(update.MessageId);
                }
                else
                {
                    PollVotes.RemoveMessage(update.MessageId);
                }

                Handle(update.MessageId, message =>
                {
                    message.ContainsUnreadPollVotes = update.ContainsUnreadPollVotes;
                    return false;
                });

                BeginOnUIThread(() => Delegate?.UpdateChatUnreadPollVoteCount(_chat, update.UnreadPollVoteCount));
            }
        }

        public void Handle(UpdateMessageUnreadReactions update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Reactions.RemoveMessage(update.MessageId);

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
                (bubble, message) => bubble.UpdateMessageInteractionInfo(message),
                (service, message) => service.UpdateMessageInteractionInfo(message));
            }
        }

        public void Handle(UpdateMessageIsPinned update)
        {
            if (update.ChatId == _chat?.Id)
            {
                if (Type == DialogType.Pinned)
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
                    BeginOnUIThread(() =>
                    {
                        if (TryGetFirstVisibleMessageId(out long firstVisibleId))
                        {
                            PinnedMessages.LoadSlice(firstVisibleId);
                        }
                    });

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
                    message.IsInitial = true;
                    message.GeneratedContentUnread = true;

                    if (message.Content is MessagePaidMedia paidMedia)
                    {
                        message.Content = new MessagePaidAlbum(paidMedia);
                    }

                    InsertMessage(message, update.OldMessageId);

                    return true;
                },
                (bubble, message) =>
                {
                    bubble.UpdateMessage(message);
                    Delegate?.ViewVisibleMessages();
                }, newMessageId: update.Message.Id);
            }
        }

        public void Handle(UpdateMessageSendSucceeded update)
        {
            if (update.Message.ChatId == _chat?.Id && Type == DialogType.History && update.Message.SchedulingState is MessageSchedulingStateSendWhenVideoProcessed)
            {
                Handle(new UpdateDeleteMessages(update.Message.ChatId, new[] { update.OldMessageId }, true, false));
                BeginOnUIThread(() =>
                {
                    NavigationService.NavigateToChat(_chat, update.Message.Id, scheduled: true);
                    ShowToast(string.Format("**{0}**\n{1}", Strings.VideoConversionTitle, Strings.VideoConversionText), ToastPopupIcon.VideoConversion);
                });
            }
            else if (update.Message.ChatId == _chat?.Id && CheckSchedulingState(update.Message))
            {
                Handle(update.OldMessageId, message =>
                {
                    message.Replace(update.Message);
                    message.IsInitial = true;
                    message.GeneratedContentUnread = true;

                    if (message.Content is MessagePaidMedia paidMedia)
                    {
                        message.Content = new MessagePaidAlbum(paidMedia);
                    }

                    InsertMessage(message, update.OldMessageId);

                    return true;
                },
                (bubble, message) =>
                {
                    if (bubble.Parent is MessageSelector selector)
                    {
                        selector.PrepareForItemOverride(message, true);
                    }

                    bubble.UpdateMessage(message);
                    Delegate?.ViewVisibleMessages();
                }, newMessageId: update.Message.Id);
            }
        }

        private void PlaySound(bool sent)
        {
            if (Settings.Notifications.InAppSounds)
            {
                var muted = ClientService.Notifications.IsMuted(Chat);
                var listeners = AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged);

                if (NavigationService.Window.ActivationMode != CoreWindowActivationMode.Deactivated && (listeners || !muted))
                {
                    _notificationsService.PlaySound(sent || !listeners);
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
                },
                (bubble, message, reply) =>
                {
                    if (reply)
                    {
                        bubble.UpdateMessageReply(message);
                    }
                    else
                    {
                        bubble.UpdateMessageTextLayout(message);
                    }
                }, null);
            }
        }

        public void Handle(UpdateMessageSummarizedText update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    message.SummarizedText = update.SummarizedText;
                },
                (bubble, message, reply) =>
                {
                    if (reply)
                    {
                        return;
                    }

                    bubble.UpdateMessageTextLayout(message);
                    Delegate?.UpdateMessageSummary(message);
                }, null);
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

        public void Handle(UpdateMessageSuggestedPostInfo update)
        {
            if (update.ChatId == _chat?.Id)
            {
                Handle(update.MessageId, message =>
                {
                    message.SuggestedPostInfo = update.SuggestedPostInfo;
                    message.ReplyMarkup = update.SuggestedPostInfo.ToReplyMarkup(message.IsOutgoing);

                    return true;
                },
                (bubble, message) => bubble.UpdateMessageSuggestedPostInfo(message));
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

        private void Handle(long messageId, Func<MessageViewModel, bool> update, Action<MessageBubble, MessageViewModel> action1 = null, Action<MessageService, MessageViewModel> action2 = null, long? newMessageId = null)
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
                            if (messageId != child.Id && newMessageId.HasValue)
                            {
                                album.Messages.Remove(messageId);
                                album.Messages.Add(child);

                                Items.UpdateMessageSendSucceeded(messageId, child.Id, message);
                            }

                            message.UpdateAlbum(album.Messages[0]);
                            album.Invalidate();

                            if (action1 != null)
                            {
                                Delegate?.UpdateBubbleWithMediaAlbumId(message.MediaAlbumId, bubble => action1(bubble, albumMessage));
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
                            if (action1 != null)
                            {
                                Delegate?.UpdateContainerWithMessageId(messageId, container =>
                                {
                                    if (action1 != null && container.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                                    {
                                        action1(bubble, message);
                                    }
                                    else if (action2 != null && container.ContentTemplateRoot is MessageService service)
                                    {
                                        action2(service, message);
                                    }
                                });
                            }
                        }
                    }

                    if (messageId != message.Id && newMessageId.HasValue)
                    {
                        Items.UpdateMessageSendSucceeded(messageId, message);
                        Delegate?.UpdateMessageSendSucceeded(messageId, message);
                    }

                    if (newMessageId.HasValue)
                    {
                        PlaySound(true);
                    }
                }
            });
        }

        private void Handle(long messageId, Action<MessageViewModel> update, Action<MessageBubble, MessageViewModel, bool> action1, Action<MessageService, MessageViewModel> action2)
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

                            message.UpdateAlbum(album.Messages[0]);
                            album.Invalidate();

                            Delegate?.UpdateBubbleWithMediaAlbumId(message.MediaAlbumId, bubble => action1(bubble, albumMessage, false));
                        }
                    }
                    else
                    {
                        update(message);
                        Delegate?.UpdateContainerWithMessageId(message.Id, container =>
                        {
                            if (container.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                            {
                                action1(bubble, message, false);
                            }
                            else if (action2 != null && container.ContentTemplateRoot is MessageService service)
                            {
                                action2.Invoke(service, message);
                            }
                        });
                    }
                }

                Delegate?.UpdateBubbleWithReplyToMessageId(messageId, (bubble, reply) =>
                {
                    update(reply.ReplyToItem as MessageViewModel);
                    action1(bubble, reply, true);
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

        private void InsertMessage(MessageViewModel message, long oldMessageId = 0)
        {
            if (IsNewestSliceLoaded == true || Type == DialogType.ScheduledMessages)
            {
                if (IsTranslating)
                {
                    _translateService.Translate(message, Settings.Translate.To);
                }

                var result = new List<MessageViewModel> { message };
                ProcessMessages(_chat, result, true);

                if (result.Count > 0)
                {
                    InsertMessageInOrder(result[0], oldMessageId);
                }
            }
            else if (message.IsOutgoing && message.SendingState is MessageSendingStatePending)
            {
                if (_composerHeader == null)
                {
                    ComposerHeader = null;
                }

                _ = LoadMessageSliceAsync(null, message.Id, VerticalAlignment.Top);
            }
        }

        public void InsertMessageInOrder(MessageViewModel message, long oldMessageId = 0, bool force = false)
        {
            var newIndex = NextIndexOf(message, oldMessageId, out int oldIndex);
            if (newIndex != -1)
            {
                if (oldIndex != -1)
                {
                    // We can't use Move because ListView seems to mess up a lot with this operationg
                    Items.RemoveAt(oldIndex);
                    Items.Insert(newIndex, message);
                }
                else
                {
                    Items.Insert(newIndex, message);
                }
            }
            else if (force && oldIndex != -1)
            {
                Items.RemoveAt(oldIndex);
                Items.Insert(oldIndex, message);
            }
        }

        private int NextIndexOf(MessageViewModel message, long oldMessageId, out int oldIndex)
        {
            oldIndex = -1;
            var newIndex = Items.Count;

            var oldIndexNeeded = Items.ContainsKey(oldMessageId != 0 ? oldMessageId : message.Id);
            var newIndexNeeded = true;

            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (item.Id == 0)
                {
                    if (item.Date <= message.Date)
                    {
                        newIndex = i + 1;
                        newIndexNeeded = false;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (item.Id < message.Id && newIndexNeeded)
                {
                    newIndex = i + 1;
                    newIndexNeeded = false;
                }

                if (item.Id == message.Id && oldIndexNeeded)
                {
                    oldIndex = i;
                    oldIndexNeeded = false;
                }

                if (!newIndexNeeded && !oldIndexNeeded)
                {
                    break;
                }
            }

            if (oldIndex != -1 && oldIndex < newIndex)
            {
                newIndex--;
            }

            if (newIndex == oldIndex)
            {
                return -1;
            }

            return newIndex;
        }

        public void UpdateQuery(string query)
        {
            Delegate?.ForEach(bubble => bubble.UpdateQuery(query));
        }
    }
}
