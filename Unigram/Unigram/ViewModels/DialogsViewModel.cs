using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Logs;
using Template10.Utils;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class DialogsViewModel : UnigramViewModelBase, 
        IHandle<TopMessageUpdatedEventArgs>, 
        IHandle<DialogAddedEventArgs>, 
        IHandle<DialogRemovedEventArgs>, 
        //IHandle<DownloadableItem>, 
        //IHandle<UploadableItem>, 
        //IHandle<string>, 
        //IHandle<TLEncryptedChatBase>, 
        IHandle<TLUpdateUserName>, 
        IHandle<UpdateCompletedEventArgs>, 
        IHandle<ChannelUpdateCompletedEventArgs>, 
        IHandle<TLUpdateNotifySettings>, 
        IHandle<TLUpdateNewAuthorization>, 
        IHandle<TLUpdateServiceNotification>, 
        //IHandle<TLUpdateUserTyping>, 
        //IHandle<TLUpdateChatUserTyping>, 
        //IHandle<ClearCacheEventArgs>, 
        //IHandle<ClearLocalDatabaseEventArgs>, 
        IHandle<TLUpdateEditMessage>, 
        IHandle<TLUpdateEditChannelMessage>, 
        IHandle<TLUpdateDraftMessage>, 
        IHandle<TLUpdateChannel>, 
        IHandle
    {
        public DialogsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<TLDialog>();
        }

        public async void LoadFirstSlice()
        {
            var lastDate = 0;
            var lastMsgId = 0;
            var lastPeer = (TLInputPeerBase)new TLInputPeerEmpty();

            var last = Items.LastOrDefault();
            if (last != null && last.TopMessageItem != null)
            {
                lastDate = last.TopMessageItem.Date;
                lastMsgId = last.TopMessage;

                if (last.Peer is TLPeerUser)
                {
                    lastPeer = new TLInputPeerUser { UserId = last.Peer.Id };
                }
                else if (last.Peer is TLPeerChat)
                {
                    lastPeer = new TLInputPeerChat { ChatId = last.Peer.Id };
                }
                else if (last.Peer is TLPeerChannel)
                {
                    lastPeer = new TLInputPeerChannel { ChannelId = last.Peer.Id };
                }
            }

            var response = await ProtoService.GetDialogsAsync(lastDate, lastMsgId, lastPeer, 200);
            if (response.IsSucceeded)
            {
                foreach (var item in response.Value.Dialogs)
                {
                    Items.Add(item);
                }
            }

            Aggregator.Subscribe(this);
        }

        #region Handle
        public void Handle(TopMessageUpdatedEventArgs eventArgs)
        {
            eventArgs.Dialog.RaisePropertyChanged(() => eventArgs.Dialog.With);
            OnTopMessageUpdated(this, eventArgs);
        }

        public void Handle(ChannelUpdateCompletedEventArgs args)
        {
            var dialog = CacheService.GetDialog(new TLPeerChannel { Id = args.ChannelId });
            if (dialog != null)
            {
                var message = dialog.Messages.FirstOrDefault();
                if (message != null)
                {
                    Handle(new TopMessageUpdatedEventArgs(dialog, message));
                }
            }
        }

        public void Handle(DialogAddedEventArgs eventArgs)
        {
            OnDialogAdded(this, eventArgs);
        }

        public void Handle(DialogRemovedEventArgs args)
        {
            Execute.BeginOnUIThread(() =>
            {
                var dialog = Items.FirstOrDefault(x => x.Index == args.Dialog.Index);
                if (dialog != null)
                {
                    Items.Remove(dialog);
                }
            });
        }

        public void Handle(TLUpdateUserName userName)
        {
            Execute.BeginOnUIThread(() =>
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].WithId == userName.UserId && Items[i].With is TLUser)
                    {
                        var user = (TLUser)Items[i].With;
                        user.FirstName = userName.FirstName;
                        user.LastName = userName.LastName;
                        user.Username = userName.Username;
                        user.HasUsername = userName.Username != null;

                        Items[i].RaisePropertyChanged(() => Items[i].With);
                        return;
                    }
                }
            });
        }

        public void Handle(UpdateCompletedEventArgs args)
        {
            var dialogs = CacheService.GetDialogs();
            ReorderDrafts(dialogs);
            Execute.BeginOnUIThread(() =>
            {
                Items.Clear();
                Items.AddRange(dialogs);
            });
        }

        //public void Handle(TLUpdateUserTyping userTyping)
        //{
        //    this.HandleTypingCommon(userTyping, this._userTypingCache);
        //}

        //public void Handle(TLUpdateChatUserTyping chatUserTyping)
        //{
        //    this.HandleTypingCommon(chatUserTyping, this._chatUserTypingCache);
        //}

        public void Handle(TLUpdateEditMessage update)
        {
            var message = update.Message as TLMessageCommonBase;
            if (message == null)
            {
                return;
            }

            Execute.BeginOnUIThread(delegate
            {
                int msgId;
                if (message.ToId is TLPeerUser)
                {
                    msgId = (message.IsOut ? message.ToId.Id : message.FromId.Value);
                }
                else
                {
                    msgId = message.ToId.Id;
                }

                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Index == msgId && Items[i].TopMessage != null && Items[i].TopMessage == message.Id)
                    {
                        Items[i].RaisePropertyChanged(() => Items[i].Self);
                    }
                }
            });
        }

        public void Handle(TLUpdateEditChannelMessage update)
        {
            var message = update.Message as TLMessageCommonBase;
            if (message == null)
            {
                return;
            }

            Execute.BeginOnUIThread(delegate
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Index == message.ToId.Id && Items[i].TopMessage != null && Items[i].TopMessage == message.Id)
                    {
                        Items[i].RaisePropertyChanged(() => Items[i].Self);
                    }
                }
            });
        }

        public void Handle(TLUpdateDraftMessage update)
        {
            Execute.BeginOnUIThread(delegate
            {
                TLDialog dialog53 = null;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Index == update.Peer.Id)
                    {
                        dialog53 = (Items[i] as TLDialog);
                        if (dialog53 != null)
                        {
                            dialog53.Draft = update.Draft;
                            dialog53.RaisePropertyChanged(() => dialog53.Draft);
                            dialog53.RaisePropertyChanged(() => dialog53.Self);
                        }
                        Items.RemoveAt(i);
                        break;
                    }
                }
                if (dialog53 != null)
                {
                    for (int j = 0; j < Items.Count; j++)
                    {
                        if (Items[j].GetDateIndexWithDraft() <= dialog53.GetDateIndexWithDraft())
                        {
                            Items.Insert(j, dialog53);
                            return;
                        }
                    }
                }
            });
        }

        public void Handle(TLUpdateChannel update)
        {
            Execute.BeginOnUIThread(delegate
            {
                var dialog = Items.FirstOrDefault(x => x.Peer is TLPeerChannel && x.Peer.Id == update.ChannelId);
                if (dialog != null)
                {
                    dialog.RaisePropertyChanged(() => dialog.Self);
                }
            });
        }

        public void Handle(TLUpdateServiceNotification serviceNotification)
        {
            if (serviceNotification.Popup)
            {
                Execute.BeginOnUIThread(delegate
                {
                    //MessageBox.Show(serviceNotification.Message.ToString(), AppResources.AppName, 0);
                });
                return;
            }
            //if (CacheService.GetUser(777000) == null)
            //{
            //    return;
            //}
            //TLMessageBase serviceMessage = this.GetServiceMessage(tLInt, serviceNotification.Message, serviceNotification.Media, null);
            //this.CacheService.SyncMessage(serviceMessage, delegate (TLMessageBase m)
            //{
            //});
        }

        public void Handle(TLUpdateNewAuthorization update)
        {
            var user = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
            var service = CacheService.GetUser(777000);
            if (user == null) return;
            if (service == null) return;

            var messageFormat = "{0},\r\nWe detected a login into your account from a new device on {1}, {2} at {3}\r\n\r\nDevice: {4}\r\nLocation: {5}\r\n\r\nIf this wasn't you, you can go to Settings — Privacy and Security — Terminate all sessions.\r\n\r\nThanks, The Telegram Team";

            var firstName = user.FirstName;
            var dateTime = TLUtils.ToDateTime(update.Date);
            var message = string.Format(messageFormat, firstName, dateTime.ToString("dddd"), dateTime.ToString("M"), dateTime.ToString("t"), update.Device, update.Location);
            var serviceMessage = TLUtils.GetMessage(777000, new TLPeerUser { UserId = SettingsHelper.UserId }, TLMessageState.Confirmed, false, true, update.Date, message, new TLMessageMediaEmpty(), TLLong.Random(), null);
            serviceMessage.Id = 0;

            CacheService.SyncMessage(serviceMessage, x => { });
        }

        public void Handle(TLUpdateNotifySettings notifySettings)
        {
            TLNotifyPeer notifyPeer = notifySettings.Peer as TLNotifyPeer;
            if (notifyPeer != null)
            {
                Execute.BeginOnUIThread(delegate
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        var dialog = Items[i] as TLDialog;
                        if (dialog != null && dialog.Peer != null && dialog.Peer.Id == notifyPeer.Peer.Id && dialog.Peer.GetType() == notifyPeer.Peer.GetType())
                        {
                            dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                            dialog.RaisePropertyChanged(() => dialog.Self);
                            return;
                        }
                    }
                });
            }
        }

        //private void HandleTypingCommon(TLUpdateTypingBase updateTyping, Dictionary<int, Telegram.Api.WindowsPhone.Tuple<TLDialog, InputTypingManager>> typingCache)
        //{
        //    Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
        //    {
        //        TelegramTransitionFrame telegramTransitionFrame = Application.get_Current().get_RootVisual() as TelegramTransitionFrame;
        //        if (telegramTransitionFrame != null && !(telegramTransitionFrame.get_Content() is ShellView))
        //        {
        //            return;
        //        }
        //        TLUpdateChatUserTyping tLUpdateChatUserTyping = updateTyping as TLUpdateChatUserTyping;
        //        TLInt tLInt = (tLUpdateChatUserTyping != null) ? tLUpdateChatUserTyping.ChatId : updateTyping.UserId;
        //        Telegram.Api.WindowsPhone.Tuple<TLDialog, InputTypingManager> tuple;
        //        if (!typingCache.TryGetValue(tLInt.Value, ref tuple))
        //        {
        //            int i = 0;
        //            while (i < this.Items.get_Count())
        //            {
        //                if ((tLUpdateChatUserTyping == null && this.Items.get_Item(i).Peer is TLPeerUser && this.Items.get_Item(i).Peer.Id.Value == tLInt.Value) || (tLUpdateChatUserTyping != null && this.Items.get_Item(i).Peer is TLPeerChat && this.Items.get_Item(i).Peer.Id.Value == tLInt.Value) || (tLUpdateChatUserTyping != null && this.Items.get_Item(i).Peer is TLPeerChannel && this.Items.get_Item(i).Peer.Id.Value == tLInt.Value))
        //                {
        //                    TLDialog dialog = this.Items.get_Item(i) as TLDialog;
        //                    if (dialog != null)
        //                    {
        //                        tuple = new Telegram.Api.WindowsPhone.Tuple<TLDialog, InputTypingManager>(dialog, new InputTypingManager(delegate (IList<Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>> users)
        //                        {
        //                            Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
        //                            {
        //                                dialog.TypingString = this.GetTypingString(dialog.Peer, users);
        //                                dialog.NotifyOfPropertyChange<string>(() => dialog.Self.TypingString);
        //                            });
        //                        }, delegate
        //                        {
        //                            Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
        //                            {
        //                                dialog.TypingString = null;
        //                                dialog.NotifyOfPropertyChange<string>(() => dialog.Self.TypingString);
        //                            });
        //                        }));
        //                        typingCache.set_Item(tLInt.Value, tuple);
        //                        break;
        //                    }
        //                    break;
        //                }
        //                else
        //                {
        //                    i++;
        //                }
        //            }
        //        }
        //        if (tuple != null)
        //        {
        //            TLSendMessageActionBase action = null;
        //            IUserTypingAction userTypingAction = updateTyping as IUserTypingAction;
        //            if (userTypingAction != null)
        //            {
        //                action = userTypingAction.Action;
        //            }
        //            tuple.Item2.AddTypingUser(updateTyping.UserId.Value, action);
        //        }
        //    });
        //}
        #endregion

        private void OnTopMessageUpdated(object sender, TopMessageUpdatedEventArgs e)
        {
            Execute.BeginOnUIThread(() =>
            {
                try
                {
                    var chat = e.Dialog.With as TLChat;
                    if (chat != null)
                    {
                        var serviceMessage = e.Dialog.TopMessageItem as TLMessageService;
                        if (serviceMessage != null)
                        {
                            var migrateAction = serviceMessage.Action as TLMessageActionChatMigrateTo;
                            if (migrateAction != null)
                            {
                                Items.Remove(e.Dialog);
                                return;
                            }
                        }
                    }

                    var channel = e.Dialog.With as TLChannel;
                    if (channel != null)
                    {
                        var serviceMessage = e.Dialog.TopMessageItem as TLMessageService;
                        if (serviceMessage != null)
                        {
                            var deleteUserAction = serviceMessage.Action as TLMessageActionChatDeleteUser;
                            if (deleteUserAction != null && deleteUserAction.UserId == SettingsHelper.UserId)
                            {
                                Items.Remove(e.Dialog);
                                return;
                            }
                        }
                    }

                    // TODO: e.Dialog.TypingString = null;

                    var currentPosition = Items.IndexOf(e.Dialog);
                    var position = currentPosition;
                    for (int i = 0; i < Items.Count; i++)
                    {
                        if (i != currentPosition && Items[i].GetDateIndexWithDraft() <= e.Dialog.GetDateIndexWithDraft())
                        {
                            position = i;
                            break;
                        }
                    }

                    if (currentPosition == -1 && currentPosition == position)
                    {
                        Execute.ShowDebugMessage(string.Concat(new object[]
                        {
                            "TLDialog with=",
                            e.Dialog.With,
                            " curPos=newPos=-1 isLastSliceLoaded=",
                            IsLastSliceLoaded
                        }));
                        if (!IsLastSliceLoaded)
                        {
                            return;
                        }
                        Items.Add(e.Dialog);
                    }
                    if (currentPosition != position)
                    {
                        if (currentPosition >= 0 && currentPosition < position)
                        {
                            if (currentPosition + 1 == position)
                            {
                                Items[currentPosition].RaisePropertyChanged(() => Items[currentPosition].Self);
                                Items[currentPosition].RaisePropertyChanged(() => Items[currentPosition].UnreadCount);
                            }
                            else
                            {
                                Items.Remove(e.Dialog);
                                Items.Insert(position - 1, e.Dialog);
                            }
                        }
                        else
                        {
                            Items.Remove(e.Dialog);
                            Items.Insert(position, e.Dialog);
                        }
                    }
                    else
                    {
                        if (!IsLastSliceLoaded && Items.Count > 0 && Items[Items.Count - 1].GetDateIndexWithDraft() > e.Dialog.GetDateIndexWithDraft())
                        {
                            Items.Remove(e.Dialog);
                        }
                        Items[currentPosition].RaisePropertyChanged(() => Items[currentPosition].Self);
                        Items[currentPosition].RaisePropertyChanged(() => Items[currentPosition].UnreadCount);
                    }
                }
                catch (Exception ex)
                {
                    Log.Write(string.Format("DialogsViewModel.Handle OnTopMessageUpdatedEventArgs ex " + ex, new object[0]), null);
                    throw ex;
                }
            });
        }

        private void OnDialogAdded(object sender, DialogAddedEventArgs e)
        {
            var dialog = e.Dialog;
            if (dialog == null)
            {
                return;
            }

            Execute.BeginOnUIThread(() =>
            {
                var index = -1;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i] == e.Dialog)
                    {
                        return;
                    }

                    if (Items[i].GetDateIndexWithDraft() < dialog.GetDateIndexWithDraft())
                    {
                        index = i;
                        break;
                    }
                }

                if (e.Dialog.Peer is TLPeerChannel)
                {
                    for (int j = 0; j < Items.Count; j++)
                    {
                        if (e.Dialog.Peer.GetType() == Items[j].Peer.GetType() && e.Dialog.Peer.Id == Items[j].Peer.Id)
                        {
                            Items.RemoveAt(j);
                            Execute.ShowDebugMessage("OnDialogAdded RemoveAt=" + j);
                            break;
                        }
                    }
                }
                if (index == -1)
                {
                    Items.Add(dialog);
                }
                else
                {
                    Items.Insert(index, dialog);
                }

                //this.Status = ((this.Items.get_Count() == 0 || this.LazyItems.get_Count() == 0) ? string.Empty : this.Status);
            });
        }

        private static void ReorderDrafts(IList<TLDialog> dialogs)
        {
            for (int i = 0; i < dialogs.Count; i++)
            {
                var dialog = dialogs[i] as TLDialog;
                var draftMessage = dialog.Draft as TLDraftMessage;
                if (draftMessage != null && dialog.GetDateIndexWithDraft() > dialog.GetDateIndex())
                {
                    int dateIndexWithDraft = dialog.GetDateIndexWithDraft();
                    for (int j = 0; j < i; j++)
                    {
                        if (dateIndexWithDraft >= dialogs[j].GetDateIndexWithDraft())
                        {
                            dialogs.RemoveAt(i);
                            dialogs.Insert(j, dialog);
                            break;
                        }
                    }
                }
            }
        }

        public bool IsLastSliceLoaded { get; set; }

        public ObservableCollection<TLDialog> Items { get; private set; }
    }
}
