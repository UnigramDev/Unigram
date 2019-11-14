using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Unigram.Native;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Automation;
using Unigram.Entities;
using Telegram.Td.Api;
using Unigram.Services;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Collections;
using Template10.Common;
using System.Windows.Input;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Controls.Primitives;
using Unigram.Controls.Views;
using Unigram.Converters;
using Windows.Graphics.Imaging;
using System.Collections;
using System.Globalization;

namespace Unigram.Controls.Chats
{
    public class ChatTextBox : FormattedTextBox
    {
        private ContentControl InlinePlaceholderTextContentPresenter;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ChatTextBox()
        {
            DefaultStyleKey = typeof(ChatTextBox);

            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            ClipboardCopyFormat = RichEditClipboardFormat.PlainText;

            Paste += OnPaste;
            //Clipboard.ContentChanged += Clipboard_ContentChanged;

            SelectionChanged += OnSelectionChanged;
            TextChanged += OnTextChanged;
        }

        protected override void OnApplyTemplate()
        {
            InlinePlaceholderTextContentPresenter = (ContentControl)GetTemplateChild("InlinePlaceholderTextContentPresenter");

            base.OnApplyTemplate();
        }

        public void InsertText(string text, bool allowPreceding = true, bool allowTrailing = true)
        {
            var start = Document.Selection.StartPosition;
            var end = Document.Selection.EndPosition;

            var preceding = start > 0 && !char.IsWhiteSpace(Document.GetRange(start - 1, start).Character);
            var trailing = !char.IsWhiteSpace(Document.GetRange(end, end + 1).Character) || Document.GetRange(end, end + 1).Character == '\r';

            var block = string.Format("{0}{1}{2}",
                preceding && allowPreceding ? " " : "",
                text,
                trailing && allowTrailing ? " " : "");

            Document.Selection.SetText(TextSetOptions.None, block);
            Document.Selection.StartPosition = Document.Selection.EndPosition;
        }

        public event EventHandler<TappedRoutedEventArgs> Capture;

        protected override void OnTapped(TappedRoutedEventArgs e)
        {
            Capture?.Invoke(this, e);
            base.OnTapped(e);
        }

        protected override void OnPaste()
        {
            OnPaste(null, null);
        }

