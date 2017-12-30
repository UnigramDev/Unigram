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
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Views;
using Unigram.Core.Models;
using Unigram.Core.Rtf;
using Unigram.Core.Rtf.Write;
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
using Unigram.Core;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Telegram.Api.TL.Channels;
using Unigram.Native;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Automation;
using Unigram.Models;

namespace Unigram.Controls
{
    public class BubbleTextBox : RichEditBox
    {
        private ContentControl InlinePlaceholderTextContentPresenter;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private MenuFlyout _flyout;
        private MenuFlyoutPresenter _presenter;

        private readonly IDisposable _textChangedSubscription;

        // True when the RichEdithBox MIGHT contains formatting (bold, italic, hyperlinks) 
        private bool _isDirty;

        public BubbleTextBox()
        {
            DefaultStyleKey = typeof(BubbleTextBox);

            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            ClipboardCopyFormat = RichEditClipboardFormat.PlainText;

            //_flyout = new MenuFlyout();
            //_flyout.Items.Add(new MenuFlyoutItem { Text = "Bold" });
            //_flyout.Items.Add(new MenuFlyoutItem { Text = "Italic" });
            //_flyout.AllowFocusOnInteraction = false;
            //_flyout.AllowFocusWhenDisabled = false;

            //((MenuFlyoutItem)_flyout.Items[0]).Click += Bold_Click;
            //((MenuFlyoutItem)_flyout.Items[1]).Click += Italic_Click;
            //((MenuFlyoutItem)_flyout.Items[1]).Loaded += Italic_Loaded;

            //#if DEBUG
            //            // To test hyperlinks (Used for mention name => to tag people that has no username)
            //            _flyout.Items.Add(new MenuFlyoutItem { Text = "Hyperlink" });
            //            ((MenuFlyoutItem)_flyout.Items[2]).Click += Hyperlink_Click;
            //#endif

            Paste += OnPaste;
            //Clipboard.ContentChanged += Clipboard_ContentChanged;

            SelectionChanged += OnSelectionChanged;
            TextChanged += OnTextChanged;

            var textChangedEvents = Observable.FromEventPattern<RoutedEventHandler, RoutedEventArgs>(
                keh => { TextChanged += keh; },
                keh => { TextChanged -= keh; });

            _textChangedSubscription = textChangedEvents
                .Throttle(TimeSpan.FromMilliseconds(200))
                .Subscribe(e => this.BeginOnUIThread(() => UpdateInlineBot(true)));

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            App.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            App.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        protected override void OnApplyTemplate()
        {
            InlinePlaceholderTextContentPresenter = (ContentControl)GetTemplateChild("InlinePlaceholderTextContentPresenter");

            base.OnApplyTemplate();
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            Document.Selection.CharacterFormat.Bold = FormatEffect.Toggle;
            Document.Selection.CharacterFormat.Italic = FormatEffect.Off;
            Document.Selection.CharacterFormat.ForegroundColor = ((SolidColorBrush)Foreground).Color;

            if (string.IsNullOrEmpty(Document.Selection.Link) == false)
            {
                Document.Selection.Link = string.Empty;
                Document.Selection.CharacterFormat.Underline = UnderlineType.None;
            }

            UpdateIsDirty(Document.Selection);
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            Document.Selection.CharacterFormat.Bold = FormatEffect.Off;
            Document.Selection.CharacterFormat.Italic = FormatEffect.Toggle;
            Document.Selection.CharacterFormat.ForegroundColor = ((SolidColorBrush)Foreground).Color;

            if (string.IsNullOrEmpty(Document.Selection.Link) == false)
            {
                Document.Selection.Link = string.Empty;
                Document.Selection.CharacterFormat.Underline = UnderlineType.None;
            }

            UpdateIsDirty(Document.Selection);
        }

#if DEBUG
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Document.Selection.CharacterFormat.Bold = FormatEffect.Off;
            Document.Selection.CharacterFormat.Italic = FormatEffect.Off;
            Document.Selection.Link = $"\"{SettingsHelper.UserId}\"";
            Document.Selection.CharacterFormat.Underline = UnderlineType.Dash;
            Document.Selection.CharacterFormat.ForegroundColor = ((SolidColorBrush)Foreground).Color;

            UpdateIsDirty(Document.Selection);
        }
#endif

