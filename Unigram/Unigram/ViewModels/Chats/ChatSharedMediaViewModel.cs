using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using System.Threading.Tasks;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Unigram.Services;
using Telegram.Td.Api;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.UI.Xaml;
using System.Linq;
using Unigram.ViewModels.Delegates;

namespace Unigram.ViewModels.Chats
{
    public class ChatSharedMediaViewModel : TLViewModelBase, IMessageDelegate, IDelegable<IFileDelegate>, IHandle<UpdateFile>
    {
        public IFileDelegate Delegate { get; set; }

        public ChatSharedMediaViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            MessagesForwardCommand = new RelayCommand(MessagesForwardExecute, MessagesForwardCanExecute);
            MessagesDeleteCommand = new RelayCommand(MessagesDeleteExecute, MessagesDeleteCanExecute);
            MessageViewCommand = new RelayCommand<Message>(MessageViewExecute);
            MessageSaveCommand = new RelayCommand<Message>(MessageSaveExecute);
            MessageDeleteCommand = new RelayCommand<Message>(MessageDeleteExecute);
            MessageForwardCommand = new RelayCommand<Message>(MessageForwardExecute);
            MessageSelectCommand = new RelayCommand<Message>(MessageSelectExecute);

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var chatId = (long)parameter;

            //Peer = (TLInputPeerBase)parameter;
            //With = Peer is TLInputPeerUser ? (ITLDialogWith)CacheService.GetUser(Peer.ToPeer().Id) : CacheService.GetChat(Peer.ToPeer().Id);

            Chat = ProtoService.GetChat(chatId);

            Media = new MediaCollection(ProtoService, chatId, new SearchMessagesFilterPhotoAndVideo());
            Files = new MediaCollection(ProtoService, chatId, new SearchMessagesFilterDocument());
            Links = new MediaCollection(ProtoService, chatId, new SearchMessagesFilterUrl());
            Music = new MediaCollection(ProtoService, chatId, new SearchMessagesFilterAudio());
            Voice = new MediaCollection(ProtoService, chatId, new SearchMessagesFilterVoiceNote());

            RaisePropertyChanged(() => Media);
            RaisePropertyChanged(() => Files);
            RaisePropertyChanged(() => Links);
            RaisePropertyChanged(() => Music);
            RaisePropertyChanged(() => Voice);

            return Task.CompletedTask;
        }

        public void Handle(UpdateFile update)
        {
            BeginOnUIThread(() => Delegate?.UpdateFile(update.File));
        }

        private Chat _chat;
        public Chat Chat
        {
            get
            {
                return _chat;
            }
            set
            {
                Set(ref _chat, value);
            }
        }

        public MediaCollection Media { get; private set; }
        public MediaCollection Files { get; private set; }
        public MediaCollection Links { get; private set; }
        public MediaCollection Music { get; private set; }
        public MediaCollection Voice { get; private set; }

