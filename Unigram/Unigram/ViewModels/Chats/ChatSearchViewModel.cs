using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels.Chats
{
    public class ChatSearchViewModel : TLViewModelBase, IDisposable
    {
        private readonly DialogViewModel _dialog;
        private readonly DisposableMutex _loadMoreLock;

        public ChatSearchViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, DialogViewModel viewModel)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _dialog = viewModel;
            _loadMoreLock = new DisposableMutex();

            NextCommand = new RelayCommand(NextExecute, NextCanExecute);
            PreviousCommand = new RelayCommand(PreviousExecute, PreviousCanExecute);
        }

        public DialogViewModel Dialog
        {
            get
            {
                return _dialog;
            }
        }

        private ICollection _autocomplete;
        public ICollection Autocomplete
        {
            get
            {
                return _autocomplete;
            }
            set
            {
                Set(ref _autocomplete, value);
            }
        }

        #region Filters

        private string _query;
        public string Query
        {
            get
            {
                return _query;
            }
            set
            {
                Set(ref _query, value);
            }
        }

        private DateTimeOffset? _date;
        public DateTimeOffset? Date
        {
            get
            {
                return _date;
            }
            set
            {
                Set(ref _date, value);
            }
        }

        private User _from;
        public User From
        {
            get
            {
                return _from;
            }
            set
            {
                Set(ref _from, value);
            }
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
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
                NextCommand.RaiseCanExecuteChanged();
                PreviousCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(() => SelectedIndex);
            }
        }

        protected SearchChatMessagesCollection _items;
        public SearchChatMessagesCollection Items
        {
            get
            {
                return _items;
            }
            set
            {
                Set(ref _items, value);
            }
        }

        #endregion

        public async void Search(string query, User from)
        {
            if (string.Equals(_query, query) && _from?.Id == from?.Id && PreviousCanExecute())
            {
                PreviousExecute();
            }
            else
            {
                From = from;
                Query = query;

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

                var collection = new SearchChatMessagesCollection(ProtoService, chat.Id, query, from?.Id ?? 0, null);
                var result = await collection.LoadMoreItemsAsync(100);
                if (result.Count > 0)
                {
                    Items = collection;
                    SelectedItem = collection.FirstOrDefault();

                    await Dialog.LoadMessageSliceAsync(null, collection[0].Id);
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
            if (Items == null || SelectedIndex >= Items.TotalCount)
            {
                return;
            }

            using (await _loadMoreLock.WaitAsync())
            {
                if (SelectedIndex >= Items.Count - 1)
                {
                    var result = await Items.LoadMoreItemsAsync(100);
                    if (result.Count < 1)
                    {
                        return;
                    }
                }
            }

            SelectedItem = Items[SelectedIndex + 1];

            if (_selectedItem != null)
            {
                await Dialog.LoadMessageSliceAsync(null, _selectedItem.Id);
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
            Items = null;
            SelectedItem = null;
        }
    }
}
