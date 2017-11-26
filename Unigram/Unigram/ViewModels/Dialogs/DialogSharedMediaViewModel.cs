using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Helpers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Dialogs
{
    public class DialogSharedMediaViewModel : UnigramViewModelBase
    {
        public DialogSharedMediaViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            MessagesForwardCommand = new RelayCommand(MessagesForwardExecute, () => SelectedItems.Count > 0 && SelectedItems.All(x => x is TLMessage));
            MessageViewCommand = new RelayCommand<TLMessageBase>(MessageViewExecute);
            MessageSaveCommand = new RelayCommand<TLMessageBase>(MessageSaveExecute);
            MessageDeleteCommand = new RelayCommand<TLMessageBase>(MessageDeleteExecute);
            MessageForwardCommand = new RelayCommand<TLMessageBase>(MessageForwardExecute);
            MessageSelectCommand = new RelayCommand<TLMessageBase>(MessageSelectExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Peer = (TLInputPeerBase)parameter;
            With = Peer is TLInputPeerUser ? (ITLDialogWith)CacheService.GetUser(Peer.ToPeer().Id) : CacheService.GetChat(Peer.ToPeer().Id);

            Media = new MediaCollection(ProtoService, _peer, new TLInputMessagesFilterPhotoVideo());
            Files = new MediaCollection(ProtoService, _peer, new TLInputMessagesFilterDocument());
            Links = new MediaCollection(ProtoService, _peer, new TLInputMessagesFilterUrl());
            Music = new MediaCollection(ProtoService, _peer, new TLInputMessagesFilterMusic());

            RaisePropertyChanged(() => Media);
            RaisePropertyChanged(() => Files);
            RaisePropertyChanged(() => Links);
            RaisePropertyChanged(() => Music);

            return Task.CompletedTask;
        }

        private ITLDialogWith _with;
        public ITLDialogWith With
        {
            get
            {
                return _with;
            }
            set
            {
                Set(ref _with, value);
            }
        }

        private TLInputPeerBase _peer;
        public TLInputPeerBase Peer
        {
            get
            {
                return _peer;
            }
            set
            {
                Set(ref _peer, value);
            }
        }

        public MediaCollection Media { get; private set; }
        public MediaCollection Files { get; private set; }
        public MediaCollection Links { get; private set; }
        public MediaCollection Music { get; private set; }

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.None;
        public ListViewSelectionMode SelectionMode
        {
            get
            {
                return _selectionMode;
            }
            set
            {
                Set(ref _selectionMode, value);
            }
        }

        private List<TLMessageCommonBase> _selectedItems = new List<TLMessageCommonBase>();
        public List<TLMessageCommonBase> SelectedItems
        {
            get
            {
                return _selectedItems;
            }
            set
            {
                Set(ref _selectedItems, value);
                MessagesForwardCommand.RaiseCanExecuteChanged();
                MessagesDeleteCommand.RaiseCanExecuteChanged();
            }
        }

        #region View

        public RelayCommand<TLMessageBase> MessageViewCommand { get; }
        private void MessageViewExecute(TLMessageBase messageBase)
        {
            NavigationService.NavigateToDialog(_with, messageBase.Id);
        }

        #endregion

        #region Save

        public RelayCommand<TLMessageBase> MessageSaveCommand { get; }
        private async void MessageSaveExecute(TLMessageBase messageBase)
        {
            var photo = messageBase.GetPhoto();
            if (photo?.Full is TLPhotoSize photoSize)
            {
                await TLFileHelper.SavePhotoAsync(photoSize, messageBase.Date, false);
            }

            var document = messageBase.GetDocument();
            if (document != null)
            {
                await TLFileHelper.SaveDocumentAsync(document, messageBase.Date, false);
            }
        }

        #endregion

        #region Delete

        public RelayCommand<TLMessageBase> MessageDeleteCommand { get; }
        private async void MessageDeleteExecute(TLMessageBase messageBase)
        {
            if (messageBase == null) return;

            var message = messageBase as TLMessage;
            if (message != null && !message.IsOut && !message.IsPost && Peer is TLInputPeerChannel)
            {
                var dialog = new DeleteChannelMessageDialog(1, message.From?.FullName);

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var channel = With as TLChannel;

                    if (dialog.DeleteAll)
                    {
                        // TODO
                    }
                    else
                    {
                        var messages = new List<TLMessageBase>() { messageBase };
                        if (messageBase.Id == 0 && messageBase.RandomId != 0L)
                        {
                            DeleteMessagesInternal(null, messages);
                            return;
                        }

                        DeleteMessages(null, null, messages, true, null, DeleteMessagesInternal);
                    }

                    if (dialog.BanUser)
                    {
                        // TODO: layer 68
                        //var response = await ProtoService.KickFromChannelAsync(channel, message.From.ToInputUser(), true);
                        //if (response.IsSucceeded)
                        //{
                        //    var updates = response.Result as TLUpdates;
                        //    if (updates != null)
                        //    {
                        //        var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
                        //        if (newChannelMessageUpdate != null)
                        //        {
                        //            Aggregator.Publish(newChannelMessageUpdate.Message);
                        //        }
                        //    }
                        //}
                    }

                    if (dialog.ReportSpam)
                    {
                        var response = await ProtoService.ReportSpamAsync(channel.ToInputChannel(), message.From.ToInputUser(), new TLVector<int> { message.Id });
                    }
                }
            }
            else
            {
                var dialog = new TLMessageDialog();
                dialog.Title = "Delete";
                dialog.Message = "Do you want to delete this message?";
                dialog.PrimaryButtonText = "Yes";
                dialog.SecondaryButtonText = "No";

                var chat = With as TLChat;

                if (message != null && (message.IsOut || (chat != null && (chat.IsCreator || chat.IsAdmin))) && message.ToId.Id != SettingsHelper.UserId && (Peer is TLInputPeerUser || Peer is TLInputPeerChat))
                {
                    var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                    var config = CacheService.GetConfig();
                    if (config != null && message.Date + config.EditTimeLimit > date)
                    {
                        if (With is TLUser user)
                        {
                            dialog.CheckBoxLabel = string.Format("Delete for {0}", user.FullName);
                        }

                        //var chat = With as TLChat;
                        if (chat != null)
                        {
                            dialog.CheckBoxLabel = "Delete for everyone";
                        }
                    }
                }
                else if (Peer is TLInputPeerUser)
                {
                    dialog.Message += "\r\n\r\nThis will delete it just for you.";
                }
                else if (Peer is TLInputPeerChat)
                {
                    dialog.Message += "\r\n\r\nThis will delete it just for you, not for other participants of the chat.";
                }
                else if (Peer is TLInputPeerChannel)
                {
                    dialog.Message += "\r\n\r\nThis will delete it for everyone in this chat.";
                }

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var revoke = dialog.IsChecked == true;

                    var messages = new List<TLMessageBase>() { messageBase };
                    if (messageBase.Id == 0 && messageBase.RandomId != 0L)
                    {
                        DeleteMessagesInternal(null, messages);
                        return;
                    }

                    DeleteMessages(null, null, messages, revoke, null, DeleteMessagesInternal);
                }
            }
        }

        private void DeleteMessagesInternal(TLMessageBase lastMessage, IList<TLMessageBase> messages)
        {
            var cachedMessages = new TLVector<long>();
            var remoteMessages = new TLVector<int>();
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].RandomId.HasValue && messages[i].RandomId != 0L)
                {
                    cachedMessages.Add(messages[i].RandomId.Value);
                }
                if (messages[i].Id > 0)
                {
                    remoteMessages.Add(messages[i].Id);
                }
            }

            CacheService.DeleteMessages(Peer.ToPeer(), lastMessage, remoteMessages);
            CacheService.DeleteMessages(cachedMessages);

            var dialog = CacheService.GetDialog(_peer.ToPeer());
            if (dialog != null)
            {
                Aggregator.Publish(new MessagesRemovedEventArgs(dialog, messages));
            }

            BeginOnUIThread(() =>
            {
                for (int j = 0; j < messages.Count; j++)
                {
                    Media.All(x => x.Remove(messages[j] as TLMessage));
                    Files.All(x => x.Remove(messages[j] as TLMessage));
                    Links.All(x => x.Remove(messages[j] as TLMessage));
                    Music.All(x => x.Remove(messages[j] as TLMessage));
                }

                RaisePropertyChanged(() => With);
                SelectionMode = ListViewSelectionMode.None;

                //this.IsEmptyDialog = (this.Items.get_Count() == 0 && this.LazyItems.get_Count() == 0);
                //this.NotifyOfPropertyChange<TLObject>(() => this.With);
            });
        }

        public async void DeleteMessages(TLMessageBase lastItem, IList<TLMessageBase> localMessages, IList<TLMessageBase> remoteMessages, bool revoke, Action<TLMessageBase, IList<TLMessageBase>> localCallback = null, Action<TLMessageBase, IList<TLMessageBase>> remoteCallback = null)
        {
            if (localMessages != null && localMessages.Count > 0)
            {
                localCallback?.Invoke(lastItem, localMessages);
            }
            if (remoteMessages != null && remoteMessages.Count > 0)
            {
                var messages = new TLVector<int>(remoteMessages.Select(x => x.Id).ToList());

                Task<MTProtoResponse<TLMessagesAffectedMessages>> task;

                if (Peer is TLInputPeerChannel)
                {
                    task = ProtoService.DeleteMessagesAsync(new TLInputChannel { ChannelId = ((TLInputPeerChannel)Peer).ChannelId, AccessHash = ((TLInputPeerChannel)Peer).AccessHash }, messages);
                }
                else
                {
                    task = ProtoService.DeleteMessagesAsync(messages, revoke);
                }

                var response = await task;
                if (response.IsSucceeded)
                {
                    remoteCallback?.Invoke(lastItem, remoteMessages);
                }
            }
        }

        #endregion

        #region Forward

        public RelayCommand<TLMessageBase> MessageForwardCommand { get; }
        private async void MessageForwardExecute(TLMessageBase messageBase)
        {
            if (messageBase is TLMessage message)
            {
                SelectionMode = ListViewSelectionMode.None;

                await ShareView.Current.ShowAsync(message);
            }
        }

        #endregion

        #region Multiple Delete

        private RelayCommand _messagesDeleteCommand;
        public RelayCommand MessagesDeleteCommand => _messagesDeleteCommand = (_messagesDeleteCommand ?? new RelayCommand(MessagesDeleteExecute, () => SelectedItems.Count > 0 && SelectedItems.All(messageCommon =>
        {
            if (_with is TLChannel channel)
            {
                if (messageCommon.Id == 1 && messageCommon.ToId is TLPeerChannel)
                {
                    return false;
                }

                if (!messageCommon.IsOut && !channel.IsCreator && !channel.HasAdminRights || (channel.AdminRights != null && !channel.AdminRights.IsDeleteMessages))
                {
                    return false;
                }
            }

            return true;
        })));

        private async void MessagesDeleteExecute()
        {
            //if (messageBase == null) return;

            //var message = messageBase as TLMessage;
            //if (message != null && !message.IsOut && !message.IsPost && Peer is TLInputPeerChannel)
            //{
            //    var dialog = new DeleteChannelMessageDialog();

            //    var result = await dialog.ShowAsync();
            //    if (result == ContentDialogResult.Primary)
            //    {
            //        var channel = With as TLChannel;

            //        if (dialog.DeleteAll)
            //        {
            //            // TODO
            //        }
            //        else
            //        {
            //            var messages = new List<TLMessageBase>() { messageBase };
            //            if (messageBase.Id == 0 && messageBase.RandomId != 0L)
            //            {
            //                DeleteMessagesInternal(null, messages);
            //                return;
            //            }

            //            DeleteMessages(null, null, messages, true, null, DeleteMessagesInternal);
            //        }

            //        if (dialog.BanUser)
            //        {
            //            var response = await ProtoService.KickFromChannelAsync(channel, message.From.ToInputUser(), true);
            //            if (response.IsSucceeded)
            //            {
            //                var updates = response.Result as TLUpdates;
            //                if (updates != null)
            //                {
            //                    var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
            //                    if (newChannelMessageUpdate != null)
            //                    {
            //                        Aggregator.Publish(newChannelMessageUpdate.Message);
            //                    }
            //                }
            //            }
            //        }

            //        if (dialog.ReportSpam)
            //        {
            //            var response = await ProtoService.ReportSpamAsync(channel.ToInputChannel(), message.From.ToInputUser(), new TLVector<int> { message.Id });
            //        }
            //    }
            //}
            //else
            {
                var messages = new List<TLMessageCommonBase>(SelectedItems);

                var dialog = new TLMessageDialog();
                dialog.Title = "Delete";
                dialog.Message = messages.Count > 1 ? string.Format("Do you want to delete this {0} messages?", messages.Count) : "Do you want to delete this message?";
                dialog.PrimaryButtonText = "Yes";
                dialog.SecondaryButtonText = "No";

                var chat = With as TLChat;

                var isOut = messages.All(x => x.IsOut);
                var toId = messages.FirstOrDefault().ToId;
                var minDate = messages.OrderBy(x => x.Date).FirstOrDefault().Date;
                var maxDate = messages.OrderByDescending(x => x.Date).FirstOrDefault().Date;

                if ((isOut || (chat != null && (chat.IsCreator || chat.IsAdmin))) && toId.Id != SettingsHelper.UserId && (Peer is TLInputPeerUser || Peer is TLInputPeerChat))
                {
                    var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                    var config = CacheService.GetConfig();
                    if (config != null && minDate + config.EditTimeLimit > date && maxDate + config.EditTimeLimit > date)
                    {
                        if (With is TLUser user)
                        {
                            dialog.CheckBoxLabel = string.Format("Delete for {0}", user.FullName);
                        }

                        //var chat = With as TLChat;
                        if (chat != null)
                        {
                            dialog.CheckBoxLabel = "Delete for everyone";
                        }
                    }
                }
                else if (Peer is TLInputPeerUser)
                {
                    dialog.Message += "\r\n\r\nThis will delete it just for you.";
                }
                else if (Peer is TLInputPeerChat)
                {
                    dialog.Message += "\r\n\r\nThis will delete it just for you, not for other participants of the chat.";
                }
                else if (Peer is TLInputPeerChannel)
                {
                    dialog.Message += "\r\n\r\nThis will delete it for everyone in this chat.";
                }

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var revoke = dialog.IsChecked == true;

                    var localMessages = new List<TLMessageBase>();
                    var remoteMessages = new List<TLMessageBase>();
                    for (int i = 0; i < messages.Count; i++)
                    {
                        var message = messages[i];
                        if (message.Id == 0 && message.RandomId != 0L)
                        {
                            localMessages.Add(message);
                        }
                        else if (message.Id != 0)
                        {
                            remoteMessages.Add(message);
                        }
                    }

                    DeleteMessages(null, localMessages, remoteMessages, revoke, DeleteMessagesInternal, DeleteMessagesInternal);
                }
            }
        }

        #endregion

        #region Multiple Forward

        public RelayCommand MessagesForwardCommand { get; }
        private async void MessagesForwardExecute()
        {
            var messages = SelectedItems.OfType<TLMessage>().Where(x => x.Id != 0).OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                SelectionMode = ListViewSelectionMode.None;

                await ShareView.Current.ShowAsync(messages);
            }
        }

        #endregion

        #region Select

        public RelayCommand<TLMessageBase> MessageSelectCommand { get; }
        private void MessageSelectExecute(TLMessageBase message)
        {
            var messageCommon = message as TLMessageCommonBase;
            if (messageCommon == null)
            {
                return;
            }

            SelectionMode = ListViewSelectionMode.Multiple;

            SelectedItems = new List<TLMessageCommonBase> { messageCommon };
            RaisePropertyChanged("SelectedItems");
        }

        #endregion
    }
}
