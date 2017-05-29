using System;
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
using Unigram.Views;
using Telegram.Api.TL.Methods.Phone;
using Windows.ApplicationModel.Calls;
using Unigram.Tasks;
using Windows.Media.Effects;
using Windows.Media.Transcoding;
using Windows.Media.MediaProperties;
using Telegram.Api.Services.Cache.EventArgs;
using Windows.Foundation.Metadata;
using Windows.UI.Text;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel : UnigramViewModelBase
    {
        public MessageCollection Messages { get; private set; } = new MessageCollection();

        private List<TLMessageCommonBase> _selectedMessages = new List<TLMessageCommonBase>();
        public List<TLMessageCommonBase> SelectedMessages
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
        private readonly ILocationService _locationService;
        private readonly IUploadFileManager _uploadFileManager;
        private readonly IUploadAudioManager _uploadAudioManager;
        private readonly IUploadDocumentManager _uploadDocumentManager;
        private readonly IUploadVideoManager _uploadVideoManager;

        public int participantCount = 0;
        public int online = 0;

        public DialogViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager, IUploadAudioManager uploadAudioManager, IUploadDocumentManager uploadDocumentManager, IUploadVideoManager uploadVideoManager, IStickersService stickersService, ILocationService locationService, DialogStickersViewModel stickers)
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;
            _uploadAudioManager = uploadAudioManager;
            _uploadDocumentManager = uploadDocumentManager;
            _uploadVideoManager = uploadVideoManager;
            _stickersService = stickersService;
            _locationService = locationService;

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

        private object _full;
        public object Full
        {
            get
            {
                return _full;
            }
            set
            {
                Set(ref _full, value);
                RaisePropertyChanged(() => With);
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

        private bool _isPhoneCallsAvailable;
        public bool IsPhoneCallsAvailable
        {
            get
            {
                return _isPhoneCallsAvailable;
            }
            set
            {
                Set(ref _isPhoneCallsAvailable, value);
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

        public BubbleTextBox TextBox { get; set; }

        public void SetText(string text, TLVector<TLMessageEntityBase> entities = null, bool focus = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                TextBox.Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang1040{\fonttbl{\f0\fnil Segoe UI;}}{\*\generator Riched20 10.0.14393}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs23\lang1033}");
            }
            else
            {
                TextBox.SetText(text, entities);
            }

            if (focus)
            {
                TextBox.Focus(FocusState.Keyboard);
            }
        }

        public string GetText()
        {
            TextBox.Document.GetText(TextGetOptions.NoHidden, out string text);
            return text;
        }

        public bool IsFirstSliceLoaded { get; set; }

        private bool _isLoadingNextSlice;
        private bool _isLoadingPreviousSlice;
        private Stack<int> _goBackStack = new Stack<int>();

        public async Task LoadNextSliceAsync()
        {
            if (_isLoadingNextSlice || _isLoadingPreviousSlice || _peer == null) return;
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

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, 0, 0, maxId, 50);
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
            if (_isLoadingNextSlice || _isLoadingPreviousSlice || _peer == null) return;
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

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, -limit, 0, maxId, limit);
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
            if (_isLoadingNextSlice || _isLoadingPreviousSlice || _peer == null) return;
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

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, offset, 0, maxId, limit);
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

        public async Task LoadDateSliceAsync(int dateOffset)
        {
            var offset = -1;
            var limit = 1;

            var obj = new TLMessagesGetHistory { Peer = Peer, OffsetId = 0, OffsetDate = dateOffset - 1, AddOffset = offset, Limit = limit, MaxId = 0, MinId = 0 };
            ProtoService.SendRequestAsync<TLMessagesMessagesBase>("messages.getHistory", obj, result =>
            {
                Execute.BeginOnUIThread(async () =>
                {
                    await LoadMessageSliceAsync(null, result.Messages[0].Id);
                });
            });

            //var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, offset, dateOffset, 0, limit);
            //if (result.IsSucceeded)
            //{
            //    await LoadMessageSliceAsync(null, result.Result.Messages[0].Id);
            //}
        }


        public async Task LoadFirstSliceAsync()
        {
            if (_isLoadingNextSlice || _isLoadingPreviousSlice || _peer == null) return;
            _isLoadingNextSlice = true;
            _isLoadingPreviousSlice = true;

            UpdatingScrollMode = _currentDialog?.UnreadCount > 0 ? ItemsUpdatingScrollMode.KeepItemsInView : ItemsUpdatingScrollMode.KeepLastItemInView;

            Debug.WriteLine("DialogViewModel: LoadFirstSliceAsync");

            var lastRead = true;

            //var maxId = _currentDialog?.UnreadCount > 0 ? _currentDialog.ReadInboxMaxId : int.MaxValue;
            var readMaxId = With as ITLReadMaxId;

            //var maxId = readMaxId.ReadInboxMaxId > 0 ? readMaxId.ReadInboxMaxId : int.MaxValue;
            //var offset = _currentDialog?.UnreadCount > 0 && maxId > 0 ? -51 : 0;

            var maxId = _currentDialog?.UnreadCount > 0 ? _currentDialog.ReadInboxMaxId : int.MaxValue;
            var offset = _currentDialog?.UnreadCount > 0 && maxId > 0 ? -16 : 0;
            var limit = 15;

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, offset, 0, maxId, limit);
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

            _isLoadingNextSlice = false;
            _isLoadingPreviousSlice = false;
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
            if (participant == null)
            {
                return;
            }

            Peer = participant.ToInputPeer();
            With = participant;

            Aggregator.Subscribe(this);

            if (tuple != null)
            {
                await LoadMessageSliceAsync(null, tuple.Item2);
            }
            else
            {
                await LoadFirstSliceAsync();
            }

            if (participant is TLUser user)
            {
                var full = CacheService.GetFullUser(user.Id);
                if (full == null)
                {
                    var response = await ProtoService.GetFullUserAsync(new TLInputUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 });
                    if (response.IsSucceeded)
                    {
                        full = response.Result;
                    }
                }

                if (full != null)
                {
                    Full = full;
                    IsPhoneCallsAvailable = full.IsPhoneCallsAvailable && ApiInformation.IsApiContractPresent("Windows.ApplicationModel.Calls.CallsVoipContract", 1);

                    if (user.IsBot && full.HasBotInfo)
                    {
                        UnfilteredBotCommands = full.BotInfo.Commands.Select(x => Tuple.Create(user, x)).ToList();
                    }
                }
            }
            else if (participant is TLChannel channel)
            {
                if (channel.IsRestricted)
                {
                    var reason = channel.ExtractRestrictionReason();
                    if (reason != null)
                    {
                        NavigationService.GoBack();

                        await TLMessageDialog.ShowAsync(reason, "Sorry", "OK");
                        return;
                    }
                }

                IsPhoneCallsAvailable = false;

                var full = CacheService.GetFullChat(channel.Id) as TLChannelFull;
                if (full == null)
                {
                    var response = await ProtoService.GetFullChannelAsync(channel.ToInputChannel());
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChannelFull;
                    }
                }

                if (full != null)
                {
                    Full = full;

                    var commands = new List<Tuple<TLUser, TLBotCommand>>();

                    foreach (var info in full.BotInfo)
                    {
                        var bot = CacheService.GetUser(info.UserId) as TLUser;
                        if (bot != null)
                        {
                            commands.AddRange(info.Commands.Select(x => Tuple.Create(bot, x)));
                        }
                    }

                    UnfilteredBotCommands = commands;

                    var channelFull = full as TLChannelFull;
                    if (channelFull.HasPinnedMsgId)
                    {
                        var update = true;

                        var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
                        if (appData.Values.TryGetValue("Pinned" + channel.Id, out object pinnedObj))
                        {
                            var pinnedId = (int)pinnedObj;
                            if (pinnedId == channelFull.PinnedMsgId)
                            {
                                update = false;
                            }
                        }

                        var pinned = CacheService.GetMessage(channelFull.PinnedMsgId, channel.Id);
                        if (pinned == null && update)
                        {
                            var y = await ProtoService.GetMessagesAsync(channel.ToInputChannel(), new TLVector<int>() { channelFull.PinnedMsgId ?? 0 });
                            if (y.IsSucceeded)
                            {
                                pinned = y.Result.Messages.FirstOrDefault();
                            }
                        }

                        if (pinned != null && update)
                        {
                            PinnedMessage = pinned;
                        }
                        else
                        {
                            PinnedMessage = null;
                        }
                    }
                }

            }
            else if (participant is TLChat chat)
            {
                IsPhoneCallsAvailable = false;

                var full = CacheService.GetFullChat(chat.Id) as TLChatFull;
                if (full == null)
                {
                    var response = await ProtoService.GetFullChatAsync(chat.Id);
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChatFull;
                    }
                }

                if (full != null)
                {
                    Full = full;

                    var commands = new List<Tuple<TLUser, TLBotCommand>>();

                    foreach (var info in full.BotInfo)
                    {
                        var bot = CacheService.GetUser(info.UserId) as TLUser;
                        if (bot != null)
                        {
                            commands.AddRange(info.Commands.Select(x => Tuple.Create(bot, x)));
                        }
                    }

                    UnfilteredBotCommands = commands;
                }
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
            else if (participant is TLChatForbidden forbiddenChat)
            {
                With = forbiddenChat;
                Peer = new TLInputPeerChat { ChatId = forbiddenChat.Id };
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

            if (dialog != null && Messages.Count > 0)
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
                readPeer.ReadInboxMaxId = dialog.TopMessage;
                dialog.ReadInboxMaxId = dialog.TopMessage;
                dialog.UnreadCount = dialog.UnreadCount - unread;
                dialog.RaisePropertyChanged(() => dialog.UnreadCount);
            }

            //StickersRecent();
            //GifsSaved();

            //var file = await KnownFolders.SavedPictures.CreateFileAsync("TEST.TXT", CreationCollisionOption.GenerateUniqueName);
            //await FileIO.WriteTextAsync(file, DateTime.Now.ToString());

            if (App.InMemoryState.ForwardMessages != null)
            {
                Reply = new TLMessagesContainter { FwdMessages = new TLVector<TLMessage>(App.InMemoryState.ForwardMessages) };
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

        public List<Tuple<TLUser, TLBotCommand>> UnfilteredBotCommands { get; private set; }

        private List<Tuple<TLUser, TLBotCommand>> _botCommands;
        public List<Tuple<TLUser, TLBotCommand>> BotCommands
        {
            get
            {
                return _botCommands;
            }
            set
            {
                Set(ref _botCommands, value);
            }
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            SaveDraft();

            //var file = await KnownFolders.SavedPictures.CreateFileAsync("TEST.TXT", CreationCollisionOption.GenerateUniqueName);
            //await FileIO.WriteTextAsync(file, DateTime.Now.ToString());
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        private ITLDialogWith GetParticipant(TLPeerBase peer)
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

        public void SaveDraft()
        {
            var messageText = GetText().Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var reply = new int?();

            if (Reply is TLMessagesContainter container)
            {
            }
            else if (Reply != null)
            {
                reply = Reply.Id;
            }

            TLDraftMessageBase draft = null;
            if (reply.HasValue || !string.IsNullOrWhiteSpace(messageText))
            {
                draft = new TLDraftMessage { Message = messageText, Date = date, ReplyToMsgId = reply };
            }
            else if (_currentDialog != null && _currentDialog.HasDraft && _currentDialog.Draft is TLDraftMessage)
            {
                draft = new TLDraftMessageEmpty();
            }

            if (draft != null)
            {
                ProtoService.SaveDraftAsync(Peer, draft, result =>
                {
                    if (_currentDialog != null)
                    {
                        _currentDialog.Draft = draft;
                        _currentDialog.HasDraft = draft != null;
                    }

                    Aggregator.Publish(new TLUpdateDraftMessage { Peer = Peer.ToPeer(), Draft = draft });
                });
            }
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
                        App.InMemoryState.ForwardMessages = null;
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
                SetText(null);
                //Aggregator.Publish(new EditMessageEventArgs(container.PreviousMessage, container.PreviousMessage.Message));
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
            await SendMessageAsync(args, null, args != null);
        }

        public async Task SendMessageAsync(string text, List<TLMessageEntityBase> entities, bool useReplyMarkup = false)
        {
            if (Peer == null)
            {
                await new MessageDialog("Something went wrong. Close the chat and open it again.").ShowQueuedAsync();
                return;
            }

            var messageText = text.Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
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
                        if (response.Error.TypeEquals(TLErrorType.PEER_FLOOD))
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
                        App.InMemoryState.ForwardMessages = null;
                        await ForwardMessagesAsync(forwardMessages);
                    }
                });
            }
            else
            {
                if (forwardMessages != null)
                {
                    App.InMemoryState.ForwardMessages = null;
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
                clone.HasReplyMarkup = false;
                clone.ReplyMarkup = null;
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

        #region Join channel

        public RelayCommand JoinChannelCommand => new RelayCommand(JoinChannelExecute);
        private async void JoinChannelExecute()
        {
            var channel = With as TLChannel;
            if (channel == null)
            {
                return;
            }

            var response = await ProtoService.JoinChannelAsync(channel);
            if (response.IsSucceeded)
            {
                RaisePropertyChanged(() => With);

                if (channel.IsMegaGroup)
                {
                    if (response.Result is TLUpdates update)
                    {
                        var newChannelMessage = update.Updates.FirstOrDefault(x => x is TLUpdateNewChannelMessage) as TLUpdateNewChannelMessage;
                        if (newChannelMessage != null)
                        {
                            Messages.Add(newChannelMessage.Message);

                            if (_currentDialog == null)
                            {
                                _currentDialog = CacheService.GetDialog(channel.ToPeer());
                            }

                            Aggregator.Publish(new TopMessageUpdatedEventArgs(this._currentDialog, newChannelMessage.Message));
                        }
                        else
                        {
                            var message = new TLMessageService
                            {
                                RandomId = TLLong.Random(),
                                FromId = SettingsHelper.UserId,
                                ToId = new TLPeerChannel
                                {
                                    Id = channel.Id
                                },
                                State = TLMessageState.Confirmed,
                                IsOut = true,
                                IsUnread = false,
                                Date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, System.DateTime.Now),
                                Action = new TLMessageActionChatAddUser
                                {
                                    Users = new TLVector<int> { SettingsHelper.UserId }
                                }
                            };

                            CacheService.SyncMessage(message, true, true, cachedMessage =>
                            {
                                Messages.Add(cachedMessage);
                            });
                        }
                    }
                }
            }
        }

        #endregion

        #region Toggle mute

        public RelayCommand ToggleMuteCommand => new RelayCommand(ToggleMuteExecute);
        private async void ToggleMuteExecute()
        {
            var channel = With as TLChannel;
            if (channel != null && _currentDialog != null)
            {
                var notifySettings = _currentDialog.NotifySettings as TLPeerNotifySettings;
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
                        channel.RaisePropertyChanged(() => _currentDialog.NotifySettings);

                        var dialog = CacheService.GetDialog(Peer.ToPeer());
                        if (dialog != null)
                        {
                            dialog.NotifySettings = _currentDialog.NotifySettings;
                            dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                            dialog.RaisePropertyChanged(() => dialog.Self);

                            var chatFull = CacheService.GetFullChat(channel.Id);
                            if (chatFull != null)
                            {
                                chatFull.NotifySettings = _currentDialog.NotifySettings;
                                chatFull.RaisePropertyChanged(() => chatFull.NotifySettings);
                            }

                            // TODO: 06/05/2017
                            //var dialogChannel = dialog.With as TLChannel;
                            //if (dialogChannel != null)
                            //{
                            //    dialogChannel.NotifySettings = channel.NotifySettings;
                            //}
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
            if (channel != null && _currentDialog != null)
            {
                var notifySettings = _currentDialog.NotifySettings as TLPeerNotifySettings;
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
                        channel.RaisePropertyChanged(() => _currentDialog.NotifySettings);

                        var dialog = CacheService.GetDialog(Peer.ToPeer());
                        if (dialog != null)
                        {
                            dialog.NotifySettings = _currentDialog.NotifySettings;
                            dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                            dialog.RaisePropertyChanged(() => dialog.Self);

                            var chatFull = CacheService.GetFullChat(channel.Id);
                            if (chatFull != null)
                            {
                                chatFull.NotifySettings = _currentDialog.NotifySettings;
                                chatFull.RaisePropertyChanged(() => chatFull.NotifySettings);
                            }

                            // TODO: 06/05/2017
                            //var dialogChannel = dialog.With as TLChannel;
                            //if (dialogChannel != null)
                            //{
                            //    dialogChannel.NotifySettings = channel.NotifySettings;
                            //}
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

        #region Delete and Exit

        public RelayCommand DialogDeleteCommand => new RelayCommand(DialogDeleteExecute);
        private void DialogDeleteExecute()
        {
            if (_currentDialog != null)
            {
                UnigramContainer.Current.ResolveType<MainViewModel>().Dialogs.DialogDeleteCommand.Execute(_currentDialog);
            }
        }

        #endregion

        #region Call

        public RelayCommand CallCommand => new RelayCommand(CallExecute);
        private async void CallExecute()
        {
            var user = With as TLUser;
            if (user == null)
            {
                return;
            }

            try
            {
                var coordinator = VoipCallCoordinator.GetDefault();
                var result = await coordinator.ReserveCallResourcesAsync("Unigram.Tasks.VoIPCallTask");
                if (result == VoipPhoneCallResourceReservationStatus.Success)
                {
                    await VoIPConnection.Current.SendRequestAsync("voip.startCall", user);
                }
            }
            catch
            {
                await TLMessageDialog.ShowAsync("Something went wrong. Please, try to close and relaunch the app.", "Unigram", "OK");
            }
        }

        #endregion

        #region Unpin message

        public RelayCommand UnpinMessageCommand => new RelayCommand(UnpinMessageExecute);
        private async void UnpinMessageExecute()
        {
            if (_pinnedMessage == null)
            {
                return;
            }

            var channel = With as TLChannel;
            if (channel == null)
            {
                return;
            }

            if (channel.IsCreator || channel.IsEditor || channel.IsModerator)
            {
                var confirm = await TLMessageDialog.ShowAsync("Would you like to unpin this message?", "Unigram", "Yes", "No");
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await ProtoService.UpdatePinnedMessageAsync(false, channel.ToInputChannel(), 0);
                    if (response.IsSucceeded)
                    {
                        PinnedMessage = null;
                    }
                    else
                    {
                        // TODO
                    }
                }
            }
            else
            {
                var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
                appData.Values["Pinned" + channel.Id] = _pinnedMessage.Id;

                PinnedMessage = null;
            }
        }

        #endregion

        #region Unblock

        public RelayCommand UnblockCommand => new RelayCommand(UnblockExecute);
        private async void UnblockExecute()
        {
            var user = _with as TLUser;
            if (user == null)
            {
                return;
            }

            var result = await ProtoService.UnblockAsync(user.ToInputUser());
            if (result.IsSucceeded)
            {
                if (Full is TLUserFull full)
                {
                    full.IsBlocked = false;
                }

                RaisePropertyChanged(() => With);
                RaisePropertyChanged(() => Full);

                CacheService.Commit();
                Aggregator.Publish(new TLUpdateUserBlocked { UserId = user.Id, Blocked = false });
            }
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
