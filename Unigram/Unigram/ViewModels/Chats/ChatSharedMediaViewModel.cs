using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Popups;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatSharedMediaViewModel : TLViewModelBase, IMessageDelegate, IDelegable<IFileDelegate>, IHandle<UpdateFile>, IHandle<UpdateDeleteMessages>
    {
        public IFileDelegate Delegate { get; set; }

        private readonly IPlaybackService _playbackService;

        public ChatSharedMediaViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IPlaybackService playbackService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _playbackService = playbackService;

            MessagesForwardCommand = new RelayCommand(MessagesForwardExecute, MessagesForwardCanExecute);
            MessagesDeleteCommand = new RelayCommand(MessagesDeleteExecute, MessagesDeleteCanExecute);
            MessageViewCommand = new RelayCommand<Message>(MessageViewExecute);
            MessageSaveCommand = new RelayCommand<Message>(MessageSaveExecute);
            MessageDeleteCommand = new RelayCommand<Message>(MessageDeleteExecute);
            MessageForwardCommand = new RelayCommand<Message>(MessageForwardExecute);
            MessageSelectCommand = new RelayCommand<Message>(MessageSelectExecute);
        }

        public IPlaybackService PlaybackService => _playbackService;

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var chatId = (long)parameter;

            if (state.TryGet("selectedIndex", out int selectedIndex))
            {
                SelectedIndex = selectedIndex;
            }

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

            Aggregator.Subscribe(this);
            return Task.CompletedTask;
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        public void Handle(UpdateFile update)
        {
            BeginOnUIThread(() => Delegate?.UpdateFile(update.File));
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.ChatId == _chat?.Id && !update.FromCache)
            {
                var table = update.MessageIds.ToImmutableHashSet();

                BeginOnUIThread(() =>
                {
                    UpdateDeleteMessages(Media, table);
                    UpdateDeleteMessages(Files, table);
                    UpdateDeleteMessages(Links, table);
                    UpdateDeleteMessages(Music, table);
                    UpdateDeleteMessages(Voice, table);
                });
            }
        }

        private void UpdateDeleteMessages(IList<Message> target, ImmutableHashSet<long> table)
        {
            for (int i = 0; i < target.Count; i++)
            {
                var message = target[i];
                if (table.Contains(message.Id))
                {
                    target.RemoveAt(i);
                    i--;
                }
            }
        }

        private Chat _chat;
        public Chat Chat
        {
            get { return _chat; }
            set { Set(ref _chat, value); }
        }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { Set(ref _selectedIndex, value); }
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
        private void MessageDeleteExecute(Message message)
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

            MessagesDelete(chat, new[] { message });
        }

        private async void MessagesDelete(Chat chat, IList<Message> messages)
        {
            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetMessages(chat.Id, messages.Select(x => x.Id).ToArray()));
            if (response is Messages updated)
            {
                for (int i = 0; i < updated.MessagesValue.Count; i++)
                {
                    if (updated.MessagesValue[i] != null)
                    {
                        messages[i] = updated.MessagesValue[i];
                    }
                    else
                    {
                        messages.RemoveAt(i);
                        updated.MessagesValue.RemoveAt(i);

                        i--;
                    }
                }
            }

            var firstSender = first.Sender as MessageSenderUser;

            var sameUser = firstSender != null && messages.All(x => x.Sender is MessageSenderUser senderUser && senderUser.UserId == firstSender.UserId);
            var dialog = new DeleteMessagesPopup(CacheService, messages.Where(x => x != null).ToArray());

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            SelectionMode = ListViewSelectionMode.None;

            if (dialog.DeleteAll && sameUser)
            {
                ProtoService.Send(new DeleteChatMessagesFromUser(chat.Id, firstSender.UserId));
            }
            else
            {
                ProtoService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), dialog.Revoke));
            }

            if (dialog.BanUser && sameUser)
            {
                ProtoService.Send(new SetChatMemberStatus(chat.Id, firstSender.UserId, new ChatMemberStatusBanned()));
            }

            if (dialog.ReportSpam && sameUser && chat.Type is ChatTypeSupergroup supertype)
            {
                ProtoService.Send(new ReportSupergroupSpam(supertype.SupergroupId, firstSender.UserId, messages.Select(x => x.Id).ToList()));
            }
        }

        #endregion

        #region Forward

        public RelayCommand<Message> MessageForwardCommand { get; }
        private async void MessageForwardExecute(Message message)
        {
            SelectionMode = ListViewSelectionMode.None;
            await SharePopup.GetForCurrentView().ShowAsync(message);
        }

        #endregion

        #region Multiple Delete

        public RelayCommand MessagesDeleteCommand { get; }
        private void MessagesDeleteExecute()
        {
            var messages = new List<Message>(SelectedItems);

            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var chat = ProtoService.GetChat(first.ChatId);
            if (chat == null)
            {
                return;
            }

            MessagesDelete(chat, messages);
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
                await SharePopup.GetForCurrentView().ShowAsync(messages);
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

        public void ReplyToMessage(MessageViewModel message)
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

        public void OpenBankCardNumber(string number)
        {
        }

        public void OpenUser(int userId)
        {
        }

        public void OpenChat(long chatId, bool profile = false)
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

        public void Call(MessageViewModel message, bool video)
        {
            throw new NotImplementedException();
        }

        public void VotePoll(MessageViewModel message, IList<PollOption> options)
        {
            throw new NotImplementedException();
        }

        public string GetAdminTitle(int userId)
        {
            throw new NotImplementedException();
        }

        public string GetAdminTitle(MessageSender sender)
        {
            throw new NotImplementedException();
        }

        public void OpenThread(MessageViewModel message)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
