using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

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
            get { return _autocomplete; }
            set { Set(ref _autocomplete, value); }
        }

        #region Filters

        private string _query;
        public string Query
        {
            get { return _query; }
            set { Set(ref _query, value); }
        }

        private DateTimeOffset? _date;
        public DateTimeOffset? Date
        {
            get { return _date; }
            set { Set(ref _date, value); }
        }

        private User _from;
        public User From
        {
            get { return _from; }
            set { Set(ref _from, value); }
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
            get { return _filter; }
            set { Set(ref _filter, value); }
        }

        public ICollection Filters
        {
            get
            {
                return new List<ChatSearchMediaFilter>
                {
                    new ChatSearchMediaFilter(new SearchMessagesFilterPhoto(), "\uEB9F", Strings.Resources.AutoDownloadPhotos),
                    new ChatSearchMediaFilter(new SearchMessagesFilterVideo(), "\uE768", Strings.Resources.AutoDownloadVideos),
                    new ChatSearchMediaFilter(new SearchMessagesFilterDocument(), "\uE160", Strings.Resources.AutoDownloadFiles),
                    new ChatSearchMediaFilter(new SearchMessagesFilterUrl(), "\uE71B", Strings.Resources.SharedLinks),
                    new ChatSearchMediaFilter(new SearchMessagesFilterAudio(), "\uE8D6", Strings.Resources.SharedAudioFiles),
                    new ChatSearchMediaFilter(new SearchMessagesFilterVoiceNote(), "\uE720", Strings.Resources.AudioAutodownload),
                    new ChatSearchMediaFilter(new SearchMessagesFilterVideoNote(), "\uE612", Strings.Resources.VideoMessagesAutodownload),
                    new ChatSearchMediaFilter(new SearchMessagesFilterAnimation(), "\uF4A9", Strings.Resources.AccDescrGIFs)
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

        public async void Search(string query, User from, SearchMessagesFilter filter)
        {
            if (Dialog.Type == DialogType.EventLog)
            {
                await Dialog.LoadEventLogSliceAsync(query);
            }
            else if (string.Equals(_query, query) && _from?.Id == from?.Id && _filter?.GetType() == filter?.GetType() && PreviousCanExecute())
            {
                PreviousExecute();
            }
            else
            {
                From = from;
                Filter = filter;
                Query = query;

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

                var field = _dialog.ListField;
                if (field != null)
                {
                    var panel = field.ItemsPanelRoot as ItemsStackPanel;
                    if (panel != null && panel.LastVisibleIndex >= 0 && panel.LastVisibleIndex < _dialog.Items.Count && _dialog.Items.Count > 0)
                    {
                        fromMessageId = _dialog.Items[panel.LastVisibleIndex].Id;
                    }
                }

                var collection = new SearchChatMessagesCollection(ProtoService, chat.Id, _dialog.ThreadId, query, from?.Id ?? 0, fromMessageId, filter);
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

    public class ChatSearchMediaFilter
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
