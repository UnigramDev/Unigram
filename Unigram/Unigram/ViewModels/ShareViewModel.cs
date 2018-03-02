using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class ShareViewModel : UnigramViewModelBase
    {
        public ShareViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, ChatsViewModel dialogs)
            : base(protoService, cacheService, aggregator)
        {
            Items = new MvxObservableCollection<Chat>();
            SelectedItems = new MvxObservableCollection<Chat>();

            SendCommand = new RelayCommand(SendExecute, () => SelectedItems?.Count > 0);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //if (mode == NavigationMode.New)
            //{
            //    _dialogs = null;
            //}

            var response = await ProtoService.SendAsync(new GetChats(long.MaxValue, 0, 200));
            if (response is TdWindows.Chats chats)
            {
                var list = ProtoService.GetChats(chats.ChatIds);

                Items.ReplaceWith(list);
            }

            //var dialogs = GetDialogs();
            //if (dialogs != null)
            //{
            //    Items.ReplaceWith(dialogs);
            //}

            //return Task.CompletedTask;
        }

        private MvxObservableCollection<Chat> _selectedItems = new MvxObservableCollection<Chat>();
        public MvxObservableCollection<Chat> SelectedItems
        {
            get
            {
                return _selectedItems;
            }
            set
            {
                Set(ref _selectedItems, value);
                SendCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _comment;
        public string Comment
        {
            get
            {
                return _comment;
            }
            set
            {
                Set(ref _comment, value);
            }
        }

        private IList<Message> _messages;
        public IList<Message> Messages
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

        private InputMessageContent _inputMedia;
        public InputMessageContent InputMedia
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

        public MvxObservableCollection<Chat> Items { get; private set; }



        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var chats = SelectedItems.ToList();
            if (chats.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_comment))
            {
                var formatted = GetFormattedText(_comment);

                foreach (var chat in chats)
                {
                    var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, false, false, null, new InputMessageText(formatted, false, false)));
                }
            }

            if (_messages != null)
            {
                foreach (var chat in chats)
                {
                    var response = await ProtoService.SendAsync(new ForwardMessages(chat.Id, _messages[0].ChatId, _messages.Select(x => x.Id).ToList(), false, false, false));
                    //if (response is TdWindows.Messages messages)
                    //{
                    //    foreach (var message in messages.MessagesData)
                    //    {
                    //        Aggregator.Publish(new UpdateNewMessage(message, true, false));
                    //    }
                    //}
                }

                NavigationService.GoBack();
            }
            else if (_inputMedia != null)
            {
                foreach (var chat in chats)
                {
                    var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, false, false, null, _inputMedia));
                }

                NavigationService.GoBack();
            }
            else if (ShareLink != null)
            {

                NavigationService.GoBack();
            }

            //App.InMemoryState.ForwardMessages = new List<TLMessage>(messages);
            //NavigationService.GoBackAt(0);
        }

        private FormattedText GetFormattedText(string text)
        {
            if (text == null)
            {
                return new FormattedText();
            }

            text = text.Format();

            var entities = Markdown.Parse(ProtoService, ref text);
            if (entities == null)
            {
                entities = new List<TextEntity>();
            }

            return new FormattedText(text, entities);

            //return ProtoService.Execute(new ParseTextEntities(text.Format(), new TextParseModeMarkdown())) as FormattedText;
        }

        #region Search

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.Multiple;
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

        public async void Search(string text)
        {
            var results = await SearchLocalAsync(text);
            if (results != null)
            {
                SelectionMode = ListViewSelectionMode.None;
                //Items.ReplaceWith(results.Cast<TLDialog>().Select(x => x.With));
            }
            else
            {
                //var dialogs = GetDialogs();
                //if (dialogs != null)
                //{
                //    foreach (var item in _selectedItems)
                //    {
                //        //dialogs.Remove(item);
                //        //dialogs.Insert(0, item);

                //        //if (dialogs.Contains(item)) { }
                //        //else
                //        //{
                //        //    dialogs.Insert(0, item);
                //        //}
                //    }

                //    SelectionMode = ListViewSelectionMode.None;
                //    //Items.ReplaceWith(dialogs);
                //}
            }
        }

        private async Task<KeyedList<string, System.Object>> SearchLocalAsync(string query1)
        {
            //if (string.IsNullOrWhiteSpace(query1))
            //{
            //    return null;
            //}

            //var dialogs = await Task.Run(() => CacheService.GetDialogs());
            //var contacts = await Task.Run(() => CacheService.GetContacts());

            //if (dialogs != null && contacts != null)
            //{
            //    var query = LocaleHelper.GetQuery(query1);

            //    var simple = new List<TLDialog>();
            //    var parent = dialogs.Where(dialog =>
            //    {
            //        if (dialog.With is TLUser user)
            //        {
            //            return user.IsLike(query, StringComparison.OrdinalIgnoreCase);
            //        }
            //        else if (dialog.With is TLChannel channel)
            //        {
            //            return channel.IsLike(query, StringComparison.OrdinalIgnoreCase);
            //        }
            //        else if (dialog.With is TLChat chat)
            //        {
            //            return !chat.HasMigratedTo && chat.IsLike(query, StringComparison.OrdinalIgnoreCase);
            //        }
            //        else
            //        {
            //            return false;
            //        }
            //    }).ToList();

            //    var contactsResults = contacts.OfType<TLUser>().Where(x => x.IsLike(query, StringComparison.OrdinalIgnoreCase));

            //    foreach (var result in contactsResults)
            //    {
            //        var dialog = parent.FirstOrDefault(x => x.Peer.TypeId == TLType.PeerUser && x.Id == result.Id);
            //        if (dialog == null)
            //        {
            //            simple.Add(new TLDialog
            //            {
            //                With = result,
            //                Peer = new TLPeerUser { UserId = result.Id }
            //            });
            //        }
            //    }

            //    if (parent.Count > 0 || simple.Count > 0)
            //    {
            //        return new KeyedList<string, TLObject>(null, parent.Union(simple.OrderBy(x => x.With.DisplayName)));
            //    }
            //}

            return null;
        }

        #endregion
    }
}
