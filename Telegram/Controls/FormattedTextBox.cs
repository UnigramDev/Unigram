//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;
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
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public class FormattedTextBox : RichEditBox
    {
        private readonly FormattedTextFlyout _selectionFlyout;
        private readonly MenuFlyoutSubItem _proofingFlyout;

        private bool _updateLocked;
        private bool _fromTextChanging;
        private bool _undoGroup;

        private int _selectionIndex;

        public CustomEmojiCanvas CustomEmoji { get; set; }
        private Grid Blocks;
        private ScrollViewer ContentElement;

        public FormattedTextBox()
        {
            DefaultStyleKey = typeof(FormattedTextBox);

            ClipboardCopyFormat = RichEditClipboardFormat.PlainText;

            Paste += OnPaste;
            PreviewKeyDown += OnPreviewKeyDown;

            _proofingFlyout = new MenuFlyoutSubItem();
            _proofingFlyout.Text = Strings.Spelling;

            SelectionFlyout = new Flyout
            {
                Content = _selectionFlyout = new FormattedTextFlyout(this),

                AllowFocusOnInteraction = false,
                ShouldConstrainToRootBounds = false,
                ShowMode = FlyoutShowMode.TransientWithDismissOnPointerMoveAway,
                FlyoutPresenterStyle = BootStrapper.Current.Resources["CommandFlyoutPresenterStyle"] as Style,
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

            TextChanging += OnTextChanging;
            TextChanged += OnTextChanged;

            SelectionChanged += OnSelectionChanged;
        }

        private void OnTextChanging(RichEditBox sender, RichEditBoxTextChangingEventArgs args)
        {
            _fromTextChanging = true;

            if (args.IsContentChanging)
            {
                UpdateFormat();
            }
            else if (_undoGroup)
            {
                _undoGroup = false;
                Document.EndUndoGroup();
            }
        }

        private void OnTextChanged(object sender, RoutedEventArgs e)
        {
            UpdateCustomEmoji();
            UpdateBlocks();
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            var index = Document.Selection.EndPosition;

            OnSelectionChanged(this, _fromTextChanging || _selectionIndex == index);

            _fromTextChanging = false;
            _selectionIndex = index;
        }

        protected virtual void OnSelectionChanged(RichEditBox sender, bool fromTextChanging)
        {

        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (double.IsNaN(Height))
            {
                return;
            }

            Height = double.NaN;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            CustomEmoji ??= GetTemplateChild(nameof(CustomEmoji)) as CustomEmojiCanvas;
            Blocks = GetTemplateChild(nameof(Blocks)) as Grid;
            ContentElement = GetTemplateChild(nameof(ContentElement)) as ScrollViewer;

            if (Blocks == null)
            {
                return;
            }

            ElementCompositionPreview.SetIsTranslationEnabled(Blocks, true);

            var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ContentElement);
            var visual = ElementCompositionPreview.GetElementVisual(Blocks);
            //var wrap = ElementCompositionPreview.GetElementVisual(Wrap);

            //wrap.Clip = visual.Compositor.CreateInsetClip();

            var translation = visual.Compositor.CreateExpressionAnimation("scrollViewer.Translation.Y");
            translation.SetReferenceParameter("scrollViewer", props);
            visual.StartAnimation("Translation.Y", translation);

            if (ContentElement.Content is FrameworkElement element)
            {
                element.SizeChanged += Element_SizeChanged;
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
            //if (_resetSize)
            //{
            //    _resetSize = false;
            //    Height = double.NaN;
            //}

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
                var alt = WindowContext.IsKeyDown(VirtualKey.Menu);
                var ctrl = WindowContext.IsKeyDown(VirtualKey.Control);
                var shift = WindowContext.IsKeyDown(VirtualKey.Shift);

                if (ctrl && shift && !alt)
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
                Text = Strings.Formatting,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.TextFont)
            };

            formatting.CreateFlyoutItem(length, ToggleQuote, Strings.Quote, Icons.QuoteBlock);
            formatting.CreateFlyoutItem(length, ToggleBold, Strings.Bold, Icons.TextBold, VirtualKey.B);
            formatting.CreateFlyoutItem(length, ToggleItalic, Strings.Italic, Icons.TextItalic, VirtualKey.I);
            formatting.CreateFlyoutItem(length, ToggleUnderline, Strings.Underline, Icons.TextUnderline, VirtualKey.U);
            formatting.CreateFlyoutItem(length, ToggleStrikethrough, Strings.Strike, Icons.TextStrikethrough, VirtualKey.X, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            formatting.CreateFlyoutItem(length && format.Name != "Consolas", ToggleMonospace, Strings.Mono, Icons.Code, VirtualKey.M, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            formatting.CreateFlyoutItem(length, ToggleSpoiler, Strings.Spoiler, Icons.TabInPrivate, VirtualKey.P, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
            formatting.CreateFlyoutSeparator();
            formatting.CreateFlyoutItem(!mention, CreateLink, clone.Link.Length > 0 ? Strings.EditLink : Strings.CreateLink, Icons.Link, VirtualKey.K);
            formatting.CreateFlyoutSeparator();
            formatting.CreateFlyoutItem(length && !IsDefault(format), ToggleRegular, Strings.Regular, null, VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

            flyout.CreateFlyoutItem(Document.CanUndo(), ContextUndo_Click, Strings.Undo, Icons.ArrowUndo, VirtualKey.Z);
            flyout.CreateFlyoutItem(Document.CanRedo(), ContextRedo_Click, Strings.Redo, Icons.ArrowRedo, VirtualKey.Y);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(length && Document.CanCopy(), ContextCut_Click, Strings.Cut, Icons.Cut, VirtualKey.X);
            flyout.CreateFlyoutItem(length && Document.CanCopy(), ContextCopy_Click, Strings.Copy, Icons.DocumentCopy, VirtualKey.C);
            flyout.CreateFlyoutItem(Document.CanPaste(), ContextPaste_Click, Strings.Paste, Icons.ClipboardPaste, VirtualKey.V);
            flyout.CreateFlyoutItem(length, ContextDelete_Click, Strings.Delete);
            flyout.CreateFlyoutSeparator();
            flyout.Items.Add(formatting);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(!IsEmpty, ContextSelectAll_Click, Strings.SelectAll, null, VirtualKey.A);

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

        public void ToggleQuote()
        {
            InsertBlockquote(Document.Selection);

            _selectionFlyout.Update(Document.Selection.CharacterFormat);
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

        public void ToggleSpoiler()
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

        public FrameworkElement CreateLinkTarget { get; set; }

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

            var popup = new CreateLinkPopup();
            popup.Text = text;
            popup.Link = range.Link.Trim('"');

            if (CreateLinkTarget != null)
            {
                popup.Target = CreateLinkTarget;
                popup.PreferredPlacement = Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.TopRight;
            }
            else
            {
                popup.Target = this;
                popup.PreferredPlacement = Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.Top;
            }

            popup.Width = popup.MinWidth = popup.MaxWidth = 314;
            popup.IsLightDismissEnabled = true;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm != true)
            {
                return;
            }

            Document.BatchDisplayUpdates();
            range.SetRange(start, end);
            range.CharacterFormat = Document.GetDefaultCharacterFormat();

            range.SetText(end > start ? TextSetOptions.Unlink : TextSetOptions.None, popup.Text);
            range.SetRange(start, start + popup.Text.Length);

            if (IsSafe(popup.Text))
            {
                range.Link = $"\"{popup.Link}\"";
            }
            else
            {
                range.Link = string.Empty;
            }

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
            return AreTheSame(format, Document.GetDefaultCharacterFormat());
        }

        protected bool AreTheSame(ITextCharacterFormat format, ITextCharacterFormat document)
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

            if (link.IsValidUrl())
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
                    var length = Math.Abs(Document.Selection.Length);

                    if (length > 0 && text.IsValidUrl())
                    {
                        Document.Selection.GetText(TextGetOptions.NoHidden, out string value);
                        InsertText(value, new[] { new TextEntity(0, value.Length, new TextEntityTypeTextUrl(text)) });
                    }
                    else
                    {
                        Document.Selection.SetText(TextSetOptions.None, text);
                        Document.Selection.SetRange(start + text.Length, start + text.Length);
                    }
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

            _updateLocked = true;

            Document.BatchDisplayUpdates();
            Document.GetText(TextGetOptions.None, out string value);

            value = value.TrimEnd('\v', '\r');

            var builder = new StringBuilder(value);

            List<TextStyleRun> runs = null;
            TextStyleRun last = null;
            TextEntityType type = default;

            var hidden = 0;

            for (int i = 0; i < value.Length; i++)
            {
                var range = Document.GetRange(i, i + 1);
                var flags = default(TextStyle);

                if (range.ParagraphFormat.SpaceAfter != 0)
                {
                    flags = TextStyle.Quote;
                }

                if (range.Text == "￼")
                {
                    range.GetText(TextGetOptions.UseObjectText, out string obj);

                    var split = obj.Split(';');
                    if (split.Length == 2 && long.TryParse(split[1], out long customEmojiId))
                    {
                        if (last != null)
                        {
                            runs ??= new();
                            runs.Add(last);
                            last = null;
                        }

                        runs ??= new();
                        runs.Add(new TextStyleRun { Start = i - hidden, End = i - hidden + split[0].Length, Flags = TextStyle.Emoji, Type = new TextEntityTypeCustomEmoji(customEmojiId) });
                        type = null;

                        builder.Remove(i - hidden, 1);
                        builder.Insert(i - hidden, split[0]);

                        hidden -= split[0].Length - "￼".Length;
                    }
                }
                else if (string.Equals(range.CharacterFormat.Name, "Consolas", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= TextStyle.Monospace;
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
                        runs ??= new();
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
                runs ??= new();
                runs.Add(last);
            }

            if (clear)
            {
                Document.Clear();
                SelectionFlyout.Hide();
            }

            Document.ApplyDisplayUpdates();

            _updateLocked = false;

            var text = builder.ToString();
            var entities = TextStyleRun.GetEntities(text, runs);

            text = text.Replace('\v', '\n').Replace('\r', '\n');
            return ClientEx.ParseMarkdown(text, entities);
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
            try
            {
                Document.Selection.Paste(0);
            }
            catch
            {
                // Seems to throw a UnauthorizedAccessException some times
                Logger.Error();
            }
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

                Document.Clear();
                SelectionFlyout.Hide();
            }
        }

        public async void SetText(string text, IList<TextEntity> entities)
        {
            try
            {
                _updateLocked = true;

                Document.BeginUndoGroup();
                Document.BatchDisplayUpdates();

                await SetTextImpl(text, entities);
            }
            catch
            {
                // TODO:
            }
            finally
            {
                Document.ApplyDisplayUpdates();
                Document.EndUndoGroup();

                _updateLocked = false;
            }
        }

        private async Task SetTextImpl(string text, IList<TextEntity> entities)
        {
            OnSettingText();
            Document.Clear();

            if (!string.IsNullOrEmpty(text))
            {
                Document.SetText(TextSetOptions.None, text);

                if (entities != null && entities.Count > 0)
                {
                    var hidden = 0;

                    // We want to enumerate entities from last to first to not
                    // fuck up ranges due to hidden texts when formatting a link
                    foreach (var entity in entities.Reverse())
                    {
                        var range = Document.GetRange(entity.Offset - hidden, entity.Offset - hidden + entity.Length);

                        if (entity.Type is TextEntityTypeBlockQuote)
                        {
                            InsertBlockquote(range, false);
                        }
                        else if (entity.Type is TextEntityTypeBold)
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
                        else if (entity.Type is TextEntityTypeTextUrl textUrl && IsSafe(text, entity))
                        {
                            range.Link = $"\"{textUrl.Url}\"";
                        }
                        else if (entity.Type is TextEntityTypeMentionName mentionName && IsSafe(text, entity))
                        {
                            range.Link = $"\"tg-user://{mentionName.UserId}\"";
                        }
                        else if (entity.Type is TextEntityTypeCustomEmoji customEmoji)
                        {
                            var emoji = text.Substring(entity.Offset, entity.Length);

                            range.SetText(TextSetOptions.None, string.Empty);
                            await InsertEmojiAsync(range, emoji, customEmoji.CustomEmojiId);

                            hidden += emoji.Length - 1;
                        }
                    }
                }

                Document.Selection.SetRange(TextConstants.MaxUnitCount, TextConstants.MaxUnitCount);
            }
        }

        public void InsertText(string text, IList<TextEntity> entities)
        {
            _updateLocked = true;

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
                        else if (entity.Type is TextEntityTypeTextUrl textUrl && IsSafe(text, entity))
                        {
                            range.Link = $"\"{textUrl.Url}\"";
                        }
                        else if (entity.Type is TextEntityTypeMentionName mentionName && IsSafe(text, entity))
                        {
                            range.Link = $"\"tg-user://{mentionName.UserId}\"";
                        }
                    }
                }

                Document.Selection.SetRange(Document.Selection.EndPosition, Document.Selection.EndPosition);
            }

            Document.ApplyDisplayUpdates();
            Document.EndUndoGroup();

            _updateLocked = false;
        }

        private static readonly char[] _unsafeChars = new[]
        {
            '\u0000', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007', '\u0008',
            '\u0009', '\u000a', '\u000b', '\u000c', '\u000d', '\u000e', '\u000f', '\u0010', '\u0011',
            '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017', '\u0018', '\u0019', '\u001a',
            '\u001b', '\u001c', '\u001d', '\u001e', '\u001f', '\u0020', '\u007f', '\u0080', '\u0081',
            '\u0082', '\u0083', '\u0084', '\u0085', '\u0086', '\u0087', '\u0088', '\u0089', '\u008a',
            '\u008b', '\u008c', '\u008d', '\u008e', '\u008f', '\u0090', '\u0091', '\u0092', '\u0093',
            '\u0094', '\u0095', '\u0096', '\u0097', '\u0098', '\u0099', '\u009a', '\u009b', '\u009c',
            '\u009d', '\u009e', '\u009f', '\u00a0', '\u0600', '\u0601', '\u0602', '\u0603', '\u06dd',
            '\u070f', '\u1680', '\u17b4', '\u17b5', '\u180e', '\u2000', '\u2001', '\u2002', '\u2003',
            '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200a', '\u200b', '\u200c',
            '\u200d', '\u200e', '\u200f', '\u2028', '\u2029', '\u202a', '\u202b', '\u202c', '\u202d',
            '\u202e', '\u202f', '\u205f', '\u2060', '\u2061', '\u2062', '\u2063', '\u2064', '\u206a',
            '\u206b', '\u206c', '\u206d', '\u206e', '\u206f', '\u3000', '\ufdd0', '\ufdd1', '\ufdd2',
            '\ufdd3', '\ufdd4', '\ufdd5', '\ufdd6', '\ufdd7', '\ufdd8', '\ufdd9', '\ufdda', '\ufddb',
            '\ufddc', '\ufddd', '\ufdde', '\ufddf', '\ufde0', '\ufde1', '\ufde2', '\ufde3', '\ufde4',
            '\ufde5', '\ufde6', '\ufde7', '\ufde8', '\ufde9', '\ufdea', '\ufdeb', '\ufdec', '\ufded',
            '\ufdee', '\ufdef', '\ufeff', '\ufff9', '\ufffa', '\ufffb', '\ufffc', '\ufffe'
        };

        private static bool IsSafe(string text, TextEntity entity = null)
        {
            if (entity != null)
            {
                text = text.Substring(entity.Offset, entity.Length);
            }

            text = text.Trim(_unsafeChars);
            return text.Length > 0;
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

            try
            {
                Document.Selection.SetText(TextSetOptions.None, block);
                Document.Selection.StartPosition = Document.Selection.EndPosition;
            }
            catch
            {
                // Seems to throw a UnauthorizedAccessException some times
                Logger.Error($"text: {text}");
            }
        }

        public async void InsertEmoji(Sticker sticker)
        {
            if (sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                await InsertEmojiAsync(Document.Selection, sticker.Emoji, customEmoji.CustomEmojiId);
                Document.Selection.StartPosition = Document.Selection.EndPosition + 1;
            }
        }

        public async Task InsertEmojiAsync(ITextRange range, string emoji, long customEmojiId)
        {
            var data = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
                0x42, 0x60, 0x82
                //0x42, 0x4D, 0x1E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1A, 0x00, 0x00, 0x00, 0x0C, 0x00,
                //0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00, 0xFF, 0x00
            };

            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(data.AsBuffer());
            await stream.FlushAsync();

            range.InsertImage(20, 18, 0, VerticalCharacterAlignment.Top, $"{emoji};{customEmojiId}", stream);
        }

        public void InsertBlockquote(string quote)
        {
            Document.Selection.SetText(TextSetOptions.None, quote);
            InsertBlockquote(Document.Selection);
        }

        public void InsertBlockquote(ITextRange textRange, bool batch = true)
        {
            var start = textRange.StartPosition;
            var end = textRange.EndPosition;

            if (batch)
            {
                Document.BeginUndoGroup();
                //Document.BatchDisplayUpdates();
            }

            var range = Document.GetRange(start, end);
            var moveStart = range.StartOf(TextRangeUnit.Paragraph, true);
            var moveEnd = range.EndOf(TextRangeUnit.Paragraph, true);

            if (moveEnd != 0 && moveEnd != 1)
            {
                range.SetRange(end, end);
                range.SetText(TextSetOptions.None, "\r");
            }
            else
            {
                range.SetRange(range.EndPosition - 1, range.EndPosition);
                range.Character = '\r';

                end -= (1 - moveEnd);
            }

            if (moveStart != 0 || (textRange.Length == 0 && moveStart != 0 && (moveEnd == 0 || moveEnd == 1)))
            {
                range.SetRange(start, start);
                range.SetText(TextSetOptions.None, "\r");

                if (moveStart != 0)
                {
                    start++;
                }

                end++;
            }
            else if (start > 0)
            {
                range.SetRange(start - 1, start);
                range.Character = '\r';
            }

            // Not sure about what's the logic exactly, but 14pt in XAML equals to 10.5pt in TOM.

            float magic = 0.75f;

            range.SetRange(start, Math.Max(start + 1, end));
            range.CharacterFormat.Size = 12 * magic;
            range.ParagraphFormat.SpaceBefore = 6 * magic;
            range.ParagraphFormat.SpaceAfter = 8 * magic;
            range.ParagraphFormat.SetIndents(0, 8 * magic, 24 * magic);

            MergeParagraphs(range);

            if (batch)
            {
                Document.Selection.SetRange(end, end);

                //Document.ApplyDisplayUpdates();
                Document.EndUndoGroup();
            }
        }

        private void MergeParagraphs(ITextRange range)
        {
            ITextRange searchRange = Document.GetRange(range.StartPosition + 1, range.EndPosition - 1);
            while (searchRange.FindText("\r", range.Length, FindOptions.None) > 0 && searchRange.EndPosition < range.EndPosition)
            {
                if (searchRange.StartPosition > range.StartPosition)
                {
                    searchRange.Character = '\v';
                }
            }
        }

        private void UpdateCustomEmoji()
        {
            if (_updateLocked)
            {
                return;
            }

            Document.GetText(TextGetOptions.None, out string value);

            HashSet<long> emoji = null;
            List<EmojiPosition> positions = null;

            var index = value.IndexOf('￼');

            while (index >= 0)
            {
                var range = Document.GetRange(index, index + 1);

                range.GetRect(PointOptions.ClientCoordinates, out Rect rect, out _);
                range.GetText(TextGetOptions.UseObjectText, out string obj);

                var split = obj.Split(';');
                if (split.Length == 2 && long.TryParse(split[1], out long customEmojiId))
                {
                    emoji ??= new();
                    emoji.Add(customEmojiId);

                    positions ??= new();
                    positions.Add(new EmojiPosition
                    {
                        CustomEmojiId = customEmojiId,
                        X = (int)rect.X + 1,
                        Y = (int)rect.Y
                    });
                }

                index = value.IndexOf('￼', index + 1);
            }

            if (CustomEmoji != null && DataContext is ViewModelBase viewModel)
            {
                CustomEmoji.UpdateEntities(viewModel.ClientService, positions);
            }
        }

        private void UpdateFormat()
        {
            var range = Document.GetRange(0, 0);

            do
            {
                if (range.MoveEnd(TextRangeUnit.CharacterFormat, 1) <= 0)
                {
                    break;
                }

                if (range.ParagraphFormat.SpaceAfter != 0 && range.CharacterFormat.Size != 9)
                {
                    _undoGroup = true;
                    Document.BeginUndoGroup();

                    range.CharacterFormat.Size = 9;
                }
                else if (range.ParagraphFormat.SpaceAfter == 0 && range.CharacterFormat.Size != 10.5f)
                {
                    _undoGroup = true;
                    Document.BeginUndoGroup();

                    range.CharacterFormat.Size = 10.5f;
                }
            } while (range.MoveStart(TextRangeUnit.CharacterFormat, 1) > 0);

            if (_undoGroup)
            {
                _undoGroup = false;
                Document.EndUndoGroup();
            }
        }

        private void UpdateBlocks()
        {
            if (Blocks == null)
            {
                return;
            }

            var range = Document.GetRange(0, 0);
            var rects = 0;

            do
            {
                if (range.MoveEnd(TextRangeUnit.HardParagraph, 1) <= 0)
                {
                    break;
                }

                if (range.StartPosition == 0)
                {
                    //ContentElement.Padding = new Thickness(48, range.ParagraphFormat.SpaceAfter == 0 ? 13 : 7, 0, 15);
                    ContentElement.Padding = new Thickness(48, 13, 0, 15);
                    ContentElement.Margin = new Thickness(0, range.ParagraphFormat.SpaceAfter == 0 ? 0 : -6, 0, 0);
                    Blocks.Margin = new Thickness(0, range.ParagraphFormat.SpaceAfter == 0 ? 0 : -6, 0, 0);
                }

                if (range.ParagraphFormat.SpaceAfter != 0)
                {
                    range.GetRect(PointOptions.None, out Rect rect, out _);
                    rects++;

                    if (Blocks.Children.Count < rects)
                    {
                        Blocks.Children.Add(CreateBlock(rect));
                    }
                    else if (Blocks.Children[rects - 1] is FrameworkElement block)
                    {
                        block.Margin = new Thickness(0, rect.Y + 2, 8, 0);
                        block.Height = rect.Height - 6;
                        block.Width = ActualWidth - 48;
                    }
                }
            } while (range.MoveStart(TextRangeUnit.HardParagraph, 1) > 0);

            for (int i = rects; i < Blocks.Children.Count; i++)
            {
                Blocks.Children.RemoveAt(i);
            }
        }

        private UIElement CreateBlock(Rect rect)
        {
            var field = new Grid
            {
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xe5, 0xf1, 0xff)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(-3, rect.Y + 4, 8, 0),
                Height = rect.Height - 4,
                Width = ActualWidth - 48,
                IsHitTestVisible = false
            };

            field.Children.Add(new TextBlock
            {
                Text = "\uE9B1",
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x7a, 0xff)),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4)
            });

            field.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x7a, 0xff)),
                BorderThickness = new Thickness(3, 0, 0, 0)
            });

            return new BlockQuote
            {
                Glyph = Icons.QuoteBlockFilled16,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, rect.Y + 4, 8, 0),
                Height = rect.Height - 4,
                Width = ActualWidth - 48,
                IsHitTestVisible = false
            };
        }

        private void Element_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Blocks.Height = e.NewSize.Height;
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                //e.Handled = true;
                //Document.Selection.SetText(TextSetOptions.None, "\v");
                //Document.Selection.SetRange(Document.Selection.StartPosition + 1, Document.Selection.StartPosition + 1);
            }
            else if (e.Key == Windows.System.VirtualKey.Down && Document.Selection.ParagraphFormat.SpaceAfter != 0)
            {
                var range = Document.Selection.GetClone();

                var start = range.MoveStart(TextRangeUnit.HardParagraph, 1);
                if (start == 0)
                {
                    e.Handled = true;

                    _undoGroup = true;
                    Document.BeginUndoGroup();

                    range.SetText(TextSetOptions.None, "\r");
                    range.SetRange(range.StartPosition + 1, range.StartPosition + 1);
                    range.ParagraphFormat = Document.GetDefaultParagraphFormat();
                    Document.Selection.SetRange(range.StartPosition + 1, range.StartPosition + 1);
                }
            }
            else if (e.Key == Windows.System.VirtualKey.Back)
            {
                var range = Document.Selection.GetClone();

                var start = range.StartOf(TextRangeUnit.Line, true);
                if (start == 0 && range.ParagraphFormat.SpaceAfter != 0)
                {
                    //_undoGroup = true;
                    //Document.BeginUndoGroup();

                    //range.EndOf(TextRangeUnit.Line, true);
                    //range.SetRange(range.EndPosition - 1, range.EndPosition);
                    //range.Character = '\r';
                    //return;

                    range.MoveStart(TextRangeUnit.Line, 1);

                    if (range.ParagraphFormat.SpaceAfter == 0)
                    {
                        if (range.Character == '\v')
                        {
                            _undoGroup = true;
                            Document.BeginUndoGroup();

                            range.Character = '\r';
                        }
                    }
                }

                var end = range.MoveEnd(TextRangeUnit.Line, -1);
                if (end == -1 && range.ParagraphFormat.SpaceAfter != 0)
                {
                    range.MoveStart(TextRangeUnit.Line, 1);

                    if (range.Character == '\v')
                    {
                        _undoGroup = true;
                        Document.BeginUndoGroup();

                        range.Character = '\r';
                    }
                }
            }
        }
    }
}
