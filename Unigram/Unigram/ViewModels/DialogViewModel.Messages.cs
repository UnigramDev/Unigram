using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Popups;

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
        private async void MessageCopyExecute(TLMessage message)
        {
            if (message == null) return;

            if (message.Media is TLMessageMediaGame)
            {
                var gameMedia = message.Media as TLMessageMediaGame;

                var button = message.ReplyMarkup.Rows.SelectMany(x => x.Buttons).OfType<TLKeyboardButtonGame>().FirstOrDefault();
                if (button != null)
                {
                    var responseBot = await ProtoService.GetBotCallbackAnswerAsync(Peer, message.Id, null, 1);
                    if (responseBot.IsSucceeded && responseBot.Value.IsHasUrl && responseBot.Value.HasUrl)
                    {
                        var user = CacheService.GetUser(message.ViaBotId) as TLUser;
                        if (user != null)
                        {
                            NavigationService.Navigate(typeof(GamePage), new GamePage.NavigationParameters { Url = responseBot.Value.Url, Username = user.Username, Title = gameMedia.Game.Title });
                        }
                    }
                }

                return;
            }

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

            var dialog = new MessageDialog("Do you want to delete this message?", "Delete");
            dialog.Commands.Add(new UICommand("Si"));
            dialog.Commands.Add(new UICommand("No"));
            var result = await dialog.ShowAsync();
            if (result != null && result.Label == "Si")
            {
                var messages = new List<TLMessageBase>() { message };
                if (message.Id == 0 && message.RandomId != 0L)
                {
                    DeleteMessagesInternal(null, messages);
                    return;
                }

                DeleteMessages(null, null, messages, null, DeleteMessagesInternal);
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

            CacheService.DeleteMessages(TLUtils.InputPeerToPeer(Peer, SettingsHelper.UserId), lastMessage, remoteMessages);
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

        public async void DeleteMessages(TLMessageBase lastItem, IList<TLMessageBase> localMessages, IList<TLMessageBase> remoteMessages, Action<TLMessageBase, IList<TLMessageBase>> localCallback = null, Action<TLMessageBase, IList<TLMessageBase>> remoteCallback = null)
        {
            if (localMessages != null && localMessages.Count > 0)
            {
                localCallback?.Invoke(lastItem, localMessages);
            }
            if (remoteMessages != null && remoteMessages.Count > 0)
            {
                var messages = new TLVector<int>(remoteMessages.Select(x => x.Id).ToList());
                var response = await ProtoService.DeleteMessagesAsync(messages);
                if (response.IsSucceeded)
                {
                    remoteCallback?.Invoke(lastItem, remoteMessages);
                }
            }
        }

        #endregion


    }
}
