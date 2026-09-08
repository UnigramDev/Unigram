//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages
{
    /// <summary>
    /// Hosts one or more text blocks to render a <see cref="StyledText"/> - either
    /// <see cref="FormattedTextBlock"/>s or, behind the direct text flag,
    /// <see cref="DirectTextBlock"/>s.
    ///
    /// A plain message (only normal paragraphs) is rendered by a SINGLE inner block — the
    /// hot path, since this is the most instantiated text surface in the app. When the text
    /// contains code blocks or quotes, the styled text is split so that each code/quote
    /// paragraph gets its own block while runs of normal paragraphs still share one block
    /// (e.g. four consecutive normal paragraphs stay in a single block).
    ///
    /// The control lays its children out exactly like a vertical <see cref="StackPanel"/>,
    /// with an optional fixed <see cref="BlockSpacing"/> between them.
    /// </summary>
    public partial class MessageTextBlock : Panel
    {
        // Gap inserted between adjacent inner blocks. Code/quote blocks already carry their
        // own internal padding, so 0 reproduces the single-block look; bump it if separate
        // blocks should breathe more.
        private const double BlockSpacing = 4;

        // The inner blocks (1:1 with the visible Children, in document order) and their
        // paragraph ranges into the shared StyledText. Both reused across SetText calls.
        private readonly List<FormattedTextBlock> _blocks = new();
        private readonly List<(int First, int Last)> _ranges = new();

        private IClientService _clientService;
        private StyledText _styled;
        private double _fontSize;
        private string _query;
        private bool _showSkeleton;

        public MessageTextBlock()
        {
            // Match RichTextBlock's default text-y baseline behaviour for stacking.
            Instrumentation.Register(this);

            TextThroughput.HostsMade++;
        }

        public bool HasCodeBlocks { get; private set; }

        // TODO: make sure all this event thing is actually needed
        private event EventHandler<TextEntityClickEventArgs> _textEntityClick;
        public event EventHandler<TextEntityClickEventArgs> TextEntityClick
        {
            add
            {
                if (_textEntityClick == null)
                {
                    foreach (var block in _blocks)
                    {
                        block.TextEntityClick += OnBlockTextEntityClick;
                    }
                }

                _textEntityClick += value;
            }
            remove
            {
                _textEntityClick -= value;

                if (_textEntityClick == null)
                {
                    foreach (var block in _blocks)
                    {
                        block.TextEntityClick -= OnBlockTextEntityClick;
                    }
                }
            }
        }

        #region Forwarded configuration

        private bool _autoFontSize = true;
        public bool AutoFontSize
        {
            get => _autoFontSize;
            set
            {
                _autoFontSize = value;

                foreach (var block in _blocks)
                {
                    block.AutoFontSize = value;
                }
            }
        }

        public void SetFontSize(double fontSize)
        {
            _fontSize = fontSize;

            foreach (var block in _blocks)
            {
                block.SetFontSize(fontSize);
            }

            // A size settled per block, and a quote does not take the same one: applied by the
            // same pass that laid the blocks out rather than written over them here.
            if (_directBlocks.Count > 0 && _styled != null)
            {
                ApplyDirect();
            }
        }

        public void ShowHideSkeleton(bool show)
        {
            _showSkeleton = show;

            foreach (var block in _blocks)
            {
                block.ShowHideSkeleton(show);
            }

            foreach (var block in _directBlocks)
            {
                block.ShowHideSkeleton(show);
            }
        }

        #endregion

        #region SetText

        public void SetText(IClientService clientService, FormattedText text, double fontSize = 0)
        {
            SetText(clientService, TextStyleRun.GetText(text), fontSize);
        }

        public void SetText(IClientService clientService, string text, Vector<TextEntity> entities, double fontSize = 0)
        {
            SetText(clientService, TextStyleRun.GetText(text, entities), fontSize);
        }

        public void SetText(IClientService clientService, StyledText styled, double fontSize = 0)
        {
            // Hot path: the exact same styled text re-applied (recycled bubble re-rendered,
            // re-measure, ...). message.Text is a cached StyledText, so this fires often — the
            // tree already matches, nothing to rebuild.
            if (clientService == _clientService && styled == _styled && fontSize == _fontSize)
            {
                return;
            }

            _clientService = clientService;
            _styled = styled;
            _fontSize = fontSize;

            if (styled == null || string.IsNullOrEmpty(styled.Text))
            {
                // A media message with no caption, in a bubble that showed text a moment ago
                // and will again: the blocks are put away rather than torn down, because what
                // they hold is exactly what the next one would have to build - a DirectWrite
                // layout and a region of the device's atlas on one engine, a templated control
                // over a native base on the other. This is where most of the tearing down was
                // coming from, and almost none of it was quotes or code.
                RecycleBlocks();
                HasCodeBlocks = false;
                return;
            }

            // Hot path: no code/quote paragraphs -> the whole message is one block (the vast
            // majority of messages). Reuse the existing single plain block (recycled bubble
            // showing a new simple message) instead of re-creating the templated control.
            if (_directText)
            {
                ApplyDirect();
                return;
            }

            if (!styled.IsComplex)
            {
                var last = styled.Paragraphs.Count - 1;

                if (_blocks.Count == 1 && Children.Count == 1 && Children[0] == _blocks[0])
                {
                    var block = _blocks[0];

                    if (block.Visibility != Visibility.Visible)
                    {
                        block.Visibility = Visibility.Visible;
                    }

                    block.ShowHideSkeleton(_showSkeleton);
                    block.SetText(_clientService, styled, 0, last, _fontSize);
                    block.SetQuery(_query);
                    _ranges[0] = (0, last);
                }
                else
                {
                    ClearBlocks();
                    ApplyBlock(0, last);
                }

                HasCodeBlocks = false;
                return;
            }

            // Complex: split into per-block ranges in a single pass (no intermediate list).
            // Each code/quote paragraph is its own block; runs of normal paragraphs share one.
            ClearBlocks();

            var paragraphs = styled.Paragraphs;
            var hasCode = false;
            var normalStart = -1;

            for (int i = 0; i < paragraphs.Count; i++)
            {
                if (paragraphs[i].Type != null)
                {
                    if (normalStart >= 0)
                    {
                        hasCode |= ApplyBlock(normalStart, i - 1).HasCodeBlocks;
                        normalStart = -1;
                    }

                    hasCode |= ApplyBlock(i, i).HasCodeBlocks;
                }
                else if (normalStart < 0)
                {
                    normalStart = i;
                }
            }

            if (normalStart >= 0)
            {
                hasCode |= ApplyBlock(normalStart, paragraphs.Count - 1).HasCodeBlocks;
            }

            HasCodeBlocks = hasCode;
        }

        /// <summary>
        /// The rectangles covering a range of the message text, in this panel's coordinates -
        /// the blocks answer in their own, and this offsets each by where it sits.
        /// </summary>
        public IList<Rect> GetHighlightRectangles(int from, int to)
        {
            List<Rect> result = null;

            for (int i = 0; i < _directBlocks.Count; i++)
            {
                var rects = _directBlocks[i].GetHighlightRectangles(from, to);

                if (rects == null)
                {
                    continue;
                }

                var offset = _directBlocks[i].TransformToPoint(this);

                for (int j = 0; j < rects.Count; j++)
                {
                    (result ??= new List<Rect>()).Add(new Rect(
                        rects[j].X + offset.X,
                        rects[j].Y + offset.Y,
                        rects[j].Width,
                        rects[j].Height));
                }
            }

            for (int i = 0; i < _blocks.Count; i++)
            {
                var rects = _blocks[i].GetHighlightRectangles(from, to);

                if (rects == null)
                {
                    continue;
                }

                var offset = _blocks[i].TransformToPoint(this);

                for (int j = 0; j < rects.Count; j++)
                {
                    (result ??= new List<Rect>()).Add(new Rect(
                        rects[j].X + offset.X,
                        rects[j].Y + offset.Y,
                        rects[j].Width,
                        rects[j].Height));
                }
            }

            return result;
        }

        public void SetQuery(string query, bool force = false)
        {
            _query = query;

            foreach (var block in _blocks)
            {
                block.SetQuery(query, force);
            }

            foreach (var block in _directBlocks)
            {
                block.SetQuery(query, force);
            }
        }

        // What a recycled bubble does to the text it was showing. The inline blocks go back to
        // their pool; the direct ones stay where they are, because what they hold - a
        // DirectWrite layout and the region of the atlas it draws into - is what the next
        // message would otherwise build again, and building it again per message is the single
        // largest thing recycling can save here.
        public void Clear()
        {
            _styled = null;
            _query = null;
            HasCodeBlocks = false;

            RecycleBlocks();
        }

        // Everything the last message put in the blocks, without the blocks themselves: each
        // keeps its slot and goes collapsed until it is given text again. Collapsed rather than
        // emptied because both engines still hold what they last rendered - a layout and the
        // surface it was drawn into, or a tree of inlines - and a block left visible would show
        // text the bubble no longer has.
        private void RecycleBlocks()
        {
            foreach (var block in _directBlocks)
            {
                block.Recycle();
            }

            foreach (var block in _blocks)
            {
                block.Clear();

                if (block.Visibility != Visibility.Collapsed)
                {
                    block.Visibility = Visibility.Collapsed;
                }
            }
        }

        // Drops all blocks. Removing them from Children unloads each FormattedTextBlock, whose
        // OnUnloaded returns its Runs/Paragraphs to the shared RecyclePool.
        //
        // Clear() is called explicitly rather than left to that: a block only receives Unloaded
        // if it was Loaded first (FrameworkElementEx tracks the pair), and a block that was
        // measured - which is when the template applies and SetText runs - but dropped before
        // its Loaded arrived would never release. What it holds in that case is a relative date
        // registration in the thread-static RelativeDateService, which pins the block, its
        // paragraph and its runs for the rest of the session and goes on ticking them.
        private void ClearBlocks()
        {
            foreach (var block in _blocks)
            {
                if (_textEntityClick != null)
                {
                    block.TextEntityClick -= OnBlockTextEntityClick;
                }

                block.Clear();
            }

            _blocks.Clear();
            _ranges.Clear();
            Children.Clear();

            foreach (var block in _directBlocks)
            {
                block.Clear();
            }

            _directBlocks.Clear();
        }

        #endregion
        #region Direct

        // Read once per process, like the chat cell: a message list never mixes the two.
        private static readonly bool _directText = AppSettings.Diagnostics.DirectTextDebug;

        /// <summary>
        /// Which engine this process builds messages with, for the diagnostics page: the flag is
        /// read once, so it cannot be answered by reading the setting back.
        /// </summary>
        public static bool IsDirectText => _directText;

        // The blocks the direct engine renders into, when it is the one in use. They replace
        // the whole of _blocks: one for a message with nothing but ordinary paragraphs - which
        // is most of them - and otherwise one per quote or code block plus one per run of
        // ordinary paragraphs between them, exactly as the inline path splits it.
        private readonly List<DirectTextBlock> _directBlocks = new();

        private void ApplyDirect()
        {
            var paragraphs = _styled.Paragraphs;

            // One block for the whole message, and it keeps its slot when it already has one:
            // the layout it holds, and the surface that layout draws into, are what tearing it
            // down would throw away. Only a quote or a code block splits a message now - the
            // layout stacks a paragraph each, so ones that read different ways still share it.
            if (!_styled.IsComplex)
            {
                if (_directBlocks.Count != 1 || Children.Count != 1 || Children[0] != _directBlocks[0])
                {
                    TextThroughput.BlocksCleared++;

                    if (_directBlocks.Count == 0)
                    {
                        TextThroughput.BlocksClearedEmpty++;
                    }

                    ClearBlocks();
                }

                ApplyDirectBlock(0, 0, paragraphs.Count - 1, null);

                HasCodeBlocks = false;
                return;
            }

            // Built again rather than reused: a slot can go from a bare block to one inside a
            // quote and back, and moving a block between the two costs more care than it saves
            // for a shape of message this rare.
            TextThroughput.BlocksCleared++;
            TextThroughput.BlocksClearedComplex++;

            ClearBlocks();

            var index = 0;
            var normalStart = -1;
            var hasCode = false;

            for (int i = 0; i < paragraphs.Count; i++)
            {
                if (paragraphs[i].Type != null)
                {
                    if (normalStart >= 0)
                    {
                        ApplyDirectBlock(index++, normalStart, i - 1, null);
                        normalStart = -1;
                    }

                    hasCode |= paragraphs[i].Type is TextParagraphTypeMonospace;
                    ApplyDirectBlock(index++, i, i, paragraphs[i].Type);
                }
                else if (normalStart < 0)
                {
                    normalStart = i;
                }
            }

            if (normalStart >= 0)
            {
                ApplyDirectBlock(index, normalStart, paragraphs.Count - 1, null);
            }

            HasCodeBlocks = hasCode;
        }

        // The slot is either already this block - the one message shape that reuses it - or it
        // does not exist yet, because everything else got here through ClearBlocks.
        private void ApplyDirectBlock(int index, int first, int last, TextParagraphType type)
        {
            var block = GetOrCreateDirect(index);

            if (block.Visibility != Visibility.Visible)
            {
                block.Visibility = Visibility.Visible;
            }
            var quote = type as TextParagraphTypeQuote;
            var monospace = type as TextParagraphTypeMonospace;

            // A quote reads at the caption size, as the inline path renders it; an explicit
            // size - the one a big-emoji message asks for - is the size of everything.
            block.FontSize = (_fontSize > 0
                ? _fontSize
                : quote != null
                ? AppSettings.Appearance.CaptionFontSize
                : AppSettings.Appearance.MessageFontSize) * BootStrapper.Current.TextScaleFactor;

            // An expandable quote shows three lines and offers the rest; everything else is
            // as long as it is.
            block.MaxLines = quote is { IsExpandable: true } ? 3 : 0;

            // The styled text and everything it says - spoilers, emoji, buttons, links - is
            // the block's to read: it renders them, so it maps them.
            block.SetText(_clientService, _styled, first, last);

            // Colours over the ranges the tokenizer finds, once it answers. Told even with no
            // language: this block may have rendered code for the message before this one.
            block.SetCode(monospace?.Language);

            // What the bubble last asked of the message as a whole, applied to the block that
            // renders this part of it.
            block.SetQuery(_query);
            block.ShowHideSkeleton(_showSkeleton);

            if (index < Children.Count)
            {
                return;
            }

            // A quote and a code block are hosted in a BlockQuote, anything else stands on its
            // own. ComputedIsExpandable asks its content whether the text was trimmed and a
            // direct block cannot answer yet, so an expandable quote renders as a plain one.
            if (type != null)
            {
                Children.Add(new BlockQuote
                {
                    Glyph = quote != null ? Icons.QuoteBlockFilled16 : null,
                    LanguageName = monospace?.Language,
                    IsExpandable = quote is { IsExpandable: true },
                    Content = block,
                    Padding = new Thickness(8, 4, 24, 6)
                });
            }
            else
            {
                Children.Add(block);
            }
        }

        private DirectTextBlock GetOrCreateDirect(int index)
        {
            if (index < _directBlocks.Count)
            {
                return _directBlocks[index];
            }

            TextThroughput.BlocksMade++;

            var block = new DirectTextBlock
            {
                // Not a drag of its own: the bubble drives one selection across every
                // block through TextSelectionManager, and this takes part in that.
                IsTextSelectionEnabled = false,
                IsSelectionEnabled = true
            };

            block.TextEntityClick += OnDirectTextEntityClick;
            Instrumentation.Register(block);

            _directBlocks.Add(block);
            return block;
        }

        private void OnDirectTextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            _textEntityClick?.Invoke(this, e);
        }

        #endregion

        #region Blocks

        private FormattedTextBlock ApplyBlock(int first, int last)
        {
            var block = CreateBlock();
            block.ShowHideSkeleton(_showSkeleton);
            block.SetText(_clientService, _styled, first, last, _fontSize);
            block.SetQuery(_query);
            block.TextSelection = TextSelectionMode.Extended;

            if (first == last && _styled.Paragraphs[first].Type is TextParagraphTypeQuote quote)
            {
                block.MaxLines = quote.IsExpandable ? 3 : 0;
                block.TextTrimming = TextTrimming.CharacterEllipsis;

                Children.Add(new BlockQuote
                {
                    Glyph = Icons.QuoteBlockFilled16,
                    IsExpandable = quote.IsExpandable,
                    Content = block,
                    Padding = new Thickness(8, 4, 24, 6)
                });
            }
            else if (first == last && _styled.Paragraphs[first].Type is TextParagraphTypeMonospace monospace)
            {
                Children.Add(new BlockQuote
                {
                    LanguageName = monospace.Language,
                    Content = block,
                    Padding = new Thickness(8, 4, 24, 6)
                });
            }
            else
            {
                block.MaxLines = 0;

                Children.Add(block);
            }

            _blocks.Add(block);
            _ranges.Add((first, last));

            return block;
        }

        private FormattedTextBlock CreateBlock()
        {
            var block = new FormattedTextBlock
            {
                AutoFontSize = _autoFontSize,
                HorizontalTextAlignment = TextAlignment.DetectFromContent,
                TextReadingOrder = TextReadingOrder.UseFlowDirection,
            };

            Instrumentation.Register(block);

            // !!!
            if (_textEntityClick != null)
            {
                block.TextEntityClick += OnBlockTextEntityClick;
            }

            return block;
        }

#if INSTRUMENTATION
        internal IEnumerable<object> DebugChildren() => _blocks;
#endif

        private void OnBlockTextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            // Forward with the originating inner block as sender (callers expect a FormattedTextBlock).
            _textEntityClick?.Invoke(sender, e);
        }

        #endregion

        #region Layout (vertical stack)

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = 0d;
            var height = 0d;
            var first = true;

            foreach (var child in Children)
            {
                child.Measure(new Size(availableSize.Width, double.PositiveInfinity));

                var desired = child.DesiredSize;
                width = Math.Max(width, desired.Width);

                if (!first)
                {
                    height += BlockSpacing;
                }
                else if (child is BlockQuote)
                {
                    // A quote or a code block opening the message needs the gap a text block
                    // does not. Asked of the type that needs it, rather than of everything
                    // that is not the one text block there used to be.
                    height += 4;
                }

                height += desired.Height;
                first = false;
            }

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var y = 0d;
            var first = true;

            foreach (var child in Children)
            {
                if (!first)
                {
                    y += BlockSpacing;
                }
                else if (child is BlockQuote)
                {
                    y += 4;
                }

                var width = finalSize.Width;
                var height = child.DesiredSize.Height;
                var x = 0d;

                if (child is BlockQuote { ComputedIsExpandable: false })
                {
                    width = Math.Min(child.DesiredSize.Width, finalSize.Width);
                }
                else if (child is FrameworkElement { HorizontalAlignment: HorizontalAlignment.Right })
                {
                    // A block that reads right to left asks for the width of its text and sits
                    // against the far edge, the way a right-aligned element does in any panel.
                    width = Math.Min(child.DesiredSize.Width, finalSize.Width);
                    x = finalSize.Width - width;
                }

                child.Arrange(new Rect(x, y, width, height));

                y += height;
                first = false;
            }

            return finalSize;
        }

        #endregion
    }
}
