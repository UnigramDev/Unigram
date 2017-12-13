using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core.Common;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class ShareViewModel : UnigramViewModelBase
    {
        public ShareViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, DialogsViewModel dialogs)
            : base(protoService, cacheService, aggregator)
        {
            Items = new MvxObservableCollection<ITLDialogWith>();
            SelectedItems = new MvxObservableCollection<ITLDialogWith>();

            SendCommand = new RelayCommand(SendExecute, () => SelectedItems?.Count > 0);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode == NavigationMode.New)
            {
                _dialogs = null;
            }

            var dialogs = GetDialogs();
            if (dialogs != null)
            {
                Items.ReplaceWith(dialogs);
            }

            return Task.CompletedTask;
        }

        private List<ITLDialogWith> _dialogs;
        private List<ITLDialogWith> GetDialogs()
        {
            if (_dialogs == null)
            {
                var dialogs = CacheService.GetDialogs().Select(x => x.With).ToList();
                if (dialogs.IsEmpty())
                {
                    // TODO: request
                }

                for (int i = 0; i < dialogs.Count; i++)
                {
                    if (dialogs[i] is TLChannel channel && (channel.IsBroadcast && !(channel.IsCreator || (channel.HasAdminRights && channel.AdminRights != null && channel.AdminRights.IsPostMessages))))
                    {
                        dialogs.RemoveAt(i);
                        i--;
                    }
                }

                var self = dialogs.FirstOrDefault(x => x is TLUser user && user.IsSelf);
                if (self == null)
                {
                    var user = CacheService.GetUser(SettingsHelper.UserId);
                    if (user == null)
                    {
                        //var response = await ProtoService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUserSelf() });
                        //if (response.IsSucceeded)
                        //{
                        //    user = response.Result.FirstOrDefault() as TLUser;
                        //}
                    }

                    if (user != null)
                    {
                        self = user;
                    }
                }

                if (self != null)
                {
                    dialogs.Remove(self);
                    dialogs.Insert(0, self);
                }

                _dialogs = dialogs;
            }

            return _dialogs;
        }

        private MvxObservableCollection<ITLDialogWith> _selectedItems = new MvxObservableCollection<ITLDialogWith>();
        public MvxObservableCollection<ITLDialogWith> SelectedItems
        {
            get
            {
                return _selectedItems;
            }
            set
            {
                Set(ref _selectedItems, value);
                SendCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _comment;
        public string Comment
        {
            get
            {
                return _comment;
            }
            set
            {
                Set(ref _comment, value);
            }
        }

        private IEnumerable<TLMessage> _messages;
        public IEnumerable<TLMessage> Messages
        {
            get
            {
                return _messages;
            }
            set
            {
                Set(ref _messages, value);
            }
        }

        private TLInputMediaBase _inputMedia;
        public TLInputMediaBase InputMedia
        {
            get
            {
                return _inputMedia;
            }
            set
            {
                Set(ref _inputMedia, value);
            }
        }

        public bool IsWithMyScore { get; set; }

        public bool IsCopyLinkEnabled
        {
            get
            {
                return _shareLink != null && DataTransferManager.IsSupported();
            }
        }

        private Uri _shareLink;
        public Uri ShareLink
        {
            get
            {
                return _shareLink;
            }
            set
            {
                Set(ref _shareLink, value);
                RaisePropertyChanged(() => IsCopyLinkEnabled);
            }
        }

        private string _shareTitle;
        public string ShareTitle
        {
            get
            {
                return _shareTitle;
            }
            set
            {
                Set(ref _shareTitle, value);
            }
        }

        public MvxObservableCollection<ITLDialogWith> Items { get; private set; }



        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            var dialogs = SelectedItems.ToList();
            if (dialogs.Count == 0)
            {
                return;
            }

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            if (_messages != null)
            {
                foreach (var dialog in dialogs)
                {
                    TLInputPeerBase toPeer = dialog.ToInputPeer();
                    TLInputPeerBase fromPeer = null;
                    TLMessage comment = null;

                    var msgs = new TLVector<TLMessage>();
                    var msgIds = new TLVector<int>();

                    var grouped = false;

                    foreach (var fwdMessage in _messages)
                    {
                        var clone = fwdMessage.Clone();
                        clone.Id = 0;
                        clone.HasEditDate = false;
                        clone.EditDate = null;
                        clone.HasReplyToMsgId = false;
                        clone.ReplyToMsgId = null;
                        clone.HasReplyMarkup = false;
                        clone.ReplyMarkup = null;
                        clone.Date = date;
                        clone.ToId = dialog.ToPeer();
                        clone.RandomId = TLLong.Random();
                        clone.IsOut = true;
                        clone.IsPost = false;
                        clone.FromId = SettingsHelper.UserId;
                        clone.IsMediaUnread = dialog.ToPeer() is TLPeerChannel ? true : false;
                        clone.IsUnread = true;
                        clone.State = TLMessageState.Sending;
                        clone.HasGroupedId = false;
                        clone.GroupedId = null;

                        if (clone.Media == null)
                        {
                            clone.HasMedia = true;
                            clone.Media = new TLMessageMediaEmpty();
                        }

                        if (fwdMessage.HasGroupedId)
                        {
                            grouped = true;
                        }

                        if (fwdMessage.Parent is TLChannel channel)
                        {
                            if (channel.IsBroadcast)
                            {
                                if (!channel.IsSignatures)
                                {
                                    clone.HasFromId = false;
                                    clone.FromId = null;
                                }

                                // TODO
                                //if (IsSilent)
                                //{
                                //    clone.IsSilent = true;
                                //}

                                clone.HasViews = true;
                                clone.Views = 1;
                            }
                        }

                        if (clone.Media is TLMessageMediaGame gameMedia)
                        {
                            clone.HasEntities = false;
                            clone.Entities = null;
                            clone.Message = null;
                        }
                        else if (clone.Media is TLMessageMediaGeoLive geoLiveMedia)
                        {
                            clone.Media = new TLMessageMediaGeo { Geo = geoLiveMedia.Geo };
                        }

                        if (fromPeer == null)
                        {
                            fromPeer = fwdMessage.Parent.ToInputPeer();
                        }

                        if (clone.FwdFrom == null && !clone.IsGame())
                        {
                            if (fwdMessage.ToId is TLPeerChannel)
                            {
                                var fwdChannel = CacheService.GetChat(fwdMessage.ToId.Id) as TLChannel;
                                if (fwdChannel != null && fwdChannel.IsMegaGroup)
                                {
                                    clone.HasFwdFrom = true;
                                    clone.FwdFrom = new TLMessageFwdHeader
                                    {
                                        HasFromId = true,
                                        FromId = fwdMessage.FromId,
                                        Date = fwdMessage.Date
                                    };
                                }
                                else
                                {
                                    clone.HasFwdFrom = true;
                                    clone.FwdFrom = new TLMessageFwdHeader
                                    {
                                        HasFromId = fwdMessage.HasFromId,
                                        FromId = fwdMessage.FromId,
                                        Date = fwdMessage.Date
                                    };

                                    if (fwdChannel.IsBroadcast)
                                    {
                                        clone.FwdFrom.HasChannelId = clone.FwdFrom.HasChannelPost = true;
                                        clone.FwdFrom.ChannelId = fwdChannel.Id;
                                        clone.FwdFrom.ChannelPost = fwdMessage.Id;
                                        clone.FwdFrom.HasPostAuthor = fwdMessage.HasPostAuthor;
                                        clone.FwdFrom.PostAuthor = fwdMessage.PostAuthor;
                                    }
                                }
                            }
                            else if (fwdMessage.FromId == SettingsHelper.UserId && fwdMessage.ToId is TLPeerUser peerUser && peerUser.UserId == SettingsHelper.UserId)
                            {

                            }
                            else
                            {
                                clone.HasFwdFrom = true;
                                clone.FwdFrom = new TLMessageFwdHeader
                                {
                                    HasFromId = true,
                                    FromId = fwdMessage.FromId,
                                    Date = fwdMessage.Date
                                };
                            }

                            if (clone.FwdFrom != null && ((toPeer is TLInputPeerUser user && user.UserId == SettingsHelper.UserId) || toPeer is TLInputPeerSelf))
                            {
                                clone.FwdFrom.SavedFromMsgId = fwdMessage.Id;
                                clone.FwdFrom.SavedFromPeer = fwdMessage.Parent?.ToPeer();
                                clone.FwdFrom.HasSavedFromMsgId = true;
                                clone.FwdFrom.HasSavedFromPeer = clone.FwdFrom.SavedFromPeer != null;
                            }
                        }

                        msgs.Add(clone);
                        msgIds.Add(fwdMessage.Id);
                    }

                    if (!string.IsNullOrEmpty(_comment))
                    {
                        comment = TLUtils.GetMessage(SettingsHelper.UserId, toPeer.ToPeer(), TLMessageState.Sending, true, true, date, _comment, new TLMessageMediaEmpty(), TLLong.Random(), null);
                        msgs.Insert(0, comment);
                    }

                    CacheService.SyncSendingMessages(msgs, null, async (m) =>
                    {
                        if (comment != null)
                        {
                            msgs.Remove(comment);

                            var result = await ProtoService.SendMessageAsync(comment, null);
                            if (result.IsSucceeded)
                            {

                            }
                            else
                            {

                            }
                        }

                        var response = await ProtoService.ForwardMessagesAsync(toPeer, fromPeer, msgIds, msgs, IsWithMyScore, grouped);
                        if (response.IsSucceeded)
                        {
                            foreach (var i in m)
                            {
                                Aggregator.Publish(i);
                            }
                        }
                        else
                        {

                        }
                    });
                }

                NavigationService.GoBack();
            }
            else if (_inputMedia != null)
            {
                if (_inputMedia is TLInputMediaDocument document)
                {
                    document.Caption = null;
                }
                else if (_inputMedia is TLInputMediaPhoto photo)
                {
                    photo.Caption = null;
                }

                foreach (var dialog in dialogs)
                {
                    TLMessage comment = null;
                    var msgs = new TLVector<TLMessage>();

                    if (!string.IsNullOrEmpty(_comment))
                    {
                        comment = TLUtils.GetMessage(SettingsHelper.UserId, dialog.ToPeer(), TLMessageState.Sending, true, true, date, _comment, new TLMessageMediaEmpty(), TLLong.Random(), null);
                        msgs.Insert(0, comment);
                    }

                    var message = TLUtils.GetMessage(SettingsHelper.UserId, dialog.ToPeer(), TLMessageState.Sending, true, true, date, null, new TLMessageMediaEmpty(), TLLong.Random(), null);
                    msgs.Add(message);

                    CacheService.SyncSendingMessages(msgs, null, async (m) =>
                    {
                        if (comment != null)
                        {
                            msgs.Remove(comment);

                            var result = await ProtoService.SendMessageAsync(comment, null);
                            if (result.IsSucceeded)
                            {
                            }
                            else
                            {

                            }
                        }

                        var response = await ProtoService.SendMediaAsync(dialog.ToInputPeer(), _inputMedia, message);
                        if (response.IsSucceeded)
                        {
                            foreach (var i in m)
                            {
                                Aggregator.Publish(i);
                            }
                        }
                    });
                }

                NavigationService.GoBack();
            }
            else if (ShareLink != null)
            {
                foreach (var dialog in dialogs)
                {
                    TLMessage comment = null;
                    var msgs = new TLVector<TLMessage>();

                    if (!string.IsNullOrEmpty(_comment))
                    {
                        comment = TLUtils.GetMessage(SettingsHelper.UserId, dialog.ToPeer(), TLMessageState.Sending, true, true, date, _comment, new TLMessageMediaEmpty(), TLLong.Random(), null);
                        msgs.Insert(0, comment);
                    }

                    var messageText = ShareLink.ToString();
                    var message = TLUtils.GetMessage(SettingsHelper.UserId, dialog.ToPeer(), TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), null);
                    msgs.Add(message);

                    CacheService.SyncSendingMessages(msgs, null, async (m) =>
                    {
                        if (comment != null)
                        {
                            msgs.Remove(comment);

                            var result = await ProtoService.SendMessageAsync(comment, null);
                            if (result.IsSucceeded)
                            {
                            }
                            else
                            {

                            }
                        }

                        var response = await ProtoService.SendMessageAsync(message, () => { message.State = TLMessageState.Confirmed; });
                        if (response.IsSucceeded)
                        {
                            foreach (var i in m)
                            {
                                Aggregator.Publish(i);
                            }
                        }
                    });
                }

                NavigationService.GoBack();
            }

            //App.InMemoryState.ForwardMessages = new List<TLMessage>(messages);
            //NavigationService.GoBackAt(0);
        }

        #region Search

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.Multiple;
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

        public async void Search(string text)
        {
            var results = await SearchLocalAsync(text);
            if (results != null)
            {
                SelectionMode = ListViewSelectionMode.None;
                Items.ReplaceWith(results.Cast<TLDialog>().Select(x => x.With));
            }
            else
            {
                var dialogs = GetDialogs();
                if (dialogs != null)
                {
                    foreach (var item in _selectedItems)
                    {
                        //dialogs.Remove(item);
                        //dialogs.Insert(0, item);

                        if (dialogs.Contains(item)) { }
                        else
                        {
                            dialogs.Insert(0, item);
                        }
                    }

                    SelectionMode = ListViewSelectionMode.None;
                    Items.ReplaceWith(dialogs);
                }
            }
        }

        private async Task<KeyedList<string, TLObject>> SearchLocalAsync(string query1)
        {
            if (string.IsNullOrWhiteSpace(query1))
            {
                return null;
            }

            var dialogs = await Task.Run(() => CacheService.GetDialogs());
            var contacts = await Task.Run(() => CacheService.GetContacts());

            if (dialogs != null && contacts != null)
            {
                var query = LocaleHelper.GetQuery(query1);

                var simple = new List<TLDialog>();
                var parent = dialogs.Where(dialog =>
                {
                    if (dialog.With is TLUser user)
                    {
                        return user.IsLike(query, StringComparison.OrdinalIgnoreCase);
                    }
                    else if (dialog.With is TLChannel channel)
                    {
                        return channel.IsLike(query, StringComparison.OrdinalIgnoreCase);
                    }
                    else if (dialog.With is TLChat chat)
                    {
                        return !chat.HasMigratedTo && chat.IsLike(query, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        return false;
                    }
                }).ToList();

                var contactsResults = contacts.OfType<TLUser>().Where(x => x.IsLike(query, StringComparison.OrdinalIgnoreCase));

                foreach (var result in contactsResults)
                {
                    var dialog = parent.FirstOrDefault(x => x.Peer.TypeId == TLType.PeerUser && x.Id == result.Id);
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

        #endregion
    }
}
