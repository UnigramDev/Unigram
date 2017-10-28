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
using Windows.ApplicationModel.DataTransfer;

namespace Unigram.ViewModels
{
    public class ShareViewModel : UnigramViewModelBase
    {
        public ShareViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, DialogsViewModel dialogs)
            : base(protoService, cacheService, aggregator)
        {
            Dialogs = dialogs;
            GroupedItems = new ObservableCollection<ShareViewModel> { this };

            SendCommand = new RelayCommand(SendExecute, () => SelectedItems.Count > 0);
        }

        private List<TLDialog> _selectedItems = new List<TLDialog>();
        public List<TLDialog> SelectedItems
        {
            get
            {
                return _selectedItems;
            }
            set
            {
                Set(ref _selectedItems, value);
                SendCommand.RaiseCanExecuteChanged();
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

        public DialogsViewModel Dialogs { get; private set; }

        public ObservableCollection<ShareViewModel> GroupedItems { get; private set; }



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

                    var msgs = new TLVector<TLMessage>();
                    var msgIds = new TLVector<int>();

                    foreach (var fwdMessage in _messages)
                    {

                        var clone = fwdMessage.Clone();
                        clone.Id = 0;
                        clone.HasReplyToMsgId = false;
                        clone.ReplyToMsgId = null;
                        clone.HasReplyMarkup = false;
                        clone.ReplyMarkup = null;
                        clone.Date = date;
                        clone.ToId = dialog.Peer;
                        clone.RandomId = TLLong.Random();
                        clone.IsOut = true;
                        clone.IsPost = false;
                        clone.FromId = SettingsHelper.UserId;
                        clone.IsMediaUnread = dialog.Peer is TLPeerChannel ? true : false;
                        clone.IsUnread = true;
                        clone.State = TLMessageState.Sending;

                        if (clone.Media == null)
                        {
                            clone.HasMedia = true;
                            clone.Media = new TLMessageMediaEmpty();
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
                    }

                        msgs.Add(clone);
                        msgIds.Add(fwdMessage.Id);
                    }

                    CacheService.SyncSendingMessages(msgs, null, async (m) =>
                    {
                        var response = await ProtoService.ForwardMessagesAsync(toPeer, fromPeer, msgIds, msgs, IsWithMyScore);
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
                    var message = TLUtils.GetMessage(SettingsHelper.UserId, dialog.Peer, TLMessageState.Sending, true, true, date, null, new TLMessageMediaEmpty(), TLLong.Random(), null);

                    CacheService.SyncSendingMessage(message, null, async (m) =>
                    {
                        var response = await ProtoService.SendMediaAsync(dialog.ToInputPeer(), _inputMedia, message);
                        if (response.IsSucceeded)
                        {
                            Aggregator.Publish(m);
                        }
                    });
                }

                NavigationService.GoBack();
            }
            else if (ShareLink != null)
            {
                foreach (var dialog in dialogs)
                {
                    var messageText = ShareLink.ToString();
                    var message = TLUtils.GetMessage(SettingsHelper.UserId, dialog.Peer, TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), null);

                    CacheService.SyncSendingMessage(message, null, async (m) =>
                    {
                        var response = await ProtoService.SendMessageAsync(message, () => { message.State = TLMessageState.Confirmed; });
                        if (response.IsSucceeded)
                        {
                            Aggregator.Publish(m);
                        }
                    });
                }

                NavigationService.GoBack();
            }

            //App.InMemoryState.ForwardMessages = new List<TLMessage>(messages);
            //NavigationService.GoBackAt(0);
        }

        //_stateService.ForwardMessages = Messages.Where(x => x.IsSelected).ToList();
        //_stateService.ForwardMessages.Reverse();

        //SelectionMode = Windows.UI.Xaml.Controls.ListViewSelectionMode.None;
        //NavigationService.GoBack();
    }
}
