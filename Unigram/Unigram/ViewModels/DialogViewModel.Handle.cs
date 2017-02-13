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

namespace Unigram.ViewModels
{
    public partial class DialogViewModel :
        IHandle<TLMessageCommonBase>,
        IHandle<TLUpdateChannelPinnedMessage>,
        IHandle<TLUpdateEditChannelMessage>,
        IHandle<TLUpdateEditMessage>,
        IHandle<MessagesRemovedEventArgs>,
        IHandle<TLUpdateUserStatus>,
        IHandle
    {
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
                var user = With as TLUser;
                if (user != null)
                {
                    LastSeen = LastSeenHelper.GetLastSeenTime(user);
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
            var user = With as TLUser;
            if (user != null && user.HasStatus)
            {
                return LastSeenHelper.GetLastSeenTime(user);
            }

            var channel = With as TLChannel;
            if (channel != null)
            {
                var response = await ProtoService.GetFullChannelAsync(new TLInputChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash.Value });
                if (response.IsSucceeded)
                {
                    var channelFull = response.Result.FullChat as TLChannelFull;
                    if (channelFull != null)
                    {
                        if (channel.IsBroadcast && channelFull.HasParticipantsCount)
                        {
                            return string.Format("{0} members", channelFull.ParticipantsCount.Value);
                        }
                        else if (channelFull.HasParticipantsCount)
                        {
                            var config = CacheService.GetConfig();
                            if (config != null && channelFull.ParticipantsCount <= config.ChatSizeMax)
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
                                        return string.Format("{0} members, {1} online", channelFull.ParticipantsCount.Value, count);
                                    }
                                }
                            }

                            return string.Format("{0} members", channelFull.ParticipantsCount.Value);
                        }
                    }
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

            if (With is TLUserBase && messageCommon.ToId is TLPeerUser && !messageCommon.IsOut && ((TLUserBase)With).Id == messageCommon.FromId.Value)
            {
                InsertMessage(messageCommon);

                //if (this._isActive)
                {
                    var message = messageCommon as TLMessage;
                    if (message != null)
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

                        //if (messageCommon is TLMessage)
                        //{
                        //    this.InputTypingManager.RemoveTypingUser(messageCommon.FromId.Value);
                        //}
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