        private void UpdateIsDirty(ITextRange range)
        {
            var link = string.IsNullOrEmpty(range.Link) == false;
            var bold = range.CharacterFormat.Bold == FormatEffect.On;
            var italic = range.CharacterFormat.Italic == FormatEffect.On;

            _isDirty |= link || bold || italic;
        }

        private void Italic_Loaded(object sender, RoutedEventArgs e)
        {
            _presenter = (MenuFlyoutPresenter)_flyout.Items[1].Ancestors<MenuFlyoutPresenter>().FirstOrDefault();
            OnSelectionChanged();
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

        private async void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            // If the user tries to paste RTF content from any TOM control (Visual Studio, Word, Wordpad, browsers)
            // we have to handle the pasting operation manually to allow plaintext only.
            var package = Clipboard.GetContent();
            if (package.Contains(StandardDataFormats.Text) && package.Contains("Rich Text Format"))
            {
                e.Handled = true;

                var formats = package.AvailableFormats.ToList();
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
            else if (package.Contains(StandardDataFormats.Bitmap))
            {
                e.Handled = true;

                var bitmap = await package.GetBitmapAsync();
                var media = new ObservableCollection<StorageMedia>();
                var cache = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\paste.jpg", CreationCollisionOption.ReplaceExisting);

                using (var stream = await bitmap.OpenReadAsync())
                using (var reader = new DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    var buffer = new byte[(int)stream.Size];
                    reader.ReadBytes(buffer);
                    await FileIO.WriteBytesAsync(cache, buffer);

                    var photo = await StoragePhoto.CreateAsync(cache, true) as StorageMedia;
                    if (photo == null)
                    {
                        photo = await StorageVideo.CreateAsync(cache, true);
                    }

                    if (photo == null)
                    {
                        return;
                    }

                    media.Add(photo);
                }

                if (package.Contains(StandardDataFormats.Text))
                {
                    media[0].Caption = await package.GetTextAsync();
                }

                ViewModel.SendMediaExecute(media, media[0]);
            }
            else if (package.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = true;

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
                        media.Add(await StoragePhoto.CreateAsync(file, true));
                    }
                    else if (file.ContentType == "video/mp4")
                    {
                        media.Add(await StorageVideo.CreateAsync(file, true));
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
            else if (package.Contains(StandardDataFormats.Text) && package.Contains("application/x-tl-field-tags"))
            {
                // This is our field format
            }
            else if (package.Contains(StandardDataFormats.Text) && package.Contains("application/x-td-field-tags"))
            {
                // This is Telegram Desktop mentions format
            }
        }

        private void Clipboard_ContentChanged(object sender, object e)
        {
            if (FocusState != FocusState.Unfocused)
            {
                bool isDirty = _isDirty;

                if (isDirty)
                {
                    Document.GetText(TextGetOptions.FormatRtf, out string text);
                    Document.GetText(TextGetOptions.NoHidden, out string planText);

                    var parser = new RtfToTLParser();
                    var reader = new RtfReader(parser);
                    reader.LoadRtfText(text);
                    reader.Parse();

                    MessageHelper.CopyToClipboard(planText, parser.Entities);
                }

                Clipboard.ContentChanged -= Clipboard_ContentChanged;
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            //if (Document.Selection.Length != 0)
            //{
            //    Document.Selection.GetRect(PointOptions.ClientCoordinates, out Rect rect, out int hit);
            //    _flyout.ShowAt(this, new Point(rect.X + 12, rect.Y - _presenter?.ActualHeight ?? 0));
            //}
            //else
            //{
            //    _flyout.Hide();
            //}
        }

        //protected override async void OnKeyDown(KeyRoutedEventArgs e)
        //{
        //    if (e.Key == VirtualKey.Enter)
        //    {
        //        // Check if CTRL or Shift is also pressed in addition to Enter key.
        //        var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
        //        var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

        //        // If there is text and CTRL/Shift is not pressed, send message. Else allow new row.
        //        if (!ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down) && !IsEmpty)
        //        {
        //            e.Handled = true;
        //            await SendAsync();
        //        }
        //    }

        //    base.OnKeyDown(e);
        //}

        private async void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if ((args.VirtualKey == VirtualKey.Enter || args.VirtualKey == VirtualKey.Tab) && args.EventType == CoreAcceleratorKeyEventType.KeyDown && FocusState != FocusState.Unfocused)
            {
                // Check if CTRL or Shift is also pressed in addition to Enter key.
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);
                var key = Window.Current.CoreWindow.GetKeyState(VirtualKey.Enter);

                if (Autocomplete != null && ViewModel.Autocomplete != null)
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send || args.VirtualKey == VirtualKey.Tab)
                    {
                        AcceptsReturn = false;
                        var container = Autocomplete.ContainerFromIndex(Math.Max(0, Autocomplete.SelectedIndex)) as ListViewItem;
                        if (container != null)
                        {
                            var peer = new ListViewItemAutomationPeer(container);
                            var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                            provider.Invoke();
                        }
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }

                    return;
                }

                // If there is text and CTRL/Shift is not pressed, send message. Else allow new row.
                if (ApplicationSettings.Current.IsSendByEnterEnabled)
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send)
                    {
                        await SendAsync();
                        AcceptsReturn = false;
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }
                }
                else
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send)
                    {
                        await SendAsync();
                        AcceptsReturn = false;
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }
                }
            }
        }

        public ListView Messages { get; set; }
        public ListView Autocomplete { get; set; }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                FormatText();

                Document.GetText(TextGetOptions.NoHidden, out string text);

                if (MessageHelper.IsValidUsername(text))
                {
                    ViewModel.ResolveInlineBot(text);
                }
            }
            else if ((e.Key == VirtualKey.Up || e.Key == VirtualKey.Down || e.Key == VirtualKey.PageUp || e.Key == VirtualKey.PageDown || e.Key == VirtualKey.Tab))
            {
                var alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                if (e.Key == VirtualKey.Up && !alt && !ctrl && !shift && IsEmpty)
                {
                    ViewModel.MessageEditLastCommand.Execute();
                    e.Handled = true;
                }
                else if ((e.Key == VirtualKey.Up && alt) || (e.Key == VirtualKey.PageUp && ctrl) || (e.Key == VirtualKey.Tab && ctrl && shift))
                {
                    ViewModel.Aggregator.Publish("move_up");
                    e.Handled = true;
                }
                else if ((e.Key == VirtualKey.Down && alt) || (e.Key == VirtualKey.PageDown && ctrl) || (e.Key == VirtualKey.Tab && ctrl && !shift))
                {
                    ViewModel.Aggregator.Publish("move_down");
                    e.Handled = true;
                }
                else if ((e.Key == VirtualKey.PageUp || e.Key == VirtualKey.Up) && Document.Selection.StartPosition == 0 && ViewModel.Autocomplete == null)
                {
                    var peer = new ListViewAutomationPeer(Messages);
                    var provider = peer.GetPattern(PatternInterface.Scroll) as IScrollProvider;
                    if (provider.VerticallyScrollable)
                    {
                        provider.Scroll(ScrollAmount.NoAmount, e.Key == VirtualKey.Up ? ScrollAmount.SmallDecrement : ScrollAmount.LargeDecrement);

                        e.Handled = true;
                    }
                }
                else if ((e.Key == VirtualKey.PageDown || e.Key == VirtualKey.Down) && Document.Selection.StartPosition == Text.TrimEnd('\r', '\v').Length && ViewModel.Autocomplete == null)
                {
                    var peer = new ListViewAutomationPeer(Messages);
                    var provider = peer.GetPattern(PatternInterface.Scroll) as IScrollProvider;
                    if (provider.VerticallyScrollable)
                    {
                        provider.Scroll(ScrollAmount.NoAmount, e.Key == VirtualKey.Down ? ScrollAmount.SmallIncrement : ScrollAmount.LargeIncrement);

                        e.Handled = true;
                    }
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
                else if (e.Key == VirtualKey.Tab && Autocomplete != null && ViewModel.Autocomplete != null)
                {
                    e.Handled = true;
                }
            }
            else if (e.Key == VirtualKey.Escape && ViewModel.Reply is TLMessagesContainter container && container.EditMessage != null)
            {
                ViewModel.ClearReplyCommand.Execute();
                e.Handled = true;
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        private void OnTextChanged(object sender, RoutedEventArgs e)
        {
            AcceptsReturn = false;
            UpdateText();
            UpdateInlineBot(false);

            //string result;
            //if (SearchByStickers(this.Text, out result))
            //{
            //    this.GetStickerHints(result);
            //}
            //else
            //{
            //    this.ClearStickerHints();
            //}

            //if (SearchInlineBotResults(this.Text, out result))
            //{
            //    this.GetInlineBotResults(result);
            //}
            //else
            //{
            //    this.ClearInlineBotResults();
            //}

            //if (SearchByUsernames(this.Text, out result))
            //{
            //    this.GetUsernameHints(result);
            //}
            //else
            //{
            //    this.ClearUsernameHints();
            //}

            //if (SearchByCommands(this.Text, out result))
            //{
            //    this.GetCommandHints(result);
            //}
            //else
            //{
            //    this.ClearCommandHints();
            //}

            if (IsEmpty == false)
            {
                ViewModel.OutputTypingManager.SetTyping(new TLSendMessageTypingAction());
            }
        }

        private void UpdateInlineBot(bool fast)
        {
            //var text = Text.Substring(0, Math.Max(Document.Selection.StartPosition, Document.Selection.EndPosition));
            var text = Text;
            var query = string.Empty;
            var inline = SearchInlineBotResults(text, out query);
            if (inline && fast)
            {
                ViewModel.GetInlineBotResults(query);
            }
            else if (!inline)
            {
                ViewModel.CurrentInlineBot = null;
                ViewModel.InlineBotResults = null;
                InlinePlaceholderText = string.Empty;

                if (fast)
                {
                    if (text.Trim().Length <= 14 && !string.IsNullOrWhiteSpace(text) && ViewModel.EditedMessage == null)
                    {
                        ViewModel.StickerPack = ViewModel.Stickers.StickersService.LoadStickersForEmoji(text.Trim());
                    }
                    else
                    {
                        ViewModel.StickerPack = null;
                    }
                }
                else
                {
                    ViewModel.StickerPack = null;

                    if (SearchByUsername(text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length)), out string username))
                    {
                        ViewModel.Autocomplete = GetUsernames(username.ToLower(), text.StartsWith('@' + username));
                    }
                    else if (SearchByEmoji(text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length)), out string replacement) && replacement.Length > 0)
                    {
                        ViewModel.Autocomplete = EmojiSuggestion.GetSuggestions(replacement);
                    }
                    else if (text.Length > 0 && text[0] == '/' && SearchByCommand(text, out string command))
                    {
                        ViewModel.Autocomplete = GetCommands(command.ToLower());
                    }
                    else
                    {
                        ViewModel.Autocomplete = null;
                    }
                }
            }
        }

        private List<TLUserCommand> GetCommands(string command)
        {
            var all = ViewModel.BotCommands;
            if (all != null)
            {
                var results = all.Where(x => x.Item.Command.ToLower().StartsWith(command, StringComparison.OrdinalIgnoreCase)).ToList();
                if (results.Count > 0)
                {
                    return results;
                }
            }

            return null;
        }

        private List<TLUser> GetUsernames(string username, bool inline)
        {
            var query = LocaleHelper.GetQuery(username);
            bool IsMatch(TLUser user)
            {
                //if (user.IsSelf)
                //{
                //    return false;
                //}

                return user.IsLike(query, StringComparison.OrdinalIgnoreCase);
            }

            var results = new List<TLUser>();

            if (inline)
            {
                var peers = UnigramContainer.Current.ResolveType<MainViewModel>().TopPeers;
                if (peers != null)
                {
                    var inlinePeers = peers.FirstOrDefault(x => x.Category is TLTopPeerCategoryBotsInline);
                    if (inlinePeers != null)
                    {
                        foreach (var peer in inlinePeers.Peers)
                        {
                            var user = InMemoryCacheService.Current.GetUser(peer.Peer.Id) as TLUser;
                            if (user != null && IsMatch(user))
                            {
                                results.Add(user);
                            }
                        }
                    }
                }
            }

            if (ViewModel.Full is TLChatFull chatFull && chatFull.Participants is TLChatParticipants chatParticipants)
            {
                foreach (var participant in chatParticipants.Participants)
                {
                    if (participant.User != null && IsMatch(participant.User))
                    {
                        results.Add(participant.User);
                    }
                }
            }
            else if (ViewModel.Full is TLChannelFull channelFull && channelFull.Participants is TLChannelsChannelParticipants channelParticipants)
            {
                foreach (var participant in channelParticipants.Participants)
                {
                    if (participant.User != null && IsMatch(participant.User))
                    {
                        results.Add(participant.User);
                    }
                }
            }

            if (results.Count > 0)
            {
                return results;
            }

            return null;
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
                    if (i == 0 || text[i - 1] == ' ')
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
                    if (i == 0 || text[i - 1] == ' ')
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

        private void UpdateText()
        {
            Document.GetText(TextGetOptions.NoHidden, out string text);
            Text = text;
        }

        private void FormatText()
        {
            if (!ApplicationSettings.Current.IsReplaceEmojiEnabled)
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

        public async Task SendAsync()
        {
            FormatText();

            bool isDirty = _isDirty;

            Document.GetText(TextGetOptions.FormatRtf, out string rtf);
            Document.GetText(TextGetOptions.NoHidden, out string text);

            //Document.SetText(TextSetOptions.FormatRtf, string.Empty);
            Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang1040{\fonttbl{\f0\fnil Segoe UI;}}{\*\generator Riched20 10.0.14393}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs23\lang1033}");

            text = text.Trim();

            //if (isDirty)
            {
                var parser = new RtfToTLParser();
                var reader = new RtfReader(parser);
                reader.LoadRtfText(rtf);
                reader.Parse();

                var message = text.Format();
                var entities = Markdown.Parse(ref message);
                if (entities == null)
                {
                    entities = new List<TLMessageEntityBase>();
                }

                foreach (var entity in parser.Entities)
                {
                    // TODO: Check intersections
                    entities.Add(entity);
                }

                await ViewModel.SendMessageAsync(message, entities.OrderBy(x => x.Offset).ToList(), false);
            }
            //else
            //{
            //    var entities = MessageHelper.GetEntities(ref text);
            //    if (entities != null)
            //    {
            //        await ViewModel.SendMessageAsync(text, entities, false);
            //    }
            //    else
            //    {
            //        ViewModel.SendCommand.Execute(text);
            //    }
            //}
        }

        public bool IsEmpty
        {
            get
            {
                var isEmpty = string.IsNullOrWhiteSpace(Text);
                if (isEmpty)
                {
                    // If the text area is empty it cannot contains markup
                    _isDirty = false;
                }

                return isEmpty;
            }
        }

        #region Username

        public static bool SearchByUsername(string text, out string searchText)
        {
            searchText = string.Empty;

            var found = true;
            var index = -1;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (text[i] == '@')
                {
                    if (i == 0 || text[i - 1] == ' ')
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
                            if (user != null)
                            {
                                InlinePlaceholderText = ViewModel.CurrentInlineBot.BotInlinePlaceholder;
                            }
                        }
                    }
                    else if (string.IsNullOrEmpty(searchText))
                    {
                        var user = ViewModel.CurrentInlineBot;
                        if (user != null)
                        {
                            InlinePlaceholderText = ViewModel.CurrentInlineBot.BotInlinePlaceholder;
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
                if (!placeholder.EndsWith(" "))
                {
                    placeholder += " ";
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
            DependencyProperty.Register("InlinePlaceholderText", typeof(string), typeof(BubbleTextBox), new PropertyMetadata(null, OnInlinePlaceholderTextChanged));

        private static void OnInlinePlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BubbleTextBox)d).UpdateInlinePlaceholder();
        }

        #endregion

        #region Reply

        public TLMessageBase Reply
        {
            get { return (TLMessageBase)GetValue(ReplyProperty); }
            set { SetValue(ReplyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Reply.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ReplyProperty =
            DependencyProperty.Register("Reply", typeof(TLMessageBase), typeof(BubbleTextBox), new PropertyMetadata(null, OnReplyChanged));

        private static void OnReplyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BubbleTextBox)d).OnReplyChanged((TLMessageBase)e.NewValue, (TLMessageBase)e.OldValue);
        }

        private async void OnReplyChanged(TLMessageBase newValue, TLMessageBase oldValue)
        {
            if (newValue != null)
            {
                await Task.Delay(200);
                Focus(FocusState.Keyboard);
            }
        }

        #endregion

        public void SetText(string text, TLVector<TLMessageEntityBase> entities)
        {
            if (entities != null && entities.Count > 0)
            {
                entities = new TLVector<TLMessageEntityBase>(entities);

                var builder = new StringBuilder(text);
                var addToOffset = 0;

                foreach (var entity in entities.ToList())
                {
                    if (entity is TLMessageEntityCode)
                    {
                        builder.Insert(entity.Offset + entity.Length + addToOffset, "`");
                        builder.Insert(entity.Offset + addToOffset, "`");
                        addToOffset += 2;
                        entities.Remove(entity);
                    }
                    else if (entity is TLMessageEntityPre)
                    {
                        builder.Insert(entity.Offset + entity.Length + addToOffset, "```");
                        builder.Insert(entity.Offset + addToOffset, "```");
                        addToOffset += 6;
                        entities.Remove(entity);
                    }
                    else if (entity is TLMessageEntityBold)
                    {
                        builder.Insert(entity.Offset + entity.Length + addToOffset, "**");
                        builder.Insert(entity.Offset + addToOffset, "**");
                        addToOffset += 4;
                        entities.Remove(entity);
                    }
                    else if (entity is TLMessageEntityItalic)
                    {
                        builder.Insert(entity.Offset + entity.Length + addToOffset, "__");
                        builder.Insert(entity.Offset + addToOffset, "__");
                        addToOffset += 4;
                        entities.Remove(entity);
                    }
                    else if (entity is TLMessageEntityTextUrl textUrl)
                    {
                        builder.Insert(entity.Offset + entity.Length + addToOffset, $"]({textUrl.Url})");
                        builder.Insert(entity.Offset + addToOffset, "[");
                        addToOffset += 4 + textUrl.Url.Length;
                        entities.Remove(entity);
                    }
                    else
                    {
                        entity.Offset += addToOffset;
                    }
                }

                text = builder.ToString();

                var document = new RtfDocument(PaperSize.A4, PaperOrientation.Portrait, Lcid.English);
                var segoe = document.CreateFont("Segoe UI");
                var consolas = document.CreateFont("Consolas");
                document.SetDefaultFont("Segoe UI");

                var paragraph = document.AddParagraph();
                var previous = 0;

                foreach (var entity in entities)
                {
                    if (entity.Offset > previous)
                    {
                        paragraph.Text.Append(text.Substring(previous, entity.Offset - previous));
                    }

                    var type = entity.TypeId;
                    //if (type == TLType.MessageEntityBold)
                    //{
                    //    paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                    //    paragraph.addCharFormat(entity.Offset, entity.Offset + entity.Length - 1).FontStyle.addStyle(FontStyleFlag.Bold);
                    //}
                    //else if (type == TLType.MessageEntityItalic)
                    //{
                    //    paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                    //    paragraph.addCharFormat(entity.Offset, entity.Offset + entity.Length - 1).FontStyle.addStyle(FontStyleFlag.Italic);
                    //}
                    //else if (type == TLType.MessageEntityCode)
                    //{
                    //    paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                    //    paragraph.addCharFormat(entity.Offset, entity.Offset + entity.Length - 1).Font = consolas;
                    //}
                    //else if (type == TLType.MessageEntityPre)
                    //{
                    //    // TODO any additional
                    //    paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                    //    paragraph.addCharFormat(entity.Offset, entity.Offset + entity.Length - 1).Font = consolas;
                    //}
                    //else 
                    if (type == TLType.MessageEntityUrl ||
                             type == TLType.MessageEntityEmail ||
                             type == TLType.MessageEntityMention ||
                             type == TLType.MessageEntityHashtag ||
                             type == TLType.MessageEntityBotCommand)
                    {
                        paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                    }
                    else if (type == TLType.MessageEntityTextUrl ||
                             type == TLType.MessageEntityMentionName ||
                             type == TLType.InputMessageEntityMentionName)
                    {
                        object data;
                        if (type == TLType.MessageEntityTextUrl)
                        {
                            data = ((TLMessageEntityTextUrl)entity).Url;
                        }
                        else if (type == TLType.MessageEntityMentionName)
                        {
                            data = ((TLMessageEntityMentionName)entity).UserId;
                        }
                        else // if(type == TLType.InputMessageEntityMentionName)
                        {
                            data = ((TLInputMessageEntityMentionName)entity).UserId;
                        }

                        //var hyper = new Hyperlink();
                        //hyper.Click += (s, args) => Hyperlink_Navigate(type, data);
                        //hyper.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                        //hyper.Foreground = foreground;
                        //paragraph.Inlines.Add(hyper);

                        paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                        paragraph.addCharFormat(entity.Offset, entity.Offset + entity.Length - 1).LocalHyperlink = data.ToString();
                    }

                    previous = entity.Offset + entity.Length;
                }

                if (text.Length > previous)
                {
                    paragraph.Text.Append(text.Substring(previous));
                }

                _isDirty = true;
                Document.SetText(TextSetOptions.FormatRtf, document.Render());
                Document.GetRange(0, text.Length).CharacterFormat.ForegroundColor = Document.GetDefaultCharacterFormat().ForegroundColor;
                Document.Selection.SetRange(text.Length, text.Length);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang1040{\fonttbl{\f0\fnil Segoe UI;}}{\*\generator Riched20 10.0.14393}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs23\lang1033}");
                }
                else
                {
                    Document.SetText(TextSetOptions.None, text);
                    Document.Selection.SetRange(text.Length, text.Length);
                }
            }
        }
    }

    public class RtfToTLParser : RtfSarParser
    {
        private bool _bold;
        private bool _italic;
        private bool _firstPard;

        private string _groupText;
        private string _lastKey;
        private int? _field;

        private int _length;

        private Stack<TLMessageEntityBase> _entities;

        public List<TLMessageEntityBase> Entities { get; private set; }

        public override void StartRtfDocument()
        {
            _entities = new Stack<TLMessageEntityBase>();
            Entities = null;

            _bold = false;
            _italic = false;
            _firstPard = false;

            _groupText = null;
            _lastKey = null;
            _field = null;

            _length = 0;
        }

        public override void StartRtfGroup()
        {
            if (_firstPard)
            {
                if (_field.HasValue)
                    _field++;
            }
        }

        public override void RtfControl(string key, bool hasParameter, int parameter)
        {
            if (_firstPard && key == "'" && hasParameter)
            {
                if (_field.HasValue && _lastKey == "fldinst")
                {
                    _groupText += (char)parameter;
                }
                else if (_field.HasValue && _lastKey.Equals("fldrslt"))
                {
                    _groupText += (char)parameter;
                }
                else if (_bold || _italic)
                {
                    _groupText += (char)parameter;
                }
                else
                {
                    _groupText += (char)parameter;
                    HandleBasicText();
                    _groupText = string.Empty;
                }
            }
        }

        public override void RtfKeyword(string key, bool hasParameter, int parameter)
        {
            if (key.Equals("pard"))
            {
                _firstPard = true;
            }
            else if (key.Equals("field"))
            {
                _field = !hasParameter || (hasParameter && parameter == 1) ? new int?(0) : null;
            }
            else if (key.Equals("b"))
            {
                if (!hasParameter || (hasParameter && parameter == 1))
                {
                    _groupText = string.Empty;
                    _bold = true;
                }
                else
                {
                    HandleBoldText();
                    _groupText = string.Empty;
                    _bold = false;
                }
            }
            else if (key.Equals("i"))
            {
                if (!hasParameter || (hasParameter && parameter == 1))
                {
                    _groupText = string.Empty;
                    _italic = true;
                }
                else
                {
                    HandleItalicText();
                    _groupText = string.Empty;
                    _italic = false;
                }
            }
            else if (key.Equals("fldinst") || key.Equals("fldrslt"))
            {
                _lastKey = key;
            }
        }

        public override void RtfText(string text)
        {
            if (_firstPard)
            {
                if (_field.HasValue && _lastKey == "fldinst")
                {
                    if (text.IndexOf("HYPERLINK") == 0)
                        _groupText += text.Substring("HYPERLINK ".Length);
                    else
                        _groupText += text;
                }
                else if (_field.HasValue && _lastKey.Equals("fldrslt"))
                {
                    _groupText += text;
                }
                else if (_bold || _italic)
                {
                    _groupText += text;
                }
                else
                {
                    _groupText += text;
                    HandleBasicText();
                    _groupText = string.Empty;
                }
            }
        }

        public override void EndRtfGroup()
        {
            if (_firstPard)
            {
                if (_bold) HandleBoldText();
                else if (_italic) HandleItalicText();
                else if (_field.HasValue && _field == 2 && _lastKey.Equals("fldinst")) HandleHyperlinkUrl();
                else if (_field.HasValue && _field == 2 && _lastKey.Equals("fldrslt")) HandleHyperlinkText();
                else if (string.IsNullOrEmpty(_groupText) == false) HandleBasicText();

                _bold = false;
                _italic = false;
                _lastKey = string.Empty;
                _groupText = string.Empty;

                if (_field.HasValue)
                    _field--;

                if (_field.HasValue && _field < 0)
                    _field = null;
            }
        }

        private void HandleBoldText()
        {
            _entities.Push(new TLMessageEntityBold { Offset = _length, Length = _groupText.Length });
            _length += _groupText.Length;
        }

        private void HandleItalicText()
        {
            _entities.Push(new TLMessageEntityItalic { Offset = _length, Length = _groupText.Length });
            _length += _groupText.Length;
        }

        private void HandleHyperlinkUrl()
        {
            var userId = int.Parse(_groupText.Trim().Trim('"'));
            var user = InMemoryCacheService.Current.GetUser(userId) as TLUser;
            if (user != null && user.HasAccessHash)
            {
                _entities.Push(new TLInputMessageEntityMentionName { UserId = new TLInputUser { UserId = user.Id, AccessHash = user.AccessHash.Value } });
            }
        }

        private void HandleHyperlinkText()
        {
            if (_entities.Count > 0)
            {
                var mention = _entities.Peek() as TLInputMessageEntityMentionName;
                if (mention != null)
                {
                    mention.Offset = _length;
                    mention.Length = _groupText.Length;
                }

                _length += _groupText.Length;
            }
        }

        private void HandleBasicText()
        {
            _length += _groupText.Length;
        }

        public override void EndRtfDocument()
        {
            if (Entities == null)
            {
                Entities = new List<TLMessageEntityBase>(_entities.Reverse());
            }
        }
    }
}
