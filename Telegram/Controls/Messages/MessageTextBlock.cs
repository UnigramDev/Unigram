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
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Controls.Messages
{
    /// <summary>
    /// Hosts one or more <see cref="FormattedTextBlock"/>s to render a <see cref="StyledText"/>.
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
        }

        public bool HasCodeBlocks { get; private set; }

        public event EventHandler<TextEntityClickEventArgs> TextEntityClick;

        #region Forwarded configuration

        private FormattedTextBlockRecyclePool _recyclePool;
        public FormattedTextBlockRecyclePool RecyclePool
        {
            get => _recyclePool;
            set
            {
                _recyclePool = value;

                foreach (var block in _blocks)
                {
                    block.RecyclePool = value;
                }
            }
        }

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

        private bool _ignoreSpoilers;
        public bool IgnoreSpoilers
        {
            get => _ignoreSpoilers;
            set
            {
                _ignoreSpoilers = value;

                foreach (var block in _blocks)
                {
                    block.IgnoreSpoilers = value;
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
        }

        public void ShowHideSkeleton(bool show)
        {
            _showSkeleton = show;

            foreach (var block in _blocks)
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

        public void SetText(IClientService clientService, string text, IList<TextEntity> entities, double fontSize = 0)
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
                ClearBlocks();
                HasCodeBlocks = false;
                return;
            }

            // Hot path: no code/quote paragraphs -> the whole message is one block (the vast
            // majority of messages). Reuse the existing single plain block (recycled bubble
            // showing a new simple message) instead of re-creating the templated control.
            if (!styled.IsComplex)
            {
                var last = styled.Paragraphs.Count - 1;

                if (_blocks.Count == 1 && Children.Count == 1 && Children[0] == _blocks[0])
                {
                    var block = _blocks[0];
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

        public Block GetBlock(int index, out double width, out Point adjustment)
        {
            width = 0;
            adjustment = default;

            for (int i = 0; i < _ranges.Count; i++)
            {
                //var child = Children[i] as FrameworkElement;
                //var height = child.ActualHeight;

                if (_ranges[i].First <= index && _ranges[i].Last >= index)
                {
                    width = _blocks[i].LastAvailableWidth;
                    adjustment = _blocks[i].TransformToPoint(this);
                    return _blocks[i].Blocks[index - _ranges[i].First];
                }

                //y += _blocks[i].ActualHeight;
            }

            return null;
        }

        public void SetQuery(string query, bool force = false)
        {
            _query = query;

            foreach (var block in _blocks)
            {
                block.SetQuery(query, force);
            }
        }

        public void Clear()
        {
            _styled = null;
            _query = null;
            HasCodeBlocks = false;

            ClearBlocks();
        }

        // Drops all blocks. Removing them from Children unloads each FormattedTextBlock, whose
        // OnUnloaded returns its Runs/Paragraphs to the shared RecyclePool.
        private void ClearBlocks()
        {
            _blocks.Clear();
            _ranges.Clear();
            Children.Clear();
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
                IgnoreSpoilers = _ignoreSpoilers,
                HorizontalTextAlignment = TextAlignment.DetectFromContent,
                TextReadingOrder = TextReadingOrder.UseFlowDirection,
                AdjustLineEnding = true,
            };

            if (_recyclePool != null)
            {
                block.RecyclePool = _recyclePool;
            }

            block.TextEntityClick += OnBlockTextEntityClick;
            return block;
        }

        private void OnBlockTextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            // Forward with the originating inner block as sender (callers expect a FormattedTextBlock).
            TextEntityClick?.Invoke(sender, e);
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
                else if (child is not FormattedTextBlock)
                {
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
                else if (child is not FormattedTextBlock)
                {
                    y += 4;
                }

                var width = child.DesiredSize.Width;
                var height = child.DesiredSize.Height;

                if (child is BlockQuote { ComputedIsExpandable: false })
                {
                    width = Math.Min(child.DesiredSize.Width, finalSize.Width);
                }
                else
                {
                    width = finalSize.Width;
                }

                child.Arrange(new Rect(0, y, width, height));

                y += height;
                first = false;
            }

            return finalSize;
        }

        #endregion
    }
}
