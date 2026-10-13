//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Automation.Text;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// The editor to a screen reader: one text pattern over the whole document, the way a
    /// RichEditBox is one, so caret browsing, reading by line and by paragraph, and the reader's
    /// own selection commands all work across blocks.
    ///
    /// Automation positions are offsets into the document read flat: every leaf in order, one
    /// newline between two. An object is one position, as it is one caret stop, and reads as the
    /// text it stands for - a custom emoji as its emoji, a formula as its source.
    /// </summary>
    public sealed partial class RichEditorAutomationPeer : FrameworkElementAutomationPeer, ITextProvider, ITextProvider2
    {
        private readonly RichEditorView _owner;

        public RichEditorAutomationPeer(RichEditorView owner)
            : base(owner)
        {
            _owner = owner;
        }

        internal RichEditorView Owner => _owner;

        internal RichDocument Document => _owner.Engine?.Document;

        protected override object GetPatternCore(PatternInterface patternInterface)
        {
            if (patternInterface is PatternInterface.Text or PatternInterface.Text2)
            {
                return this;
            }

            return base.GetPatternCore(patternInterface);
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Edit;
        }

        protected override string GetClassNameCore()
        {
            return nameof(RichEditorView);
        }

        protected override string GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? Strings.ArticleEditor : name;
        }

        protected override bool IsKeyboardFocusableCore() => true;

        /// <summary>Says something the text pattern cannot: a block selected whole, a line that changed kind.</summary>
        internal void Announce(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent, text, "RichEditor");
        }

        // The blocks' own elements - checkboxes, the details toggle, buttons - stay reachable
        // through the children XAML already reports.

        internal void RaiseChanged(RichEditorChanges changes)
        {
            if (!ListenerExists(AutomationEvents.TextPatternOnTextChanged) && !ListenerExists(AutomationEvents.TextPatternOnTextSelectionChanged))
            {
                return;
            }

            if ((changes & (RichEditorChanges.Text | RichEditorChanges.Structure)) != 0)
            {
                RaiseAutomationEvent(AutomationEvents.TextPatternOnTextChanged);
            }

            if ((changes & RichEditorChanges.Selection) != 0)
            {
                RaiseAutomationEvent(AutomationEvents.TextPatternOnTextSelectionChanged);
            }
        }

        // ---------------------------------------------------------------------------------
        // Positions
        // ---------------------------------------------------------------------------------

        internal int Length
        {
            get
            {
                var leaves = Document?.Leaves;
                if (leaves == null || leaves.Count == 0)
                {
                    return 0;
                }

                var length = leaves.Count - 1;
                foreach (var leaf in leaves)
                {
                    length += leaf.Length;
                }

                return length;
            }
        }

        internal int ToFlat(RichPosition position)
        {
            var leaves = Document.Leaves;
            var flat = 0;

            for (int i = 0; i < position.Leaf.Index && i < leaves.Count; i++)
            {
                flat += leaves[i].Length + 1;
            }

            return flat + position.Offset;
        }

        internal RichPosition ToPosition(int flat)
        {
            var leaves = Document.Leaves;

            for (int i = 0; i < leaves.Count; i++)
            {
                if (flat <= leaves[i].Length || i == leaves.Count - 1)
                {
                    return new RichPosition(leaves[i], Math.Clamp(flat, 0, leaves[i].Length));
                }

                flat -= leaves[i].Length + 1;
            }

            return new RichPosition(leaves[0], 0);
        }

        internal string GetText(int start, int end)
        {
            var builder = new StringBuilder(Math.Max(0, end - start));
            var leaves = Document.Leaves;
            var flat = 0;

            for (int i = 0; i < leaves.Count && flat < end; i++)
            {
                var leaf = leaves[i];
                var leafEnd = flat + leaf.Length;

                var from = Math.Max(start, flat);
                var to = Math.Min(end, leafEnd);

                if (to > from)
                {
                    builder.Append(leaf.ToPlainText(from - flat, to - flat));
                }

                if (i < leaves.Count - 1 && leafEnd >= start && leafEnd < end)
                {
                    builder.Append('\n');
                }

                flat = leafEnd + 1;
            }

            return builder.ToString();
        }

        // ---------------------------------------------------------------------------------
        // ITextProvider
        // ---------------------------------------------------------------------------------

        public SupportedTextSelection SupportedTextSelection => SupportedTextSelection.Single;

        public ITextRangeProvider DocumentRange => new RichTextRangeProvider(this, 0, Length);

        public ITextRangeProvider[] GetSelection()
        {
            var engine = _owner.Engine;
            if (engine == null)
            {
                return Array.Empty<ITextRangeProvider>();
            }

            return new ITextRangeProvider[]
            {
                new RichTextRangeProvider(this, ToFlat(engine.Start), ToFlat(engine.End))
            };
        }

        public ITextRangeProvider[] GetVisibleRanges()
        {
            return new[] { DocumentRange };
        }

        public ITextRangeProvider RangeFromChild(IRawElementProviderSimple childElement)
        {
            // A block sits between two lines: its range is the newline there.
            foreach (var (block, flat) in EnumerateNodes())
            {
                if (_owner.TryGetNodeView(block, out FrameworkElement element)
                    && FrameworkElementAutomationPeer.FromElement(element) is AutomationPeer peer
                    && ReferenceEquals(ProviderFromPeer(peer), childElement))
                {
                    return new RichTextRangeProvider(this, flat, Math.Min(flat + 1, Length));
                }
            }

            return DocumentRange;
        }

        /// <summary>The blocks selected whole, each with the flat position of the newline it sits at.</summary>
        internal IEnumerable<(RichBlock Block, int Flat)> EnumerateNodes()
        {
            var document = Document;
            if (document == null)
            {
                yield break;
            }

            var flat = 0;
            var leafStarted = false;

            foreach (var stop in document.Stops)
            {
                if (stop.Leaf != null)
                {
                    if (leafStarted)
                    {
                        flat += 1;
                    }

                    flat += stop.Leaf.Length;
                    leafStarted = true;
                }
                else
                {
                    yield return (stop.Block, flat);
                }
            }
        }

        internal IRawElementProviderSimple[] GetChildren(int start, int end)
        {
            List<IRawElementProviderSimple> children = null;

            foreach (var (block, flat) in EnumerateNodes())
            {
                if (flat < start || flat > end)
                {
                    continue;
                }

                if (_owner.TryGetNodeView(block, out FrameworkElement element))
                {
                    var peer = FrameworkElementAutomationPeer.FromElement(element) ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
                    if (peer != null)
                    {
                        (children ??= new List<IRawElementProviderSimple>()).Add(ProviderFromPeer(peer));
                    }
                }
            }

            return children?.ToArray() ?? Array.Empty<IRawElementProviderSimple>();
        }

        /// <summary>
        /// A text attribute at a flat position: the styles of the run there, and what the block
        /// around it is - a heading of a level, a quote, a list - which is how a reader moves by
        /// heading and says "bulleted list" on entering one.
        /// </summary>
        internal object GetAttribute(int flat, int attributeId)
        {
            if (Document == null)
            {
                return null;
            }

            var position = ToPosition(flat);
            var leaf = position.Leaf;

            var index = leaf.FindRun(Math.Min(position.Offset, Math.Max(0, leaf.Length - 1)), out _);
            var marks = index >= 0 ? leaf.Runs[index].Marks : RichMarks.None;

            var text = leaf.Owner as RichParagraphBlock;
            var list = FindAncestor<RichListBlock>(leaf.Owner);

            switch ((AutomationTextAttributesEnum)attributeId)
            {
                case AutomationTextAttributesEnum.IsReadOnlyAttribute:
                    return false;

                case AutomationTextAttributesEnum.FontWeightAttribute:
                    return (marks & RichMarks.Bold) != 0 ? 700 : text?.Kind == RichBlockKind.Heading ? 600 : 400;

                case AutomationTextAttributesEnum.IsItalicAttribute:
                    return (marks & RichMarks.Italic) != 0;

                case AutomationTextAttributesEnum.UnderlineStyleAttribute:
                    return (int)((marks & RichMarks.Underline) != 0 ? AutomationTextDecorationLineStyle.Single : AutomationTextDecorationLineStyle.None);

                case AutomationTextAttributesEnum.StrikethroughStyleAttribute:
                    return (int)((marks & RichMarks.Strikethrough) != 0 ? AutomationTextDecorationLineStyle.Single : AutomationTextDecorationLineStyle.None);

                case AutomationTextAttributesEnum.IsSubscriptAttribute:
                    return (marks & RichMarks.Subscript) != 0;

                case AutomationTextAttributesEnum.IsSuperscriptAttribute:
                    return (marks & RichMarks.Superscript) != 0;

                case AutomationTextAttributesEnum.BulletStyleAttribute:
                    return (int)(list?.Style == RichListStyle.Bullet || list?.Style == RichListStyle.Checklist
                        ? AutomationBulletStyle.FilledRoundBullet
                        : AutomationBulletStyle.None);

                case AutomationTextAttributesEnum.HorizontalTextAlignmentAttribute:
                    // 0 left, 1 centred: a pull quote and its credit are drawn centred.
                    return leaf.Owner is RichTextQuote ? 1 : 0;

                case AutomationTextAttributesEnum.StyleIdAttribute:
                    return (int)GetStyleId(leaf, text, list);

                default:
                    return null;
            }
        }

        // UIA's StyleId_NumberedList. The XAML enum stops one short of it, at BulletedList, but
        // the value is UIA's own and readers know it.
        private const AutomationStyleId NumberedList = (AutomationStyleId)70016;

        private static AutomationStyleId GetStyleId(RichTextLeaf leaf, RichParagraphBlock text, RichListBlock list)
        {
            if (text?.Kind == RichBlockKind.Heading)
            {
                return text.HeadingSize switch
                {
                    1 => AutomationStyleId.Heading1,
                    2 => AutomationStyleId.Heading2,
                    3 => AutomationStyleId.Heading3,
                    4 => AutomationStyleId.Heading4,
                    5 => AutomationStyleId.Heading5,
                    _ => AutomationStyleId.Heading6
                };
            }

            if (list != null)
            {
                return list.Style == RichListStyle.Ordered ? NumberedList : AutomationStyleId.BulletedList;
            }

            if (FindAncestor<RichBlockQuote>(leaf.Owner) != null || leaf.Owner is RichTextQuote)
            {
                return AutomationStyleId.Quote;
            }

            return AutomationStyleId.Normal;
        }

        private static T FindAncestor<T>(RichBlock block) where T : RichBlock
        {
            for (var current = block; current != null; current = current.Parent)
            {
                if (current is T match)
                {
                    return match;
                }
            }

            return null;
        }

        public ITextRangeProvider RangeFromPoint(Point screenLocation)
        {
            var bounds = GetBoundingRectangle();
            var local = new Point(screenLocation.X - bounds.X, screenLocation.Y - bounds.Y);

            if (_owner.HitTestFromControl(local, out RichPosition position))
            {
                var flat = ToFlat(position);
                return new RichTextRangeProvider(this, flat, flat);
            }

            return new RichTextRangeProvider(this, 0, 0);
        }

        // ITextProvider2: what a reader asks to follow the caret as it moves.
        public ITextRangeProvider GetCaretRange(out bool isActive)
        {
            isActive = _owner.FocusState != FocusState.Unfocused;

            var engine = _owner.Engine;
            var flat = engine != null ? ToFlat(engine.Focus) : 0;

            return new RichTextRangeProvider(this, flat, flat);
        }

        public ITextRangeProvider RangeFromAnnotation(IRawElementProviderSimple annotationElement)
        {
            return DocumentRange;
        }

        internal IRawElementProviderSimple Provider => ProviderFromPeer(this);
    }

    public sealed partial class RichTextRangeProvider : ITextRangeProvider
    {
        private readonly RichEditorAutomationPeer _peer;

        private int _start;
        private int _end;

        public RichTextRangeProvider(RichEditorAutomationPeer peer, int start, int end)
        {
            _peer = peer;
            _start = Math.Min(start, end);
            _end = Math.Max(start, end);
        }

        private int Length => _peer.Length;

        public ITextRangeProvider Clone() => new RichTextRangeProvider(_peer, _start, _end);

        public bool Compare(ITextRangeProvider textRangeProvider)
        {
            return textRangeProvider is RichTextRangeProvider other && other._start == _start && other._end == _end;
        }

        public int CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider textRangeProvider, TextPatternRangeEndpoint targetEndpoint)
        {
            if (textRangeProvider is not RichTextRangeProvider other)
            {
                return 0;
            }

            var mine = endpoint == TextPatternRangeEndpoint.Start ? _start : _end;
            var theirs = targetEndpoint == TextPatternRangeEndpoint.Start ? other._start : other._end;

            return mine.CompareTo(theirs);
        }

        public void ExpandToEnclosingUnit(TextUnit unit)
        {
            Unit(_start, unit, out _start, out _end);
        }

        /// <summary>The unit of <paramref name="unit"/> kind that holds <paramref name="flat"/>.</summary>
        private void Unit(int flat, TextUnit unit, out int start, out int end)
        {
            var length = Length;
            flat = Math.Clamp(flat, 0, length);

            if (unit is TextUnit.Document or TextUnit.Page)
            {
                start = 0;
                end = length;
                return;
            }

            var position = _peer.ToPosition(flat);
            var leaf = position.Leaf;
            var leafStart = flat - position.Offset;

            switch (unit)
            {
                case TextUnit.Character:
                    start = flat;
                    end = position.Offset < leaf.Length
                        ? leafStart + RichEditorEngine.NextCaretStop(leaf.Text, position.Offset)
                        : Math.Min(flat + 1, length);
                    return;

                case TextUnit.Word:
                    RichEditorEngine.GetWordRange(leaf.Text, position.Offset, out int wordStart, out int wordEnd);
                    start = leafStart + wordStart;
                    end = leafStart + wordEnd;
                    return;

                case TextUnit.Format:
                    {
                        var index = leaf.FindRun(Math.Min(position.Offset, Math.Max(0, leaf.Length - 1)), out int runStart);
                        var runLength = index >= 0 ? leaf.Runs[index].Length : leaf.Length;

                        start = leafStart + (index >= 0 ? runStart : 0);
                        end = start + runLength;
                        return;
                    }

                case TextUnit.Line:
                    LineAt(leaf, position.Offset, out int lineStart, out int lineEnd);
                    start = leafStart + lineStart;
                    end = leafStart + lineEnd;
                    return;

                default:
                    start = leafStart;
                    end = leafStart + leaf.Length;
                    return;
            }
        }

        private void LineAt(RichTextLeaf leaf, int offset, out int start, out int end)
        {
            start = 0;
            end = leaf.Length;

            if (!_peer.Owner.TryGetLeafView(leaf, out RichLeafView view) || view.TextBlock == null)
            {
                return;
            }

            var lengths = view.TextBlock.GetLineLengths();
            var position = 0;

            for (int i = 0; i < lengths?.Length; i++)
            {
                if (offset < position + lengths[i] || i == lengths.Length - 1)
                {
                    start = Math.Min(position, leaf.Length);
                    end = Math.Min(position + lengths[i], leaf.Length);
                    return;
                }

                position += lengths[i];
            }
        }

        /// <summary>
        /// The first run in the range whose attribute has <paramref name="value"/>: how a reader
        /// jumps to the next heading, or the next bold word.
        /// </summary>
        public ITextRangeProvider FindAttribute(int attributeId, object value, bool backward)
        {
            var position = backward ? _end : _start;

            while (backward ? position > _start : position < _end)
            {
                Unit(backward ? position - 1 : position, TextUnit.Format, out int start, out int end);

                start = Math.Max(start, _start);
                end = Math.Min(end, _end);

                if (end <= start)
                {
                    // The newline between two leaves belongs to no run.
                    position += backward ? -1 : 1;
                    continue;
                }

                if (Equals(_peer.GetAttribute(start, attributeId), value))
                {
                    return new RichTextRangeProvider(_peer, start, end);
                }

                position = backward ? start : end;
            }

            return null;
        }

        public ITextRangeProvider FindText(string text, bool backward, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            // Searched in the flat text: objects read wider than one position, so a match is only
            // exact on text without objects - which is all a find command types.
            var haystack = _peer.GetText(_start, _end);
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            var index = backward ? haystack.LastIndexOf(text, comparison) : haystack.IndexOf(text, comparison);
            return index < 0 ? null : new RichTextRangeProvider(_peer, _start + index, _start + index + text.Length);
        }

        public object GetAttributeValue(int attributeId)
        {
            return _peer.GetAttribute(_start, attributeId);
        }

        public void GetBoundingRectangles(out double[] rectangles)
        {
            var result = new List<double>();
            var bounds = _peer.GetBoundingRectangle();
            var owner = _peer.Owner;

            var start = _peer.ToPosition(_start);
            var end = _peer.ToPosition(_end);
            var leaves = _peer.Document.Leaves;

            for (int i = start.Leaf.Index; i <= end.Leaf.Index && i < leaves.Count; i++)
            {
                if (!owner.TryGetLeafView(leaves[i], out RichLeafView view) || view.Visibility != Visibility.Visible)
                {
                    continue;
                }

                var from = i == start.Leaf.Index ? start.Offset : 0;
                var to = i == end.Leaf.Index ? end.Offset : leaves[i].Length;

                // A degenerate range is the caret: a reader still wants to know where it is.
                Rect[] rects = to > from
                    ? view.GetRangeRects(from, to)
                    : new[] { view.GetCaretRect(from) };

                var transform = view.TransformToVisual(owner);

                foreach (var rect in rects)
                {
                    var origin = transform.TransformPoint(new Point(rect.X, rect.Y));

                    result.Add(bounds.X + origin.X);
                    result.Add(bounds.Y + origin.Y);
                    result.Add(Math.Max(1, rect.Width));
                    result.Add(rect.Height);
                }
            }

            rectangles = result.ToArray();
        }

        public IRawElementProviderSimple GetEnclosingElement() => _peer.Provider;

        public string GetText(int maxLength)
        {
            var text = _peer.GetText(_start, _end);
            return maxLength >= 0 && maxLength < text.Length ? text.Substring(0, maxLength) : text;
        }

        public int Move(TextUnit unit, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            // A degenerate range moves by whole units from where it is; a wide one collapses to
            // the end it moves away from first.
            var position = count > 0 ? _end : _start;
            if (_end > _start && count > 0)
            {
                Unit(_end - 1, unit, out _, out position);
            }

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

            Unit(position, unit, out _start, out _end);

            if (count > 0 && moved == 0)
            {
                return 0;
            }

            return count > 0 ? moved : -moved;
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

        /// <summary>The start of the next unit forward, or of this or the previous one backward.</summary>
        private int Step(int position, TextUnit unit, bool forward)
        {
            var length = Length;

            if (forward)
            {
                if (position >= length)
                {
                    return length;
                }

                Unit(position, unit, out _, out int end);

                // At the end of a leaf the next unit starts after the newline.
                return end > position ? Math.Min(end, length) : Math.Min(position + 1, length);
            }

            if (position <= 0)
            {
                return 0;
            }

            Unit(position - 1, unit, out int start, out _);
            return start;
        }

        public void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider textRangeProvider, TextPatternRangeEndpoint targetEndpoint)
        {
            if (textRangeProvider is not RichTextRangeProvider other)
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
            _peer.Owner.Engine?.Select(_peer.ToPosition(_start), _peer.ToPosition(_end));
        }

        public void AddToSelection()
        {
        }

        public void RemoveFromSelection()
        {
        }

        public void ScrollIntoView(bool alignToTop)
        {
            var position = _peer.ToPosition(alignToTop ? _start : _end);
            if (_peer.Owner.TryGetLeafView(position.Leaf, out RichLeafView view))
            {
                view.StartBringIntoView();
            }
        }

        public IRawElementProviderSimple[] GetChildren() => _peer.GetChildren(_start, _end);
    }
}
