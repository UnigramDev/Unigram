//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;

namespace Telegram.ViewModels.Chats
{
    public partial class ChatSearchViewModel : ViewModelBase, IDisposable
    {
        private readonly DialogViewModel _dialog;
        private readonly DisposableMutex _loadMoreLock;

        public ChatSearchViewModel(IClientService clientService, INavigationService navigationService, ISettingsService settingsService, IEventAggregator aggregator, DialogViewModel viewModel, string query)
            : base(clientService, settingsService, aggregator)
        {
            _dialog = viewModel;
            _loadMoreLock = new DisposableMutex();

            NavigationService = navigationService;

            NextCommand = new RelayCommand(NextExecute, NextCanExecute);
            PreviousCommand = new RelayCommand(PreviousExecute, PreviousCanExecute);

            if (!string.IsNullOrEmpty(query))
            {
                Search(query, null, null, null);
            }
        }

        public DialogViewModel Dialog => _dialog;

        private ICollection _autocomplete;
        public ICollection Autocomplete
        {
            get => _autocomplete;
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

        private SearchMessagesFilter _filter;
        public SearchMessagesFilter Filter
        {
            get => _filter;
            set => Set(ref _filter, value);
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

        public ICollection Filters
        {
            get
            {
                return new List<ChatSearchMediaFilter>
                {
                    new ChatSearchMediaFilter(new SearchMessagesFilterPhoto(), Icons.Image, Strings.AutoDownloadPhotos),
                    new ChatSearchMediaFilter(new SearchMessagesFilterVideo(), Icons.Play, Strings.AutoDownloadVideos),
                    new ChatSearchMediaFilter(new SearchMessagesFilterDocument(), Icons.Document, Strings.AutoDownloadFiles),
                    new ChatSearchMediaFilter(new SearchMessagesFilterUrl(), Icons.Link, Strings.SharedLinks),
                    new ChatSearchMediaFilter(new SearchMessagesFilterAudio(), Icons.MusicNote, Strings.SharedAudioFiles),
                    new ChatSearchMediaFilter(new SearchMessagesFilterVoiceNote(), Icons.MicOn, Strings.AudioAutodownload),
                    new ChatSearchMediaFilter(new SearchMessagesFilterVideoNote(), "\uE612", Strings.VideoMessagesAutodownload),
                    new ChatSearchMediaFilter(new SearchMessagesFilterAnimation(), Icons.Gif, Strings.AccDescrGIFs)
                    //new SearchMessagesFilterCall(),
                    //new SearchMessagesFilterChatPhoto(),
                    //new SearchMessagesFilterMention(),
                    //new SearchMessagesFilterMissedCall(),
                    //new SearchMessagesFilterPhotoAndVideo(),
                    //new SearchMessagesFilterUnreadMention(),
                    //new SearchMessagesFilterVoiceAndVideoNote(),
                };
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
            set => Set(ref _items, value);
        }

        #endregion

        public async void ShowResults()
        {
            var popup = new SearchChatResultsPopup(Items);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary && popup.SelectedItem != null)
            {
                SelectedItem = popup.SelectedItem;
                await Dialog.LoadMessageSliceAsync(null, popup.SelectedItem.Id);
            }
        }

        public async void Search(string query, MessageSender from, SearchMessagesFilter filter, ReactionType savedMessagesTag)
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
            else if (string.Equals(_query, query) && FromEquals(_from, from) && _filter?.GetType() == filter?.GetType() && _savedMessagesTag.AreTheSame(savedMessagesTag) && PreviousCanExecute())
            {
                PreviousExecute();
            }
            else
            {
                From = from;
                Filter = filter;
                Query = query;
                SavedMessagesTag = savedMessagesTag;

                var chat = _dialog.Chat;
                if (chat == null)
                {
                    return;
                }

                Items = null;
                SelectedItem = null;

                if (string.IsNullOrEmpty(query) && from == null && filter == null)
                {
                    return;
                }

                var fromMessageId = 0L;

                //var panel = _dialog.ListField?.ItemsPanelRoot as ItemsStackPanel;
                //if (panel != null && panel.LastVisibleIndex >= 0 && panel.LastVisibleIndex < _dialog.Items.Count && _dialog.Items.Count > 0)
                //{
                //    fromMessageId = _dialog.Items[panel.LastVisibleIndex].Id;
                //}

                var collection = new SearchChatMessagesCollection(ClientService, chat.Id, _dialog.ThreadId, _dialog.SavedMessagesTopicId, query, from, fromMessageId, filter, savedMessagesTag);

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

    public partial class ChatSearchMediaFilter
    {
        public SearchMessagesFilter Filter { get; private set; }
        public string Glyph { get; private set; }
        public string Text { get; private set; }

        public ChatSearchMediaFilter(SearchMessagesFilter filter, string glyph, string text)
        {
            Filter = filter;
            Glyph = glyph;
            Text = text;
        }
    }
}
