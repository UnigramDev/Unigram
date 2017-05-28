using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel :
        IHandle<TLMessageCommonBase>,
        IHandle<TLUpdateChannelPinnedMessage>,
        IHandle<TLUpdateEditChannelMessage>,
        IHandle<TLUpdateEditMessage>,
        IHandle<MessagesRemovedEventArgs>,
        IHandle<TLUpdateUserStatus>,
        IHandle<string>
    {
        public async void Handle(string message)
        {
            if (message.Equals("Window_Activated"))
            {
                var participant = _with;
                var dialog = _currentDialog;
                if (dialog != null && Messages.Count > 0)
                {
                    var unread = dialog.UnreadCount;
                    if (Peer is TLInputPeerChannel && participant is TLChannel channel)
                    {
                        await ProtoService.ReadHistoryAsync(channel, dialog.TopMessage);
                    }
                    else
                    {
                        await ProtoService.ReadHistoryAsync(Peer, dialog.TopMessage, 0);
                    }

                    var readPeer = With as ITLReadMaxId;
                    readPeer.ReadInboxMaxId = dialog.TopMessage;
                    dialog.ReadInboxMaxId = dialog.TopMessage;
                    dialog.UnreadCount = dialog.UnreadCount - unread;
                    dialog.RaisePropertyChanged(() => dialog.UnreadCount);
                }
            }
            else if (message.Equals("Window_Deactivated"))
            {
                SaveDraft();
            }
        }

        public void Handle(MessagesRemovedEventArgs args)
        {
            if (With == args.Dialog.With && args.Messages != null)
            {
                Execute.BeginOnUIThread(() =>
                {
                    foreach (var message in args.Messages)
                    {
                        var removed = Messages.Remove(message);
                        if (removed == false)
                        {
                            // Check if this is really needed

                            var already = Messages.FirstOrDefault(x => x.Id == message.Id);
                            if (already != null)
                            {
                                Messages.Remove(already);
                            }
                        }
                    }
                });
            }
        }

        public void Handle(TLUpdateUserStatus statusUpdate)
        {
            Execute.BeginOnUIThread(() =>
            {
                if (With is TLUser user)
                {
                    LastSeen = LastSeenConverter.GetLabel(user, true);
                }
                else
                {
                    //if (online > -1)
                    //{
                    //    if (statusUpdate.Status.GetType() == typeof(TLUserStatusOnline)) online++;
                    //    else online--;
                    //    LastSeen = participantCount + " members" + ((online > 0) ? (", " + online + " online") : "");
                    //}
                }
            });
        }

        private async Task<string> GetSubtitle()
        {
            if (With is TLUser user)
            {
                return LastSeenConverter.GetLabel(user, true);
            }
            else if (With is TLChannel channel && channel.HasAccessHash && channel.AccessHash.HasValue)
            {
                var full = Full as TLChannelFull;
                if (full == null)
                {
                    full = CacheService.GetFullChat(channel.Id) as TLChannelFull;
                }

                if (full == null)
                {
                    var response = await ProtoService.GetFullChannelAsync(new TLInputChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash.Value });
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChannelFull;
                    }
                }

                if (full == null)
                {
                    return string.Empty;
                }

                if (channel.IsBroadcast && full.HasParticipantsCount)
                {
                    return string.Format("{0} members", full.ParticipantsCount ?? 0);
                }
                else if (full.HasParticipantsCount)
                {
                    var config = CacheService.GetConfig();
                    if (config != null && full.ParticipantsCount <= config.ChatSizeMax)
                    {
                        var participants = await ProtoService.GetParticipantsAsync(new TLInputChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash.Value }, null, 0, config.ChatSizeMax);
                        if (participants.IsSucceeded)
                        {
                            var count = 0;
                            foreach (var item in participants.Result.Users.OfType<TLUser>())
                            {
                                if (item.HasStatus && item.Status is TLUserStatusOnline)
                                {
                                    count++;
                                }
                            }

                            if (count > 1)
                            {
                                return string.Format("{0} members, {1} online", full.ParticipantsCount ?? 0, count);
                            }
                        }
                    }

                    return string.Format("{0} members", full.ParticipantsCount ?? 0);
                }
            }
            else if (With is TLChat chat)
            {
                var full = Full as TLChatFull;
                if (full == null)
                {
                    full = CacheService.GetFullChat(chat.Id) as TLChatFull;
                }

                if (full == null)
                {
                    var response = await ProtoService.GetFullChatAsync(chat.Id);
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChatFull;
                    }
                }

                if (full == null)
                {
                    return string.Empty;
                }

                var participants = full.Participants as TLChatParticipants;
                if (participants != null)
                {
                    var count = 0;
                    foreach (var item in participants.Participants)
                    {
                        if (item.User != null && item.User.HasStatus && item.User.Status is TLUserStatusOnline)
                        {
                            count++;
                        }
                    }

                    if (count > 1)
                    {
                        return string.Format("{0} members, {1} online", participants.Participants.Count, count);
                    }

                    return string.Format("{0} members", participants.Participants.Count);
                }
            }

            return string.Empty;
        }

        public void Handle(TLUpdateEditChannelMessage update)
        {
            var channel = With as TLChannel;
            if (channel == null)
            {
                return;
            }

            var message = update.Message as TLMessage;
            if (message == null || !(message.ToId is TLPeerChannel))
            {
                return;
            }

            if (channel.Id == message.ToId.Id)
            {
                Execute.BeginOnUIThread(() =>
                {
                    var already = Messages.FirstOrDefault(x => x.Id == update.Message.Id) as TLMessage;
                    if (already == null)
                    {
                        return;
                    }

                    //if (already != message)
                    {
                        already.Edit(message);
                    }

                    message = already;

                    message.RaisePropertyChanged(() => message.HasEditDate);
                    message.RaisePropertyChanged(() => message.Message);
                    message.RaisePropertyChanged(() => message.Media);
                    message.RaisePropertyChanged(() => message.ReplyMarkup);
                    message.RaisePropertyChanged(() => message.Self);
                });
            }
        }

        public void Handle(TLUpdateEditMessage update)
        {
            var message = update.Message as TLMessage;
            if (message == null)
            {
                return;
            }

            var flag = false;

            var userBase = With as TLUserBase;
            var chatBase = With as TLChatBase;
            if (userBase != null && message.ToId is TLPeerUser && !message.IsOut && userBase.Id == message.FromId.Value)
            {
                flag = true;
            }
            else if (userBase != null && message.ToId is TLPeerUser && message.IsOut && userBase.Id == message.ToId.Id)
            {
                flag = true;
            }
            else if (chatBase != null && message.ToId is TLPeerChat && chatBase.Id == message.ToId.Id)
            {
                flag = true;
            }

            if (flag)
            {
                Execute.BeginOnUIThread(() =>
                {
                    var already = Messages.FirstOrDefault(x => x.Id == update.Message.Id) as TLMessage;
                    if (already == null)
                    {
                        return;
                    }

                    //if (already != message)
                    {
                        already.Edit(message);
                    }

                    message = already;

                    message.RaisePropertyChanged(() => message.HasEditDate);
                    message.RaisePropertyChanged(() => message.Message);
                    message.RaisePropertyChanged(() => message.Media);
                    message.RaisePropertyChanged(() => message.ReplyMarkup);
                    message.RaisePropertyChanged(() => message.Self);
                });
            }
        }

        public void Handle(TLUpdateChannelPinnedMessage update)
        {
            var channel = With as TLChannel;
            if (channel != null && channel.Id == update.ChannelId)
            {
                ShowPinnedMessage(channel);
            }
        }

        public void Handle(TLMessageCommonBase messageCommon)
        {
            if (messageCommon == null) return;

            //if (!this._isFirstSliceLoaded)
            //{
            //    Execute.ShowDebugMessage("DialogDetailsViewModel.Handle(TLMessageCommon) _isFirstSliceLoaded=false");
            //    return;
            //}

            if (messageCommon is TLMessage message)
            {
                if (message.IsOut && !message.HasFwdFrom && message.Media is TLMessageMediaDocument documentMedia)
                {
                    if (message.IsGif(true))
                    {
                        _stickersService.AddRecentGif(documentMedia.Document as TLDocument, message.Date);
                    }
                    else if (message.IsSticker())
                    {
                        _stickersService.AddRecentSticker(StickerType.Image, documentMedia.Document as TLDocument, message.Date);
                    }
                }
            }

            if (With is TLUserBase && messageCommon.ToId is TLPeerUser && !messageCommon.IsOut && ((TLUserBase)With).Id == messageCommon.FromId.Value)
            {
                InsertMessage(messageCommon);

                //if (this._isActive)
                {
                    //var message = messageCommon as TLMessage;
                    //if (message != null)
                    {
                        //var replyKeyboardRows = message.ReplyMarkup as IReplyKeyboardRows;
                        //if (replyKeyboardRows != null)
                        //{
                        //    var keyboardButtonBase = Enumerable.FirstOrDefault<TLKeyboardButtonBase>(Enumerable.SelectMany<TLKeyboardButtonRow, TLKeyboardButtonBase>(replyKeyboardRows.Rows, (TLKeyboardButtonRow x) => x.Buttons), (TLKeyboardButtonBase x) => x is TLKeyboardButtonSwitchInline);
                        //    if (keyboardButtonBase != null)
                        //    {
                        //        this.Send(messageCommon, keyboardButtonBase, true);
                        //    }
                        //}
                    }
                }
            }
            else if (With is TLUserBase && messageCommon.ToId is TLPeerUser && messageCommon.IsOut && ((TLUserBase)With).Id == messageCommon.ToId.Id)
            {
                InsertMessage(messageCommon);
            }
            else if (With is TLChatBase && ((messageCommon.ToId is TLPeerChat && ((TLChatBase)With).Id == messageCommon.ToId.Id) || (messageCommon.ToId is TLPeerChannel && ((TLChatBase)With).Id == messageCommon.ToId.Id)))
            {
                InsertMessage(messageCommon);
                RaisePropertyChanged(() => With);

                var serviceMessage = messageCommon as TLMessageService;
                if (serviceMessage != null)
                {
                    var migrateAction = serviceMessage.Action as TLMessageActionChatMigrateTo;
                    if (migrateAction != null)
                    {
                        var channel = CacheService.GetChat(migrateAction.ChannelId) as TLChannel;
                        if (channel != null)
                        {
                            //channel.MigratedFromChatId = ((TLChatBase)this.With).Id;
                            //channel.MigratedFromMaxId = serviceMessage.Id;

                            Execute.BeginOnUIThread(() =>
                            {
                                //this.StateService.With = channel;
                                //this.StateService.RemoveBackEntries = true;
                                //this.NavigationService.Navigate(new Uri("/Views/Dialogs/DialogDetailsView.xaml?rndParam=" + TLInt.Random(), 2));
                            });
                        }
                        return;
                    }

                    var deleteUserAction = serviceMessage.Action as TLMessageActionChatDeleteUser;
                    if (deleteUserAction != null)
                    {
                        var userId = deleteUserAction.UserId;
                        //if (this._replyMarkupMessage != null && this._replyMarkupMessage.FromId.Value == userId.Value)
                        //{
                        //    this.SetReplyMarkup(null, false);
                        //}
                        //this.GetFullInfo();
                    }

                    var addUserAction = serviceMessage.Action as TLMessageActionChatAddUser;
                    if (addUserAction != null)
                    {
                        //this.GetFullInfo();
                    }

                    //this.Subtitle = this.GetSubtitle();
                }
            }

            //this.IsEmptyDialog = (base.Items.get_Count() == 0 && this.LazyItems.get_Count() == 0);
        }

        private void InsertMessage(TLMessageCommonBase messageCommon)
        {
            ProcessReplies(new List<TLMessageBase> { messageCommon });

            Execute.BeginOnUIThread(() =>
            {
                var index = TLDialog.InsertMessageInOrder(Messages, messageCommon);
                if (index != -1)
                {
                    var message = messageCommon as TLMessage;
                    if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                    {
                        var user = CacheService.GetUser(message.FromId) as TLUser;
                        if (user != null && user.IsBot)
                        {
                            SetReplyMarkup(message);
                        }
                    }

                    Execute.BeginOnThreadPool(delegate
                    {
                        MarkAsRead(messageCommon);

                        if (messageCommon is TLMessage)
                        {
                            InputTypingManager.RemoveTypingUser(messageCommon.FromId ?? 0);
                        }
                    });
                }
            });
        }

        private void MarkAsRead(TLMessageCommonBase messageCommon)
        {
            //if (!this._isActive)
            //{
            //    return;
            //}

            if (!App.IsActive || !App.IsVisible)
            {
                return;
            }

            if (messageCommon != null && !messageCommon.IsOut && messageCommon.IsUnread)
            {
                //base.StateService.GetNotifySettingsAsync(delegate (Settings settings)
                //{
                //    if (settings.InvisibleMode)
                //    {
                //        return;
                //    }
                _currentDialog = (_currentDialog ?? CacheService.GetDialog(Peer.ToPeer()));

                var dialog = _currentDialog;
                if (dialog != null)
                {
                    var topMessage = dialog.TopMessageItem as TLMessageCommonBase;
                    SetRead(topMessage, d => 0);
                }

                var channel = With as TLChannel;
                if (channel != null)
                {
                    ProtoService.ReadHistoryAsync(channel, messageCommon.Id);
                }
                else
                {
                    ProtoService.ReadHistoryAsync(Peer, messageCommon.Id, 0);
                }
                //});
            }
        }

        private void SetRead(TLMessageCommonBase topMessage, Func<TLDialog, int> getUnreadCount)
        {
            Execute.BeginOnUIThread(delegate
            {
                for (int i = 0; i < Messages.Count; i++)
                {
                    var messageCommon = Messages[i] as TLMessageCommonBase;
                    if (messageCommon != null && !messageCommon.IsOut && messageCommon.IsUnread)
                    {
                        messageCommon.SetUnread(false);
                    }
                }

                if (topMessage != null && !topMessage.IsOut && topMessage.IsUnread)
                {
                    topMessage.SetUnread(false);
                }

                _currentDialog.UnreadCount = getUnreadCount.Invoke(_currentDialog);
                _currentDialog.RaisePropertyChanged(() => _currentDialog.UnreadCount);

                var dialog = _currentDialog as TLDialog;
                if (dialog != null)
                {
                    dialog.RaisePropertyChanged(() => dialog.TopMessageItem);
                }

                _currentDialog.RaisePropertyChanged(() => _currentDialog.Self);

                CacheService.Commit();
            });
        }
    }
}
