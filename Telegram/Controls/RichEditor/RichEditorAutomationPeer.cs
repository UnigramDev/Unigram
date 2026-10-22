//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Common;
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
    public sealed partial class RichEditorAutomationPeer : FrameworkElementAutomationPeer, ITextProvider, ITextProvider2, ITextEditProvider
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
            if (patternInterface is PatternInterface.Text or PatternInterface.Text2 or PatternInterface.TextEdit)
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

        // RichEditBox reads its placeholder out while it is empty, through DescribedBy pointing at
        // the placeholder's element. The hint here is drawn by a line, not an element of its own.
        protected override string GetHelpTextCore()
        {
            var help = base.GetHelpTextCore();
            if (string.IsNullOrEmpty(help) && Document is RichDocument document && document.IsEmpty)
            {
                return _owner.PlaceholderText ?? string.Empty;
            }

            return help;
        }

        protected override bool IsKeyboardFocusableCore() => true;

        protected override void ShowContextMenuCore()
        {
            _owner.RequestKeyboardMenu();
        }

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

        /// <summary>The text an IME is composing, as it changes and once it is committed: what a reader says while it converts.</summary>
        internal void RaiseComposition(string text, bool finalized)
        {
            if (ListenerExists(AutomationEvents.TextEditTextChanged))
            {
                var type = finalized ? AutomationTextEditChangeType.CompositionFinalized : AutomationTextEditChangeType.Composition;
                RaiseTextEditTextChangedEvent(type, new[] { text });
            }
        }

        internal void RaiseConversionTargetChanged()
        {
            if (ListenerExists(AutomationEvents.ConversionTargetChanged))
            {
                RaiseAutomationEvent(AutomationEvents.ConversionTargetChanged);
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

        /// <summary>
        /// The text of [start, end). An object reads as its text, which is wider than its one
        /// position: <paramref name="positions"/>, when given, gets the position each character
        /// was read from.
        /// </summary>
        internal string GetText(int start, int end, List<int> positions = null)
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

                if (to > from && positions != null)
                {
                    for (int j = from; j < to; j++)
                    {
                        var c = leaf[j - flat];
                        if (c != RichTextLeaf.ObjectChar)
                        {
                            builder.Append(c);
                            positions.Add(j);
                            continue;
                        }

                        var text = leaf.GetObjectAt(j - flat)?.PlainText ?? string.Empty;
                        builder.Append(text);

                        for (int k = 0; k < text.Length; k++)
                        {
                            positions.Add(j);
                        }
                    }
                }
                else if (to > from)
                {
                    builder.Append(leaf.ToPlainText(from - flat, to - flat));
                }

                if (i < leaves.Count - 1 && leafEnd >= start && leafEnd < end)
                {
                    builder.Append('\n');
                    positions?.Add(leafEnd);
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

        /// <summary>The lines on screen, a range for each stretch of them that follows on in the document.</summary>
        public ITextRangeProvider[] GetVisibleRanges()
        {
            var document = Document;

            if (document == null || !_owner.TryGetVisibleBounds(out Rect viewport))
            {
                return new[] { DocumentRange };
            }

            if (viewport.IsEmpty)
            {
                return Array.Empty<ITextRangeProvider>();
            }

            List<ITextRangeProvider> ranges = null;
            var start = -1;
            var end = -1;
            var flat = 0;

            foreach (var leaf in document.Leaves)
            {
                var leafStart = flat;
                flat += leaf.Length + 1;

                if (!_owner.TryGetLeafView(leaf, out RichLeafView view) || view.Visibility != Visibility.Visible || !GetVisibleLines(leaf, view, viewport, out int from, out int to))
                {
                    continue;
                }

                // Lines of a table's cells side by side are on screen together, but not one after
                // the other in the document.
                if (start < 0 || leafStart + from > end + 1)
                {
                    if (start >= 0)
                    {
                        (ranges ??= new List<ITextRangeProvider>()).Add(new RichTextRangeProvider(this, start, end));
                    }

                    start = leafStart + from;
                }

                end = leafStart + to;
            }

            if (start >= 0)
            {
                (ranges ??= new List<ITextRangeProvider>()).Add(new RichTextRangeProvider(this, start, end));
            }

            return ranges?.ToArray() ?? Array.Empty<ITextRangeProvider>();
        }

        private bool GetVisibleLines(RichTextLeaf leaf, RichLeafView view, Rect viewport, out int from, out int to)
        {
            from = 0;
            to = leaf.Length;

            var transform = view.TransformToVisual(_owner);
            var bounds = transform.TransformBounds(new Rect(0, 0, view.ActualWidth, view.ActualHeight));

            if (bounds.Bottom <= viewport.Top || bounds.Top >= viewport.Bottom || bounds.Right <= viewport.Left || bounds.Left >= viewport.Right)
            {
                return false;
            }

            var lengths = view.TextBlock?.GetLineLengths();
            if (lengths == null || lengths.Length < 2 || (bounds.Top >= viewport.Top && bounds.Bottom <= viewport.Bottom))
            {
                return true;
            }

            var first = -1;
            var position = 0;

            for (int i = 0; i < lengths.Length; i++)
            {
                var line = transform.TransformBounds(view.GetCaretRect(position));
                var lineEnd = Math.Min(position + lengths[i], leaf.Length);

                if (line.Bottom > viewport.Top && line.Top < viewport.Bottom)
                {
                    if (first < 0)
                    {
                        first = position;
                    }

                    to = lineEnd;
                }

                position = lineEnd;
            }

            from = Math.Max(first, 0);
            return first >= 0;
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

                case AutomationTextAttributesEnum.AnnotationTypesAttribute:
                    var limit = leaf.Length;
                    return _owner.IsMisspelled(leaf, position.Offset, ref limit) ? SpellingError : NoAnnotations;

                case AutomationTextAttributesEnum.LinkAttribute:
                    if (leaf.TryGetLinkRange(position.Offset, out int linkStart, out int linkEnd))
                    {
                        var leafStart = flat - position.Offset;
                        return new RichTextRangeProvider(this, leafStart + linkStart, leafStart + linkEnd);
                    }

                    return null;

                case AutomationTextAttributesEnum.FontNameAttribute:
                    if ((marks & RichMarks.Monospace) != 0)
                    {
                        return GetFaceName(Theme.MonospaceFontFamily.Source);
                    }

                    return _owner.TryGetLeafView(leaf, out RichLeafView named) ? GetFaceName(named.TextBlock?.FontFamily?.Source) : null;

                case AutomationTextAttributesEnum.FontSizeAttribute:
                    return _owner.TryGetLeafView(leaf, out RichLeafView sized) ? sized.TextBlock?.FontSize : null;

                default:
                    return null;
            }
        }

        // A family source is a fallback list, "Segoe UI, ms-appx:///...#Segoe UI Emoji": the
        // reader wants the face the text is set in.
        private static string GetFaceName(string source)
        {
            var comma = source?.IndexOf(',') ?? -1;
            return comma < 0 ? source : source.Substring(0, comma).Trim();
        }

        // XAML reads the types as a vector view of ints. Shared, so equal values are one instance.
        private static readonly IReadOnlyList<int> SpellingError = new List<int> { (int)AnnotationType.SpellingError };
        private static readonly IReadOnlyList<int> NoAnnotations = new List<int>();

        /// <summary>
        /// The attribute over [start, end): its value, or UnsetValue where it changes inside the
        /// range, which XAML hands to UIA as mixed.
        /// </summary>
        internal object GetAttribute(int start, int end, int attributeId)
        {
            var value = GetAttribute(start, attributeId);

            // Each span finds its leaf from the start of the document: a reader asking the whole
            // of it for what is the same everywhere would wait on every run.
            if (Document == null || !VariesInText(attributeId))
            {
                return value;
            }

            for (var position = GetSpanEnd(start, attributeId); position < end; position = GetSpanEnd(position, attributeId))
            {
                if (!IsSameValue(value, GetAttribute(position, attributeId)))
                {
                    return DependencyProperty.UnsetValue;
                }
            }

            return value;
        }

        /// <summary>
        /// Where the attribute at <paramref name="flat"/> can next change: the end of its run, or
        /// of the misspelled word. The newline after a leaf is a span of its own.
        /// </summary>
        internal int GetSpanEnd(int flat, int attributeId)
        {
            var position = ToPosition(flat);
            var leaf = position.Leaf;

            if (position.Offset >= leaf.Length)
            {
                return flat + 1;
            }

            var index = leaf.FindRun(position.Offset, out int runStart);
            var end = index >= 0 ? runStart + leaf.Runs[index].Length : leaf.Length;

            if (attributeId == (int)AutomationTextAttributesEnum.AnnotationTypesAttribute)
            {
                _owner.IsMisspelled(leaf, position.Offset, ref end);
            }

            return flat - position.Offset + end;
        }

        private static bool VariesInText(int attributeId)
        {
            return (AutomationTextAttributesEnum)attributeId is AutomationTextAttributesEnum.FontWeightAttribute
                or AutomationTextAttributesEnum.IsItalicAttribute
                or AutomationTextAttributesEnum.UnderlineStyleAttribute
                or AutomationTextAttributesEnum.StrikethroughStyleAttribute
                or AutomationTextAttributesEnum.IsSubscriptAttribute
                or AutomationTextAttributesEnum.IsSuperscriptAttribute
                or AutomationTextAttributesEnum.BulletStyleAttribute
                or AutomationTextAttributesEnum.HorizontalTextAlignmentAttribute
                or AutomationTextAttributesEnum.StyleIdAttribute
                or AutomationTextAttributesEnum.AnnotationTypesAttribute
                or AutomationTextAttributesEnum.LinkAttribute
                or AutomationTextAttributesEnum.FontNameAttribute
                or AutomationTextAttributesEnum.FontSizeAttribute;
        }

        internal static bool IsSameValue(object x, object y)
        {
            return x is RichTextRangeProvider range
                ? y is RichTextRangeProvider other && range.Compare(other)
                : Equals(x, y);
        }

        /// <summary>
        /// Which line a position at a wrap is on, which the offset alone does not say: here it
        /// is the start of the next, as the line unit takes it.
        /// </summary>
        internal AutomationCaretPosition GetCaretPosition(int flat)
        {
            var position = ToPosition(flat);
            var leaf = position.Leaf;

            if (position.Offset == 0)
            {
                return AutomationCaretPosition.BeginningOfLine;
            }

            if (position.Offset >= leaf.Length)
            {
                return AutomationCaretPosition.EndOfLine;
            }

            if (_owner.TryGetLeafView(leaf, out RichLeafView view) && view.TextBlock?.GetLineLengths() is int[] lengths)
            {
                var lineStart = 0;

                for (int i = 0; i < lengths.Length && lineStart < position.Offset; i++)
                {
                    lineStart += lengths[i];
                }

                if (lineStart == position.Offset)
                {
                    return AutomationCaretPosition.BeginningOfLine;
                }
            }

            return AutomationCaretPosition.Unknown;
        }

        /// <summary>The end of the selection that moves when it is extended, for a range that is the selection.</summary>
        internal AutomationActiveEnd GetActiveEnd(int start, int end)
        {
            var engine = _owner.Engine;
            if (engine == null || start == end)
            {
                return AutomationActiveEnd.None;
            }

            var anchor = ToFlat(engine.Anchor);
            var focus = ToFlat(engine.Focus);

            if (Math.Min(anchor, focus) != start || Math.Max(anchor, focus) != end)
            {
                return AutomationActiveEnd.None;
            }

            return focus < anchor ? AutomationActiveEnd.Start : AutomationActiveEnd.End;
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
            var scale = _owner.XamlRoot?.RasterizationScale ?? 1;
            var local = new Point((screenLocation.X - bounds.X) / scale, (screenLocation.Y - bounds.Y) / scale);

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

        // ITextEditProvider: the text an IME is composing, and the clause in it being converted.
        public ITextRangeProvider GetActiveComposition()
        {
            return _owner.TryGetComposition(false, out RichTextLeaf leaf, out int start, out int end) ? RangeIn(leaf, start, end) : null;
        }

        public ITextRangeProvider GetConversionTarget()
        {
            return _owner.TryGetComposition(true, out RichTextLeaf leaf, out int start, out int end) ? RangeIn(leaf, start, end) : null;
        }

        private RichTextRangeProvider RangeIn(RichTextLeaf leaf, int start, int end)
        {
            var flat = ToFlat(new RichPosition(leaf, 0));
            return new RichTextRangeProvider(this, flat + start, flat + end);
        }

        internal IRawElementProviderSimple Provider => ProviderFromPeer(this);
    }

    public sealed partial class RichTextRangeProvider : ITextRangeProvider, ITextRangeProvider2
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

        /// <summary>
        /// The unit of <paramref name="unit"/> kind that holds <paramref name="flat"/>. A unit that
        /// ends a leaf takes the newline after it, so the next one starts on the next line rather
        /// than at the newline; only as a character is the newline a unit of its own.
        /// </summary>
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
                        ? leafStart + RichEditorEngine.NextCaretStop(leaf.Chars, position.Offset)
                        : Math.Min(flat + 1, length);
                    return;

                case TextUnit.Word:
                    WordAt(leaf, position.Offset, out int wordStart, out int wordEnd);
                    start = leafStart + wordStart;
                    end = leafStart + wordEnd;
                    break;

                case TextUnit.Format:
                    {
                        var index = leaf.FindRun(Math.Min(position.Offset, Math.Max(0, leaf.Length - 1)), out int runStart);
                        var runLength = index >= 0 ? leaf.Runs[index].Length : leaf.Length;

                        start = leafStart + (index >= 0 ? runStart : 0);
                        end = start + runLength;
                        break;
                    }

                case TextUnit.Line:
                    LineAt(leaf, position.Offset, out int lineStart, out int lineEnd);
                    start = leafStart + lineStart;
                    end = leafStart + lineEnd;
                    break;

                default:
                    start = leafStart;
                    end = leafStart + leaf.Length;
                    break;
            }

            if (end == leafStart + leaf.Length && end < length)
            {
                end++;
            }
        }

        /// <summary>A word and the spaces after it, as UIA has a word; spaces that start a line are a word of their own.</summary>
        private static void WordAt(RichTextLeaf leaf, int offset, out int start, out int end)
        {
            var chars = leaf.Chars;
            RichEditorEngine.GetWordRange(chars, offset, out start, out end);

            if (start < end && char.IsWhiteSpace(chars[start]))
            {
                if (start > 0 && !char.IsWhiteSpace(chars[start - 1]))
                {
                    RichEditorEngine.GetWordRange(chars, start - 1, out start, out _);
                }

                return;
            }

            while (end < chars.Length && char.IsWhiteSpace(chars[end]))
            {
                end++;
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
        /// The first stretch of the range whose attribute has <paramref name="value"/>, or the
        /// last one backward: how a reader jumps to the next heading, or the next misspelling.
        /// </summary>
        public ITextRangeProvider FindAttribute(int attributeId, object value, bool backward)
        {
            if (_peer.Document == null)
            {
                return null;
            }

            var found = -1;
            var foundEnd = -1;
            var position = _start;

            // Walked forward either way: a span is only known from where it starts.
            while (position < _end)
            {
                var end = Math.Min(_peer.GetSpanEnd(position, attributeId), _end);

                if (RichEditorAutomationPeer.IsSameValue(_peer.GetAttribute(position, attributeId), value))
                {
                    if (found < 0 || foundEnd != position)
                    {
                        found = position;
                    }

                    foundEnd = end;
                }
                else if (found >= 0 && !backward)
                {
                    break;
                }

                position = end;
            }

            return found < 0 ? null : new RichTextRangeProvider(_peer, found, foundEnd);
        }

        public ITextRangeProvider FindText(string text, bool backward, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var positions = new List<int>();
            var haystack = _peer.GetText(_start, _end, positions);
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            var index = backward ? haystack.LastIndexOf(text, comparison) : haystack.IndexOf(text, comparison);
            if (index < 0)
            {
                return null;
            }

            return new RichTextRangeProvider(_peer, positions[index], positions[index + text.Length - 1] + 1);
        }

        public object GetAttributeValue(int attributeId)
        {
            // XAML hands UIA a missing link as a null range, which Windows 10's GetAttributeValues
            // dereferences inside the reader. NotSupportedException carries the HRESULT XAML turns
            // into UIA's NotSupported value, which readers take for no link.
            if (attributeId == (int)AutomationTextAttributesEnum.LinkAttribute)
            {
                return _peer.Document != null && _peer.GetAttribute(_start, _end, attributeId) is RichTextRangeProvider link
                    ? link
                    : throw new NotSupportedException();
            }

            if (_peer.Document == null)
            {
                return null;
            }

            switch ((AutomationTextAttributesEnum)attributeId)
            {
                case AutomationTextAttributesEnum.CaretPositionAttribute:
                    return (int)_peer.GetCaretPosition(_start);

                case AutomationTextAttributesEnum.SelectionActiveEndAttribute:
                    return (int)_peer.GetActiveEnd(_start, _end);

                default:
                    return _peer.GetAttribute(_start, _end, attributeId);
            }
        }

        public void GetBoundingRectangles(out double[] rectangles)
        {
            var result = new List<double>();
            var owner = _peer.Owner;

            // The peer's bounds are in screen pixels, the layout in effective pixels.
            var bounds = _peer.GetBoundingRectangle();
            var scale = owner.XamlRoot?.RasterizationScale ?? 1;

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

                // A range ending on a newline ends at the start of the next leaf, where it has
                // nothing to show.
                if (to <= from && _end > _start)
                {
                    continue;
                }

                // A degenerate range is the caret: a reader still wants to know where it is.
                Rect[] rects = to > from
                    ? view.GetRangeRects(from, to)
                    : new[] { view.GetCaretRect(from) };

                var transform = view.TransformToVisual(owner);

                foreach (var rect in rects)
                {
                    var origin = transform.TransformPoint(new Point(rect.X, rect.Y));

                    result.Add(bounds.X + origin.X * scale);
                    result.Add(bounds.Y + origin.Y * scale);
                    result.Add(Math.Max(1, rect.Width * scale));
                    result.Add(rect.Height * scale);
                }
            }

            rectangles = result.ToArray();
        }

        public IRawElementProviderSimple GetEnclosingElement() => _peer.Provider;

        public string GetText(int maxLength)
        {
            // Only as far as was asked, rather than the whole range cut down: a reader asks for
            // the start of the document after every change, and the document is the range. An
            // object reads as its text, which is not one character, so a short answer is built
            // again in full.
            if (maxLength >= 0 && maxLength < _end - _start)
            {
                var start = _peer.GetText(_start, _start + maxLength);

                if (start.Length >= maxLength)
                {
                    return start.Substring(0, maxLength);
                }
            }

            var text = _peer.GetText(_start, _end);
            return maxLength >= 0 && maxLength < text.Length ? text.Substring(0, maxLength) : text;
        }

        public int Move(TextUnit unit, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            // As UIA has it: collapsed to the start, back to the start of the unit there, moved by
            // whole units, then expanded to one. Moving forward from the expanded end instead
            // skips a unit, the next line read being the one after it.
            Unit(_start, unit, out int position, out _);

            var moved = 0;
            for (int i = 0; i < Math.Abs(count); i++)
            {
                var next = Step(position, unit, count > 0);
                if (next == position)
                {
                    break;
                }

                // The end of the document starts no unit, unless the last line is empty.
                if (count > 0)
                {
                    Unit(next, unit, out int start, out _);
                    if (start != next)
                    {
                        break;
                    }
                }

                position = next;
                moved++;
            }

            Unit(position, unit, out _start, out _end);

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
            _peer.Owner.EnsureRealized(position.Leaf);

            if (_peer.Owner.TryGetLeafView(position.Leaf, out RichLeafView view))
            {
                view.StartBringIntoView();
            }
        }

        public IRawElementProviderSimple[] GetChildren() => _peer.GetChildren(_start, _end);

        // ITextRangeProvider2. The menu acts on the selection, so the range becomes it first.
        public void ShowContextMenu()
        {
            Select();
            _peer.Owner.RequestKeyboardMenu();
        }
    }
}
