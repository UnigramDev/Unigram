//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

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

        private void SetAutocomplete(IAutocompleteCollection collection, bool recycle = false)
        {
            if (collection != null)
            {
                if (ViewModel.Autocomplete is AutocompleteCollection autocomplete && recycle)
                {
                    autocomplete.Update(collection);
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
                    if (clone.EndPosition > Document.Selection.EndPosition && AreTheSame(clone.CharacterFormat, Document.Selection.CharacterFormat))
                    {

                    }
                    else
                    {
                        Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                    }
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
            else if (e.Key is VirtualKey.Up or VirtualKey.Down)
            {
                var alt = WindowContext.IsKeyDown(VirtualKey.Menu);
                var ctrl = WindowContext.IsKeyDown(VirtualKey.Control);
                var shift = WindowContext.IsKeyDown(VirtualKey.Shift);

                if (e.Key is VirtualKey.Up or VirtualKey.Down && !alt && !ctrl && !shift && ViewModel.Autocomplete == null)
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
                else if (e.Key == VirtualKey.Up && ctrl && !alt && !shift)
                {
                    ViewModel.MessageReplyPrevious();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Down && ctrl && !alt && !shift)
                {
                    ViewModel.MessageReplyNext();
                    e.Handled = true;
                }
                else if (e.Key is VirtualKey.Up or VirtualKey.Down)
                {
                    if (ControlledList != null && ControlledList.Items.Count > 0 && ViewModel.Autocomplete?.Orientation == Orientation.Vertical)
                    {
                        ControlledList.SelectionMode = ListViewSelectionMode.Single;

                        var index = e.Key == VirtualKey.Up ? -1 : 1;
                        var next = ControlledList.SelectedIndex + index;
                        if (next >= 0 && next < ViewModel.Autocomplete.Count)
                        {
                            ControlledList.SelectedIndex = next;
                            ControlledList.ScrollIntoView(ControlledList.SelectedItem);
                        }

                        e.Handled = true;
                    }
                }
            }
            else if (e.Key is VirtualKey.Left or VirtualKey.Right)
            {
                if (ControlledList != null && ControlledList.Items.Count > 0 && ViewModel.Autocomplete?.Orientation == Orientation.Horizontal)
                {
                    if (ControlledList.SelectedIndex == 0 && e.Key == VirtualKey.Left)
                    {
                        ControlledList.SelectedIndex = -1;
                        e.Handled = true;
                    }
                    else if (ControlledList.SelectedIndex == ControlledList.Items.Count - 1 && e.Key == VirtualKey.Right)
                    {
                        ControlledList.SelectedIndex = 0;
                        e.Handled = true;
                    }
                    else
                    {
                        ControlledList.SelectionMode = ListViewSelectionMode.Single;

                        var index = e.Key == VirtualKey.Left ? -1 : 1;
                        var next = ControlledList.SelectedIndex + index;
                        if (next >= 0 && next < ViewModel.Autocomplete.Count)
                        {
                            ControlledList.SelectedIndex = next;
                            ControlledList.ScrollIntoView(ControlledList.SelectedItem);

                            e.Handled = true;
                        }
                    }
                }
            }
            else if ((e.Key == VirtualKey.Tab || e.Key == VirtualKey.Enter) && ControlledList != null && ControlledList.Items.Count > 0 && ViewModel.Autocomplete != null
                && ((ViewModel.Autocomplete.InsertOnKeyDown is false && ControlledList.SelectedItem != null) || ViewModel.Autocomplete.InsertOnKeyDown))
            {
                var shift = WindowContext.IsKeyDown(VirtualKey.Shift);
                if (shift)
                {
                    return;
                }

                var container = ControlledList.ContainerFromIndex(Math.Max(0, ControlledList.SelectedIndex)) as GridViewItem;
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
                var ctrl = WindowContext.IsKeyDown(VirtualKey.Control);
                if (ctrl)
                {
                    return;
                }
            }
            else if (e.Key == VirtualKey.X && Math.Abs(Document.Selection.Length) == 4)
            {
                var alt = WindowContext.IsKeyDown(VirtualKey.Menu);
                if (alt)
                {
                    Document.Selection.GetText(TextGetOptions.NoHidden, out string hex);

                    if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int result))
                    {
                        Document.Selection.SetText(TextSetOptions.None, new string((char)result, 1));
                    }
                }
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        protected override void OnAccept()
        {
            _ = SendAsync();
        }

        private DateTime _lastKeystroke;

        private void OnTextChanged(object sender, RoutedEventArgs e)
        {
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
            if (SearchInlineBotResults(text, out string inlineQuery))
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
            var prev = ViewModel.Autocomplete;

            if (prev is AutocompleteCollection collection)
            {
                prev = collection.Source;
            }

            if (TryGetAutocomplete(text, query, prev, fromTextChanging, out var autocomplete, out bool recycle))
            {
                ClearInlineBotResults();
                SetAutocomplete(autocomplete, recycle);
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

                    if (SearchInlineBotResults(text, out query))
                    {
                        SetAutocomplete(null);
                        GetInlineBotResults(query);
                        return;
                    }
                }

                ClearInlineBotResults();
            }
        }

        private bool TryGetAutocomplete(string text, string query, IAutocompleteCollection prev, bool fromTextChanging, out IAutocompleteCollection autocomplete, out bool recycle)
        {
            autocomplete = null;
            recycle = false;

            if (Emoji.ContainsSingleEmoji(text) && ViewModel.ComposerHeader?.EditingMessage == null)
            {
                var chat = ViewModel.Chat;
                if (chat?.Permissions.CanSendOtherMessages == false)
                {
                    return false;
                }

                if (ViewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    if (supergroup.Status is ChatMemberStatusRestricted restricted && !restricted.Permissions.CanSendOtherMessages)
                    {
                        return false;
                    }
                }

                if (prev is SearchStickersCollection collection && !collection.IsCustomEmoji && prev.Query.Equals(text.Trim()))
                {
                    autocomplete = prev;
                    return true;
                }

                autocomplete = new SearchStickersCollection(ViewModel.ClientService, ViewModel.Settings, false, text.Trim(), ViewModel.Chat.Id);
                return true;
            }
            else if (AutocompleteEntityFinder.TrySearch(query, out AutocompleteEntity entity, out string result, out int index))
            {
                if (entity == AutocompleteEntity.Username)
                {
                    var chat = ViewModel.Chat;
                    if (chat == null)
                    {
                        return false;
                    }

                    var members = true;
                    if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret || chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                    {
                        members = false;
                    }

                    if (prev is UsernameCollection && prev.Query.Equals(result))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    autocomplete = new UsernameCollection(ViewModel.ClientService, ViewModel.Chat.Id, ViewModel.ThreadId, result, index == 0, members);
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
                    return true;
                }
                else if (entity == AutocompleteEntity.Sticker)
                {
                    if (prev is SearchStickersCollection collection && collection.IsCustomEmoji && prev.Query.Equals(text.Trim()))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    autocomplete = new SearchStickersCollection(ViewModel.ClientService, ViewModel.Settings, true, result, ViewModel.Chat?.Id ?? 0);
                    recycle = prev is SearchStickersCollection { IsCustomEmoji: true };
                    return true;
                }
                else if (entity == AutocompleteEntity.Emoji && fromTextChanging)
                {
                    if (prev is EmojiCollection && prev.Query.Equals(result))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    autocomplete = new EmojiCollection(ViewModel.ClientService, result, ViewModel.Chat.Id);
                    recycle = prev is EmojiCollection;
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
                    return true;
                }
            }

            autocomplete = null;
            return false;
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
            private readonly long _threadId;
            private readonly string _query;

            private readonly bool _bots;
            private readonly bool _members;

            private bool _hasMore = true;

            public UsernameCollection(IClientService clientService, long chatId, long threadId, string query, bool bots, bool members)
            {
                _clientService = clientService;
                _chatId = chatId;
                _threadId = threadId;
                _query = query;

                _bots = bots;
                _members = members;
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
                                if (user != null && user.HasActiveUsername(_query, out _))
                                {
                                    Add(user);
                                    count++;
                                }
                            }
                        }
                    }

                    if (_members)
                    {
                        var response = await _clientService.SendAsync(new SearchChatMembers(_chatId, _query, 20, new ChatMembersFilterMention(_threadId)));
                        if (response is ChatMembers members)
                        {
                            foreach (var member in members.Members)
                            {
                                if (_clientService.TryGetUser(member.MemberId, out Telegram.Td.Api.User user))
                                {
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
                _query = query;
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
                            var results = emojis.EmojiKeywordsValue
                                .DistinctBy(x => x.Emoji)
                                .Select(x => x.Emoji)
                                .OrderBy(x =>
                                {
                                    var index = SettingsService.Current.Emoji.RecentEmoji.IndexOf(x);
                                    if (index < 0)
                                    {
                                        return int.MaxValue;
                                    }

                                    return index;
                                });

                            _emoji = string.Join(" ", results);

                            foreach (var emoji in results)
                            {
                                Add(new EmojiData(emoji));
                                count++;
                            }
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

        public async Task SendAsync(bool disableNotification = false)
        {
            if (ViewModel.Type == DialogType.ScheduledMessages && ViewModel.ComposerHeader?.EditingMessage == null)
            {
                await ScheduleAsync(false);
                return;
            }

            var options = new MessageSendOptions(disableNotification, false, false, false, null, Effect?.Id ?? 0, 0, false);

            Sending?.Invoke(this, EventArgs.Empty);
            Effect = null;

            var text = GetFormattedText(true);
            await ViewModel.SendMessageAsync(text, options);
        }

        public async Task ScheduleAsync(bool whenOnline)
        {
            Sending?.Invoke(this, EventArgs.Empty);

            MessageSendOptions options;

            if (whenOnline)
            {
                options = new MessageSendOptions(false, false, false, false, new MessageSchedulingStateSendWhenOnline(), 0, 0, false);
            }
            else
            {
                options = await ViewModel.PickMessageSendOptionsAsync(true);
            }

            if (options != null)
            {
                var text = GetFormattedText(true);
                await ViewModel.SendMessageAsync(text, options);
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
                if (ViewModel?.Chat?.Type is not ChatTypePrivate)
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

        private bool SearchInlineBotResults(string text, out string searchText)
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
            else
            {
                ClearInlineBotResults();
            }

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

            return false;
        }

        public void UpdateItem(object oldItem, object newItem)
        {

        }
    }

    public partial class AutocompleteCollection : DiffObservableCollection<object>, ISupportIncrementalLoading, IAutocompleteCollection
    {
        private readonly DisposableMutex _mutex = new();
        private CancellationTokenSource _token;

        private IAutocompleteCollection _source;
        private ISupportIncrementalLoading _incrementalSource;

        private bool _initialized;

        public AutocompleteCollection(IAutocompleteCollection collection)
            : base(collection, new AutocompleteDiffHandler(), Constants.DiffOptions)
        {
            _source = collection;
            _incrementalSource = collection as ISupportIncrementalLoading;
        }

        public IAutocompleteCollection Source => _source;

        public async void Update(IAutocompleteCollection source)
        {
            _token?.Cancel();

            if (source is ISupportIncrementalLoading incremental && incremental.HasMoreItems)
            {
                var token = new CancellationTokenSource();

                _token = token;

                _source = source;
                _incrementalSource = incremental;

                if (_initialized)
                {
                    using (await _mutex.WaitAsync())
                    {
                        await incremental.LoadMoreItemsAsync(0);

                        // 100% redundant
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, source, DefaultDiffHandler, DefaultOptions));
                        ReplaceDiff(diff);
                        UpdateEmpty();

                        if (Count < 1 && incremental.HasMoreItems)
                        {
                            // This is 100% illegal and will cause a lot
                            // but really a lot of problems for sure.
                            Add(default);
                        }
                    }
                }
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async _ =>
            {
                using (await _mutex.WaitAsync())
                {
                    _initialized = true;
                    _token?.Cancel();

                    var token = _token = new CancellationTokenSource();
                    var result = await _incrementalSource?.LoadMoreItemsAsync(count);

                    // 100% redundant
                    if (token.IsCancellationRequested)
                    {
                        return result;
                    }

                    if (result.Count > 0)
                    {
                        var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, _source, DefaultDiffHandler, DefaultOptions));
                        ReplaceDiff(diff);
                        UpdateEmpty();
                    }

                    return result;
                }
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
