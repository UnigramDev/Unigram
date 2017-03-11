using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
using Unigram.Common;
using Unigram.Controls;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
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
        //IHandle<TLUpdateNewAuthorization>, 
        IHandle<TLUpdateServiceNotification>,
        //IHandle<TLUpdateUserTyping>, 
        //IHandle<TLUpdateChatUserTyping>, 
        //IHandle<ClearCacheEventArgs>, 
        //IHandle<ClearLocalDatabaseEventArgs>, 
        IHandle<TLUpdateEditMessage>,
        IHandle<TLUpdateEditChannelMessage>,
        IHandle<TLUpdateDraftMessage>,
        IHandle<TLUpdateDialogPinned>,
        IHandle<TLUpdatePinnedDialogs>,
        IHandle<TLUpdateChannel>,
        IHandle
    {
        public DialogsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<TLDialog>();
            Search = new ObservableCollection<KeyedList<string, TLObject>>();
            SearchTokens = new Dictionary<string, CancellationTokenSource>();
        }

        public int PinnedDialogsIndex { get; set; }

        public int PinnedDialogsCountMax { get; set; }

        private bool _isFirstPinned;
        public bool IsFirstPinned
        {
            get
            {
                return _isFirstPinned;
            }
            set
            {
                Set(ref _isFirstPinned, value);
            }
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

            //ProtoService.GetDialogsCallback(lastDate, lastMsgId, lastPeer, 200, (result) =>
            //{
            //    var pinnedIndex = 0;

            //    Execute.BeginOnUIThread(() =>
            //    {
            //        foreach (var item in result.Dialogs)
            //        {
            //            if (item.IsPinned)
            //            {
            //                item.PinnedIndex = pinnedIndex++;
            //            }

            //            var chat = item.With as TLChat;
            //            if (chat != null && chat.HasMigratedTo)
            //            {
            //                continue;
            //            }
            //            else
            //            {
            //                Items.Add(item);
            //            }
            //        }

            //        IsFirstPinned = Items.Any(x => x.IsPinned);
            //        PinnedDialogsIndex = pinnedIndex;
            //        PinnedDialogsCountMax = config.PinnedDialogsCountMax;
            //    });
            //});

            var response = await ProtoService.GetDialogsAsync(lastDate, lastMsgId, lastPeer, 200);
            if (response.IsSucceeded)
            {
                var config = CacheService.GetConfig();
                var pinnedIndex = 0;

                Execute.BeginOnUIThread(() =>
                {
                    foreach (var item in response.Result.Dialogs)
                    {
                        if (item.IsPinned)
                        {
                            item.PinnedIndex = pinnedIndex++;
                        }

                        var chat = item.With as TLChat;
                        if (chat != null && chat.HasMigratedTo)
                        {
                            continue;
                        }
                        else
                        {
                            Items.Add(item);
                        }
                    }

                    IsFirstPinned = Items.Any(x => x.IsPinned);
                    PinnedDialogsIndex = pinnedIndex;
                    PinnedDialogsCountMax = config.PinnedDialogsCountMax;
                });
            }

            Aggregator.Subscribe(this);
        }

        public async Task UpdatePinnedItemsAsync()
        {
            var pinned = Items.Where(x => x.IsPinned).Select(x => x.ToInputPeer());

            var response = await ProtoService.ReorderPinnedDialogsAsync(new TLVector<TLInputPeerBase>(pinned), true);
            if (response.IsSucceeded)
            {

            }
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
            dialogs = ReorderDrafts(dialogs);
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
                TLDialog dialog = null;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Index == update.Peer.Id)
                    {
                        dialog = (Items[i] as TLDialog);
                        if (dialog != null)
                        {
                            dialog.Draft = update.Draft;
                            dialog.RaisePropertyChanged(() => dialog.Draft);
                            dialog.RaisePropertyChanged(() => dialog.Self);
                        }
                        Items.RemoveAt(i);
                        break;
                    }
                }

                if (dialog != null)
                {
                    for (int j = 0; j < Items.Count; j++)
                    {
                        if (Items[j].GetDateIndexWithDraft() <= dialog.GetDateIndexWithDraft())
                        {
                            Items.Insert(j, dialog);
                            return;
                        }
                    }
                }
            });
        }

        public void Handle(TLUpdateDialogPinned update)
        {
            Execute.BeginOnUIThread(() =>
            {
                TLDialog dialog = null;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Index == update.Peer.Id)
                    {
                        dialog = (Items[i] as TLDialog);
                        if (dialog != null)
                        {
                            dialog.IsPinned = update.IsPinned;
                            dialog.RaisePropertyChanged(() => dialog.IsPinned);
                            dialog.RaisePropertyChanged(() => dialog.Self);
                        }

                        Items.RemoveAt(i);
                        break;
                    }
                }

                if (dialog == null)
                {
                    dialog = CacheService.GetDialog(update.Peer);
                }

                if (dialog != null)
                {
                    IsFirstPinned = dialog.IsPinned ? true : Items.Any(x => x.IsPinned);
                    PinnedDialogsIndex = 1;

                    if (dialog.IsPinned)
                    {
                        dialog.PinnedIndex = PinnedDialogsIndex++;
                    }
                    else
                    {
                        dialog.PinnedIndex = 0;
                        PinnedDialogsIndex--;
                    }

                    foreach (var cached in Items)
                    {
                        if (cached.IsPinned)
                        {
                            cached.PinnedIndex = PinnedDialogsIndex++;
                        }
                    }

                    for (int j = 0; j < Items.Count; j++)
                    {
                        if (Items[j].GetDateIndexWithDraft() <= dialog.GetDateIndexWithDraft())
                        {
                            Items.Insert(j, dialog);
                            return;
                        }
                    }
                }
            });
        }

        public void Handle(TLUpdatePinnedDialogs update)
        {
            Execute.BeginOnUIThread(() =>
            {
                if (update.HasOrder)
                {
                    var pinned = new List<TLDialog>(update.Order.Count);

                    for (int i = 0; i < update.Order.Count; i++)
                    {
                        var dialog = Items.FirstOrDefault(x => x.Index == update.Order[i].Id);
                        if (dialog != null)
                        {
                            dialog.PinnedIndex = i;
                            dialog.IsPinned = true;

                            var index = Items.IndexOf(dialog);
                            if (index != i)
                            {
                                Items.Remove(dialog);
                                Items.Insert(i, dialog);
                            }
                        }

                        PinnedDialogsIndex = i;
                    }
                }

                IsFirstPinned = Items.FirstOrDefault()?.IsPinned ?? false;
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
            if (serviceNotification.IsPopup)
            {
                Execute.BeginOnUIThread(async () =>
                {
                    await TLMessageDialog.ShowAsync(serviceNotification.Message, "Telegram", "OK");
                });
            }
            else
            {
                Debugger.Break();
            }

            //if (serviceNotification.Popup)
            //{
            //    Execute.BeginOnUIThread(delegate
            //    {
            //        //MessageBox.Show(serviceNotification.Message.ToString(), AppResources.AppName, 0);
            //    });
            //    return;
            //}
            //if (CacheService.GetUser(777000) == null)
            //{
            //    return;
            //}
            //TLMessageBase serviceMessage = this.GetServiceMessage(tLInt, serviceNotification.Message, serviceNotification.Media, null);
            //this.CacheService.SyncMessage(serviceMessage, delegate (TLMessageBase m)
            //{
            //});
        }

        //public void Handle(TLUpdateNewAuthorization update)
        //{
        //    var user = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
        //    var service = CacheService.GetUser(777000);
        //    if (user == null) return;
        //    if (service == null) return;

        //    var messageFormat = "{0},\r\nWe detected a login into your account from a new device on {1}, {2} at {3}\r\n\r\nDevice: {4}\r\nLocation: {5}\r\n\r\nIf this wasn't you, you can go to Settings — Privacy and Security — Terminate all sessions.\r\n\r\nThanks, The Telegram Team";

        //    var firstName = user.FirstName;
        //    var dateTime = TLUtils.ToDateTime(update.Date);
        //    var message = string.Format(messageFormat, firstName, dateTime.ToString("dddd"), dateTime.ToString("M"), dateTime.ToString("t"), update.Device, update.Location);
        //    var serviceMessage = TLUtils.GetMessage(777000, new TLPeerUser { UserId = SettingsHelper.UserId }, TLMessageState.Confirmed, false, true, update.Date, message, new TLMessageMediaEmpty(), TLLong.Random(), null);
        //    serviceMessage.Id = 0;

        //    CacheService.SyncMessage(serviceMessage, x => { });
        //}

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
                    if (currentPosition == -1)
                    {
                        var already = Items.FirstOrDefault(x => x.Index == e.Dialog.Index);
                        if (already != null)
                        {
                            Execute.BeginOnUIThread(async () => await new MessageDialog("Something is gone really wrong and the InMemoryCacheService is messed up.", "Warning").ShowAsync());

                            e.Dialog = already;
                            currentPosition = Items.IndexOf(already);
                        }
                    }

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

        private static IList<TLDialog> ReorderDrafts(IList<TLDialog> dialogs)
        {
            return dialogs.OrderByDescending(x => x.GetDateIndexWithDraft()).ToList();

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

        #region Search

        public ObservableCollection<KeyedList<string, TLObject>> Search { get; private set; }

        public Dictionary<string, CancellationTokenSource> SearchTokens { get; private set; }

        private string _searchQuery;
        public string SearchQuery
        {
            get
            {
                return _searchQuery;
            }
            set
            {
                Set(ref _searchQuery, value);
                SearchSync(value);
            }
        }

        public async void SearchSync(string query)
        {
            query = query.TrimStart('@');

            var local = await SearchLocalAsync(query);

            if (query.Equals(_searchQuery.TrimStart('@')))
            {
                Search.Clear();
                if (local != null) Search.Insert(0, local);
            }
        }

        public async Task SearchAsync(string query)
        {
            query = query.TrimStart('@');

            var global = await SearchGlobalAsync(query);
            var messages = await SearchMessagesAsync(query);

            if (query.Equals(_searchQuery.TrimStart('@')))
            {
                if (Search.Count > 2) Search.RemoveAt(2);
                if (Search.Count > 1) Search.RemoveAt(1);
                if (global != null) Search.Add(global);
                if (messages != null) Search.Add(messages);
            }

            //SearchQuery = query;
        }

        private async Task<KeyedList<string, TLObject>> SearchLocalAsync(string query)
        {
            var dialogs = await Task.Run(() => CacheService.GetDialogs());
            var contacts = await Task.Run(() => CacheService.GetContacts());

            if (dialogs != null && contacts != null)
            {
                var simple = new List<TLDialog>();
                var parent = dialogs.Where(dialog =>
                {
                    var user = dialog.With as TLUser;
                    if (user != null)
                    {
                        return (user.FullName.Like(query, StringComparison.OrdinalIgnoreCase)) ||
                               (user.HasUsername && user.Username.StartsWith(query, StringComparison.OrdinalIgnoreCase));
                    }

                    var channel = dialog.With as TLChannel;
                    if (channel != null)
                    {
                        return (channel.Title.Like(query, StringComparison.OrdinalIgnoreCase)) ||
                               (channel.HasUsername && channel.Username.StartsWith(query, StringComparison.OrdinalIgnoreCase));
                    }

                    var chat = dialog.With as TLChat;
                    if (chat != null)
                    {
                        return (chat.Title.Like(query, StringComparison.OrdinalIgnoreCase));
                    }

                    return false;
                }).ToList();

                var contactsResults = contacts.OfType<TLUser>().Where(x =>
                    (x.FullName.Like(query, StringComparison.OrdinalIgnoreCase)) ||
                    (x.HasUsername && x.Username.StartsWith(query, StringComparison.OrdinalIgnoreCase)));

                foreach (var result in contactsResults)
                {
                    var dialog = parent.FirstOrDefault(x => x.Peer.TypeId == TLType.PeerUser && x.Index == result.Id);
                    if (dialog == null)
                    {
                        simple.Add(new TLDialog
                        {
                            With = result,
                            Peer = new TLPeerUser { UserId = result.Id }
                        });
                    }
                }

                if (parent.Count > 0 || simple.Count > 0)
                {
                    return new KeyedList<string, TLObject>(null, parent.OrderByDescending(x => x.GetDateIndexWithDraft()).Union(simple.OrderBy(x => x.With.DisplayName)));
                }
            }

            return null;
        }

        private async Task<KeyedList<string, TLObject>> SearchGlobalAsync(string query)
        {
            if (query.Length < 5)
            {
                return null;
            }

            var result = await ProtoService.SearchAsync(query, 100);
            if (result.IsSucceeded)
            {
                if (result.Result.Results.Count > 0)
                {
                    var parent = new KeyedList<string, TLObject>("Global search results");

                    CacheService.SyncUsersAndChats(result.Result.Users, result.Result.Chats,
                        tuple =>
                        {
                            result.Result.Users = tuple.Item1;
                            result.Result.Chats = tuple.Item2;

                            foreach (var peer in result.Result.Results)
                            {
                                var item = result.Result.Users.FirstOrDefault(x => x.Id == peer.Id) ?? (TLObject)result.Result.Chats.FirstOrDefault(x => x.Id == peer.Id);
                                if (item != null)
                                {
                                    parent.Add(item);
                                }
                            }
                        });

                    return parent;
                }
            }

            return null;
        }

        private async Task<KeyedList<string, TLObject>> SearchMessagesAsync(string query)
        {
            var result = await ProtoService.SearchGlobalAsync(query, 0, new TLInputPeerEmpty(), 0, 20);
            if (result.IsSucceeded)
            {
                KeyedList<string, TLObject> parent;

                var slice = result.Result as TLMessagesMessagesSlice;
                if (slice != null)
                {
                    parent = new KeyedList<string, TLObject>(string.Format("Found {0} messages", slice.Count));
                }
                else
                {
                    if (result.Result.Messages.Count > 0)
                    {
                        parent = new KeyedList<string, TLObject>(string.Format("Found {0} messages", result.Result.Messages.Count));
                    }
                    else
                    {
                        parent = new KeyedList<string, TLObject>("No messages found");
                    }
                }

                CacheService.SyncUsersAndChats(result.Result.Users, result.Result.Chats,
                    tuple =>
                    {
                        result.Result.Users = tuple.Item1;
                        result.Result.Chats = tuple.Item2;

                        foreach (var message in result.Result.Messages.OfType<TLMessageCommonBase>())
                        {
                            var peer = message.IsOut || message.ToId is TLPeerChannel || message.ToId is TLPeerChat ? message.ToId : new TLPeerUser { UserId = message.FromId.Value };
                            var with = result.Result.Users.FirstOrDefault(x => x.Id == peer.Id) ?? (ITLDialogWith)result.Result.Chats.FirstOrDefault(x => x.Id == peer.Id);
                            var item = new TLDialog
                            {
                                IsSearchResult = true,
                                TopMessage = message.Id,
                                TopMessageRandomId = message.RandomId,
                                TopMessageItem = message,
                                With = with,
                                Peer = peer
                            };

                            parent.Add(item);
                        }
                    });


                return parent;
            }

            return null;
        }

        #endregion

        #region Commands

        public RelayCommand<TLDialog> DialogPinCommand => new RelayCommand<TLDialog>(DialogPinExecute);
        private async void DialogPinExecute(TLDialog dialog)
        {
            if (Items.Where(x => x.IsPinned).Count() == PinnedDialogsCountMax && !dialog.IsPinned)
            {
                var question = new TLMessageDialog();
                question.Title = "Warning";
                question.Message = string.Format("Sorry, you can pin no more than {0} chats to the top.", PinnedDialogsCountMax);
                question.PrimaryButtonText = "OK";
                await question.ShowAsync();
                return;
            }

            var peer = dialog.ToInputPeer();

            var result = await ProtoService.ToggleDialogPinAsync(peer, !dialog.IsPinned);
            if (result.IsSucceeded)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Index == dialog.Index)
                    {
                        dialog = (Items[i] as TLDialog);
                        Items.RemoveAt(i);
                        break;
                    }
                }

                if (dialog != null)
                {
                    IsFirstPinned = dialog.IsPinned ? true : Items.Any(x => x.IsPinned);
                    PinnedDialogsIndex = 1;

                    if (dialog.IsPinned)
                    {
                        dialog.PinnedIndex = PinnedDialogsIndex++;
                    }
                    else
                    {
                        dialog.PinnedIndex = 0;
                        PinnedDialogsIndex--;
                    }

                    foreach (var cached in Items)
                    {
                        if (cached.IsPinned)
                        {
                            cached.PinnedIndex = PinnedDialogsIndex++;
                        }
                    }

                    for (int j = 0; j < Items.Count; j++)
                    {
                        if (Items[j].GetDateIndexWithDraft() <= dialog.GetDateIndexWithDraft())
                        {
                            Items.Insert(j, dialog);
                            return;
                        }
                    }
                }
            }
        }

        public RelayCommand<TLDialog> DialogDeleteCommand => new RelayCommand<TLDialog>(DialogDeleteExecute);
        private async void DialogDeleteExecute(TLDialog dialog)
        {
            var peer = dialog.ToInputPeer();
            var message = "Are you sure you want to delete the group?";
            var which = 0;

            var user = dialog.With as TLUser;
            if (user != null)
            {
                which = 1;
                message = "Are you sure you want to delete the conversation?";
            }

            var channel = dialog.With as TLChannel;
            if (channel != null)
            {
                if (channel.IsBroadcast) message = "Are you sure you want to delete the channel?";
                which = (channel.IsCreator) ? 2 : 3;
            }

            var chat = dialog.With as TLChat;
            if (chat != null)
            {
                which = 4;
            }

            var question = new TLMessageDialog();
            question.Title = "Delete";
            question.Message = message;
            question.PrimaryButtonText = "Yes";
            question.SecondaryButtonText = "No";

            var failNotification = new TLMessageDialog();
            failNotification.Title = "Error";
            failNotification.Message = "Chat could not be deleted!";
            failNotification.PrimaryButtonText = "Okay";
            failNotification.SecondaryButtonText = "";
            failNotification.IsSecondaryButtonEnabled = false;

            var result = await question.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                switch (which)
                {
                    case 1:
                        var delResultUser = await ProtoService.DeleteHistoryAsync(false, peer, 0);
                        if (delResultUser.IsSucceeded) { CacheService.DeleteDialog(dialog); }
                        else { await failNotification.ShowAsync(); return; }
                        break;
                    case 2:
                        var delResultChannel = await ProtoService.DeleteChannelAsync(channel);
                        if (delResultChannel.IsSucceeded) { CacheService.DeleteDialog(dialog); }
                        else { await failNotification.ShowAsync(); return; }
                        break;
                    case 3:
                        var leaveResultChat = await ProtoService.LeaveChannelAsync(channel);
                        if (leaveResultChat.IsSucceeded) { CacheService.DeleteDialog(dialog); }
                        else { await failNotification.ShowAsync(); return; }
                        break;
                    case 4:
                        // TODO: Make normal chats deletable
                        break;
                    default:
                        Debug.WriteLine("Dialog is nothing");
                        break;
                }

                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Index == dialog.Index)
                    {
                        dialog = (Items[i] as TLDialog);
                        Items.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public RelayCommand<TLDialog> DialogClearCommand => new RelayCommand<TLDialog>(DialogClearExecute);
        private async void DialogClearExecute(TLDialog dialog)
        {
            var clear = await TLMessageDialog.ShowAsync("Do you really want to clear the chat?", "Delete", "Yes", "No");
            if (clear == ContentDialogResult.Primary)
            {
                var peer = dialog.ToInputPeer();
                var result = await ProtoService.DeleteHistoryAsync(true, peer, 0);
                if (result.IsSucceeded)
                {
                    CacheService.ClearDialog(dialog.Peer);
                    dialog.RaisePropertyChanged(() => dialog.UnreadCount);
                }
                else
                {
                    await TLMessageDialog.ShowAsync("Clearing the chat failed!", "Error", "Okay");
                }
            }
        }

        #endregion
    }
}
