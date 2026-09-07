//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Automation.Text;

namespace Telegram.Controls
{
    /// <summary>
    /// The text pattern over a <see cref="DirectTextBlock"/>. A screen reader reads a text
    /// control through this: without it the block is a blank element, where the RichTextBlock
    /// it replaces supplies one of its own.
    ///
    /// Every answer comes off the layout the text was drawn from - positions, lines,
    /// rectangles - so nothing here has to reconstruct what is on screen.
    /// </summary>
    public partial class DirectTextBlockAutomationPeer : FrameworkElementAutomationPeer, ITextProvider
    {
        private readonly DirectTextBlock _owner;

        public DirectTextBlockAutomationPeer(DirectTextBlock owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override object GetPatternCore(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Text)
            {
                return this;
            }

            return base.GetPatternCore(patternInterface);
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Text;
        }

        protected override string GetClassNameCore()
        {
            return nameof(DirectTextBlock);
        }

        // The whole text as the name as well, so a reader that asks for neither the pattern nor
        // the children still says something useful.
        protected override string GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? _owner.Text ?? string.Empty : name;
        }

        // The children XAML already gives - the inline buttons and the emoji players are real
        // elements - plus one peer per link, which is not: a link is a range, and without this
        // nothing in the tree says it is there.
        protected override IList<AutomationPeer> GetChildrenCore()
        {
            var children = base.GetChildrenCore() ?? new List<AutomationPeer>();

            for (int i = 0; i < _owner.LinkCount; i++)
            {
                children.Add(new DirectLinkAutomationPeer(this, _owner, i));
            }

            return children;
        }

        public SupportedTextSelection SupportedTextSelection => SupportedTextSelection.Single;

        public ITextRangeProvider DocumentRange => new DirectTextRangeProvider(this, _owner, 0, _owner.ContentLength);

        public ITextRangeProvider[] GetSelection()
        {
            _owner.GetSelection(out var start, out var end);

            return new ITextRangeProvider[]
            {
                new DirectTextRangeProvider(this, _owner, start, end)
            };
        }

        // Everything is visible: the block is as tall as its text, and whatever scrolls it is
        // somewhere above.
        public ITextRangeProvider[] GetVisibleRanges()
        {
            return new[] { DocumentRange };
        }

        public ITextRangeProvider RangeFromChild(IRawElementProviderSimple childElement)
        {
            return DocumentRange;
        }

        public ITextRangeProvider RangeFromPoint(Point screenLocation)
        {
            // Automation speaks in screen coordinates and the layout in the element's own. The
            // peer's bounding rectangle is the element in screen coordinates, which is the
            // whole conversion - there is no scale or rotation in between.
            var bounds = GetBoundingRectangle();
            var point = new Point(screenLocation.X - bounds.X, screenLocation.Y - bounds.Y);

            var position = _owner.GetPositionFromPoint(point, out _);

            return new DirectTextRangeProvider(this, _owner, position, position);
        }

