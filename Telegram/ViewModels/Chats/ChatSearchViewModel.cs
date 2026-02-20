//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Chats
{
    public partial class ChatSearchViewModel : ViewModelBase, IDisposable
    {
        private readonly DialogViewModel _dialog;
        private readonly DisposableMutex _loadMoreLock;

        public ChatSearchViewModel(IClientService clientService, INavigationService navigationService, ISettingsService settingsService, IEventAggregator aggregator, DialogViewModel viewModel, string query, MessageSender from)
            : base(clientService, settingsService, aggregator)
        {
            _dialog = viewModel;
            _loadMoreLock = new DisposableMutex();

            NavigationService = navigationService;

            NextCommand = new RelayCommand(NextExecute, NextCanExecute);
            PreviousCommand = new RelayCommand(PreviousExecute, PreviousCanExecute);

            if (!string.IsNullOrEmpty(query) || from != null)
            {
                Search(query, from, null);
            }
        }

        public DialogViewModel Dialog => _dialog;

        private ICollection _autocomplete;
        public ICollection Autocomplete
        {
            get => _autocomplete ?? Items;
            set => Set(ref _autocomplete, value);
        }

        #region Filters

        private string _query;
        public string Query
        {
            get => _query;
            set => Set(ref _query, value);
        }

        private DateTimeOffset? _date;
        public DateTimeOffset? Date
        {
            get => _date;
            set => Set(ref _date, value);
        }

        private MessageSender _from;
        public MessageSender From
        {
            get => _from;
            set => Set(ref _from, value);
        }

        public bool IsFromEnabled
        {
            get
            {
                var chat = Dialog.Chat;
                if (chat == null)
                {
                    return false;
                }

                if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel)
                {
                    return true;
                }

                return false;
            }
        }

        private ReactionType _savedMessagesTag;
        public ReactionType SavedMessagesTag
        {
            get => _savedMessagesTag;
            set
            {
                var reaction = _savedMessagesTag ?? value;

                if (Set(ref _savedMessagesTag, value))
                {
                    Dialog.UpdateSavedMessagesTag(value, _filterByTag, reaction);
                }
            }
        }

        private bool _filterByTag = true;
        public bool FilterByTag
        {
            get => _filterByTag;
            set
            {
                if (Set(ref _filterByTag, value))
                {
                    Dialog.UpdateSavedMessagesTag(_savedMessagesTag, value, _savedMessagesTag);
                }
            }
        }

        #endregion

        #region Results

        public int SelectedIndex
        {
            get
            {
                if (Items == null || SelectedItem == null)
                {
                    return 0;
                }

                var index = Items.IndexOf(SelectedItem);
                //if (index == Items.Count - 1)
                //{
                //    LoadNext();
                //}
                //if (index == 0)
                //{
                //    LoadPrevious();
                //}

                return index;
            }
        }

        protected Message _selectedItem;
        public Message SelectedItem
        {
            get => _selectedItem;
            set
            {
                Set(ref _selectedItem, value);
                NextCommand.RaiseCanExecuteChanged();
                PreviousCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(SelectedIndex));
            }
        }

        protected SearchChatMessagesCollection _items;
        public SearchChatMessagesCollection Items
        {
            get => _items;
            set
            {
                if (Set(ref _items, value) && _autocomplete == null)
                {
                    RaisePropertyChanged(nameof(Autocomplete));
                }
            }
        }

        #endregion

        public void SetSelectedItem(Message message)
        {
            SelectedItem = message;
            _ = Dialog.LoadMessageSliceAsync(null, message.Id);
        }

        public async void Search(string query, MessageSender from, ReactionType savedMessagesTag)
        {
            static bool FromEquals(MessageSender x, MessageSender y)
            {
                if (x is MessageSenderUser userX && y is MessageSenderUser userY)
                {
                    return userX.UserId == userY.UserId;
                }
                else if (x is MessageSenderChat chatX && y is MessageSenderChat chatY)
                {
                    return chatX.ChatId == chatY.ChatId;
                }

                return x == null && y == null;
            }

            if (Dialog.Type == DialogType.EventLog)
            {
                await Dialog.LoadEventLogSliceAsync(query);
            }
            else if (string.Equals(_query, query) && FromEquals(_from, from) && _savedMessagesTag.AreTheSame(savedMessagesTag) && PreviousCanExecute())
            {
                PreviousExecute();
            }
            else
            {
                From = from;
                Query = query;
                SavedMessagesTag = savedMessagesTag;

                var chat = _dialog.Chat;
                if (chat == null)
                {
                    return;
                }

                Items = null;
                SelectedItem = null;

                if (string.IsNullOrEmpty(query) && from == null)
                {
                    return;
                }

                var fromMessageId = 0L;

                //var panel = _dialog.ListField?.ItemsPanelRoot as ItemsStackPanel;
                //if (panel != null && panel.LastVisibleIndex >= 0 && panel.LastVisibleIndex < _dialog.Items.Count && _dialog.Items.Count > 0)
                //{
                //    fromMessageId = _dialog.Items[panel.LastVisibleIndex].Id;
                //}

                var collection = new SearchChatMessagesCollection(ClientService, chat.Id, _dialog.TopicId, query, from, fromMessageId, null, savedMessagesTag);

                var result = await collection.LoadMoreItemsAsync(100);
                if (result.Count > 0)
                {
                    var target = collection.FirstOrDefault();

                    if (fromMessageId != 0)
                    {
                        var closest = collection.Aggregate((x, y) => Math.Abs(x.Id - fromMessageId) < Math.Abs(y.Id - fromMessageId) ? x : y);
                        if (closest != null)
                        {
                            target = closest;
                        }
                    }

                    Items = collection;
                    SelectedItem = target;

                    await Dialog.LoadMessageSliceAsync(null, target.Id);
                    Dialog.UpdateQuery(query);
                }
                else
                {
                    Items = collection;
                    SelectedItem = null;
                }
            }
        }

        public RelayCommand NextCommand { get; }
        private async void NextExecute()
        {
            if (Items == null || SelectedIndex <= 0)
            {
                return;
            }

            SelectedItem = Items[SelectedIndex - 1];

            if (_selectedItem != null)
            {
                await Dialog.LoadMessageSliceAsync(null, _selectedItem.Id);
            }
        }

        private bool NextCanExecute()
        {
            if (Items == null)
            {
                return false;
            }

            return SelectedIndex > 0;
        }

        public RelayCommand PreviousCommand { get; }
        private async void PreviousExecute()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                if (Items == null || SelectedIndex >= Items.TotalCount)
                {
                    return;
                }

                if (SelectedIndex >= Items.Count - 1)
                {
                    var result = await Items.LoadMoreItemsAsync(100);
                    if (result.Count < 1)
                    {
                        return;
                    }
                }

                SelectedItem = Items[SelectedIndex + 1];

                if (_selectedItem != null)
                {
                    await Dialog.LoadMessageSliceAsync(null, _selectedItem.Id);
                }
            }
        }

        private bool PreviousCanExecute()
        {
            if (Items == null)
            {
                return false;
            }

            return SelectedIndex < Items.TotalCount - 1;
        }

        public void Dispose()
        {
            Autocomplete = null;
            Query = null;
            From = null;
            SavedMessagesTag = null;
            Items = null;
            SelectedItem = null;
        }
    }
}