        private async void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            // If the user tries to paste RTF content from any TOM control (Visual Studio, Word, Wordpad, browsers)
            // we have to handle the pasting operation manually to allow plaintext only.
            var package = Clipboard.GetContent();
            if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
            {
                if (e != null)
                {
                    e.Handled = true;
                }

                var bitmap = await package.GetBitmapAsync();
                var media = new ObservableCollection<StorageMedia>();

                var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.png", DateTime.Now);
                var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                using (var stream = await bitmap.OpenReadAsync())
                {
                    var result = await ImageHelper.TranscodeAsync(stream, cache, BitmapEncoder.PngEncoderId);
                    var photo = await StoragePhoto.CreateAsync(result, true);
                    if (photo == null)
                    {
                        return;
                    }

                    media.Add(photo);
                }

                if (package.AvailableFormats.Contains(StandardDataFormats.Text))
                {
                    media[0].Caption = new FormattedText(await package.GetTextAsync(), new TextEntity[0]);
                }

                ViewModel.SendMediaExecute(media, media[0]);
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.WebLink))
            {

            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
            {
                if (e != null)
                {
                    e.Handled = true;
                }

                var items = await package.GetStorageItemsAsync();
                var media = new ObservableCollection<StorageMedia>();
                var files = new List<StorageFile>(items.Count);

                foreach (var file in items.OfType<StorageFile>())
                {
                    if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
                    {
                        var photo = await StoragePhoto.CreateAsync(file, true);
                        if (photo != null)
                        {
                            media.Add(photo);
                        }
                    }
                    else if (file.ContentType == "video/mp4")
                    {
                        var video = await StorageVideo.CreateAsync(file, true);
                        if (video != null)
                        {
                            media.Add(video);
                        }
                    }

                    files.Add(file);
                }

                // Send compressed __only__ if user is dropping photos and videos only
                if (media.Count > 0 && media.Count == files.Count)
                {
                    ViewModel.SendMediaExecute(media, media[0]);
                }
                else if (files.Count > 0)
                {
                    ViewModel.SendFileExecute(files);
                }
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.Text) && package.AvailableFormats.Contains("application/x-tl-field-tags"))
            {
                if (e != null)
                {
                    e.Handled = true;
                }

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
            else if (package.AvailableFormats.Contains(StandardDataFormats.Text) && package.AvailableFormats.Contains("application/x-td-field-tags"))
            {
                // This is Telegram Desktop mentions format
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.Text) /*&& package.Contains("Rich Text Format")*/)
            {
                if (e != null)
                {
                    e.Handled = true;
                }

                var text = await package.GetTextAsync();
                var start = Document.Selection.StartPosition;

                var result = Emoticon.Pattern.Replace(text, (match) =>
                {
                    var emoticon = match.Groups[1].Value;
                    var emoji = Emoticon.Replace(emoticon);
                    if (match.Value.StartsWith(" "))
                    {
                        emoji = $" {emoji}";
                    }

                    return emoji;
                });

                Document.Selection.SetText(TextSetOptions.None, result);
                Document.Selection.SetRange(start + result.Length, start + result.Length);
            }
        }

        public ListView Messages { get; set; }
        public OrientableListView Autocomplete { get; set; }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                if (Document.Selection.Length > 0)
                {
                    return;
                }

                FormatText();

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
            else if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down)
            {
                var alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                if (e.Key == VirtualKey.Up && !alt && !ctrl && !shift && IsEmpty)
                {
                    ViewModel.MessageEditLastCommand.Execute();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Up && ctrl)
                {
                    ViewModel.MessageReplyPreviousCommand.Execute();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Down && ctrl)
                {
                    ViewModel.MessageReplyNextCommand.Execute();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down)
                {
                    if (Autocomplete != null && ViewModel.Autocomplete != null)
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
            else if ((e.Key == VirtualKey.Tab || e.Key == VirtualKey.Enter) && Autocomplete != null && Autocomplete.Items.Count > 0 && ViewModel.Autocomplete != null && !(ViewModel.Autocomplete is SearchStickersCollection))
            {
                var container = Autocomplete.ContainerFromIndex(Math.Max(0, Autocomplete.SelectedIndex)) as GridViewItem;
                if (container != null)
                {
                    var peer = new GridViewItemAutomationPeer(container);
                    var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    provider.Invoke();
                }

                Logs.Logger.Debug(Logs.Target.Chat, "Tab pressed and handled");
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
            else if (e.Key == VirtualKey.Enter)
            {
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

                var send = false;

                if (ViewModel.Settings.IsSendByEnterEnabled)
                {
                    send = !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                }
                else
                {
                    send = ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                }

                AcceptsReturn = !send;
                e.Handled = send;

                if (send)
                {
                    _ = SendAsync();
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

        private void OnTextChanged(object sender, RoutedEventArgs e)
        {
            //AcceptsReturn = false;
            UpdateText();

            if (IsEmpty == false)
            {
                ViewModel.ChatActionManager.SetTyping(new ChatActionTyping());
            }
        }

        private async void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            Document.GetText(TextGetOptions.NoHidden, out string text);

            // This needs to run before text empty check as it cleans up
            // some stuff it inline bot isn't found
            if (SearchInlineBotResults(text, out string inlineQuery))
            {
                ViewModel.Autocomplete = null;

                ViewModel.GetInlineBotResults(inlineQuery);
                return;
            }

            if (string.IsNullOrEmpty(text) || Document.Selection.Length != 0)
            {
                ViewModel.Autocomplete = null;
                return;
            }

            var query = text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length));

            if (TryGetAutocomplete(text, query, out var autocomplete))
            {
                ViewModel.CurrentInlineBot = null;
                ViewModel.InlineBotResults = null;
                InlinePlaceholderText = string.Empty;

                ViewModel.Autocomplete = autocomplete;
            }
            else
            {
                ViewModel.Autocomplete = null;

                if (SearchByInlineBot(query, out string username, out int index) && await ViewModel.ResolveInlineBotAsync(username))
                {
                    if (SearchInlineBotResults(text, out query))
                    {
                        ViewModel.Autocomplete = null;

                        ViewModel.GetInlineBotResults(query);
                        return;
                    }
                }

                ViewModel.CurrentInlineBot = null;
                ViewModel.InlineBotResults = null;
                InlinePlaceholderText = string.Empty;
            }
        }

        private bool TryGetAutocomplete(string text, string query, out IAutocompleteCollection autocomplete)
        {
            if (Emoji.ContainsSingleEmoji(text) && ViewModel.ComposerHeader?.EditingMessage == null)
            {
                autocomplete = new SearchStickersCollection(ViewModel.ProtoService, ViewModel.Settings, text.Trim());
                return true;
            }
            else if (SearchByUsername(query, out string username, out int index))
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

                autocomplete = new UsernameCollection(ViewModel.ProtoService, ViewModel.Chat.Id, username, index == 0, members);
                return true;
            }
            else if (SearchByHashtag(query, out string hashtag, out int index2))
            {
                autocomplete = new SearchHashtagsCollection(ViewModel.ProtoService, hashtag);
                return true;
            }
            else if (SearchByEmoji(query, out string replacement) && replacement.Length > 0)
            {
                autocomplete = new EmojiCollection(ViewModel.ProtoService, replacement);
                return true;
            }
            else if (text.Length > 0 && text[0] == '/' && SearchByCommand(text, out string command))
            {
                autocomplete = GetCommands(command.ToLower());
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
                var results = new AutocompleteList<UserCommand>(all.Where(x => x.Item.Command.ToLower().StartsWith(command, StringComparison.OrdinalIgnoreCase)));
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
            private readonly string _query;

            private readonly bool _bots;
            private readonly bool _members;

            private bool _hasMore = true;

            public UsernameCollection(IProtoService protoService, long chatId, string query, bool bots, bool members)
            {
                _protoService = protoService;
                _chatId = chatId;
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
                        var response = await _protoService.SendAsync(new SearchChatMembers(_chatId, _query, 20, null));
                        if (response is ChatMembers members)
                        {
                            foreach (var member in members.Members)
                            {
                                var user = _protoService.GetUser(member.UserId);
                                if (user != null)
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

            public Orientation Orientation => Orientation.Vertical;
        }

        public class EmojiCollection : MvxObservableCollection<EmojiData>, IAutocompleteCollection, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly string _query;

            private bool _hasMore = true;

            public EmojiCollection(IProtoService protoService, string query)
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

                    var response = await _protoService.SendAsync(new SearchEmojis(_query, false));
                    if (response is Emojis emojis)
                    {
                        foreach (var emoji in emojis.EmojisValue)
                        {
                            Add(new EmojiData(emoji));
                            count++;
                        }
                    }

                    return new LoadMoreItemsResult { Count = count };
                });
            }

            public bool HasMoreItems => _hasMore;

            public Orientation Orientation => Orientation.Horizontal;
        }

        public class EmojiGroupCollection : MvxObservableCollection<List<EmojiGroup>>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly string _query;
            private readonly EmojiSkinTone _skin;

            private bool _hasMore = true;

            public EmojiGroupCollection(IProtoService protoService, string query, EmojiSkinTone skin)
            {
                _protoService = protoService;
                _query = query;
                _skin = skin;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    _hasMore = false;

                    Add(await Emoji.SearchAsync(_protoService, _query, _skin));

                    return new LoadMoreItemsResult { Count = 1 };
                });
            }

            public bool HasMoreItems => _hasMore;
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

            public Orientation Orientation => Orientation.Vertical;
        }

        private void UpdateText()
        {
            Document.GetText(TextGetOptions.NoHidden, out string text);
            Text = text;
        }

        private void FormatText()
        {
            if (!ViewModel.Settings.IsReplaceEmojiEnabled)
            {
                return;
            }

            Document.GetText(TextGetOptions.NoHidden, out string text);

            var caretPosition = Document.Selection.StartPosition;
            var result = Emoticon.Pattern.Matches(text);

            Document.BatchDisplayUpdates();

            foreach (Match match in result)
            {
                var emoticon = match.Groups[1].Value;
                var emoji = Emoticon.Replace(emoticon);
                if (match.Index + match.Length < caretPosition)
                {
                    caretPosition += emoji.Length - emoticon.Length;
                }
                if (match.Value.StartsWith(" "))
                {
                    emoji = $" {emoji}";
                }

                Document.GetRange(match.Index, match.Index + match.Length).SetText(TextSetOptions.None, emoji);
            }

            Document.ApplyDisplayUpdates();
            Document.Selection.SetRange(caretPosition, caretPosition);
        }

        public async Task SendAsync(bool disableNotification = false)
        {
            await ViewModel.SendMessageAsync(GetFormattedText(true), disableNotification);
        }

        protected override void OnGettingFormattedText()
        {
            FormatText();
        }

        public override bool IsEmpty
        {
            get
            {
                var isEmpty = string.IsNullOrWhiteSpace(Text);
                if (isEmpty)
                {
                    Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                }

                return isEmpty;
            }
        }

        #region Username

        public static bool SearchByUsername(string text, out string searchText, out int index)
        {
            index = -1;
            searchText = string.Empty;

            var found = true;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (text[i] == '@')
                {
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r' || text[i - 1] == '\v')
                    {
                        index = i;
                        break;
                    }

                    found = false;
                    break;
                }
                else
                {
                    if (!MessageHelper.IsValidUsernameSymbol(text[i]))
                    {
                        found = false;
                        break;
                    }

                    i--;
                }
            }

            if (found)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(index).TrimStart('@');
            }

            return found;
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

        public static bool SearchByCommand(string text, out string searchText)
        {
            searchText = string.Empty;

            var c = '/';
            var flag = true;
            var index = -1;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (text[i] == c)
                {
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r' || text[i - 1] == '\v')
                    {
                        index = i;
                        break;
                    }
                    flag = false;
                    break;
                }
                else
                {
                    if (!MessageHelper.IsValidCommandSymbol(text[i]))
                    {
                        flag = false;
                        break;
                    }
                    i--;
                }
            }
            if (flag)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(index).TrimStart(c);
            }

            return flag;
        }

        public static bool SearchByEmoji(string text, out string searchText)
        {
            searchText = string.Empty;

            var c = ':';
            var flag = true;
            var index = -1;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (text[i] == c)
                {
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r' || text[i - 1] == '\v')
                    {
                        index = i;
                        break;
                    }
                    flag = false;
                    break;
                }
                else
                {
                    if (!MessageHelper.IsValidCommandSymbol(text[i]))
                    {
                        flag = false;
                        break;
                    }
                    i--;
                }
            }
            if (flag)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(index).TrimStart(c);
            }

            if (searchText.Length == 1 && searchText == searchText.ToUpper())
            {
                searchText = string.Empty;
                flag = false;
            }

            return flag;
        }

        public static bool SearchByHashtag(string text, out string searchText, out int index)
        {
            index = -1;
            searchText = string.Empty;

            var found = true;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (text[i] == '#')
                {
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r' || text[i - 1] == '\v')
                    {
                        index = i;
                        break;
                    }

                    found = false;
                    break;
                }
                else
                {
                    if (!MessageHelper.IsValidUsernameSymbol(text[i]))
                    {
                        found = false;
                        break;
                    }

                    i--;
                }
            }

            if (found)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(index).TrimStart('#');
            }

            return found;
        }

        #endregion

        #region Inline bots

        private bool SearchInlineBotResults(string text, out string searchText)
        {
            var flag = false;
            searchText = string.Empty;

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
                            ViewModel.CurrentInlineBot = null;
                            ViewModel.InlineBotResults = null;
                            InlinePlaceholderText = string.Empty;
                        }
                        else
                        {
                            var user = ViewModel.CurrentInlineBot;
                            if (user != null && user.Type is UserTypeBot bot)
                            {
                                InlinePlaceholderText = bot.InlineQueryPlaceholder;
                            }
                        }
                    }
                    else if (string.IsNullOrEmpty(searchText))
                    {
                        var user = ViewModel.CurrentInlineBot;
                        if (user != null && user.Type is UserTypeBot bot)
                        {
                            InlinePlaceholderText = bot.InlineQueryPlaceholder;
                        }
                    }
                    else
                    {
                        InlinePlaceholderText = string.Empty;
                    }
                }
                else
                {
                    ViewModel.CurrentInlineBot = null;
                    ViewModel.InlineBotResults = null;
                    InlinePlaceholderText = string.Empty;
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

        private void UpdateInlinePlaceholder()
        {
            if (InlinePlaceholderTextContentPresenter != null)
            {
                var placeholder = Text;
                if (placeholder == null)
                {
                    return;
                }

                var range = Document.GetRange(Text.Length, Text.Length);
                range.GetRect(PointOptions.ClientCoordinates, out Rect rect, out int hit);

                var translateTransform = new TranslateTransform();
                translateTransform.X = rect.X;
                InlinePlaceholderTextContentPresenter.RenderTransform = translateTransform;
            }
        }

        #endregion

        public string Text { get; private set; }

        #region InlinePlaceholderText

        public string InlinePlaceholderText
        {
            get { return (string)GetValue(InlinePlaceholderTextProperty); }
            set { SetValue(InlinePlaceholderTextProperty, value); }
        }

        public static readonly DependencyProperty InlinePlaceholderTextProperty =
            DependencyProperty.Register("InlinePlaceholderText", typeof(string), typeof(ChatTextBox), new PropertyMetadata(null, OnInlinePlaceholderTextChanged));

        private static void OnInlinePlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatTextBox)d).UpdateInlinePlaceholder();
        }

        #endregion

        #region Reply

        public object Reply
        {
            get { return (object)GetValue(ReplyProperty); }
            set { SetValue(ReplyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Reply.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ReplyProperty =
            DependencyProperty.Register("Reply", typeof(object), typeof(ChatTextBox), new PropertyMetadata(null, OnReplyChanged));

        private static void OnReplyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatTextBox)d).OnReplyChanged((object)e.NewValue, (object)e.OldValue);
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
        public Orientation Orientation { get; }
    }

    public class AutocompleteList<T> : List<T>, IAutocompleteCollection
    {
        public Orientation Orientation { get; set; } = Orientation.Vertical;

        public AutocompleteList(IEnumerable<T> collection)
            : base(collection)
        {

        }
    }
}
