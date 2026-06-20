//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public partial class ChatTextBox : FormattedTextBox
    {
        private TextBlock InlinePlaceholderTextContentPresenter;
        private ScrollViewer ContentElement;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public MessageEffect Effect { get; set; }

        public ChatTextBox()
        {
            DefaultStyleKey = typeof(ChatTextBox);
            TextChanged += OnTextChanged;

            _blockPadding = 48;
        }

        protected override void OnApplyTemplate()
        {
            InlinePlaceholderTextContentPresenter = (TextBlock)GetTemplateChild(nameof(InlinePlaceholderTextContentPresenter));
            ContentElement = (ScrollViewer)GetTemplateChild(nameof(ContentElement));

            base.OnApplyTemplate();
        }

        public event EventHandler Sending;
        public event EventHandler<TappedRoutedEventArgs> Capture;

        protected override void OnTapped(TappedRoutedEventArgs e)
        {
            Capture?.Invoke(this, e);
            base.OnTapped(e);
        }

        protected override async void OnPaste(HandledEventArgs e, DataPackageView package)
        {
            try
            {
                // If the user tries to paste RTF content from any TOM control (Visual Studio, Word, Wordpad, browsers)
                // we have to handle the pasting operation manually to allow plaintext only.
                if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap) || package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                {
                    e.Handled = true;
                    await ViewModel.HandlePackageAsync(package);
                }
                else
                {
                    base.OnPaste(e, package);
                }
            }
            catch { }
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

        private void SetAutocomplete(IAutocompleteCollection collection, bool recycle = false, bool inline = false)
        {
            if (inline is false)
            {
                _emojiQuery = null;
                _emojiFlyout?.Hide();
                CancelEmoji();
            }

            if (collection != null)
            {
                if (ViewModel.Autocomplete is AutocompleteCollection autocomplete)
                {
                    if (autocomplete.Source != collection)
                    {
                        if (recycle)
                        {
                            autocomplete.Update(collection);
                        }
                        else
                        {
                            ViewModel.Autocomplete = new AutocompleteCollection(collection);
                        }
                    }
                }
                else
                {
                    ViewModel.Autocomplete = new AutocompleteCollection(collection);
                }
            }
            else
            {
                ViewModel.Autocomplete = null;
            }
        }

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
                    _emojiQuery = null;
                    _emojiFlyout?.Hide();
                    _emojiToken?.Cancel();

                    e.Handled = true;
                }
            }
            else if (e.Key is VirtualKey.Up or VirtualKey.Down or VirtualKey.Left or VirtualKey.Right or VirtualKey.Tab or VirtualKey.Enter)
            {
                IAutocompleteCollection autocomplete;
                ListViewBase autocompleteList;

                if (_emojiFlyout?.Content is ChatTextFlyout presenter)
                {
                    autocomplete = presenter.ItemsSource;
                    autocompleteList = presenter.ControlledList;
                }
                else
                {
                    autocomplete = ViewModel.Autocomplete;
                    autocompleteList = ControlledList;
                }

                var modifiers = WindowContext.KeyModifiers();

                if (e.Key is VirtualKey.Up or VirtualKey.Down)
                {
                    if (e.Key is VirtualKey.Up or VirtualKey.Down && modifiers == VirtualKeyModifiers.None && autocomplete == null)
                    {
                        if (e.Key == VirtualKey.Up && IsEmpty)
                        {
                            ViewModel.EditLastMessage();
                            e.Handled = true;
                        }
                        else
                        {
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
                    }
                    else if (e.Key == VirtualKey.Up && modifiers == VirtualKeyModifiers.Control)
                    {
                        ViewModel.MessageReplyPrevious();
                        e.Handled = true;
                    }
                    else if (e.Key == VirtualKey.Down && modifiers == VirtualKeyModifiers.Control)
                    {
                        ViewModel.MessageReplyNext();
                        e.Handled = true;
                    }
                    else if (e.Key is VirtualKey.Up or VirtualKey.Down)
                    {
                        if (autocompleteList != null && autocompleteList.Items.Count > 0 && autocomplete?.Orientation == Orientation.Vertical)
                        {
                            autocompleteList.SelectionMode = ListViewSelectionMode.Single;

                            var index = e.Key == VirtualKey.Up ? -1 : 1;
                            var next = autocompleteList.SelectedIndex + index;
                            if (next >= 0 && next < autocomplete.Count)
                            {
                                autocompleteList.SelectedIndex = next;
                                autocompleteList.ScrollIntoView(autocompleteList.SelectedItem);
                            }

                            e.Handled = true;
                        }
                    }
                }
                else if (e.Key is VirtualKey.Left or VirtualKey.Right && modifiers == VirtualKeyModifiers.None)
                {
                    if (autocompleteList != null && autocompleteList.Items.Count > 0 && autocomplete?.Orientation == Orientation.Horizontal)
                    {
                        if (autocompleteList.SelectedIndex == 0 && e.Key == VirtualKey.Left)
                        {
                            autocompleteList.SelectedIndex = -1;
                            e.Handled = true;
                        }
                        else if (autocompleteList.SelectedIndex == autocompleteList.Items.Count - 1 && e.Key == VirtualKey.Right)
                        {
                            autocompleteList.SelectedIndex = 0;
                            e.Handled = true;
                        }
                        else
                        {
                            autocompleteList.SelectionMode = ListViewSelectionMode.Single;

                            var index = e.Key == VirtualKey.Left ? -1 : 1;
                            var next = autocompleteList.SelectedIndex + index;
                            if (next >= 0 && next < autocomplete.Count)
                            {
                                autocompleteList.SelectedIndex = next;
                                autocompleteList.ScrollIntoView(autocompleteList.SelectedItem);

                                e.Handled = true;
                            }
                        }
                    }
                }
                else if (e.Key is VirtualKey.Tab or VirtualKey.Enter && autocompleteList != null && autocompleteList.Items.Count > 0 && autocomplete != null
                    && ((autocomplete.InsertOnKeyDown is false && autocompleteList.SelectedItem != null) || autocomplete.InsertOnKeyDown))
                {
                    if (modifiers == VirtualKeyModifiers.Shift)
                    {
                        return;
                    }

                    var container = autocompleteList.ContainerFromIndex(Math.Max(0, autocompleteList.SelectedIndex)) as GridViewItem;
                    if (container != null)
                    {
                        var peer = new GridViewItemAutomationPeer(container);
                        var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                        provider.Invoke();
                    }

                    Logger.Debug("Tab pressed and handled");
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Tab)
                {
                    // Ignored to allow Ctrl+Tab and Ctrl+Shift+Tab to switch chats
                    if (modifiers != VirtualKeyModifiers.None)
                    {
                        return;
                    }
                }
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        protected override bool CanAccept()
        {
            return ViewModel.CurrentInlineBot == null;
        }

        protected override void OnAccept()
        {
            Send();
        }

        private DateTime _lastKeystroke;

        private void OnTextChanged(object sender, RoutedEventArgs e)
        {
            if (IsEmpty)
            {
                return;
            }

            var diff = DateTime.Now - _lastKeystroke;
            if (diff.TotalSeconds > 4 || (_wasEmpty && !IsEmpty))
            {
                _lastKeystroke = DateTime.Now;
                ViewModel.ChatActionManager.SetTyping(new ChatActionTyping());
            }
        }

        private CancellationTokenSource _inlineBotToken;

        private void CancelInlineBotToken()
        {
            if (_inlineBotToken != null)
            {
                _inlineBotToken.Cancel();
                _inlineBotToken.Dispose();
                _inlineBotToken = null;
            }
        }

        private void GetInlineBotResults(string inlineQuery)
        {
            if (ViewModel.InlineBotResults != null && string.Equals(inlineQuery, ViewModel.InlineBotResults.Query, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CancelInlineBotToken();

            _inlineBotToken = new CancellationTokenSource();
            ViewModel.GetInlineBotResults(inlineQuery, _inlineBotToken.Token);
        }

        private void ClearInlineBotResults()
        {
            CancelInlineBotToken();

            ViewModel.CurrentInlineBot = null;
            ViewModel.InlineBotResults = null;
            UpdateInlinePlaceholder(null, null);
        }

        protected override async void OnSelectionChanged(RichEditBox sender, bool fromTextChanging)
        {
            if (_isMenuExpanded)
            {
                if (ViewModel.Autocomplete is not AutocompleteList)
                {
                    ClearInlineBotResults();
                    SetAutocomplete(GetCommands(string.Empty));
                }

                return;
            }

            Document.GetText(TextGetOptions.NoHidden, out string text);

            // This needs to run before text empty check as it cleans up
            // some stuff it inline bot isn't found
            if (SearchInlineBotResults(text, true, out string inlineQuery))
            {
                SetAutocomplete(null);
                GetInlineBotResults(inlineQuery);
                return;
            }

            if (string.IsNullOrEmpty(text) || Document.Selection.Length != 0)
            {
                ClearInlineBotResults();
                SetAutocomplete(null);
                return;
            }

            var query = text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length));
            var selection = Document.Selection.GetClone();

            var prev = ViewModel.Autocomplete;

            if (prev is AutocompleteCollection collection)
            {
                prev = collection.Source;
            }

            if (TryGetAutocomplete(selection, text, query, prev, fromTextChanging, out var autocomplete, out bool recycle, out bool inline))
            {
                ClearInlineBotResults();
                SetAutocomplete(autocomplete, recycle, inline);
            }
            else
            {
                SetAutocomplete(null);
                CancelInlineBotToken();

                var token = (_inlineBotToken = new CancellationTokenSource()).Token;
                if (SearchByInlineBot(query, out string username, out _) && await ViewModel.ResolveInlineBotAsync(username, token))
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (SearchInlineBotResults(text, true, out query))
                    {
                        SetAutocomplete(null);
                        GetInlineBotResults(query);
                        return;
                    }
                }

                ClearInlineBotResults();
            }
        }

        private bool TryGetAutocomplete(ITextRange selection, string text, string query, IAutocompleteCollection prev, bool fromTextChanging, out IAutocompleteCollection autocomplete, out bool recycle, out bool inline)
        {
            autocomplete = null;
            recycle = false;
            inline = false;

            if (AutocompleteEntityFinder.TrySearch(selection, out AutocompleteEntity entity, out string result, out int index))
            {
                if (entity == AutocompleteEntity.Username)
                {
                    var chat = ViewModel.Chat;
                    if (chat == null)
                    {
                        return false;
                    }

                    if (prev is UsernameCollection && prev.Query.Equals(result))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    var guestBots = chat.Type is not ChatTypeSecret;
                    var members = chat.Type is /*ChatTypePrivate or ChatTypeSecret or*/ ChatTypeBasicGroup or ChatTypeSupergroup { IsChannel: false };

                    autocomplete = new UsernameCollection(ViewModel.ClientService, ViewModel.Chat.Id, ViewModel.TopicId, result, index == 0, guestBots, members, false);
                    recycle = prev is UsernameCollection;

                    return true;
                }
                else if (entity == AutocompleteEntity.Hashtag)
                {
                    if (prev is SearchHashtagsCollection && prev.Query.Equals(result))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    autocomplete = new SearchHashtagsCollection(ViewModel.ClientService, result);
                    recycle = prev is SearchHashtagsCollection;

                    return true;
                }
                else if (entity == AutocompleteEntity.Sticker)
                {
                    if (index == 0 && ViewModel.ComposerHeader?.Editing == null)
                    {
                        ShowOrUpdateEmojiFlyout(0, new SearchStickersCollection(ViewModel.ClientService, ViewModel.Settings, true, text, ViewModel.Chat?.Id ?? 0));
                        inline = true;

                        var chat = ViewModel.Chat;
                        if (chat == null || !chat.CanSendOtherMessages(ViewModel.ClientService))
                        {
                            autocomplete = null;
                            return true;
                        }

                        if (prev is SearchStickersCollection collection && !collection.IsCustomEmoji && prev.Query.Equals(text.Trim()))
                        {
                            autocomplete = prev;
                            return true;
                        }

                        autocomplete = new SearchStickersCollection(ViewModel.ClientService, ViewModel.Settings, false, text.Trim(), chat.Id);
                        return true;
                    }
                    else
                    {
                        ShowOrUpdateEmojiFlyout(index, new SearchStickersCollection(ViewModel.ClientService, ViewModel.Settings, true, result, ViewModel.Chat?.Id ?? 0));

                        autocomplete = null;
                        inline = true;
                        return true;
                    }
                }
                else if (entity == AutocompleteEntity.Emoji && fromTextChanging)
                {
                    ShowOrUpdateEmojiFlyout(index, new EmojiCollection(ViewModel.ClientService, result, ViewModel.Chat.Id));

                    autocomplete = null;
                    inline = true;
                    return true;
                }
                else if (entity == AutocompleteEntity.Command && index == 0)
                {
                    if (prev is AutocompleteList && prev.Query.Equals(result))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    autocomplete = GetCommands(result);
                    recycle = prev is AutocompleteList;

                    return true;
                }
            }

            autocomplete = null;
            return false;
        }

        private Flyout _emojiFlyout;
        private string _emojiQuery;
        private CancellationTokenSource _emojiToken;

        public CancellationTokenSource CancelEmoji()
        {
            _emojiToken?.Cancel();
            _emojiToken = new();
            return _emojiToken;
        }

        private async void ShowOrUpdateEmojiFlyout(int index, IAutocompleteCollection collection)
        {
            if (_emojiQuery == collection.Query)
            {
                return;
            }

            var token = CancelEmoji();
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

            _emojiQuery = collection.Query;

            if (_emojiFlyout?.Content is ChatTextFlyout presenter)
            {
                presenter.Update(collection);
                return;
            }

            var range = Document.GetRange(index, index);
            range.GetRect(PointOptions.None, out Rect rect, out _);

            var style = new Style
            {
                TargetType = typeof(FlyoutPresenter),
                BasedOn = BootStrapper.Current.Resources["CommandFlyoutPresenterStyle"] as Style
            };

            style.Setters.Add(new Setter(FlyoutPresenter.IsDefaultShadowEnabledProperty, false));
            style.Setters.Add(new Setter(FlyoutPresenter.MinWidthProperty, 40));

            _emojiFlyout = new Flyout
            {
                Content = new ChatTextFlyout(this, source),
                AllowFocusOnInteraction = false,
                ShouldConstrainToRootBounds = false,
                FlyoutPresenterStyle = style,
            };

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
            if (sender is Flyout { Content: ChatTextFlyout { Parent: FlyoutPresenter flyout } presenter })
            {
                AutomationProperties.GetControlledPeers(this).Clear();
                AutomationProperties.GetControlledPeers(this).Add(presenter.ControlledList);

                var child = VisualTreeHelper.GetChild(flyout, 0);
                if (child is UIElement element)
                {
                    element.Translation = new Vector3(0, 0, 12);
                    element.Shadow = new ThemeShadow();
                }
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

        private AutocompleteList GetCommands(string command)
        {
            var all = ViewModel.BotCommands;
            if (all != null)
            {
                var results = new AutocompleteList(command, all.Where(x => x.Item.Command.StartsWith(command, StringComparison.OrdinalIgnoreCase)));
                if (results.Count > 0)
                {
                    return results;
                }
            }
            else if (ViewModel.ClientService.TryGetUser(ViewModel.Chat, out var user) && user.Type is UserTypeRegular)
            {
                // TODO: is this actually needed?
                ViewModel.ClientService.Send(new LoadQuickReplyShortcuts());

                var replies = ViewModel.ClientService.GetQuickReplyShortcuts();

                var results = new AutocompleteList(command, replies.Where(x => x.Name.StartsWith(command, StringComparison.OrdinalIgnoreCase)));
                if (results.Count > 0)
                {
                    return results;
                }
            }

            return null;
        }

        public partial class UsernameCollection : MvxObservableCollection<object>, IAutocompleteCollection, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly long _chatId;
            private readonly MessageTopic _topicId;
            private readonly string _query;

            private readonly bool _bots;
            private readonly bool _guestBots;
            private readonly bool _members;
            private readonly bool _self;

            private bool _hasMore = true;

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

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    count = 0;
                    _hasMore = false;

                    if (_bots)
                    {
                        var response = await _clientService.SendAsync(new GetTopChats(new TopChatCategoryInlineBots(), 10));
                        if (response is Telegram.Td.Api.Chats chats)
                        {
                            foreach (var id in chats.ChatIds)
                            {
                                var user = _clientService.GetUser(_clientService.GetChat(id));
                                if (user != null && (user.HasActiveUsername(_query, out _) || ClientEx.SearchByPrefix(user.FullName(), _query)))
                                {
                                    Add(user);
                                    count++;
                                }
                            }
                        }
                    }

                    if (_guestBots)
                    {
                        var response = await _clientService.SendAsync(new GetTopChats(new TopChatCategoryGuestBots(), 10));
                        if (response is Telegram.Td.Api.Chats chats)
                        {
                            foreach (var id in chats.ChatIds)
                            {
                                var user = _clientService.GetUser(_clientService.GetChat(id));
                                if (user != null && (user.HasActiveUsername(_query, out _) || ClientEx.SearchByPrefix(user.FullName(), _query)))
                                {
                                    Add(user);
                                    count++;
                                }
                            }
                        }
                    }

                    if (_members)
                    {
                        if (_self && string.IsNullOrEmpty(_query) && _clientService.TryGetUser(_clientService.Options.MyId, out Td.Api.User self))
                        {
                            Add(self);
                            count++;
                        }

                        var response = await _clientService.SendAsync(new SearchChatMembers(_chatId, _query, 20, new ChatMembersFilterMention(_topicId)));
                        if (response is ChatMembers members)
                        {
                            foreach (var member in members.Members)
                            {
                                if (_clientService.TryGetUser(member.MemberId, out Td.Api.User user))
                                {
                                    if (user.Id == _clientService.Options.MyId)
                                    {
                                        continue;
                                    }

                                    Add(user);
                                    count++;
                                }
                            }
                        }
                    }

                    return new LoadMoreItemsResult { Count = count };
                });
            }

            public bool HasMoreItems => _hasMore;

            public string Query => _query;

            public Orientation Orientation => Orientation.Vertical;

            public bool InsertOnKeyDown => true;
        }

        public partial class EmojiCollection : MvxObservableCollection<object>, IAutocompleteCollection, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly string _query;
            private readonly string _inputLanguage;
            private readonly long _chatId;

            private bool _hasMore = true;

            private string _emoji;

            public EmojiCollection(IClientService clientService, string query, long chatId)
            {
                _clientService = clientService;
                _query = query.Replace('_', ' ');
                _inputLanguage = NativeUtils.GetKeyboardCulture();
                _chatId = chatId;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
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
                                var index = SettingsService.Current.Emoji.RecentEmoji.IndexOf(emoji);
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

                    if (_emoji?.Length > 0)
                    {
                        var response = await _clientService.SendAsync(new GetStickers(new StickerTypeCustomEmoji(), _emoji, 1000, _chatId));
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

        public partial class SearchHashtagsCollection : MvxObservableCollection<object>, IAutocompleteCollection, ISupportIncrementalLoading
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
                return AsyncInfo.Run(async token =>
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

        public async void Send(bool disableNotification = false)
        {
            Document.GetText(TextGetOptions.NoHidden, out string plain);

            // This needs to run before text empty check as it cleans up
            // some stuff if inline bot isn't found
            if (SearchInlineBotResults(plain, false, out string inlineQuery))
            {
                if (string.IsNullOrEmpty(inlineQuery))
                {
                    SetText(null);
                }
                else
                {
                    var split = plain.Split(' ');
                    var username = split[0] + " ";

                    Document.SetText(TextSetOptions.None, username);
                    Document.Selection.StartPosition = username.Length;
                }

                return;
            }
            else if (ViewModel.Type == DialogType.ScheduledMessages && ViewModel.ComposerHeader?.Editing == null)
            {
                Schedule(false);
                return;
            }

            var options = await ViewModel.PickMessageSendOptionsAsync(1, SchedulingState.Auto, disableNotification, false);
            if (options == null)
            {
                return;
            }

            options.EffectId = Effect?.Id ?? 0;

            Sending?.Invoke(this, EventArgs.Empty);
            Effect = null;

            var linkPreview = ViewModel.GetLinkPreviewOptions();
            var text = GetFormattedText(true);

            await ViewModel.SendMessageAsync(text, linkPreview, options);
        }

        public async void Schedule(bool whenOnline)
        {
            var options = await ViewModel.PickMessageSendOptionsAsync(1, whenOnline ? SchedulingState.WhenOnline : SchedulingState.Schedule);
            if (options != null)
            {
                options.EffectId = Effect?.Id ?? 0;

                Sending?.Invoke(this, EventArgs.Empty);
                Effect = null;

                var linkPreview = ViewModel.GetLinkPreviewOptions();
                var text = GetFormattedText(true);

                await ViewModel.SendMessageAsync(text, linkPreview, options);
            }
        }

        protected override void OnGettingFormattedText()
        {
        }

        protected override void OnSettingText()
        {
            UpdateInlinePlaceholder(null, null);
        }

        private bool _isMenuExpanded;
        public bool IsMenuExpanded
        {
            get => _isMenuExpanded;
            set
            {
                if (ViewModel?.Chat?.Type is not ChatTypePrivate && value)
                {
                    return;
                }

                _isMenuExpanded = value;
                OnSelectionChanged(this, false);
            }
        }

        #region Username

        public static bool SearchByInlineBot(string text, out string searchText, out int index)
        {
            index = -1;
            searchText = string.Empty;

            var found = true;
            var i = 0;

            while (i < text.Length)
            {
                if (i == 0 && text[i] != '@')
                {
                    found = false;
                    break;
                }
                else if (text[i] == ' ')
                {
                    index = i;
                    break;
                }
                else if (text[i] == '@')
                {
                    i++;
                }
                else
                {
                    if (!MessageHelper.IsValidUsernameSymbol(text[i]))
                    {
                        found = false;
                        break;
                    }

                    i++;
                }
            }

            if (found)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(0, index).TrimStart('@');
            }

            return found;
        }

        #endregion

        #region Inline bots

        private bool SearchInlineBotResults(string text, bool apply, out string searchText)
        {
            var flag = false;
            searchText = string.Empty;

            if (text.EndsWith('\r'))
            {
                text = text.Substring(0, text.Length - 1);
            }

            if (text.StartsWith('@'))
            {
                text = text.Substring(1);
            }

            var split = text.Split(' ');
            if (split.Length >= 1 && ViewModel.CurrentInlineBot != null && ViewModel.CurrentInlineBot.HasActiveUsername(split[0], out string username))
            {
                searchText = ReplaceFirst(text.TrimStart(), username, string.Empty);
                if (searchText.StartsWith(" "))
                {
                    searchText = ReplaceFirst(searchText, " ", string.Empty);
                    flag = true;
                }

                if (apply)
                {
                    if (!flag)
                    {
                        if (string.Equals(text.TrimStart(), "@" + username, StringComparison.OrdinalIgnoreCase))
                        {
                            ClearInlineBotResults();
                        }
                        else
                        {
                            var user = ViewModel.CurrentInlineBot;
                            if (user != null && user.Type is UserTypeBot bot)
                            {
                                UpdateInlinePlaceholder(username, bot.InlineQueryPlaceholder);
                            }
                        }
                    }
                    else if (string.IsNullOrEmpty(searchText))
                    {
                        var user = ViewModel.CurrentInlineBot;
                        if (user != null && user.Type is UserTypeBot bot)
                        {
                            UpdateInlinePlaceholder(username, bot.InlineQueryPlaceholder);
                        }
                    }
                    else
                    {
                        UpdateInlinePlaceholder(null, null);
                    }
                }
            }
            else if (apply)
            {
                ClearInlineBotResults();
            }

            searchText = searchText.TrimEnd('\r', '\n');
            return flag;
        }

        public string ReplaceFirst(string text, string search, string replace)
        {
            var index = text.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return text;
            }

            return text.Substring(0, index) + replace + text.Substring(index + search.Length);
        }

        private bool _hasInlinePlaceholder;

        private void UpdateInlinePlaceholder(string username, string placeholder)
        {
            if (InlinePlaceholderTextContentPresenter != null)
            {
                if (username != null && placeholder != null)
                {
                    _hasInlinePlaceholder = true;
                    InlinePlaceholderTextContentPresenter.Inlines.Clear();
                    InlinePlaceholderTextContentPresenter.Inlines.Add(new Run { Text = "@" + username + " ", Foreground = null });
                    InlinePlaceholderTextContentPresenter.Inlines.Add(new Run { Text = placeholder });
                }
                else if (_hasInlinePlaceholder)
                {
                    _hasInlinePlaceholder = false;
                    InlinePlaceholderTextContentPresenter.Inlines.Clear();
                }
            }
        }

        #endregion

        #region Reply

        public object Reply
        {
            get => GetValue(ReplyProperty);
            set => SetValue(ReplyProperty, value);
        }

        // Using a DependencyProperty as the backing store for Reply.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ReplyProperty =
            DependencyProperty.Register("Reply", typeof(object), typeof(ChatTextBox), new PropertyMetadata(null, OnReplyChanged));

        private static void OnReplyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatTextBox)d).OnReplyChanged(e.NewValue, e.OldValue);
        }

        private async void OnReplyChanged(object newValue, object oldValue)
        {
            if (newValue != null)
            {
                await Task.Delay(200);
                Focus(FocusState.Keyboard);
            }
        }

        #endregion
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
            : base(collection, new AutocompleteDiffHandler(), Constants.DiffOptions)
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
                    var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, source, DefaultDiffHandler, DefaultOptions));

                    if (token.IsCancellationRequested)
                    {
                        _loading = false;
                        return;
                    }

                    ReplaceDiff(diff);
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

                    var token = Cancel();
                    var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, source, DefaultDiffHandler, DefaultOptions));

                    if (token.IsCancellationRequested)
                    {
                        _loading = false;
                        return;
                    }

                    ReplaceDiff(diff);
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

                    var token = Cancel();
                    var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, source, DefaultDiffHandler, DefaultOptions));

                    if (token.IsCancellationRequested)
                    {
                        _loading = false;
                        return;
                    }

                    ReplaceDiff(diff);
                    UpdateEmpty();

                    _loading = false;
                }
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async _ =>
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
                    var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, _source, DefaultDiffHandler, DefaultOptions));

                    if (token.IsCancellationRequested)
                    {
                        _loading = false;
                        return result;
                    }

                    ReplaceDiff(diff);
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
