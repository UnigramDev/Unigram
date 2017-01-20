using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Logs;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class DialogCollection : ObservableCollection<TLDialog>, ISupportIncrementalLoading,
        IHandle,
        IHandle<TopMessageUpdatedEventArgs>,
        IHandle<TLUpdateUserName>,
        IHandle<UpdateCompletedEventArgs>,
        IHandle<DialogAddedEventArgs>,
        IHandle<DialogRemovedEventArgs>,
        IHandle<TLUpdateEditMessage>,
        IHandle<TLUpdateEditChannelMessage>,
        IHandle<TLUpdateDraftMessage>
    //IHandle<TLUpdateUserTyping>,
    //IHandle<TLUpdateChatUserTyping>
    {
        private readonly IMTProtoService _protoService;
        private readonly ICacheService _cacheService;

        public DialogCollection(IMTProtoService protoService, ICacheService cacheService)
        {
            _protoService = protoService;
            _cacheService = cacheService;

            HasMoreItems = false;
            LoadMoreItemsAsync(20);
            //InitializeAsync();
        }

        #region Handles

        public void Handle(UpdateCompletedEventArgs args)
        {
            var dialogs = _cacheService.GetDialogs();
            ReorderDrafts(dialogs);
            Execute.BeginOnUIThread(() =>
            {
                Clear();

                foreach (var dialog in dialogs)
                {
                    Add(dialog);
                }
            });
        }

        public void Handle(TopMessageUpdatedEventArgs e)
        {
            e.Dialog.RaisePropertyChanged(() => e.Dialog.With);

            Execute.BeginOnUIThread(() =>
            {
                try
                {
                    var chat = e.Dialog.With as TLChat;
                    if (chat != null)
                    {
                        var dialog = e.Dialog as TLDialog;
                        if (dialog != null)
                        {
                            var serviceMessage = dialog.TopMessageItem as TLMessageService;
                            if (serviceMessage != null)
                            {
                                var migrateAction = serviceMessage.Action as TLMessageActionChatMigrateTo;
                                if (migrateAction != null)
                                {
                                    Remove(e.Dialog);
                                    return;
                                }
                            }
                        }
                    }

                    var channel = e.Dialog.With as TLChannel;
                    if (channel != null)
                    {
                        var dialog = e.Dialog as TLDialog;
                        if (dialog != null)
                        {
                            var messageService = dialog.TopMessageItem as TLMessageService;
                            if (messageService != null)
                            {
                                var deleteUserAction = messageService.Action as TLMessageActionChatDeleteUser;
                                if (deleteUserAction != null && deleteUserAction.UserId == SettingsHelper.UserId)
                                {
                                    Remove(e.Dialog);
                                    return;
                                }
                            }
                        }
                    }

                    // TODO: e.Dialog.TypingString = null;

                    var currentPosition = IndexOf(e.Dialog);
                    if (currentPosition < 0)
                    {
                        var already = this.FirstOrDefault(x => x.Index == e.Dialog.Index);
                        if (already != null)
                        {
                            currentPosition = IndexOf(already);
                        }
                    }

                    var index = currentPosition;

                    for (int i = 0; i < Count; i++)
                    {
                        if (i != currentPosition && this[i].GetDateIndexWithDraft() <= e.Dialog.GetDateIndexWithDraft())
                        {
                            index = i;
                            break;
                        }
                    }

                    if (currentPosition == -1 && currentPosition == index)
                    {
                        // TODO
                        //Telegram.Api.Helpers.Execute.ShowDebugMessage(string.Concat(new object[]
                        //{
                        //    "TLDialog with=",
                        //    e.Dialog.With,
                        //    " curPos=newPos=-1 isLastSliceLoaded=",
                        //    this.IsLastSliceLoaded
                        //}));
                        //if (!this.IsLastSliceLoaded)
                        //{
                        //    return;
                        //}
                        //Add(e.Dialog);
                    }

                    if (currentPosition != index)
                    {
                        if (currentPosition >= 0 && currentPosition < index)
                        {
                            if (currentPosition + 1 == index)
                            {
                                this[currentPosition].RaisePropertyChanged(() => this[currentPosition].Self);
                                this[currentPosition].RaisePropertyChanged(() => this[currentPosition].UnreadCount);
                            }
                            else
                            {
                                Remove(e.Dialog);
                                Insert(index - 1, e.Dialog);
                            }
                        }
                        else
                        {
                            Remove(e.Dialog);
                            Insert(index, e.Dialog);
                        }
                    }
                    else
                    {
                        if (/*!this.IsLastSliceLoaded &&*/ Count > 0 && this[Count - 1].GetDateIndexWithDraft() > e.Dialog.GetDateIndexWithDraft())
                        {
                            Remove(e.Dialog);
                        }

                        this[currentPosition].RaisePropertyChanged(() => this[currentPosition].Self);
                        this[currentPosition].RaisePropertyChanged(() => this[currentPosition].UnreadCount);
                    }
                }
                catch (Exception ex)
                {
                    Log.Write(string.Format("DialogsViewModel.Handle OnTopMessageUpdatedEventArgs ex " + ex, new object[0]), null);
                    throw ex;
                }
            });
        }

        public void Handle(TLUpdateEditMessage update)
        {
            var message = update.Message as TLMessage;
            if (message == null)
            {
                return;
            }

            Execute.BeginOnUIThread(() =>
            {
                int peerId;
                if (message.ToId is TLPeerUser)
                {
                    peerId = message.IsOut ? message.ToId.Id : message.FromId.Value;
                }
                else
                {
                    peerId = message.ToId.Id;
                }

                for (int i = 0; i < Count; i++)
                {
                    if (this[i].Peer.Id == peerId && this[i].TopMessage == message.Id)
                    {
                        this[i].RaisePropertyChanged(() => this[i].Self);
                    }
                }
            });
        }

        public void Handle(TLUpdateEditChannelMessage update)
        {
            var message = update.Message as TLMessage;
            if (message == null)
            {
                return;
            }

            Execute.BeginOnUIThread(() =>
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i].Peer.Id == message.ToId.Id && this[i].TopMessage == message.Id)
                    {
                        this[i].RaisePropertyChanged(() => this[i].Self);
                    }
                }
            });
        }

        public void Handle(TLUpdateDraftMessage update)
        {
            Execute.BeginOnUIThread(() =>
            {
                TLDialog dialog = null;
                for (int i = 0; i < Count; i++)
                {
                    if (this[i].Peer.Id == update.Peer.Id)
                    {
                        dialog = this[i];
                        if (dialog != null)
                        {
                            dialog.Draft = update.Draft;
                            dialog.RaisePropertyChanged(() => dialog.Draft);
                            dialog.RaisePropertyChanged(() => dialog.Self);
                        }
                        RemoveAt(i);
                        break;
                    }
                }

                if (dialog != null)
                {
                    for (int j = 0; j < Count; j++)
                    {
                        if (this[j].GetDateIndexWithDraft() <= dialog.GetDateIndexWithDraft())
                        {
                            Insert(j, dialog);
                            return;
                        }
                    }
                }
            });
        }

        public void Handle(DialogAddedEventArgs args)
        {
            if (args.Dialog == null) return;

            Execute.BeginOnUIThread(() =>
            {
                var index = -1;
                for (int i = 0; i < Count; i++)
                {
                    if (this[i] == args.Dialog)
                    {
                        return;
                    }

                    if (this[i].GetDateIndex() < args.Dialog.GetDateIndex())
                    {
                        index = i;
                        break;
                    }
                }

                if (index == -1)
                {
                    Add(args.Dialog);
                }
                else
                {
                    Insert(index, args.Dialog);
                }

                //this.Status = ((this.Items.get_Count() == 0 || this.LazyItems.get_Count() == 0) ? string.Empty : this.Status);
            });
        }

        public void Handle(DialogRemovedEventArgs args)
        {
            Execute.BeginOnUIThread(() =>
            {
                var dialog = this.FirstOrDefault(x => x.Peer.Id == args.Dialog.Peer.Id);
                if (dialog != null)
                {
                    Remove(dialog);
                }
            });
        }

        public void Handle(TLUpdateUserName userName)
        {
            Execute.BeginOnUIThread(() =>
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i].WithId == userName.UserId && this[i].With is TLUser)
                    {
                        var userBase = (TLUser)this[i].With;
                        userBase.FirstName = userName.FirstName;
                        userBase.LastName = userName.LastName;
                        userBase.Username = userName.Username;

                        this[i].RaisePropertyChanged(() => this[i].With);
                        return;
                    }
                }
            });
        }
        #endregion

        public bool HasMoreItems { get; private set; } = true;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async (token) =>
            {
                var lastDate = 0;
                var lastMsgId = 0;
                var lastPeer = (TLInputPeerBase)new TLInputPeerEmpty();

                var last = this.LastOrDefault();
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

                var response = await _protoService.GetDialogsAsync(lastDate, lastMsgId, lastPeer, 200);
                if (response.IsSucceeded)
                {
                    foreach (var item in response.Result.Dialogs)
                    {
                        Add(item);
                    }

                    return new LoadMoreItemsResult { Count = (uint)response.Result.Dialogs.Count };
                }

                return new LoadMoreItemsResult { Count = 20 };
            });
        }

        private async Task InitializeAsync()
        {
            if (SettingsHelper.IsAuthorized)
            {
                var dialogs = _cacheService.GetDialogs();
                if (dialogs.Count > 0)
                {
                    var dictionary = new Dictionary<int, TLDialog>();
                    var clearedDialogs = new List<TLDialog>();
                    foreach (var current in dialogs)
                    {
                        if (!dictionary.ContainsKey(current.Index))
                        {
                            clearedDialogs.Add(current);
                            dictionary[current.Index] = current;
                        }
                        else
                        {
                            var tLDialogBase = dictionary[current.Index];
                            if (tLDialogBase.Peer is TLPeerUser && current.Peer is TLPeerUser)
                            {
                                _cacheService.DeleteDialog(current);
                            }
                            else if (tLDialogBase.Peer is TLPeerChat && current.Peer is TLPeerChat)
                            {
                                _cacheService.DeleteDialog(current);
                            }
                            else if (tLDialogBase.Peer is TLPeerChannel && current.Peer is TLPeerChannel)
                            {
                                _cacheService.DeleteDialog(current);
                            }
                        }
                    }

                    ReorderDrafts(clearedDialogs);

                    Execute.BeginOnUIThread(() =>
                    {
                    //this.Status = ((dialogs.get_Count() == 0) ? AppResources.Loading : string.Empty);

                    Clear();

                        int num = 0;
                        int count = 0;
                        int num2 = 0;
                        while (num2 < clearedDialogs.Count && num < 8)
                        {
                            Add(clearedDialogs[num2]);

                            var chat = clearedDialogs[num2].With as TLChat;
                            if (chat == null || !chat.HasMigratedTo)
                            {
                                num++;
                            }

                            num2++;
                            count++;
                        }
                        if (count < clearedDialogs.Count)
                        {
                            Execute.BeginOnUIThread(delegate
                            {
                                for (int i = count; i < clearedDialogs.Count; i++)
                                {
                                    this.Items.Add(clearedDialogs[i]);
                                }

                                UpdateItemsAsync(Math.Max(20, this.OfType<TLDialog>().Count()));
                            });
                            return;
                        }

                        UpdateItemsAsync(Math.Max(20, this.OfType<TLDialog>().Count()));
                    });
                }
                else
                {
                    var response = await _protoService.GetDialogsAsync(0, 0, new TLInputPeerEmpty(), 20);
                    var result = response.Result;
                    result.Dialogs = new TLVector<TLDialog>(result.Dialogs.OrderByDescending(x => x.GetDateIndexWithDraft()));

                    foreach (var dialog in result.Dialogs)
                    {
                        Add(dialog);
                    }
                }

                //var dialogs = _cacheService.GetDialogs();
                //var cachedDialogs = new Dictionary<int, TLDialog>();
                //var clearedDialogs = new List<TLDialog>();

                //foreach (var dialog in dialogs)
                //{
                //    if (!cachedDialogs.ContainsKey(dialog.Peer.Id))
                //    {
                //        clearedDialogs.Add(dialog);
                //        cachedDialogs[dialog.Peer.Id] = dialog;
                //    }
                //    else
                //    {
                //        var cached = cachedDialogs[dialog.Peer.Id];
                //        if (cached.Peer is TLPeerUser && dialog.Peer is TLPeerUser)
                //        {
                //            _cacheService.DeleteDialog(dialog);
                //        }
                //        else if (cached.Peer is TLPeerChat && dialog.Peer is TLPeerChat)
                //        {
                //            _cacheService.DeleteDialog(dialog);
                //        }
                //    }
                //}

                //await Execute.BeginOnUIThreadAsync(async () =>
                //{
                //    //this.Status = ((dialogs.get_Count() == 0) ? AppResources.Loading : string.Empty);
                //    Clear();

                //    for (int i = 0; i < clearedDialogs.Count && i < 8; i++)
                //    {
                //        Add(clearedDialogs[i]);
                //    }

                //    if (clearedDialogs.Count > 8)
                //    {
                //        await Execute.BeginOnUIThreadAsync(async () =>
                //        {
                //            for (int i = 8; i < clearedDialogs.Count; i++)
                //            {
                //                Add(clearedDialogs[i]);
                //            }

                //            await UpdateItemsAsync(0, 0, 20);
                //        });

                //        return;
                //    }

                //    await UpdateItemsAsync(0, 0, 20);
                //});
            }
        }

        private async void UpdateItemsAsync(int limit)
        {
            //base.IsWorking = true;

            var response = await _protoService.GetDialogsAsync(0, 0, new TLInputPeerEmpty(), limit);
            var result = response.Result;
            result.Dialogs = new TLVector<TLDialog>(result.Dialogs.OrderByDescending(x => x.GetDateIndexWithDraft()));

            Execute.BeginOnUIThread(() =>
            {
                //this.IsWorking = false;
                //this.IsLastSliceLoaded = (result.Dialogs.Count < limit);
                //this._offset = limit;
                bool flag = false;
                int count = Count;
                int num = 0;
                int num2 = 0;
                while (num < result.Dialogs.Count && num2 < Count)
                {
                    if (count - 1 < num || result.Dialogs[num] != this[num2])
                    {
                        var dialog = this[num2] as TLDialog;
                        if (dialog != null)
                        {
                            var messageService = dialog.TopMessageItem as TLMessageService;
                            //if (messageService != null && messageService.Action is TLMessageActionContactRegistered)
                            //{
                            //    num--;
                            //    goto IL_11D;
                            //}
                        }

                        //var encryptedDialog = this[num2] as TLEncryptedDialog;
                        //if (encryptedDialog == null)
                        //{
                        //    flag = true;
                        //    break;
                        //}
                        num--;
                    }
                    IL_11D:
                    num++;
                    num2++;
                }
                if (num < num2)
                {
                    for (int i = num; i < num2; i++)
                    {
                        if (i < result.Dialogs.Count)
                        {
                            this.Items.Add(result.Dialogs[i]);
                        }
                    }
                }
                //this.Status = ((this.Items.get_Count() == 0 && result.Dialogs.Count == 0) ? string.Format("{0}", AppResources.NoDialogsHere) : string.Empty);
                if (flag)
                {
                    //IEnumerable<TLEncryptedDialog> enumerable = Enumerable.OfType<TLEncryptedDialog>(this.Items);
                    //int num3 = 0;
                    //using (IEnumerator<TLEncryptedDialog> enumerator2 = enumerable.GetEnumerator())
                    //{
                    //    while (enumerator2.MoveNext())
                    //    {
                    //        TLEncryptedDialog current2 = enumerator2.get_Current();
                    //        for (int j = num3; j < result.Dialogs.Count; j++)
                    //        {
                    //            if (current2.GetDateIndexWithDraft() > result.Dialogs[j].GetDateIndexWithDraft())
                    //            {
                    //                result.Dialogs.Insert(j, current2);
                    //                num3 = j;
                    //                break;
                    //            }
                    //        }
                    //    }
                    //}
                    //IEnumerable<TLBroadcastDialog> enumerable2 = Enumerable.OfType<TLBroadcastDialog>(this.Items);
                    //num3 = 0;
                    //using (IEnumerator<TLBroadcastDialog> enumerator3 = enumerable2.GetEnumerator())
                    //{
                    //    while (enumerator3.MoveNext())
                    //    {
                    //        TLBroadcastDialog current3 = enumerator3.get_Current();
                    //        for (int k = num3; k < result.Dialogs.Count; k++)
                    //        {
                    //            if (current3.GetDateIndexWithDraft() > result.Dialogs[k].GetDateIndexWithDraft())
                    //            {
                    //                result.Dialogs.Insert(k, current3);
                    //                num3 = k;
                    //                break;
                    //            }
                    //        }
                    //    }
                    //}

                    Clear();

                    foreach (var dialog in result.Dialogs)
                    {
                        Add(dialog);
                    }

                    //this.IsLastSliceLoaded = false;
                    //this._isUpdated = true;
                    return;
                }
                //this._isUpdated = true;
            });
            //}, delegate (TLRPCError error)
            //{
            //    base.BeginOnUIThread(delegate
            //    {
            //        this._isUpdated = true;
            //        base.Status = string.Empty;
            //        base.IsWorking = false;
            //    });
            //});
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

        private async Task UpdateItemsAsync(int offset, int maxId, int count, bool forceClear = true)
        {
            //var lastDate = 0;
            //var lastMsgId = 0;
            //var lastPeer = (TLInputPeerBase)new TLInputPeerEmpty();

            //var last = this.LastOrDefault();
            //if (last != null)
            //{
            //    lastDate = last.TopMessageItem.Date;
            //    lastMsgId = last.TopMessage;

            //    if (last.Peer is TLPeerUser)
            //    {
            //        lastPeer = new TLInputPeerUser { UserId = last.Peer.Id };
            //    }
            //    else if (last.Peer is TLPeerChat)
            //    {
            //        lastPeer = new TLInputPeerChat { ChatId = last.Peer.Id };
            //    }
            //    else if (last.Peer is TLPeerChannel)
            //    {
            //        lastPeer = new TLInputPeerChannel { ChannelId = last.Peer.Id };
            //    }
            //}

            var response = await _protoService.GetDialogsAsync(0, 0, new TLInputPeerEmpty(), count);
            if (response.IsSucceeded)
            {
                var result = response.Result;
                var vector = new TLVector<TLDialog>(result.Dialogs.Count);
                foreach (var dialog in result.Dialogs.OrderBy(x => x.GetDateIndex()))
                {
                    vector.Add(dialog);
                }

                //if (!forceClear) forceClear = Count == 0;

                result.Dialogs = vector;
                await Execute.BeginOnUIThreadAsync(() =>
                {
                    var needUpdate = false;
                    var itemsCount = Count;
                    var i = 0;
                    var k = 0;
                    while (i < result.Dialogs.Count && k < Count)
                    {
                        if (itemsCount - 1 < i || result.Dialogs[i] != this[k])
                        {
                            var dialog = this[k] as TLDialog;
                            if (dialog != null)
                            {
                                var serviceMessage = dialog.TopMessageItem as TLMessageService;
                                //if (serviceMessage != null && serviceMessage.Action is TLMessageActionContactRegistered)
                                //{
                                //    i--;
                                //    goto IL_AE;
                                //}
                            }

                            //var encryptedDialog = this[k] as TLEncryptedDialog;
                            //if (encryptedDialog == null)
                            //{
                            //    needUpdate = true;
                            //    break;
                            //}
                            i--;
                        }

                        IL_AE:
                        i++;
                        k++;
                    }
                    //this.Status = ((this.Items.get_Count() == 0 && result.Dialogs.Count == 0) ? string.Format("{0}", AppResources.NoDialogsHere) : string.Empty);
                    if (needUpdate || forceClear)
                    {
                        //var encryptedDialogs = this.OfType<TLEncryptedDialog>();
                        var startIndex = 0;

                        //foreach (var encryptedDialog in encryptedDialogs)
                        //{
                        //    for (int j = startIndex; j < result.Dialogs.Count; j++)
                        //    {
                        //        if (encryptedDialog.GetDateIndex() > result.Dialogs[j].GetDateIndex())
                        //        {
                        //            result.Dialogs.Insert(j, encryptedDialog);
                        //            startIndex = j;
                        //            break;
                        //        }
                        //    }
                        //}

                        //var broadcastDialogs = Enumerable.OfType<TLBroadcastDialog>(this);
                        startIndex = 0;

                        //foreach (var broadcastDialog in broadcastDialogs)
                        //{
                        //    for (int j = startIndex; j < result.Dialogs.Count; j++)
                        //    {
                        //        if (broadcastDialog.GetDateIndex() > result.Dialogs[j].GetDateIndex())
                        //        {
                        //            result.Dialogs.Insert(j, broadcastDialog);
                        //            startIndex = j;
                        //            break;
                        //        }
                        //    }
                        //}

                        if (forceClear)
                        {
                            Clear();
                        }

                        foreach (var dialog in result.Dialogs)
                        {
                            if (forceClear)
                            {
                                Insert(0, dialog);
                            }
                            else
                            {
                                MoveOrInsert(dialog);
                            }
                        }

                        //HasMoreItems = Count > 0;
                    }
                    else
                    {
                        //HasMoreItems = Count > 0;
                    }
                    //this.IsWorking = false;
                });
            }
            //}, delegate (TLRPCError error)
            //{
            //    Execute.BeginOnUIThread(delegate
            //    {
            //        HasMoreItems = Count > 0;
            //    });
            //});
        }

        private void MoveOrInsert(TLDialog dialog)
        {
            var currentPosition = IndexOf(dialog);
            var index = currentPosition;
            for (int i = 0; i < Count; i++)
            {
                if (i != currentPosition && this[i].GetDateIndex() <= dialog.GetDateIndex())
                {
                    index = i;
                    break;
                }
            }

            if (currentPosition == index)
            {
                if (currentPosition == -1)
                {
                    return;
                }

                if (HasMoreItems && Count > 0 && this[Count - 1].GetDateIndex() > dialog.GetDateIndex())
                {
                    Remove(dialog);
                }

                this[currentPosition].RaisePropertyChanged(() => this[currentPosition].Self);
                this[currentPosition].RaisePropertyChanged(() => this[currentPosition].UnreadCount);
                return;
            }

            if (currentPosition < 0 || currentPosition >= index)
            {
                Remove(dialog);
                Insert(index, dialog);
                return;
            }

            if (currentPosition + 1 == index)
            {
                this[currentPosition].RaisePropertyChanged(() => this[currentPosition].Self);
                this[currentPosition].RaisePropertyChanged(() => this[currentPosition].UnreadCount);
                return;
            }

            Remove(dialog);
            Insert(index - 1, dialog);
        }
    }
}
