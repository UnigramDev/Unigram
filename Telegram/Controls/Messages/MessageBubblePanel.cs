//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Controls.Messages
{
    public class MessageBubblePanel : Panel
    {
        // Needed for Text CanvasTextLayout
        public bool ForceNewLine { get; set; }

        // Needed for Measure
        public MessageReply Reply { get; set; }

        private bool _placeholder = true;
        public bool Placeholder
        {
            get => _placeholder;
            set
            {
                if (_placeholder != value)
                {
                    _placeholder = value;

                    // TODO: removed as an experiment
                    //InvalidateMeasure();
                }
            }
        }

        private Size _margin;

        protected override Size MeasureOverride(Size availableSize)
        {
            FormattedTextBlock text;
            FrameworkElement media;
            UIElement factCheck;
            UIElement third;

            var first = Children[0];
            if (first is MessageFactCheck)
            {
                text = Children[1] as FormattedTextBlock;
                media = Children[2] as FrameworkElement;
                third = Children[3];
                factCheck = first as MessageFactCheck;
            }
            else
            {
                text = Children[0] as FormattedTextBlock;
                media = Children[1] as FrameworkElement;
                third = Children[2];
                factCheck = null;
            }

            MessageFooter footer;
            ReactionsPanel reactions;
            if (third is ReactionsPanel)
            {
                footer = Children[^1] as MessageFooter;
                reactions = third as ReactionsPanel;
            }
            else
            {
                footer = third as MessageFooter;
                reactions = null;
            }

            var textRow = Grid.GetRow(text);
            var mediaRow = Grid.GetRow(media);
            var footerRow = Grid.GetRow(footer);

            text.Measure(availableSize);
            media.Measure(availableSize);
            factCheck?.Measure(availableSize);
            footer.Measure(availableSize);

            if (reactions != null)
            {
                if (reactions.Footer != footer.DesiredSize && reactions.Children.Count > 0)
                {
                    reactions.InvalidateMeasure();
                }

                reactions.Footer = footer.DesiredSize;
                reactions.Measure(availableSize);
            }

            if (reactions != null && reactions.HasReactions)
            {
                _margin = new Size(0, 0);
            }
            else if (textRow == footerRow)
            {
                _margin = Margins(availableSize.Width, text, footer);
            }
            else if (mediaRow == footerRow)
            {
                _margin = new Size(0, 0);
            }
            else
            {
                _margin = new Size(0, footer.DesiredSize.Height);
            }

            var margin = _margin;
            var width = media.DesiredSize.Width == availableSize.Width
                ? media.DesiredSize.Width
                : Math.Max(media.DesiredSize.Width, text.DesiredSize.Width + margin.Width);

            var reactionsWidth = reactions?.DesiredSize.Width ?? 0;
            var reactionsHeight = reactions?.DesiredSize.Height ?? 0;

            if (factCheck != null)
            {
                reactionsWidth = Math.Max(reactionsWidth, factCheck.DesiredSize.Width);
                reactionsHeight += factCheck.DesiredSize.Height;
            }

            var finalWidth = Math.Max(Math.Max(reactionsWidth, footer.DesiredSize.Width), width);
            var finalHeight = text.DesiredSize.Height + media.DesiredSize.Height + reactionsHeight + margin.Height;

            if (Reply != null)
            {
                Reply.ContentWidth = finalWidth;
            }

            return new Size(finalWidth, finalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            FormattedTextBlock text;
            FrameworkElement media;
            UIElement factCheck;
            UIElement third;

            var first = Children[0];
            if (first is MessageFactCheck)
            {
                text = Children[1] as FormattedTextBlock;
                media = Children[2] as FrameworkElement;
                third = Children[3];
                factCheck = first as MessageFactCheck;
            }
            else
            {
                text = Children[0] as FormattedTextBlock;
                media = Children[1] as FrameworkElement;
                third = Children[2];
                factCheck = null;
            }

            MessageFooter footer;
            ReactionsPanel reactions;
            if (third is ReactionsPanel)
            {
                footer = Children[^1] as MessageFooter;
                reactions = third as ReactionsPanel;
            }
            else
            {
                footer = third as MessageFooter;
                reactions = null;
            }

            var textRow = Grid.GetRow(text);
            var mediaRow = Grid.GetRow(media);

            if (textRow < mediaRow)
            {
                text.Arrange(new Rect(0, 0, finalSize.Width, text.DesiredSize.Height));

                if (factCheck != null)
                {
                    factCheck.Arrange(new Rect(0, text.DesiredSize.Height, finalSize.Width, factCheck.DesiredSize.Height));
                    media.Arrange(new Rect(0, text.DesiredSize.Height + factCheck.DesiredSize.Height, finalSize.Width, media.DesiredSize.Height));
                }
                else
                {
                    media.Arrange(new Rect(0, text.DesiredSize.Height, finalSize.Width, media.DesiredSize.Height));
                }
            }
            else
            {
                media.Arrange(new Rect(0, 0, finalSize.Width, media.DesiredSize.Height));
                text.Arrange(new Rect(0, media.DesiredSize.Height, finalSize.Width, text.DesiredSize.Height));

                factCheck?.Arrange(new Rect(0, media.DesiredSize.Height + text.DesiredSize.Height, finalSize.Width, factCheck.DesiredSize.Height));
            }

            var reactionsHeight = reactions?.DesiredSize.Height ?? 0;

            if (factCheck != null)
            {
                reactions?.Arrange(new Rect(0, text.DesiredSize.Height + media.DesiredSize.Height + factCheck.DesiredSize.Height, finalSize.Width, reactions.DesiredSize.Height));
                reactionsHeight += factCheck.DesiredSize.Height;
            }
            else
            {
                reactions?.Arrange(new Rect(0, text.DesiredSize.Height + media.DesiredSize.Height, finalSize.Width, reactions.DesiredSize.Height));
            }

            var margin = _margin;
            var footerWidth = footer.DesiredSize.Width /*- footer.Margin.Right + footer.Margin.Left*/;
            var footerHeight = footer.DesiredSize.Height /*- footer.Margin.Bottom + footer.Margin.Top*/;
            footer.Arrange(new Rect(finalSize.Width - footerWidth,
                text.DesiredSize.Height + media.DesiredSize.Height + reactionsHeight - footerHeight + margin.Height,
                footer.DesiredSize.Width,
                footer.DesiredSize.Height));

            return finalSize;
        }

        private Size Margins(double availableWidth, FormattedTextBlock text, MessageFooter footer)
        {
            var marginLeft = 0d;
            var marginBottom = 0d;

            if (_placeholder)
            {
                var maxWidth = availableWidth;
                var footerWidth = footer.DesiredSize.Width + footer.Margin.Left + footer.Margin.Right;

                var fontSize = Theme.Current.MessageFontSize;

                if (text.HasLineEnding)
                {
                    return new Size(0, fontSize * 1.33);
                }
                else if (ForceNewLine)
                {
                    return new Size(Math.Max(0, footerWidth - 16), 0);
                }

                var width = text.DesiredSize.Width;
                var bounds = ContentEnd(text.Text, availableWidth, fontSize * BootStrapper.Current.TextScaleFactor);

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

        private float ContentEnd(StyledText caption, double availableWidth, double fontSize)
        {
            if (caption?.Paragraphs.Count == 0 || string.IsNullOrEmpty(caption?.Text))
            {
                return 0;
            }

            var paragraph = caption.Paragraphs[^1];

            var text = caption.Text.Substring(paragraph.Offset, paragraph.Length);
            var entities = paragraph.Entities;

            var block = Children[0] is FormattedTextBlock formatted ? formatted : Children[1] as FormattedTextBlock;
            var width = availableWidth - block.Margin.Left - block.Margin.Right;

            if (width <= 0)
            {
                return 0;
            }

            try
            {
                // TODO: this condition will be true whenever the message has more than a paragraph.

                var bounds = PlaceholderImageHelper.Current.ContentEnd(text, entities, fontSize, width);
                if (bounds.Y < block.DesiredSize.Height)
                {
                    return bounds.X;
                }
            }
            catch { }

            return int.MaxValue;
        }
    }
}
