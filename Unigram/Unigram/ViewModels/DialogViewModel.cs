﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;
using Template10.Common;
using Telegram.Api.Helpers;
using Unigram.Core.Services;
using Telegram.Api.Services.Updates;
using Telegram.Api.Transport;
using Telegram.Api.Services.Connection;
using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections;
using System.ComponentModel;
using Windows.UI.Xaml;
using Unigram.Converters;
using Windows.UI.Xaml.Media;
using System.Diagnostics;
using Telegram.Api.Services.FileManager;
using Windows.Storage.Pickers;
using System.IO;
using Windows.Storage;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Helpers;
using Unigram.Controls.Views;
using Unigram.Core.Models;
using Unigram.Controls;
using Unigram.Core.Helpers;
using Org.BouncyCastle.Security;
using Unigram.Core;
using Unigram.Services;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using Telegram.Api.TL.Methods.Messages;
using Telegram.Api;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel : UnigramViewModelBase
    {
        public MessageCollection Messages { get; private set; } = new MessageCollection();

        private List<TLMessageBase> _selectedMessages = new List<TLMessageBase>();
        public List<TLMessageBase> SelectedMessages
        {
            get
            {
                return _selectedMessages;
            }
            set
            {
                Set(ref _selectedMessages, value);
                MessagesForwardCommand.RaiseCanExecuteChanged();
                MessagesDeleteCommand.RaiseCanExecuteChanged();
            }
        }

        private readonly DialogStickersViewModel _stickers;
        private readonly IStickersService _stickersService;
        private readonly IUploadFileManager _uploadFileManager;
        private readonly IUploadAudioManager _uploadAudioManager;
        private readonly IUploadDocumentManager _uploadDocumentManager;
        private readonly IUploadVideoManager _uploadVideoManager;

        public int participantCount = 0;
        public int online = 0;

        public DialogViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager, IUploadAudioManager uploadAudioManager, IUploadDocumentManager uploadDocumentManager, IUploadVideoManager uploadVideoManager, IStickersService stickersService, DialogStickersViewModel stickers)
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;
            _uploadAudioManager = uploadAudioManager;
            _uploadDocumentManager = uploadDocumentManager;
            _uploadVideoManager = uploadVideoManager;
            _stickersService = stickersService;

            _stickers = stickers;
        }

        public DialogStickersViewModel Stickers { get { return _stickers; } }



        private TLDialog _currentDialog;

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
                RaisePropertyChanged(() => IsSilentVisible);
            }
        }

        private string _lastSeen;
        public string LastSeen
        {
            get
            {
                return _lastSeen;
            }
            set
            {
                Set(ref _lastSeen, value);
            }
        }

        private string _text;
        public string Text
        {
            get
            {
                return _text;
            }
            set
            {
                Set(ref _text, value);
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

        private TLMessageBase _pinnedMessage;
        public TLMessageBase PinnedMessage
        {
            get
            {
                return _pinnedMessage;
            }
            set
            {
                Set(ref _pinnedMessage, value);
            }
        }

        private bool _isReportSpam;
        public bool IsReportSpam
        {
            get
            {
                return _isReportSpam;
            }
            set
            {
                Set(ref _isReportSpam, value);
            }
        }

        private ItemsUpdatingScrollMode _updatingScrollMode;
        public ItemsUpdatingScrollMode UpdatingScrollMode
        {
            get
            {
                return _updatingScrollMode;
            }
            set
            {
                Set(ref _updatingScrollMode, value);
            }
        }

        public bool IsFirstSliceLoaded { get; set; }

        private bool _isLoadingNextSlice;
        private bool _isLoadingPreviousSlice;
        private Stack<int> _goBackStack = new Stack<int>();

        public async Task LoadNextSliceAsync()
        {
            if (_isLoadingNextSlice || _isLoadingPreviousSlice) return;
            _isLoadingNextSlice = true;

            UpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;

            Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");

            var maxId = int.MaxValue;

            for (int i = 0; i < Messages.Count; i++)
            {
                if (Messages[i].Id != 0 && Messages[i].Id < maxId)
                {
                    maxId = Messages[i].Id;
                }
            }

            Debug.WriteLine("DialogViewModel: LoadNextSliceAsync: Begin request");

            //return;

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, 0, maxId, 50);
            if (result.IsSucceeded)
            {
                ProcessReplies(result.Result.Messages);

                Debug.WriteLine("DialogViewModel: LoadNextSliceAsync: Replies processed");

                //foreach (var item in result.Result.Messages.OrderByDescending(x => x.Date))
                for (int i = 0; i < result.Result.Messages.Count; i++)
                {
                    var item = result.Result.Messages[i];
                    Messages.Insert(0, item);
                    //InsertMessage(item as TLMessageCommonBase);
                }

                Debug.WriteLine("DialogViewModel: LoadNextSliceAsync: Items added");

                //foreach (var item in result.Result.Messages.OrderBy(x => x.Date))
                for (int i = result.Result.Messages.Count - 1; i >= 0; i--)
                {
                    var item = result.Result.Messages[i];
                    var message = item as TLMessage;
                    if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                    {
                        var user = CacheService.GetUser(message.FromId) as TLUser;
                        if (user != null && user.IsBot)
                        {
                            SetReplyMarkup(message);
                        }
                    }
                }
            }

            _isLoadingNextSlice = false;
        }

        public async Task LoadPreviousSliceAsync()
        {
            if (_isLoadingNextSlice || _isLoadingPreviousSlice) return;
            _isLoadingPreviousSlice = true;

            UpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView;

            Debug.WriteLine("DialogViewModel: LoadPreviousSliceAsync");

            var maxId = int.MaxValue;
            var limit = 50;

            //for (int i = 0; i < Messages.Count; i++)
            //{
            //    if (Messages[i].Id != 0 && Messages[i].Id < maxId)
            //    {
            //        maxId = Messages[i].Id;
            //    }
            //}

            maxId = Messages.LastOrDefault()?.Id ?? 1;

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, -limit, maxId, limit);
            if (result.IsSucceeded)
            {
                ProcessReplies(result.Result.Messages);

                //foreach (var item in result.Result.Messages.OrderBy(x => x.Date))
                for (int i = result.Result.Messages.Count - 1; i >= 0; i--)
                {
                    var item = result.Result.Messages[i];
                    if (item.Id > maxId)
                    {
                        Messages.Add(item);
                    }
                    //InsertMessage(item as TLMessageCommonBase);
                }

                foreach (var item in result.Result.Messages.OrderBy(x => x.Date))
                {
                    var message = item as TLMessage;
                    if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                    {
                        var user = CacheService.GetUser(message.FromId) as TLUser;
                        if (user != null && user.IsBot)
                        {
                            SetReplyMarkup(message);
                        }
                    }
                }

                IsFirstSliceLoaded = result.Result.Messages.Count < limit;
            }

            _isLoadingPreviousSlice = false;
        }

        public RelayCommand PreviousSliceCommand => new RelayCommand(PreviousSliceExecute);
        private async void PreviousSliceExecute()
        {
            if (_goBackStack.Count > 0)
            {
                await LoadMessageSliceAsync(null, _goBackStack.Pop());
            }
            else
            {
                Messages.Clear();
                await LoadFirstSliceAsync();
            }
        }

        public async Task LoadMessageSliceAsync(int? previousId, int maxId)
        {
            if (_isLoadingNextSlice || _isLoadingPreviousSlice) return;
            _isLoadingNextSlice = true;
            _isLoadingPreviousSlice = true;

            UpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView;

            Debug.WriteLine("DialogViewModel: LoadMessageSliceAsync");

            if (previousId.HasValue)
            {
                _goBackStack.Push(previousId.Value);
            }

            Messages.Clear();

            var offset = -50;
            var limit = 50;

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, offset, maxId, limit);
            if (result.IsSucceeded)
            {
                ProcessReplies(result.Result.Messages);

                //foreach (var item in result.Result.Messages.OrderByDescending(x => x.Date))
                for (int i = result.Result.Messages.Count - 1; i >= 0; i--)
                {
                    var item = result.Result.Messages[i];
                    Messages.Add(item);

                    var message = item as TLMessage;
                    if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                    {
                        var user = CacheService.GetUser(message.FromId) as TLUser;
                        if (user != null && user.IsBot)
                        {
                            SetReplyMarkup(message);
                        }
                    }
                }

                IsFirstSliceLoaded = result.Result.Messages.Count < limit;
            }

            _isLoadingNextSlice = false;
            _isLoadingPreviousSlice = false;
        }

        public async Task LoadFirstSliceAsync()
        {
            if (_isLoadingNextSlice || _isLoadingPreviousSlice) return;
            _isLoadingNextSlice = true;
            _isLoadingPreviousSlice = true;

            UpdatingScrollMode = _currentDialog?.UnreadCount > 0 ? ItemsUpdatingScrollMode.KeepItemsInView : ItemsUpdatingScrollMode.KeepLastItemInView;

            Debug.WriteLine("DialogViewModel: LoadFirstSliceAsync");

            var lastRead = true;

            var maxId = _currentDialog?.UnreadCount > 0 ? _currentDialog.ReadInboxMaxId : int.MaxValue;
            var offset = _currentDialog?.UnreadCount > 0 && maxId > 0 ? -51 : 0;
            var limit = 50;

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, offset, maxId, limit);
            if (result.IsSucceeded)
            {
                ProcessReplies(result.Result.Messages);

                //foreach (var item in result.Result.Messages.OrderBy(x => x.Date))
                for (int i = result.Result.Messages.Count - 1; i >= 0; i--)
                {
                    var item = result.Result.Messages[i];
                    var message = item as TLMessageCommonBase;

                    if (item.Id > maxId && lastRead && message != null && !message.IsOut)
                    {
                        var serviceMessage = new TLMessageService
                        {
                            FromId = SettingsHelper.UserId,
                            ToId = Peer.ToPeer(),
                            State = TLMessageState.Sending,
                            IsOut = true,
                            IsUnread = true,
                            Date = item.Date,
                            Action = new TLMessageActionUnreadMessages(),
                            RandomId = TLLong.Random()
                        };

                        Messages.Add(serviceMessage);
                        lastRead = false;
                    }

                    Messages.Add(item);
                }

                IsFirstSliceLoaded = result.Result.Messages.Count < limit;
            }

            _isLoadingNextSlice = false;
            _isLoadingPreviousSlice = false;
        }

        public async Task LoadFirstSliceAsyncASDFASRFHJNDKDFKJFD()
        {
            if (_isLoadingNextSlice || _isLoadingPreviousSlice) return;
            _isLoadingNextSlice = true;
            _isLoadingPreviousSlice = true;

            UpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView;

            Debug.WriteLine("DialogViewModel: LoadFirstSliceAsync");

            var already = new List<long>();
            var sets = new Dictionary<long, TLInputStickerSetBase>();
            var lastId = 0;

            while (already.Count < 250 || lastId < int.MaxValue)
            {
                var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, 0, lastId, 100);
                if (result.IsSucceeded)
                {
                    foreach (var message in result.Result.Messages.OfType<TLMessage>())
                    {
                        if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document && message.IsSticker())
                        {
                            var set = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault().StickerSet as TLInputStickerSetID;

                            already.Add(documentMedia.Document.Id);

                            if (sets.ContainsKey(set.Id))
                            {
                                await ProtoService.DeleteMessagesAsync(((TLChannel)With).ToInputChannel(), new TLVector<int> { message.Id });
                            }

                            sets[set.Id] = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault().StickerSet;
                        }
                    }

                    lastId = result.Result.Messages.LastOrDefault()?.Id ?? int.MaxValue;

                    if (result.Result.Messages.Count == 0)
                    {
                        break;
                    }
                }
            }

            foreach (var set in sets)
            {
                await ProtoService.InstallStickerSetAsync(set.Value, false);
                await Task.Delay(200);
            }

            return;

            var response = await ProtoService.GetAllStickersAsync(new byte[0]);
            if (response.IsSucceeded)
            {
                if (response.Result is TLMessagesAllStickers stickers)
                {
                    foreach (var set in stickers.Sets)
                    {
                        if (already.Count == 200)
                        {
                            return;
                        }

                        var send = false;
                        var documents = stickers.Documents.OfType<TLDocument>().Where(y => ((TLInputStickerSetID)y.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault().StickerSet).Id == set.Id).ToList();
                        var first = documents.FirstOrDefault(x => already.Contains(x.Id));
                        if (first == null)
                        {
                            send = true;
                        }

                        if (send)
                        {
                            var media = new TLMessageMediaDocument { Document = documents[0] };
                            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

                            var input = new TLInputMediaDocument
                            {
                                Id = new TLInputDocument
                                {
                                    Id = documents[0].Id,
                                    AccessHash = documents[0].AccessHash
                                }
                            };

                            already.Add(documents[0].Id);

                            await ProtoService.SendMediaAsync(Peer, input, message);
                            await Task.Delay(500);
                        }
                    }
                }
            }
        }

        public async void ProcessReplies(IList<TLMessageBase> messages)
        {
            var replyIds = new TLVector<int>();
            var replyToMsgs = new List<TLMessageCommonBase>();

            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i] as TLMessageCommonBase;
                if (message != null)
                {
                    var replyId = message.ReplyToMsgId;
                    if (replyId != null && replyId.Value != 0)
                    {
                        var channelId = new int?();
                        var channel = message.ToId as TLPeerChannel;
                        if (channel != null)
                        {
                            channelId = channel.Id;
                        }

                        var reply = CacheService.GetMessage(replyId.Value, channelId);
                        if (reply != null)
                        {
                            messages[i].Reply = reply;
                            messages[i].RaisePropertyChanged(() => messages[i].Reply);
                            messages[i].RaisePropertyChanged(() => messages[i].ReplyInfo);

                            if (messages[i] is TLMessageService)
                            {
                                messages[i].RaisePropertyChanged(() => ((TLMessageService)messages[i]).Self);
                            }
                        }
                        else
                        {
                            replyIds.Add(replyId.Value);
                            replyToMsgs.Add(message);
                        }
                    }

                    //if (message.NotListened && message.Media != null)
                    //{
                    //    message.Media.NotListened = true;
                    //}
                }
            }

            if (replyIds.Count > 0)
            {
                Task<MTProtoResponse<TLMessagesMessagesBase>> task = null;

                if (Peer is TLInputPeerChannel)
                {
                    var first = replyToMsgs.FirstOrDefault();
                    if (first.ToId is TLPeerChat)
                    {
                        task = ProtoService.GetMessagesAsync(replyIds);
                    }
                    else
                    {
                        var peer = Peer as TLInputPeerChannel;
                        task = ProtoService.GetMessagesAsync(new TLInputChannel { ChannelId = peer.ChannelId, AccessHash = peer.AccessHash }, replyIds);
                    }
                }
                else
                {
                    task = ProtoService.GetMessagesAsync(replyIds);
                }

                var result = await task;
                if (result.IsSucceeded)
                {
                    CacheService.AddChats(result.Result.Chats, (results) => { });
                    CacheService.AddUsers(result.Result.Users, (results) => { });

                    for (int j = 0; j < result.Result.Messages.Count; j++)
                    {
                        for (int k = 0; k < replyToMsgs.Count; k++)
                        {
                            var message = replyToMsgs[k];
                            if (message != null && message.ReplyToMsgId.Value == result.Result.Messages[j].Id)
                            {
                                replyToMsgs[k].Reply = result.Result.Messages[j];
                                replyToMsgs[k].RaisePropertyChanged(() => replyToMsgs[k].Reply);
                                replyToMsgs[k].RaisePropertyChanged(() => replyToMsgs[k].ReplyInfo);

                                if (replyToMsgs[k] is TLMessageService)
                                {
                                    replyToMsgs[k].RaisePropertyChanged(() => ((TLMessageService)replyToMsgs[k]).Self);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Execute.ShowDebugMessage("messages.getMessages error " + result.Error);
                }
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Messages.Clear();

            var tuple = parameter as Tuple<TLPeerBase, int>;
            if (tuple != null)
            {
                parameter = tuple.Item1;
            }

            var participant = GetParticipant(parameter as TLPeerBase);
            var channel = participant as TLChannel;
            var chat = participant as TLChat;
            var user = participant as TLUser;

            if (user != null)
            {
                var full = await ProtoService.GetFullUserAsync(new TLInputUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 });

                With = user;
                Peer = new TLInputPeerUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 };

                //test calls
                //var config = await ProtoService.GetDHConfigAsync(0, 0);
                //if (config.IsSucceeded)
                //{
                //    var dh = config.Result;
                //    if (!TLUtils.CheckPrime(dh.P, dh.G))
                //    {
                //        return;
                //    }

                //    var array = new byte[256];
                //    var secureRandom = new SecureRandom();
                //    secureRandom.NextBytes(array);

                //    var a = array;
                //    var p = dh.P;
                //    var g = dh.G;
                //    var gb = MTProtoService.GetGB(array, dh.G, dh.P);
                //    var ga = gb;

                //    var request = new Telegram.Api.TL.Methods.Phone.TLPhoneRequestCall
                //    {
                //        UserId = new TLInputUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 },
                //        RandomId = TLInt.Random(),
                //        GA = ga,
                //        Protocol = new TLPhoneCallProtocol
                //        {
                //            IsUdpP2p = true,
                //            IsUdpReflector = true,
                //            MinLayer = 65,
                //            MaxLayer = 65,
                //        }
                //    };

                //    var proto = (MTProtoService)ProtoService;
                //    proto.SendInformativeMessageInternal<TLPhonePhoneCall>("phone.requestCall", request,
                //    result =>
                //    {
                //        Debugger.Break();
                //    },
                //    fault =>
                //    {
                //        Debugger.Break();
                //    });
                //}
            }
            else if (channel != null)
            {
                With = channel;
                Peer = new TLInputPeerChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash ?? 0 };

                var input = new TLInputChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash ?? 0 };
                var channelDetails = await ProtoService.GetFullChannelAsync(input);
                if (channelDetails.IsSucceeded)
                {
                    var channelFull = channelDetails.Result.FullChat as TLChannelFull;
                    if (channelFull.HasPinnedMsgId)
                    {
                        var y = await ProtoService.GetMessagesAsync(input, new TLVector<int>() { channelFull.PinnedMsgId ?? 0 });
                        if (y.IsSucceeded)
                        {
                            PinnedMessage = y.Result.Messages.FirstOrDefault();
                        }
                    }
                    //online = 0;
                    //participantCount = channelFull.ParticipantsCount ?? default(int);
                    //if (participantCount < 200)
                    //{
                    //    try
                    //    {
                    //        var temp = await ProtoService.GetParticipantsAsync(input, null, 0, 5000);
                    //        if (temp.IsSucceeded)
                    //        {
                    //            foreach (TLUserBase now in temp.Result.Users)
                    //            {
                    //                TLUser tempUser = now as TLUser;

                    //                if (LastSeenHelper.GetLastSeen(tempUser).Item1.Equals("online") && !tempUser.IsSelf) online++;
                    //            }
                    //        }
                    //    }
                    //    catch (Exception e)
                    //    {
                    //        Debug.WriteLine(e.ToString());
                    //        online = -2;
                    //    }
                    //}
                    //else
                    //{
                    //    online = -2;
                    //}
                    //LastSeen = participantCount + " members" + ((online > 0) ? (", " + online + " online") : "");
                }

            }
            else if (chat != null)
            {
                With = chat;
                Peer = new TLInputPeerChat { ChatId = chat.Id };

                //var chatDetails = await ProtoService.GetFullChatAsync(chat.Id);
                //if (chatDetails.IsSucceeded)
                //{
                //    participantCount = chatDetails.Result.Users.Count;
                //    if (participantCount < 200)
                //    {
                //        foreach (TLUserBase now in chatDetails.Result.Users)
                //        {
                //            TLUser tempUser = now as TLUser;
                //            if (LastSeenHelper.GetLastSeen(tempUser).Item1.Equals("online") && !tempUser.IsSelf) online++;
                //        }
                //    }
                //    else
                //    {
                //        online = -2;
                //    }
                //    LastSeen = participantCount + " members" + ((online > 0) ? (", " + online + " online") : "");
                //}
            }

            _currentDialog = _currentDialog ?? CacheService.GetDialog(Peer.ToPeer());

            var dialog = _currentDialog;
            if (dialog != null && dialog.HasDraft)
            {
                if (dialog.Draft is TLDraftMessage draft)
                {
                    Aggregator.Publish(new TLUpdateDraftMessage { Draft = draft, Peer = Peer.ToPeer() });
                    ProcessDraftReply(draft);
                }
            }

            var settings = await ProtoService.GetPeerSettingsAsync(Peer);
            if (settings.IsSucceeded)
            {
                IsReportSpam = settings.Result.IsReportSpam;
            }

            LastSeen = await GetSubtitle();

            //#if !DEBUG
            //#endif

            Aggregator.Subscribe(this);

            if (tuple != null)
            {
                await LoadMessageSliceAsync(null, tuple.Item2);
            }
            else
            {
                await LoadFirstSliceAsync();
            }

            if (dialog != null && Messages.Count > 0)
            {
                var unread = dialog.UnreadCount;
                if (Peer is TLInputPeerChannel && channel != null)
                {
                    await ProtoService.ReadHistoryAsync(channel, dialog.TopMessage);
                }
                else
                {
                    await ProtoService.ReadHistoryAsync(Peer, dialog.TopMessage, 0);
                }

                dialog.UnreadCount = dialog.UnreadCount - unread;
                dialog.RaisePropertyChanged(() => dialog.UnreadCount);
            }

            //StickersRecent();
            //GifsSaved();

            //var file = await KnownFolders.SavedPictures.CreateFileAsync("TEST.TXT", CreationCollisionOption.GenerateUniqueName);
            //await FileIO.WriteTextAsync(file, DateTime.Now.ToString());

            if (App.State.ForwardMessages != null)
            {
                Reply = new TLMessagesContainter { FwdMessages = new TLVector<TLMessage>(App.State.ForwardMessages) };
            }
        }

        private async void ShowPinnedMessage(TLChannel channel)
        {
            if (channel == null) return;
            if (channel.PinnedMsgId == null) return;
            if (PinnedMessage != null && PinnedMessage.Id == channel.PinnedMsgId.Value) return;
            if (channel.HiddenPinnedMsgId != null && channel.HiddenPinnedMsgId.Value == channel.PinnedMsgId.Value) return;
            if (channel.PinnedMsgId.Value <= 0)
            {
                if (PinnedMessage != null)
                {
                    PinnedMessage = null;
                    return;
                }
            }
            else
            {
                var messageBase = CacheService.GetMessage(channel.PinnedMsgId, channel.Id);
                if (messageBase != null)
                {
                    PinnedMessage = messageBase;
                    return;
                }

                var respponse = await ProtoService.GetMessagesAsync(channel.ToInputChannel(), new TLVector<int> { channel.PinnedMsgId.Value });
                if (respponse.IsSucceeded)
                {
                    PinnedMessage = respponse.Result.Messages.FirstOrDefault(x => x.Id == channel.PinnedMsgId.Value);
                }
                else
                {
                    Telegram.Api.Helpers.Execute.ShowDebugMessage("channels.getMessages error " + respponse.Error);
                }
            }
        }

        private List<TLDocument> _stickerPack;
        public List<TLDocument> StickerPack
        {
            get
            {
                return _stickerPack;
            }
            set
            {
                Set(ref _stickerPack, value);
            }
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            //var file = await KnownFolders.SavedPictures.CreateFileAsync("TEST.TXT", CreationCollisionOption.GenerateUniqueName);
            //await FileIO.WriteTextAsync(file, DateTime.Now.ToString());
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        private TLObject GetParticipant(TLPeerBase peer)
        {
            if (peer is TLPeerUser user)
            {
                return CacheService.GetUser(user.UserId);
            }

            if (peer is TLPeerChat chat)
            {
                return CacheService.GetChat(chat.ChatId);
            }

            if (peer is TLPeerChannel channel)
            {
                return CacheService.GetChat(channel.ChannelId);
            }

            return null;
        }

        public async void ProcessDraftReply(TLDraftMessage draft)
        {
            var shouldFetch = false;

            var replyId = draft.ReplyToMsgId;
            if (replyId != null && replyId.Value != 0)
            {
                var channelId = new int?();
                //var channel = message.ToId as TLPeerChat;
                //if (channel != null)
                //{
                //    channelId = channel.Id;
                //}
                // TODO: verify
                if (Peer is TLInputPeerChannel)
                {
                    channelId = Peer.ToPeer().Id;
                }

                var reply = CacheService.GetMessage(replyId.Value, channelId);
                if (reply != null)
                {
                    Reply = reply;
                }
                else
                {
                    shouldFetch = true;
                }
            }

            if (shouldFetch)
            {
                Task<MTProtoResponse<TLMessagesMessagesBase>> task = null;

                if (Peer is TLInputPeerChannel)
                {
                    // TODO: verify
                    //var first = replyToMsgs.FirstOrDefault();
                    //if (first.ToId is TLPeerChat)
                    //{
                    //    task = ProtoService.GetMessagesAsync(new TLVector<int> { draft.ReplyToMsgId.Value });
                    //}
                    //else
                    {
                        var peer = Peer as TLInputPeerChannel;
                        task = ProtoService.GetMessagesAsync(new TLInputChannel { ChannelId = peer.ChannelId, AccessHash = peer.AccessHash }, new TLVector<int> { draft.ReplyToMsgId.Value });
                    }
                }
                else
                {
                    task = ProtoService.GetMessagesAsync(new TLVector<int> { draft.ReplyToMsgId.Value });
                }

                var result = await task;
                if (result.IsSucceeded)
                {
                    CacheService.AddChats(result.Result.Chats, (results) => { });
                    CacheService.AddUsers(result.Result.Users, (results) => { });

                    for (int j = 0; j < result.Result.Messages.Count; j++)
                    {
                        if (draft.ReplyToMsgId.Value == result.Result.Messages[j].Id)
                        {
                            Reply = result.Result.Messages[j];
                        }
                    }
                }
                else
                {
                    Execute.ShowDebugMessage("messages.getMessages error " + result.Error);
                }
            }
        }

        private bool _isSilent;
        public bool IsSilent
        {
            get
            {
                return _isSilent && With is TLChannel && ((TLChannel)With).IsBroadcast;
            }
            set
            {
                Set(ref _isSilent, value);
            }
        }

        public bool IsSilentVisible
        {
            get
            {
                return With is TLChannel && ((TLChannel)With).IsBroadcast;
            }
        }

        #region Reply 

        private TLMessageBase _reply;
        public TLMessageBase Reply
        {
            get
            {
                return _reply;
            }
            set
            {
                if (_reply != value)
                {
                    if (_reply is TLMessagesContainter container && container.FwdMessages != null && value == null)
                    {
                        App.State.ForwardMessages = null;
                    }

                    _reply = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(() => ReplyInfo);
                }
            }
        }

        public ReplyInfo ReplyInfo
        {
            get
            {
                if (_reply != null)
                {
                    return new ReplyInfo
                    {
                        Reply = _reply,
                        ReplyToMsgId = _reply.Id
                    };
                }

                return null;
            }
        }

        public RelayCommand ClearReplyCommand => new RelayCommand(ClearReplyExecute);
        private void ClearReplyExecute()
        {
            if (Reply is TLMessagesContainter container && container.EditMessage != null)
            {
                Aggregator.Publish(new EditMessageEventArgs(container.PreviousMessage, container.PreviousMessage.Message));
            }

            Reply = null;
        }

        #endregion

        public RelayCommand PinnedCommand => new RelayCommand(PinnedExecute);
        private async void PinnedExecute()
        {
            if (PinnedMessage != null)
            {
                await LoadMessageSliceAsync(null, PinnedMessage.Id);
            }
        }

        public RelayCommand<string> SendCommand => new RelayCommand<string>(SendMessage);
        private async void SendMessage(string args)
        {
            await SendMessageAsync(null, args != null);
        }

        public async Task SendMessageAsync(List<TLMessageEntityBase> entities, bool useReplyMarkup = false)
        {
            var messageText = Text?.Replace("\r\n", "\n");
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), null);

            message.Entities = entities != null ? new TLVector<TLMessageEntityBase>(entities) : null;
            message.HasEntities = entities != null;

            MessageHelper.PreprocessEntities(ref message);

            var channel = With as TLChannel;
            if (channel != null && channel.IsBroadcast)
            {
                message.IsSilent = IsSilent;
            }

            IEnumerable<TLMessage> forwardMessages = null;

            if (Reply != null)
            {
                if (Reply is TLMessagesContainter container)
                {
                    if (container.EditMessage != null)
                    {
                        var edit = await ProtoService.EditMessageAsync(Peer, container.EditMessage.Id, message.Message, message.Entities, null, false);
                        if (edit.IsSucceeded)
                        {
                        }
                        else
                        {

                        }

                        Reply = null;
                        return;
                    }
                    else if (container.FwdMessages != null)
                    {
                        forwardMessages = container.FwdMessages;
                        Reply = null;
                    }
                }
                else if (Reply != null)
                {
                    message.HasReplyToMsgId = true;
                    message.ReplyToMsgId = Reply.Id;
                    message.Reply = Reply;
                    Reply = null;
                }
            }

            if (string.IsNullOrWhiteSpace(messageText) == false)
            {
                var previousMessage = InsertSendingMessage(message, useReplyMarkup);
                CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
                {
                    var response = await ProtoService.SendMessageAsync(message, () => { message.State = TLMessageState.Confirmed; });
                    if (response.IsSucceeded)
                    {
                        message.RaisePropertyChanged(() => message.Media);
                    }
                    else
                    {
                        if (response.Error.CodeEquals(TLErrorCode.PEER_FLOOD))
                        {
                            var dialog = new TLMessageDialog();
                            dialog.Title = "Telegram";
                            dialog.Message = "Sorry, you can only send messages to mutual contacts at the moment.";
                            dialog.PrimaryButtonText = "More info";
                            dialog.SecondaryButtonText = "OK";

                            var confirm = await dialog.ShowAsync();
                            if (confirm == ContentDialogResult.Primary)
                            {
                                MessageHelper.HandleTelegramUrl("t.me/SpamBot");
                            }
                        }

                        return;
                    }

                    if (forwardMessages != null)
                    {
                        App.State.ForwardMessages = null;
                        await ForwardMessagesAsync(forwardMessages);
                    }
                });
            }
            else
            {
                if (forwardMessages != null)
                {
                    App.State.ForwardMessages = null;
                    await ForwardMessagesAsync(forwardMessages);
                }
            }
        }

        public async Task ForwardMessagesAsync(IEnumerable<TLMessage> forwardMessages)
        {
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            TLInputPeerBase fromPeer = null;
            var msgs = new TLVector<TLMessage>();
            var msgIds = new TLVector<int>();

            foreach (var fwdMessage in forwardMessages)
            {
                var clone = fwdMessage.Clone();
                clone.Id = 0;
                clone.HasReplyToMsgId = false;
                clone.ReplyToMsgId = null;
                clone.Date = date;
                clone.ToId = Peer.ToPeer();
                clone.RandomId = TLLong.Random();
                clone.IsOut = true;
                clone.IsPost = false;
                clone.FromId = SettingsHelper.UserId;
                clone.IsMediaUnread = Peer is TLInputPeerChannel ? true : false;
                clone.IsUnread = true;
                clone.State = TLMessageState.Sending;

                if (clone.Media == null)
                {
                    clone.HasMedia = true;
                    clone.Media = new TLMessageMediaEmpty();
                }

                if (With is TLChannel channel)
                {
                    if (channel.IsBroadcast)
                    {
                        if (!channel.IsSignatures)
                        {
                            clone.HasFromId = false;
                            clone.FromId = null;
                        }

                        if (IsSilent)
                        {
                            clone.IsSilent = true;
                        }

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

                if (fromPeer == null)
                {
                    fromPeer = fwdMessage.Parent.ToInputPeer();
                }

                if (clone.FwdFrom == null)
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

                Messages.Add(clone);
            }

            CacheService.SyncSendingMessages(msgs, null, async (_) =>
            {
                await ProtoService.ForwardMessagesAsync(Peer, fromPeer, msgIds, msgs, false);
            });
        }

        public RelayCommand<TLDocument> SendStickerCommand => new RelayCommand<TLDocument>(SendStickerExecute);
        public void SendStickerExecute(TLDocument document)
        {
            var media = new TLMessageMediaDocument { Document = document };
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message, false);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var input = new TLInputMediaDocument
                {
                    Id = new TLInputDocument
                    {
                        Id = document.Id,
                        AccessHash = document.AccessHash
                    }
                };

                await ProtoService.SendMediaAsync(Peer, input, message);
            });
        }

        public RelayCommand<TLDocument> SendGifCommand => new RelayCommand<TLDocument>(SendGifExecute);
        public void SendGifExecute(TLDocument document)
        {
            var media = new TLMessageMediaDocument { Document = document };
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message, false);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var input = new TLInputMediaDocument
                {
                    Id = new TLInputDocument
                    {
                        Id = document.Id,
                        AccessHash = document.AccessHash,
                    }
                };

                await ProtoService.SendMediaAsync(Peer, input, message);
            });
        }

        public RelayCommand<StorageFile> SendFileCommand => new RelayCommand<StorageFile>(SendFileExecute);
        private async void SendFileExecute(StorageFile file)
        {
            ObservableCollection<StorageFile> storages = null;

            if (file == null)
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                var files = await picker.PickMultipleFilesAsync();
                if (files != null)
                {
                    storages = new ObservableCollection<StorageFile>(files);
                }
            }
            else
            {
                storages = new ObservableCollection<StorageFile> { file };
            }

            if (storages != null && storages.Count > 0)
            {
                foreach (var storage in storages)
                {
                    await SendFileAsync(storage, null);
                }
            }
        }

        private async Task SendFileAsync(StorageFile file, string caption)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.dat", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            await file.CopyAndReplaceAsync(fileCache);

            var basicProps = await fileCache.GetBasicPropertiesAsync();
            var thumbnail = await FileUtils.GetFileThumbnailAsync(file);
            if (thumbnail as TLPhotoSize != null)
            {
                await SendThumbnailFileAsync(file, fileLocation, fileName, basicProps, thumbnail as TLPhotoSize, fileCache, caption);
            }
            else
            {
                var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

                var document = new TLDocument
                {
                    Id = 0,
                    AccessHash = 0,
                    Date = date,
                    Size = (int)basicProps.Size,
                    MimeType = fileCache.ContentType,
                    Attributes = new TLVector<TLDocumentAttributeBase>
                {
                    new TLDocumentAttributeFilename
                    {
                        FileName = file.Name
                    }
                }
                };

                var media = new TLMessageMediaDocument
                {
                    Document = document,
                    Caption = caption
                };

                var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

                if (Reply != null)
                {
                    message.HasReplyToMsgId = true;
                    message.ReplyToMsgId = Reply.Id;
                    message.Reply = Reply;
                    Reply = null;
                }

                var previousMessage = InsertSendingMessage(message);
                CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
                {
                    var fileId = TLLong.Random();
                    var upload = await _uploadDocumentManager.UploadFileAsync(fileId, fileName, false).AsTask(media.Upload());
                    if (upload != null)
                    {
                        var inputMedia = new TLInputMediaUploadedDocument
                        {
                            File = upload.ToInputFile(),
                            MimeType = document.MimeType,
                            Caption = media.Caption,
                            Attributes = new TLVector<TLDocumentAttributeBase>
                            {
                                new TLDocumentAttributeFilename
                                {
                                    FileName = file.Name
                                }
                            }
                        };

                        var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                        //if (result.IsSucceeded)
                        //{
                        //    var update = result.Result as TLUpdates;
                        //    if (update != null)
                        //    {
                        //        var newMessage = update.Updates.OfType<TLUpdateNewMessage>().FirstOrDefault();
                        //        if (newMessage != null)
                        //        {
                        //            var newM = newMessage.Message as TLMessage;
                        //            if (newM != null)
                        //            {
                        //                message.Media = newM.Media;
                        //                message.RaisePropertyChanged(() => message.Media);
                        //            }
                        //        }
                        //    }
                        //}
                    }
                });
            }
        }

        private async Task SendThumbnailFileAsync(StorageFile file, TLFileLocation fileLocation, string fileName, BasicProperties basicProps, TLPhotoSize thumbnail, StorageFile fileCache, string caption)
        {
            var desiredName = string.Format("{0}_{1}_{2}.jpg", thumbnail.Location.VolumeId, thumbnail.Location.LocalId, thumbnail.Location.Secret);

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var document = new TLDocument
            {
                Id = 0,
                AccessHash = 0,
                Date = date,
                Size = (int)basicProps.Size,
                MimeType = fileCache.ContentType,
                Thumb = thumbnail,
                Attributes = new TLVector<TLDocumentAttributeBase>
                {
                    new TLDocumentAttributeFilename
                    {
                        FileName = file.Name
                    }
                }
            };

            var media = new TLMessageMediaDocument
            {
                Document = document,
                Caption = caption
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var fileId = TLLong.Random();
                var upload = await _uploadDocumentManager.UploadFileAsync(fileId, fileCache.Name, false).AsTask(media.Upload());
                if (upload != null)
                {
                    var thumbFileId = TLLong.Random();
                    var thumbUpload = await _uploadDocumentManager.UploadFileAsync(thumbFileId, desiredName);
                    if (thumbUpload != null)
                    {
                        var inputMedia = new TLInputMediaUploadedThumbDocument
                        {
                            File = upload.ToInputFile(),
                            Thumb = thumbUpload.ToInputFile(),
                            MimeType = document.MimeType,
                            Caption = media.Caption,
                            Attributes = new TLVector<TLDocumentAttributeBase>
                            {
                                new TLDocumentAttributeFilename
                                {
                                    FileName = file.Name
                                }
                            }
                        };

                        var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                    }
                    //if (result.IsSucceeded)
                    //{
                    //    var update = result.Result as TLUpdates;
                    //    if (update != null)
                    //    {
                    //        var newMessage = update.Updates.OfType<TLUpdateNewMessage>().FirstOrDefault();
                    //        if (newMessage != null)
                    //        {
                    //            var newM = newMessage.Message as TLMessage;
                    //            if (newM != null)
                    //            {
                    //                message.Media = newM.Media;
                    //                message.RaisePropertyChanged(() => message.Media);
                    //            }
                    //        }
                    //    }
                    //}
                }
            });
        }

        public RelayCommand<StoragePhoto> SendPhotoCommand => new RelayCommand<StoragePhoto>(SendPhotoExecute);
        private async void SendPhotoExecute(StoragePhoto file)
        {
            ObservableCollection<StorageMedia> storages = null;

            if (file == null)
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.MediaTypes);

                var files = await picker.PickMultipleFilesAsync();
                if (files != null)
                {
                    storages = new ObservableCollection<StorageMedia>(files.Select(x => new StoragePhoto(x)));
                }
            }
            else
            {
                storages = new ObservableCollection<StorageMedia> { file };
            }

            if (storages != null && storages.Count > 0)
            {
                var dialog = new SendPhotosView { Items = storages, SelectedItem = storages[0] };
                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogBaseResult.OK)
                {
                    foreach (var storage in dialog.Items)
                    {
                        await SendPhotoAsync(storage.File, storage.Caption);
                    }
                }
            }
        }

        public async void SendPhotoDrop(ObservableCollection<StorageFile> files)
        {
            ObservableCollection<StorageMedia> storages = null;

            if (files != null)
            {
                storages = new ObservableCollection<StorageMedia>(files.Select(x => new StoragePhoto(x)));
            }

            if (storages != null && storages.Count > 0)
            {
                var dialog = new SendPhotosView { Items = storages, SelectedItem = storages[0] };
                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogBaseResult.OK)
                {
                    foreach (var storage in dialog.Items)
                    {
                        await SendPhotoAsync(storage.File, storage.Caption);
                    }
                }
            }
        }

        private async Task SendPhotoAsync(StorageFile file, string caption)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.jpg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            var fileScale = await ImageHelper.ScaleJpegAsync(file, fileCache, 1280, 0.77);
            if (fileScale == null && Path.GetExtension(file.Name).Equals(".gif"))
            {
                // TODO: animated gif!
                await fileCache.DeleteAsync();
                await SendGifAsync(file, caption);
                return;
            }

            var basicProps = await fileScale.GetBasicPropertiesAsync();
            var imageProps = await fileScale.Properties.GetImagePropertiesAsync();

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var photoSize = new TLPhotoSize
            {
                Type = "y",
                W = (int)imageProps.Width,
                H = (int)imageProps.Height,
                Location = fileLocation,
                Size = (int)basicProps.Size
            };

            var photo = new TLPhoto
            {
                Id = 0,
                AccessHash = 0,
                Date = date,
                Sizes = new TLVector<TLPhotoSizeBase> { photoSize }
            };

            var media = new TLMessageMediaPhoto
            {
                Photo = photo,
                Caption = caption
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var fileId = TLLong.Random();
                var upload = await _uploadFileManager.UploadFileAsync(fileId, fileCache.Name, false).AsTask(media.Upload());
                if (upload != null)
                {
                    var inputMedia = new TLInputMediaUploadedPhoto
                    {
                        Caption = media.Caption,
                        File = upload.ToInputFile()
                    };

                    var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                }
            });
        }

        private async Task SendGifAsync(StorageFile file, string caption)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.gif", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            await file.CopyAndReplaceAsync(fileCache);

            var basicProps = await fileCache.GetBasicPropertiesAsync();
            var imageProps = await fileCache.Properties.GetImagePropertiesAsync();

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var media = new TLMessageMediaDocument
            {
                // TODO: Document = ...
                Caption = caption
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var fileId = TLLong.Random();
                var upload = await _uploadDocumentManager.UploadFileAsync(fileId, fileName, false).AsTask(media.Upload());
                if (upload != null)
                {
                    var inputMedia = new TLInputMediaUploadedDocument
                    {
                        File = upload.ToInputFile(),
                        MimeType = "image/gif",
                        Caption = media.Caption,
                        Attributes = new TLVector<TLDocumentAttributeBase>
                        {
                            new TLDocumentAttributeAnimated(),
                            new TLDocumentAttributeImageSize
                            {
                                W = (int)imageProps.Width,
                                H = (int)imageProps.Height,
                            }
                        }
                    };

                    var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                }
            });
        }

        public async Task SendAudioAsync(StorageFile file, int duration, bool voice, string title, string performer, string caption)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.ogg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            await file.CopyAndReplaceAsync(fileCache);

            var basicProps = await fileCache.GetBasicPropertiesAsync();
            var imageProps = await fileCache.Properties.GetImagePropertiesAsync();

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            var media = new TLMessageMediaDocument
            {
                Caption = caption,
                Document = new TLDocument
                {
                    Id = TLLong.Random(),
                    AccessHash = TLLong.Random(),
                    Date = date,
                    MimeType = "audio/ogg",
                    Size = (int)basicProps.Size,
                    Thumb = new TLPhotoSizeEmpty
                    {
                        Type = string.Empty
                    },
                    Version = 0,
                    DCId = 0,
                    Attributes = new TLVector<TLDocumentAttributeBase>
                    {
                        new TLDocumentAttributeAudio
                        {
                            IsVoice = voice,
                            Duration = duration,
                            Title = title,
                            Performer = performer
                        }
                    }
                }
            };

            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            var previousMessage = InsertSendingMessage(message);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                var fileId = TLLong.Random();
                var upload = await _uploadDocumentManager.UploadFileAsync(fileId, fileName, false).AsTask(media.Upload());
                if (upload != null)
                {
                    var inputMedia = new TLInputMediaUploadedDocument
                    {
                        File = upload.ToInputFile(),
                        MimeType = "audio/ogg",
                        Caption = media.Caption,
                        Attributes = new TLVector<TLDocumentAttributeBase>
                        {
                            new TLDocumentAttributeAudio
                            {
                                IsVoice = voice,
                                Duration = duration,
                                Title = title,
                                Performer = performer
                            }
                        }
                    };

                    var result = await ProtoService.SendMediaAsync(Peer, inputMedia, message);
                }
            });
        }

        private TLMessageBase InsertSendingMessage(TLMessage message, bool useReplyMarkup = false)
        {
            TLMessageBase result;
            if (Messages.Count > 0)
            {
                if (useReplyMarkup && _replyMarkupMessage != null)
                {
                    var chat = With as TLChatBase;
                    if (chat != null)
                    {
                        message.ReplyToMsgId = _replyMarkupMessage.Id;
                        message.Reply = _replyMarkupMessage;
                    }

                    Execute.BeginOnUIThread(() =>
                    {
                        if (Reply != null)
                        {
                            Reply = null;
                            SetReplyMarkup(null);
                        }
                    });
                }

                var messagesContainer = Reply as TLMessagesContainter;
                if (Reply != null)
                {
                    if (Reply.Id != 0)
                    {
                        message.ReplyToMsgId = Reply.Id;
                        message.Reply = Reply;
                    }
                    else if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                    {
                        message.Reply = Reply;
                    }

                    var replyMessage = Reply as TLMessage;
                    if (replyMessage != null)
                    {
                        //var replyMarkup = replyMessage.ReplyMarkup;
                        //if (replyMarkup != null)
                        //{
                        //    replyMarkup.HasResponse = true;
                        //}
                    }

                    Execute.BeginOnUIThread(delegate
                    {
                        Reply = null;
                    });
                }

                result = Messages.LastOrDefault();
                Messages.Add(message);

                if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                {
                    foreach (var fwdMessage in messagesContainer.FwdMessages)
                    {
                        Messages.Insert(0, fwdMessage);
                    }
                }

                //for (int i = 1; i < Messages.Count; i++)
                //{
                //    var serviceMessage = Messages[i] as TLMessageService;
                //    if (serviceMessage != null)
                //    {
                //        var unreadAction = serviceMessage.Action as TLMessageActionUnreadMessages;
                //        if (unreadAction != null)
                //        {
                //            Messages.RemoveAt(i);
                //            break;
                //        }
                //    }
                //}
            }
            else
            {
                var messagesContainer = Reply as TLMessagesContainter;
                if (Reply != null)
                {
                    if (Reply.Id != 0)
                    {
                        message.HasReplyToMsgId = true;
                        message.ReplyToMsgId = Reply.Id;
                        message.Reply = Reply;
                    }
                    else if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                    {
                        message.Reply = Reply;
                    }
                    Reply = null;
                }

                Messages.Clear();
                Messages.Add(message);

                var history = CacheService.GetHistory(Peer.ToPeer(), 15);
                result = history.FirstOrDefault();

                for (int j = 0; j < history.Count; j++)
                {
                    Messages.Add(history[j]);
                }

                //if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                //{
                //    foreach (var fwdMessage in messagesContainer.FwdMessages)
                //    {
                //        Messages.Insert(0, fwdMessage);
                //    }
                //}

                //for (int k = 1; k < Messages.Count; k++)
                //{
                //    var serviceMessage = Messages[k] as TLMessageService;
                //    if (serviceMessage != null)
                //    {
                //        var unreadAction = serviceMessage.Action as TLMessageActionUnreadMessages;
                //        if (unreadAction != null)
                //        {
                //            Messages.RemoveAt(k);
                //            break;
                //        }
                //    }
                //}
            }
            return result;
        }

        #region Toggle mute

        public RelayCommand ToggleMuteCommand => new RelayCommand(ToggleMuteExecute);
        private async void ToggleMuteExecute()
        {
            var channel = With as TLChannel;
            if (channel != null)
            {
                var notifySettings = channel.NotifySettings as TLPeerNotifySettings;
                if (notifySettings != null)
                {
                    var muteUntil = notifySettings.MuteUntil == int.MaxValue ? 0 : int.MaxValue;
                    var settings = new TLInputPeerNotifySettings
                    {
                        MuteUntil = muteUntil,
                        IsShowPreviews = notifySettings.IsShowPreviews,
                        IsSilent = notifySettings.IsSilent,
                        Sound = notifySettings.Sound
                    };

                    var response = await ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyPeer { Peer = Peer }, settings);
                    if (response.IsSucceeded)
                    {
                        notifySettings.MuteUntil = muteUntil;
                        channel.RaisePropertyChanged(() => channel.NotifySettings);

                        var dialog = CacheService.GetDialog(Peer.ToPeer());
                        if (dialog != null)
                        {
                            dialog.NotifySettings = channel.NotifySettings;
                            dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                            dialog.RaisePropertyChanged(() => dialog.Self);

                            var dialogChannel = dialog.With as TLChannel;
                            if (dialogChannel != null)
                            {
                                dialogChannel.NotifySettings = channel.NotifySettings;
                            }
                        }

                        CacheService.Commit();
                        RaisePropertyChanged(() => With);
                    }
                }
            }
        }

        #endregion

        #region Toggle silent

        public RelayCommand ToggleSilentCommand => new RelayCommand(ToggleSilentExecute);
        private async void ToggleSilentExecute()
        {
            var channel = With as TLChannel;
            if (channel != null)
            {
                var notifySettings = channel.NotifySettings as TLPeerNotifySettings;
                if (notifySettings != null)
                {
                    var silent = !notifySettings.IsSilent;
                    var settings = new TLInputPeerNotifySettings
                    {
                        MuteUntil = notifySettings.MuteUntil,
                        IsShowPreviews = notifySettings.IsShowPreviews,
                        IsSilent = silent,
                        Sound = notifySettings.Sound
                    };

                    var response = await ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyPeer { Peer = Peer }, settings);
                    if (response.IsSucceeded)
                    {
                        notifySettings.IsSilent = silent;
                        channel.RaisePropertyChanged(() => channel.NotifySettings);

                        var dialog = CacheService.GetDialog(Peer.ToPeer());
                        if (dialog != null)
                        {
                            dialog.NotifySettings = channel.NotifySettings;
                            dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                            dialog.RaisePropertyChanged(() => dialog.Self);

                            var dialogChannel = dialog.With as TLChannel;
                            if (dialogChannel != null)
                            {
                                dialogChannel.NotifySettings = channel.NotifySettings;
                            }
                        }

                        CacheService.Commit();
                        RaisePropertyChanged(() => With);
                    }
                }
            }
        }

        #endregion

        #region Report Spam

        public RelayCommand HideReportSpamCommand => new RelayCommand(HideReportSpamExecute);
        private async void HideReportSpamExecute()
        {
            var response = await ProtoService.HideReportSpamAsync(Peer);
            if (response.IsSucceeded)
            {
                IsReportSpam = false;
            }
        }

        public RelayCommand ReportSpamCommand => new RelayCommand(ReportSpamExecute);
        private async void ReportSpamExecute()
        {
            if (IsReportSpam)
            {
                var response = await ProtoService.ReportSpamAsync(Peer);
                if (response.IsSucceeded)
                {

                }
            }
        }

        #endregion

        #region Stickers

        public RelayCommand OpenStickersCommand => new RelayCommand(OpenStickersExecute);
        private void OpenStickersExecute()
        {
            _stickers.SyncStickers();
            _stickers.SyncGifs();
        }

        #endregion

    }

    public class MessageCollection : ObservableCollection<TLMessageBase>
    {
        protected override void InsertItem(int index, TLMessageBase item)
        {
            base.InsertItem(index, item);

            var previous = index > 0 ? this[index - 1] : null;
            var next = index < Count - 1 ? this[index + 1] : null;

            //if (next is TLMessageEmpty)
            //{
            //    next = index > 1 ? this[index - 2] : null;
            //}
            //if (previous is TLMessageEmpty)
            //{
            //    previous = index < Count - 2 ? this[index + 2] : null;
            //}

            UpdateSeparatorOnInsert(item, next, index);
            UpdateSeparatorOnInsert(previous, item, index - 1);

            UpdateAttach(next, item, index + 1);
            UpdateAttach(item, previous, index);
        }

        protected override void RemoveItem(int index)
        {
            var next = index > 0 ? this[index - 1] : null;
            var previous = index < Count - 1 ? this[index + 1] : null;

            UpdateAttach(previous, next, index + 1);

            base.RemoveItem(index);

            UpdateSeparatorOnRemove(next, previous, index);
        }

        private void UpdateSeparatorOnInsert(TLMessageBase item, TLMessageBase previous, int index)
        {
            if (item != null && previous != null)
            {
                var itemDate = Utils.UnixTimestampToDateTime(item.Date);
                var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
                if (previousDate.Date != itemDate.Date)
                {
                    var timestamp = (int)Utils.DateTimeToUnixTimestamp(previousDate.Date);
                    var service = new TLMessageService
                    {
                        Date = timestamp,
                        FromId = SettingsHelper.UserId,
                        HasFromId = true,
                        Action = new TLMessageActionDate
                        {
                            Date = timestamp
                        }
                    };

                    base.InsertItem(index + 1, service);
                }
            }
        }

        private void UpdateSeparatorOnRemove(TLMessageBase next, TLMessageBase previous, int index)
        {
            if (next is TLMessageService && previous != null)
            {
                var action = ((TLMessageService)next).Action as TLMessageActionDate;
                if (action != null)
                {
                    var itemDate = Utils.UnixTimestampToDateTime(action.Date);
                    var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
                    if (previousDate.Date != itemDate.Date)
                    {
                        base.RemoveItem(index - 1);
                    }
                }
            }
            else if (next is TLMessageService && previous == null)
            {
                var action = ((TLMessageService)next).Action as TLMessageActionDate;
                if (action != null)
                {
                    base.RemoveItem(index - 1);
                }
            }
        }

        private void UpdateAttach(TLMessageBase item, TLMessageBase previous, int index)
        {
            if (item == null)
            {
                if (previous != null)
                {
                    previous.IsLast = true;
                }

                return;
            }

            var oldFirst = item.IsFirst;
            var isItemPost = false;
            if (item is TLMessage) isItemPost = ((TLMessage)item).IsPost;

            if (!isItemPost)
            {
                var attach = false;
                if (previous != null)
                {
                    var isPreviousPost = false;
                    if (previous is TLMessage) isPreviousPost = ((TLMessage)previous).IsPost;

                    attach = !isPreviousPost &&
                             !(previous is TLMessageService && !(((TLMessageService)previous).Action is TLMessageActionPhoneCall)) &&
                             !(previous is TLMessageEmpty) &&
                             previous.FromId == item.FromId &&
                             item.Date - previous.Date < 900;
                }

                item.IsFirst = !attach;

                if (previous != null)
                {
                    previous.IsLast = item.IsFirst;
                }
            }
            else
            {
                item.IsFirst = true;

                if (previous != null)
                {
                    previous.IsLast = false;
                }
            }

            //if (item.IsFirst && item is TLMessage)
            //{
            //    var message = item as TLMessage;
            //    if (message != null && !message.IsPost && !message.IsOut)
            //    {
            //        base.InsertItem(index, new TLMessageEmpty { Date = item.Date, FromId = item.FromId, Id = item.Id, ToId = item.ToId });
            //    }
            //}

            //if (!item.IsFirst && oldFirst)
            //{
            //    var next = index > 0 ? this[index - 1] : null;
            //    if (next is TLMessageEmpty)
            //    {
            //        Remove(item);
            //    }
            //}
        }

        //public ObservableCollection<MessageGroup> Groups { get; private set; } = new ObservableCollection<MessageGroup>();

        //protected override void InsertItem(int index, TLMessageBase item)
        //{
        //    base.InsertItem(index, item);

        //    var group = GroupForIndex(index);
        //    if (group == null || group?.FromId != item.FromId)
        //    {
        //        group = new MessageGroup(this, item.From, item.FromId, item.ToId, item is TLMessage ? ((TLMessage)item).IsOut : false);
        //        Groups.Insert(index == 0 ? 0 : Groups.Count, group); // TODO: should not be 0 all the time
        //    }

        //    group.Insert(Math.Max(0, index - group.FirstIndex), item);
        //}

        //protected override void RemoveItem(int index)
        //{
        //    base.RemoveItem(index);

        //    var group = GroupForIndex(index);
        //    if (group != null)
        //    {
        //        group.RemoveAt(index - group.FirstIndex);
        //    }
        //}

        //private MessageGroup GroupForIndex(int index)
        //{
        //    if (index == 0)
        //    {
        //        return Groups.FirstOrDefault();
        //    }
        //    else if (index == Count - 1)
        //    {
        //        return Groups.LastOrDefault();
        //    }
        //    else
        //    {
        //        return Groups.FirstOrDefault(x => x.FirstIndex >= index && x.LastIndex <= index);
        //    }
        //}
    }

    //public class MessageGroup : ObservableCollection<TLMessageBase>
    //{
    //    private ObservableCollection<MessageGroup> _parent;

    //    public MessageGroup(MessageCollection parent, TLUser from, int? fromId, TLPeerBase toId, bool isOut)
    //    {
    //        _parent = parent.Groups;

    //        From = from;
    //        if (fromId == null)
    //            FromId = 33303409;
    //        FromId = fromId;
    //        ToId = toId;
    //        IsOut = isOut;
    //    }

    //    public TLUser From { get; private set; }

    //    public int? FromId { get; private set; }

    //    public TLPeerBase ToId { get; private set; }

    //    public bool IsOut { get; private set; }

    //    public int FirstIndex
    //    {
    //        get
    //        {
    //            var count = 0;
    //            var index = _parent.IndexOf(this);
    //            if (index > 0)
    //            {
    //                count = _parent[index - 1].LastIndex + 1;
    //            }

    //            return count;
    //        }
    //    }

    //    public int LastIndex
    //    {
    //        get
    //        {
    //            return FirstIndex + Math.Max(0, Count - 1);
    //        }
    //    }

    //    protected override void InsertItem(int index, TLMessageBase item)
    //    {
    //        // TODO: experimental
    //        if (index == 0)
    //        {
    //            if (Count > 0)
    //                this[0].IsFirst = false;

    //            item.IsFirst = true;
    //        }

    //        base.InsertItem(index, item);
    //    }

    //    protected override void RemoveItem(int index)
    //    {
    //        base.RemoveItem(index);
    //    }
    //}
}
