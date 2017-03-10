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
using Unigram.Core.Dependency;
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

namespace Unigram.Controls
{
    public class BubbleTextBox : RichEditBox, IHandle<TLUpdateDraftMessage>, IHandle<EditMessageEventArgs>, IHandle
    {
        private ContentControl InlinePlaceholderTextContentPresenter;

        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private MenuFlyout _flyout;
        private MenuFlyoutPresenter _presenter;

        private bool _updatingText;

        private readonly IDisposable _textChangedSubscription;

        // True when the RichEdithBox MIGHT contains formatting (bold, italic, hyperlinks) 
        private bool _isDirty;

        public BubbleTextBox()
        {
            DefaultStyleKey = typeof(BubbleTextBox);
            ClipboardCopyFormat = RichEditClipboardFormat.PlainText;

            _flyout = new MenuFlyout();
            _flyout.Items.Add(new MenuFlyoutItem { Text = "Bold" });
            _flyout.Items.Add(new MenuFlyoutItem { Text = "Italic" });
            _flyout.AllowFocusOnInteraction = false;
            _flyout.AllowFocusWhenDisabled = false;

            ((MenuFlyoutItem)_flyout.Items[0]).Click += Bold_Click;
            ((MenuFlyoutItem)_flyout.Items[1]).Click += Italic_Click;
            ((MenuFlyoutItem)_flyout.Items[1]).Loaded += Italic_Loaded;

#if DEBUG
            // To test hyperlinks (Used for mention name => to tag people that has no username)
            _flyout.Items.Add(new MenuFlyoutItem { Text = "Hyperlink" });
            ((MenuFlyoutItem)_flyout.Items[2]).Click += Hyperlink_Click;
#endif

            Paste += OnPaste;
            Clipboard.ContentChanged += Clipboard_ContentChanged;

            SelectionChanged += OnSelectionChanged;
            TextChanged += OnTextChanged;

            var textChangedEvents = Observable.FromEventPattern<RoutedEventHandler, RoutedEventArgs>(
                keh => { TextChanged += keh; },
                keh => { TextChanged -= keh; });

            _textChangedSubscription = textChangedEvents
                .Throttle(TimeSpan.FromMilliseconds(200))
                .Subscribe(e => Execute.BeginOnUIThread(() => UpdateInlineBot(true)));

            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UnigramContainer.Current.ResolveType<ITelegramEventAggregator>().Subscribe(this);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnigramContainer.Current.ResolveType<ITelegramEventAggregator>().Unsubscribe(this);
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
            }
            else if (package.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = true;
            }
            else if (package.Contains(StandardDataFormats.Bitmap))
            {
                e.Handled = true;

                var bitmap = await package.GetBitmapAsync();
                var cache = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\paste.jpg", CreationCollisionOption.ReplaceExisting);

                using (var stream = await bitmap.OpenReadAsync())
                using (var reader = new DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    var buffer = new byte[(int)stream.Size];
                    reader.ReadBytes(buffer);
                    await FileIO.WriteBytesAsync(cache, buffer);
                }

                ViewModel.SendPhotoCommand.Execute(new StoragePhoto(cache));
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
            if (Document.Selection.Length != 0)
            {
                Document.Selection.GetRect(PointOptions.ClientCoordinates, out Rect rect, out int hit);
                _flyout.ShowAt(this, new Point(rect.X + 12, rect.Y - _presenter?.ActualHeight ?? 0));
            }
            else
            {
                _flyout.Hide();
            }
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
            if (args.VirtualKey == VirtualKey.Enter && FocusState != FocusState.Unfocused)
            {
                // Check if CTRL or Shift is also pressed in addition to Enter key.
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);
                var key = Window.Current.CoreWindow.GetKeyState(args.VirtualKey);

                // If there is text and CTRL/Shift is not pressed, send message. Else allow new row.
                if (key.HasFlag(CoreVirtualKeyStates.Down) && !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down))
                {
                    AcceptsReturn = false;
                    await SendAsync();
                }
                else
                {
                    AcceptsReturn = true;
                }
            }
        }

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

            base.OnKeyDown(e);
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
        }

        private void UpdateInlineBot(bool fast)
        {
            var text = Text;
            var command = string.Empty;
            var inline = SearchInlineBotResults(text, out command);
            if (inline && fast)
            {
                ViewModel.GetInlineBotResults(command);
            }
            else if (!inline)
            {
                ViewModel.CurrentInlineBot = null;
                ViewModel.InlineBotResults = null;
                InlinePlaceholderText = string.Empty;

                if (fast)
                {
                    // TODO: verify if it is actually a sticker
                    if (text.Length < 14 && !string.IsNullOrWhiteSpace(text))
                    {
                        ViewModel.StickerPack = DatabaseContext.Current.SelectStickerPack(text.Trim());
                    }
                    else
                    {
                        ViewModel.StickerPack = null;
                    }
                }
                else
                {
                    ViewModel.StickerPack = null;
                }
            }
        }

        private void UpdateText()
        {
            Document.GetText(TextGetOptions.NoHidden, out string text);

            _updatingText = true;
            Text = text;
            _updatingText = false;
        }

        private void FormatText()
        {
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

            Document.GetText(TextGetOptions.FormatRtf, out string text);
            Document.GetText(TextGetOptions.NoHidden, out string planText);

            //Document.SetText(TextSetOptions.FormatRtf, string.Empty);
            Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang1040{\fonttbl{\f0\fnil Segoe UI;}}{\*\generator Riched20 10.0.14393}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs23\lang1033}");

            _updatingText = true;
            planText = planText.Trim();
            ViewModel.Text = planText;
            _updatingText = false;

            if (isDirty)
            {
                var parser = new RtfToTLParser();
                var reader = new RtfReader(parser);
                reader.LoadRtfText(text);
                reader.Parse();

                await ViewModel.SendMessageAsync(parser.Entities, false);
            }
            else
            {
                ViewModel.SendCommand.Execute(null);
            }
        }

        public bool IsEmpty
        {
            get
            {
                // TODO: a better implementation?

                Document.GetText(TextGetOptions.None, out string text);

                var isEmpty = string.IsNullOrWhiteSpace(text);
                if (isEmpty)
                {
                    // If the text area is empty it cannot contains markup
                    _isDirty = false;
                }

                return isEmpty;
            }
        }

        #region Username

        private static bool SearchByUsernames(string text, out string searchText)
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

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(BubbleTextBox), new PropertyMetadata(string.Empty, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BubbleTextBox)d).OnTextChanged((string)e.NewValue, (string)e.OldValue);
        }

        private void OnTextChanged(string newValue, string oldValue)
        {
            if (_updatingText)
            {
                _updatingText = false;
                return;
            }

            if (string.IsNullOrEmpty(newValue))
            {
                Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang1040{\fonttbl{\f0\fnil Segoe UI;}}{\*\generator Riched20 10.0.14393}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs23\lang1033}");
            }
            else
            {
                Document.SetText(TextSetOptions.None, newValue);
            }
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

        private void OnMessageChanged(string text, TLVector<TLMessageEntityBase> entities)
        {
            if (entities != null && entities.Count > 0)
            {
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
                    if (type == TLType.MessageEntityBold)
                    {
                        paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                        paragraph.addCharFormat(entity.Offset, entity.Offset + entity.Length - 1).FontStyle.addStyle(FontStyleFlag.Bold);
                    }
                    else if (type == TLType.MessageEntityItalic)
                    {
                        paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                        paragraph.addCharFormat(entity.Offset, entity.Offset + entity.Length - 1).FontStyle.addStyle(FontStyleFlag.Italic);
                    }
                    else if (type == TLType.MessageEntityCode)
                    {
                        paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                        paragraph.addCharFormat(entity.Offset, entity.Offset + entity.Length - 1).Font = consolas;
                    }
                    else if (type == TLType.MessageEntityPre)
                    {
                        // TODO any additional
                        paragraph.Text.Append(text.Substring(entity.Offset, entity.Length));
                        paragraph.addCharFormat(entity.Offset, entity.Offset + entity.Length - 1).Font = consolas;
                    }
                    else if (type == TLType.MessageEntityUrl ||
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
                Document.Selection.SetRange(text.Length, text.Length);
            }
            else
            {
                Document.SetText(TextSetOptions.None, text);
                Document.Selection.SetRange(text.Length, text.Length);
            }
        }

        public void Handle(EditMessageEventArgs args)
        {
            Execute.BeginOnUIThread(() =>
            {
                var message = args.Message;
                var flag = false;

                var userBase = ViewModel.With as TLUserBase;
                var chatBase = ViewModel.With as TLChatBase;
                if (userBase != null && message.ToId is TLPeerUser && !message.IsOut && userBase.Id == message.FromId.Value)
                {
                    flag = true;
                }
                else if (userBase != null && message.ToId is TLPeerUser && message.IsOut && userBase.Id == message.ToId.Id)
                {
                    flag = true;
                }
                else if (chatBase != null && message.ToId is TLPeerChat && chatBase.Id == message.ToId.Id)
                {
                    flag = true;
                }
                else if (chatBase != null && message.ToId is TLPeerChannel && chatBase.Id == message.ToId.Id)
                {
                    flag = true;
                }

                if (flag)
                {
                    OnMessageChanged(args.Text, message.Entities);
                }
            });
        }

        public void Handle(TLUpdateDraftMessage args)
        {
            Execute.BeginOnUIThread(() =>
            {
                var flag = false;

                var userBase = ViewModel.With as TLUserBase;
                var chatBase = ViewModel.With as TLChatBase;
                if (userBase != null && args.Peer is TLPeerUser && userBase.Id == args.Peer.Id)
                {
                    flag = true;
                }
                else if (chatBase != null && args.Peer is TLPeerChat && chatBase.Id == args.Peer.Id)
                {
                    flag = true;
                }
                else if (chatBase != null && args.Peer is TLPeerChannel && chatBase.Id == args.Peer.Id)
                {
                    flag = true;
                }

                if (flag)
                {
                    var draft = args.Draft as TLDraftMessage;
                    if (draft != null)
                    {
                        OnMessageChanged(draft.Message, draft.Entities);
                    }

                    var emptyDraft = args.Draft as TLDraftMessageEmpty;
                    if (emptyDraft != null)
                    {
                        Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang1040{\fonttbl{\f0\fnil Segoe UI;}}{\*\generator Riched20 10.0.14393}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs23\lang1033}");
                    }
                }
            });
        }
    }

    public class EditMessageEventArgs : EventArgs
    {
        public TLMessage Message { get; private set; }

        public string Text { get; private set; }

        public EditMessageEventArgs(TLMessage message, string text)
        {
            Message = message;
            Text = text;
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
