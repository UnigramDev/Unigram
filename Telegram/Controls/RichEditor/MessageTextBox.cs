//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Text.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// A message's text on the native editor: what <see cref="FormattedTextBox"/> is over
    /// RichEdit, without RichEdit. The engine runs in a <see cref="RichSchema"/> built from
    /// <see cref="AllowedEntities"/>, so a heading or a list cannot be made in it, and the text
    /// goes in and out through <see cref="RichFormattedTextConverter"/>.
    /// </summary>
    public sealed partial class MessageTextBox : Control, IFormattedTextInput
    {
        private readonly RichEditorEngine _engine = new();

        private RichEditorView Editor;
        private TextBlock InlinePlaceholder;

        private IClientService _clientService;
        private ISettingsService _settings;
        private long _chatId;

        private FormattedTextEntity _allowedEntities = FormattedTextEntity.All;

        // Custom emoji picked out of a pack, as FormattedTextBox keeps them: intersected with the
        // text on the way out rather than pruned as the user edits.
        private HashSet<long> _insertedCustomEmojiIds;

        private bool _trimPending;
        private bool _emoticonPending;

        // The leaf and length the last change left, so a change of one more character in the
        // same leaf can be told apart as typing: an emoticon is replaced as it is typed, not when
        // a deletion or a paste happens to leave one before the caret.
        private RichTextLeaf _lastLeaf;
        private int _lastLength;
        private bool _focused;
        private bool _pointerOver;

        public MessageTextBox()
        {
            DefaultStyleKey = typeof(MessageTextBox);

            _engine.Schema = ToSchema(_allowedEntities);
            _engine.Load(new RichDocument());

            // The engine is this control's own, so the subscription lives and dies with both.
            _engine.Changed += OnEngineChanged;

            Loaded += OnLoaded;
            IsEnabledChanged += OnIsEnabledChanged;
        }

        // A checklist disables the box of a task that is locked, on a container it recycles.
        private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateVisualState(false);
        }

        public RichEditorEngine Engine => _engine;

        /// <summary>
        /// What custom emoji are drawn with. Found through the window's session when it is not
        /// given, which is enough for a box that is not in a chat.
        /// </summary>
        public IClientService ClientService
        {
            get => _clientService;
            set
            {
                if (_clientService != value)
                {
                    _clientService = value;
                    Editor?.Initialize(value, _engine);
                }
            }
        }

        /// <summary>The chat the text is for, which custom emoji suggestions are found for.</summary>
        public long ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    UpdateSuggestions();
                }
            }
        }

        // Offered once there is a session to keep the recent emoji in.
        private void UpdateSuggestions()
        {
            if (Editor != null)
            {
                Editor.Suggestions = _settings != null ? new RichSuggestionContext(_settings, _chatId) : null;
            }
        }

        protected override void OnApplyTemplate()
        {
            if (Editor != null)
            {
                Unhook(Editor);

                if (_controlledList != null)
                {
                    AutomationProperties.GetControlledPeers(Editor).Remove(_controlledList);
                }
            }

            Editor = GetTemplateChild(nameof(Editor)) as RichEditorView;
            InlinePlaceholder = GetTemplateChild(nameof(InlinePlaceholder)) as TextBlock;

            if (Editor != null)
            {
                Editor.PlaceholderText = PlaceholderText ?? string.Empty;
                Editor.IsEnabled = !IsReadOnly;
                Editor.TextInputScope = _textInputScope;
                Editor.PreventKeyboardDisplayOnProgrammaticFocus = _preventKeyboardDisplayOnProgrammaticFocus;

                // The editor is what UI Automation sees, so it answers to the box's name.
                var automationId = AutomationProperties.GetAutomationId(this);
                AutomationProperties.SetAutomationId(Editor, string.IsNullOrEmpty(automationId) ? Name : automationId);

                if (_controlledList != null)
                {
                    AutomationProperties.GetControlledPeers(Editor).Add(_controlledList);
                }

                // Not left to Loaded, which comes after the first render: a custom emoji in text
                // set before then needs the service to draw.
                if (_clientService == null && WindowContext.GetNavigationService(this)?.Session is ISession session)
                {
                    _clientService = session.Resolve<IClientService>();
                    _settings ??= session.Settings;
                }

                Editor.Initialize(_clientService, _engine);
                Hook(Editor);
                UpdateSuggestions();
            }

            UpdateVisualState(false);
            base.OnApplyTemplate();
        }

        private void Hook(RichEditorView editor)
        {
            editor.PreviewKeyDown += OnEditorPreviewKeyDown;
            editor.MenuRequested += OnMenuRequested;
            editor.LinkRequested += OnLinkRequested;
            editor.LanguageRequested += OnLanguageRequested;
            editor.PasteRequested += OnPasteRequested;
            editor.GotFocus += OnEditorGotFocus;
            editor.LostFocus += OnEditorLostFocus;
            editor.PointerEntered += OnEditorPointerEntered;
            editor.PointerExited += OnEditorPointerExited;
        }

        private void Unhook(RichEditorView editor)
        {
            editor.PreviewKeyDown -= OnEditorPreviewKeyDown;
            editor.MenuRequested -= OnMenuRequested;
            editor.LinkRequested -= OnLinkRequested;
            editor.LanguageRequested -= OnLanguageRequested;
            editor.PasteRequested -= OnPasteRequested;
            editor.GotFocus -= OnEditorGotFocus;
            editor.LostFocus -= OnEditorLostFocus;
            editor.PointerEntered -= OnEditorPointerEntered;
            editor.PointerExited -= OnEditorPointerExited;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (WindowContext.GetNavigationService(this)?.Session is ISession session)
            {
                if (_clientService == null)
                {
                    ClientService = session.Resolve<IClientService>();
                }

                if (_settings == null)
                {
                    _settings = session.Settings;
                    UpdateSuggestions();
                }
            }
        }

        // =====================================================================================
        // States
        // =====================================================================================

        private void OnEditorGotFocus(object sender, RoutedEventArgs e)
        {
            _focused = true;
            UpdateVisualState(true);
        }

        private void OnEditorLostFocus(object sender, RoutedEventArgs e)
        {
            _focused = false;
            UpdateVisualState(true);
        }

        private void OnEditorPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _pointerOver = true;
            UpdateVisualState(true);
        }

        private void OnEditorPointerExited(object sender, PointerRoutedEventArgs e)
        {
            _pointerOver = false;
            UpdateVisualState(true);
        }

        private void UpdateVisualState(bool useTransitions)
        {
            var state = !IsEnabled ? "Disabled"
                : _focused ? "Focused"
                : _pointerOver ? "PointerOver"
                : "Normal";

            VisualStateManager.GoToState(this, state, useTransitions);
        }

        // =====================================================================================
        // Text
        // =====================================================================================

        // The document as a message, converted once for as long as it stays the same: the
        // composer and the chat each ask for it on every keystroke, and converting is a copy of
        // the whole text each time. Keyed on the document rather than dropped on a change
        // event, which a batch holds back until it ends while its own code may read the text.
        // A leaf's version only grows, so their sum moves with any edit to any of them.
        private FormattedText _converted;
        private RichDocument _convertedDocument;
        private int _convertedStructure;
        private long _convertedLeaves;

        private FormattedText Converted()
        {
            var document = _engine.Document;
            var leaves = document.Leaves;
            var versions = 0L;

            for (int i = 0; i < leaves.Count; i++)
            {
                versions += leaves[i].Version;
            }

            if (_converted == null || !ReferenceEquals(_convertedDocument, document) || _convertedStructure != document.Version || _convertedLeaves != versions)
            {
                _converted = RichFormattedTextConverter.ToFormattedText(document);
                _convertedDocument = document;
                _convertedStructure = document.Version;
                _convertedLeaves = versions;
            }

            return _converted;
        }

        public string Text => Converted().Text;

        public bool IsEmpty => _engine.Document.IsEmpty;

        public FormattedText GetFormattedText(bool clear = false, bool parseMarkdown = true)
        {
            // A vector of its own, as callers adjust what they are given; the text is a string,
            // and shared.
            var converted = Converted();
            var text = new FormattedText(converted.Text, new MutableVector<TextEntity>(converted.Entities));

            if (clear)
            {
                ClearText();
            }

            return parseMarkdown
                ? ClientEx.ParseMarkdown(text)
                : text;
        }

        public PreparedText GetPreparedText(bool clear = false, bool parseMarkdown = true)
        {
            var text = GetFormattedText(clear, parseMarkdown);
            return new PreparedText(text, ContainsInsertedCustomEmoji(text.Entities));
        }

        private bool ContainsInsertedCustomEmoji(Vector<TextEntity> entities)
        {
            if (_insertedCustomEmojiIds == null || entities == null)
            {
                return false;
            }

            foreach (var entity in entities)
            {
                if (entity.Type is TextEntityTypeCustomEmoji customEmoji && _insertedCustomEmojiIds.Contains(customEmoji.CustomEmojiId))
                {
                    return true;
                }
            }

            return false;
        }

        public void SetText(FormattedText text)
        {
            if (MaxLength > 0 && text != null && text.Text?.Length > MaxLength)
            {
                (var trimmed, var entities) = text.Text.Substring(text.Entities, 0, MaxLength);
                text = new FormattedText(trimmed, entities);
            }

            // A page that keeps the text in its view model writes it back on every change, often
            // as GetFormattedText gave it, markdown parsed. Loading what is already there would
            // move the caret and end a composition.
            if (string.IsNullOrEmpty(text?.Text) ? _engine.Document.IsEmpty : IsCurrentText(text))
            {
                return;
            }

            if (TryDeferLoad(text))
            {
                return;
            }

            _engine.Load(RichFormattedTextConverter.FromFormattedText(text, _engine.Schema));
            _engine.MoveToDocumentEdge(1, false);
        }

        private bool IsCurrentText(FormattedText text)
        {
            var current = RichFormattedTextConverter.ToFormattedText(_engine.Document);
            // Markdown parsed is never longer, so this is not the text either way.
            if (current.Text.Length < text.Text.Length)
            {
                return false;
            }

            return IsSameText(current, text) || IsSameText(ClientEx.ParseMarkdown(current), text);
        }

        // True only when sure: a difference this cannot see, a date's format, counts as one.
        private static bool IsSameText(FormattedText x, FormattedText y)
        {
            if (x.Text != y.Text || (x.Entities?.Count ?? 0) != (y.Entities?.Count ?? 0))
            {
                return false;
            }

            for (int i = 0; i < (x.Entities?.Count ?? 0); i++)
            {
                var a = x.Entities[i];
                var b = y.Entities[i];

                if (a.Offset != b.Offset || a.Length != b.Length || a.Type.GetType() != b.Type.GetType())
                {
                    return false;
                }

                var same = a.Type switch
                {
                    TextEntityTypeCustomEmoji customEmoji => customEmoji.CustomEmojiId == ((TextEntityTypeCustomEmoji)b.Type).CustomEmojiId,
                    TextEntityTypeTextUrl textUrl => textUrl.Url == ((TextEntityTypeTextUrl)b.Type).Url,
                    TextEntityTypeMentionName mention => mention.UserId == ((TextEntityTypeMentionName)b.Type).UserId,
                    TextEntityTypePreCode preCode => preCode.Language == ((TextEntityTypePreCode)b.Type).Language,
                    TextEntityTypeMediaTimestamp timestamp => timestamp.MediaTimestamp == ((TextEntityTypeMediaTimestamp)b.Type).MediaTimestamp,
                    TextEntityTypeDateTime => ReferenceEquals(a.Type, b.Type),
                    _ => true
                };

                if (!same)
                {
                    return false;
                }
            }

            return true;
        }

        public void ClearText()
        {
            _insertedCustomEmojiIds = null;

            if (!TryDeferLoad(null))
            {
                _engine.Load(new RichDocument());
            }
        }

        // ContentChanged is raised from inside the engine's change, which often runs inside the
        // input service's own callback. A handler that loads new text there, as the checklist
        // does to turn what was typed into a task, would put the service out of step, so the
        // load waits for the next dispatcher turn.
        private int _raisingContentChanged;
        private bool _loadPending;
        private FormattedText _pendingText;

        private bool TryDeferLoad(FormattedText text)
        {
            if (_raisingContentChanged == 0)
            {
                return false;
            }

            _pendingText = text;
            _loadPending = _loadPending || (DispatcherQueue.GetForCurrentThread()?.TryEnqueue(LoadPending) ?? false);
            return _loadPending;
        }

        private void LoadPending()
        {
            var text = _pendingText;

            _loadPending = false;
            _pendingText = null;

            if (string.IsNullOrEmpty(text?.Text))
            {
                _engine.Load(new RichDocument());
            }
            else
            {
                _engine.Load(RichFormattedTextConverter.FromFormattedText(text, _engine.Schema));
                _engine.MoveToDocumentEdge(1, false);
            }
        }

        public void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (Editor != null)
            {
                Editor.InsertPlainText(text);
            }
            else
            {
                _engine.InsertText(text.Replace("\r\n", "\n").Replace('\r', '\n'));
            }
        }

        public void InsertEmoji(Sticker sticker, bool track = false)
        {
            if (sticker?.FullType is not StickerFullTypeCustomEmoji customEmoji || !_engine.Schema.AllowsCustomEmoji)
            {
                return;
            }

            if (track)
            {
                _insertedCustomEmojiIds ??= new HashSet<long>();
                _insertedCustomEmojiIds.Add(customEmoji.CustomEmojiId);
            }

            _engine.InsertObject(RichInlineObject.FromCustomEmoji(customEmoji.CustomEmojiId, sticker.Emoji));
        }

        public void InsertFormattedText(FormattedText text)
        {
            if (string.IsNullOrEmpty(text?.Text))
            {
                return;
            }

            _engine.InsertFragment(RichFormattedTextConverter.FromFormattedText(text, _engine.Schema));
        }

        public void InsertMention(string text, long userId)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            InsertFormattedText(new FormattedText(text + " ", new[] { new TextEntity(0, text.Length, new TextEntityTypeMentionName(userId)) }));
        }

        public void PasteFromClipboard() => PasteFormatted();

        public bool FocusInput(FocusState state) => Editor?.Focus(state) ?? false;

        /// <summary>The editor, which is what takes focus: this control is only its frame.</summary>
        public DependencyObject FocusTarget => Editor;

        public FocusState InputFocusState => Editor?.FocusState ?? FocusState.Unfocused;

        // =====================================================================================
        // The caret
        // =====================================================================================

        public bool IsSelectionEmpty => _engine.IsSelectionEmpty && _engine.SelectedNode == null;

        public int CaretOffset => RichFormattedTextConverter.GetOffset(_engine.Document, _engine.End);

        public void MoveCaretToEnd() => _engine.MoveToDocumentEdge(1, false);

        public bool IsCaretAtStart => IsSelectionEmpty && _engine.Focus.Offset == 0 && _engine.Focus.Leaf.Index == 0;

        public bool IsCaretAtEnd
        {
            get
            {
                var focus = _engine.Focus;
                return IsSelectionEmpty && focus.Offset == focus.Leaf.Length && focus.Leaf.Index == _engine.Document.Leaves.Count - 1;
            }
        }

        public void SplitAtSelection(out FormattedText before, out FormattedText after)
        {
            var text = RichFormattedTextConverter.ToFormattedText(_engine.Document);
            var start = RichFormattedTextConverter.GetOffset(_engine.Document, _engine.Start);
            var end = RichFormattedTextConverter.GetOffset(_engine.Document, _engine.End);

            (var head, var headEntities) = text.Text.Substring(text.Entities, 0, start);
            (var tail, var tailEntities) = text.Text.Substring(text.Entities, end, text.Text.Length - end);

            before = ClientEx.ParseMarkdown(new FormattedText(head, headEntities));
            after = ClientEx.ParseMarkdown(new FormattedText(tail, tailEntities));
        }

        // The query is in the caret's line, so where it starts is an offset in that line.
        public bool TryGetQuery(out AutocompleteEntity entity, out string query, out int start)
        {
            var focus = _engine.Focus;
            if (!IsSelectionEmpty || focus.Leaf.IsPlain)
            {
                entity = AutocompleteEntity.None;
                query = string.Empty;
                start = -1;
                return false;
            }

            return AutocompleteEntityFinder.TrySearch(focus.Leaf.Chars, focus.Offset, RichTextLeaf.ObjectChar, out entity, out query, out start);
        }

        // An ordinary replacement, not a substitution: Backspace after a mention or a command
        // deletes a character, as typing it would have.
        public void ReplaceQuery(int start, FormattedText text)
        {
            var focus = _engine.Focus;
            if (!IsSelectionEmpty || start < 0 || start > focus.Offset)
            {
                return;
            }

            _engine.Select(new RichPosition(focus.Leaf, start), focus);

            if (string.IsNullOrEmpty(text?.Text))
            {
                _engine.DeleteSelection();
            }
            else
            {
                InsertFormattedText(text);
            }
        }

        public bool HasOpenSuggestions => Editor?.IsSuggesting ?? false;

        public void SetInlinePlaceholder(string prefix, string hint)
        {
            if (InlinePlaceholder == null)
            {
                return;
            }

            // Drawn over the text, its prefix in no colour so that the hint starts where the text
            // ends, as the old box does it.
            InlinePlaceholder.Inlines.Clear();

            if (prefix != null && hint != null)
            {
                InlinePlaceholder.Inlines.Add(new Run { Text = prefix, Foreground = null });
                InlinePlaceholder.Inlines.Add(new Run { Text = hint });
            }
        }

        public event EventHandler ContentChanged;

        public event EventHandler SelectionChanged;

        public event EventHandler Submitted;

        private void OnEngineChanged(object sender, RichEditorChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);

            if ((e.Changes & (RichEditorChanges.Text | RichEditorChanges.Structure)) == 0)
            {
                return;
            }

            // Not trimmed here: this runs before the view has seen the change, and often inside
            // the input service's own callback, which an edit of ours would put out of step.
            if (MaxLength > 0 && !_trimPending && RichFormattedTextConverter.GetLength(_engine.Document) > MaxLength)
            {
                _trimPending = DispatcherQueue.GetForCurrentThread()?.TryEnqueue(TrimToMaxLength) ?? false;
            }

            // Typing, not Backspace putting back an emoticon whose emoji was one character short.
            var typed = _engine.LastEdit == RichEditKind.Typing && (e.Changes & RichEditorChanges.Structure) == 0
                && e.Leaf != null && ReferenceEquals(e.Leaf, _lastLeaf) && e.Leaf.Length == _lastLength + 1;

            _lastLeaf = e.Leaf;
            _lastLength = e.Leaf?.Length ?? 0;

            if (typed && IsReplaceEmojiEnabled && !_emoticonPending && EndsWithEmoticonKey())
            {
                _emoticonPending = DispatcherQueue.GetForCurrentThread()?.TryEnqueue(ReplaceEmoticon) ?? false;
            }

            _raisingContentChanged++;

            try
            {
                ContentChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _raisingContentChanged--;
            }
        }

        // Emoticons are keyed by their last character, and nearly no keystroke is one, so this is
        // all a keystroke costs.
        private bool EndsWithEmoticonKey()
        {
            var focus = _engine.Focus;
            return _engine.IsSelectionEmpty && focus.Offset > 0 && !focus.Leaf.IsPlain
                && Emoticon.Matches.ContainsKey(focus.Leaf[focus.Offset - 1]);
        }

        /// <summary>The emoticon just typed, as the emoji it stands for. Not in code, as the old box did not.</summary>
        private void ReplaceEmoticon()
        {
            _emoticonPending = false;

            var focus = _engine.Focus;
            var leaf = focus.Leaf;
            var offset = focus.Offset;

            if (!_engine.IsSelectionEmpty || offset == 0 || leaf.IsPlain || !Emoticon.Matches.TryGetValue(leaf[offset - 1], out string[] matches))
            {
                return;
            }

            if ((leaf.Runs[leaf.FindRun(offset - 1, out _)].Marks & RichMarks.Monospace) != 0)
            {
                return;
            }

            foreach (var match in matches)
            {
                var start = offset - match.Length;
                if (start >= 0 && leaf.Chars.Slice(start, match.Length).SequenceEqual(match.AsSpan()))
                {
                    _engine.Substitute(start, Emoticon.Data[match]);
                    return;
                }
            }
        }

        /// <summary>
        /// What went past <see cref="MaxLength"/> is taken off before the caret, where it was
        /// typed or pasted. The limit counts what is sent; RichEdit counted its hidden link and
        /// emoji payloads too, so this box takes slightly more than the old one did.
        /// </summary>
        private void TrimToMaxLength()
        {
            _trimPending = false;

            var maxLength = MaxLength;
            if (maxLength <= 0)
            {
                return;
            }

            var excess = RichFormattedTextConverter.GetLength(_engine.Document) - maxLength;
            var focus = _engine.Focus;

            if (excess <= 0 || !_engine.IsSelectionEmpty || focus.Offset < excess)
            {
                return;
            }

            _engine.Select(new RichPosition(focus.Leaf, focus.Offset - excess), focus);
            _engine.DeleteSelection();
        }

        // =====================================================================================
        // Keys
        // =====================================================================================

        // The box's own keys, before the editor's: Enter is sending here, not a new paragraph.
        private void OnEditorPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var ctrl = WindowContext.IsKeyDownForEvent(VirtualKey.Control);
            var shift = WindowContext.IsKeyDownForEvent(VirtualKey.Shift);
            var alt = WindowContext.IsKeyDownForEvent(VirtualKey.Menu);

            // Enter and Tab pick a suggestion while there is one, before they send.
            if (Editor.ProcessSuggestionKey(e.Key, ctrl, shift, alt))
            {
                e.Handled = true;
                return;
            }

            switch (e.Key)
            {
                case VirtualKey.Enter when !alt:
                    {
                        var send = AppSettings.IsSendByEnterEnabled
                            ? !ctrl && !shift
                            : ctrl && !shift;

                        if (send && IsSubmitEnabled)
                        {
                            Submitted?.Invoke(this, EventArgs.Empty);
                        }
                        else if (AcceptsReturn || send)
                        {
                            // A paragraph rather than a line break: either is one newline in
                            // the message, and a paragraph is what a quote or code ends on.
                            _engine.InsertParagraph();
                        }

                        e.Handled = true;
                        break;
                    }

                case VirtualKey.V when ctrl && !shift && !alt:
                case VirtualKey.Insert when shift && !ctrl && !alt:
                    PasteFormatted();
                    e.Handled = true;
                    break;

                case VirtualKey.V when ctrl && shift && !alt:
                    Editor?.Paste(true);
                    e.Handled = true;
                    break;

                // Ctrl+Shift+. quotes, as in the old box.
                case (VirtualKey)190 when ctrl && shift && !alt:
                    _engine.ToggleBlockQuote();
                    e.Handled = true;
                    break;

                // Alt+X turns the four hex digits selected into their character.
                case VirtualKey.X when alt && !ctrl && !shift:
                    {
                        var hex = _engine.GetSelectedText();
                        if (hex?.Length == 4 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int result))
                        {
                            _engine.InsertText(new string((char)result, 1));
                            e.Handled = true;
                        }
                        break;
                    }
            }
        }

        // =====================================================================================
        // Menus
        // =====================================================================================

        private async void OnMenuRequested(object sender, RichMenuRequestedEventArgs e)
        {
            if (IsReadOnly)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (Editor.TryGetMisspelling(out string misspelled))
            {
                foreach (var suggestion in await Editor.SuggestAsync(misspelled))
                {
                    flyout.CreateFlyoutItem(() => Editor.ReplaceMisspelling(suggestion), suggestion);
                }

                flyout.CreateFlyoutItem(Editor.AddToDictionary, Strings.SpellingAddToDictionary, Icons.Add);
                flyout.CreateFlyoutItem(Editor.IgnoreMisspelling, Strings.SpellingIgnoreWord);
                flyout.CreateFlyoutSeparator();
            }

            var status = _engine.GetStatus();
            var clipboard = ClipboardEx.TryGetContent();
            var canPaste = clipboard != null && (clipboard.Contains(StandardDataFormats.Text)
                || (PasteRequested != null && (clipboard.Contains(StandardDataFormats.Bitmap) || clipboard.Contains(StandardDataFormats.StorageItems))));

            flyout.CreateFlyoutItem(status.CanUndo, _engine.Undo, Strings.TextUndo, Icons.ArrowUndo, VirtualKey.Z);
            flyout.CreateFlyoutItem(status.CanRedo, _engine.Redo, Strings.Redo, Icons.ArrowRedo, VirtualKey.Y);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(status.HasTextSelection, Editor.Cut, Strings.Cut, Icons.Cut, VirtualKey.X);
            flyout.CreateFlyoutItem(status.HasTextSelection, Editor.Copy, Strings.Copy, Icons.Copy, VirtualKey.C);
            flyout.CreateFlyoutItem(canPaste, PasteFormatted, Strings.Paste, Icons.ClipboardPaste, VirtualKey.V);
            flyout.CreateFlyoutItem(!status.IsSelectionEmpty, _engine.DeleteSelection, Strings.Delete);
            flyout.CreateFlyoutSeparator();

            var schema = _engine.Schema;
            if (schema.Marks != RichMarks.None || schema.AllowsQuotes || schema.AllowsUrls)
            {
                var formatting = new MenuFlyoutSubItem
                {
                    Text = Strings.Formatting,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextFont)
                };

                var length = status.HasTextSelection;

                if (schema.AllowsMark(RichMarks.Bold))
                {
                    formatting.CreateFlyoutItem(length, ToggleBold, Strings.Bold, Icons.TextBold, VirtualKey.B);
                }

                if (schema.AllowsMark(RichMarks.Italic))
                {
                    formatting.CreateFlyoutItem(length, ToggleItalic, Strings.Italic, Icons.TextItalic, VirtualKey.I);
                }

                if (schema.AllowsMark(RichMarks.Underline))
                {
                    formatting.CreateFlyoutItem(length, ToggleUnderline, Strings.Underline, Icons.TextUnderline, VirtualKey.U);
                }

                if (schema.AllowsMark(RichMarks.Strikethrough))
                {
                    formatting.CreateFlyoutItem(length, ToggleStrikethrough, Strings.Strike, Icons.TextStrikethrough, VirtualKey.X, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
                }

                if (schema.AllowsQuotes)
                {
                    formatting.CreateFlyoutItem(length || status.InBlockQuote, _engine.ToggleBlockQuote, Strings.Quote, Icons.QuoteBlock, (VirtualKey)190, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
                }

                if (schema.AllowsMark(RichMarks.Monospace))
                {
                    formatting.CreateFlyoutItem(length, ToggleMonospace, Strings.Mono, Icons.Code, VirtualKey.M, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
                }

                if (schema.AllowsMark(RichMarks.Spoiler))
                {
                    formatting.CreateFlyoutItem(length, ToggleSpoiler, Strings.Spoiler, Icons.Spoiler, VirtualKey.P, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
                }

                if (schema.AllowsDates)
                {
                    formatting.CreateFlyoutItem(length && !status.HasLink, CreateDate, Strings.FormattedDate, Icons.Calendar);
                }

                if (schema.AllowsUrls)
                {
                    formatting.CreateFlyoutSeparator();
                    formatting.CreateFlyoutItem(!status.HasDate, CreateLink, status.HasLink ? Strings.EditLink : Strings.CreateLink, Icons.Link, VirtualKey.K);
                }

                formatting.CreateFlyoutSeparator();
                formatting.CreateFlyoutItem(length && (status.Marks != RichMarks.None || status.HasLink), _engine.ClearFormatting, Strings.Regular, null, VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

                flyout.Items.Add(formatting);
            }

            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(!status.IsDocumentEmpty, _engine.SelectAll, Strings.SelectAll, null, VirtualKey.A);

            Editor.ShowMenu(flyout, e.Position, e.IsTouch, e.IsKeyboard);
        }

        private void ToggleBold() => _engine.ToggleMark(RichMarks.Bold);

        private void ToggleItalic() => _engine.ToggleMark(RichMarks.Italic);

        private void ToggleUnderline() => _engine.ToggleMark(RichMarks.Underline);

        private void ToggleStrikethrough() => _engine.ToggleMark(RichMarks.Strikethrough);

        private void ToggleMonospace() => _engine.ToggleMark(RichMarks.Monospace);

        private void ToggleSpoiler() => _engine.ToggleMark(RichMarks.Spoiler);

        private async void PasteFormatted()
        {
            if (await TryLinkSelectionAsync() || RaiseRichPaste())
            {
                return;
            }

            Editor?.Paste(false);
        }

        /// <summary>
        /// A URL pasted over a selection labels it instead of replacing it, as the old box does,
        /// unless the selection is a URL itself or already a link: that is replacing the link.
        /// </summary>
        private async Task<bool> TryLinkSelectionAsync()
        {
            if (!_engine.Schema.AllowsUrls || _engine.IsSelectionEmpty || _engine.SelectedNode != null || _engine.GetSelectionLink() != null)
            {
                return false;
            }

            var selected = _engine.GetSelectedText();
            if (string.IsNullOrWhiteSpace(selected) || selected.IsValidUrl())
            {
                return false;
            }

            var content = ClipboardEx.TryGetContent();
            if (content == null || !content.Contains(StandardDataFormats.Text) || content.Contains("application/x-tl-field-tags"))
            {
                return false;
            }

            try
            {
                var text = await content.GetTextAsync();
                if (text == null || !text.Trim().IsValidUrl() || selected != _engine.GetSelectedText())
                {
                    return false;
                }

                _engine.SetLink(RichLink.FromUrl(text.Trim()));
                _engine.SetCaret(_engine.End);
                return true;
            }
            catch
            {
                // The clipboard changed, or its source went away, while being read.
                return false;
            }
        }

        // Files and pictures are not text: whoever hosts the box decides what they become, as the
        // composer's paste handler does for the old box.
        private void OnPasteRequested(object sender, RichPasteRequestedEventArgs e)
        {
            PasteRequested?.Invoke(this, new FormattedTextPasteEventArgs(e.Content));
        }

        // HTML goes to the host first, which the composer offers to open in the rich editor:
        // anything but a message's own text, unless that came out of a rich message.
        private bool RaiseRichPaste()
        {
            var handler = PasteRequested;
            var content = handler != null ? ClipboardEx.TryGetContent() : null;

            if (content == null || !content.Contains(StandardDataFormats.Html) || (content.Contains("application/x-tl-field-tags") && !content.Contains(RichClipboard.FormatId)))
            {
                return false;
            }

            var args = new FormattedTextPasteEventArgs(content);
            handler(this, args);
            return args.Handled;
        }

        /// <summary>Raised for what the clipboard or a drop holds that is not text (files, bitmaps), and for HTML pasted with Ctrl+V.</summary>
        public event EventHandler<FormattedTextPasteEventArgs> PasteRequested;

        private void OnLinkRequested(object sender, EventArgs e)
        {
            if (_engine.Schema.AllowsUrls)
            {
                CreateLink();
            }
        }

        private async void OnLanguageRequested(object sender, RichLanguageRequestedEventArgs e)
        {
            var language = await CodeLanguagePopup.ChooseAsync(XamlRoot, e.Block.Language);
            if (language != null)
            {
                _engine.SetLanguage(e.Block, language);
            }

            Editor?.Focus(FocusState.Programmatic);
        }

        private async void CreateLink()
        {
            var selected = _engine.GetSelectedText();
            var existing = _engine.GetSelectionLink();

            var popup = new CreateLinkPopup
            {
                Text = selected,
                Link = existing?.Kind == RichLinkKind.Url ? existing.Url : string.Empty,
                IsLightDismissEnabled = true
            };

            if (string.IsNullOrEmpty(popup.Link))
            {
                var clipboard = ClipboardEx.TryGetContent();
                if (clipboard != null && clipboard.Contains(StandardDataFormats.Text))
                {
                    try
                    {
                        var link = await clipboard.GetTextAsync();
                        if (link.IsValidUrl())
                        {
                            popup.Link = link;
                        }
                    }
                    catch
                    {
                        // The clipboard changed or went away while being read.
                    }
                }
            }

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == true && !string.IsNullOrEmpty(popup.Link))
            {
                ApplyLink(RichLink.FromUrl(popup.Link), popup.Text, selected);
            }

            Editor?.Focus(FocusState.Programmatic);
        }

        // The selected text, shown as a date in the reader's own format.
        private async void CreateDate()
        {
            var selected = _engine.GetSelectedText();

            var url = await ChooseDateTimeToast.ChooseDateUrlAsync(XamlRoot);
            if (url != null && TdExtensions.TryParseDateTime(url, out TextEntityTypeDateTime dateTime) && selected == _engine.GetSelectedText())
            {
                ApplyLink(RichLink.FromDateTime(dateTime.UnixTime, dateTime.FormattingType), selected, selected);
            }

            Editor?.Focus(FocusState.Programmatic);
        }

        // As the rich editor window does it: over the selection when its text was kept, over new
        // text replacing it when the text was changed in the popup.
        private void ApplyLink(RichLink link, string text, string selected)
        {
            if (!string.IsNullOrEmpty(selected) && text == selected)
            {
                _engine.SetLink(link);
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _engine.BeginBatch();

            if (_engine.IsSelectionEmpty)
            {
                _engine.SetLink(link, text);
            }
            else
            {
                _engine.InsertText(text);

                var end = _engine.Focus;
                _engine.Select(new RichPosition(end.Leaf, Math.Max(0, end.Offset - text.Length)), end);
                _engine.SetLink(link);
                _engine.SetCaret(end);
            }

            _engine.EndBatch();
        }

        // =====================================================================================
        // Settings
        // =====================================================================================

        public FormattedTextEntity AllowedEntities
        {
            get => _allowedEntities;
            set
            {
                if (_allowedEntities == value)
                {
                    return;
                }

                _allowedEntities = value;
                _engine.Schema = ToSchema(value);

                // What the text already holds is conformed to the new schema, as loading it would be.
                _engine.Load(_engine.Document);
            }
        }

        private static RichSchema ToSchema(FormattedTextEntity entities)
        {
            if (entities == FormattedTextEntity.All)
            {
                return RichSchema.Message;
            }

            var marks = RichMarks.None;

            if ((entities & FormattedTextEntity.Bold) != 0) marks |= RichMarks.Bold;
            if ((entities & FormattedTextEntity.Italic) != 0) marks |= RichMarks.Italic;
            if ((entities & FormattedTextEntity.Underline) != 0) marks |= RichMarks.Underline;
            if ((entities & FormattedTextEntity.Strikethrough) != 0) marks |= RichMarks.Strikethrough;
            if ((entities & FormattedTextEntity.Spoiler) != 0) marks |= RichMarks.Spoiler;
            if ((entities & FormattedTextEntity.Mono) != 0) marks |= RichMarks.Monospace;

            // Code blocks go with monospace: the old box made both out of the same font.
            return new RichSchema(marks,
                urls: (entities & FormattedTextEntity.TextUrl) != 0,
                mentions: (entities & FormattedTextEntity.Mention) != 0,
                dates: (entities & FormattedTextEntity.Date) != 0,
                customEmoji: (entities & FormattedTextEntity.CustomEmoji) != 0,
                quotes: (entities & FormattedTextEntity.Quote) != 0,
                code: (entities & FormattedTextEntity.Mono) != 0);
        }

        /// <summary>The longest text, in UTF-16 units of what is sent; 0 for no limit.</summary>
        public int MaxLength { get; set; }

        public bool IsReplaceEmojiEnabled { get; set; } = true;

        public bool IsSubmitEnabled { get; set; } = true;

        // Kept for the interface only: the editor follows the system spell checker setting by
        // itself, for squiggles and for the keyboard's suggestions, and that setting is what
        // callers set these two from.
        public bool IsSpellCheckEnabled { get; set; } = true;

        public bool IsTextPredictionEnabled { get; set; } = true;

        /// <summary>False for a box of one line, where Enter that does not send does nothing.</summary>
        public bool AcceptsReturn { get; set; } = true;

        private CoreTextInputScope _textInputScope = CoreTextInputScope.Text;

        private ListViewBase _controlledList;

        /// <summary>The suggestions a page shows for the box outside it, which a screen reader is told the editor controls.</summary>
        public ListViewBase ControlledList
        {
            get => _controlledList;
            set
            {
                if (Editor != null && _controlledList != null)
                {
                    AutomationProperties.GetControlledPeers(Editor).Remove(_controlledList);
                }

                _controlledList = value;

                if (Editor != null && _controlledList != null)
                {
                    AutomationProperties.GetControlledPeers(Editor).Add(_controlledList);
                }
            }
        }

        private bool _preventKeyboardDisplayOnProgrammaticFocus;

        public bool PreventKeyboardDisplayOnProgrammaticFocus
        {
            get => _preventKeyboardDisplayOnProgrammaticFocus;
            set
            {
                _preventKeyboardDisplayOnProgrammaticFocus = value;
                Editor?.PreventKeyboardDisplayOnProgrammaticFocus = value;
            }
        }

        /// <summary>What the touch keyboard offers for the text, outside code.</summary>
        public CoreTextInputScope TextInputScope
        {
            get => _textInputScope;
            set
            {
                _textInputScope = value;
                Editor?.TextInputScope = value;
            }
        }

        #region PlaceholderText

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register("PlaceholderText", typeof(string), typeof(MessageTextBox), new PropertyMetadata(null, OnPlaceholderTextChanged));

        private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (MessageTextBox)d;
            sender.Editor?.PlaceholderText = (string)e.NewValue ?? string.Empty;
        }

        #endregion

        #region IsReadOnly

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(MessageTextBox), new PropertyMetadata(false, OnIsReadOnlyChanged));

        // The editor has no read-only mode: text reaches it from the input service, past any key
        // handler. Disabled, it takes no focus and so no input at all, which is what a locked
        // checklist item needs.
        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (MessageTextBox)d;
            sender.Editor?.IsEnabled = !(bool)e.NewValue;
        }

        #endregion
    }
}
