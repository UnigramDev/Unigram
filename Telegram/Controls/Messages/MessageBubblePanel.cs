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

namespace Telegram.Controls.Messages
{
    public partial class MessageBubblePanel : Panel
    {
        // Needed for Text CanvasTextLayout
        public bool ForceNewLine { get; set; }

        // Needed for Measure
        public MessageReply Reply { get; set; }

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

            if (_reactions != null && _reactions.HasReactions)
            {
                _margin = new Size(0, 0);
            }
            else if (_textRow == _footerRow && _text.Children.Count > 0)
            {
                _margin = Margins(availableSize.Width, _text.DesiredSize.Width, _text.Children[^1], _footer);
            }
            else if (_mediaRow == _footerRow)
            {
                _margin = new Size(0, 0);
            }
            else if (_media is Border { Child: InstantContent rich })
            {
                if (rich.LastBlock is FormattedTextBlock lastBlock)
                {
                    _margin = Margins(availableSize.Width, lastBlock.DesiredSize.Width, lastBlock, _footer);
                }
                else
                {
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
                : Math.Max(_media.DesiredSize.Width, _text.DesiredSize.Width + margin.Width);

            var reactionsWidth = _reactions?.DesiredSize.Width ?? 0;
            var reactionsHeight = _reactions?.DesiredSize.Height ?? 0;

            if (_factCheck != null)
            {
                reactionsWidth = Math.Max(reactionsWidth, _factCheck.DesiredSize.Width);
                reactionsHeight += _factCheck.DesiredSize.Height;
            }

            var finalWidth = Math.Max(Math.Max(reactionsWidth, _footer.DesiredSize.Width), width);
            var finalHeight = _text.DesiredSize.Height + _media.DesiredSize.Height + reactionsHeight + margin.Height;

            Reply?.ContentWidth = finalWidth;

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
                _text.DesiredSize.Height + _media.DesiredSize.Height + reactionsHeight - footerHeight + margin.Height,
                _footer.DesiredSize.Width,
                _footer.DesiredSize.Height));

            return finalSize;
        }

        // The last block of the text, whichever engine rendered it: both can say where their
        // last line ends, and that is all this needs to know.
        private Size Margins(double availableWidth, double desiredWidth, UIElement text, MessageFooter footer)
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

                var width = desiredWidth;
                var bounds = text is DirectTextBlock direct
                    ? direct.ContentEnd()
                    : ((FormattedTextBlock)text).ContentEnd();

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
