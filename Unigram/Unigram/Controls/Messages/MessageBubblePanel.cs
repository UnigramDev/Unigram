using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages
{
    public class MessageBubblePanel : Panel
    {
        // Needed for Text CanvasTextLayout
        public MessageViewModel Message { get; set; }

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
            var text = Children[0] as RichTextBlock;
            var media = Children[1] as FrameworkElement;
            var footer = Children[2] as MessageFooter;

            var textRow = Grid.GetRow(text);
            var mediaRow = Grid.GetRow(media);
            var footerRow = Grid.GetRow(footer);

            text.Measure(availableSize);
            media.Measure(availableSize);
            footer.Measure(availableSize);

            if (textRow == footerRow)
            {
                _margin = Margins(availableSize.ToVector2());
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
            return new Size(media.DesiredSize.Width > 0 ? media.DesiredSize.Width : text.DesiredSize.Width + margin.Width,
                text.DesiredSize.Height + media.DesiredSize.Height + margin.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var text = Children[0] as RichTextBlock;
            var media = Children[1] as FrameworkElement;
            var footer = Children[2] as MessageFooter;

            var textRow = Grid.GetRow(text);
            var mediaRow = Grid.GetRow(media);

            var maxWidth = Math.Max(text.DesiredSize.Width, media.DesiredSize.Width);

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

            var margin = _margin;
            var footerWidth = footer.DesiredSize.Width /*- footer.Margin.Right + footer.Margin.Left*/;
            var footerHeight = footer.DesiredSize.Height /*- footer.Margin.Bottom + footer.Margin.Top*/;
            footer.Arrange(new Rect(finalSize.Width - footerWidth,
                text.DesiredSize.Height + media.DesiredSize.Height - footerHeight + margin.Height,
                footer.DesiredSize.Width,
                footer.DesiredSize.Height));

            return finalSize;
        }

        private Size Margins(Vector2 size)
        {
            var text = Children[0] as RichTextBlock;
            var footer = Children[2] as MessageFooter;

            var marginLeft = 0d;
            var marginBottom = 0d;

            if (_placeholder)
            {
                var maxWidth = size.X;
                var footerWidth = footer.DesiredSize.Width + footer.Margin.Left + footer.Margin.Right;

                var width = text.DesiredSize.Width;
                var rect = ContentEnd(size);

                var diff = width - rect.Right;
                if (diff < footerWidth /*|| _placeholderVertical*/)
                {
                    if (rect.Right + footerWidth < maxWidth /*&& !_placeholderVertical*/)
                    {
                        marginLeft = footerWidth - diff;
                    }
                    else
                    {
                        marginBottom = footer.DesiredSize.Height;
                    }
                }
            }

            return new Size(marginLeft, marginBottom);
        }

        private Rect ContentEnd(Vector2 size)
        {
            var caption = Message?.Content?.GetCaption();
            if (caption == null)
            {
                return Rect.Empty;
            }

            var text = Children[0] as RichTextBlock;
            using (CanvasTextFormat format = new CanvasTextFormat { FontFamily = "Assets\\Emoji\\apple.ttf#Segoe UI Emoji", FontSize = (float)text.FontSize })
            using (CanvasTextLayout textLayout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), caption.Text, format, size.X, size.Y))
            {
                foreach (var entity in caption.Entities)
                {
                    if (entity.Type is TextEntityTypeBold)
                    {
                        textLayout.SetFontWeight(entity.Offset, entity.Length, FontWeights.SemiBold);
                    }
                    else if (entity.Type is TextEntityTypeItalic)
                    {
                        textLayout.SetFontStyle(entity.Offset, entity.Length, FontStyle.Italic);
                    }
                    else if (entity.Type is TextEntityTypeCode || entity.Type is TextEntityTypePre || entity.Type is TextEntityTypePreCode)
                    {
                        textLayout.SetFontFamily(entity.Offset, entity.Length, "Consolas");
                    }
                }

                var regions = textLayout.GetCharacterRegions(caption.Text.Length, 0);
                if (regions.Length > 0)
                {
                    return regions[0].LayoutBounds;
                }
            }

            return Rect.Empty;
        }
    }
}
