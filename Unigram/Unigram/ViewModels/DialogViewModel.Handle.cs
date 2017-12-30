using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.TL.Channels;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.System.Profile;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel :
        IHandle<TLMessageCommonBase>,
        IHandle<TLUpdateChannelPinnedMessage>,
        IHandle<TLUpdateEditChannelMessage>,
        IHandle<TLUpdateEditMessage>,
        IHandle<TLUpdateUserStatus>,
        IHandle<TLUpdateDraftMessage>,
        IHandle<TLUpdateContactLink>,
        IHandle<TLUpdateChannel>,
        IHandle<MessagesRemovedEventArgs>,
        IHandle<MessageExpiredEventArgs>,
        IHandle<DialogRemovedEventArgs>,
        IHandle<UpdateCompletedEventArgs>,
        IHandle<ChannelUpdateCompletedEventArgs>,
        IHandle<ChannelAvailableMessagesEventArgs>,
        IHandle<string>
    {
        public async void Handle(string message)
        {
            if (message.Equals("Window_Activated"))
            {
                if (!IsActive || !App.IsActive || !App.IsVisible)
                {
                    return;
                }

                var participant = _with;
                var dialog = _dialog;
                if (dialog != null && Items.Count > 0)
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
                    if (readPeer != null)
                    {
                        readPeer.ReadInboxMaxId = dialog.TopMessage;
                    }

                    dialog.ReadInboxMaxId = dialog.TopMessage;
                    dialog.UnreadCount = dialog.UnreadCount - unread;
                    dialog.RaisePropertyChanged(() => dialog.UnreadCount);

                    RemoveNotifications();
                }

                TextField.FocusMaybe(FocusState.Keyboard);
            }
            else if (message.Equals("Window_Deactivated"))
            {
                if (Dispatcher != null)
                {
                    Dispatcher.Dispatch(SaveDraft);
                }
            }
        }

        public void Handle(ChannelAvailableMessagesEventArgs args)
        {
            if (With == args.Dialog.With)
            {
                BeginOnUIThread(() =>
                {
                    var groups = new Dictionary<long, Tuple<TLMessage, GroupedMessages>>();

                    for (var i = 0; i < Items.Count; i++)
                    {
                        var messageCommon = Items[i] as TLMessageCommonBase;
                        if (messageCommon != null && messageCommon.ToId is TLPeerChannel && messageCommon.Id <= args.AvailableMinId)
                        {
                            if (messageCommon is TLMessage grouped && grouped.HasGroupedId && grouped.GroupedId is long groupedId && _groupedMessages.TryGetValue(groupedId, out TLMessage group) && group.Media is TLMessageMediaGroup groupMedia)
                            {
                                groupMedia.Layout.Messages.Remove(grouped);
                                groups[groupedId] = Tuple.Create(group, groupMedia.Layout);
                            }
                        }
                    }

                    foreach (var group in groups.Values)
                    {
                        if (group.Item2.Messages.Count > 0)
                        {
                            group.Item2.Calculate();
                            group.Item1.RaisePropertyChanged(() => group.Item1.Self);
                        }
                        else
                        {
                            _groupedMessages.TryRemove(group.Item2.GroupedId, out TLMessage removed);
                            Items.Remove(group.Item1);
                        }
                    }

                    for (var i = 0; i < Items.Count; i++)
                    {
                        var messageCommon = Items[i] as TLMessageCommonBase;
                        if (messageCommon != null && messageCommon.ToId is TLPeerChannel && messageCommon.Id <= args.AvailableMinId)
                        {
                            Items.RemoveAt(i--);
                        }
                    }

                    //IsEmpty = Items.Count == 0 && (_messages == null || _messages.Count == 0) && LazyItems.Count == 0;
                });
            }
        }

        public void Handle(TLUpdateContactLink update)
        {
            if (With is TLUser user && user.Id == update.UserId)
            {
                BeginOnUIThread(() =>
                {
                    IsShareContactAvailable = user.HasAccessHash && !user.HasPhone && !user.IsSelf && !user.IsContact && !user.IsMutualContact;
                    IsAddContactAvailable = user.HasAccessHash && user.HasPhone && !user.IsSelf && !user.IsContact && !user.IsMutualContact;

                    RaisePropertyChanged(() => With);

                    //this.Subtitle = this.GetSubtitle();
                    //base.NotifyOfPropertyChange<TLObject>(() => this.With);
                    //this.ChangeUserAction();
                });
            }
        }

        public void Handle(TLUpdateDraftMessage args)
        {
            var flag = false;

            var userBase = With as TLUserBase;
            var chatBase = With as TLChatBase;
            if (userBase != null && args.Peer is TLPeerUser && userBase.Id == args.Peer.Id)
            {
                flag = true;
            }
            else if (chatBase != null && args.Peer is TLPeerChat && chatBase.Id == args.Peer.Id)
            {
                flag = true;
            }
            else if (chatBase != null && args.Peer is TLPeerChannel && chatBase.Id == args.Peer.Id)
            {
                flag = true;
            }

            if (flag)
            {
                BeginOnUIThread(() =>
                {
                    if (args.Draft is TLDraftMessage draft)
                    {
                        SetText(draft.Message, draft.Entities);
                    }
                    else if (args.Draft is TLDraftMessageEmpty emptyDraft)
                    {
                        SetText(null);
                    }
                });
            }
        }

        public void Handle(ChannelUpdateCompletedEventArgs args)
        {
            if (With is TLChannel channel && channel.Id == args.ChannelId)
            {
                Handle(new UpdateCompletedEventArgs());
            }
        }

        public void Handle(UpdateCompletedEventArgs args)
        {
            BeginOnUIThread(async () =>
            {
                IsFirstSliceLoaded = false;
                IsLastSliceLoaded = false;

                var maxId = _dialog?.UnreadCount > 0 ? _dialog.ReadInboxMaxId : int.MaxValue;
                var offset = _dialog?.UnreadCount > 0 && maxId > 0 ? -16 : 0;
                await LoadFirstSliceAsync(maxId, offset);
            });
        }

        public void Handle(DialogRemovedEventArgs args)
        {
            if (With == args.Dialog.With)
            {
                BeginOnUIThread(() =>
                {
                    Items.Clear();
                    SelectedItems.Clear();
                    SelectionMode = Windows.UI.Xaml.Controls.ListViewSelectionMode.None;
                });
            }
        }

        public void Handle(MessagesRemovedEventArgs args)
        {
            if (With == args.Dialog.With && args.Messages != null)
            {
                BeginOnUIThread(() =>
                {
                    var groups = new Dictionary<long, Tuple<TLMessage, GroupedMessages>>();

                    foreach (var message in args.Messages)
                    {
                        if (message is TLMessage grouped && grouped.HasGroupedId && grouped.GroupedId is long groupedId && _groupedMessages.TryGetValue(groupedId, out TLMessage group) && group.Media is TLMessageMediaGroup groupMedia)
                        {
                            groupMedia.Layout.Messages.Remove(grouped);
                            groups[groupedId] = Tuple.Create(group, groupMedia.Layout);
                        }
                    }

                    foreach (var group in groups.Values)
                    {
                        if (group.Item2.Messages.Count > 0)
                        {
                            group.Item2.Calculate();
                            group.Item1.RaisePropertyChanged(() => group.Item1.Self);
                        }
                        else
                        {
                            _groupedMessages.TryRemove(group.Item2.GroupedId, out TLMessage removed);
                            Items.Remove(group.Item1);
                        }
                    }

                    foreach (var message in args.Messages)
                    {
                        if (EditedMessage?.Id == message.Id)
                        {
                            ClearReplyCommand.Execute();
                        }
                        else if (ReplyInfo?.ReplyToMsgId == message.Id)
                        {
                            ClearReplyCommand.Execute();
                        }

                        if (PinnedMessage?.Id == message.Id)
                        {
                            PinnedMessage = null;
                        }

                        if (Full is TLChannelFull channelFull && channelFull.PinnedMsgId == message.Id)
                        {
                            channelFull.PinnedMsgId = null;
                            channelFull.HasPinnedMsgId = false;
                        }

                        var removed = Items.Remove(message);
                        if (removed == false)
                        {
                            // Check if this is really needed

                            var already = Items.FirstOrDefault(x => x.Id == message.Id);
                            if (already != null)
                            {
                                Items.Remove(already);
                            }
                        }
                    }
                });
            }
        }

        public void Handle(TLUpdateUserStatus statusUpdate)
        {
            BeginOnUIThread(() =>
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
                BeginOnUIThread(() =>
                {
                    var already = Items.FirstOrDefault(x => x.Id == update.Message.Id) as TLMessage;
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
                    message.RaisePropertyChanged(() => message.SelfBase);
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
                BeginOnUIThread(() =>
                {
                    var already = Items.FirstOrDefault(x => x.Id == update.Message.Id) as TLMessage;
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
                    message.RaisePropertyChanged(() => message.SelfBase);
                });
            }
        }

        public void Handle(MessageExpiredEventArgs update)
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
                BeginOnUIThread(() =>
                {
                    var index = Items.IndexOf(message);
                    Items.RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, message, index, index));
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

            if (!IsFirstSliceLoaded)
            {
                Execute.ShowDebugMessage("DialogViewModel.Handle(TLMessageCommonBase) IsFirstSliceLoaded=false");
                return;
            }

            if (messageCommon is TLMessage message)
            {
                if (message.IsOut && !message.HasFwdFrom && message.Media is TLMessageMediaDocument documentMedia)
                {
                    if (message.IsGif())
                    {
                        _stickersService.AddRecentGif(documentMedia.Document as TLDocument, message.Date);
                    }
                    else if (message.IsSticker())
                    {
                        _stickersService.AddRecentSticker(StickerType.Image, documentMedia.Document as TLDocument, message.Date, false);
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

                            BeginOnUIThread(() =>
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

        private async void InsertMessage(TLMessageCommonBase messageCommon)
        {
            using (await _insertLock.WaitAsync())
            {
                var result = new List<TLMessageBase> { messageCommon };
                ProcessReplies(result);

                messageCommon = result.FirstOrDefault() as TLMessageCommonBase;
                if (messageCommon == null)
                {
                    return;
                }

                BeginOnUIThread(() =>
                {
                    var index = InsertMessageInOrder(Items, messageCommon);
                    if (index < 0)
                    {
                        return;
                    }

                    if (messageCommon is TLMessage message && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                    {
                        var user = CacheService.GetUser(message.FromId) as TLUser;
                        if (user != null && user.IsBot)
                        {
                            SetReplyMarkup(message);

                            if (message.ReplyMarkup is TLReplyKeyboardMarkup)
                            {
                                InputPane.GetForCurrentView().TryHide();
                            }
                        }
                    }

                    Execute.BeginOnThreadPool(() =>
                    {
                        MarkAsRead(messageCommon);

                        if (messageCommon is TLMessage)
                        {
                            InputTypingManager.RemoveTypingUser(messageCommon.FromId ?? 0);
                        }
                    });
                });
            }
        }

        public static int InsertMessageInOrder(IList<TLMessageBase> messages, TLMessageBase message)
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


#if DEBUG
        [DllImport("user32.dll")]
        public static extern Boolean GetLastInputInfo(ref LASTINPUTINFO plii);
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public Int32 dwTime;
        }
#endif

        private void MarkAsRead(TLMessageCommonBase messageCommon)
        {
            if (!IsActive || !App.IsActive || !App.IsVisible)
            {
                return;
            }

#if DEBUG
            if (AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Desktop"))
            {
                LASTINPUTINFO lastInput = new LASTINPUTINFO();
                lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
                lastInput.dwTime = 0;

                if (GetLastInputInfo(ref lastInput))
                {
                    var idleTime = Environment.TickCount - lastInput.dwTime;
                    if (idleTime >= 60 * 1000)
                    {
                        return;
                    }
                }
            }
#endif

            if (messageCommon is TLMessage message && message.Media is TLMessageMediaGroup groupMedia)
            {
                messageCommon = groupMedia.Layout.Messages.LastOrDefault();
            }

            if (messageCommon != null && !messageCommon.IsOut && messageCommon.IsUnread)
            {
                _dialog = (_dialog ?? CacheService.GetDialog(Peer.ToPeer()));

                var dialog = _dialog;
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

                RemoveNotifications();
            }
        }

        private void SetRead(TLMessageCommonBase topMessage, Func<TLDialog, int> getUnreadCount)
        {
            BeginOnUIThread(delegate
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var messageCommon = Items[i] as TLMessageCommonBase;
                    if (messageCommon != null && !messageCommon.IsOut && messageCommon.IsUnread)
                    {
                        messageCommon.SetUnread(false);
                    }
                }

                if (topMessage != null && !topMessage.IsOut && topMessage.IsUnread)
                {
                    topMessage.SetUnread(false);
                }

                _dialog.ReadInboxMaxId = Dialog.TopMessage;
                _dialog.UnreadCount = getUnreadCount.Invoke(_dialog);
                _dialog.RaisePropertyChanged(() => _dialog.UnreadCount);

                var dialog = _dialog as TLDialog;
                if (dialog != null)
                {
                    dialog.RaisePropertyChanged(() => dialog.TopMessageItem);
                }

                _dialog.RaisePropertyChanged(() => _dialog.Self);

                CacheService.Commit();
            });
        }

        public void Handle(TLUpdateChannel update)
        {
            if (With is TLChannel channel && channel.Id == update.ChannelId)
            {
                RaisePropertyChanged(() => With);
                RaisePropertyChanged(() => Full);
                RaisePropertyChanged(() => WithChannel);
                RaisePropertyChanged(() => FullChannel);

                if (channel.HasBannedRights && channel.BannedRights.IsSendMessages)
                {
                    BeginOnUIThread(() => SetText(null));
                }

                if (Full is TLChannelFull channelFull)
                {
                    _stickers.SyncGroup(channelFull);
                }
            }
        }
    }
}
