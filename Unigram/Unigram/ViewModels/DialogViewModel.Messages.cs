using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Native;
using Unigram.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        #region Reply

        public RelayCommand<TLMessageBase> MessageReplyCommand => new RelayCommand<TLMessageBase>(MessageReplyExecute);
        private void MessageReplyExecute(TLMessageBase message)
        {
            if (message == null) return;

            var serviceMessage = message as TLMessageService;
            if (serviceMessage != null)
            {
                var action = serviceMessage.Action;
                // TODO: 
                //if (action is TLMessageActionEmpty || action is TLMessageActionUnreadMessages)
                //{
                //    return;
                //}
            }

            if (message.Id <= 0) return;

            var message31 = message as TLMessage;
            if (message31 != null && !message31.IsOut && message31.HasFromId)
            {
                var fromId = message31.FromId.Value;
                var user = CacheService.GetUser(fromId) as TLUser;
                if (user != null && user.IsBot)
                {
                    // TODO: SetReplyMarkup(message31);
                }
            }

            Reply = message;
            Aggregator.Publish("/dlg_focus");
        }

        #endregion

        #region Delete

        public RelayCommand<TLMessageBase> MessageDeleteCommand => new RelayCommand<TLMessageBase>(MessageDeleteExecute);
        private async void MessageDeleteExecute(TLMessageBase messageBase)
        {
            if (messageBase == null) return;

            var message = messageBase as TLMessage;
            if (message != null && !message.IsOut && !message.IsPost && Peer is TLInputPeerChannel)
            {
                var dialog = new DeleteChannelMessageDialog();

                var result = await dialog.ShowAsync();
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
                        var response = await ProtoService.KickFromChannelAsync(channel, message.From.ToInputUser(), true);
                        if (response.IsSucceeded)
                        {
                            var updates = response.Result as TLUpdates;
                            if (updates != null)
                            {
                                var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
                                if (newChannelMessageUpdate != null)
                                {
                                    Aggregator.Publish(newChannelMessageUpdate.Message);
                                }
                            }
                        }
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

                if (message != null && message.IsOut && message.ToId.Id != SettingsHelper.UserId && (Peer is TLInputPeerUser || Peer is TLInputPeerChat))
                {
                    var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                    var config = CacheService.GetConfig();
                    if (config != null && message.Date + config.EditTimeLimit > date)
                    {
                        var user = With as TLUser;
                        if (user != null)
                        {
                            dialog.CheckBoxLabel = string.Format("Delete for {0}", user.FullName);
                        }

                        var chat = With as TLChat;
                        if (chat != null)
                        {
                            dialog.CheckBoxLabel = "Delete for everyone";
                        }
                    }
                }
                else if (Peer is TLInputPeerChat)
                {
                    dialog.Message += "\r\n\r\nThis will delete it just for you, not for other participants of the chat.";
                }

                var result = await dialog.ShowAsync();
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

            Execute.BeginOnUIThread(() =>
            {
                for (int j = 0; j < messages.Count; j++)
                {
                    Messages.Remove(messages[j]);
                }

                RaisePropertyChanged(() => With);

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

        public RelayCommand<TLMessageBase> MessageForwardCommand => new RelayCommand<TLMessageBase>(MessageForwardExecute);
        private void MessageForwardExecute(TLMessageBase message)
        {
            if (message is TLMessage)
            {
                App.State.ForwardMessages = new List<TLMessage> { message as TLMessage };
                NavigationService.GoBack();
            }
        }

        #endregion

        #region Multiple Delete

        private RelayCommand _messagesDeleteCommand;
        public RelayCommand MessagesDeleteCommand => _messagesDeleteCommand = (_messagesDeleteCommand ?? new RelayCommand(MessagesDeleteExecute, () => SelectedMessages?.Count > 0));

        private void MessagesDeleteExecute()
        {
            //TLMessageBase lastMessage = null;
            //var localMessages = new List<TLMessageBase>();
            //var remoteMessages = new List<TLMessageBase>();
            //for (int i = 0; i < Messages.Count; i++)
            //{
            //    var message = Messages[i];
            //    if (message.IsSelected)
            //    {
            //        if (message.Index == 0 && message.RandomIndex != 0L)
            //        {
            //            localMessages.Add(message);
            //            lastMessage = null;
            //        }
            //        else if (message.Index != 0)
            //        {
            //            remoteMessages.Add(message);
            //            lastMessage = null;
            //        }
            //    }
            //    else if (lastMessage == null)
            //    {
            //        lastMessage = message;
            //    }
            //}

            //if (localMessages.Count > 0 || remoteMessages.Count > 0)
            //{
            //    //this.IsSelectionEnabled = false;
            //}

            //if (With is TLBroadcastChat)
            //{
            //    DeleteMessagesInternal(lastMessage, localMessages);
            //    DeleteMessagesInternal(lastMessage, remoteMessages);
            //    return;
            //}

            //DeleteMessages(lastMessage, localMessages, remoteMessages, DeleteMessagesInternal, DeleteMessagesInternal);
        }

        #endregion

        #region Multiple Forward

        private RelayCommand _messagesForwardCommand;
        public RelayCommand MessagesForwardCommand => _messagesForwardCommand = (_messagesForwardCommand ?? new RelayCommand(MessagesForwardExecute, () => SelectedMessages?.Count > 0));

        private void MessagesForwardExecute()
        {
            var messages = SelectedMessages.OfType<TLMessage>().Where(x => x.Id != 0).OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                App.State.ForwardMessages = new List<TLMessage>(messages);
                NavigationService.GoBack();
            }

            //_stateService.ForwardMessages = Messages.Where(x => x.IsSelected).ToList();
            //_stateService.ForwardMessages.Reverse();

            //SelectionMode = Windows.UI.Xaml.Controls.ListViewSelectionMode.None;
            //NavigationService.GoBack();
        }

        #endregion

        #region Copy

        public RelayCommand<TLMessage> MessageCopyCommand => new RelayCommand<TLMessage>(MessageCopyExecute);
        private void MessageCopyExecute(TLMessage message)
        {
            if (message == null) return;

            string text = null;

            var media = message.Media as ITLMediaCaption;
            if (media != null && !string.IsNullOrWhiteSpace(media.Caption))
            {
                text = media.Caption;
            }
            else if (!string.IsNullOrWhiteSpace(message.Message))
            {
                text = message.Message;
            }

            if (text != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(text);
                Clipboard.SetContent(dataPackage);
            }
        }

        #endregion

        #region CopyLink

        public RelayCommand<TLMessage> MessageCopyLinkCommand => new RelayCommand<TLMessage>(MessageCopyLinkExecute);
        private void MessageCopyLinkExecute(TLMessage message)
        {
            if (message == null) return;

            var channel = With as TLChannel;
            if (channel != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetWebLink(new Uri($"https://t.me/{channel.Username}/{message.Id}"));
                Clipboard.SetContent(dataPackage);
            }
        }

        #endregion

        #region Edit

        public RelayCommand<TLMessage> MessageEditCommand => new RelayCommand<TLMessage>(MessageEditExecute);
        private async void MessageEditExecute(TLMessage message)
        {
            var result = await ProtoService.GetMessageEditDataAsync(Peer, message.Id);
            if (result.IsSucceeded)
            {
                Execute.BeginOnUIThread(() =>
                {
                    var messageEditText = GetMessageEditText(result.Result, message);
                    StartEditMessage(messageEditText, message);
                });
            }
            else
            {
                Execute.BeginOnUIThread(() =>
                {
                    //this.IsWorking = false;
                    //if (error.CodeEquals(ErrorCode.BAD_REQUEST) && error.TypeEquals(ErrorType.MESSAGE_ID_INVALID))
                    //{
                    //    MessageBox.Show(AppResources.EditMessageError, AppResources.Error, 0);
                    //    return;
                    //}
                    Execute.ShowDebugMessage("messages.getMessageEditData error " + result.Error);
                });
            }
        }

        public void StartEditMessage(string text, TLMessage message)
        {
            if (text == null)
            {
                return;
            }
            if (message == null)
            {
                return;
            }

            _editedMessage = message;

            var config = CacheService.GetConfig();
            var editUntil = (config != null) ? (message.Date + config.EditTimeLimit + 300) : 0;
            if (message.FromId != null && message.ToId is TLPeerUser && message.FromId.Value == message.ToId.Id)
            {
                editUntil = 0;
            }

            Reply = new TLMessagesContainter
            {
                EditMessage = _editedMessage,
                EditUntil = editUntil
            };

            Aggregator.Publish(new EditMessageEventArgs(_editedMessage, text));

            //if (this._editMessageTimer == null)
            //{
            //    this._editMessageTimer = new DispatcherTimer();
            //    this._editMessageTimer.add_Tick(new EventHandler(this.OnEditMessageTimerTick));
            //    this._editMessageTimer.set_Interval(System.TimeSpan.FromSeconds(1.0));
            //}
            //this._editMessageTimer.Start();
            //this.IsEditingEnabled = true;
            //this.Text = text.ToString();

            CurrentInlineBot = null;

            //this.ClearStickerHints();
            //this.ClearInlineBotResults();
            //this.ClearUsernameHints();
            //this.ClearHashtagHints();
            //this.ClearCommandHints();
        }

        private string GetMessageEditText(TLMessagesMessageEditData editData, TLMessage message)
        {
            if (editData.IsCaption)
            {
                var mediaCaption = message.Media as ITLMediaCaption;
                if (mediaCaption != null)
                {
                    return mediaCaption.Caption;
                }
            }
            else
            {
                return message.Message;
            }

            return null;

            //if (!editData.IsCaption)
            //{
            //    var text = message.Message.ToString();
            //    var stringBuilder = new StringBuilder();

            //    if (message != null && message.Entities != null && message.Entities.Count > 0)
            //    {
            //        //this.ClearMentions();

            //        if (message.Entities.FirstOrDefault(x => !(x is TLMessageEntityMentionName) && !(x is TLInputMessageEntityMentionName)) == null)
            //        {
            //            for (int i = 0; i < message.Entities.Count; i++)
            //            {
            //                int num = (i == 0) ? 0 : (message.Entities[i - 1].Offset + message.Entities[i - 1].Length);
            //                int num2 = (i == 0) ? message.Entities[i].Offset : (message.Entities[i].Offset - num);

            //                stringBuilder.Append(text.Substring(num, num2));

            //                var entityMentionName = message.Entities[i] as TLMessageEntityMentionName;
            //                if (entityMentionName != null)
            //                {
            //                    var user = CacheService.GetUser(entityMentionName.UserId);
            //                    if (user != null)
            //                    {
            //                        //this.AddMention(user);
            //                        string text2 = text.Substring(message.Entities[i].Offset, message.Entities[i].Length);
            //                        stringBuilder.Append(string.Format("@({0})", text2));
            //                    }
            //                }
            //                else
            //                {
            //                    var entityInputMentionName = message.Entities[i] as TLInputMessageEntityMentionName;
            //                    if (entityInputMentionName != null)
            //                    {
            //                        var inputUser = entityInputMentionName.UserId as TLInputUser;
            //                        if (inputUser != null)
            //                        {
            //                            TLUserBase user2 = this.CacheService.GetUser(inputUser.UserId);
            //                            if (user2 != null)
            //                            {
            //                                //this.AddMention(user2);
            //                                string text3 = text.Substring(message.Entities[i].Offset, message.Entities[i].Length);
            //                                stringBuilder.Append(string.Format("@({0})", text3));
            //                            }
            //                        }
            //                    }
            //                    else
            //                    {
            //                        num = message.Entities[i].Offset;
            //                        num2 = message.Entities[i].Length;
            //                        stringBuilder.Append(text.Substring(num, num2));
            //                    }
            //                }
            //            }

            //            var baseEntity = message.Entities[message.Entities.Count - 1];
            //            if (baseEntity != null)
            //            {
            //                stringBuilder.Append(text.Substring(baseEntity.Offset + baseEntity.Length));
            //            }
            //        }
            //        else
            //        {
            //            stringBuilder.Append(text);
            //        }
            //    }
            //    else
            //    {
            //        stringBuilder.Append(text);
            //    }

            //    return stringBuilder.ToString();
            //}

            //var mediaCaption = message.Media as ITLMediaCaption;
            //if (mediaCaption != null)
            //{
            //    return mediaCaption.Caption;
            //}

            //return null;
        }

        #endregion

        #region Pin

        public RelayCommand<TLMessageBase> MessagePinCommand => new RelayCommand<TLMessageBase>(MessagePinExecute);
        private async void MessagePinExecute(TLMessageBase message)
        {
            if (PinnedMessage?.Id == message.Id)
            {
                var dialog = new TLMessageDialog();
                dialog.Title = "Unpin message";
                dialog.Message = "Would you like to unpin this message?";
                dialog.PrimaryButtonText = "Yes";
                dialog.SecondaryButtonText = "No";

                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogResult.Primary)
                {
                    var channel = Peer as TLInputPeerChannel;
                    var inputChannel = new TLInputChannel { ChannelId = channel.ChannelId, AccessHash = channel.AccessHash };

                    var result = await ProtoService.UpdatePinnedMessageAsync(false, inputChannel, 0);
                    if (result.IsSucceeded)
                    {
                        PinnedMessage = null;
                    }
                }
            }
            else
            {
                var dialog = new TLMessageDialog();
                dialog.Title = "Pin message";
                dialog.Message = "Would you like to pin this message?";
                dialog.CheckBoxLabel = "Notify all members";
                dialog.IsChecked = true;
                dialog.PrimaryButtonText = "Yes";
                dialog.SecondaryButtonText = "No";

                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogResult.Primary)
                {
                    var channel = Peer as TLInputPeerChannel;
                    var inputChannel = new TLInputChannel { ChannelId = channel.ChannelId, AccessHash = channel.AccessHash };

                    var silent = dialog.IsChecked == false;
                    var result = await ProtoService.UpdatePinnedMessageAsync(silent, inputChannel, message.Id);
                    if (result.IsSucceeded)
                    {
                        var updates = result.Result as TLUpdates;
                        if (updates != null)
                        {
                            var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
                            if (newChannelMessageUpdate != null)
                            {
                                Handle(newChannelMessageUpdate.Message as TLMessageCommonBase);
                                Aggregator.Publish(new TopMessageUpdatedEventArgs(_currentDialog, newChannelMessageUpdate.Message));
                            }
                        }

                        PinnedMessage = message;
                    }
                }
            }
        }

        #endregion

        #region KeyboardButton

        private TLMessage _replyMarkupMessage;
        private TLReplyMarkupBase _replyMarkup;
        private TLMessage _editedMessage;

        public TLReplyMarkupBase ReplyMarkup
        {
            get
            {
                return _replyMarkup;
            }
            set
            {
                Set(ref _replyMarkup, value);
            }
        }

        private void SetReplyMarkup(TLMessage message)
        {
            if (Reply != null && message != null)
            {
                return;
            }

            if (message != null && message.ReplyMarkup != null)
            {
                if (message.ReplyMarkup is TLReplyInlineMarkup)
                {
                    return;
                }

                //var keyboardMarkup = message.ReplyMarkup as TLReplyKeyboardMarkup;
                //if (keyboardMarkup != null && keyboardMarkup.IsPersonal && !message.IsMention)
                //{
                //    return;
                //}

                var keyboardHide = message.ReplyMarkup as TLReplyKeyboardHide;
                if (keyboardHide != null && _replyMarkupMessage != null && _replyMarkupMessage.FromId.Value != message.FromId.Value)
                {
                    return;
                }

                var keyboardForceReply = message.ReplyMarkup as TLReplyKeyboardForceReply;
                if (keyboardForceReply != null /*&& !keyboardForceReply.HasResponse*/)
                {
                    _replyMarkupMessage = null;
                    ReplyMarkup = null;
                    Reply = message;
                    return;
                }

            }

            //this.SuppressOpenCommandsKeyboard = (message != null && message.ReplyMarkup != null && suppressOpenKeyboard);

            _replyMarkupMessage = message;
            ReplyMarkup = message?.ReplyMarkup;
        }

        //public RelayCommand<TLKeyboardButtonBase> KeyboardButtonCommand => new RelayCommand<TLKeyboardButtonBase>(KeyboardButtonExecute);
        public async void KeyboardButtonExecute(TLKeyboardButtonBase button, TLMessage message)
        {
            var switchInlineButton = button as TLKeyboardButtonSwitchInline;
            if (switchInlineButton != null)
            {
                return;
            }

            var urlButton = button as TLKeyboardButtonUrl;
            if (urlButton != null)
            {
                if (urlButton.Url.Contains("telegram.me") || urlButton.Url.Contains("t.me"))
                {
                    MessageHelper.HandleTelegramUrl(urlButton.Url);
                }
                else
                {
                    var navigation = urlButton.Url;
                    var dialog = new MessageDialog(navigation, "Open this link?");
                    dialog.Commands.Add(new UICommand("OK", (_) => { }, 0));
                    dialog.Commands.Add(new UICommand("Cancel", (_) => { }, 1));
                    dialog.DefaultCommandIndex = 0;
                    dialog.CancelCommandIndex = 1;

                    var result = await dialog.ShowAsync();
                    if (result == null || (int)result?.Id == 1)
                    {
                        return;
                    }

                    if (!navigation.StartsWith("http"))
                    {
                        navigation = "http://" + navigation;
                    }

                    if (Uri.TryCreate(navigation, UriKind.Absolute, out Uri uri))
                    {
                        await Launcher.LaunchUriAsync(uri);
                    }
                }

                return;
            }

            var callbackButton = button as TLKeyboardButtonCallback;
            if (callbackButton != null)
            {
                var response = await ProtoService.GetBotCallbackAnswerAsync(Peer, message.Id, callbackButton.Data, false);
                if (response.IsSucceeded && response.Result.HasMessage)
                {
                    if (response.Result.IsAlert)
                    {
                        await new MessageDialog(response.Result.Message).ShowAsync();
                    }
                    else
                    {
                        // TODO:
                        await new MessageDialog(response.Result.Message).ShowAsync();
                    }
                }

                return;
            }

            var gameButton = button as TLKeyboardButtonGame;
            if (gameButton != null)
            {
                var gameMedia = message.Media as TLMessageMediaGame;
                if (gameMedia != null)
                {
                    var response = await ProtoService.GetBotCallbackAnswerAsync(Peer, message.Id, null, true);
                    if (response.IsSucceeded && response.Result.IsHasUrl && response.Result.HasUrl)
                    {
                        var user = CacheService.GetUser(message.ViaBotId) as TLUser;
                        if (user != null)
                        {
                            NavigationService.Navigate(typeof(GamePage), new GamePage.NavigationParameters { Url = response.Result.Url, Username = user.Username, Title = gameMedia.Game.Title });
                        }
                    }
                }

                return;
            }

            var requestPhoneButton = button as TLKeyboardButtonRequestPhone;
            if (requestPhoneButton != null)
            {
                return;
            }

            var requestGeoButton = button as TLKeyboardButtonRequestGeoLocation;
            if (requestGeoButton != null)
            {
                return;
            }

            var keyboardButton = button as TLKeyboardButton;
            if (keyboardButton != null)
            {
                _text = keyboardButton.Text;
                await SendMessageAsync(null, false, true);
            }
        }

        #endregion

        #region Open reply

        public RelayCommand<TLMessageCommonBase> MessageOpenReplyCommand => new RelayCommand<TLMessageCommonBase>(MessageOpenReplyExecute);
        private async void MessageOpenReplyExecute(TLMessageCommonBase messageCommon)
        {
            if (messageCommon != null && messageCommon.ReplyToMsgId.HasValue)
            {
                await LoadMessageSliceAsync(messageCommon.Id, messageCommon.ReplyToMsgId.Value);
            }
        }

        #endregion

        #region Save sticker as

        public RelayCommand<TLMessage> MessageSaveStickerCommand => new RelayCommand<TLMessage>(MessageSaveStickerExecute);
        private async void MessageSaveStickerExecute(TLMessage message)
        {
            if (message != null)
            {
                var documentMedia = message.Media as TLMessageMediaDocument;
                if (documentMedia != null)
                {
                    var document = documentMedia.Document as TLDocument;
                    if (document != null)
                    {
                        var fileName = document.GetFileName();
                        if (File.Exists(FileUtils.GetTempFileName(fileName)))
                        {
                            var picker = new FileSavePicker();
                            picker.FileTypeChoices.Add("WebP image", new[] { ".webp" });
                            picker.FileTypeChoices.Add("PNG image", new[] { ".png" });
                            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                            picker.SuggestedFileName = "sticker.webp";

                            var fileNameAttribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                            if (fileNameAttribute != null)
                            {
                                picker.SuggestedFileName = fileNameAttribute.FileName;
                            }

                            var file = await picker.PickSaveFileAsync();
                            if (file != null)
                            {
                                var sticker = await FileUtils.GetTempFileAsync(fileName);

                                if (Path.GetExtension(file.Name).Equals(".webp"))
                                {
                                    await sticker.CopyAndReplaceAsync(file);
                                }
                                else if (Path.GetExtension(file.Name).Equals(".png"))
                                {
                                    var buffer = await FileIO.ReadBufferAsync(sticker);
                                    var bitmap = WebPImage.DecodeFromBuffer(buffer);

                                    using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                                    {
                                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                                        var pixelStream = bitmap.PixelBuffer.AsStream();
                                        var pixels = new byte[pixelStream.Length];

                                        await pixelStream.ReadAsync(pixels, 0, pixels.Length);

                                        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96.0, 96.0, pixels);
                                        await encoder.FlushAsync();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Save file as

        public RelayCommand<TLMessage> MessageSaveMediaCommand => new RelayCommand<TLMessage>(MessageSaveMediaExecute);
        private async void MessageSaveMediaExecute(TLMessage message)
        {
            if (message != null)
            {
                var photoMedia = message.Media as TLMessageMediaPhoto;
                if (photoMedia != null)
                {
                    var photo = photoMedia.Photo as TLPhoto;
                    if (photo != null)
                    {
                        var photoSize = photo.Full as TLPhotoSize;
                        if (photoSize != null)
                        {
                            var location = photoSize.Location;
                            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
                            if (File.Exists(FileUtils.GetTempFileName(fileName)))
                            {
                                var picker = new FileSavePicker();
                                picker.FileTypeChoices.Add("JPEG Image", new[] { ".jpg" });
                                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                                picker.SuggestedFileName = BindConvert.Current.DateTime(message.Date).ToString("photo_yyyyMMdd_HH_mm_ss") + ".jpg";

                                var file = await picker.PickSaveFileAsync();
                                if (file != null)
                                {
                                    var result = await FileUtils.GetTempFileAsync(fileName);
                                    await result.CopyAndReplaceAsync(file);
                                }
                            }
                        }
                    }
                }

                var documentMedia = message.Media as TLMessageMediaDocument;
                if (documentMedia != null)
                {
                    var document = documentMedia.Document as TLDocument;
                    if (document != null)
                    {
                        var fileName = document.GetFileName();
                        if (File.Exists(FileUtils.GetTempFileName(fileName)))
                        {
                            var extension = document.GetFileExtension();

                            var picker = new FileSavePicker();
                            picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} File", new[] { document.GetFileExtension() });
                            picker.SuggestedStartLocation = PickerLocationId.Downloads;
                            picker.SuggestedFileName = BindConvert.Current.DateTime(message.Date).ToString("photo_yyyyMMdd_HH_mm_ss") + extension;

                            var fileNameAttribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                            if (fileNameAttribute != null)
                            {
                                picker.SuggestedFileName = fileNameAttribute.FileName;
                            }

                            var file = await picker.PickSaveFileAsync();
                            if (file != null)
                            {
                                var result = await FileUtils.GetTempFileAsync(fileName);
                                await result.CopyAndReplaceAsync(file);
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}
