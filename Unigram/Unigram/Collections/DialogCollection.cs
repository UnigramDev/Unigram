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
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class DialogCollection : ObservableCollection<TLDialog>, ISupportIncrementalLoading,
        IHandle,
        IHandle<TopMessageUpdatedEventArgs>,
        IHandle<TLUpdateUserName>,
        IHandle<UpdateCompletedEventArgs>,
        //IHandle<UploadableItem>,
        IHandle<DownloadableItem>,
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
            InitializeAsync();
        }

        #region Handles
        public void Handle(DownloadableItem item)
        {
            //var userProfilePhoto = item.Owner as TLUserProfilePhoto;
            //if (userProfilePhoto == null)
            //{
            //    var chatPhoto = item.Owner as TLChatPhoto;
            //    if (chatPhoto != null)
            //    {
            //        var chat = _cacheService.GetChat(chatPhoto);
            //        if (chat != null)
            //        {
            //            chat.RaisePropertyChanged(() => chat.Photo);
            //            return;
            //        }

            //        var channel = _cacheService.GetChannel(chatPhoto);
            //        if (channel != null)
            //        {
            //            channel.RaisePropertyChanged(() => channel.Photo);
            //            return;
            //        }

            //        Execute.ShowDebugMessage("Handle TLChatPhoto chat=null");
            //    }

            //    return;
            //}

            //var user = _cacheService.GetUser(userProfilePhoto) as TLUser;
            //if (user != null)
            //{
            //    user.RaisePropertyChanged(() => user.Photo);
            //    return;
            //}

            //Execute.ShowDebugMessage("Handle TLUserProfilePhoto user=null");
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

        public void Handle(UpdateCompletedEventArgs args)
        {
            var dialogs = _cacheService.GetDialogs();
            Execute.BeginOnUIThread(() =>
            {
                Clear();

                foreach (var dialog in dialogs)
                {
                    Add(dialog);
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

        public void Handle(TopMessageUpdatedEventArgs message)
        {
            message.Dialog.RaisePropertyChanged(() => message.Dialog.With);

            Execute.BeginOnUIThread(() =>
            {
                //message.Dialog.TypingString = null;
                var currentPosition = IndexOf(message.Dialog);
                var index = currentPosition;
                for (int i = 0; i < Count; i++)
                {
                    if (i != currentPosition && this[i].GetDateIndex() <= message.Dialog.GetDateIndex())
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

                    if (HasMoreItems && Count > 0 && this[Count - 1].GetDateIndex() > message.Dialog.GetDateIndex())
                    {
                        Remove(message.Dialog);
                    }

                    this[currentPosition].RaisePropertyChanged(() => this[currentPosition].Self);
                    this[currentPosition].RaisePropertyChanged(() => this[currentPosition].UnreadCount);
                    return;
                }

                if (currentPosition < 0 || currentPosition >= index)
                {
                    Remove(message.Dialog);
                    Insert(index, message.Dialog);
                    return;
                }

                if (currentPosition + 1 == index)
                {
                    this[currentPosition].RaisePropertyChanged(() => this[currentPosition].Self);
                    this[currentPosition].RaisePropertyChanged(() => this[currentPosition].UnreadCount);
                    return;
                }

                Remove(message.Dialog);
                Insert(index - 1, message.Dialog);
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
                if (Count == 0)
                {
                    await InitializeAsync();
                }
                else
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
                    else
                    {
                        //HasMoreItems = false;
                        return new LoadMoreItemsResult { Count = 0 };
                    }

                    var response = await _protoService.GetDialogsAsync(lastDate, lastMsgId, lastPeer, 20);
                    if (response.IsSucceeded)
                    {
                        foreach (var item in response.Value.Dialogs)
                        {
                            Add(item);
                        }
                    }
                }
                return new LoadMoreItemsResult { Count = 20 };
            });
        }

        private async Task InitializeAsync()
        {
            if (SettingsHelper.IsAuthorized)
            {
                var dialogs = _cacheService.GetDialogs();
                var cachedDialogs = new Dictionary<int, TLDialog>();
                var clearedDialogs = new List<TLDialog>();

                foreach (var dialog in dialogs)
                {
                    if (!cachedDialogs.ContainsKey(dialog.Peer.Id))
                    {
                        clearedDialogs.Add(dialog);
                        cachedDialogs[dialog.Peer.Id] = dialog;
                    }
                    else
                    {
                        var cached = cachedDialogs[dialog.Peer.Id];
                        if (cached.Peer is TLPeerUser && dialog.Peer is TLPeerUser)
                        {
                            _cacheService.DeleteDialog(dialog);
                        }
                        else if (cached.Peer is TLPeerChat && dialog.Peer is TLPeerChat)
                        {
                            _cacheService.DeleteDialog(dialog);
                        }
                    }
                }

                await Execute.BeginOnUIThreadAsync(async () =>
                {
                    //this.Status = ((dialogs.get_Count() == 0) ? AppResources.Loading : string.Empty);
                    Clear();

                    for (int i = 0; i < clearedDialogs.Count && i < 8; i++)
                    {
                        Add(clearedDialogs[i]);
                    }

                    if (clearedDialogs.Count > 8)
                    {
                        await Execute.BeginOnUIThreadAsync(async () =>
                        {
                            for (int i = 8; i < clearedDialogs.Count; i++)
                            {
                                Add(clearedDialogs[i]);
                            }

                            await UpdateItemsAsync(0, 0, 20);
                        });

                        return;
                    }

                    await UpdateItemsAsync(0, 0, 20);
                });
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
                var result = response.Value;
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
