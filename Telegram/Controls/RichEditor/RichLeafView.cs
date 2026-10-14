//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Native;
using Telegram.Services;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.RichEditor
{
    /// <summary>How a leaf looks; the name of a visual state in the template.</summary>
    public enum RichLeafAppearance
    {
        Paragraph,
        Heading1,
        Heading2,
        Heading3,
        Heading4,
        Heading5,
        Heading6,
        Preformatted,
        Footer,
        Caption,
        QuoteCredit,
        PullQuote,
        PullQuoteCredit,
        DetailsHeader,
        HeaderCell,
    }

    /// <summary>
    /// One leaf, drawn by a <see cref="DirectTextBlock"/>. The block is fed the leaf's own
    /// string, so every offset the editor has is an offset the layout answers for: objects are
    /// one character each in both, and nothing is inserted or rewritten - a date is drawn as
    /// it was written, not formatted, for exactly that reason.
    /// </summary>
    public sealed partial class RichLeafView : Control
    {
        // What a layout of nothing measures to is nothing, and a caret needs a line to stand
        // on. An empty leaf, or one ending in a line break, is drawn with this after it; it is
        // past every offset the leaf has, so nothing that asks the layout about the leaf sees it.
        private const string LineHolder = "​";

        private DirectTextBlock Text;
        private TextBlock Placeholder;

        private IClientService _clientService;
        private RichTextLeaf _leaf;
        private RichLeafAppearance _appearance;
        private string _placeholderText;
        private string _language;

        private int _renderedVersion = -1;
        private RichTextLeaf _renderedLeaf;
        private string _renderedLanguage;
        private bool _renderedRightToLeft;

        private int _selectionStart;
        private int _selectionEnd;

        private readonly List<TextStylePart> _parts = new();
        private readonly List<DirectTextBlock.CustomEmojiRange> _emoji = new();
        private readonly List<DirectTextBlock.MathRange> _math = new();
        private readonly List<DirectTextBlock.InlineButtonRange> _buttons = new();

        public RichLeafView()
        {
            DefaultStyleKey = typeof(RichLeafView);
        }

        public RichTextLeaf Leaf => _leaf;

        /// <summary>
        /// What a pointer has to be over to mean this leaf, when that is more than the text: a
        /// table cell is clicked anywhere inside it, and its text is aligned to one side of it,
        /// or is nothing at all in an empty cell.
        /// </summary>
        public FrameworkElement HitTarget { get; set; }

        /// <summary>The block the text is drawn by, for the automation peer and the decorations layer.</summary>
        public DirectTextBlock TextBlock => Text;

        /// <summary>The lines drawn, all of them for 0: a collapsed quote's text shows its first.</summary>
        public int MaxLines
        {
            get => _maxLines;
            set
            {
                if (_maxLines != value)
                {
                    _maxLines = value;

                    if (Text != null)
                    {
                        Text.MaxLines = value;
                    }
                }
            }
        }

        private int _maxLines;

        /// <summary>Raised when an inline button in the text is clicked: the editor opens it for editing.</summary>
        public event EventHandler<RichInlineObjectClickEventArgs> ObjectClick;

        /// <summary>
        /// Raised when the text was arranged at a new size, whenever that lands: a caret in it may
        /// stand on a line of another height. Managed, and raised from arrange, rather than
        /// SizeChanged: views are bound again on every structural change.
        /// </summary>
        internal event EventHandler Resized;

        private Size _arrangedSize;

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = base.ArrangeOverride(finalSize);

            if (_arrangedSize != size)
            {
                _arrangedSize = size;
                Resized?.Invoke(this, EventArgs.Empty);
            }

            return size;
        }

        protected override void OnApplyTemplate()
        {
            if (Text != null)
            {
                Text.TextEntityClick -= OnTextEntityClick;
                Text.HyperlinkColorChanged -= OnHyperlinkColorChanged;
            }

            Text = GetTemplateChild(nameof(Text)) as DirectTextBlock;
            Placeholder = GetTemplateChild(nameof(Placeholder)) as TextBlock;

            if (Text != null)
            {
                Text.TextEntityClick += OnTextEntityClick;
                Text.HyperlinkColorChanged += OnHyperlinkColorChanged;
                Text.MaxLines = _maxLines;
            }

            _renderedVersion = -1;
            _renderedLeaf = null;

            VisualStateManager.GoToState(this, _appearance.ToString(), false);
            Render();
        }

        /// <summary>
        /// Shows <paramref name="leaf"/>. Cheap to call again with what is already shown: the
        /// text is only laid out again when the leaf changed since.
        /// </summary>
        public void Bind(IClientService clientService, RichTextLeaf leaf, RichLeafAppearance appearance, string placeholder, string language = null)
        {
            _clientService = clientService;

            if (_appearance != appearance)
            {
                _appearance = appearance;
                VisualStateManager.GoToState(this, appearance.ToString(), false);
            }

            _leaf = leaf;
            _placeholderText = placeholder;
            _language = language;

            Render();
        }

        /// <summary>The hint shown while the leaf is empty; it follows the caret, so it changes on its own.</summary>
        public void SetPlaceholder(string placeholder)
        {
            if (_placeholderText != placeholder)
            {
                _placeholderText = placeholder;

                if (_leaf != null)
                {
                    UpdatePlaceholder();
                }
            }
        }

        public void Render()
        {
            if (Text == null || _leaf == null)
            {
                return;
            }

            UpdatePlaceholder();

            // What a line with no strong character of its own reads as: the editor's direction,
            // which is the article's, or the app's for a message's text. Kept with the rest, as
            // the article's direction can change under a line that did not.
            var rightToLeft = FlowDirection == FlowDirection.RightToLeft;

            if (ReferenceEquals(_renderedLeaf, _leaf) && _renderedVersion == _leaf.Version && _renderedLanguage == _language && _renderedRightToLeft == rightToLeft)
            {
                return;
            }

            _renderedLeaf = _leaf;
            _renderedVersion = _leaf.Version;
            _renderedLanguage = _language;
            _renderedRightToLeft = rightToLeft;

            var text = _leaf.Text;
            var display = text.Length == 0 || text[text.Length - 1] == '\n'
                ? text + LineHolder
                : text;

            BuildParts();

            // Each line runs the way its first strong character does, as a message's paragraph
            // does: an Arabic line in an English article reads right to left, and the other way.
            var direction = text.Length > 0 ? NativeUtils.GetDirectionality(text) : TextDirectionality.Neutral;
            if (direction == TextDirectionality.Neutral)
            {
                direction = rightToLeft ? TextDirectionality.RightToLeft : TextDirectionality.LeftToRight;
            }

            Text.SetText(display, _parts.Count > 0 ? _parts : null, direction);

            if (_leaf.IsPlain)
            {
                Text.SetCode(string.IsNullOrEmpty(_language) ? null : _language);
            }
            else
            {
                Text.SetCustomEmoji(_clientService, _emoji.Count > 0 ? _emoji : null);
                Text.SetInlineButtons(_clientService, _buttons.Count > 0 ? _buttons : null);
                Text.SetMath(_math.Count > 0 ? _math : null);

                ApplyLinkColors();
            }

            // SetText clears the selection the block drew; the editor's still stands.
            Text.Select(_selectionStart, _selectionEnd);
        }

        private void UpdatePlaceholder()
        {
            if (Placeholder == null)
            {
                return;
            }

            var visible = _leaf.IsEmpty && !string.IsNullOrEmpty(_placeholderText);
            Placeholder.Text = visible ? _placeholderText : string.Empty;
            Placeholder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BuildParts()
        {
            _parts.Clear();
            _emoji.Clear();
            _math.Clear();
            _buttons.Clear();

            if (_leaf.IsPlain)
            {
                return;
            }

            var runs = _leaf.Runs;

            // A part per style, not one per run with the styles combined: the layout matches
            // each part's type against one style, so a combined part matches none of them and a
            // bold italic word is drawn plain. Neighbouring runs sharing a style share its part.
            for (int s = 0; s < _styles.Length; s++)
            {
                var style = _styles[s];
                var start = -1;
                var position = 0;

                for (int i = 0; i <= runs.Count; i++)
                {
                    var has = i < runs.Count && (ToStyle(runs[i]) & style) != 0;

                    if (has && start < 0)
                    {
                        start = position;
                    }
                    else if (!has && start >= 0)
                    {
                        _parts.Add(new TextStylePart
                        {
                            Offset = start,
                            Length = position - start,
                            Type = style
                        });

                        start = -1;
                    }

                    if (i < runs.Count)
                    {
                        position += runs[i].Length;
                    }
                }
            }

            var offset = 0;

            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                var position = offset;

                if (run.Object is RichInlineObject inline)
                {
                    switch (inline.Kind)
                    {
                        case RichInlineKind.CustomEmoji:
                            _emoji.Add(new DirectTextBlock.CustomEmojiRange(position, 1, inline.CustomEmojiId));
                            break;
                        case RichInlineKind.Math:
                            _math.Add(new DirectTextBlock.MathRange(position, 1, inline.Text));
                            break;
                        case RichInlineKind.Button when inline.Button != null:
                            _buttons.Add(new DirectTextBlock.InlineButtonRange(position, 1, inline.Button));
                            break;
                    }
                }

                offset += run.Length;
            }
        }

        private static readonly TextStyle[] _styles =
        {
            TextStyle.Bold,
            TextStyle.Italic,
            TextStyle.Underline,
            TextStyle.Strikethrough,
            TextStyle.Monospace,
            TextStyle.Subscript,
            TextStyle.Superscript,
            TextStyle.Url,
            TextStyle.Mention,
            TextStyle.Button,
            TextStyle.Math,
        };

        /// <summary>The ranges of the leaf carrying <paramref name="mark"/>, for what the layout cannot draw itself.</summary>
        public void GetMarkRanges(RichMarks mark, List<(int Start, int End)> ranges)
        {
            ranges.Clear();

            if (_leaf == null)
            {
                return;
            }

            var start = -1;
            var position = 0;
            var runs = _leaf.Runs;

            for (int i = 0; i <= runs.Count; i++)
            {
                var has = i < runs.Count && (runs[i].Marks & mark) != 0;

                if (has && start < 0)
                {
                    start = position;
                }
                else if (!has && start >= 0)
                {
                    ranges.Add((start, position));
                    start = -1;
                }

                if (i < runs.Count)
                {
                    position += runs[i].Length;
                }
            }
        }

        private static TextStyle ToStyle(in RichTextRun run)
        {
            var style = TextStyle.None;
            var marks = run.Marks;

            if ((marks & RichMarks.Bold) != 0) style |= TextStyle.Bold;
            if ((marks & RichMarks.Italic) != 0) style |= TextStyle.Italic;
            if ((marks & RichMarks.Underline) != 0) style |= TextStyle.Underline;
            if ((marks & RichMarks.Strikethrough) != 0) style |= TextStyle.Strikethrough;
            if ((marks & RichMarks.Monospace) != 0) style |= TextStyle.Monospace;
            if ((marks & RichMarks.Marked) != 0) style |= TextStyle.Marked;
            if ((marks & RichMarks.Subscript) != 0) style |= TextStyle.Subscript;
            if ((marks & RichMarks.Superscript) != 0) style |= TextStyle.Superscript;

            // Spoilers stay readable while being written: hiding them is what sending does. The
            // editor marks them in its decorations layer instead.
            if (run.Link != null)
            {
                style |= run.Link.Kind == RichLinkKind.MentionName ? TextStyle.Mention : TextStyle.Url;
            }

            if (run.Object?.Kind == RichInlineKind.Button)
            {
                style |= TextStyle.Button;
            }
            else if (run.Object?.Kind == RichInlineKind.Math)
            {
                style |= TextStyle.Math;
            }

            return style;
        }

        private void ApplyLinkColors()
        {
            // The colour the block's own style gives links, read back from it rather than looked
            // up, so the theme stays the one place it is decided.
            if (Text.HyperlinkForeground is not SolidColorBrush brush)
            {
                return;
            }

            var color = brush.Color;
            var runs = _leaf.Runs;
            var position = 0;

            for (int i = 0; i < runs.Count; i++)
            {
                if (runs[i].Link != null && runs[i].Object == null)
                {
                    Text.SetColor(position, runs[i].Length, color);
                }

                position += runs[i].Length;
            }
        }

        // The block's style can reach it after the text was first drawn, and a theme changes the
        // colour later: the links are coloured again from it either way.
        private void OnHyperlinkColorChanged(object sender, EventArgs e)
        {
            if (_leaf != null && !_leaf.IsPlain && Text != null)
            {
                ApplyLinkColors();
            }
        }

        private void OnTextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (e.Type is Td.Api.TextEntityTypeButton button)
            {
                ObjectClick?.Invoke(this, new RichInlineObjectClickEventArgs(_leaf, button.Button));
            }
        }

        // ---------------------------------------------------------------------------------
        // Geometry. Points and rectangles are in this control's coordinates.
        //
        // The text block is pinned left to right (see the template), so in a right to left
        // editor its x runs the other way from this control's: every crossing flips it here,
        // and everything outside, which shares this control's direction, reads straight.
        // ---------------------------------------------------------------------------------

        private bool IsMirrored => FlowDirection == FlowDirection.RightToLeft && Text.FlowDirection == FlowDirection.LeftToRight;

        // A rectangle of the text block's, in this control's coordinates.
        private Rect FromText(Rect rect, Vector3 offset, bool mirrored)
        {
            var x = mirrored
                ? offset.X + Text.ActualSize.X - rect.X - rect.Width
                : offset.X + rect.X;

            return new Rect(x, rect.Y + offset.Y, rect.Width, rect.Height);
        }

        public void Select(int start, int end)
        {
            _selectionStart = start;
            _selectionEnd = end;

            Text?.Select(start, end);
        }

        public int GetPositionFromPoint(Point point)
        {
            if (Text == null || _leaf == null || _leaf.IsEmpty)
            {
                return 0;
            }

            var offset = Text.ActualOffset;
            var x = IsMirrored
                ? offset.X + Text.ActualSize.X - point.X
                : point.X - offset.X;

            var position = Text.GetPositionFromPoint(new Point(x, point.Y - offset.Y), out _);

            return Math.Clamp(position, 0, _leaf.Length);
        }

        /// <summary>
        /// The start or the end of the visual line at <paramref name="y"/>: where Home and End go.
        /// Asked in the text block's own coordinates, which are left to right whatever the
        /// editor's are, so the line's direction alone says which side its start is on.
        /// </summary>
        public int GetLineEdge(double y, bool end)
        {
            if (Text == null || _leaf == null || _leaf.IsEmpty)
            {
                return 0;
            }

            var x = end != Text.IsRightToLeft ? 100000d : -100000d;
            var position = Text.GetPositionFromPoint(new Point(x, y - Text.ActualOffset.Y), out _);

            return Math.Clamp(position, 0, _leaf.Length);
        }

        public Rect GetCaretRect(int position)
        {
            if (Text == null || _leaf == null)
            {
                return Rect.Empty;
            }

            var rect = Text.GetCaretRectangle(Math.Clamp(position, 0, _leaf.Length));
            var offset = Text.ActualOffset;

            if (rect.Height <= 0)
            {
                // Before any text, at the edge the line starts from.
                return new Rect(Text.IsRightToLeft != IsMirrored ? ActualSize.X : 0, 0, 0, Math.Max(ActualHeight, 16));
            }

            rect = FromText(new Rect(rect.X, rect.Y, 0, rect.Height), offset, IsMirrored);
            return new Rect(rect.X, rect.Y, 0, rect.Height);
        }

        public Rect[] GetRangeRects(int start, int end)
        {
            if (Text == null || _leaf == null || end <= start)
            {
                return Array.Empty<Rect>();
            }

            var rects = Text.GetRangeRectangles(start, end);
            var offset = Text.ActualOffset;
            var mirrored = IsMirrored;

            for (int i = 0; i < rects?.Length; i++)
            {
                rects[i] = FromText(rects[i], offset, mirrored);
            }

            return rects ?? Array.Empty<Rect>();
        }
    }

    public sealed class RichInlineObjectClickEventArgs : EventArgs
    {
        public RichInlineObjectClickEventArgs(RichTextLeaf leaf, Td.Api.InlineButton button)
        {
            Leaf = leaf;
            Button = button;
        }

        public RichTextLeaf Leaf { get; }

        public Td.Api.InlineButton Button { get; }
    }
}
