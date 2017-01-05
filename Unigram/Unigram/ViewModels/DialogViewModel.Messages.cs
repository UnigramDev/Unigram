using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Views;
using Windows.ApplicationModel.DataTransfer;
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

        #region Forward

        public RelayCommand<TLMessageBase> MessageForwardCommand => new RelayCommand<TLMessageBase>(MessageForwardExecute);
        private void MessageForwardExecute(TLMessageBase message)
        {
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

        #region Delete

        public RelayCommand<TLMessageBase> MessageDeleteCommand => new RelayCommand<TLMessageBase>(MessageDeleteExecute);
        private async void MessageDeleteExecute(TLMessageBase message)
        {
            if (message == null) return;

            var check = default(CheckBox);
            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = "Are you sure you want to delete 1 message?",
                Margin = new Windows.UI.Xaml.Thickness(0, 12, 0, 0)
            });

            var messageCommon = message as TLMessageCommonBase;
            if (messageCommon != null && messageCommon.IsOut && Peer is TLInputPeerUser)
            {
                var date = BindConvert.Current.DateTime(messageCommon.Date);
                var elapsed = DateTime.Now - date;

                if (elapsed.TotalHours < 48)
                {
                    var user = With as TLUser;
                    if (user != null)
                    {
                        check = new CheckBox
                        {
                            Content = string.Format("Delete for {0}", user.FullName),
                            Margin = new Windows.UI.Xaml.Thickness(0, 12, 0, 0)
                        };

                        content.Children.Add(check);
                    }
                }
            }

            //var dialog = new MessageDialog("Do you want to delete this message?", "Delete");
            //dialog.Commands.Add(new UICommand("Si"));
            //dialog.Commands.Add(new UICommand("No"));
            //var result = await dialog.ShowAsync();
            //if (result != null && result.Label == "Si")

            var dialog = new ContentDialog();
            dialog.Title = "Delete";
            dialog.Content = content;
            dialog.PrimaryButtonText = "Yes";
            dialog.SecondaryButtonText = "No";
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var revoke = check?.IsChecked == true;

                var messages = new List<TLMessageBase>() { message };
                if (message.Id == 0 && message.RandomId != 0L)
                {
                    DeleteMessagesInternal(null, messages);
                    return;
                }

                DeleteMessages(null, null, messages, revoke, null, DeleteMessagesInternal);
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

        #region Edit

        public RelayCommand<TLMessage> MessageEditCommand => new RelayCommand<TLMessage>(MessageEditExecute);
        private async void MessageEditExecute(TLMessage message)
        {
            var result = await ProtoService.GetMessageEditDataAsync(Peer, message.Id);
            if (result.IsSucceeded)
            {
                Execute.BeginOnUIThread(() =>
                {
                    var messageEditText = this.GetMessageEditText(result.Value, message);
                    this.StartEditMessage(messageEditText, message);
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
                    Telegram.Api.Helpers.Execute.ShowDebugMessage("messages.getMessageEditData error " + result.Error);
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

            Aggregator.Publish(new EditMessageEventArgs(_editedMessage));

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
            if (!editData.IsCaption)
            {
                var text = message.Message.ToString();
                var stringBuilder = new StringBuilder();

                if (message != null && message.Entities != null && message.Entities.Count > 0)
                {
                    //this.ClearMentions();

                    if (message.Entities.FirstOrDefault(x => !(x is TLMessageEntityMentionName) && !(x is TLInputMessageEntityMentionName)) == null)
                    {
                        for (int i = 0; i < message.Entities.Count; i++)
                        {
                            int num = (i == 0) ? 0 : (message.Entities[i - 1].Offset + message.Entities[i - 1].Length);
                            int num2 = (i == 0) ? message.Entities[i].Offset : (message.Entities[i].Offset - num);

                            stringBuilder.Append(text.Substring(num, num2));

                            var entityMentionName = message.Entities[i] as TLMessageEntityMentionName;
                            if (entityMentionName != null)
                            {
                                var user = CacheService.GetUser(entityMentionName.UserId);
                                if (user != null)
                                {
                                    //this.AddMention(user);
                                    string text2 = text.Substring(message.Entities[i].Offset, message.Entities[i].Length);
                                    stringBuilder.Append(string.Format("@({0})", text2));
                                }
                            }
                            else
                            {
                                var entityInputMentionName = message.Entities[i] as TLInputMessageEntityMentionName;
                                if (entityInputMentionName != null)
                                {
                                    var inputUser = entityInputMentionName.UserId as TLInputUser;
                                    if (inputUser != null)
                                    {
                                        TLUserBase user2 = this.CacheService.GetUser(inputUser.UserId);
                                        if (user2 != null)
                                        {
                                            //this.AddMention(user2);
                                            string text3 = text.Substring(message.Entities[i].Offset, message.Entities[i].Length);
                                            stringBuilder.Append(string.Format("@({0})", text3));
                                        }
                                    }
                                }
                                else
                                {
                                    num = message.Entities[i].Offset;
                                    num2 = message.Entities[i].Length;
                                    stringBuilder.Append(text.Substring(num, num2));
                                }
                            }
                        }

                        var baseEntity = message.Entities[message.Entities.Count - 1];
                        if (baseEntity != null)
                        {
                            stringBuilder.Append(text.Substring(baseEntity.Offset + baseEntity.Length));
                        }
                    }
                    else
                    {
                        stringBuilder.Append(text);
                    }
                }
                else
                {
                    stringBuilder.Append(text);
                }

                return stringBuilder.ToString();
            }

            var mediaCaption = message.Media as ITLMediaCaption;
            if (mediaCaption != null)
            {
                return mediaCaption.Caption;
            }

            return null;
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

                    Uri uri;
                    if (Uri.TryCreate(navigation, UriKind.Absolute, out uri))
                    {
                        await Launcher.LaunchUriAsync(uri);
                    }
                }

                return;
            }

            var callbackButton = button as TLKeyboardButtonCallback;
            if (callbackButton != null)
            {
                var response = await ProtoService.GetBotCallbackAnswerAsync(Peer, message.Id, callbackButton.Data, 0);
                if (response.IsSucceeded && response.Value.HasMessage)
                {
                    if (response.Value.IsAlert)
                    {
                        await new MessageDialog(response.Value.Message).ShowAsync();
                    }
                    else
                    {
                        // TODO:
                        await new MessageDialog(response.Value.Message).ShowAsync();
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
                    var response = await ProtoService.GetBotCallbackAnswerAsync(Peer, message.Id, null, 1);
                    if (response.IsSucceeded && response.Value.IsHasUrl && response.Value.HasUrl)
                    {
                        var user = CacheService.GetUser(message.ViaBotId) as TLUser;
                        if (user != null)
                        {
                            NavigationService.Navigate(typeof(GamePage), new GamePage.NavigationParameters { Url = response.Value.Url, Username = user.Username, Title = gameMedia.Game.Title });
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
    }
}
