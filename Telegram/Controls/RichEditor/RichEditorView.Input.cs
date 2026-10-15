//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.RichEditor
{
    public sealed partial class RichEditorView
    {
        // Mouse and pen. Touch places the caret on Tapped instead, so a finger that pans the
        // document does not drag a selection out of it.
        private bool _isDragging;
        private TextSelectionGranularity _dragGranularity;
        private RichPosition _dragOrigin;
        private RichPosition _dragOriginEnd;

        private int _clickCount;
        private ulong _lastClickTime;
        private Point _lastClickPoint;

        // Where vertical movement aims: the column the caret started a run of Up and Down in,
        // so passing a short line does not pull it to the left for the rest of the run.
        private double _goalX = double.NaN;

        // Whether the selection was made by touch: only then are the handles shown.
        private bool _touchSelection;

        private CoreCursor _beamCursor;
        private CoreCursor _arrowCursor;
        private bool _beam;

        /// <summary>Raised for a context menu: right click, Shift+F10, the menu key, or a touch on the selection.</summary>
        public event EventHandler<RichMenuRequestedEventArgs> MenuRequested;

        /// <summary>
        /// Raised on a double click or Enter on a block selected whole: a formula opens its
        /// editor, a button row its buttons. Unhandled, Enter adds a paragraph after the block.
        /// </summary>
        public event EventHandler<RichNodeActivatedEventArgs> NodeActivated;

        /// <summary>Raised by the + on a picture or a collage: the host picks media to add to it.</summary>
        public event EventHandler<RichNodeActivatedEventArgs> MediaAddRequested;

        /// <summary>Raised on Ctrl+K, for the host to ask for the link.</summary>
        public event EventHandler LinkRequested;

        /// <summary>Raised when clipboard content is not text - an image, files - for the host to turn into media.</summary>
        public event EventHandler<RichPasteRequestedEventArgs> PasteRequested;

        private void HookPointer(FrameworkElement root)
        {
            root.PointerPressed += OnPointerPressed;
            root.PointerMoved += OnPointerMoved;
            root.PointerReleased += OnPointerReleased;
            root.PointerCaptureLost += OnPointerCaptureLost;
            root.PointerExited += OnPointerExited;
            root.Tapped += OnTapped;
            root.DoubleTapped += OnDoubleTapped;
            root.Holding += OnHolding;
            root.DragOver += OnDragOver;
            root.Drop += OnDrop;
            root.LayoutUpdated += OnLayoutUpdated;
            ContextRequested += OnContextRequested;

            root.AllowDrop = true;
        }

        private void UnhookPointer(FrameworkElement root)
        {
            root.PointerPressed -= OnPointerPressed;
            root.PointerMoved -= OnPointerMoved;
            root.PointerReleased -= OnPointerReleased;
            root.PointerCaptureLost -= OnPointerCaptureLost;
            root.PointerExited -= OnPointerExited;
            root.Tapped -= OnTapped;
            root.DoubleTapped -= OnDoubleTapped;
            root.Holding -= OnHolding;
            root.DragOver -= OnDragOver;
            root.Drop -= OnDrop;
            root.LayoutUpdated -= OnLayoutUpdated;
            ContextRequested -= OnContextRequested;
        }

        // =====================================================================================
        // Hit testing
        // =====================================================================================

        /// <summary>
        /// The position under <paramref name="point"/> (root coordinates), or the block if the
        /// point is on a block that is selected whole. Off the text, the nearest leaf answers:
        /// above or below a line is that line, which is what makes dragging past the end of a
        /// paragraph select to its end instead of jumping.
        /// </summary>
        internal bool HitTest(Point point, out RichPosition position, out RichBlock node)
        {
            position = default;
            node = null;

            if (Surface == null)
            {
                return false;
            }

            // A tile before its collage: the tile is inside the collage's bounds, and a click on
            // one means that photo.
            foreach (var pair in _itemViews)
            {
                var tile = pair.Value;
                if (tile.ActualWidth <= 0 || tile.Visibility != Visibility.Visible)
                {
                    continue;
                }

                if (Bounds(tile).Contains(point))
                {
                    node = pair.Key;
                    position = default;
                    return true;
                }
            }

            foreach (var pair in _nodeViews)
            {
                var element = pair.Value.NodeElement;
                if (element.ActualWidth <= 0)
                {
                    continue;
                }

                var bounds = Bounds(element);
                if (bounds.Contains(point))
                {
                    node = pair.Key;
                    position = default;
                    return true;
                }
            }

            RichLeafView best = null;
            Rect bestBounds = default;
            var bestDistance = double.MaxValue;

            foreach (var view in _leafViews.Values)
            {
                if (view.Visibility != Visibility.Visible || view.ActualHeight <= 0)
                {
                    continue;
                }

                var bounds = Bounds(view.HitTarget ?? view);

                var dy = point.Y < bounds.Top ? bounds.Top - point.Y : point.Y > bounds.Bottom ? point.Y - bounds.Bottom : 0;
                var dx = point.X < bounds.Left ? bounds.Left - point.X : point.X > bounds.Right ? point.X - bounds.Right : 0;

                // Rows first, then columns: a point beside a table cell belongs to the cell on
                // its row rather than to the paragraph nearest it on a straight line.
                var distance = dy * 4096 + dx;

                if (distance < bestDistance)
                {
                    best = view;
                    bestBounds = bounds;
                    bestDistance = distance;

                    if (distance == 0)
                    {
                        break;
                    }
                }
            }

            if (best == null)
            {
                return false;
            }

            // Into the text's own box, wherever in the target the point was: clamped there, a
            // click right of a cell's text is the end of it.
            if (best.HitTarget != null)
            {
                bestBounds = Bounds(best);
            }

            var local = new Point(
                Math.Clamp(point.X - bestBounds.X, 0, Math.Max(0, bestBounds.Width)),
                Math.Clamp(point.Y - bestBounds.Y, 0, Math.Max(0, bestBounds.Height - 1)));

            position = new RichPosition(best.Leaf, best.GetPositionFromPoint(local));
            return true;
        }

        /// <summary>
        /// A point below the last block (surface coordinates) when that block is not text: a line
        /// after it, as Down from it gives, and the caret on that line. Below text, the nearest
        /// line answers as it does anywhere else.
        /// </summary>
        internal bool TryAppendBelow(Point point)
        {
            var children = Blocks?.Children;
            if (_engine == null || children == null || children.Count == 0 || children[children.Count - 1] is not FrameworkElement last)
            {
                return false;
            }

            // Not the column's height: the scroll viewer can stretch it to the viewport, and it
            // then says nothing of where the blocks end.
            return point.Y > Bounds(last).Bottom && _engine.AppendLine();
        }

        /// <summary>The text position under a point in this control's coordinates; blocks answer nothing.</summary>
        internal bool HitTestFromControl(Point point, out RichPosition position)
        {
            position = default;

            if (Surface == null)
            {
                return false;
            }

            var local = TransformToVisual(Surface).TransformPoint(point);
            return HitTest(local, out position, out RichBlock node) && node == null;
        }

        /// <summary>
        /// Where an element is on the surface. Asked for every leaf on every pointer move while
        /// dragging, and each answer is a transform object: kept until the next layout pass,
        /// which is the only thing that moves anything - scrolling moves the surface, not what
        /// is on it.
        /// </summary>
        private Rect Bounds(FrameworkElement element)
        {
            if (_bounds.TryGetValue(element, out Rect bounds))
            {
                return bounds;
            }

            bounds = element.TransformToVisual(Surface).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            _bounds[element] = bounds;

            return bounds;
        }

        private readonly Dictionary<FrameworkElement, Rect> _bounds = new();

        private void OnLayoutUpdated(object sender, object e)
        {
            if (_bounds.Count > 0)
            {
                _bounds.Clear();
            }

            if (_caretStale)
            {
                _caretStale = false;
                UpdateCaret();
                UpdateGrippers();
            }

            UpdateComposition();

            if (_toggleStale != null)
            {
                var quote = _toggleStale;
                _toggleStale = null;
                quote.UpdateToggle();
            }
        }

        // A quote whose text changed: whether it is long enough to collapse is known after layout.
        private RichBlockQuotePresenter _toggleStale;

        // A text measures in layout, so a block whose font changed answers a caret query with
        // the old line until then - and an empty one with its old height.
        private bool _caretStale;

        // =====================================================================================
        // Mouse and pen
        // =====================================================================================

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(Surface);

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
            {
                return;
            }

            if (!point.Properties.IsLeftButtonPressed)
            {
                // A right click outside the selection moves the caret there first, so the menu
                // that follows is about what is under the pointer.
                if (point.Properties.IsRightButtonPressed && HitTest(point.Position, out RichPosition hit, out RichBlock hitNode) && !IsInSelection(hit, hitNode))
                {
                    Focus(FocusState.Pointer);

                    if (hitNode != null)
                    {
                        _engine.SelectNode(hitNode);
                    }
                    else
                    {
                        _engine.SetCaret(hit);
                    }
                }

                return;
            }

            Focus(FocusState.Pointer);

            _touchSelection = false;
            _goalX = double.NaN;

            CountClick(point);

            var shift = WindowContext.IsKeyDown(VirtualKey.Shift);

            if (!shift && TryAppendBelow(point.Position))
            {
                e.Handled = true;
                return;
            }

            if (!HitTest(point.Position, out RichPosition position, out RichBlock node))
            {
                return;
            }

            if (node != null)
            {
                _engine.SelectNode(node);

                if (_clickCount == 2 && node is RichBlockQuote quote)
                {
                    _engine.SetQuoteCollapsed(quote, false);
                }
                else if (_clickCount == 2)
                {
                    NodeActivated?.Invoke(this, new RichNodeActivatedEventArgs(node));
                }

                e.Handled = true;
                return;
            }

            if (shift)
            {
                _engine.ExtendTo(position);
                _dragGranularity = TextSelectionGranularity.Character;
                _dragOrigin = _dragOriginEnd = _engine.Anchor;
            }
            else if (_clickCount == 2)
            {
                _engine.SelectWord(position);
                _dragGranularity = TextSelectionGranularity.Word;
                _dragOrigin = _engine.Start;
                _dragOriginEnd = _engine.End;
            }
            else if (_clickCount >= 3)
            {
                _engine.SelectLeaf(position.Leaf);
                _dragGranularity = TextSelectionGranularity.Paragraph;
                _dragOrigin = _engine.Start;
                _dragOriginEnd = _engine.End;
            }
            else
            {
                _engine.SetCaret(position);
                _dragGranularity = TextSelectionGranularity.Character;
                _dragOrigin = _dragOriginEnd = position;
            }

            _isDragging = Surface.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void CountClick(PointerPoint point)
        {
            // The system's double click time: what every other text box on the machine counts by.
            _settings ??= new Windows.UI.ViewManagement.UISettings();

            var time = point.Timestamp;
            var interval = (ulong)_settings.DoubleClickTime * 1000;

            var near = Math.Abs(point.Position.X - _lastClickPoint.X) <= 4 && Math.Abs(point.Position.Y - _lastClickPoint.Y) <= 4;

            _clickCount = time - _lastClickTime <= interval && near ? _clickCount + 1 : 1;
            _lastClickTime = time;
            _lastClickPoint = point.Position;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(Surface);

            if (!_isDragging)
            {
                if (e.Pointer.PointerDeviceType != PointerDeviceType.Touch)
                {
                    UpdateCursor(point.Position);
                }

                return;
            }

            TrackAutoScroll(point.Position, ScrollDrag.Pointer);

            if (!HitTest(point.Position, out RichPosition position, out RichBlock node) || node != null)
            {
                return;
            }

            ExtendDrag(position);
            e.Handled = true;
        }

        /// <summary>
        /// Dragging after a double or triple click extends by whole words or lines, keeping the
        /// word or line first clicked inside the selection whichever way the drag goes.
        /// </summary>
        private void ExtendDrag(RichPosition position)
        {
            if (_dragGranularity == TextSelectionGranularity.Character)
            {
                _engine.Select(_dragOrigin, position);
                return;
            }

            int start;
            int end;

            if (_dragGranularity == TextSelectionGranularity.Word)
            {
                RichEditorEngine.GetWordRange(position.Leaf.Text, position.Offset, out start, out end);
            }
            else
            {
                start = 0;
                end = position.Leaf.Length;
            }

            if (RichPosition.Compare(position, _dragOrigin) < 0)
            {
                _engine.Select(_dragOriginEnd, new RichPosition(position.Leaf, start));
            }
            else
            {
                _engine.Select(_dragOrigin, new RichPosition(position.Leaf, end));
            }
        }

        // What drags near the edge: a mouse selection, or one of the two handles.
        private enum ScrollDrag
        {
            Pointer,
            StartGripper,
            EndGripper,
        }

        private DispatcherTimer _scrollTimer;
        private ScrollDrag _scrollDrag;
        private Point _scrollPoint;
        private double _scrollPointOffset;

        /// <summary>
        /// Scrolls while a drag rests near the top or bottom edge, and keeps extending the
        /// selection as the text moves under it: a pointer held still past the edge should go on
        /// selecting, not stop where the last move event left it.
        /// </summary>
        private void TrackAutoScroll(Point point, ScrollDrag drag)
        {
            if (ScrollViewer == null)
            {
                return;
            }

            _scrollPoint = point;
            _scrollPointOffset = ScrollViewer.VerticalOffset;
            _scrollDrag = drag;

            if (EdgeSpeed() != 0)
            {
                if (_scrollTimer == null)
                {
                    _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                    _scrollTimer.Tick += OnScrollTimerTick;
                }

                _scrollTimer.Start();
            }
            else
            {
                _scrollTimer?.Stop();
            }
        }

        private void StopAutoScroll()
        {
            _scrollTimer?.Stop();
        }

        /// <summary>How far to scroll this tick: faster the further past the edge, and nothing inside it.</summary>
        private double EdgeSpeed()
        {
            const double Edge = 32;
            const double Max = 24;

            // Where the point is within the viewport: it does not move while the text scrolls.
            var y = _scrollPoint.Y + Padding.Top - _scrollPointOffset;
            var viewport = ScrollViewer.ViewportHeight;

            if (y < Edge)
            {
                return -Math.Min(Max, (Edge - y) / 2);
            }

            if (y > viewport - Edge)
            {
                return Math.Min(Max, (y - (viewport - Edge)) / 2);
            }

            return 0;
        }

        private void OnScrollTimerTick(object sender, object e)
        {
            if (!_isDragging || ScrollViewer == null)
            {
                _scrollTimer.Stop();
                return;
            }

            var speed = EdgeSpeed();
            if (speed == 0)
            {
                _scrollTimer.Stop();
                return;
            }

            var from = ScrollViewer.VerticalOffset;
            var to = Math.Clamp(from + speed, 0, ScrollViewer.ScrollableHeight);

            if (to == from)
            {
                return;
            }

            ScrollViewer.ChangeView(null, to, null, true);

            // The same place in the viewport is further along the text now.
            var point = new Point(_scrollPoint.X, _scrollPoint.Y + (to - _scrollPointOffset));

            if (HitTest(point, out RichPosition position, out RichBlock node) && node == null)
            {
                ApplyDrag(position);
            }
        }

        private void ApplyDrag(RichPosition position)
        {
            switch (_scrollDrag)
            {
                case ScrollDrag.StartGripper:
                    _engine.Select(_engine.End, position);
                    break;
                case ScrollDrag.EndGripper:
                    _engine.Select(_engine.Start, position);
                    break;
                default:
                    ExtendDrag(position);
                    break;
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                StopAutoScroll();
                Surface.ReleasePointerCapture(e.Pointer);

                UpdateCaret();
                e.Handled = true;
            }
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
            StopAutoScroll();
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_beam && !_isDragging)
            {
                SetCursor(false);
            }
        }

        // UWP has no cursor per element, so the window's is set while the pointer is over text:
        // an I-beam on text, the arrow on a block that is selected whole.
        private void UpdateCursor(Point point)
        {
            var text = HitTest(point, out RichPosition position, out RichBlock node) && node == null;
            SetCursor(text);
        }

        private void SetCursor(bool beam)
        {
            if (_beam == beam)
            {
                return;
            }

            var window = CoreWindow.GetForCurrentThread();
            if (window == null)
            {
                return;
            }

            _beam = beam;

            if (beam)
            {
                window.PointerCursor = _beamCursor ??= new CoreCursor(CoreCursorType.IBeam, 0);
            }
            else
            {
                window.PointerCursor = _arrowCursor ??= new CoreCursor(CoreCursorType.Arrow, 0);
            }
        }

        // =====================================================================================
        // Touch
        // =====================================================================================

        /// <summary>
        /// A gesture that ended on a button in a block - a picture's controls, a slideshow's
        /// arrows, a checkbox. The button has acted on its own, and the gestures still bubble up
        /// to the editor, which would otherwise act on the same tap a second time.
        /// </summary>
        private bool IsFromButton(object source)
        {
            for (var element = source as DependencyObject; element != null && element != Surface; element = VisualTreeHelper.GetParent(element))
            {
                if (element is ButtonBase)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != PointerDeviceType.Touch || IsFromButton(e.OriginalSource))
            {
                return;
            }

            Focus(FocusState.Pointer);
            ShowInputPane();

            var point = e.GetPosition(Surface);
            if (TryAppendBelow(point))
            {
                e.Handled = true;
                return;
            }

            if (!HitTest(point, out RichPosition position, out RichBlock node))
            {
                return;
            }

            // A tap on the selection is a request for what to do with it.
            if (!_engine.IsSelectionEmpty && IsInSelection(position, node))
            {
                MenuRequested?.Invoke(this, new RichMenuRequestedEventArgs(point, true));
                e.Handled = true;
                return;
            }

            _touchSelection = false;
            _goalX = double.NaN;

            if (node != null)
            {
                _engine.SelectNode(node);
            }
            else
            {
                _engine.SetCaret(position);
            }

            e.Handled = true;
        }

        private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != PointerDeviceType.Touch || IsFromButton(e.OriginalSource))
            {
                return;
            }

            if (HitTest(e.GetPosition(Surface), out RichPosition position, out RichBlock node) && node == null)
            {
                _touchSelection = true;
                _engine.SelectWord(position);
                e.Handled = true;
            }
        }

        private void OnHolding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState != HoldingState.Started || e.PointerDeviceType != PointerDeviceType.Touch || IsFromButton(e.OriginalSource))
            {
                return;
            }

            Focus(FocusState.Pointer);

            var point = e.GetPosition(Surface);
            if (HitTest(point, out RichPosition position, out RichBlock node))
            {
                if (node != null)
                {
                    _engine.SelectNode(node);
                }
                else
                {
                    _touchSelection = true;
                    _engine.SelectWord(position);
                }

                MenuRequested?.Invoke(this, new RichMenuRequestedEventArgs(point, true));
            }

            e.Handled = true;
        }

        private bool IsInSelection(RichPosition position, RichBlock node)
        {
            if (_engine.SelectedNode != null)
            {
                return ReferenceEquals(node, _engine.SelectedNode);
            }

            if (node != null || !position.IsValid || _engine.IsSelectionEmpty)
            {
                return false;
            }

            return RichPosition.Compare(position, _engine.Start) >= 0 && RichPosition.Compare(position, _engine.End) <= 0;
        }

        // The handles: shown under the two ends of a touch selection, and dragged to move them.
        private void HookGripper(RichSelectionGripper gripper)
        {
            gripper.ManipulationStarted += OnGripperManipulationStarted;
            gripper.ManipulationDelta += OnGripperManipulationDelta;
            gripper.ManipulationCompleted += OnGripperManipulationCompleted;
        }

        private void UnhookGripper(RichSelectionGripper gripper)
        {
            gripper.ManipulationStarted -= OnGripperManipulationStarted;
            gripper.ManipulationDelta -= OnGripperManipulationDelta;
            gripper.ManipulationCompleted -= OnGripperManipulationCompleted;
        }

        private Point _gripperOrigin;
        private double _gripperScrollOrigin;

        private void OnGripperManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            var gripper = (FrameworkElement)sender;

            // The point on the line the handle hangs from, not the handle's own centre.
            _gripperOrigin = new Point(Canvas.GetLeft(gripper) + gripper.ActualWidth / 2, Canvas.GetTop(gripper) - 4);
            _gripperScrollOrigin = ScrollViewer?.VerticalOffset ?? 0;
            _isDragging = true;
            e.Handled = true;
        }

        private void OnGripperManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // The translation is in screen space; whatever scrolled since the drag began moved the
            // text under the finger by as much.
            var scrolled = (ScrollViewer?.VerticalOffset ?? 0) - _gripperScrollOrigin;
            var point = new Point(_gripperOrigin.X + e.Cumulative.Translation.X, _gripperOrigin.Y + e.Cumulative.Translation.Y + scrolled);

            var drag = ReferenceEquals(sender, StartGripper) ? ScrollDrag.StartGripper : ScrollDrag.EndGripper;
            TrackAutoScroll(point, drag);

            if (!HitTest(point, out RichPosition position, out RichBlock node) || node != null)
            {
                return;
            }

            // The other end stays where it is; the dragged end follows the finger, and the two
            // may cross, as they can with a mouse.
            _scrollDrag = drag;
            ApplyDrag(position);

            e.Handled = true;
        }

        private void OnGripperManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            _isDragging = false;
            StopAutoScroll();
            UpdateGrippers();
            MenuRequested?.Invoke(this, new RichMenuRequestedEventArgs(_gripperOrigin, true));
            e.Handled = true;
        }

        private void UpdateGrippers()
        {
            if (StartGripper == null)
            {
                return;
            }

            var visible = _touchSelection && !_engine.IsSelectionEmpty && _engine.SelectedNode == null && _hasFocus;
            if (!visible)
            {
                StartGripper.Visibility = Visibility.Collapsed;
                EndGripper.Visibility = Visibility.Collapsed;
                return;
            }

            PlaceGripper(StartGripper, _engine.Start);
            PlaceGripper(EndGripper, _engine.End);
        }

        private void PlaceGripper(RichSelectionGripper gripper, RichPosition position)
        {
            if (!_leafViews.TryGetValue(position.Leaf, out RichLeafView view))
            {
                gripper.Visibility = Visibility.Collapsed;
                return;
            }

            var rect = view.GetCaretRect(position.Offset);
            var origin = view.TransformToVisual(Surface).TransformPoint(new Point(rect.X, rect.Y + rect.Height));

            Canvas.SetLeft(gripper, origin.X - gripper.Width / 2);
            Canvas.SetTop(gripper, origin.Y);
            gripper.Visibility = Visibility.Visible;
        }

        // =====================================================================================
        // Drag and drop: files and images become media where they are dropped, text is typed
        // there. Media is the host's to prepare, so it is handed over like a paste.
        // =====================================================================================

        private void OnDragOver(object sender, DragEventArgs e)
        {
            var data = e.DataView;
            if (data.Contains(StandardDataFormats.StorageItems) || data.Contains(StandardDataFormats.Bitmap) || data.Contains(StandardDataFormats.Text))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;

                // The caret follows the pointer, so where the drop lands is visible before it does.
                if (HitTest(e.GetPosition(Surface), out RichPosition position, out RichBlock node) && node == null)
                {
                    _engine.SetCaret(position);
                }
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            var data = e.DataView;

            if (HitTest(e.GetPosition(Surface), out RichPosition position, out RichBlock node))
            {
                if (node != null)
                {
                    _engine.SelectNode(node);
                }
                else
                {
                    _engine.SetCaret(position);
                }
            }

            Focus(FocusState.Pointer);

            if (data.Contains(StandardDataFormats.StorageItems) || data.Contains(StandardDataFormats.Bitmap))
            {
                PasteRequested?.Invoke(this, new RichPasteRequestedEventArgs(data));
                return;
            }

            if (data.Contains(StandardDataFormats.Text))
            {
                var deferral = e.GetDeferral();

                try
                {
                    InsertPlainText(await data.GetTextAsync());
                }
                catch
                {
                    // The source went away before handing the text over.
                }
                finally
                {
                    deferral.Complete();
                }
            }
        }

        // =====================================================================================
        // Context menu
        // =====================================================================================

        /// <summary>The … on a picture: the menu a right click on it opens, under the button.</summary>
        internal void RequestMediaMenu(RichBlock node, FrameworkElement anchor)
        {
            _engine.SelectNode(node);

            var bounds = Bounds(anchor);
            MenuRequested?.Invoke(this, new RichMenuRequestedEventArgs(new Point(bounds.Left, bounds.Bottom), false) { IsBlockMenu = true });
        }

        internal void RequestMediaAdd(RichBlock block)
        {
            MediaAddRequested?.Invoke(this, new RichNodeActivatedEventArgs(block));
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var keyboard = !args.TryGetPosition(Surface, out Point point);
            if (keyboard)
            {
                // From the keyboard: at the caret, or under the selected block.
                var caret = GetCaretRect();
                point = caret.IsEmpty ? new Point(0, 0) : new Point(caret.X, caret.Bottom);
            }

            MenuRequested?.Invoke(this, new RichMenuRequestedEventArgs(point, false) { IsKeyboard = keyboard });
            args.Handled = true;
        }

        /// <summary>
        /// The host shows its menu from here; the editor keeps its selection drawn while it is open.
        /// One opened by pointer is transient, so the editor keeps focus and its caret; one opened
        /// from the keyboard takes focus, or its items could not be reached.
        /// </summary>
        public void ShowMenu(FlyoutBase flyout, Point point, bool touch = false, bool keyboard = false)
        {
            _menuOpen = true;
            flyout.Closed += OnMenuClosed;

            var mode = keyboard ? FlyoutShowMode.Standard : FlyoutShowMode.Transient;

            // By touch the menu goes above the selection, where the finger is not.
            if (touch && TryGetSelectionTop(out Point top))
            {
                flyout.ShowAt(Surface, new FlyoutShowOptions
                {
                    Position = top,
                    Placement = FlyoutPlacementMode.Top,
                    ShowMode = mode
                });

                return;
            }

            flyout.ShowAt(Surface, new FlyoutShowOptions
            {
                Position = point,
                ShowMode = mode
            });
        }

        private bool TryGetSelectionTop(out Point point)
        {
            point = default;

            if (_engine.SelectedNode is RichBlock node && _nodeViews.TryGetValue(node, out RichBlockPresenter presenter))
            {
                var bounds = Bounds(presenter.NodeElement);
                point = new Point(bounds.X + bounds.Width / 2, bounds.Top - 8);
                return true;
            }

            var start = _engine.Start;
            if (!_leafViews.TryGetValue(start.Leaf, out RichLeafView view))
            {
                return false;
            }

            var caret = view.GetCaretRect(start.Offset);
            var origin = view.TransformToVisual(Surface).TransformPoint(new Point(caret.X, caret.Y));

            point = new Point(origin.X, origin.Y - 8);
            return true;
        }

        private void OnMenuClosed(object sender, object e)
        {
            _menuOpen = false;
            ((FlyoutBase)sender).Closed -= OnMenuClosed;

            Focus(FocusState.Programmatic);
        }

        // =====================================================================================
        // Keyboard
        // =====================================================================================

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            // 229 is VK_PROCESSKEY: a key the IME is consuming, which it reports through the edit context.
            if (_engine == null || e.Key == (VirtualKey)229 || _composing)
            {
                base.OnKeyDown(e);
                return;
            }

            var ctrl = WindowContext.IsKeyDown(VirtualKey.Control);
            var shift = WindowContext.IsKeyDown(VirtualKey.Shift);
            var alt = WindowContext.IsKeyDown(VirtualKey.Menu);

            e.Handled = ProcessSuggestionKey(e.Key, ctrl, shift, alt) || HandleKey(e.Key, ctrl, shift, alt);

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        private bool HandleKey(VirtualKey key, bool ctrl, bool shift, bool alt)
        {
            var vertical = key is VirtualKey.Up or VirtualKey.Down or VirtualKey.PageUp or VirtualKey.PageDown;
            if (!vertical)
            {
                _goalX = double.NaN;
            }

            if (key is not (VirtualKey.Shift or VirtualKey.Control or VirtualKey.Menu))
            {
                _touchSelection = false;
            }

            switch (key)
            {
                // A photo of a collage moves along it.
                case VirtualKey.Left when alt && _engine.SelectedNode?.Parent is RichMediaGroupBlock:
                    _engine.MoveBlock(-1);
                    return true;
                case VirtualKey.Right when alt && _engine.SelectedNode?.Parent is RichMediaGroupBlock:
                    _engine.MoveBlock(1);
                    return true;

                case VirtualKey.Left when alt:
                case VirtualKey.Right when alt:
                    return false;

                case VirtualKey.Left:
                case VirtualKey.Right:
                    {
                        var direction = key == VirtualKey.Left ? -1 : 1;

                        // In a line that reads right to left, Left goes forward.
                        if (IsRightToLeft(_engine.Focus.Leaf))
                        {
                            direction = -direction;
                        }

                        if (ctrl)
                        {
                            _engine.MoveWord(direction, shift);
                        }
                        else
                        {
                            _engine.MoveCharacter(direction, shift);
                        }

                        return true;
                    }

                case VirtualKey.Up when alt && !ctrl:
                    _engine.MoveBlock(-1);
                    return true;
                case VirtualKey.Down when alt && !ctrl:
                    _engine.MoveBlock(1);
                    return true;

                case VirtualKey.Up:
                    MoveLine(-1, shift, 1);
                    return true;
                case VirtualKey.Down:
                    MoveLine(1, shift, 1);
                    return true;
                case VirtualKey.PageUp:
                    MoveLine(-1, shift, PageLines());
                    return true;
                case VirtualKey.PageDown:
                    MoveLine(1, shift, PageLines());
                    return true;

                case VirtualKey.Home:
                    if (ctrl)
                    {
                        _engine.MoveToDocumentEdge(-1, shift);
                    }
                    else
                    {
                        MoveToLineEdge(-1, shift);
                    }
                    return true;
                case VirtualKey.End:
                    if (ctrl)
                    {
                        _engine.MoveToDocumentEdge(1, shift);
                    }
                    else
                    {
                        MoveToLineEdge(1, shift);
                    }
                    return true;

                case VirtualKey.Back:
                    _engine.DeleteBackward(ctrl);
                    return true;
                case VirtualKey.Delete when shift && !ctrl:
                    Cut();
                    return true;
                case VirtualKey.Delete:
                    _engine.DeleteForward(ctrl);
                    return true;

                case VirtualKey.Enter when ctrl && shift:
                    // The checkbox of a checklist item, or a collapsible block's open state:
                    // both are otherwise only a click.
                    return ToggleAtCaret();
                case VirtualKey.Enter when shift:
                    _engine.InsertLineBreak();
                    return true;
                case VirtualKey.Enter when _engine.SelectedNode is RichBlock node:
                    {
                        var args = new RichNodeActivatedEventArgs(node);
                        NodeActivated?.Invoke(this, args);

                        if (!args.Handled)
                        {
                            _engine.InsertParagraph();
                        }

                        return true;
                    }
                case VirtualKey.Enter:
                    _engine.InsertParagraph();
                    return true;

                case VirtualKey.Tab when !ctrl && !alt:
                    return HandleTab(shift);

                case VirtualKey.Escape:
                    if (!_engine.IsSelectionEmpty)
                    {
                        _engine.SetCaret(_engine.Focus);
                        return true;
                    }
                    return false;

                case VirtualKey.Insert when shift && !ctrl:
                    Paste(false);
                    return true;
                case VirtualKey.Insert when ctrl && !shift:
                    Copy();
                    return true;
            }

            if (ctrl && !alt)
            {
                switch (key)
                {
                    case VirtualKey.A:
                        _engine.SelectAll();
                        return true;
                    case VirtualKey.C:
                        Copy();
                        return true;
                    case VirtualKey.X when !shift:
                        Cut();
                        return true;
                    case VirtualKey.V:
                        Paste(shift);
                        return true;
                    case VirtualKey.Z when shift:
                    case VirtualKey.Y:
                        _engine.Redo();
                        return true;
                    case VirtualKey.Z:
                        _engine.Undo();
                        return true;
                    case VirtualKey.B:
                        _engine.ToggleMark(RichMarks.Bold);
                        return true;
                    case VirtualKey.I:
                        _engine.ToggleMark(RichMarks.Italic);
                        return true;
                    case VirtualKey.U:
                        _engine.ToggleMark(RichMarks.Underline);
                        return true;
                    case VirtualKey.X when shift:
                        _engine.ToggleMark(RichMarks.Strikethrough);
                        return true;
                    case VirtualKey.M when shift:
                        _engine.ToggleMark(RichMarks.Monospace);
                        return true;
                    case VirtualKey.P when shift:
                        _engine.ToggleMark(RichMarks.Spoiler);
                        return true;
                    case VirtualKey.N when shift:
                        _engine.ClearFormatting();
                        return true;
                    case VirtualKey.K:
                        LinkRequested?.Invoke(this, EventArgs.Empty);
                        return true;
                    case (VirtualKey)187 when shift:
                        _engine.ToggleMark(RichMarks.Superscript);
                        return true;
                    case (VirtualKey)187:
                        _engine.ToggleMark(RichMarks.Subscript);
                        return true;
                }
            }

            // Ctrl+Alt+0 to 6: paragraph and the six headings, as in word processors.
            if (ctrl && alt && key >= VirtualKey.Number0 && key <= VirtualKey.Number6)
            {
                var level = key - VirtualKey.Number0;
                _engine.SetTextKind(level == 0 ? RichBlockKind.Paragraph : RichBlockKind.Heading, Math.Max(1, level));
                return true;
            }

            return false;
        }

        private bool ToggleAtCaret()
        {
            if (_engine.SelectedNode is RichBlockQuote { IsCollapsed: true } collapsed)
            {
                _engine.SetQuoteCollapsed(collapsed, false);
                Peer?.Announce(Strings.AccDescrIVExpanded);
                return true;
            }

            var leaf = _engine.Focus.Leaf;

            if (_engine.SelectedNode == null && leaf.Owner?.Parent is RichBlockQuote { IsCollapsible: true } quote)
            {
                _engine.SetQuoteCollapsed(quote, true);
                Peer?.Announce(Strings.AccDescrIVCollapsed);
                return true;
            }

            if (leaf.Owner is RichDetailsBlock details && leaf.Role == RichLeafRole.Header)
            {
                _engine.SetDetailsOpen(details, !details.IsOpen);
                Peer?.Announce(DescribeLine(leaf));
                return true;
            }

            var item = _engine.GetItemOf(leaf.Owner, out RichListBlock list);
            if (item != null && list.Style == RichListStyle.Checklist)
            {
                _engine.SetChecked(item, !item.IsChecked);
                Peer?.Announce(item.IsChecked ? Strings.AccDescrChecked : Strings.AccDescrNotChecked);
                return true;
            }

            return false;
        }

        private bool HandleTab(bool shift)
        {
            var leaf = _engine.Focus.Leaf;

            if (leaf.Role == RichLeafRole.Cell)
            {
                // Tab walks the cells, which are consecutive leaves; past the last one it leaves
                // the table, as the caption follows.
                var target = leaf.Index + (shift ? -1 : 1);
                var leaves = _engine.Document.Leaves;

                if (target >= 0 && target < leaves.Count && leaves[target].Owner == leaf.Owner)
                {
                    _engine.SelectLeaf(leaves[target]);
                    return true;
                }

                return false;
            }

            if (_engine.GetItemOf(leaf.Owner, out _) != null)
            {
                if (shift)
                {
                    _engine.Outdent();
                }
                else
                {
                    _engine.Indent((int)(_clientService?.Options.RichMessageDepthMax ?? 16));
                }

                return true;
            }

            // Anywhere else Tab moves focus on, as it does out of any other control: an editor
            // that swallows it is one a keyboard user cannot leave.
            return false;
        }

        private bool IsRightToLeft(RichTextLeaf leaf)
        {
            // The line's own direction, not the element's: the text block is pinned left to
            // right, and an Arabic line in an English article still reads right to left.
            return _leafViews.TryGetValue(leaf, out RichLeafView view)
                && view.TextBlock?.IsRightToLeft == true;
        }

        private int PageLines()
        {
            var height = ScrollViewer?.ViewportHeight ?? 0;
            return Math.Max(1, (int)(height / 24) - 1);
        }

        /// <summary>
        /// Up and Down: the position on the line above or below, at the column the run of
        /// vertical moves started in. From a block selected whole, the move is from its edge.
        /// </summary>
        private void MoveLine(int direction, bool extend, int lines)
        {
            for (int i = 0; i < lines; i++)
            {
                if (!MoveLineOnce(direction, extend))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// One line up or down. Within the text the caret is in, the line next to it; past that,
        /// the nearest thing wholly beyond the line - the closest in that direction, and of those
        /// the one under the column the run of moves started in, which keeps a table's column.
        /// Searched for rather than probed for: a point just past the line can still be nearest
        /// to the line it left, in the gap between two blocks or in a cell's padding.
        /// </summary>
        private bool MoveLineOnce(int direction, bool extend)
        {
            Surface.UpdateLayout();

            Rect from;
            RichLeafView current = null;
            RichBlock currentNode = null;

            if (_engine.SelectedNode != null && !extend && TryGetNodeBounds(_engine.SelectedNode, out Rect nodeBounds))
            {
                from = nodeBounds;
                currentNode = _engine.SelectedNode;

                if (double.IsNaN(_goalX))
                {
                    _goalX = from.X;
                }
            }
            else
            {
                var focus = _engine.Focus;
                if (!_leafViews.TryGetValue(focus.Leaf, out current))
                {
                    return false;
                }

                var caret = current.GetCaretRect(focus.Offset);
                var origin = current.TransformToVisual(Surface).TransformPoint(new Point(caret.X, caret.Y));
                from = new Rect(origin.X, origin.Y, 0, caret.Height);

                if (double.IsNaN(_goalX))
                {
                    _goalX = origin.X;
                }
            }

            var goal = _goalX;

            // Another line of the same text.
            if (current != null)
            {
                var text = Bounds(current);
                var y = direction < 0 ? from.Top - 1 : from.Bottom + 1;

                if (y > text.Top && y < text.Bottom)
                {
                    var offset = current.GetPositionFromPoint(new Point(Math.Clamp(goal - text.X, 0, text.Width), y - text.Y));
                    if (!IsSameLine(current, offset, from))
                    {
                        _engine.Move(new RichPosition(current.Leaf, offset), extend);
                        _goalX = goal;
                        return true;
                    }
                }
            }

            var edge = direction < 0 ? from.Top : from.Bottom;

            RichLeafView bestLeaf = null;
            RichBlock bestNode = null;
            var bestScore = double.MaxValue;

            foreach (var view in _leafViews.Values)
            {
                if (ReferenceEquals(view, current) || view.Visibility != Visibility.Visible || view.ActualHeight <= 0)
                {
                    continue;
                }

                var score = Score(Bounds(view.HitTarget ?? view), edge, direction, goal);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestLeaf = view;
                    bestNode = null;
                }
            }

            // Blocks selected whole are stops on the way, but not for a selection being extended:
            // that crosses them to the text beyond.
            if (!extend)
            {
                foreach (var pair in _nodeViews)
                {
                    if (ReferenceEquals(pair.Key, currentNode) || !TryGetNodeBounds(pair.Key, out Rect bounds))
                    {
                        continue;
                    }

                    var score = Score(bounds, edge, direction, goal);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestLeaf = null;
                        bestNode = pair.Key;
                    }
                }
            }

            if (bestNode != null)
            {
                _engine.SelectNode(bestNode);
                _goalX = goal;
                return true;
            }

            if (bestLeaf == null)
            {
                // Down out of a quote, a list or a collapsible block that ends the document: a
                // line after it, or there would be no way past it.
                if (direction > 0 && !extend && _engine.AppendLineAfterLast())
                {
                    _goalX = goal;
                    return true;
                }

                // Nothing further that way: to the very start or end, as a text box does on its
                // first and last line.
                _engine.MoveToDocumentEdge(direction, extend);
                _goalX = goal;
                return false;
            }

            // Onto the line of it nearest the one left: its last going up, its first going down.
            var target = Bounds(bestLeaf);
            var local = new Point(
                Math.Clamp(goal - target.X, 0, target.Width),
                direction < 0 ? Math.Max(0, target.Height - 1) : 1);

            _engine.Move(new RichPosition(bestLeaf.Leaf, bestLeaf.GetPositionFromPoint(local)), extend);

            // Moving reset the goal; this run of moves keeps it.
            _goalX = goal;
            return true;
        }

        /// <summary>
        /// How good a candidate is for a vertical move from <paramref name="edge"/>: lower is
        /// better, and anything not wholly past the edge does not count. Distance in the
        /// direction of the move first, then how far it is from the column.
        /// </summary>
        private static double Score(Rect bounds, double edge, int direction, double goal)
        {
            const double Slop = 0.5;

            double distance;

            if (direction < 0)
            {
                if (bounds.Bottom > edge + Slop)
                {
                    return double.MaxValue;
                }

                distance = edge - bounds.Bottom;
            }
            else
            {
                if (bounds.Top < edge - Slop)
                {
                    return double.MaxValue;
                }

                distance = bounds.Top - edge;
            }

            var dx = goal < bounds.Left ? bounds.Left - goal : goal > bounds.Right ? goal - bounds.Right : 0;
            return Math.Max(0, distance) * 4096 + dx;
        }

        private bool TryGetNodeBounds(RichBlock node, out Rect bounds)
        {
            if (_itemViews.TryGetValue(node, out FrameworkElement tile) && tile.ActualWidth > 0)
            {
                bounds = Bounds(tile);
                return true;
            }

            if (_nodeViews.TryGetValue(node, out RichBlockPresenter presenter) && presenter.NodeElement.ActualWidth > 0)
            {
                bounds = Bounds(presenter.NodeElement);
                return true;
            }

            bounds = default;
            return false;
        }

        private bool IsSameLine(RichLeafView view, int offset, Rect line)
        {
            var caret = view.GetCaretRect(offset);
            var top = view.TransformToVisual(Surface).TransformPoint(new Point(caret.X, caret.Y)).Y;

            return Math.Abs(top - line.Top) < 1;
        }

        private void MoveToLineEdge(int direction, bool extend)
        {
            var focus = _engine.Focus;
            if (_engine.SelectedNode != null || !_leafViews.TryGetValue(focus.Leaf, out RichLeafView view))
            {
                _engine.MoveToLeafEdge(direction, extend);
                return;
            }

            Surface.UpdateLayout();

            var caret = view.GetCaretRect(focus.Offset);
            var offset = view.GetLineEdge(caret.Y + caret.Height / 2, direction > 0);

            _engine.Move(new RichPosition(focus.Leaf, offset), extend);
        }

        // =====================================================================================
        // Clipboard
        // =====================================================================================

        public async void Copy()
        {
            await CopyAsync();
        }

        /// <summary>
        /// The selection in every form another place can take: plain text, the chat's text with
        /// entities, HTML, and a token naming the whole fragment for a paste back in here.
        /// </summary>
        private async Task<bool> CopyAsync()
        {
            var fragment = _engine.GetSelectionFragment();
            if (fragment == null)
            {
                return false;
            }

            var package = new DataPackage();

            try
            {
                await MessageHelper.SetTextAsync(package, RichClipboard.ToFormattedText(fragment));
                package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(RichClipboard.ToHtml(fragment)));
                package.SetData(RichClipboard.FormatId, RichClipboard.Store(fragment));
            }
            catch
            {
                return false;
            }

            ClipboardEx.TrySetContent(package);
            return true;
        }

        public async void Cut()
        {
            if (_engine.IsSelectionEmpty)
            {
                return;
            }

            // The fragment is taken before the first await, so the selection it was taken from
            // is the one deleted.
            var copy = CopyAsync();
            _engine.DeleteSelection();

            await copy;
        }

        public async void Paste(bool plain)
        {
            var content = ClipboardEx.TryGetContent();
            if (content == null)
            {
                return;
            }

            try
            {
                // Copied from this editor: the document itself, media and all.
                if (!plain && content.Contains(RichClipboard.FormatId)
                    && RichClipboard.Take(await content.GetDataAsync(RichClipboard.FormatId) as string) is RichDocument fragment)
                {
                    _engine.InsertFragment(fragment);
                    return;
                }

                // A copied image often carries a file name or a link as text too; the image is
                // what was meant, unless the paste asked for plain text.
                var media = content.Contains(StandardDataFormats.StorageItems) || content.Contains(StandardDataFormats.Bitmap);

                if ((media && !plain) || !content.Contains(StandardDataFormats.Text))
                {
                    PasteRequested?.Invoke(this, new RichPasteRequestedEventArgs(content));
                    return;
                }

                if (!plain && content.Contains("application/x-tl-field-tags"))
                {
                    var formatted = await MessageHelper.PasteTextAsync(content);
                    _engine.InsertFragment(_engine.Schema != null
                        ? RichFormattedTextConverter.FromFormattedText(formatted, _engine.Schema)
                        : RichDocumentConverter.FromFormattedText(formatted));
                    return;
                }

                // Into a message's text, HTML is the text it reads as, as it always was in the
                // composer: a page's styles are rarely the ones meant for a chat.
                if (!plain && _engine.Schema == null && content.Contains(StandardDataFormats.Html))
                {
                    var html = HtmlFormatHelper.GetStaticFragment(await content.GetHtmlFormatAsync());
                    var document = RichClipboard.FromHtml(html);

                    if (!document.IsEmpty)
                    {
                        _engine.InsertFragment(document);
                        return;
                    }
                }

                InsertPlainText(await content.GetTextAsync());
            }
            catch
            {
                // The clipboard changed, or its source went away, while being read.
            }
        }

        /// <summary>
        /// Text with line breaks: a paragraph per line in ordinary text, the breaks kept as they
        /// are in code and in cells, which hold several lines of their own.
        /// </summary>
        public void InsertPlainText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            var leaf = _engine.Focus.Leaf;
            if (leaf.IsPlain || leaf.Role != RichLeafRole.Text || leaf.Owner is not RichParagraphBlock || text.IndexOf('\n') < 0)
            {
                _engine.InsertText(text);
                return;
            }

            _engine.BeginChange();

            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    _engine.InsertParagraph();
                }

                _engine.InsertText(lines[i]);
            }

            _engine.EndChange();
        }
    }

    public sealed class RichMenuRequestedEventArgs : EventArgs
    {
        public RichMenuRequestedEventArgs(Point position, bool touch)
        {
            Position = position;
            IsTouch = touch;
        }

        /// <summary>In the coordinates of the element <see cref="RichEditorView.ShowMenu"/> shows menus at.</summary>
        public Point Position { get; }

        public bool IsTouch { get; }

        /// <summary>From a block's own menu button: what can be done to the block, and nothing else.</summary>
        public bool IsBlockMenu { get; init; }

        /// <summary>From the keyboard, which the menu has to take focus for.</summary>
        public bool IsKeyboard { get; init; }
    }

    public sealed class RichNodeActivatedEventArgs : EventArgs
    {
        public RichNodeActivatedEventArgs(RichBlock node)
        {
            Node = node;
        }

        public RichBlock Node { get; }

        public bool Handled { get; set; }
    }

    public sealed class RichPasteRequestedEventArgs : EventArgs
    {
        public RichPasteRequestedEventArgs(DataPackageView content)
        {
            Content = content;
        }

        public DataPackageView Content { get; }
    }
}
