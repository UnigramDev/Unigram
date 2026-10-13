//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Text.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

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

        private readonly List<Rectangle> _compositionLines = new();
        private CoreTextRange _compositionRange;

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

                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, UpdateCaret);
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
                    _inputPane = InputPane.GetForCurrentView();
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
            }
            catch
            {
                // No text services for this view: hosted on a thread without one. Keys still
                // navigate and delete; nothing can be typed, which is better than a crash.
                _editContext = null;
                return;
            }

            _editContext.InputScope = CoreTextInputScope.Text;
            _editContext.InputPaneDisplayPolicy = CoreTextInputPaneDisplayPolicy.Automatic;
            _editContext.Name = "RichEditor";

            _editContext.TextRequested += OnTextRequested;
            _editContext.SelectionRequested += OnSelectionRequested;
            _editContext.TextUpdating += OnTextUpdating;
            _editContext.SelectionUpdating += OnSelectionUpdating;
            _editContext.FormatUpdating += OnFormatUpdating;
            _editContext.LayoutRequested += OnLayoutRequested;
            _editContext.CompositionStarted += OnCompositionStarted;
            _editContext.CompositionCompleted += OnCompositionCompleted;
            _editContext.FocusRemoved += OnFocusRemoved;

            if (FocusState != FocusState.Unfocused)
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
            _editContext.TextUpdating -= OnTextUpdating;
            _editContext.SelectionUpdating -= OnSelectionUpdating;
            _editContext.FormatUpdating -= OnFormatUpdating;
            _editContext.LayoutRequested -= OnLayoutRequested;
            _editContext.CompositionStarted -= OnCompositionStarted;
            _editContext.CompositionCompleted -= OnCompositionCompleted;
            _editContext.FocusRemoved -= OnFocusRemoved;

            _editContext = null;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);

            EnterContext();

            _placeholderLeaf = null;
            UpdatePlaceholders();
            UpdateSelection();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            // Focus inside the editor - a button of a block - is still the editor's.
            if (FocusState != FocusState.Unfocused)
            {
                return;
            }

            LeaveContext();

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

            _contextLeaf = _engine.SelectedNode == null ? _engine.Focus.Leaf : null;
            _contextLength = _contextLeaf?.Length ?? 0;

            if (!_contextFocused)
            {
                _contextFocused = true;
                _editContext.NotifyFocusEnter();
            }
        }

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

        /// <summary>Tells the input service about a change it did not make itself.</summary>
        private void SyncEditContext(RichEditorChanges changes)
        {
            if (_editContext == null || !_contextFocused || _imeUpdating)
            {
                return;
            }

            var leaf = _engine.SelectedNode == null ? _engine.Focus.Leaf : null;

            if ((changes & RichEditorChanges.Structure) != 0 || !ReferenceEquals(leaf, _contextLeaf))
            {
                // Another text altogether: what was being composed is over, and the service
                // asks for the new text when focus comes back.
                _contextLeaf = leaf;
                _contextLength = leaf?.Length ?? 0;

                _editContext.NotifyFocusLeave();
                _editContext.NotifyFocusEnter();
                return;
            }

            if ((changes & RichEditorChanges.Text) != 0)
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
            var text = _contextLeaf?.Text ?? string.Empty;
            var range = args.Request.Range;

            var start = Math.Clamp(range.StartCaretPosition, 0, text.Length);
            var end = Math.Clamp(range.EndCaretPosition, start, text.Length);

            args.Request.Text = text.Substring(start, end - start);
        }

        private void OnSelectionRequested(CoreTextEditContext sender, CoreTextSelectionRequestedEventArgs args)
        {
            args.Request.Selection = GetContextSelection();
        }

        private void OnTextUpdating(CoreTextEditContext sender, CoreTextTextUpdatingEventArgs args)
        {
            var text = args.Text ?? string.Empty;

            _imeUpdating = true;

            try
            {
                if (text == "\r" || text == "\n")
                {
                    // Some keyboards send Enter as text rather than as a key.
                    _engine.InsertParagraph();
                    args.Result = CoreTextTextUpdatingResult.Succeeded;
                    return;
                }

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

                    // A space that completed a markdown rule turned the line into another block:
                    // the selection the service asks for is in text that is no longer there.
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

                args.Result = CoreTextTextUpdatingResult.Succeeded;
            }
            finally
            {
                _imeUpdating = false;
            }

            // Whatever the update ended in is the text the service now believes in, unless the
            // update moved to another leaf or rebuilt the line: then it is told so.
            var current = _engine.SelectedNode == null ? _engine.Focus.Leaf : null;
            if (!ReferenceEquals(current, _contextLeaf) || _structureChanged)
            {
                _structureChanged = false;
                _contextLeaf = current;
                _contextLength = current?.Length ?? 0;

                _editContext.NotifyFocusLeave();
                _editContext.NotifyFocusEnter();
            }
            else
            {
                _contextLength = current?.Length ?? 0;
            }
        }

        private bool _structureChanged;

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

        // The composition's own underline: the service decides how the text being composed
        // looks, and drawing it is what tells the user which part is still provisional.
        private void OnFormatUpdating(CoreTextEditContext sender, CoreTextFormatUpdatingEventArgs args)
        {
            _compositionRange = args.Range;
            DrawComposition(args.UnderlineType ?? UnderlineType.None);

            args.Result = CoreTextFormatUpdatingResult.Succeeded;
        }

        private void DrawComposition(UnderlineType underline)
        {
            if (Decorations == null || _contextLeaf == null || !_leafViews.TryGetValue(_contextLeaf, out RichLeafView view))
            {
                return;
            }

            var rects = underline == UnderlineType.None
                ? Array.Empty<Rect>()
                : view.GetRangeRects(_compositionRange.StartCaretPosition, _compositionRange.EndCaretPosition);

            var thickness = underline is UnderlineType.Thick or UnderlineType.Double ? 2 : 1;
            var transform = view.TransformToVisual(Surface);

            for (int i = 0; i < rects.Length; i++)
            {
                Rectangle line;
                if (i < _compositionLines.Count)
                {
                    line = _compositionLines[i];
                }
                else
                {
                    line = new Rectangle { Fill = Caret?.Fill };
                    _compositionLines.Add(line);
                    Decorations.Children.Add(line);
                }

                var origin = transform.TransformPoint(new Point(rects[i].X, rects[i].Bottom - thickness));

                Canvas.SetLeft(line, origin.X);
                Canvas.SetTop(line, origin.Y);
                line.Width = rects[i].Width;
                line.Height = thickness;
                line.Visibility = Visibility.Visible;
            }

            for (int i = rects.Length; i < _compositionLines.Count; i++)
            {
                _compositionLines[i].Visibility = Visibility.Collapsed;
            }
        }

        private void ClearComposition()
        {
            foreach (var line in _compositionLines)
            {
                line.Visibility = Visibility.Collapsed;
            }
        }

        private void OnLayoutRequested(CoreTextEditContext sender, CoreTextLayoutRequestedEventArgs args)
        {
            var window = CoreWindow.GetForCurrentThread();
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
        }

        private void OnCompositionCompleted(CoreTextEditContext sender, CoreTextCompositionCompletedEventArgs args)
        {
            _composing = false;
            ClearComposition();
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
