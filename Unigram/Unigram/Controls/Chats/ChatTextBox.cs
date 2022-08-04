using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Text.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls.Chats
{
    public class ChatTextBox : FormattedTextBox
    {
        private TextBlock InlinePlaceholderTextContentPresenter;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ChatTextBox()
        {
            DefaultStyleKey = typeof(ChatTextBox);

            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            //Clipboard.ContentChanged += Clipboard_ContentChanged;

            TextChanged += OnTextChanged;
        }

        protected override void OnApplyTemplate()
        {
            InlinePlaceholderTextContentPresenter = (TextBlock)GetTemplateChild("InlinePlaceholderTextContentPresenter");

            base.OnApplyTemplate();
        }

        public event EventHandler Sending;
        public event EventHandler<TappedRoutedEventArgs> Capture;

        protected override void OnTapped(TappedRoutedEventArgs e)
        {
            Capture?.Invoke(this, e);
            base.OnTapped(e);
        }

        protected override async void OnPaste(HandledEventArgs e)
        {
            try
            {
                // If the user tries to paste RTF content from any TOM control (Visual Studio, Word, Wordpad, browsers)
                // we have to handle the pasting operation manually to allow plaintext only.
                var package = Clipboard.GetContent();
                if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap) || package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                {
                    e.Handled = true;
                    await ViewModel.HandlePackageAsync(package);
                }
                else if (package.AvailableFormats.Contains(StandardDataFormats.Text) && package.AvailableFormats.Contains("application/x-tl-field-tags"))
                {
                    e.Handled = true;

                    // This is our field format
                    var text = await package.GetTextAsync();
                    var data = await package.GetDataAsync("application/x-tl-field-tags") as IRandomAccessStream;
                    var reader = new DataReader(data.GetInputStreamAt(0));
                    var length = await reader.LoadAsync((uint)data.Size);

                    var count = reader.ReadInt32();
                    var entities = new List<TextEntity>(count);

                    for (int i = 0; i < count; i++)
                    {
                        var entity = new TextEntity { Offset = reader.ReadInt32(), Length = reader.ReadInt32() };
                        var type = reader.ReadByte();

                        switch (type)
                        {
                            case 1:
                                entity.Type = new TextEntityTypeBold();
                                break;
                            case 2:
                                entity.Type = new TextEntityTypeItalic();
                                break;
                            case 3:
                                entity.Type = new TextEntityTypePreCode();
                                break;
                            case 4:
                                entity.Type = new TextEntityTypeTextUrl { Url = reader.ReadString(reader.ReadUInt32()) };
                                break;
                            case 5:
                                entity.Type = new TextEntityTypeMentionName { UserId = reader.ReadInt32() };
                                break;
                        }

                        entities.Add(entity);
                    }

                    InsertText(text, entities);
                }
                else if (package.AvailableFormats.Contains(StandardDataFormats.Text) /*&& package.Contains("Rich Text Format")*/)
                {
                    e.Handled = true;

                    var text = await package.GetTextAsync();
                    var start = Document.Selection.StartPosition;

                    Document.Selection.SetText(TextSetOptions.None, text);
                    Document.Selection.SetRange(start + text.Length, start + text.Length);
                }
            }
            catch { }
        }

        public ListView Messages { get; set; }
        public OrientableListView Autocomplete { get; set; }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space && Document.Selection.Length == 0)
            {
                var clone = Document.Selection.GetClone();
                var end = clone.EndOf(TextRangeUnit.CharacterFormat, true);

                if (clone.EndPosition > Document.Selection.EndPosition && IsEqual(clone.CharacterFormat, Document.Selection.CharacterFormat))
                {

                }
                else
                {
                    Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                }
            }
            else if (e.Key is VirtualKey.Up or VirtualKey.Down)
            {
                var alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                if (e.Key == VirtualKey.Up && !alt && !ctrl && !shift && IsEmpty && ViewModel.Autocomplete == null)
                {
                    ViewModel.MessageEditLast();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Up && ctrl)
                {
                    ViewModel.MessageReplyPrevious();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Down && ctrl)
                {
                    ViewModel.MessageReplyNext();
                    e.Handled = true;
                }
                else if (e.Key is VirtualKey.Up or VirtualKey.Down)
                {
                    if (Autocomplete != null && ViewModel.Autocomplete?.Orientation == Orientation.Vertical)
                    {
                        Autocomplete.SelectionMode = ListViewSelectionMode.Single;

                        var index = e.Key == VirtualKey.Up ? -1 : 1;
                        var next = Autocomplete.SelectedIndex + index;
                        if (next >= 0 && next < ViewModel.Autocomplete.Count)
                        {
                            Autocomplete.SelectedIndex = next;
                            Autocomplete.ScrollIntoView(Autocomplete.SelectedItem);
                        }

                        e.Handled = true;
                    }
                }
            }
            else if (e.Key is VirtualKey.Left or VirtualKey.Right)
            {
                if (Autocomplete != null && ViewModel.Autocomplete?.Orientation == Orientation.Horizontal)
                {
                    if (Autocomplete.SelectedIndex == 0 && e.Key == VirtualKey.Left)
                    {
                        Autocomplete.SelectedIndex = -1;
                        e.Handled = true;
                    }
                    else if (Autocomplete.SelectedIndex == Autocomplete.Items.Count - 1 && e.Key == VirtualKey.Right)
                    {
                        Autocomplete.SelectedIndex = 0;
                        e.Handled = true;
                    }
                    else
                    {
                        Autocomplete.SelectionMode = ListViewSelectionMode.Single;

                        var index = e.Key == VirtualKey.Left ? -1 : 1;
                        var next = Autocomplete.SelectedIndex + index;
                        if (next >= 0 && next < ViewModel.Autocomplete.Count)
                        {
                            Autocomplete.SelectedIndex = next;
                            Autocomplete.ScrollIntoView(Autocomplete.SelectedItem);

                            e.Handled = true;
                        }
                    }
                }
            }
            else if ((e.Key == VirtualKey.Tab || e.Key == VirtualKey.Enter) && Autocomplete != null && Autocomplete.Items.Count > 0 && ViewModel.Autocomplete != null
                && ((ViewModel.Autocomplete is EmojiCollection emojiCollection && Autocomplete.SelectedItem != null)
                || (ViewModel.Autocomplete is SearchStickersCollection && Autocomplete.SelectedItem != null) || ViewModel.Autocomplete is not SearchStickersCollection and not EmojiCollection))
            {
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
                if (shift)
                {
                    return;
                }

                var container = Autocomplete.ContainerFromIndex(Math.Max(0, Autocomplete.SelectedIndex)) as GridViewItem;
                if (container != null)
                {
                    var peer = new GridViewItemAutomationPeer(container);
                    var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    provider.Invoke();
                }

                Logs.Logger.Debug(Logs.LogTarget.Chat, "Tab pressed and handled");
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Tab)
            {
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                if (ctrl)
                {
                    return;
                }
            }
            else if (e.Key == VirtualKey.X && Math.Abs(Document.Selection.Length) == 4)
            {
                var alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
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
            if (string.Equals(inlineQuery, ViewModel.InlineBotResults?.Query, StringComparison.OrdinalIgnoreCase))
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
                if (ViewModel.Autocomplete is not AutocompleteList<UserCommand>)
                {
                    ClearInlineBotResults();
                    ViewModel.Autocomplete = GetCommands(string.Empty);
                }

                return;
            }

            Document.GetText(TextGetOptions.NoHidden, out string text);

            // This needs to run before text empty check as it cleans up
            // some stuff it inline bot isn't found
            if (SearchInlineBotResults(text, out string inlineQuery))
            {
                ViewModel.Autocomplete = null;

                GetInlineBotResults(inlineQuery);
                return;
            }

            if (string.IsNullOrEmpty(text) || Document.Selection.Length != 0)
            {
                ViewModel.Autocomplete = null;
                return;
            }

            var query = text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length));

            if (TryGetAutocomplete(text, query, ViewModel.Autocomplete, fromTextChanging, out var autocomplete))
            {
                ClearInlineBotResults();
                ViewModel.Autocomplete = autocomplete;
            }
            else
            {
                ViewModel.Autocomplete = null;

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
                        ViewModel.Autocomplete = null;

                        GetInlineBotResults(query);
                        return;
                    }
                }

                ClearInlineBotResults();
            }
        }

        private bool TryGetAutocomplete(string text, string query, IAutocompleteCollection prev, bool fromTextChanging, out IAutocompleteCollection autocomplete)
        {
            if (Emoji.ContainsSingleEmoji(text) && ViewModel.ComposerHeader?.EditingMessage == null)
            {
                var chat = ViewModel.Chat;
                if (chat?.Permissions.CanSendOtherMessages == false)
                {
                    autocomplete = null;
                    return false;
                }

                if (ViewModel.CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    if (supergroup.Status is ChatMemberStatusRestricted restricted && !restricted.Permissions.CanSendOtherMessages)
                    {
                        autocomplete = null;
                        return false;
                    }
                }

                if (prev is SearchStickersCollection collection && !collection.IsCustomEmoji && prev.Query.Equals(text.Trim()))
                {
                    autocomplete = prev;
                    return true;
                }

                autocomplete = new SearchStickersCollection(ViewModel.ProtoService, ViewModel.Settings, false, text.Trim());
                return true;
            }
            else if (AutocompleteEntityFinder.TrySearch(query, out AutocompleteEntity entity, out string result, out int index))
            {
                if (entity == AutocompleteEntity.Username)
                {
                    var chat = ViewModel.Chat;
                    if (chat == null)
                    {
                        autocomplete = null;
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

                    autocomplete = new UsernameCollection(ViewModel.ProtoService, ViewModel.Chat.Id, ViewModel.ThreadId, result, index == 0, members);
                    return true;
                }
                else if (entity == AutocompleteEntity.Hashtag)
                {
                    if (prev is SearchHashtagsCollection && prev.Query.Equals(result))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    autocomplete = new SearchHashtagsCollection(ViewModel.ProtoService, result);
                    return true;
                }
                else if (entity == AutocompleteEntity.Emoji && fromTextChanging)
                {
                    if (prev is EmojiCollection && prev.Query.Equals(result))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    autocomplete = new EmojiCollection(ViewModel.ProtoService, result, CoreTextServicesManager.GetForCurrentView().InputLanguage.LanguageTag);
                    return true;
                }
                else if (entity == AutocompleteEntity.Command && index == 0)
                {
                    if (prev is AutocompleteList<UserCommand> && prev.Query.Equals(result))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    autocomplete = GetCommands(result.ToLower());
                    return true;
                }
            }
            else if (SearchByCustomEmoji(query, out string customEmoji, out int _))
            {
                if (prev is SearchStickersCollection collection && collection.IsCustomEmoji && prev.Query.Equals(text.Trim()))
                {
                    autocomplete = prev;
                    return true;
                }

                autocomplete = new SearchStickersCollection(ViewModel.ProtoService, ViewModel.Settings, true, customEmoji);
                return true;
            }

            autocomplete = null;
            return false;
        }

        private AutocompleteList<UserCommand> GetCommands(string command)
        {
            var all = ViewModel.BotCommands;
            if (all != null)
            {
                var results = new AutocompleteList<UserCommand>(command, all.Where(x => x.Item.Command.ToLower().StartsWith(command, StringComparison.OrdinalIgnoreCase)));
                if (results.Count > 0)
                {
                    return results;
                }
            }

            return null;
        }

        public class UsernameCollection : MvxObservableCollection<Telegram.Td.Api.User>, IAutocompleteCollection, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly long _chatId;
            private readonly long _threadId;
            private readonly string _query;

            private readonly bool _bots;
            private readonly bool _members;

            private bool _hasMore = true;

            public UsernameCollection(IProtoService protoService, long chatId, long threadId, string query, bool bots, bool members)
            {
                _protoService = protoService;
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
                        var response = await _protoService.SendAsync(new GetTopChats(new TopChatCategoryInlineBots(), 10));
                        if (response is Telegram.Td.Api.Chats chats)
                        {
                            foreach (var id in chats.ChatIds)
                            {
                                var user = _protoService.GetUser(_protoService.GetChat(id));
                                if (user != null && user.Username.StartsWith(_query, StringComparison.OrdinalIgnoreCase))
                                {
                                    Add(user);
                                    count++;
                                }
                            }
                        }
                    }

                    if (_members)
                    {
                        var response = await _protoService.SendAsync(new SearchChatMembers(_chatId, _query, 20, new ChatMembersFilterMention(_threadId)));
                        if (response is ChatMembers members)
                        {
                            foreach (var member in members.Members)
                            {
                                if (_protoService.TryGetUser(member.MemberId, out Telegram.Td.Api.User user))
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
        }

        public class EmojiCollection : MvxObservableCollection<object>, IAutocompleteCollection, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly string _query;
            private readonly string _inputLanguage;

            private bool _hasMore = true;

            private string[] _emoji;
            private int _emojiIndex;

            public EmojiCollection(IProtoService protoService, string query, string inputLanguage)
            {
                _protoService = protoService;
                _query = query;
                _inputLanguage = inputLanguage;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    count = 0;

                    if (string.IsNullOrWhiteSpace(_query))
                    {
                        foreach (var emoji in SettingsService.Current.Emoji.GetRecentEmoji())
                        {
                            Add(emoji);
                            count++;
                        }
                    }
                    else
                    {
                        if (_emoji == null)
                        {
                            var response = await _protoService.SendAsync(new SearchEmojis(_query, false, new[] { _inputLanguage }));
                            if (response is Emojis emojis)
                            {
                                var results = emojis.EmojisValue.Reverse();
                                results = results.OrderBy(x =>
                                {
                                    var index = SettingsService.Current.Emoji.RecentEmoji.IndexOf(x);
                                    if (index < 0)
                                    {
                                        return int.MaxValue;
                                    }

                                    return index;
                                });

                                _emoji = results.ToArray();
                            }
                        }

                        if (_emojiIndex < _emoji.Length)
                        {
                            var response = await _protoService.SendAsync(new GetStickers(new StickerTypeCustomEmoji(), _emoji[_emojiIndex++], 1000));
                            if (response is Stickers stickers)
                            {
                                foreach (var sticker in stickers.StickersValue)
                                {
                                    Add(sticker);
                                    count++;
                                }
                            }
                        }
                        else
                        {
                            _emojiIndex++;

                            foreach (var emoji in _emoji)
                            {
                                Add(new EmojiData(emoji));
                                count++;
                            }
                        }
                    }

                    _hasMore = _emojiIndex <= _emoji.Length;
                    return new LoadMoreItemsResult { Count = count };
                });
            }

            public bool HasMoreItems => _hasMore;

            public string Query => _query;

            public Orientation Orientation => Orientation.Horizontal;
        }

        public class SearchHashtagsCollection : MvxObservableCollection<string>, IAutocompleteCollection, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly string _query;

            private bool _hasMore = true;

            public SearchHashtagsCollection(IProtoService protoService, string query)
            {
                _protoService = protoService;
                _query = query;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    count = 0;
                    _hasMore = false;

                    var response = await _protoService.SendAsync(new SearchHashtags(_query, 20));
                    if (response is Hashtags hashtags)
                    {
                        foreach (var value in hashtags.HashtagsValue)
                        {
                            Add("#" + value);
                            count++;
                        }
                    }

                    return new LoadMoreItemsResult { Count = count };
                });
            }

            public bool HasMoreItems => _hasMore;

            public string Query => _query;

            public Orientation Orientation => Orientation.Vertical;
        }

        public async Task SendAsync(bool disableNotification = false)
        {
            if (ViewModel.Type == DialogType.ScheduledMessages && ViewModel.ComposerHeader?.EditingMessage == null)
            {
                await ScheduleAsync();
                return;
            }

            Sending?.Invoke(this, EventArgs.Empty);

            var options = new MessageSendOptions(disableNotification, false, false, null);

            var text = GetFormattedText(true);
            await ViewModel.SendMessageAsync(text, options);
        }

        public async Task ScheduleAsync()
        {
            Sending?.Invoke(this, EventArgs.Empty);

            var options = await ViewModel.PickMessageSendOptionsAsync(true);
            if (options == null)
            {
                return;
            }

            var text = GetFormattedText(true);
            await ViewModel.SendMessageAsync(text, options);
        }

        protected override void OnGettingFormattedText()
        {
        }

        protected override void OnSettingText()
        {
            UpdateInlinePlaceholder(null, null);
        }

        private bool _isMenuExpanded;
        public bool? IsMenuExpanded
        {
            get => _isMenuExpanded;
            set
            {
                if (ViewModel?.Chat?.Type is not ChatTypePrivate)
                {
                    return;
                }

                _isMenuExpanded = value ?? false;
                OnSelectionChanged(this, false);
            }
        }

        #region Username

        public static bool SearchByCustomEmoji(string text, out string searchText, out int index)
        {
            index = -1;
            searchText = string.Empty;

            var shorter = text;
            if (shorter.Length > 11)
            {
                shorter = shorter.Substring(shorter.Length - 11);
            }

            var emoji = Emoji.EnumerateByComposedCharacterSequence(shorter);
            var last = emoji.LastOrDefault();

            if (last != null && Emoji.ContainsSingleEmoji(last))
            {
                searchText = last;
                index = text.Length - last.Length;
                return true;
            }

            return false;
        }


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

            if (ViewModel.CurrentInlineBot != null)
            {
                var username = ViewModel.CurrentInlineBot.Username;
                if (text != null && text.TrimStart().StartsWith("@" + username, StringComparison.OrdinalIgnoreCase))
                {
                    searchText = ReplaceFirst(text.TrimStart(), "@" + username, string.Empty);
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

        private void UpdateInlinePlaceholder(string username, string placeholder)
        {
            if (InlinePlaceholderTextContentPresenter != null)
            {
                InlinePlaceholderTextContentPresenter.Inlines.Clear();

                if (username != null && placeholder != null)
                {
                    InlinePlaceholderTextContentPresenter.Inlines.Add(new Run { Text = "@" + username + " ", Foreground = null });
                    InlinePlaceholderTextContentPresenter.Inlines.Add(new Run { Text = placeholder });
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

    public interface IAutocompleteCollection : ICollection
    {
        public string Query { get; }

        public Orientation Orientation { get; }
    }

    public class AutocompleteList<T> : List<T>, IAutocompleteCollection
    {
        public string Query { get; }

        public Orientation Orientation { get; set; } = Orientation.Vertical;

        public AutocompleteList(string query, IEnumerable<T> collection)
            : base(collection)
        {
            Query = query;
        }
    }
}