        public void Find(SearchMessagesFilter filter, string query)
        {
            switch (filter)
            {
                case SearchMessagesFilterPhotoAndVideo photoAndVideo:
                    Media = new MediaCollection(ProtoService, Chat.Id, photoAndVideo, query);
                    RaisePropertyChanged(() => Media);
                    break;
                case SearchMessagesFilterDocument document:
                    Files = new MediaCollection(ProtoService, Chat.Id, document, query);
                    RaisePropertyChanged(() => Files);
                    break;
                case SearchMessagesFilterUrl url:
                    Links = new MediaCollection(ProtoService, Chat.Id, url, query);
                    RaisePropertyChanged(() => Links);
                    break;
                case SearchMessagesFilterAudio audio:
                    Music = new MediaCollection(ProtoService, Chat.Id, audio, query);
                    RaisePropertyChanged(() => Music);
                    break;
                case SearchMessagesFilterVoiceNote voiceNote:
                    Voice = new MediaCollection(ProtoService, Chat.Id, voiceNote, query);
                    RaisePropertyChanged(() => Voice);
                    break;
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

        private List<Message> _selectedItems = new List<Message>();
        public List<Message> SelectedItems
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

        public RelayCommand<Message> MessageViewCommand { get; }
        private void MessageViewExecute(Message message)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.NavigateToChat(chat, message: message.Id);
        }

        #endregion

        #region Save

        public RelayCommand<Message> MessageSaveCommand { get; }
        private async void MessageSaveExecute(Message message)
        {
            var result = message.GetFileAndName(true);

            var file = result.File;
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var fileName = result.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = System.IO.Path.GetFileName(file.Local.Path);
            }

            var extension = System.IO.Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".dat";
            }

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} File", new[] { extension });
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.SuggestedFileName = fileName;

            var picked = await picker.PickSaveFileAsync();
            if (picked != null)
            {
                try
                {
                    var cached = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    await cached.CopyAndReplaceAsync(picked);
                }
                catch { }
            }
        }

        #endregion

        #region Delete

        public RelayCommand<Message> MessageDeleteCommand { get; }
        private async void MessageDeleteExecute(Message message)
        {
            if (message == null)
            {
                return;
            }

            var chat = ProtoService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            //if (message != null && message.Media is TLMessageMediaGroup groupMedia)
            //{
            //    ExpandSelection(new[] { message });
            //    MessagesDeleteExecute();
            //    return;
            //}

            if (message != null && !message.IsOutgoing && !message.IsChannelPost && chat.Type is ChatTypeSupergroup super && !super.IsChannel)
            {
                var sender = ProtoService.GetUser(message.SenderUserId);
                var dialog = new DeleteChannelMessageDialog(1, sender?.GetFullName());

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    if (dialog.DeleteAll)
                    {
                        ProtoService.Send(new DeleteChatMessagesFromUser(chat.Id, message.SenderUserId));
                    }
                    else
                    {
                        ProtoService.Send(new DeleteMessages(chat.Id, new[] { message.Id }, true));
                    }

                    if (dialog.BanUser)
                    {
                        ProtoService.Send(new SetChatMemberStatus(chat.Id, message.SenderUserId, new ChatMemberStatusBanned()));
                    }

                    if (dialog.ReportSpam && chat.Type is ChatTypeSupergroup supertype)
                    {
                        ProtoService.Send(new ReportSupergroupSpam(supertype.SupergroupId, message.SenderUserId, new[] { message.Id }));
                    }
                }
            }
            else
            {
                var dialog = new TLMessageDialog();
                dialog.Title = Strings.Resources.Message;
                dialog.Message = string.Format(Strings.Resources.AreYouSureDeleteMessages, Locale.Declension("Messages", 1));
                dialog.PrimaryButtonText = Strings.Resources.OK;
                dialog.SecondaryButtonText = Strings.Resources.Cancel;

                if (message.CanBeDeletedForAllUsers && message.CanBeDeletedOnlyForSelf)
                {
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        var user = ProtoService.GetUser(privata.UserId);
                        if (user != null && !(user.Type is UserTypeBot))
                        {
                            dialog.CheckBoxLabel = string.Format(Strings.Resources.DeleteForUser, ProtoService.GetTitle(chat));
                        }
                    }
                    else if (chat.Type is ChatTypeBasicGroup)
                    {
                        dialog.CheckBoxLabel = Strings.Resources.DeleteForAll;
                    }
                }

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    ProtoService.Send(new DeleteMessages(chat.Id, new[] { message.Id }, dialog.IsChecked == true));
                }
            }
        }

        #endregion

        #region Forward

        public RelayCommand<Message> MessageForwardCommand { get; }
        private async void MessageForwardExecute(Message message)
        {
            SelectionMode = ListViewSelectionMode.None;
            await ShareView.GetForCurrentView().ShowAsync(message);
        }

        #endregion

        #region Multiple Delete

        public RelayCommand MessagesDeleteCommand { get; }
        private async void MessagesDeleteExecute()
        {
            var messages = new List<Message>(SelectedItems);
            var message = messages.FirstOrDefault();
            if (message == null)
            {
                return;
            }

            var chat = ProtoService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            //for (int i = 0; i < messages.Count; i++)
            //{
            //    if (messages[i] is TLMessage message && message.Media is TLMessageMediaGroup groupMedia)
            //    {
            //        messages.RemoveAt(i);

            //        for (int j = 0; j < groupMedia.Layout.Messages.Count; j++)
            //        {
            //            messages.Insert(i, groupMedia.Layout.Messages[j]);
            //            i++;
            //        }

            //        i--;
            //    }
            //}

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
                var dialog = new TLMessageDialog();
                dialog.Title = Strings.Resources.Message;
                dialog.Message = string.Format(Strings.Resources.AreYouSureDeleteMessages, Locale.Declension("Messages", messages.Count));
                dialog.PrimaryButtonText = Strings.Resources.OK;
                dialog.SecondaryButtonText = Strings.Resources.Cancel;

                var canBeDeletedForAllUsers = messages.All(x => x.CanBeDeletedForAllUsers);
                var canBeDeletedOnlyForSelf = messages.All(x => x.CanBeDeletedOnlyForSelf);

                if (canBeDeletedForAllUsers && canBeDeletedOnlyForSelf)
                {
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        var user = ProtoService.GetUser(privata.UserId);
                        if (user != null && !(user.Type is UserTypeBot))
                        {
                            dialog.CheckBoxLabel = string.Format(Strings.Resources.DeleteForUser, ProtoService.GetTitle(chat));
                        }
                    }
                    else if (chat.Type is ChatTypeBasicGroup)
                    {
                        dialog.CheckBoxLabel = Strings.Resources.DeleteForAll;
                    }
                }

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    ProtoService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), dialog.IsChecked == true));
                }
            }
        }

        private bool MessagesDeleteCanExecute()
        {
            return SelectedItems.Count > 0 && SelectedItems.All(x => x.CanBeDeletedForAllUsers || x.CanBeDeletedOnlyForSelf);
        }

        #endregion

        #region Multiple Forward

        public RelayCommand MessagesForwardCommand { get; }
        private async void MessagesForwardExecute()
        {
            var messages = SelectedItems.Where(x => x.CanBeForwarded).OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                SelectionMode = ListViewSelectionMode.None;
                await ShareView.GetForCurrentView().ShowAsync(messages);
            }
        }

        private bool MessagesForwardCanExecute()
        {
            return SelectedItems.Count > 0 && SelectedItems.All(x => x.CanBeForwarded);
        }

        #endregion

        #region Select

        public RelayCommand<Message> MessageSelectCommand { get; }
        private void MessageSelectExecute(Message message)
        {
            SelectionMode = ListViewSelectionMode.Multiple;

            SelectedItems = new List<Message> { message };
            RaisePropertyChanged("SelectedItems");
        }

        #endregion

        #region Delegate

        public bool CanBeDownloaded(MessageViewModel message)
        {
            return true;
        }

        public void DownloadFile(MessageViewModel message, Telegram.Td.Api.File file)
        {
        }

        public void OpenReply(MessageViewModel message)
        {
        }

        public async void OpenFile(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                try
                {
                    var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    var result = await Windows.System.Launcher.LaunchFileAsync(temp);
                    //var folder = await temp.GetParentAsync();
                    //var options = new Windows.System.FolderLauncherOptions();
                    //options.ItemsToSelect.Add(temp);

                    //var result = await Windows.System.Launcher.LaunchFolderAsync(folder, options);
                }
                catch { }
            }
        }

        public void OpenWebPage(WebPage webPage)
        {
        }

        public void OpenSticker(Sticker sticker)
        {
        }

        public void OpenLocation(Location location, string title)
        {
        }

        public void OpenLiveLocation(MessageViewModel message)
        {

        }

        public void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button)
        {
        }

        public void OpenMedia(MessageViewModel message, FrameworkElement target)
        {
        }

        public void PlayMessage(MessageViewModel message)
        {
        }

        public void OpenUsername(string username)
        {
        }

        public void OpenHashtag(string hashtag)
        {
        }

        public void OpenUser(int userId)
        {
        }

        public void OpenChat(long chatId)
        {
        }

        public void OpenChat(long chatId, long messageId)
        {
        }

        public void OpenViaBot(int viaBotUserId)
        {
        }

        public void OpenUrl(string url, bool untrust)
        {
        }

        public void SendBotCommand(string command)
        {
        }

        public bool IsAdmin(int userId)
        {
            return false;
        }

        public void Call(MessageViewModel message)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
