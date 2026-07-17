//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.Devices.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
{
    /// <summary>
    /// Drives a single continuous selection across the <see cref="ISelectableControl"/>
    /// descendants of a working tree (a parent <see cref="Panel"/>) — the read view's
    /// equivalent of selecting across an editor's blocks. It owns the gesture and
    /// renders selection purely through each control's highlight (see
    /// <see cref="ISelectableControl"/>), so it works regardless of focus and across
    /// any selectable kind (text today, formulae/etc. later).
    ///
    /// Gesture: press is only a CANDIDATE (taps and links keep working); once the
    /// pointer moves past a small threshold the manager captures the pointer and
    /// starts selecting, so moves/release continue to fire even outside the tree.
    /// </summary>
    public sealed class TextSelectionManager
    {
        private const double DragThreshold = 4.0;

        // Multi-tap detection (mouse): a press counts as a continuation of the previous one
        // when it lands within TapSlop and inside the system double-click time.
        private const double TapSlop = 4.0;
        private static readonly ulong DoubleClickTime = new Windows.UI.ViewManagement.UISettings().DoubleClickTime;

        private readonly Control _owner;
        private readonly UIElement _root;

        // Selectable controls in document (visual pre-order) order, rebuilt per gesture.
        private readonly List<ISelectableControl> _items = new();

        private bool _candidate;       // pointer is down on a selectable, not yet a drag
        private bool _selecting;       // drag confirmed, selecting
        private bool _captured;        // the root captured the pointer
        private Pointer _pointer;
        private Point _pressPoint;     // in _root coordinates

        private ISelectableControl _anchor;
        private int _anchorPosition;

        // The anchor end snapped to the gesture's granularity (a word/paragraph range, or
        // just the caret for Character). Fixed for the gesture; the moving end is snapped
        // per move and unioned with this.
        private int _anchorStart;
        private int _anchorEnd;
        private TextSelectionGranularity _granularity;

        // Mouse multi-tap state: 2 taps = word, 3 = paragraph. Tracked by hand because the
        // inner RichTextBlock's native double-tap word-select is off in Extended mode.
        private int _tapCount;
        private ulong _lastTapTime;
        private Point _lastTapPoint;

        private bool _hasSelection; // a finalized selection is currently shown
        private bool _watchingFocus; // subscribed to the (static) FocusManager.LostFocus

        // The current per-control selected ranges, in document order (for copy).
        private readonly List<(ISelectableControl Item, int Start, int End)> _selectedRanges = new();

        public event EventHandler SelectionChanged;

        public bool HasSelection => _hasSelection;

        // AddHandler/RemoveHandler match by the exact delegate instance — a fresh
        // `new PointerEventHandler(...)` in Detach does NOT remove these, so keep the instances
        // and reuse them. Otherwise the handlers stay on _root, rooting the manager (and through
        // _owner the whole MessageSelector) for the session.
        private readonly PointerEventHandler _pointerPressed;
        private readonly PointerEventHandler _pointerMoved;
        private readonly PointerEventHandler _pointerReleased;
        private readonly PointerEventHandler _pointerCaptureLost;

        public TextSelectionManager(Control owner, UIElement root, bool handleContextMenu = false)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _root.AddHandler(UIElement.PointerPressedEvent, _pointerPressed = new PointerEventHandler(OnPointerPressed), true);
            _root.AddHandler(UIElement.PointerMovedEvent, _pointerMoved = new PointerEventHandler(OnPointerMoved), true);
            _root.AddHandler(UIElement.PointerReleasedEvent, _pointerReleased = new PointerEventHandler(OnPointerReleased), true);
            _root.AddHandler(UIElement.PointerCaptureLostEvent, _pointerCaptureLost = new PointerEventHandler(OnPointerCaptureLost), true);
            // FocusManager.LostFocus is a STATIC event; if we unload mid-selection we'd
            // leak the subscription (and this whole control), so always unhook on unload.
            _owner = owner;
            _owner.KeyDown += OnKeyDown;
            _owner.Unloaded += OnUnloaded;

            if (handleContextMenu)
            {
                _root.ContextRequested += OnContextRequested;
            }
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_hasSelection && e.Key == VirtualKey.A && WindowContext.KeyModifiers(VirtualKeyModifiers.Control))
            {
                SelectAll();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopWatchingFocus();
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            if (HasSelection)
            {
                flyout.CreateFlyoutItem(CopySelectionToClipboard, Strings.Copy, Icons.Copy);
            }

            flyout.CreateFlyoutItem(SelectAll, Strings.SelectAll);
            flyout.ShowAt(sender, args);
        }

        public void Detach()
        {
            StopWatchingFocus();
            _root.RemoveHandler(UIElement.PointerPressedEvent, _pointerPressed);
            _root.RemoveHandler(UIElement.PointerMovedEvent, _pointerMoved);
            _root.RemoveHandler(UIElement.PointerReleasedEvent, _pointerReleased);
            _root.RemoveHandler(UIElement.PointerCaptureLostEvent, _pointerCaptureLost);
            _root.ContextRequested -= OnContextRequested;
            _owner.KeyDown -= OnKeyDown;
            _owner.Unloaded -= OnUnloaded;
        }

        // The per-window coordinator that keeps a single selection across bubbles. Resolved
        // lazily (the owner has a XamlRoot only once it's in the tree).
        private TextSelectionCoordinator Coordinator => TextSelectionCoordinator.GetFor(_owner?.XamlRoot);

        /// <summary>Clears the highlight on every selectable control and resets state.</summary>
        public void ClearSelection()
        {
            foreach (var item in _items)
            {
                item.ClearSelection();
            }

            var had = _hasSelection;
            Reset();
            _hasSelection = false;
            StopWatchingFocus();
            Coordinator?.Deactivate(this);
            if (had)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Selects all text across every selectable control in the tree (e.g. Ctrl+A),
        /// finalized like a completed drag — becomes the window's sole selection and clears
        /// on focus loss.
        /// </summary>
        public void SelectAll()
        {
            // Drop any current highlight, then (re)collect the tree.
            foreach (var item in _items)
            {
                item.ClearSelection();
            }

            Reset();
            RebuildItems();

            if (_items.Count == 0)
            {
                _hasSelection = false;
                StopWatchingFocus();
                Coordinator?.Deactivate(this);
                return;
            }

            // Become the window's sole selection owner, then highlight everything.
            Coordinator?.Activate(this);

            var last = _items.Count - 1;
            ApplySelection(0, 0, last, _items[last].ContentLength);

            _hasSelection = true;
            BeginWatchingFocus();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Reset()
        {
            _candidate = false;
            _selecting = false;
            _captured = false;
            _pointer = null;
            _anchor = null;
            _anchorPosition = 0;
            _selectedRanges.Clear();
        }

        // --- gesture ------------------------------------------------------------

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Mouse: left button only. Touch/pen: any contact.
            var point = e.GetCurrentPoint(_root);
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Take over as the window's selection owner: pressing in this bubble clears any
            // other bubble's selection (single selection across the chat). Done before our own
            // clear so the coordinator's callback into our Deactivate is a no-op.
            Coordinator?.Activate(this);

            // A new gesture clears the previous selection's highlight.
            foreach (var item in _items)
            {
                item.ClearSelection();
            }

            Reset();
            _hasSelection = false;
            StopWatchingFocus();
            RebuildItems();

            // Anchor at the press point. If the press isn't on a selectable (media,
            // gap, ...), do nothing — let the press through.
            if (!ResolvePosition(e, point.Position, out _anchor, out _anchorPosition, out var exact))
            {
                _anchor = null;
                return;
            }

            _pointer = e.Pointer;
            _pressPoint = point.Position;

            // Multi-tap (mouse only): 2 -> word, 3 -> paragraph; otherwise a caret-drag.
            _granularity = TextSelectionGranularity.Character;
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                var now = Logger.TickCount;
                var near = Math.Abs(point.Position.X - _lastTapPoint.X) < TapSlop
                        && Math.Abs(point.Position.Y - _lastTapPoint.Y) < TapSlop;
                _tapCount = near && now - _lastTapTime <= DoubleClickTime ? (_tapCount % 3) + 1 : 1;
                _lastTapTime = now;
                _lastTapPoint = point.Position;
                _granularity = _tapCount switch
                {
                    2 => TextSelectionGranularity.Word,
                    3 => TextSelectionGranularity.Paragraph,
                    _ => TextSelectionGranularity.Character
                };
            }
            else
            {
                _tapCount = 1;
            }

            // Word/paragraph selection only applies to a DIRECT hit — a press in a gap (clamped
            // to the nearest block) falls back to a plain caret-drag instead of word-selecting
            // the closest control.
            if (!exact)
            {
                _granularity = TextSelectionGranularity.Character;
            }

            // Anchor end snapped to the granularity (Character -> (pos, pos)).
            _anchor.GetSelectionBoundary(_anchorPosition, _granularity, out _anchorStart, out _anchorEnd);

            // Capture on the ROOT so the whole gesture (moves/release, even outside the
            // tree) is delivered here and no child runs its own selection.
            _captured = _root.CapturePointer(e.Pointer);

            if (_granularity == TextSelectionGranularity.Character)
            {
                _candidate = true;
            }
            else
            {
                // Word/paragraph selects immediately on press (no drag needed); a following
                // drag then keeps extending by whole words/paragraphs.
                _selecting = true;
                _owner.Focus(FocusState.Pointer);

                var index = _items.IndexOf(_anchor);
                ApplySelection(index, _anchorStart, index, _anchorEnd);
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if ((!_candidate && !_selecting) || e.Pointer.PointerId != _pointer?.PointerId)
            {
                return;
            }

            var rootPoint = e.GetCurrentPoint(_root).Position;

            if (_candidate)
            {
                // Promote to a real selection only once the user actually drags.
                if (Math.Abs(rootPoint.X - _pressPoint.X) < DragThreshold &&
                    Math.Abs(rootPoint.Y - _pressPoint.Y) < DragThreshold)
                {
                    return;
                }

                _candidate = false;
                _selecting = true;
                _owner.Focus(FocusState.Pointer);
            }

            UpdateSelection(e, rootPoint);
            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _pointer?.PointerId)
            {
                return;
            }

            var selecting = _selecting;

            if (_captured)
            {
                _root.ReleasePointerCapture(e.Pointer);
            }

            _candidate = false;
            _selecting = false;
            _captured = false;
            _pointer = null;

            if (selecting)
            {
                // Keep the highlight + _anchor so the selection persists and can be
                // copied. Now that it's finalized, watch for focus moving elsewhere
                // (clicked a control outside, window deactivated, ...) to clear it.
                _hasSelection = true;
                BeginWatchingFocus();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _candidate = false;
            _selecting = false;
            _captured = false;
            _pointer = null;
        }

        // --- "lost focus" clearing ---------------------------------------------
        // A finalized selection doesn't take focus, so we start listening only after
        // it's made: the first focus loss means focus went somewhere else (a control
        // outside the read view, or the window deactivated), which is our cue to clear.

        private void BeginWatchingFocus()
        {
            if (_watchingFocus)
            {
                return;
            }

            _watchingFocus = true;
            //FocusManager.LostFocus += OnFocusLost;
            FocusManager.GotFocus += OnGotFocus;
        }

        private void StopWatchingFocus()
        {
            if (!_watchingFocus)
            {
                return;
            }

            _watchingFocus = false;
            //FocusManager.LostFocus -= OnFocusLost;
            FocusManager.GotFocus -= OnGotFocus;
        }

        private void OnFocusLost(object sender, FocusManagerLostFocusEventArgs e)
        {
            ClearSelection();
        }

        private void OnGotFocus(object sender, FocusManagerGotFocusEventArgs e)
        {
            if (e.NewFocusedElement is RichEditBox or TextBox)
            {
                ClearSelection();
            }
        }

        // --- selection ----------------------------------------------------------

        private void UpdateSelection(PointerRoutedEventArgs e, Point rootPoint)
        {
            // Drag still clamps to the nearest block (exact is irrelevant here).
            if (!ResolvePosition(e, rootPoint, out var current, out var currentPosition, out _) || _anchor == null)
            {
                return;
            }

            var anchorIndex = _items.IndexOf(_anchor);
            var currentIndex = _items.IndexOf(current);
            if (anchorIndex < 0 || currentIndex < 0)
            {
                return;
            }

            // Snap the moving end to the gesture granularity, then union with the (fixed)
            // anchor range in document order. Character granularity is a no-op snap, so this
            // reduces to the plain caret-to-caret behaviour.
            current.GetSelectionBoundary(currentPosition, _granularity, out var curStart, out var curEnd);

            int startPos, endPos, startIndex, endIndex;
            if (currentIndex > anchorIndex)
            {
                startIndex = anchorIndex; startPos = _anchorStart;
                endIndex = currentIndex; endPos = curEnd;
            }
            else if (currentIndex < anchorIndex)
            {
                startIndex = currentIndex; startPos = curStart;
                endIndex = anchorIndex; endPos = _anchorEnd;
            }
            else
            {
                // Same control: union so the anchor's word/paragraph stays covered.
                startIndex = endIndex = anchorIndex;
                startPos = Math.Min(_anchorStart, curStart);
                endPos = Math.Max(_anchorEnd, curEnd);
            }

            ApplySelection(startIndex, startPos, endIndex, endPos);
        }

        // Highlights [startIndex:startPos .. endIndex:endPos] across the items (clearing the
        // rest) and records the per-control ranges for copy.
        private void ApplySelection(int startIndex, int startPos, int endIndex, int endPos)
        {
            _selectedRanges.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (i < startIndex || i > endIndex)
                {
                    item.ClearSelection();
                    continue;
                }

                int from, to;
                if (startIndex == endIndex)
                {
                    from = startPos; to = endPos;
                }
                else if (i == startIndex)
                {
                    from = startPos; to = item.ContentLength;
                }
                else if (i == endIndex)
                {
                    from = 0; to = endPos;
                }
                else
                {
                    from = 0; to = item.ContentLength;
                }

                item.Select(from, to);
                _selectedRanges.Add((item, from, to));
            }
        }

        public void CopySelectionToClipboard()
        {
            MessageHelper.CopyText(_owner.XamlRoot, GetSelectedText());
        }

        /// <summary>
        /// The whole selection as a single <see cref="FormattedText"/> — each selected
        /// control's slice in document order, joined by newlines. Null when empty.
        /// </summary>
        public FormattedText GetSelectedText()
        {
            if (_selectedRanges.Count == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            var entities = new List<TextEntity>();

            foreach (var range in _selectedRanges)
            {
                var part = range.Item.GetSelectedText(range.Start, range.End);
                if (part == null || string.IsNullOrEmpty(part.Text))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                var baseOffset = builder.Length;
                builder.Append(part.Text);

                if (part.Entities != null)
                {
                    foreach (var entity in part.Entities)
                    {
                        entities.Add(new TextEntity(baseOffset + entity.Offset, entity.Length, entity.Type));
                    }
                }
            }

            return builder.Length > 0 ? new FormattedText(builder.ToString(), entities) : null;
        }

        /// <summary>
        /// The selection mapped back to the ORIGINAL source text (a message's
        /// <see cref="FormattedText"/>) plus the absolute source offset of its start — for a
        /// reply quote, which references the original text by position. Unlike
        /// <see cref="GetSelectedText"/> (the visual copy joined with newlines), this returns
        /// the contiguous original slice with no virtual breaks. Assumes the selection is
        /// within a single source (the quoting case); null when empty.
        /// </summary>
        public FormattedText GetSelectedSourceText(out int position)
        {
            position = 0;
            if (_selectedRanges.Count == 0)
            {
                return null;
            }

            // Overall selection range in SOURCE offsets (all selected blocks share the source).
            var from = int.MaxValue;
            var to = int.MinValue;

            foreach (var range in _selectedRanges)
            {
                var start = range.Item.GetSourceOffset(range.Start);
                var end = range.Item.GetSourceOffset(range.End);

                if (start < from) from = start;
                if (end > to) to = end;
            }

            if (to <= from)
            {
                return null;
            }

            position = from;
            return _selectedRanges[0].Item.GetSourceText(from, to);
        }

        // Resolve a pointer to (control, position). Directly over a selectable -> use
        // it. Otherwise clamp sensibly: within a row (table cells share a vertical
        // band) clamp HORIZONTALLY to the nearest cell; in a vertical gap clamp to the
        // control above; above everything -> start of the first control.
        // `exact` is true only when the pointer is DIRECTLY over a selectable; the clamp
        // fallbacks (gap/row/above) return true with exact=false.
        private bool ResolvePosition(PointerRoutedEventArgs e, Point rootPoint, out ISelectableControl control, out int position, out bool exact)
        {
            control = null;
            position = 0;
            exact = false;
            if (_items.Count == 0)
            {
                return false;
            }

            ISelectableControl firstValid = null;  // first laid-out control (above-all clamp)
            ISelectableControl clampBefore = null;  // last control fully above the pointer
            ISelectableControl rowFirst = null;     // first control in the pointer's row (Y band)
            ISelectableControl rowLeftOf = null;    // last row control entirely left of the pointer

            foreach (var item in _items)
            {
                var element = (FrameworkElement)item;
                if (element.ActualHeight <= 0 || element.ActualWidth <= 0)
                {
                    continue; // not laid out / effectively hidden — never resolve to it
                }

                firstValid ??= item;
                var local = e.GetCurrentPoint(element).Position;

                if (local.Y >= 0 && local.Y <= element.ActualHeight)
                {
                    // in this control's vertical band (its row)
                    rowFirst ??= item;

                    if (local.X >= 0 && local.X <= element.ActualWidth)
                    {
                        control = item; // directly over it
                        position = item.GetPositionFromPoint(local);
                        exact = true;
                        return true;
                    }

                    if (local.X > element.ActualWidth)
                    {
                        rowLeftOf = item; // pointer is to the right of this cell
                    }
                }
                else if (local.Y > element.ActualHeight)
                {
                    clampBefore = item; // pointer is below this control
                }
            }

            // within a row but not over a cell: clamp horizontally
            if (rowLeftOf != null)
            {
                control = rowLeftOf;
                //position = rowLeftOf.ContentLength; // to the right of this cell -> its end
                position = rowLeftOf.GetPositionFromPoint(e.GetCurrentPoint((FrameworkElement)rowLeftOf).Position);
                return true;
            }
            if (rowFirst != null)
            {
                control = rowFirst;
                //position = 0; // left of the first cell in the row -> its start
                position = rowFirst.GetPositionFromPoint(e.GetCurrentPoint((FrameworkElement)rowFirst).Position);
                return true;
            }

            // Vertical clamp. Hit-test the clamped block with the pointer's actual position
            // (its Y is out of the block's band): RichTextBlock returns the CLOSEST position,
            // i.e. the block's last/first line at the pointer's X — so dragging left/right past
            // the last (or above the first) block keeps moving that edge instead of snapping to
            // the very end/start.
            if (clampBefore != null)
            {
                control = clampBefore;
                position = clampBefore.GetPositionFromPoint(e.GetCurrentPoint((FrameworkElement)clampBefore).Position);
                return true;
            }
            if (firstValid != null)
            {
                control = firstValid; // above everything -> its first line at the pointer's X
                position = firstValid.GetPositionFromPoint(e.GetCurrentPoint((FrameworkElement)firstValid).Position);
                return true;
            }

            return false;
        }

        // --- working tree -------------------------------------------------------

        private void RebuildItems()
        {
            _items.Clear();
            Collect(_root);
        }

        private void Collect(DependencyObject node)
        {
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);

                // Skip collapsed subtrees: a hidden selectable must not participate, and
                // its stale (zero) geometry would corrupt hit-testing. Skipping at the
                // collapsed ancestor also covers selectables nested below it.
                if (child is FrameworkElement fe && fe.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                if (child is ISelectableControl selectable)
                {
                    // Only controls that opt into manager-driven (Extended) selection;
                    // a selectable control owns its own content, so never descend into it.
                    if (selectable.IsSelectionEnabled)
                    {
                        _items.Add(selectable);
                    }

                    continue;
                }

                Collect(child);
            }
        }
    }
}
