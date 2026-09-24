//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Controls.Messages.Content;
using Telegram.Services;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public partial class MessageBubblePanel : Panel
    {
        // Needed for Text CanvasTextLayout
        public bool ForceNewLine { get; set; }

        public bool Placeholder { get; set; } = true;

        private Size _margin;

        // The children, found by one method rather than by the same index arithmetic written
        // out in both passes. The template declares text, media and footer in that order; a
        // fact check or a summary is inserted in front of them from code behind and the
        // reactions panel is realized between media and footer when there are reactions, so
        // where each one sits has to be found rather than assumed.
        //
        // Resolved again in arrange, not carried over from measure: both of those come and go,
        // and a field that outlives the pass names an element that may already be gone from
        // Children - and holds it alive until the next one.
        private UIElement _factCheck;
        private MessageTextBlock _text;
        private FrameworkElement _media;
        private ReactionsPanel _reactions;
        private MessageFooter _footer;

        // The row each was given. The bubble moves the text above or below the media and puts
        // the footer in one of their rows, which is how it says whether the footer shares the
        // last line of the text.
        private int _textRow;
        private int _mediaRow;
        private int _footerRow;

        private void Resolve()
        {
            var index = 0;

            if (Children[index] is MessageFactCheck or MessageSummary)
            {
                _factCheck = Children[index++];
            }
            else
            {
                _factCheck = null;
            }

            _text = Children[index++] as MessageTextBlock;
            _media = Children[index++] as FrameworkElement;
            _reactions = Children[index] as ReactionsPanel;

            if (_reactions != null)
            {
                index++;
            }

            _footer = Children[index] as MessageFooter;

            _textRow = Grid.GetRow(_text);
            _mediaRow = Grid.GetRow(_media);
            _footerRow = Grid.GetRow(_footer);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Resolve();

            _text.Measure(availableSize);
            _media.Measure(availableSize);
            _factCheck?.Measure(availableSize);
            _footer.Measure(availableSize);

            if (_reactions != null)
            {
                if (_reactions.Footer != _footer.DesiredSize && _reactions.Children.Count > 0)
                {
                    _reactions.InvalidateMeasure();
                }

                _reactions.Footer = _footer.DesiredSize;
                _reactions.Measure(availableSize);
            }

            // The width the room the footer needs beside the last line is added to. The text for a
            // plain message; for a rich one the text block is empty and the last line belongs to a
            // block inside the media, so it is the article that has to widen.
            var footerTarget = _text.DesiredSize.Width;

            if (_reactions != null && _reactions.HasReactions)
            {
                _margin = new Size(0, 0);
            }
            else if (_textRow == _footerRow && _text.Children.Count > 0)
            {
                _margin = Margins(availableSize.Width, 0, _text.DesiredSize.Width, _text.Children[^1], _footer);
            }
            else if (_mediaRow == _footerRow)
            {
                _margin = new Size(0, 0);
            }
            else if (_media is Border { Child: InstantContent rich })
            {
                if (rich.LastBlock is FormattedTextBlock lastBlock)
                {
                    footerTarget = _media.DesiredSize.Width;
                    _margin = Margins(availableSize.Width, LeftInset(lastBlock, _media), footerTarget, lastBlock, _footer);
                }
                else
                {
                    // The article ends on something the footer cannot share a line with - a photo,
                    // a table, the "Show more" button - so it takes a line of its own.
                    _margin = new Size(0, _footer.DesiredSize.Height);
                }
            }
            else
            {
                _margin = new Size(0, _footer.DesiredSize.Height);
            }

            var margin = _margin;
            var width = _media.DesiredSize.Width == availableSize.Width
                ? _media.DesiredSize.Width
                : Math.Max(_media.DesiredSize.Width, footerTarget + margin.Width);

            var reactionsWidth = _reactions?.DesiredSize.Width ?? 0;
            var reactionsHeight = _reactions?.DesiredSize.Height ?? 0;

            if (_factCheck != null)
            {
                reactionsWidth = Math.Max(reactionsWidth, _factCheck.DesiredSize.Width);
                reactionsHeight += _factCheck.DesiredSize.Height;
            }

            var finalWidth = Math.Max(Math.Max(reactionsWidth, _footer.DesiredSize.Width), width);
            var finalHeight = _text.DesiredSize.Height + _media.DesiredSize.Height + reactionsHeight + margin.Height;

            // A single line of text is ~24 pixels, we force it to 26 so that the total bubble height is at least 30 pixels
            if (finalHeight >= 24)
            {
                finalHeight = Math.Max(finalHeight, 26);
            }

            return new Size(finalWidth, finalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Resolve();

            if (_textRow < _mediaRow)
            {
                _text.Arrange(new Rect(0, 0, finalSize.Width, _text.DesiredSize.Height));

                if (_factCheck != null)
                {
                    _factCheck.Arrange(new Rect(0, _text.DesiredSize.Height, finalSize.Width, _factCheck.DesiredSize.Height));
                    _media.Arrange(new Rect(0, _text.DesiredSize.Height + _factCheck.DesiredSize.Height, finalSize.Width, _media.DesiredSize.Height));
                }
                else
                {
                    _media.Arrange(new Rect(0, _text.DesiredSize.Height, finalSize.Width, _media.DesiredSize.Height));
                }
            }
            else
            {
                _media.Arrange(new Rect(0, 0, finalSize.Width, _media.DesiredSize.Height));
                _text.Arrange(new Rect(0, _media.DesiredSize.Height, finalSize.Width, _text.DesiredSize.Height));

                _factCheck?.Arrange(new Rect(0, _media.DesiredSize.Height + _text.DesiredSize.Height, finalSize.Width, _factCheck.DesiredSize.Height));
            }

            var reactionsHeight = _reactions?.DesiredSize.Height ?? 0;

            if (_factCheck != null)
            {
                _reactions?.Arrange(new Rect(0, _text.DesiredSize.Height + _media.DesiredSize.Height + _factCheck.DesiredSize.Height, finalSize.Width, _reactions.DesiredSize.Height));
                reactionsHeight += _factCheck.DesiredSize.Height;
            }
            else
            {
                _reactions?.Arrange(new Rect(0, _text.DesiredSize.Height + _media.DesiredSize.Height, finalSize.Width, _reactions.DesiredSize.Height));
            }

            var margin = _margin;
            var footerWidth = _footer.DesiredSize.Width /*- footer.Margin.Right + footer.Margin.Left*/;
            var footerHeight = _footer.DesiredSize.Height /*- footer.Margin.Bottom + footer.Margin.Top*/;
            _footer.Arrange(new Rect(finalSize.Width - footerWidth,
                finalSize.Height - footerHeight,
                _footer.DesiredSize.Width,
                _footer.DesiredSize.Height));

            return finalSize;
        }

        /// <summary>
        /// How far the text of <paramref name="element"/> starts from the left edge of the panel.
        /// </summary>
        /// <remarks>
        /// A paragraph of an article is inset by its own margin, and
        /// <see cref="FormattedTextBlock.ContentEnd"/> answers in the block's own space, so the
        /// two only line up once what is between them is added. Walked rather than assumed: the
        /// values belong to the article's renderer and the bubble's template, not here. The depth
        /// is fixed - <see cref="InstantContent.LastBlock"/> only ever returns a block the article
        /// holds directly.
        /// </remarks>
        private static double LeftInset(FrameworkElement element, FrameworkElement root)
        {
            var offset = 0d;

            while (element != null)
            {
                offset += element.Margin.Left;

                // Padding is not on FrameworkElement, and the three that carry it here share no
                // base that declares it.
                if (element is Control control)
                {
                    offset += control.Padding.Left + control.BorderThickness.Left;
                }
                else if (element is Border border)
                {
                    offset += border.Padding.Left + border.BorderThickness.Left;
                }
                else if (element is StackPanel stack)
                {
                    offset += stack.Padding.Left + stack.BorderThickness.Left;
                }
                else if (element is Grid grid)
                {
                    offset += grid.Padding.Left + grid.BorderThickness.Left;
                }

                if (element == root)
                {
                    break;
                }

                // Not Parent: the root of a control template has no logical parent, and every
                // article is rendered inside one.
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }

            return offset;
        }

        /// <summary>
        /// The room the footer needs beside the last line of <paramref name="text"/>: a width to
        /// add if it can share that line, a height if it has to take one of its own.
        /// </summary>
        /// <param name="left">
        /// Where the text of <paramref name="text"/> starts within the panel. The block answers in
        /// its own space and the footer is placed in the panel's, so the two only line up here.
        /// </param>
        /// <param name="width">
        /// The width the footer is placed against, and the one the returned width is added to: the
        /// whole text for a plain message, the whole article for a rich one, where the last line
        /// belongs to a block nested inside the media.
        /// </param>
        /// <remarks>
        /// The last block of the text, whichever engine rendered it: both can say where their last
        /// line ends, and that is all this needs to know.
        /// </remarks>
        private Size Margins(double availableWidth, double left, double width, UIElement text, MessageFooter footer)
        {
            var marginLeft = 0d;
            var marginBottom = 0d;

            var hasLineEnding = text is FormattedTextBlock formatted && formatted.HasLineEnding;

            if (text is not FormattedTextBlock and not DirectTextBlock)
            {
                return new Size(0, AppSettings.Appearance.MessageFontSize * 1.33);
            }
            else if (Placeholder)
            {
                var maxWidth = availableWidth;
                var footerWidth = footer.DesiredSize.Width + footer.Margin.Left + footer.Margin.Right;

                var fontSize = AppSettings.Appearance.MessageFontSize;

                if (hasLineEnding)
                {
                    return new Size(0, fontSize * 1.33);
                }
                else if (ForceNewLine)
                {
                    return new Size(Math.Max(0, footerWidth - 16), 0);
                }

                var bounds = left + (text is DirectTextBlock direct
                    ? direct.ContentEnd()
                    : ((FormattedTextBlock)text).ContentEnd());

                var diff = width - bounds;
                if (diff < footerWidth /*|| _placeholderVertical*/)
                {
                    if (bounds + footerWidth < maxWidth /*&& !_placeholderVertical*/)
                    {
                        marginLeft = footerWidth - diff;
                    }
                    else
                    {
                        marginBottom = fontSize * 1.33; //18.62;
                    }
                }
            }

            return new Size(marginLeft, marginBottom);
        }
    }
}