        internal IRawElementProviderSimple Provider => ProviderFromPeer(this);
    }

    /// <summary>
    /// A range of the text, in the same offsets everything else in this control speaks:
    /// positions into the string as it was given.
    /// </summary>
    public partial class DirectTextRangeProvider : ITextRangeProvider
    {
        private readonly DirectTextBlockAutomationPeer _peer;
        private readonly DirectTextBlock _owner;

        private int _start;
        private int _end;

        public DirectTextRangeProvider(DirectTextBlockAutomationPeer peer, DirectTextBlock owner, int start, int end)
        {
            _peer = peer;
            _owner = owner;

            _start = Math.Min(start, end);
            _end = Math.Max(start, end);
        }

        private string Text => _owner.Text ?? string.Empty;

        private int Length => Text.Length;

        public ITextRangeProvider Clone()
        {
            return new DirectTextRangeProvider(_peer, _owner, _start, _end);
        }

        public bool Compare(ITextRangeProvider textRangeProvider)
        {
            return textRangeProvider is DirectTextRangeProvider other
                && other._owner == _owner
                && other._start == _start
                && other._end == _end;
        }

        public int CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider textRangeProvider, TextPatternRangeEndpoint targetEndpoint)
        {
            if (textRangeProvider is not DirectTextRangeProvider other)
            {
                return 0;
            }

            var mine = endpoint == TextPatternRangeEndpoint.Start ? _start : _end;
            var theirs = targetEndpoint == TextPatternRangeEndpoint.Start ? other._start : other._end;

            return mine.CompareTo(theirs);
        }

        public void ExpandToEnclosingUnit(TextUnit unit)
        {
            switch (unit)
            {
                case TextUnit.Character:
                    _end = Math.Min(_start + 1, Length);
                    break;
                case TextUnit.Word:
                case TextUnit.Format:
                    _owner.GetSelectionBoundary(_start, 0, TextSelectionGranularity.Word, out _start, out _end);
                    break;
                case TextUnit.Line:
                    LineAt(_start, out _start, out _end);
                    break;
                case TextUnit.Paragraph:
                    _owner.GetSelectionBoundary(_start, 0, TextSelectionGranularity.Paragraph, out _start, out _end);
                    break;
                default:
                    _start = 0;
                    _end = Length;
                    break;
            }
        }

        // The line a position sits on, from the lengths the layout reports: which line a
        // position is on is a question about how the text was laid out, not about the string.
        private void LineAt(int position, out int start, out int end)
        {
            var lengths = _owner.GetLineLengths();
            var offset = 0;

            for (int i = 0; i < lengths?.Length; i++)
            {
                if (position < offset + lengths[i] || i == lengths.Length - 1)
                {
                    start = offset;
                    end = Math.Min(offset + lengths[i], Length);
                    return;
                }

                offset += lengths[i];
            }

            start = 0;
            end = Length;
        }

        public ITextRangeProvider FindAttribute(int attributeId, object value, bool backward)
        {
            return null;
        }

        public ITextRangeProvider FindText(string text, bool backward, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var haystack = Text.Substring(_start, _end - _start);

            var index = backward
                ? haystack.LastIndexOf(text, comparison)
                : haystack.IndexOf(text, comparison);

            return index < 0
                ? null
                : new DirectTextRangeProvider(_peer, _owner, _start + index, _start + index + text.Length);
        }

        public object GetAttributeValue(int attributeId)
        {
            return null;
        }

        public void GetBoundingRectangles(out double[] rectangles)
        {
            var rects = _owner.GetRangeRectangles(_start, _end);
            var bounds = _peer.GetBoundingRectangle();

            rectangles = new double[(rects?.Length ?? 0) * 4];

            for (int i = 0; i < rects?.Length; i++)
            {
                // The element in screen coordinates plus the rectangle in the element's: what
                // automation asks for, without a conversion of our own.
                rectangles[i * 4 + 0] = bounds.X + rects[i].X;
                rectangles[i * 4 + 1] = bounds.Y + rects[i].Y;
                rectangles[i * 4 + 2] = rects[i].Width;
                rectangles[i * 4 + 3] = rects[i].Height;
            }
        }

        public IRawElementProviderSimple GetEnclosingElement()
        {
            return _peer.Provider;
        }

        public string GetText(int maxLength)
        {
            var text = Text.Substring(_start, _end - _start);

            return maxLength >= 0 && maxLength < text.Length
                ? text.Substring(0, maxLength)
                : text;
        }

        public int Move(TextUnit unit, int count)
        {
            var moved = MoveEndpointByUnit(TextPatternRangeEndpoint.Start, unit, count);

            _end = _start;
            ExpandToEnclosingUnit(unit);

            return moved;
        }

        public int MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count)
        {
            var position = endpoint == TextPatternRangeEndpoint.Start ? _start : _end;
            var moved = 0;

            for (int i = 0; i < Math.Abs(count); i++)
            {
                var next = Step(position, unit, count > 0);

                if (next == position)
                {
                    break;
                }

                position = next;
                moved++;
            }

            if (endpoint == TextPatternRangeEndpoint.Start)
            {
                _start = position;
                _end = Math.Max(_end, _start);
            }
            else
            {
                _end = position;
                _start = Math.Min(_start, _end);
            }

            return count > 0 ? moved : -moved;
        }

        private int Step(int position, TextUnit unit, bool forward)
        {
            var from = Math.Clamp(position + (forward ? 1 : -1), 0, Length);

            switch (unit)
            {
                case TextUnit.Character:
                    return from;
                case TextUnit.Word:
                case TextUnit.Format:
                    _owner.GetSelectionBoundary(from, 0, TextSelectionGranularity.Word, out var wordStart, out var wordEnd);
                    return forward ? wordEnd : wordStart;
                case TextUnit.Line:
                    LineAt(from, out var lineStart, out var lineEnd);
                    return forward ? lineEnd : lineStart;
                case TextUnit.Paragraph:
                    _owner.GetSelectionBoundary(from, 0, TextSelectionGranularity.Paragraph, out var paragraphStart, out var paragraphEnd);
                    return forward ? paragraphEnd : paragraphStart;
                default:
                    return forward ? Length : 0;
            }
        }

        public void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider textRangeProvider, TextPatternRangeEndpoint targetEndpoint)
        {
            if (textRangeProvider is not DirectTextRangeProvider other)
            {
                return;
            }

            var target = targetEndpoint == TextPatternRangeEndpoint.Start ? other._start : other._end;

            if (endpoint == TextPatternRangeEndpoint.Start)
            {
                _start = target;
                _end = Math.Max(_end, _start);
            }
            else
            {
                _end = target;
                _start = Math.Min(_start, _end);
            }
        }

        public void Select()
        {
            _owner.Select(_start, _end);
        }

        // One selection at a time, which is what SupportedTextSelection.Single says.
        public void AddToSelection()
        {
        }

        public void RemoveFromSelection()
        {
        }

        public void ScrollIntoView(bool alignToTop)
        {
            _owner.StartBringIntoView();
        }

        public IRawElementProviderSimple[] GetChildren()
        {
            return Array.Empty<IRawElementProviderSimple>();
        }
    }
    /// <summary>
    /// A link in the automation tree. There is no element behind it - a link is a range of the
    /// text - so this peer answers for one directly: what it says, where it is, and what
    /// invoking it does. It is how a screen reader reaches a link that the pointer reaches by
    /// hit testing.
    /// </summary>
    public partial class DirectLinkAutomationPeer : AutomationPeer, IInvokeProvider
    {
        private readonly DirectTextBlockAutomationPeer _parent;
        private readonly DirectTextBlock _owner;
        private readonly int _index;

        public DirectLinkAutomationPeer(DirectTextBlockAutomationPeer parent, DirectTextBlock owner, int index)
        {
            _parent = parent;
            _owner = owner;
            _index = index;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Hyperlink;
        }

        protected override string GetClassNameCore()
        {
            return "Hyperlink";
        }

        protected override string GetNameCore()
        {
            return _owner.GetLinkText(_index);
        }

        protected override object GetPatternCore(PatternInterface patternInterface)
        {
            return patternInterface == PatternInterface.Invoke ? this : null;
        }

        // The union of the rectangles the link covers - it can wrap across lines - offset by
        // where the block itself is on screen.
        protected override Rect GetBoundingRectangleCore()
        {
            var rects = _owner.GetLinkRectangles(_index);

            if (rects == null || rects.Length == 0)
            {
                return default;
            }

            var bounds = _parent.GetBoundingRectangle();

            var left = double.MaxValue;
            var top = double.MaxValue;
            var right = double.MinValue;
            var bottom = double.MinValue;

            for (int i = 0; i < rects.Length; i++)
            {
                left = Math.Min(left, rects[i].Left);
                top = Math.Min(top, rects[i].Top);
                right = Math.Max(right, rects[i].Right);
                bottom = Math.Max(bottom, rects[i].Bottom);
            }

            return new Rect(bounds.X + left, bounds.Y + top, right - left, bottom - top);
        }

        protected override Point GetClickablePointCore()
        {
            var rect = GetBoundingRectangleCore();
            return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        }

        protected override IList<AutomationPeer> GetChildrenCore() => null;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;

        protected override bool IsEnabledCore() => true;

        protected override bool IsOffscreenCore() => false;

        // Not focusable: nothing in the tree can take focus, because a range cannot. Reaching
        // a link by keyboard would mean the block owning focus itself and moving between them.
        protected override bool IsKeyboardFocusableCore() => false;

        protected override bool HasKeyboardFocusCore() => false;

        protected override void SetFocusCore()
        {
        }

        protected override string GetAutomationIdCore() => string.Empty;

        protected override string GetHelpTextCore() => string.Empty;

        protected override string GetItemStatusCore() => string.Empty;

        protected override string GetItemTypeCore() => string.Empty;

        protected override string GetAcceleratorKeyCore() => string.Empty;

        protected override string GetAccessKeyCore() => string.Empty;

        protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;

        protected override string GetLocalizedControlTypeCore() => "link";

        protected override AutomationPeer GetLabeledByCore() => null;

        protected override bool IsPasswordCore() => false;

        protected override bool IsRequiredForFormCore() => false;

        public void Invoke()
        {
            _owner.InvokeLink(_index);
        }
    }
}
