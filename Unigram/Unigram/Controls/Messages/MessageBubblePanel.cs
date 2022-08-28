using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Native;
using Unigram.Navigation;
using Windows.Foundation;
using Windows.UI.Text;
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
            var offset = Children.Count < 5 ? 0 : 1;

            var emoji = Children.Count < 5 ? null : Children[1];
            var text = Children[0] as RichTextBlock;
            var media = Children[1 + offset] as FrameworkElement;
            var footer = Children[3 + offset] as MessageFooter;
            var reactions = Children[2 + offset] as ReactionsPanel;

            var textRow = Grid.GetRow(text);
            var mediaRow = Grid.GetRow(media);
            var footerRow = Grid.GetRow(footer);

            emoji?.Measure(availableSize);
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
            var offset = Children.Count < 5 ? 0 : 1;

            var emoji = Children.Count < 5 ? null : Children[1];
            var text = Children[0] as RichTextBlock;
            var media = Children[1 + offset] as FrameworkElement;
            var footer = Children[3 + offset] as MessageFooter;
            var reactions = Children[2 + offset] as ReactionsPanel;

            var textRow = Grid.GetRow(text);
            var mediaRow = Grid.GetRow(media);

            if (textRow < mediaRow)
            {
                emoji?.Arrange(new Rect(0, 0, finalSize.Width, text.DesiredSize.Height));
                text.Arrange(new Rect(0, 0, finalSize.Width, text.DesiredSize.Height));
                media.Arrange(new Rect(0, text.DesiredSize.Height, finalSize.Width, media.DesiredSize.Height));
            }
            else
            {
                media.Arrange(new Rect(0, 0, finalSize.Width, media.DesiredSize.Height));
                text.Arrange(new Rect(0, media.DesiredSize.Height, finalSize.Width, text.DesiredSize.Height));
                emoji?.Arrange(new Rect(0, media.DesiredSize.Height, finalSize.Width, text.DesiredSize.Height));
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
            var offset = Children.Count < 5 ? 0 : 1;

            var text = Children[0] as RichTextBlock;
            var footer = Children[3 + offset] as MessageFooter;

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

            var text = Children[0] as RichTextBlock;
            var fontSize = Theme.Current.MessageFontSize * BootStrapper.Current.UISettings.TextScaleFactor;
            var width = availableWidth - text.Margin.Left - text.Margin.Right;

            try
            {
                var bounds = PlaceholderImageHelper.Current.ContentEnd(caption.Text, fontSize, width);
                if (bounds.Y < text.DesiredSize.Height)
                {
                    return bounds;
                }
            }
            catch { }

            return new Vector2(int.MaxValue, 0);
        }

        private CanvasTextFormat _format;
        private CanvasTextLayout _layout;

        private FormattedText _prevText;

        private CanvasTextLayout GetTextLayout(FormattedText caption, float width)
        {
            if (_prevText == caption && _layout != null)
            {
                _layout.RequestedSize = new Size(width, double.PositiveInfinity);
                return _layout;
            }

            var fontSize = (float)(Theme.Current.MessageFontSize * BootStrapper.Current.UISettings.TextScaleFactor);

            _format ??= new CanvasTextFormat { FontFamily = "Assets\\Emoji\\apple.ttf#Segoe UI Emoji", FontSize = fontSize };
            _layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), caption.Text, _format, width, float.PositiveInfinity);

            foreach (var entity in caption.Entities)
            {
                if (entity.Type is TextEntityTypeBold)
                {
                    _layout.SetFontWeight(entity.Offset, entity.Length, FontWeights.SemiBold);
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    _layout.SetFontStyle(entity.Offset, entity.Length, FontStyle.Italic);
                }
                else if (entity.Type is TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode)
                {
                    _layout.SetFontFamily(entity.Offset, entity.Length, "Consolas");
                }
            }

            _prevText = caption;
            return _layout;
        }
    }
}
