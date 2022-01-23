using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class FormattedTextBox : RichEditBox
    {
        private readonly FormattedTextFlyout _selectionFlyout;
        private readonly MenuFlyoutSubItem _proofingFlyout;

        public FormattedTextBox()
        {
            DefaultStyleKey = typeof(FormattedTextBox);

            ClipboardCopyFormat = RichEditClipboardFormat.PlainText;

            Paste += OnPaste;

            _proofingFlyout = new MenuFlyoutSubItem();
            _proofingFlyout.Text = Strings.Resources.lng_spellchecker_submenu;

            SelectionFlyout = new Flyout
            {
                Content = _selectionFlyout = new FormattedTextFlyout(this),

                AllowFocusOnInteraction = false,
                ShowMode = FlyoutShowMode.TransientWithDismissOnPointerMoveAway,
                FlyoutPresenterStyle = App.Current.Resources["CommandFlyoutPresenterStyle"] as Style,
            };

            ContextFlyout = new MenuFlyout();
            ContextFlyout.Opening += OnContextFlyoutOpening;
            ContextFlyout.Closing += OnContextFlyoutClosing;

            DisabledFormattingAccelerators = DisabledFormattingAccelerators.All;
            KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

            CreateKeyboardAccelerator(VirtualKey.B);
            CreateKeyboardAccelerator(VirtualKey.I);
            CreateKeyboardAccelerator(VirtualKey.U);
            CreateKeyboardAccelerator(VirtualKey.X, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            CreateKeyboardAccelerator(VirtualKey.M, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            CreateKeyboardAccelerator(VirtualKey.P, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            CreateKeyboardAccelerator(VirtualKey.K);
            CreateKeyboardAccelerator(VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

            // Overridden but not used
            CreateKeyboardAccelerator(VirtualKey.E);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private bool _resetSize;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_resetSize)
            {
                Height = double.NaN;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
        }

        public bool IsReplaceEmojiEnabled { get; set; } = true;

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (FocusState == FocusState.Unfocused || !IsReplaceEmojiEnabled || string.Equals(Document.Selection.CharacterFormat.Name, "Consolas", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var character = Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));

            //var matches = Emoticon.Data.Keys.Where(x => x.EndsWith(character)).ToArray();
            if (Emoticon.Matches.TryGetValue(character[0], out string[] matches))
            {
                var length = matches.Max(x => x.Length);
                var start = Math.Max(Document.Selection.EndPosition - length, 0);

                var range = Document.GetRange(start, Document.Selection.EndPosition);
                range.GetText(TextGetOptions.NoHidden, out string value);

                var emoticon = matches.FirstOrDefault(x => value.EndsWith(x));
                if (emoticon != null)
                {
                    Document.BeginUndoGroup();
                    range.SetRange(range.EndPosition - emoticon.Length, range.EndPosition);
                    range.SetText(TextSetOptions.None, Emoticon.Data[emoticon]);
                    range.SetRange(range.EndPosition, range.EndPosition);
                    range.SetText(TextSetOptions.None, emoticon);
                    range.CharacterFormat.Hidden = FormatEffect.On;
                    Document.EndUndoGroup();

                    Document.Selection.SetRange(range.EndPosition, range.EndPosition);
                    args.Handled = true;
                }
            }
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Back && IsReplaceEmojiEnabled)
            {
                Document.GetText(TextGetOptions.None, out _);

                var range = Document.Selection.GetClone();
                if (range.Expand(TextRangeUnit.Hidden) != 0 && Emoticon.Data.TryGetValue(range.Text, out string emoji))
                {
                    var emoticon = range.Text;

                    Document.BeginUndoGroup();
                    range.SetRange(range.StartPosition - emoji.Length, range.EndPosition);
                    range.SetText(TextSetOptions.Unhide, emoticon);
                    Document.EndUndoGroup();

                    Document.Selection.SetRange(range.EndPosition, range.EndPosition);
                    return;
                }
            }
            else if (e.Key == VirtualKey.Enter)
            {
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

                var send = SettingsService.Current.IsSendByEnterEnabled
                    ? !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down)
                    : ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);

                AcceptsReturn = !send;
                e.Handled = send;

                // If handwriting panel is open, the app would crash on send.
                // Still, someone should fill a ticket to Microsoft about this.
                if (send && HandwritingView.IsOpen)
                {
                    void handler(object s, RoutedEventArgs args)
                    {
                        OnAccept();
                        HandwritingView.Unloaded -= handler;
                    }

                    HandwritingView.Unloaded += handler;
                    HandwritingView.TryClose();
                }
                else if (send)
                {
                    OnAccept();
                }
            }
            else if (e.Key == VirtualKey.Z)
            {
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                if (ctrl && shift)
                {
                    if (Document.CanRedo())
                    {
                        Document.Redo();
                    }

                    e.Handled = true;
                }
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        protected virtual void OnAccept()
        {
            Accept?.Invoke(this, EventArgs.Empty);
        }

        public event TypedEventHandler<FormattedTextBox, EventArgs> Accept;

        #region Context menu

        private void OnContextFlyoutOpening(object sender, object e)
        {
            var flyout = ContextFlyout as MenuFlyout;
            if (flyout == null)
            {
                return;
            }

            flyout.Items.Clear();

            var selection = Document.Selection;
            var format = Document.Selection.CharacterFormat;

            var length = Math.Abs(selection.Length) > 0;

            var clipboard = Clipboard.GetContent();

            var clone = Document.Selection.GetClone();
            clone.StartOf(TextRangeUnit.Link, true);
            var mention = TryGetUserId(clone, out long userId);

            var formatting = new MenuFlyoutSubItem
            {
                Text = Strings.Resources.lng_menu_formatting,
                Icon = new FontIcon { Glyph = Icons.TextFont, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily }
            };

            formatting.CreateFlyoutItem(length, ToggleBold, Strings.Resources.Bold, new FontIcon { Glyph = Icons.TextBold }, VirtualKey.B);
            formatting.CreateFlyoutItem(length, ToggleItalic, Strings.Resources.Italic, new FontIcon { Glyph = Icons.TextItalic }, VirtualKey.I);
            formatting.CreateFlyoutItem(length, ToggleUnderline, Strings.Resources.Underline, new FontIcon { Glyph = Icons.TextUnderline }, VirtualKey.U);
            formatting.CreateFlyoutItem(length, ToggleStrikethrough, Strings.Resources.Strike, new FontIcon { Glyph = Icons.TextStrikethrough }, VirtualKey.X, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            formatting.CreateFlyoutItem(length && format.Name != "Consolas", ToggleMonospace, Strings.Resources.Mono, new FontIcon { Glyph = Icons.Code }, VirtualKey.M, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            formatting.CreateFlyoutItem(length, ToggleSpoiler, Strings.Resources.Spoiler, new FontIcon { Glyph = Icons.TabInPrivate }, VirtualKey.P, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            formatting.CreateFlyoutSeparator();
            formatting.CreateFlyoutItem(!mention, CreateLink, clone.Link.Length > 0 ? Strings.Resources.EditLink : Strings.Resources.lng_menu_formatting_link_create, new FontIcon { Glyph = Icons.Link }, VirtualKey.K);
            formatting.CreateFlyoutSeparator();
            formatting.CreateFlyoutItem(length && !IsDefault(format), ToggleRegular, Strings.Resources.Regular, null, VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

            flyout.CreateFlyoutItem(Document.CanUndo(), ContextUndo_Click, Strings.Resources.lng_wnd_menu_undo, new FontIcon { Glyph = Icons.ArrowUndo }, VirtualKey.Z);
            flyout.CreateFlyoutItem(Document.CanRedo(), ContextRedo_Click, Strings.Resources.lng_wnd_menu_redo, new FontIcon { Glyph = Icons.ArrowRedo }, VirtualKey.Y);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(length && Document.CanCopy(), ContextCut_Click, Strings.Resources.lng_mac_menu_cut, new FontIcon { Glyph = Icons.Cut }, VirtualKey.X);
            flyout.CreateFlyoutItem(length && Document.CanCopy(), ContextCopy_Click, Strings.Resources.Copy, new FontIcon { Glyph = Icons.DocumentCopy }, VirtualKey.C);
            flyout.CreateFlyoutItem(Document.CanPaste(), ContextPaste_Click, Strings.Resources.lng_mac_menu_paste, new FontIcon { Glyph = Icons.ClipboardPaste }, VirtualKey.V);
            flyout.CreateFlyoutItem(length, ContextDelete_Click, Strings.Resources.Delete);
            flyout.CreateFlyoutSeparator();
            flyout.Items.Add(formatting);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(!IsEmpty, ContextSelectAll_Click, Strings.Resources.lng_mac_menu_select_all, null, VirtualKey.A);

            if (ProofingMenuFlyout is MenuFlyout proofing && proofing.Items.Count > 0)
            {
                flyout.CreateFlyoutSeparator();
                //flyout.Items.Add(_proofingMenu);

                foreach (var item in proofing.Items)
                {
                    flyout.Items.Add(item);
                }
            }
        }

        private void OnContextFlyoutClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            _proofingFlyout.Items.Clear();

            if (sender is MenuFlyout flyout)
            {
                flyout.Items.Clear();
            }
        }

        public void ToggleBold()
        {
            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, false);
            Document.Selection.CharacterFormat.Bold = FormatEffect.Toggle;
            Document.ApplyDisplayUpdates();

            _selectionFlyout.Update(Document.Selection.CharacterFormat);
        }

        public void ToggleItalic()
        {
            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, false);
            Document.Selection.CharacterFormat.Italic = FormatEffect.Toggle;
            Document.ApplyDisplayUpdates();

            _selectionFlyout.Update(Document.Selection.CharacterFormat);
        }

        public void ToggleUnderline()
        {
            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, false);
            Document.Selection.CharacterFormat.Underline = Document.Selection.CharacterFormat.Underline != UnderlineType.Single ? UnderlineType.Single : UnderlineType.None;
            Document.ApplyDisplayUpdates();

            _selectionFlyout.Update(Document.Selection.CharacterFormat);
        }

        public void ToggleStrikethrough()
        {
            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, false);
            Document.Selection.CharacterFormat.Strikethrough = FormatEffect.Toggle;
            Document.ApplyDisplayUpdates();

            _selectionFlyout.Update(Document.Selection.CharacterFormat);
        }

        public void ToggleMonospace()
        {
            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, true);
            Document.Selection.CharacterFormat.Name = "Consolas";
            Document.ApplyDisplayUpdates();

            _selectionFlyout.Update(Document.Selection.CharacterFormat);
        }

        private void ToggleSpoiler()
        {
            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, false);
            Document.Selection.CharacterFormat.BackgroundColor = Colors.Gray;
            Document.ApplyDisplayUpdates();

            _selectionFlyout.Update(Document.Selection.CharacterFormat);
        }

        public void ToggleRegular()
        {
            if (Math.Abs(Document.Selection.Length) < 1)
            {
                return;
            }

            Document.BatchDisplayUpdates();
            ClearStyle(Document.Selection, true);
            Document.ApplyDisplayUpdates();
        }

        public async void CreateLink()
        {
            var range = Document.Selection.GetClone();
            var clone = Document.Selection.GetClone();
            clone.StartOf(TextRangeUnit.Link, true);

            if (clone.Link.Length > 0)
            {
                range.Expand(TextRangeUnit.Link);
            }

            range.GetText(TextGetOptions.NoHidden, out string text);

            var start = Math.Min(range.StartPosition, range.EndPosition);
            var end = Math.Max(range.StartPosition, range.EndPosition);

            var dialog = new CreateLinkPopup();
            dialog.Text = text;
            dialog.Link = range.Link.Trim('"');

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            Document.BatchDisplayUpdates();
            range.SetRange(start, end);
            range.CharacterFormat = Document.GetDefaultCharacterFormat();

            range.SetText(end > start ? TextSetOptions.Unlink : TextSetOptions.None, dialog.Text);
            range.SetRange(start, start + dialog.Text.Length);
            range.Link = $"\"{dialog.Link}\"";

            Document.Selection.SetRange(range.EndPosition, range.EndPosition);
            Document.ApplyDisplayUpdates();
        }

        private void ClearStyle(ITextRange range, bool monospace)
        {
            if (monospace)
            {
                var start = Math.Min(range.StartPosition, range.EndPosition);
                var end = Math.Max(range.StartPosition, range.EndPosition);

                range.SetRange(start, end);
                range.CharacterFormat = Document.GetDefaultCharacterFormat();

                range.GetText(TextGetOptions.NoHidden, out string text);
                range.SetText(TextSetOptions.Unlink, text);
                range.SetRange(start, start + text.Length);
            }
            else
            {
                range.CharacterFormat.Name = Document.GetDefaultCharacterFormat().Name;
            }
        }

        protected bool IsDefault(ITextCharacterFormat format)
        {
            return IsEqual(format, Document.GetDefaultCharacterFormat());
        }

        protected bool IsEqual(ITextCharacterFormat format, ITextCharacterFormat document)
        {
            return document.AllCaps == format.AllCaps &&
                document.BackgroundColor == format.BackgroundColor &&
                document.Bold == format.Bold &&
                document.FontStretch == format.FontStretch &&
                document.FontStyle == format.FontStyle &&
                document.ForegroundColor == format.ForegroundColor &&
                document.Hidden == format.Hidden &&
                document.Italic == format.Italic &&
                document.Kerning == format.Kerning &&
                //document.LanguageTag == format.LanguageTag &&
                document.LinkType == format.LinkType &&
                document.Name == format.Name &&
                document.Outline == format.Outline &&
                document.Position == format.Position &&
                document.ProtectedText == format.ProtectedText &&
                document.Size == format.Size &&
                document.SmallCaps == format.SmallCaps &&
                document.Spacing == format.Spacing &&
                document.Strikethrough == format.Strikethrough &&
                document.Subscript == format.Subscript &&
                document.Superscript == format.Superscript &&
                //document.TextScript == format.TextScript &&
                document.Underline == format.Underline &&
                document.Weight == format.Weight;
        }

        protected bool TryGetUserId(ITextRange range, out long userId)
        {
            var link = range.Link.Trim('"');
            if (link.StartsWith("tg-user://") && long.TryParse(link.Substring("tg-user://".Length), out userId))
            {
                return true;
            }

            userId = 0;
            return false;
        }

        protected bool TryGetEntityType(string link, out TextEntityType type)
        {
            link = link.Trim('"');

            if (IsUrlValid(link))
            {
                type = new TextEntityTypeTextUrl(link);
                return true;
            }
            else if (link.StartsWith("tg-user://") && long.TryParse(link.Substring("tg-user://".Length), out long userId))
            {
                type = new TextEntityTypeMentionName(userId);
                return true;
            }

            type = null;
            return false;
        }

        private bool IsUrlValid(string url)
        {
            var response = Client.Execute(new GetTextEntities(url));
            if (response is TextEntities entities)
            {
                return entities.Entities.Count == 1 && entities.Entities[0].Offset == 0 && entities.Entities[0].Length == url.Length && entities.Entities[0].Type is TextEntityTypeUrl;
            }

            return false;
        }

        private void ContextUndo_Click()
        {
            Document.Undo();
        }

        private void ContextRedo_Click()
        {
            Document.Redo();
        }

        private void ContextCut_Click()
        {
            Document.Selection.Cut();
        }

        private void ContextCopy_Click()
        {
            Document.Selection.Copy();
        }

        private void ContextPaste_Click()
        {
            OnPaste(new HandledEventArgs(false));
        }

        private void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            var args = new HandledEventArgs(false);
            OnPaste(args);

            e.Handled = args.Handled;
        }

        protected virtual async void OnPaste(HandledEventArgs e)
        {
            try
            {
                // If the user tries to paste RTF content from any TOM control (Visual Studio, Word, Wordpad, browsers)
                // we have to handle the pasting operation manually to allow plaintext only.
                var package = Clipboard.GetContent();
                if (package.AvailableFormats.Contains(StandardDataFormats.Text) && package.AvailableFormats.Contains("application/x-tl-field-tags"))
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

        private void ContextDelete_Click()
        {
            Document.Selection.SetText(TextSetOptions.None, string.Empty);
        }

        private void ContextSelectAll_Click()
        {
            Document.Selection.Expand(TextRangeUnit.Paragraph);
        }

        private void CreateKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            var accelerator = new KeyboardAccelerator { Modifiers = modifiers, Key = key, ScopeOwner = this };
            accelerator.Invoked += FlyoutAccelerator_Invoked;

            KeyboardAccelerators.Add(accelerator);
        }

        private void FlyoutAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;

            var selection = Document.Selection;
            var format = Document.Selection.CharacterFormat;

            var length = Math.Abs(selection.Length) > 0;

            if (sender.Key == VirtualKey.B && sender.Modifiers == VirtualKeyModifiers.Control && length)
            {
                ToggleBold();
            }
            else if (sender.Key == VirtualKey.I && sender.Modifiers == VirtualKeyModifiers.Control && length)
            {
                ToggleItalic();
            }
            else if (sender.Key == VirtualKey.U && sender.Modifiers == VirtualKeyModifiers.Control && length)
            {
                ToggleUnderline();
            }
            else if (sender.Key == VirtualKey.X && sender.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift) && length)
            {
                ToggleStrikethrough();
            }
            else if (sender.Key == VirtualKey.M && sender.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift) && length && format.Name != "Consolas")
            {
                ToggleMonospace();
            }
            else if (sender.Key == VirtualKey.P && sender.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift) && length && format.ForegroundColor != Colors.Gray)
            {
                ToggleSpoiler();
            }
            else if (sender.Key == VirtualKey.K && sender.Modifiers == VirtualKeyModifiers.Control)
            {
                CreateLink();
            }
            else if (sender.Key == VirtualKey.N && sender.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift) && length && !IsDefault(format))
            {
                ToggleRegular();
            }
        }

        #endregion

        public FormattedText GetFormattedText(bool clear = false)
        {
            OnGettingFormattedText();

            if (IsEmpty)
            {
                return new FormattedText(string.Empty, new TextEntity[0]);
            }

            Document.BatchDisplayUpdates();
            Document.GetText(TextGetOptions.None, out string value);

            value = value.TrimEnd('\v', '\r');

            var builder = new StringBuilder(value);
            var runs = new List<TextStyleRun>();

            var last = default(TextStyleRun);
            var type = default(TextEntityType);

            var hidden = 0;

            for (int i = 0; i < value.Length; i++)
            {
                var range = Document.GetRange(i, i + 1);
                var flags = default(TextStyle);

                if (string.Equals(range.CharacterFormat.Name, "Consolas", StringComparison.OrdinalIgnoreCase))
                {
                    flags = TextStyle.Monospace;
                }
                else
                {
                    if (range.CharacterFormat.Hidden == FormatEffect.On)
                    {
                        builder.Remove(i - hidden, 1);

                        hidden++;
                        continue;
                    }

                    if (range.CharacterFormat.Bold == FormatEffect.On)
                    {
                        flags |= TextStyle.Bold;
                    }
                    if (range.CharacterFormat.Italic == FormatEffect.On)
                    {
                        flags |= TextStyle.Italic;
                    }
                    if (range.CharacterFormat.Strikethrough == FormatEffect.On)
                    {
                        flags |= TextStyle.Strikethrough;
                    }
                    if (range.CharacterFormat.Underline == UnderlineType.Single)
                    {
                        flags |= TextStyle.Underline;
                    }
                    if (range.CharacterFormat.BackgroundColor == Colors.Gray)
                    {
                        flags |= TextStyle.Spoiler;
                    }

                    if (range.Link.Length > 0 && TryGetEntityType(range.Link, out type))
                    {
                        flags |= TextStyle.Url;
                        flags &= ~TextStyle.Underline;
                    }
                    else
                    {
                        type = null;
                    }
                }

                if (last != null && last.Flags == flags)
                {
                    last.End++;
                }
                else
                {
                    if (last != null)
                    {
                        runs.Add(last);
                        last = null;
                    }

                    if (flags != 0)
                    {
                        last = new TextStyleRun { Start = i - hidden, End = i - hidden + 1, Flags = flags, Type = type };
                        type = null;
                    }
                }
            }

            if (last != null)
            {
                runs.Add(last);
            }

            Document.GetText(TextGetOptions.NoHidden, out string text);

            if (clear)
            {
                // This is needed to prevent resize animation, doh
                if (ActualHeight > 48)
                {
                    _resetSize = true;
                    Height = 48;
                }

                Document.LoadFromStream(TextSetOptions.None, new InMemoryRandomAccessStream());
            }

            Document.ApplyDisplayUpdates();

            var entities = TextStyleRun.GetEntities(text, runs);

            text = text.Replace('\v', '\n').Replace('\r', '\n');
            return Client.Execute(new ParseMarkdown(new FormattedText(text, entities))) as FormattedText;
        }

        protected virtual void OnGettingFormattedText()
        {

        }

        protected virtual void OnSettingText()
        {

        }

        protected bool _wasEmpty;
        public virtual bool IsEmpty
        {
            get
            {
                var end = Document.GetRange(int.MaxValue, int.MaxValue);
                var empty = end.EndPosition == 0;

                if (empty && !_wasEmpty)
                {
                    Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                }

                _wasEmpty = empty;
                return empty;
            }
        }

        public bool CanPasteClipboardContent => Document.Selection.CanPaste(0);

        public void PasteFromClipboard()
        {
            Document.Selection.Paste(0);
        }

        public void SetText(FormattedText formattedText)
        {
            if (formattedText != null)
            {
                SetText(formattedText.Text, formattedText.Entities);
            }
            else if (!IsEmpty)
            {
                OnSettingText();
                Document.LoadFromStream(TextSetOptions.None, new InMemoryRandomAccessStream());
            }
        }

        public void SetText(string text, IList<TextEntity> entities)
        {
            OnSettingText();

            Document.BeginUndoGroup();
            Document.BatchDisplayUpdates();
            Document.LoadFromStream(TextSetOptions.None, new InMemoryRandomAccessStream());

            if (!string.IsNullOrEmpty(text))
            {
                Document.SetText(TextSetOptions.None, text);

                if (entities != null && entities.Count > 0)
                {
                    // We want to enumerate entities from last to first to not
                    // fuck up ranges due to hidden texts when formatting a link
                    foreach (var entity in entities.Reverse())
                    {
                        var range = Document.GetRange(entity.Offset, entity.Offset + entity.Length);

                        if (entity.Type is TextEntityTypeBold)
                        {
                            range.CharacterFormat.Bold = FormatEffect.On;
                        }
                        else if (entity.Type is TextEntityTypeItalic)
                        {
                            range.CharacterFormat.Italic = FormatEffect.On;
                        }
                        else if (entity.Type is TextEntityTypeUnderline)
                        {
                            range.CharacterFormat.Underline = UnderlineType.Single;
                        }
                        else if (entity.Type is TextEntityTypeStrikethrough)
                        {
                            range.CharacterFormat.Strikethrough = FormatEffect.On;
                        }
                        else if (entity.Type is TextEntityTypeSpoiler)
                        {
                            range.CharacterFormat.BackgroundColor = Colors.Gray;
                        }
                        else if (entity.Type is TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode)
                        {
                            range.CharacterFormat.Name = "Consolas";
                        }
                        else if (entity.Type is TextEntityTypeTextUrl textUrl)
                        {
                            range.Link = $"\"{textUrl.Url}\"";
                        }
                        else if (entity.Type is TextEntityTypeMentionName mentionName)
                        {
                            range.Link = $"\"tg-user://{mentionName.UserId}\"";
                        }
                    }
                }

                // We need to get full text as hidden content has been added and we don't want to hardcode lengths
                Document.GetText(TextGetOptions.None, out string result);
                Document.Selection.SetRange(result.Length, result.Length);
            }

            Document.ApplyDisplayUpdates();
            Document.EndUndoGroup();
        }

        public void InsertText(string text, IList<TextEntity> entities)
        {
            Document.BeginUndoGroup();
            Document.BatchDisplayUpdates();

            if (!string.IsNullOrEmpty(text))
            {
                var index = Math.Min(Document.Selection.StartPosition, Document.Selection.EndPosition);

                Document.Selection.SetText(TextSetOptions.None, text);

                if (entities != null && entities.Count > 0)
                {
                    // We want to enumerate entities from last to first to not
                    // fuck up ranges due to hidden texts when formatting a link
                    foreach (var entity in entities.Reverse())
                    {
                        var range = Document.GetRange(index + entity.Offset, index + entity.Offset + entity.Length);

                        if (entity.Type is TextEntityTypeBold)
                        {
                            range.CharacterFormat.Bold = FormatEffect.On;
                        }
                        else if (entity.Type is TextEntityTypeItalic)
                        {
                            range.CharacterFormat.Italic = FormatEffect.On;
                        }
                        else if (entity.Type is TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode)
                        {
                            range.CharacterFormat.Name = "Consolas";
                        }
                        else if (entity.Type is TextEntityTypeTextUrl textUrl)
                        {
                            range.Link = $"\"{textUrl.Url}\"";
                        }
                        else if (entity.Type is TextEntityTypeMentionName mentionName)
                        {
                            range.Link = $"\"tg-user://{mentionName.UserId}\"";
                        }
                    }
                }

                Document.Selection.SetRange(Document.Selection.EndPosition, Document.Selection.EndPosition);
            }

            Document.ApplyDisplayUpdates();
            Document.EndUndoGroup();
        }

        public void InsertText(string text, bool allowPreceding = false, bool allowTrailing = false)
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
    }
}
