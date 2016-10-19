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

        private bool _isStickerPickerOpen;
        public bool IsStickerPickerOpen
        {
            set
            {
                Set(ref _isStickerPickerOpen, value);
            }
            get
            {
                return _isStickerPickerOpen;
            }
        }

        public GridLength StickerKeyboardHeight
        {
            get
            {
                return IsStickerPickerOpen ? new GridLength(200) : new GridLength(0);
            }
        }


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
                }
            }

            _isLoadingNextSlice = false;
        }

        public async void ProcessReplies(IList<TLMessageBase> messages)
        {
            var replyIds = new TLVector<int>();
            var replyToMsgs = new List<TLMessage>();

            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i] as TLMessage;
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
                            messages[i].RaisePropertyChanged(() => replyToMsgs[i].Reply);
                            messages[i].RaisePropertyChanged(() => replyToMsgs[i].ReplyInfo);
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

                //Happy Birthday Alexmitter xD
                Messages.Clear();
                photo = user.Photo;
                DialogTitle = user.FullName;
                PlaceHolderColor = BindConvert.Current.Bubble(user.Id);
                LastSeen = LastSeenHelper.GetLastSeen(user).Item1;
                LastSeenVisible = Visibility.Visible;
                Peer = new TLInputPeerUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 };
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
                photo = channelDetails.Value.Chats[0].Photo;
                LastSeenVisible = Visibility.Collapsed;
            }
            else if (chat != null)
            {
                With = chat;
                Peer = new TLInputPeerChat { ChatId = chat.Id };

                var chatDetails = await ProtoService.GetFullChatAsync(chat.Id);
                DialogTitle = chatDetails.Value.Chats[0].FullName;
                photo = chatDetails.Value.Chats[0].Photo;
                PlaceHolderColor = BindConvert.Current.Bubble(chatDetails.Value.Chats[0].Id);
                LastSeenVisible = Visibility.Collapsed;
            }

            await LoadNextSliceAsync();

            _currentDialog = _currentDialog ?? CacheService.GetDialog(Peer.ToPeer());

            var dialog = _currentDialog;
            if (dialog != null && dialog.HasDraft)
            {
                var draft = dialog.Draft as TLDraftMessage;
                if (draft != null)
                {
                    Text = draft.Message;
                    ProcessDraftReply(draft);
                }
            }

            Aggregator.Subscribe(this);
        }

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

        public RelayCommand<string> StartRecording => new RelayCommand<string>(RecordAudio);
        private async void RecordAudio(string args)
        {
            // TODO
            //Implement method for voice recording
            return;
        }

        public RelayCommand<string> SendCommand => new RelayCommand<string>(SendMessage);
        private async void SendMessage(string args)
        {
            IsStickerPickerOpen = !IsStickerPickerOpen;
            //await SendMessageAsync(null, args != null);
        }

        public async Task SendMessageAsync(List<TLMessageEntityBase> entities, bool sticker)
        {
            var messageText = Text?.Replace('\r', '\n');

            TLDocument document = null;
            TLMessageMediaBase media = null;
            if (sticker)
            {
                messageText = string.Empty;

                var set = await ProtoService.GetStickerSetAsync(new TLInputStickerSetShortName { ShortName = "TrashPack" });
                if (set.IsSucceeded)
                {
                    document = set.Value.Documents.FirstOrDefault(x => x.Id == 169171005078503693) as TLDocument;
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
            var fileCache = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

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
            var fileCache = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

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
                var upload = await _uploadDocumentManager.UploadFileAsync(fileId, fileCache.Name, false).AsTask(media.Upload());
                if (upload != null)
                {
                    var inputMedia = new TLInputMediaUploadedDocument
                    {
                        File = new TLInputFile
                        {
                            Id = upload.FileId,
                            Md5Checksum = string.Empty,
                            Name = "test.gif",
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

        private TLMessageBase InsertSendingMessage(TLMessage message, bool useReplyMarkup = false)
        {
            TLMessageBase result;
            if (Messages.Count > 0)
            {
                //if (useReplyMarkup && _replyMarkupMessage != null)
                //{
                //    var chat = With as TLChatBase;
                //    if (chat != null)
                //    {
                //        message.ReplyToMsgId = _replyMarkupMessage.Id;
                //        message.Reply = _replyMarkupMessage;
                //    }

                //    Execute.BeginOnUIThread(() =>
                //    {
                //        if (Reply != null)
                //        {
                //            Reply = null;
                //            SetReplyMarkup(null);
                //        }
                //    });
                //}

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

                var history = CacheService.GetHistory(TLUtils.InputPeerToPeer(Peer, ProtoService.CurrentUserId), 15);
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

            UpdateAttach(previous, item);
            UpdateAttach(item, next);
        }

        protected override void RemoveItem(int index)
        {
            var next = index > 0 ? this[index - 1] : null;
            var previous = index < Count - 1 ? this[index + 1] : null;

            UpdateAttach(previous, next);

            base.RemoveItem(index);
        }

        private void UpdateAttach(TLMessageBase item, TLMessageBase previous)
        {
            if (item == null) return;
            
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
