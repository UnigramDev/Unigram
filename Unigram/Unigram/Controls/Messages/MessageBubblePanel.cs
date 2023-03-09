//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Native;
using Unigram.Navigation;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages
{
    public class MessageBubblePanel : Panel
    {
        // Needed for Text CanvasTextLayout
        public MessageContent Content { get; set; }

        // Needed for Measure
        public MessageReference Reply { get; set; }

        private bool _placeholder = true;
        public bool Placeholder
        {
            get => _placeholder;
            set
            {
                if (_placeholder != value)
                {
                    _placeholder = value;
                    InvalidateMeasure();
                }
            }
        }

        private Size _margin;

        protected override Size MeasureOverride(Size availableSize)
        {
            var text = Children[0] as FormattedTextBlock;
            var media = Children[1] as FrameworkElement;
            var footer = Children[3] as MessageFooter;
            var reactions = Children[2] as ReactionsPanel;

            var textRow = Grid.GetRow(text);
            var mediaRow = Grid.GetRow(media);
            var footerRow = Grid.GetRow(footer);

            text.Measure(availableSize);
            media.Measure(availableSize);
            footer.Measure(availableSize);

            reactions.Footer = footer.DesiredSize;
            reactions.Measure(availableSize);

            if (reactions.HasReactions)
            {
                _margin = new Size(0, 0);
            }
            else if (textRow == footerRow)
            {
                _margin = Margins(availableSize.Width);
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

            if (Reply != null)
            {
                Reply.ContentWidth = Math.Max(Math.Max(reactions.DesiredSize.Width, footer.DesiredSize.Width), width);
            }

            return new Size(Math.Max(Math.Max(reactions.DesiredSize.Width, footer.DesiredSize.Width), width),
                text.DesiredSize.Height + media.DesiredSize.Height + reactions.DesiredSize.Height + margin.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var text = Children[0] as FormattedTextBlock;
            var media = Children[1] as FrameworkElement;
            var footer = Children[3] as MessageFooter;
            var reactions = Children[2] as ReactionsPanel;

            var textRow = Grid.GetRow(text);
            var mediaRow = Grid.GetRow(media);

            if (textRow < mediaRow)
            {
                text.Arrange(new Rect(0, 0, finalSize.Width, text.DesiredSize.Height));
                media.Arrange(new Rect(0, text.DesiredSize.Height, finalSize.Width, media.DesiredSize.Height));
            }
            else
            {
                media.Arrange(new Rect(0, 0, finalSize.Width, media.DesiredSize.Height));
                text.Arrange(new Rect(0, media.DesiredSize.Height, finalSize.Width, text.DesiredSize.Height));
            }

            reactions.Arrange(new Rect(0, text.DesiredSize.Height + media.DesiredSize.Height, finalSize.Width, reactions.DesiredSize.Height));

            var margin = _margin;
            var footerWidth = footer.DesiredSize.Width /*- footer.Margin.Right + footer.Margin.Left*/;
            var footerHeight = footer.DesiredSize.Height /*- footer.Margin.Bottom + footer.Margin.Top*/;
            footer.Arrange(new Rect(finalSize.Width - footerWidth,
                text.DesiredSize.Height + media.DesiredSize.Height + reactions.DesiredSize.Height - footerHeight + margin.Height,
                footer.DesiredSize.Width,
                footer.DesiredSize.Height));

            return finalSize;
        }

        private Size Margins(double availableWidth)
        {
            var text = Children[0] as FormattedTextBlock;
            var footer = Children[3] as MessageFooter;

            var marginLeft = 0d;
            var marginBottom = 0d;

            if (_placeholder)
            {
                var maxWidth = availableWidth;
                var footerWidth = footer.DesiredSize.Width + footer.Margin.Left + footer.Margin.Right;

                if (Content is MessageBigEmoji)
                {
                    return new Size(Math.Max(0, footerWidth - 16), 0);
                }

                var width = text.DesiredSize.Width;
                var rect = ContentEnd(availableWidth);

                var diff = width - rect.X;
                if (diff < footerWidth /*|| _placeholderVertical*/)
                {
                    if (rect.X + footerWidth < maxWidth /*&& !_placeholderVertical*/)
                    {
                        marginLeft = footerWidth - diff;
                    }
                    else
                    {
                        marginBottom = 18;
                    }
                }
            }

            return new Size(marginLeft, marginBottom);
        }

        private Vector2 ContentEnd(double availableWidth)
        {
            var caption = Content?.GetCaption();
            if (string.IsNullOrEmpty(caption?.Text))
            {
                return Vector2.Zero;
            }

            var formatted = FormatText(caption);

            var text = Children[0] as FormattedTextBlock;
            var fontSize = Theme.Current.MessageFontSize * BootStrapper.Current.UISettings.TextScaleFactor;
            var width = availableWidth - text.Margin.Left - text.Margin.Right;

            try
            {
                var bounds = PlaceholderImageHelper.Current.ContentEnd(formatted.Text, formatted.Entities, fontSize, width);
                if (bounds.Y < text.DesiredSize.Height)
                {
                    return bounds;
                }
            }
            catch { }

            return new Vector2(int.MaxValue, 0);
        }

        private FormattedText _prevText;
        private IList<PlaceholderEntity> _prevTextEntities;

        // Not the most elegant solution
        private (string Text, IList<PlaceholderEntity> Entities) FormatText(FormattedText text)
        {
            if (text == _prevText)
            {
                return (_prevText.Text, _prevTextEntities);
            }

            IList<PlaceholderEntity> entities = null;

            foreach (var entity in text.Entities)
            {
                if (entity.Type is TextEntityTypeCustomEmoji
                    or TextEntityTypeSpoiler
                    or TextEntityTypeMentionName
                    or TextEntityTypeTextUrl)
                {
                    continue;
                }

                entities ??= new List<PlaceholderEntity>();
                entities.Add(new PlaceholderEntity
                {
                    Offset = entity.Offset,
                    Length = entity.Length,
                    Type = entity.Type switch
                    {
                        TextEntityTypeBold => PlaceholderEntityType.Bold,
                        TextEntityTypeCode => PlaceholderEntityType.Code,
                        TextEntityTypeItalic => PlaceholderEntityType.Italic,
                        TextEntityTypePre => PlaceholderEntityType.Code,
                        TextEntityTypePreCode => PlaceholderEntityType.Code,
                        TextEntityTypeStrikethrough => PlaceholderEntityType.Strikethrough,
                        _ => PlaceholderEntityType.Underline
                    }
                });
            }

            _prevText = text;
            _prevTextEntities = entities ?? Array.Empty<PlaceholderEntity>();

            return (text.Text, _prevTextEntities);
        }
    }
}
