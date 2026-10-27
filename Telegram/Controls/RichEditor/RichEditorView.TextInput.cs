//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Windows.Foundation;
using Windows.Globalization;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Text.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
// CoreText reports the Windows.UI.Text enum in every flavour, including the one where the bare
// name is Microsoft.UI.Text's.
using CoreTextUnderlineType = Windows.UI.Text.UnderlineType;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// Text input. The input service sees one leaf at a time - the one the caret is in - as the
    /// whole of the text: composition, reconversion and autocorrect only ever work inside a line,
    /// and handing it the document would mean inventing a flat string for tables and captions.
    /// Moving to another leaf tells it the focus moved, which is what it is.
    /// </summary>
    public sealed partial class RichEditorView
    {
        private CoreTextEditContext _editContext;

        // The leaf the input service was last told about, and how long it then was: a change
        // made by a key or the mouse is reported as that whole text replaced.
        private RichTextLeaf _contextLeaf;
        private int _contextLength;

        private bool _contextFocused;
        private bool _imeUpdating;
        private bool _composing;

        // Where the composition is in the context leaf, which the service never says: followed
        // through its edits, for a screen reader.
        private int _compositionStart;
        private int _compositionEnd;

        // One per range the service formatted. An IME formats a composition clause by clause, a
        // call each - the clause being converted set apart from the rest - so each range keeps its
        // own look, and they are drawn together.
        private readonly List<CompositionSegment> _composition = new();
        private readonly List<Rectangle> _compositionLines = new();
        private readonly List<Rectangle> _compositionBackgrounds = new();
        private readonly Dictionary<UIElementType, SolidColorBrush> _compositionBrushes = new();
        private UISettings _uiSettings;
        private bool _compositionStale;

        private readonly struct CompositionSegment
        {
            public CompositionSegment(CoreTextRange range, CoreTextUnderlineType underline, UIElementType? underlineColor, UIElementType? background)
            {
                Range = range;
                Underline = underline;
                UnderlineColor = underlineColor;
                Background = background;
            }

            public CoreTextRange Range { get; }

            public CoreTextUnderlineType Underline { get; }

            public UIElementType? UnderlineColor { get; }

            public UIElementType? Background { get; }
        }

        private InputPane _inputPane;

        // The touch keyboard covers the bottom of the window rather than resizing it: the part of
        // the editor under it is taken out of the scroll viewport, so the caret can be scrolled
        // above it and the last lines can still be reached.
        private void OnInputPaneShowing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            if (ScrollViewer == null)
            {
                return;
            }

            var occluded = args.OccludedRect;
            var bounds = ScrollViewer.TransformToVisual(null).TransformBounds(new Rect(0, 0, ScrollViewer.ActualWidth, ScrollViewer.ActualHeight));

            var overlap = bounds.Bottom - occluded.Top;
            if (overlap > 0)
            {
                args.EnsuredFocusedElementInView = true;
                ScrollViewer.Margin = new Thickness(0, 0, 0, overlap);

                this.PostOnUIThread(UpdateCaret, low: true);
            }
        }

        private void OnInputPaneHiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            if (ScrollViewer != null)
            {
                ScrollViewer.Margin = new Thickness(0);
            }
        }

        private void CreateEditContext()
        {
            if (_inputPane == null)
            {
                try
                {
#if WINUI3
                    _inputPane = InputPaneInterop.GetForWindow(WindowContext.GetWindowHandle(XamlRoot));
#else
                    _inputPane = InputPane.GetForCurrentView();
#endif
                    _inputPane.Showing += OnInputPaneShowing;
                    _inputPane.Hiding += OnInputPaneHiding;
                }
                catch
                {
                    // No input pane on this view: there is no touch keyboard to make room for.
                    _inputPane = null;
                }
            }

            if (_editContext != null)
            {
                return;
            }

            try
            {
                var manager = CoreTextServicesManager.GetForCurrentView();
                _editContext = manager.CreateEditContext();

                // The manager lives as long as the view, which outlives this control.
                _textServices = manager;
                _textServices.InputLanguageChanged += OnInputLanguageChanged;
            }
            catch
            {
                // No text services for this view: hosted on a thread without one. Keys still
                // navigate and delete; nothing can be typed, which is better than a crash.
                _editContext = null;
                return;
            }

            UpdateKeyboardDirection();

            _editContext.InputScope = GetInputScope(_engine?.Focus.Leaf);
            _editContext.InputPaneDisplayPolicy = CoreTextInputPaneDisplayPolicy.Automatic;
            _editContext.Name = "RichEditor";

            _editContext.TextRequested += OnTextRequested;
            _editContext.SelectionRequested += OnSelectionRequested;
            _editContext.TextUpdating += OnTextUpdatingGuarded;
            _editContext.SelectionUpdating += OnSelectionUpdatingGuarded;
            _editContext.FormatUpdating += OnFormatUpdatingGuarded;
            _editContext.LayoutRequested += OnLayoutRequested;
            _editContext.CompositionStarted += OnCompositionStarted;
            _editContext.CompositionCompleted += OnCompositionCompleted;
            _editContext.FocusRemoved += OnFocusRemoved;

            if (_hasFocus)
            {
                EnterContext();
            }
        }

        private void DestroyEditContext()
        {
            // The input pane lives as long as the view, which outlives this control: a handler
            // left on it would keep the whole editor alive.
            if (_inputPane != null)
            {
                _inputPane.Showing -= OnInputPaneShowing;
                _inputPane.Hiding -= OnInputPaneHiding;
                _inputPane = null;
            }

            ReleaseEditContext();
        }

        private CoreTextServicesManager _textServices;

        // Whether the keyboard writes right to left. What has no direction of its own goes its
        // way while it is typed in: the caret's line before its first letter, and the caret's
        // flag, as Windows draws it for a right to left keyboard.
        private bool _keyboardRightToLeft;

        private void OnInputLanguageChanged(CoreTextServicesManager sender, object args)
        {
            UpdateKeyboardDirection();
        }

        private void UpdateKeyboardDirection()
        {
            var rightToLeft = false;

            try
            {
                rightToLeft = _textServices?.InputLanguage?.LayoutDirection == LanguageLayoutDirection.Rtl;
            }
            catch
            {
                // No input language to be had: the keyboard reads as the app does.
            }

            if (_keyboardRightToLeft != rightToLeft)
            {
                _keyboardRightToLeft = rightToLeft;
                UpdateCaret(false);
            }
        }

        private void ReleaseEditContext()
        {
            if (_editContext == null)
            {
                return;
            }

            if (_contextFocused)
            {
                _editContext.NotifyFocusLeave();
                _contextFocused = false;
            }

            _editContext.TextRequested -= OnTextRequested;
            _editContext.SelectionRequested -= OnSelectionRequested;
            _editContext.TextUpdating -= OnTextUpdatingGuarded;
            _editContext.SelectionUpdating -= OnSelectionUpdatingGuarded;
            _editContext.FormatUpdating -= OnFormatUpdatingGuarded;
            _editContext.LayoutRequested -= OnLayoutRequested;
            _editContext.CompositionStarted -= OnCompositionStarted;
            _editContext.CompositionCompleted -= OnCompositionCompleted;
            _editContext.FocusRemoved -= OnFocusRemoved;

            _editContext = null;

            if (_textServices != null)
            {
                _textServices.InputLanguageChanged -= OnInputLanguageChanged;
                _textServices = null;
            }

            // A new context holds no text.
            _contextLeaf = null;
            _contextLength = 0;
            _contextStale = false;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);

            // Raised by the editor and bubbled from a block's button alike; moving focus between
            // them raises no LostFocus here.
            _hasFocus = true;
            EnterContext();

            _placeholderLeaf = null;
            UpdatePlaceholders();
            UpdateSelection();
        }

        // The text comes through the edit context; the routed event is only marked, as a text
        // box marks it, so that what listens on the window to send typing somewhere (ChatView
        // into its composer) does not type it a second time.
        protected override void OnCharacterReceived(CharacterReceivedRoutedEventArgs e)
        {
            if (_engine != null && IsEnabled)
            {
                e.Handled = true;

#if WINUI3
                // A keyboard layout that is not a text service types past the store, as WM_CHAR,
                // where the framework's CoreText picked the characters up by itself.
                if (_contextFocused)
                {
                    _editContext?.ReceiveCharacter(e.Character);
                }
#endif
            }

            base.OnCharacterReceived(e);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            // Focus left the editor, or the window was deactivated with it here: either way the
            // caret, the hints and the suggestions go until GotFocus.
            _hasFocus = false;

            LeaveContext();
            HideSuggestions();

            _placeholderLeaf = null;
            UpdatePlaceholders();
            UpdateSelection();
        }

        private void EnterContext()
        {
            if (_editContext == null || _engine == null)
            {
                return;
            }

            // The keyboard may have been switched while focus was elsewhere.
            UpdateKeyboardDirection();

            // The service keeps its own copy of the text across focus, and a change made while
            // focus was away was never reported to it: typing would go in where it remembers the
            // caret, ahead of the character ChatView put in on the way here. A new context has no
            // copy, and asks.
            if (_changedAway && !_contextFocused)
            {
                _changedAway = false;

                ReleaseEditContext();
                CreateEditContext();
                return;
            }

            var leaf = _engine.SelectedNode == null ? _engine.Focus.Leaf : null;
            if (!ReferenceEquals(leaf, _contextLeaf))
            {
                _contextLeaf = leaf;
                _contextStale = true;
            }

            if (!_contextFocused)
            {
                _contextFocused = true;
                _editContext.InputScope = GetInputScope(_contextLeaf);
                _editContext.InputPaneDisplayPolicy = PreventKeyboardDisplayOnProgrammaticFocus && FocusState == FocusState.Programmatic
                    ? CoreTextInputPaneDisplayPolicy.Manual
                    : CoreTextInputPaneDisplayPolicy.Automatic;
                _editContext.NotifyFocusEnter();
            }

            NotifyContextReplaced();
        }

        // Whether the service still holds the text of the leaf it was told about before
        // _contextLeaf, which _contextLength is then the length of.
        private bool _contextStale;

        /// <summary>
        /// Hands the service another leaf, or the same one rebuilt. Leaving and entering focus
        /// ends what was being composed and makes the service read the input scope again, but
        /// it keeps the text it had: the pinyin IME went on composing at its old caret, into a
        /// line that was not that long, until <see cref="NotifyContextReplaced"/> told it.
        /// </summary>
        private void SwitchContext(RichTextLeaf leaf)
        {
            _contextLeaf = leaf;
            _contextStale = true;

            _editContext.NotifyFocusLeave();
            _editContext.InputScope = GetInputScope(leaf);
            _editContext.NotifyFocusEnter();
        }

        /// <summary>Tells the service the text it holds was replaced, all of it, by the leaf's.</summary>
        private void NotifyContextReplaced()
        {
            if (_editContext == null || !_contextFocused || !_contextStale)
            {
                return;
            }

            _contextStale = false;

            var previous = _contextLength;
            _contextLength = _contextLeaf?.Length ?? 0;

            _editContext.NotifyTextChanged(new CoreTextRange { StartCaretPosition = 0, EndCaretPosition = previous }, _contextLength, GetContextSelection());
        }

        /// <summary>
        /// Whether focus given from code leaves the touch keyboard down, as on a text box: the
        /// composer takes focus whenever a chat opens. A tap still brings the keyboard up.
        /// </summary>
        public bool PreventKeyboardDisplayOnProgrammaticFocus { get; set; }

        // A tap is asking for the keyboard, however focus arrived.
        private void ShowInputPane()
        {
            if (_editContext != null && _editContext.InputPaneDisplayPolicy == CoreTextInputPaneDisplayPolicy.Manual)
            {
                _editContext.InputPaneDisplayPolicy = CoreTextInputPaneDisplayPolicy.Automatic;
                _inputPane?.TryShow();
            }
        }

        /// <summary>
        /// What the keyboard offers for the text the caret is in. Code takes no suggestions and no
        /// autocorrect, and neither does anything when the user turned the system's spell checker
        /// off - the setting the composer follows for the same two things. Set before focus is
        /// entered, which is when the service reads it.
        /// </summary>
        private CoreTextInputScope GetInputScope(RichTextLeaf leaf)
        {
            return leaf == null || leaf.IsPlain || !AppSettings.UseSystemSpellChecker
                ? CoreTextInputScope.Default
                : TextInputScope;
        }

        /// <summary>What the keyboard offers for text outside code: a message box asks for <see cref="CoreTextInputScope.Chat"/>. Read when focus enters.</summary>
        public CoreTextInputScope TextInputScope { get; set; } = CoreTextInputScope.Text;

        private void LeaveContext()
        {
            if (_editContext != null && _contextFocused)
            {
                _contextFocused = false;
                _editContext.NotifyFocusLeave();
            }

            _composing = false;
            ClearComposition();
        }

        // Whether the text changed while the service was not told, which EnterContext makes up for.
        private bool _changedAway;

        /// <summary>Tells the input service about a change it did not make itself.</summary>
        private void SyncEditContext(RichEditorChanges changes)
        {
            if (_editContext != null && !_contextFocused && (changes & (RichEditorChanges.Text | RichEditorChanges.Structure)) != 0)
            {
                _changedAway = true;
            }

            if (_editContext == null || !_contextFocused || _imeUpdating)
            {
                return;
            }

            var leaf = _engine.SelectedNode == null ? _engine.Focus.Leaf : null;

            if ((changes & RichEditorChanges.Structure) != 0 || !ReferenceEquals(leaf, _contextLeaf))
            {
                // Another text altogether: what was being composed is over.
                SwitchContext(leaf);
                NotifyContextReplaced();
                return;
            }

            if (_contextStale)
            {
                NotifyContextReplaced();
            }
            else if ((changes & RichEditorChanges.Text) != 0)
            {
                var previous = _contextLength;
                _contextLength = leaf?.Length ?? 0;

                _editContext.NotifyTextChanged(new CoreTextRange { StartCaretPosition = 0, EndCaretPosition = previous }, _contextLength, GetContextSelection());
            }
            else if ((changes & RichEditorChanges.Selection) != 0)
            {
                _editContext.NotifySelectionChanged(GetContextSelection());
            }
        }

        private void NotifyLayoutChanged()
        {
            if (_editContext != null && _contextFocused)
            {
                _editContext.NotifyLayoutChanged();
            }
        }

        /// <summary>
        /// The selection as the service sees it, inside the one leaf. A selection reaching into
        /// other leaves is reported as its part in this one; typing over it still replaces all
        /// of it, which <see cref="OnTextUpdating"/> takes care of.
        /// </summary>
        private CoreTextRange GetContextSelection()
        {
            var leaf = _contextLeaf;
            if (leaf == null)
            {
                return default;
            }

            var start = _engine.Start;
            var end = _engine.End;

            var from = ReferenceEquals(start.Leaf, leaf) ? start.Offset : 0;
            var to = ReferenceEquals(end.Leaf, leaf) ? end.Offset : leaf.Length;

            if (!ReferenceEquals(start.Leaf, leaf) && !ReferenceEquals(end.Leaf, leaf))
            {
                from = to = Math.Min(_engine.Focus.Offset, leaf.Length);
            }

            return new CoreTextRange { StartCaretPosition = from, EndCaretPosition = to };
        }

        private void OnTextRequested(CoreTextEditContext sender, CoreTextTextRequestedEventArgs args)
        {
            // What was asked for, and not the line made into a string to cut it from: a service
            // composing asks again on every key.
            var leaf = _contextLeaf;
            var length = leaf?.Length ?? 0;
            var range = args.Request.Range;

            var start = Math.Clamp(range.StartCaretPosition, 0, length);
            var end = Math.Clamp(range.EndCaretPosition, start, length);

            args.Request.Text = leaf == null ? string.Empty : start == 0 && end == length ? leaf.Text : leaf.Substring(start, end - start);
        }

        private void OnSelectionRequested(CoreTextEditContext sender, CoreTextSelectionRequestedEventArgs args)
        {
            args.Request.Selection = GetContextSelection();
        }

        // The service's edits go through these: an exception here ends the process, so a
        // message box that throws sends every box back to RichEdit on the next launch, before
        // the user has to find the diagnostics page.
        private void OnTextUpdatingGuarded(CoreTextEditContext sender, CoreTextTextUpdatingEventArgs args)
        {
            try
            {
                OnTextUpdating(sender, args);
            }
            catch (Exception ex) when (FallBackFromNative(ex))
            {
                // Never reached: the filter only takes note.
            }
        }

        private void OnSelectionUpdatingGuarded(CoreTextEditContext sender, CoreTextSelectionUpdatingEventArgs args)
        {
            try
            {
                OnSelectionUpdating(sender, args);
            }
            catch (Exception ex) when (FallBackFromNative(ex))
            {
                // Never reached: the filter only takes note.
            }
        }

        private void OnFormatUpdatingGuarded(CoreTextEditContext sender, CoreTextFormatUpdatingEventArgs args)
        {
            try
            {
                OnFormatUpdating(sender, args);
            }
            catch (Exception ex) when (FallBackFromNative(ex))
            {
                // Never reached: the filter only takes note.
            }
        }

        /// <summary>
        /// Notes an exception on its way out of the editor, for a box the diagnostics switch put
        /// it in, and returns false so it carries on as it would have: in a filter, the stack is
        /// left as it was for the crash report.
        /// </summary>
        private bool FallBackFromNative(Exception ex)
        {
            if (_engine?.Schema != null && AppSettings.Diagnostics.NativeTextInput != NativeTextInputMode.Off)
            {
                Logger.Error(ex);
                AppSettings.Diagnostics.NativeTextInput = NativeTextInputMode.Off;
            }

            return false;
        }

        private void OnTextUpdating(CoreTextEditContext sender, CoreTextTextUpdatingEventArgs args)
        {
            var text = args.Text ?? string.Empty;

            // What the service holds once this is applied, whatever the editor makes of it.
            var held = _contextLength - (args.Range.EndCaretPosition - args.Range.StartCaretPosition) + text.Length;

            _imeUpdating = true;
            _composition.Clear();
            _compositionStale = true;

            try
            {
                if (text == "\r" || text == "\n")
                {
                    // Some keyboards send Enter as text rather than as a key. The line it makes
                    // is another leaf, which the service is told of below.
                    _engine.InsertParagraph();
                }
                else
                {
                    text = text.Replace("\r\n", "\n").Replace('\r', '\n');

                    var leaf = _contextLeaf;
                    var range = args.Range;

                    if (leaf == null || !ReferenceEquals(_engine.Focus.Leaf, leaf) || _engine.SelectedNode != null)
                    {
                        // A block is selected, or the selection reaches past this leaf: what is
                        // typed replaces the selection the user sees, all of it.
                        _engine.InsertText(text);
                    }
                    else if (!_engine.IsSelectionEmpty && !ReferenceEquals(_engine.Start.Leaf, _engine.End.Leaf) && IsContextSelection(range))
                    {
                        _engine.InsertText(text);
                    }
                    else
                    {
                        var version = _engine.Document.Version;
                        _engine.ReplaceText(leaf, range.StartCaretPosition, range.EndCaretPosition, text);

                        // A space that completed a markdown rule turned the line into another
                        // block: the selection the service asks for is in text no longer there.
                        if (_engine.Document.Version != version)
                        {
                            _structureChanged = true;
                        }
                        else
                        {
                            var selection = args.NewSelection;
                            var caret = new RichPosition(leaf, Math.Clamp(selection.StartCaretPosition, 0, leaf.Length));
                            var focus = new RichPosition(leaf, Math.Clamp(selection.EndCaretPosition, 0, leaf.Length));

                            if (!caret.Equals(_engine.Anchor) || !focus.Equals(_engine.Focus))
                            {
                                _engine.Select(caret, focus);
                            }
                        }
                    }
                }

                args.Result = CoreTextTextUpdatingResult.Succeeded;
            }
            finally
            {
                _imeUpdating = false;
            }

            if (_composing && _contextLeaf is RichTextLeaf composed && ReferenceEquals(_engine.Focus.Leaf, composed))
            {
                var range = args.Range;
                var delta = text.Length - (range.EndCaretPosition - range.StartCaretPosition);

                _compositionStart = Math.Clamp(Math.Min(_compositionStart, range.StartCaretPosition), 0, composed.Length);
                _compositionEnd = Math.Clamp(Math.Max(_compositionEnd + delta, range.StartCaretPosition + text.Length), _compositionStart, composed.Length);

                Peer?.RaiseComposition(composed.ToPlainText(_compositionStart, _compositionEnd), false);
            }

            // Whatever the update ended in is the text the service now believes in, unless the
            // update moved to another leaf or rebuilt the line: then it is told so, once this
            // handler has returned - the service is in the middle of its own edit.
            var current = _engine.SelectedNode == null ? _engine.Focus.Leaf : null;
            if (!ReferenceEquals(current, _contextLeaf) || _structureChanged)
            {
                _structureChanged = false;
                _contextLength = Math.Max(0, held);

                SwitchContext(current);
                this.PostOnUIThread(NotifyContextReplaced);
            }
            else
            {
                _contextLength = _contextStale ? Math.Max(0, held) : current?.Length ?? 0;
            }
        }

        private bool _structureChanged;

        /// <summary>
        /// A key typed at the caret, taken as OnTextUpdating takes the service's: for the meter,
        /// which has no service to type through.
        /// </summary>
        internal void TypeForMeasure(string text, int deleted = 0)
        {
            var focus = _engine.Focus;

            _imeUpdating = true;

            try
            {
                _engine.ReplaceText(focus.Leaf, Math.Max(focus.Offset - deleted, 0), focus.Offset, text);
            }
            finally
            {
                _imeUpdating = false;
            }
        }

        private bool IsContextSelection(CoreTextRange range)
        {
            var selection = GetContextSelection();
            return range.StartCaretPosition == selection.StartCaretPosition && range.EndCaretPosition == selection.EndCaretPosition;
        }

        private void OnSelectionUpdating(CoreTextEditContext sender, CoreTextSelectionUpdatingEventArgs args)
        {
            var leaf = _contextLeaf;
            if (leaf == null)
            {
                return;
            }

            _imeUpdating = true;

            try
            {
                var selection = args.Selection;
                _engine.Select(
                    new RichPosition(leaf, Math.Clamp(selection.StartCaretPosition, 0, leaf.Length)),
                    new RichPosition(leaf, Math.Clamp(selection.EndCaretPosition, 0, leaf.Length)));
            }
            finally
            {
                _imeUpdating = false;
            }

            args.Result = CoreTextSelectionUpdatingResult.Succeeded;
        }

        // How the text being composed looks is the service's to say, and drawing it is what tells
        // the user which part is still provisional and which clause is being converted.
        private void OnFormatUpdating(CoreTextEditContext sender, CoreTextFormatUpdatingEventArgs args)
        {
            var range = args.Range;

            // A range formatted again replaces what it overlapped.
            for (int i = _composition.Count - 1; i >= 0; i--)
            {
                var other = _composition[i].Range;
                if (other.StartCaretPosition < range.EndCaretPosition && range.StartCaretPosition < other.EndCaretPosition)
                {
                    _composition.RemoveAt(i);
                }
            }

            _composition.Add(new CompositionSegment(range, args.UnderlineType ?? CoreTextUnderlineType.None, args.UnderlineColor, args.BackgroundColor));
            Peer?.RaiseConversionTargetChanged();

            // The text this formats may not be laid out yet - a format follows the update that
            // typed it - so it is drawn once layout has run.
            _compositionStale = true;
            InvalidateArrange();

            args.Result = CoreTextFormatUpdatingResult.Succeeded;
        }

        /// <summary>Draws the composition again if it changed since it was last drawn; after layout.</summary>
        private void UpdateComposition()
        {
            if (_compositionStale)
            {
                _compositionStale = false;
                DrawComposition();
            }
        }

        private void DrawComposition()
        {
            if (Decorations == null || _contextLeaf == null || !_leafViews.TryGetValue(_contextLeaf, out RichLeafView view))
            {
                ClearComposition();
                return;
            }

            var transform = view.TransformToVisual(Surface);
            var lines = 0;
            var backgrounds = 0;

            foreach (var segment in _composition)
            {
                if (segment.Underline == CoreTextUnderlineType.None && segment.Background == null)
                {
                    continue;
                }

                var rects = view.GetRangeRects(segment.Range.StartCaretPosition, segment.Range.EndCaretPosition);
                var thickness = segment.Underline is CoreTextUnderlineType.Thick or CoreTextUnderlineType.Double ? 2 : 1;

                foreach (var rect in rects)
                {
                    if (segment.Background is UIElementType background)
                    {
                        var fill = Take(_compositionBackgrounds, ref backgrounds, true);
                        var origin = transform.TransformPoint(new Point(rect.X, rect.Y));

                        fill.Fill = GetCompositionBrush(background);
                        Canvas.SetLeft(fill, origin.X);
                        Canvas.SetTop(fill, origin.Y);
                        fill.Width = rect.Width;
                        fill.Height = rect.Height;
                    }

                    if (segment.Underline != CoreTextUnderlineType.None)
                    {
                        var line = Take(_compositionLines, ref lines, false);
                        var origin = transform.TransformPoint(new Point(rect.X, rect.Bottom - thickness));

                        // The caret's own brush, from the template, unless the service asked
                        // for a colour of the system's.
                        line.Fill = segment.UnderlineColor is UIElementType color ? GetCompositionBrush(color) : Caret?.Fill;
                        Canvas.SetLeft(line, origin.X);
                        Canvas.SetTop(line, origin.Y);
                        line.Width = rect.Width;
                        line.Height = thickness;
                    }
                }
            }

            for (int i = lines; i < _compositionLines.Count; i++)
            {
                _compositionLines[i].Visibility = Visibility.Collapsed;
            }

            for (int i = backgrounds; i < _compositionBackgrounds.Count; i++)
            {
                _compositionBackgrounds[i].Visibility = Visibility.Collapsed;
            }
        }

        // A shape of the pool, shown; backgrounds go under everything else in the layer.
        private Rectangle Take(List<Rectangle> pool, ref int used, bool bottom)
        {
            Rectangle shape;
            if (used < pool.Count)
            {
                shape = pool[used];
            }
            else
            {
                shape = new Rectangle { IsHitTestVisible = false };
                pool.Add(shape);

                if (bottom)
                {
                    Decorations.Children.Insert(0, shape);
                }
                else
                {
                    Decorations.Children.Add(shape);
                }
            }

            used++;
            shape.Visibility = Visibility.Visible;
            return shape;
        }

        /// <summary>
        /// A colour the input service names: one of the system's own, which the IME picked, so it
        /// is read from the system rather than from the app's theme. One brush per element type.
        /// </summary>
        private SolidColorBrush GetCompositionBrush(UIElementType type)
        {
            if (!_compositionBrushes.TryGetValue(type, out SolidColorBrush brush))
            {
                _uiSettings ??= new UISettings();
                _compositionBrushes[type] = brush = new SolidColorBrush(_uiSettings.UIElementColor(type));
            }

            return brush;
        }

        private void ClearComposition()
        {
            _composition.Clear();
            _compositionStale = false;

            foreach (var line in _compositionLines)
            {
                line.Visibility = Visibility.Collapsed;
            }

            foreach (var background in _compositionBackgrounds)
            {
                background.Visibility = Visibility.Collapsed;
            }
        }

        private void OnLayoutRequested(CoreTextEditContext sender, CoreTextLayoutRequestedEventArgs args)
        {
#if WINUI3
            var window = WindowContext.ForXamlRoot(this);
#else
            var window = CoreWindow.GetForCurrentThread();
#endif
            if (window == null || Surface == null)
            {
                return;
            }

            var caret = GetCaretRect();
            if (caret.IsEmpty)
            {
                caret = new Rect(0, 0, 0, 0);
            }

            // Screen pixels: the element in the window, the window on the screen, then the scale.
            var scale = XamlRoot?.RasterizationScale ?? 1;
            var bounds = window.Bounds;

            var text = Surface.TransformToVisual(null).TransformBounds(new Rect(caret.X, caret.Y, Math.Max(1, caret.Width), caret.Height));
            var control = TransformToVisual(null).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));

            args.Request.LayoutBounds.TextBounds = Scale(text, bounds, scale);
            args.Request.LayoutBounds.ControlBounds = Scale(control, bounds, scale);
        }

        private static Rect Scale(Rect rect, Rect window, double scale)
        {
            return new Rect(
                (rect.X + window.X) * scale,
                (rect.Y + window.Y) * scale,
                rect.Width * scale,
                rect.Height * scale);
        }

        private void OnCompositionStarted(CoreTextEditContext sender, CoreTextCompositionStartedEventArgs args)
        {
            _composing = true;
            _compositionStart = _compositionEnd = GetContextSelection().StartCaretPosition;
            ClearComposition();
        }

        private void OnCompositionCompleted(CoreTextEditContext sender, CoreTextCompositionCompletedEventArgs args)
        {
            if (_composing && _contextLeaf is RichTextLeaf composed)
            {
                var start = Math.Min(_compositionStart, composed.Length);
                var end = Math.Clamp(_compositionEnd, start, composed.Length);

                Peer?.RaiseComposition(composed.ToPlainText(start, end), true);
            }

            _composing = false;
            ClearComposition();
        }

        /// <summary>
        /// The text being composed or, with <paramref name="target"/>, the clause of it being
        /// converted, in the leaf the input service is in.
        /// </summary>
        internal bool TryGetComposition(bool target, out RichTextLeaf leaf, out int start, out int end)
        {
            leaf = _contextLeaf;
            start = end = 0;

            if (!_composing || leaf == null)
            {
                return false;
            }

            if (!target)
            {
                start = Math.Min(_compositionStart, leaf.Length);
                end = Math.Clamp(_compositionEnd, start, leaf.Length);
                return true;
            }

            // An IME sets the clause it is converting apart from the rest by a background, or
            // failing that by a heavier underline.
            var found = -1;

            for (int i = 0; i < _composition.Count; i++)
            {
                if (_composition[i].Background != null)
                {
                    found = i;
                    break;
                }

                if (found < 0 && _composition[i].Underline is CoreTextUnderlineType.Thick or CoreTextUnderlineType.Double)
                {
                    found = i;
                }
            }

            if (found < 0)
            {
                return false;
            }

            var range = _composition[found].Range;
            start = Math.Clamp(range.StartCaretPosition, 0, leaf.Length);
            end = Math.Clamp(range.EndCaretPosition, start, leaf.Length);
            return true;
        }

        private void OnFocusRemoved(CoreTextEditContext sender, object args)
        {
            // The service took focus away itself - another text control asked for it.
            _contextFocused = false;
            _composing = false;
            ClearComposition();
        }
    }
}
