//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Profile
{
    public partial class MediaSourceCollection : BindableBase, IDiffHandler<MessageWithOwner>
    {
        public MediaSourceCollection(Func<object, string, MediaCollection> factory, SearchMessagesFilter filter)
        {
            Items = new SearchCollection<MessageWithOwner, MediaCollection>(factory, _filter = filter, this);
        }

        public SearchCollection<MessageWithOwner, MediaCollection> Items { get; private set; }

        public string Query
        {
            get => Items.Query;
            set => Items.Query = value;
        }

        public void UpdateQuery(string query)
        {
            Items.UpdateQuery(query);
        }

        public bool Empty()
        {
            if (DataSource != null)
            {
                return DataSource.Count > 0;
            }

            return Items.Count > 0;
        }

        private SearchMessagesFilter _filter;
        public SearchMessagesFilter Filter
        {
            get => _filter;
            set
            {
                _filter = value;
                DataSource?.SetFilter(value);
                Items.UpdateSender(value);
            }
        }

        private MediaDataSource _dataSource;
        public MediaDataSource DataSource
        {
            get => _dataSource;
            set
            {
                _dataSource = value;
                UseDataSource = value != null;
            }
        }

        protected bool _useDataSource;
        public bool UseDataSource
        {
            get => _useDataSource && _dataSource != null;
            set
            {
                if (_useDataSource != value)
                {
                    _useDataSource = value;
                    RaisePropertyChanged(nameof(ItemsView));
                }
            }
        }

        public object ItemsView
        {
            get
            {
                if (_useDataSource && DataSource != null)
                {
                    return DataSource;
                }

                return Items;
            }
        }

        public bool CompareItems(MessageWithOwner oldItem, MessageWithOwner newItem)
        {
            return oldItem?.Id == newItem?.Id && oldItem?.ChatId == newItem?.ChatId;
        }

        public void UpdateItem(MessageWithOwner oldItem, MessageWithOwner newItem)
        {
        }
    }

    public abstract partial class MediaTabsViewModelBase : MultiViewModelBase
    {
        private readonly IStorageService _storageService;

        private readonly IMessageDelegate _messageDelegate;

        public MediaTabsViewModelBase(IClientService clientService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _storageService = storageService;

            _messageDelegate = new MessageDelegate(this);

            Media = new MediaSourceCollection(SetSearch, new SearchMessagesFilterPhotoAndVideo());
            Files = new MediaSourceCollection(SetSearch, new SearchMessagesFilterDocument());
            Links = new MediaSourceCollection(SetSearch, new SearchMessagesFilterUrl());
            Music = new MediaSourceCollection(SetSearch, new SearchMessagesFilterAudio());
            Voice = new MediaSourceCollection(SetSearch, new SearchMessagesFilterVoiceAndVideoNote());
            Animations = new MediaSourceCollection(SetSearch, new SearchMessagesFilterAnimation());

            SelectedItems = new MvxObservableCollection<MessageWithOwner>();
            SelectedItems.CollectionChanged += OnCollectionChanged;
        }

        private async void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var selectedItems = SelectedItems.Where(x => x != null).ToList();
            var properties = await ClientService.GetMessagePropertiesAsync(selectedItems.Select(x => new MessageId(x)));

            CanDeleteSelectedMessages = properties.Count > 0 && properties.Values.All(x => x.CanBeDeletedForAllUsers || x.CanBeDeletedOnlyForSelf);
            CanForwardSelectedMessages = properties.Count > 0 && properties.Values.All(x => x.CanBeForwarded);
        }

        public IStorageService StorageService => _storageService;

        public MessageTopic Topic { get; set; }

        public bool IsDeactivated { get; set; }

        public MediaSourceCollection Media { get; private set; }
        public MediaSourceCollection Files { get; private set; }
        public MediaSourceCollection Links { get; private set; }
        public MediaSourceCollection Music { get; private set; }
        public MediaSourceCollection Voice { get; private set; }
        public MediaSourceCollection Animations { get; private set; }

        public virtual MediaCollection SetSearch(object sender, string query)
        {
            if (sender is SearchMessagesFilter filter && !IsDeactivated)
            {
                return new MediaCollection(ClientService, filter, query);
            }

            return null;
        }

        public ObservableCollection<MessageWithOwner> SelectedItems { get; }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateDeleteMessages>(this, Handle);
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.FromCache)
            {
                return;
            }

            // TODO: this isn't really valid at the moment
            if (ShouldHandleDeleteMessages(update))
            {
                var table = update.MessageIds.ToHashSet();

                BeginOnUIThread(() =>
                {
                    UpdateDeleteMessages(Media.Items.Source, table);
                    UpdateDeleteMessages(Files.Items.Source, table);
                    UpdateDeleteMessages(Links.Items.Source, table);
                    UpdateDeleteMessages(Music.Items.Source, table);
                    UpdateDeleteMessages(Voice.Items.Source, table);
                    UpdateDeleteMessages(Animations.Items.Source, table);
                });
            }
        }

        protected virtual bool ShouldHandleDeleteMessages(UpdateDeleteMessages update)
        {
            return true;
        }

        private void UpdateDeleteMessages(MediaCollection target, HashSet<long> table)
        {
            //target.Cancel();

            for (int i = 0; i < target.Count; i++)
            {
                var message = target[i];
                if (table.Contains(message.Id))
                {
                    target.RemoveAt(i);
                    i--;

                    break;
                }
            }
        }

        #region View

        public void ViewMessage(MessageWithOwner message)
        {
            NavigationService.NavigateToChat(message.Chat, message.Id, Topic);
        }

        #endregion

        #region Save file as

        public async void SaveMessageMedia(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.SaveFileAsAsync(file);
            }
        }

        #endregion

        #region Open with

        public async void OpenMessageWith(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenFileWithAsync(file);
            }
        }

        #endregion

        #region Show in folder

        public async void OpenMessageFolder(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenFolderAsync(file);
            }
        }

        #endregion

        #region Copy path

        public async void CopyMessagePath(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.CopyFilePathAsync(XamlRoot, file);
            }
        }

        #endregion

        #region Delete

        public void DeleteMessage(MessageWithOwner message)
        {
            if (message == null)
            {
                return;
            }

            var chat = ClientService.GetChat(message.ChatId);
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

            DeleteMessages(chat, [message]);
        }

        private async void DeleteMessages(Chat chat, IList<MessageWithOwner> messages)
        {
            var items = messages
                .Where(x => x != null)
                .DistinctBy(x => x.Id)
                .ToList();

            if (items.Empty())
            {
                return;
            }

            var properties = await ClientService.GetMessagePropertiesAsync(items.Select(x => new MessageId(x)));

            var updated = items
                .Where(x => properties.ContainsKey(new MessageId(x)))
                .ToList();

            if (updated.Empty())
            {
                return;
            }

            var popup = new DeleteMessagesPopup(ClientService, chat, Topic, updated, properties);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            UnselectMessages();

            ClientService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), popup.Revoke));

            foreach (var sender in popup.DeleteAll)
            {
                ClientService.Send(new DeleteChatMessagesBySender(chat.Id, sender));
            }

            foreach (var sender in popup.BanUser)
            {
                ClientService.Send(new SetChatMemberStatus(chat.Id, sender, popup.SelectedStatus));
            }

            if (chat.Type is ChatTypeSupergroup supertype)
            {
                foreach (var sender in popup.ReportSpam)
                {
                    var messageIds = messages
                        .Where(x => x.SenderId.AreTheSame(sender))
                        .Select(x => x.Id)
                        .ToList();

                    ClientService.Send(new ReportSupergroupSpam(supertype.SupergroupId, messageIds));
                }
            }
        }

        #endregion

        #region Forward

        public void ForwardMessage(MessageWithOwner message)
        {
            var selectedItems = new[] { message }.ToDictionary(x => new MessageId(x));

            UnselectMessages();
            ForwardMessages(selectedItems);
        }

        #endregion

        #region Multiple Delete

        public void DeleteSelectedMessages()
        {
            var messages = new List<MessageWithOwner>(SelectedItems);

            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var chat = ClientService.GetChat(first.ChatId);
            if (chat == null)
            {
                return;
            }

            DeleteMessages(chat, messages);
        }

        private bool _canDeleteSelectedMessages;
        public bool CanDeleteSelectedMessages
        {
            get => _canDeleteSelectedMessages;
            set => Set(ref _canDeleteSelectedMessages, value);
        }

        #endregion

        #region Multiple Forward

        public void ForwardSelectedMessages()
        {
            var selectedItems = SelectedItems
                .Where(x => x != null)
                .DistinctBy(x => x.Id)
                .ToDictionary(x => new MessageId(x));

            UnselectMessages();
            ForwardMessages(selectedItems);
        }

        private async void ForwardMessages(Dictionary<MessageId, MessageWithOwner> selectedItems)
        {
            var properties = await ClientService.GetMessagePropertiesAsync(selectedItems.Select(x => x.Key));

            var messages = properties.Where(x => x.Value.CanBeForwarded).OrderBy(x => x.Key.Id).ToList();
            if (messages.Count > 0)
            {
                var messagesToShare = new List<MessageToShare>(messages.Count);

                foreach (var property in messages)
                {
                    if (selectedItems.TryGetValue(property.Key, out var message))
                    {
                        messagesToShare.Add(new MessageToShare(message, property.Value));
                    }
                }

                ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationShareMessages(messagesToShare));
            }
        }

        private bool _canForwardSelectedMessages;
        public bool CanForwardSelectedMessages
        {
            get => _canForwardSelectedMessages;
            set => Set(ref _canForwardSelectedMessages, value);
        }

        #endregion

        #region Select

        public void SelectMessage(MessageWithOwner message)
        {
            SelectedItems.Add(message);
        }

        #endregion

        #region Unselect

        public void UnselectMessages()
        {
            SelectedItems.Clear();
        }

        #endregion

        #region Delegate

        public IMessageDelegate MessageDelegate => _messageDelegate;

        public void OpenUsername(string username)
        {
            _messageDelegate.OpenUsername(username);
        }

        public void OpenUser(long userId)
        {
            _messageDelegate.OpenUser(userId);
        }

        public void OpenUrl(string url, bool untrust)
        {
            _messageDelegate.OpenUrl(url, untrust);
        }

        #endregion
    }
}
