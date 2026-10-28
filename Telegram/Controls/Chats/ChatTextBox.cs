//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Chats
{
    /// <summary>
    /// The composer's box over RichEdit. What makes it the composer is in
    /// <see cref="ComposerController"/>; what is left here is RichEdit's: its emoji and custom
    /// emoji suggestions, the caret jumping to the ends, the inline bot's placeholder drawn over
    /// the text, and a space typed after formatting that ends it.
    /// </summary>
    public partial class ChatTextBox : FormattedTextBox
    {
        private TextBlock InlinePlaceholderTextContentPresenter;
        private ScrollViewer ContentElement;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ChatTextBox()
        {
            DefaultStyleKey = typeof(ChatTextBox);

            _blockPadding = 48;
        }

        protected override void OnApplyTemplate()
        {
            InlinePlaceholderTextContentPresenter = (TextBlock)GetTemplateChild(nameof(InlinePlaceholderTextContentPresenter));
            ContentElement = (ScrollViewer)GetTemplateChild(nameof(ContentElement));

            base.OnApplyTemplate();
        }

        public event EventHandler<TappedRoutedEventArgs> Capture;

        protected override void OnTapped(TappedRoutedEventArgs e)
        {
            Capture?.Invoke(this, e);
            base.OnTapped(e);
        }

        private OrientableListView _controlledList;
        public OrientableListView ControlledList
        {
            get => _controlledList;
            set => SetControlledList(value);
        }

        private void SetControlledList(OrientableListView value)
        {
            if (_controlledList != null)
            {
                AutomationProperties.GetControlledPeers(this).Remove(_controlledList);
            }

            _controlledList = value;

            if (_controlledList != null)
            {
                AutomationProperties.GetControlledPeers(this).Add(_controlledList);
            }
        }

        public override bool HasOpenSuggestions => _emojiFlyout != null;

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space && Document.Selection.Length == 0)
            {
                try
                {
                    var clone = Document.Selection.GetClone();
                    if (!AreTheSame(clone.CharacterFormat, Document.Selection.CharacterFormat))
                    {
                        Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                    }
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
            else if (e.Key is VirtualKey.Escape)
            {
                if (_emojiFlyout != null)
                {
                    HideEmoji();

                    e.Handled = true;
                }
            }
            else if (_emojiFlyout?.Content is ChatTextFlyout presenter && presenter.ItemsSource is IAutocompleteCollection autocomplete)
            {
                e.Handled = ProcessFlyoutKey(e.Key, presenter.ControlledList, autocomplete);
            }
            else if (e.Key is VirtualKey.Up or VirtualKey.Down && WindowContext.KeyModifiers() == VirtualKeyModifiers.None && ViewModel?.Autocomplete == null)
            {
                // On the first line Up goes to the start, on the last Down goes to the end, as
                // in a text box; the composer has already taken Up in an empty one.
                Document.Selection.GetRect(PointOptions.ClientCoordinates, out Rect rect, out _);

                if (e.Key == VirtualKey.Up && rect.Y.AlmostEqualsToZero())
                {
                    Document.Selection.SetRange(0, 0);
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Down && rect.Bottom >= ContentElement.ExtentHeight - 1)
                {
                    Document.Selection.SetRange(TextConstants.MaxUnitCount, TextConstants.MaxUnitCount);
                    e.Handled = true;
                }
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        // The emoji flyout's list runs across: Left and Right move along it, Tab or Enter pick.
        internal static bool ProcessFlyoutKey(VirtualKey key, ListViewBase list, IAutocompleteCollection autocomplete)
        {
            var modifiers = WindowContext.KeyModifiers();
            if (list == null || list.Items.Count == 0 || modifiers == VirtualKeyModifiers.Control || modifiers == VirtualKeyModifiers.Menu)
            {
                return false;
            }

            if (key is VirtualKey.Left or VirtualKey.Right && modifiers == VirtualKeyModifiers.None && autocomplete.Orientation == Orientation.Horizontal)
            {
                if (list.SelectedIndex == 0 && key == VirtualKey.Left)
                {
                    list.SelectedIndex = -1;
                    return true;
                }
                else if (list.SelectedIndex == list.Items.Count - 1 && key == VirtualKey.Right)
                {
                    list.SelectedIndex = 0;
                    return true;
                }

                list.SelectionMode = ListViewSelectionMode.Single;

                var next = list.SelectedIndex + (key == VirtualKey.Left ? -1 : 1);
                if (next >= 0 && next < autocomplete.Count)
                {
                    list.SelectedIndex = next;
                    list.ScrollIntoView(list.SelectedItem);
                    return true;
                }

                return false;
            }

            if (key is VirtualKey.Tab or VirtualKey.Enter && modifiers != VirtualKeyModifiers.Shift
                && (autocomplete.InsertOnKeyDown || list.SelectedItem != null))
            {
                if (list.ContainerFromIndex(Math.Max(0, list.SelectedIndex)) is GridViewItem container
                    && new GridViewItemAutomationPeer(container).GetPattern(PatternInterface.Invoke) is IInvokeProvider provider)
                {
                    provider.Invoke();
                }

                return true;
            }

            return false;
        }

        protected override void OnSelectionChanged(RichEditBox sender, bool fromTextChanging)
        {
            var viewModel = ViewModel;
            if (viewModel == null || IsEmpty || Document.Selection.Length != 0
                || !AutocompleteEntityFinder.TrySearch(Document.Selection.GetClone(), out AutocompleteEntity entity, out string result, out int index))
            {
                HideEmoji();
                return;
            }

            var chatId = viewModel.Chat?.Id ?? 0;

            if (entity == AutocompleteEntity.Sticker)
            {
                // A message that is one emoji is a query for custom emoji as a whole; the
                // composer offers stickers for it as well, in the list under the box.
                if (index == 0 && viewModel.ComposerHeader?.Editing == null)
                {
                    Document.GetText(TextGetOptions.NoHidden, out string text);
                    ShowOrUpdateEmojiFlyout(0, new SearchStickersCollection(viewModel.ClientService, true, text, chatId));
                }
                else
                {
                    ShowOrUpdateEmojiFlyout(index, new SearchStickersCollection(viewModel.ClientService, true, result, chatId));
                }
            }
            else if (entity == AutocompleteEntity.Emoji && fromTextChanging)
            {
                ShowOrUpdateEmojiFlyout(index, new EmojiCollection(viewModel.ClientService, viewModel.Settings.RecentEmoji, result, chatId));
            }
            else
            {
                HideEmoji();
            }
        }

        private Flyout _emojiFlyout;
        private string _emojiQuery;
        private CancellationTokenSource _emojiToken;

        public void CancelEmoji()
        {
            if (_emojiToken != null)
            {
                _emojiToken.Cancel();
                _emojiToken.Dispose();
                _emojiToken = null;
            }
        }

        public void HideEmoji()
        {
            _emojiQuery = null;
            _emojiFlyout?.Hide();
            CancelEmoji();
        }

        // Separate from cancelling, which happens on almost every keystroke and has no
        // query of its own to hand a token to
        private CancellationTokenSource BeginEmoji()
        {
            CancelEmoji();
            return _emojiToken = new CancellationTokenSource();
        }

        private async void ShowOrUpdateEmojiFlyout(int index, IAutocompleteCollection collection)
        {
            if (_emojiQuery == collection.Query)
            {
                return;
            }

            _emojiQuery = collection.Query;

            var token = BeginEmoji();
            var source = new AutocompleteCollection(collection);

            var result = await source.LoadMoreItemsAsync(0);
            if (result.Count == 0 || token.IsCancellationRequested || !this.IsConnected())
            {
                // Only reset if this is the active query
                if (token == _emojiToken)
                {
                    _emojiQuery = null;
                    _emojiFlyout?.Hide();
                }

                return;
            }

            if (_emojiFlyout?.Content is ChatTextFlyout presenter)
            {
                presenter.Update(collection);
                return;
            }

            var range = Document.GetRange(index, index);
            range.GetRect(PointOptions.None, out Rect rect, out _);

            _emojiFlyout = ChatTextFlyout.CreateFlyout(new ChatTextFlyout(this, source));

            _emojiFlyout.Opened += EmojiFlyout_Opened;
            _emojiFlyout.Closing += EmojiFlyout_Closing;

            _emojiFlyout.ShowAt(this, new FlyoutShowOptions
            {
                Position = new Windows.Foundation.Point(rect.X + Padding.Left - 8, rect.Y + 6 - ContentElement.VerticalOffset),
                Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
                ShowMode = FlyoutShowMode.Transient
            });
        }

        void EmojiFlyout_Opened(object sender, object args)
        {
            if (ChatTextFlyout.Elevate(sender) is ChatTextFlyout presenter)
            {
                AutomationProperties.GetControlledPeers(this).Clear();
                AutomationProperties.GetControlledPeers(this).Add(presenter.ControlledList);
            }
        }

        private void EmojiFlyout_Closing(object sender, object e)
        {
            _emojiFlyout.Opened -= EmojiFlyout_Opened;
            _emojiFlyout.Closing -= EmojiFlyout_Closing;

            _emojiFlyout = null;

            AutomationProperties.GetControlledPeers(this).Clear();

            if (_controlledList != null)
            {
                AutomationProperties.GetControlledPeers(this).Add(_controlledList);
            }
        }

        protected override void OnSettingText()
        {
            UpdateInlinePlaceholder(null, null);
        }

        public override void SetInlinePlaceholder(string prefix, string hint)
        {
            UpdateInlinePlaceholder(prefix, hint);
        }

        private bool _hasInlinePlaceholder;

        // Drawn over the text, its prefix in no colour so that the hint starts where the text ends.
        private void UpdateInlinePlaceholder(string prefix, string hint)
        {
            if (InlinePlaceholderTextContentPresenter != null)
            {
                if (prefix != null && hint != null)
                {
                    _hasInlinePlaceholder = true;
                    InlinePlaceholderTextContentPresenter.Inlines.Clear();
                    InlinePlaceholderTextContentPresenter.Inlines.Add(new Run { Text = prefix, Foreground = null });
                    InlinePlaceholderTextContentPresenter.Inlines.Add(new Run { Text = hint });
                }
                else if (_hasInlinePlaceholder)
                {
                    _hasInlinePlaceholder = false;
                    InlinePlaceholderTextContentPresenter.Inlines.Clear();
                }
            }
        }

        public partial class UsernameCollection : IncrementalCollection<object>, IAutocompleteCollection, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly long _chatId;
            private readonly MessageTopic _topicId;
            private readonly string _query;

            private readonly bool _bots;
            private readonly bool _guestBots;
            private readonly bool _members;
            private readonly bool _self;

            public UsernameCollection(IClientService clientService, long chatId, MessageTopic topicId, string query, bool bots, bool guestBots, bool members, bool self)
            {
                _clientService = clientService;
                _chatId = chatId;
                _topicId = topicId;
                _query = query;

                _bots = bots;
                _guestBots = guestBots;
                _members = members;
                _self = self;
            }

            protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
            {
                var totalCount = 0u;
                var userIds = new HashSet<long>();

                if (_bots)
                {
                    var response = await _clientService.SendAsync(new GetTopChats(new TopChatCategoryInlineBots(), 10));
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        foreach (var chat in _clientService.GetChats(chats.ChatIds))
                        {
                            var user = _clientService.GetUser(chat);
                            if (user != null && !userIds.Contains(user.Id) && (user.HasActiveUsername(_query, out _) || ClientEx.SearchByPrefix(user.FullName(), _query)))
                            {
                                userIds.Add(user.Id);

                                Add(user);
                                totalCount++;
                            }
                        }
                    }
                }

                if (_guestBots)
                {
                    var response = await _clientService.SendAsync(new GetTopChats(new TopChatCategoryGuestBots(), 10));
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        foreach (var chat in _clientService.GetChats(chats.ChatIds))
                        {
                            var user = _clientService.GetUser(chat);
                            if (user != null && !userIds.Contains(user.Id) && (user.HasActiveUsername(_query, out _) || ClientEx.SearchByPrefix(user.FullName(), _query)))
                            {
                                userIds.Add(user.Id);

                                Add(user);
                                totalCount++;
                            }
                        }
                    }
                }

                if (_members)
                {
                    if (_self && string.IsNullOrEmpty(_query) && _clientService.TryGetUser(_clientService.Options.MyId, out Td.Api.User self))
                    {
                        Add(self);
                        totalCount++;
                    }

                    var response = await _clientService.SendAsync(new SearchChatMembers(_chatId, _query, 20, new ChatMembersFilterMention(_topicId)));
                    if (response is ChatMembers members)
                    {
                        foreach (var member in members.Members)
                        {
                            if (_clientService.TryGetUser(member.MemberId, out Td.Api.User user))
                            {
                                if (user.Id == _clientService.Options.MyId || userIds.Contains(user.Id))
                                {
                                    continue;
                                }

                                Add(user);
                                totalCount++;
                            }
                        }
                    }
                }

                return new IncrementalLoadResult(totalCount, false);
            }

            public string Query => _query;

            public Orientation Orientation => Orientation.Vertical;

            public bool InsertOnKeyDown => true;
        }

        public partial class EmojiCollection : RangeObservableCollection<object>, IAutocompleteCollection, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly RecentEmojiSettings _recent;
            private readonly string _query;
            private readonly string _inputLanguage;
            private readonly long _chatId;
            private readonly bool _customEmoji;

            private bool _hasMore = true;

            private string _emoji;

            public EmojiCollection(IClientService clientService, RecentEmojiSettings recent, string query, long chatId, bool customEmoji = true)
            {
                _clientService = clientService;
                _recent = recent;
                _query = query.Replace('_', ' ');
                _inputLanguage = NativeUtils.GetKeyboardCulture();
                _chatId = chatId;
                _customEmoji = customEmoji;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return IncrementalLoading.Run(async token =>
                {
                    count = 0;

                    if (_emoji == null)
                    {
                        var response = await _clientService.SendAsync(new SearchEmojis(_query, new[] { _inputLanguage }));
                        if (response is EmojiKeywords emojis)
                        {
                            var results = new List<string>();
                            var recent = new Dictionary<int, string>();

                            var distinct = emojis.EmojiKeywordsValue
                                .DistinctBy(x => x.Emoji)
                                .Select(x => x.Emoji);

                            foreach (var emoji in distinct)
                            {
                                var index = _recent.Items.IndexOf(emoji);
                                if (index >= 0)
                                {
                                    recent[index] = emoji;
                                }
                                else
                                {
                                    results.Add(emoji);
                                }
                            }

                            foreach (var emoji in recent.OrderByDescending(x => x.Key))
                            {
                                results.Insert(0, emoji.Value);
                            }

                            _emoji = string.Join(" ", results);

                            foreach (var emoji in results)
                            {
                                Add(new EmojiData(emoji));
                                count++;
                            }

                            return new LoadMoreItemsResult { Count = count };
                        }
                    }

                    if (_customEmoji && _emoji?.Length > 0)
                    {
                        var response = await _clientService.SendAsync(new GetStickers(new StickerTypeCustomEmoji(), _emoji, 50, _chatId));
                        if (response is Stickers stickers)
                        {
                            foreach (var sticker in stickers.StickersValue)
                            {
                                Add(sticker);
                                count++;
                            }
                        }
                    }

                    _hasMore = false;
                    return new LoadMoreItemsResult { Count = count };
                });
            }

            public bool HasMoreItems => _hasMore;

            public string Query => _query;

            public Orientation Orientation => Orientation.Horizontal;

            public bool InsertOnKeyDown => true;
        }

        public partial class SearchHashtagsCollection : RangeObservableCollection<object>, IAutocompleteCollection, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly string _query;

            private bool _hasMore = true;

            public SearchHashtagsCollection(IClientService clientService, string query)
            {
                _clientService = clientService;
                _query = query;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return IncrementalLoading.Run(async token =>
                {
                    count = 0;
                    _hasMore = false;

                    var response = await _clientService.SendAsync(new SearchHashtags(_query, 20));
                    if (response is Hashtags hashtags)
                    {
                        foreach (var value in hashtags.HashtagsValue)
                        {
                            Add("#" + value);
                            count++;
                        }
                    }

                    return new LoadMoreItemsResult
                    {
                        Count = count
                    };
                });
            }

            public bool HasMoreItems => _hasMore;

            public string Query => _query;

            public Orientation Orientation => Orientation.Vertical;

            public bool InsertOnKeyDown => true;
        }
    }

    public interface IAutocompleteCollection : ICollection, IEnumerable<object>
    {
        public string Query { get; }

        public Orientation Orientation { get; }

        public bool InsertOnKeyDown { get; }
    }

    public partial class AutocompleteList : List<object>, IAutocompleteCollection
    {
        public string Query { get; }

        public Orientation Orientation { get; set; } = Orientation.Vertical;

        public bool InsertOnKeyDown { get; } = true;

        public AutocompleteList(string query, IEnumerable<object> collection)
            : base(collection)
        {
            Query = query;
        }
    }

    public partial class AutocompleteDiffHandler : IDiffHandler<object>
    {
        public bool CompareItems(object oldItem, object newItem)
        {
            if (oldItem is EmojiData oldEmoji && newItem is EmojiData newEmoji)
            {
                return oldEmoji.Value == newEmoji.Value;
            }
            else if (oldItem is Sticker oldSticker && newItem is Sticker newSticker)
            {
                return oldSticker.Id == newSticker.Id && oldSticker.SetId == newSticker.SetId;
            }
            else if (oldItem is User oldUser && newItem is User newUser)
            {
                return oldUser.Id == newUser.Id;
            }

            return false;
        }

        public void UpdateItem(object oldItem, object newItem)
        {

        }
    }

    public partial class AutocompleteCollection : DiffObservableCollection<object>, ISupportIncrementalLoading, IAutocompleteCollection
    {
        private CancellationTokenSource _cancellation;

        private IAutocompleteCollection _source;
        private ISupportIncrementalLoading _incrementalSource;

        private bool _initialized;
        private bool _loading;

        public AutocompleteCollection(IAutocompleteCollection collection)
            : base(collection, new AutocompleteDiffHandler())
        {
            _source = collection;
            _incrementalSource = collection as ISupportIncrementalLoading;
            _initialized = collection is not ISupportIncrementalLoading;
        }

        public IAutocompleteCollection Source => _source;

        public CancellationTokenSource Cancel()
        {
            _cancellation?.Cancel();
            _cancellation = new();
            return _cancellation;
        }

        public void Update(IAutocompleteCollection source)
        {
            UpdateImpl(source, false);
        }

        private async void UpdateImpl(IAutocompleteCollection source, bool reentrancy)
        {
            if (source is ISupportIncrementalLoading incremental && incremental.HasMoreItems)
            {
                _source = source;
                _incrementalSource = incremental;

                if (_initialized)
                {
                    _loading = true;

                    var token = Cancel();

                    await incremental.LoadMoreItemsAsync(0);

                    if (token.IsCancellationRequested)
                    {
                        _loading = false;
                        return;
                    }

                    // Same rule as SearchCollection: computing this off-thread raced the two
                    // enumerations, and the positions it reports only hold while nothing else
                    // touches this collection.
                    ReplaceDiff(source);
                    UpdateEmpty();

                    _loading = false;

                    // I'm not sure in what condition this can happen, but it happens
                    if (Count < 1 && incremental.HasMoreItems && !reentrancy)
                    {
                        UpdateImpl(source, true);
                    }
                }
            }
            else
            {
                _source = source;
                _incrementalSource = null;

                if (_initialized)
                {
                    _loading = true;

                    // Same rule as SearchCollection: computing this off-thread raced the two
                    // enumerations, and the positions it reports only hold while nothing else
                    // touches this collection.
                    ReplaceDiff(source);
                    UpdateEmpty();

                    _loading = false;
                }
            }
        }

        public async void Replace(IAutocompleteCollection source)
        {
            if (source is ISupportIncrementalLoading incremental)
            {
                _source = source;
                _incrementalSource = incremental;

                if (_initialized)
                {
                    _loading = true;

                    // Same rule as SearchCollection: computing this off-thread raced the two
                    // enumerations, and the positions it reports only hold while nothing else
                    // touches this collection.
                    ReplaceDiff(source);
                    UpdateEmpty();

                    _loading = false;
                }
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return IncrementalLoading.Run(async _ =>
            {
                if (_loading || _incrementalSource == null || !_incrementalSource.HasMoreItems)
                {
                    return new LoadMoreItemsResult
                    {
                        Count = 0
                    };
                }

                _loading = true;

                var token = Cancel();
                var result = await _incrementalSource?.LoadMoreItemsAsync(count);

                if (result.Count > 0 && !token.IsCancellationRequested)
                {
                    ReplaceDiff(_source);
                    UpdateEmpty();
                }

                _initialized = true;
                _loading = false;

                return result;
            });
        }

        public bool HasMoreItems
        {
            get
            {
                if (_incrementalSource != null)
                {
                    return _incrementalSource.HasMoreItems;
                }

                _initialized = true;
                return false;
            }
        }

        private bool _isEmpty = true;
        public bool IsEmpty
        {
            get => _isEmpty;
            private set
            {
                if (_isEmpty != value)
                {
                    _isEmpty = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsEmpty)));
                }
            }
        }

        public string Query => _source.Query;

        public Orientation Orientation => _source.Orientation;

        public bool InsertOnKeyDown => _source.InsertOnKeyDown;

        private void UpdateEmpty()
        {
            IsEmpty = Count == 0;
        }
    }
}
