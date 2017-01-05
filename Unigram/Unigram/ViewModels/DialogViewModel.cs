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

namespace Unigram.ViewModels
{
    public partial class DialogViewModel : UnigramViewModelBase
    {
        public MessageCollection Messages { get; private set; } = new MessageCollection();

        public Brush PlaceHolderColor { get; internal set; }
        public string DialogTitle;
        public string LastSeen;
        public Visibility pinnedMessageVisible = Visibility.Collapsed;
        public Visibility LastSeenVisible;
        public string debug;













        private readonly IUploadFileManager _uploadFileManager;
        private readonly IUploadAudioManager _uploadAudioManager;
        private readonly IUploadDocumentManager _uploadDocumentManager;
        private readonly IUploadVideoManager _uploadVideoManager;

        public DialogViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager, IUploadAudioManager uploadAudioManager, IUploadDocumentManager uploadDocumentManager, IUploadVideoManager uploadVideoManager)
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;
            _uploadAudioManager = uploadAudioManager;
            _uploadDocumentManager = uploadDocumentManager;
            _uploadVideoManager = uploadVideoManager;
        }







        private TLDialog _currentDialog;

        public TLObject With { get; set; }


        public object photo;

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

        private bool _isLoadingNextSlice;

        public async Task LoadNextSliceAsync()
        {
            if (_isLoadingNextSlice) return;
            _isLoadingNextSlice = true;

            Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");

            var maxId = int.MaxValue;

            for (int i = 0; i < Messages.Count; i++)
            {
                if (Messages[i].Id != 0 && Messages[i].Id < maxId)
                {
                    maxId = Messages[i].Id;
                }
            }

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, 0, maxId, 15);
            if (result.IsSucceeded)
            {
                ProcessReplies(result.Value.Messages);

                foreach (var item in result.Value.Messages)
                {
                    Messages.Insert(0, item);
                    //InsertMessage(item as TLMessageCommonBase);
                }
            }

            _isLoadingNextSlice = false;
        }

        public async Task LoadFirstSliceAsync()
        {
            if (_isLoadingNextSlice) return;
            _isLoadingNextSlice = true;

            Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");

            var maxId = _currentDialog.ReadInboxMaxId;
            var offset = Math.Max(0, _currentDialog.UnreadCount - 10);

            var lastUnread = true;

            var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, -offset, maxId, 20);
            if (result.IsSucceeded)
            {
                ProcessReplies(result.Value.Messages);

                foreach (var item in result.Value.Messages)
                {
                    if (lastUnread && !item.IsUnread)
                    {
                        var serviceMessage = new TLMessageService
                        {
                            FromId = SettingsHelper.UserId,
                            ToId = Peer.ToPeer(),
                            State = TLMessageState.Sending,
                            IsOut = true,
                            IsUnread = true,
                            Date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now),
                            Action = new TLMessageActionUnreadMessages(),
                            RandomId = TLLong.Random()
                        };

                        Messages.Insert(0, serviceMessage);
                    }

                    lastUnread = item.IsUnread;

                    Messages.Insert(0, item);
                    //InsertMessage(item as TLMessageCommonBase);
                }
            }

            _isLoadingNextSlice = false;
        }

        public class TLMessageActionUnreadMessages : TLMessageActionBase
        {

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
                        var channel = message.ToId as TLPeerChat;
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
                    CacheService.AddChats(result.Value.Chats, (results) => { });
                    CacheService.AddUsers(result.Value.Users, (results) => { });

                    for (int j = 0; j < result.Value.Messages.Count; j++)
                    {
                        for (int k = 0; k < replyToMsgs.Count; k++)
                        {
                            var message = replyToMsgs[k];
                            if (message != null && message.ReplyToMsgId.Value == result.Value.Messages[j].Id)
                            {
                                replyToMsgs[k].Reply = result.Value.Messages[j];
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
            var participant = GetParticipant(parameter as TLPeerBase);
            var channel = participant as TLChannel;
            var chat = participant as TLChat;
            var user = participant as TLUser;

            if (user != null)
            {
                With = user;
                Peer = new TLInputPeerUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 };

                Messages.Clear();
                photo = user.Photo;
                DialogTitle = user.FullName;
                PlaceHolderColor = BindConvert.Current.Bubble(user.Id);
                LastSeen = LastSeenHelper.GetLastSeen(user).Item1;
                LastSeenVisible = Visibility.Visible;
                Peer = new TLInputPeerUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 };

                // test calls
                //var config = await ProtoService.GetDHConfigAsync(0, 0);
                //if (config.IsSucceeded)
                //{
                //    var dh = config.Value;
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
                //        Protocol = new TLPhoneCallProtocol()
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
                DialogTitle = channelDetails.Value.Chats[0].FullName;

                var channelFull = (TLChannelFull)channelDetails.Value.FullChat;
                if (channelFull.HasPinnedMsgId)
                {
                    pinnedMessageVisible = Visibility.Visible;

                    var y = await ProtoService.GetMessagesAsync(input, new TLVector<int>() { channelFull.PinnedMsgId ?? 0 });
                    if (y.IsSucceeded)
                    {
                        PinnedMessage = y.Value.Messages.FirstOrDefault();
                    }
                }
                else
                {
                    pinnedMessageVisible = Visibility.Collapsed;
                }

                PlaceHolderColor = BindConvert.Current.Bubble(channelDetails.Value.Chats[0].Id);
                // TODO: photo = channelDetails.Value.Chats[0].Photo;
                LastSeenVisible = Visibility.Collapsed;
            }
            else if (chat != null)
            {
                With = chat;
                Peer = new TLInputPeerChat { ChatId = chat.Id };

                var chatDetails = await ProtoService.GetFullChatAsync(chat.Id);
                DialogTitle = chatDetails.Value.Chats[0].FullName;
                // TODO: photo = chatDetails.Value.Chats[0].Photo;
                PlaceHolderColor = BindConvert.Current.Bubble(chatDetails.Value.Chats[0].Id);
                LastSeenVisible = Visibility.Collapsed;
            }

            _currentDialog = _currentDialog ?? CacheService.GetDialog(Peer.ToPeer());

            await LoadNextSliceAsync();

            var dialog = _currentDialog;
            if (dialog != null && dialog.HasDraft)
            {
                var draft = dialog.Draft as TLDraftMessage;
                if (draft != null)
                {
                    Aggregator.Publish(new TLUpdateDraftMessage { Draft = draft, Peer = Peer.ToPeer() });
                    ProcessDraftReply(draft);
                }
            }

            //if (dialog != null && Messages.Count > 0)
            //{
            //    var unread = dialog.UnreadCount;
            //    if (Peer is TLInputPeerChannel)
            //    {
            //        if (channel != null)
            //        {
            //            await ProtoService.ReadHistoryAsync(channel, dialog.TopMessage);
            //        }
            //    }
            //    else
            //    {
            //        await ProtoService.ReadHistoryAsync(Peer, dialog.TopMessage, 0);
            //    }

            //    dialog.UnreadCount = dialog.UnreadCount - unread;
            //    dialog.RaisePropertyChanged(() => dialog.UnreadCount);
            //}

            Aggregator.Subscribe(this);

            //await StickersRecent();
        }

        private async Task StickersRecent()
        {
            var response = await ProtoService.GetRecentStickersAsync(false, 0);
            if (response.IsSucceeded)
            {
                var recent = response.Value as TLMessagesRecentStickers;
                if (recent != null)
                {
                    await StickersAll(recent);
                }
            }
        }

        private async Task StickersAll(TLMessagesRecentStickers recent)
        {
            var response = await ProtoService.GetAllStickersAsync(new byte[0]);
            if (response.IsSucceeded)
            {
                var result = response.Value as TLMessagesAllStickers;
                if (result != null)
                {
                    //var stickerSets = result.Sets.Select(x => new KeyedList<TLStickerSet, TLDocument>(x, Extensions.Buffered<TLDocument>(x.Count)));
                    var stickerSets = result.Sets.Select(x => new KeyedList<TLStickerSet, TLDocument>(x, result.Documents.OfType<TLDocument>().Where(y => ((TLInputStickerSetID)y.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault().Stickerset).Id == x.Id)));
                    StickerSets = new List<KeyedList<TLStickerSet, TLDocument>>(stickerSets);
                    StickerSets.Insert(0, new KeyedList<TLStickerSet, TLDocument>(new TLStickerSet { Title = "Frequently used", ShortName = "tlg/recentlyUsed" }, recent.Stickers.OfType<TLDocument>()));
                    RaisePropertyChanged(() => StickerSets);

                    //await Task.Delay(1000);

                    //var set = await ProtoService.GetStickerSetAsync(new TLInputStickerSetShortName { ShortName = StickerSets[1].Key.ShortName });
                    //if (set.IsSucceeded)
                    //{
                    //    for (int i = 0; i < set.Value.Documents.Count; i++)
                    //    {
                    //        StickerSets[1][i] = set.Value.Documents[i] as TLDocument;
                    //    }
                    //}

                    Debug.WriteLine("Done");

                    //var boh = result.Documents.OfType<TLDocument>().GroupBy(x => ((TLInputStickerSetID)x.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault().Stickerset).Id).ToList();
                }
            }
        }

        public List<KeyedList<TLStickerSet, TLDocument>> StickerSets { get; set; }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return base.OnNavigatedFromAsync(pageState, suspending);
        }

        private TLObject GetParticipant(TLPeerBase peer)
        {
            var user = peer as TLPeerUser;
            if (user != null)
            {
                return CacheService.GetUser(user.UserId);
            }

            var chat = peer as TLPeerChat;
            if (chat != null)
            {
                return CacheService.GetChat(chat.ChatId);
            }

            var channel = peer as TLPeerChannel;
            if (channel != null)
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
                    CacheService.AddChats(result.Value.Chats, (results) => { });
                    CacheService.AddUsers(result.Value.Users, (results) => { });

                    for (int j = 0; j < result.Value.Messages.Count; j++)
                    {
                        if (draft.ReplyToMsgId.Value == result.Value.Messages[j].Id)
                        {
                            Reply = result.Value.Messages[j];
                        }
                    }
                }
                else
                {
                    Execute.ShowDebugMessage("messages.getMessages error " + result.Error);
                }
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

        public RelayCommand ClearReplyCommand => new RelayCommand(() => { Reply = null; });

        #endregion

        public RelayCommand<string> SendCommand => new RelayCommand<string>(SendMessage);
        private async void SendMessage(string args)
        {
            await SendMessageAsync(null, args != null);
        }

        public async Task SendMessageAsync(List<TLMessageEntityBase> entities, bool sticker, bool useReplyMarkup = false)
        {
            var messageText = Text?.Replace('\r', '\n');

            TLDocument document = null;
            TLMessageMediaBase media = null;
            if (sticker)
            {
                messageText = string.Empty;

                var set = await ProtoService.GetStickerSetAsync(new TLInputStickerSetShortName { ShortName = "cyanide_happiness_by_naeim" });
                if (set.IsSucceeded)
                {
                    document = set.Value.Documents.FirstOrDefault(x => x.Id == 342543832497260385) as TLDocument;
                }
            }

            if (document != null)
            {
                media = new TLMessageMediaDocument { Document = document };
            }
            else
            {
                media = new TLMessageMediaEmpty();
            }

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, messageText, media, TLLong.Random(), null);

            message.Entities = entities != null ? new TLVector<TLMessageEntityBase>(entities) : null;
            message.HasEntities = entities != null;

            MessageHelper.PreprocessEntities(ref message);

            if (Reply != null)
            {
                var container = Reply as TLMessagesContainter;
                if (container != null)
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
                }
                else
                {
                    message.HasReplyToMsgId = true;
                    message.ReplyToMsgId = Reply.Id;
                    message.Reply = Reply;
                    Reply = null;
                }
            }

            var previousMessage = InsertSendingMessage(message, useReplyMarkup);
            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                if (document != null)
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
                }
                else
                {
                    var response = await ProtoService.SendMessageAsync(message, () =>
                    {
                        message.State = TLMessageState.Confirmed;
                    });
                    if (response.IsSucceeded)
                    {
                        message.RaisePropertyChanged(() => message.Media);
                    }
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
            var fileCache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

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
                Type = string.Empty,
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
                        File = new TLInputFile
                        {
                            Id = upload.FileId,
                            Name = "file.jpg",
                            Parts = upload.Parts.Count,
                            Md5Checksum = string.Empty
                        }
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
            var fileCache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

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
                        File = new TLInputFile
                        {
                            Id = upload.FileId,
                            Md5Checksum = string.Empty,
                            Name = fileName,
                            Parts = upload.Parts.Count
                        },
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
            var fileCache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

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
                        File = new TLInputFile
                        {
                            Id = upload.FileId,
                            Md5Checksum = string.Empty,
                            Name = fileName,
                            Parts = upload.Parts.Count
                        },
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
    }

    public class MessageCollection : ObservableCollection<TLMessageBase>
    {
        protected override void InsertItem(int index, TLMessageBase item)
        {
            base.InsertItem(index, item);

            var next = index > 0 ? this[index - 1] : null;
            var previous = index < Count - 1 ? this[index + 1] : null;

            //if (next is TLMessageEmpty)
            //{
            //    next = index > 1 ? this[index - 2] : null;
            //}
            //if (previous is TLMessageEmpty)
            //{
            //    previous = index < Count - 2 ? this[index + 2] : null;
            //}

            UpdateSeparatorOnInsert(item, previous, index);
            UpdateSeparatorOnInsert(next, item, index - 1);

            UpdateAttach(previous, item, index + 1);
            UpdateAttach(item, next, index);
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
            if (item == null) return;

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
                             !(previous is TLMessageService) &&
                             !(previous is TLMessageEmpty) &&
                             previous.FromId == item.FromId &&
                             item.Date - previous.Date < 900;
                }

                item.IsFirst = !attach;
            }
            else
            {
                item.IsFirst = true;
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
