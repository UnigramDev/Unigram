using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class ReplyMarkupButtonClickEventArgs : EventArgs
    {
        public ReplyMarkupButtonClickEventArgs(KeyboardButton button, bool oneTime)
        {
            Button = button;
            OneTime = oneTime;
        }

        public KeyboardButton Button { get; }

        public bool OneTime { get; }
    }

    public class ReplyMarkupInlineButtonClickEventArgs : EventArgs
    {
        public ReplyMarkupInlineButtonClickEventArgs(InlineKeyboardButton button)
        {
            Button = button;
        }

        public InlineKeyboardButton Button { get; }
    }

    public class ReplyMarkupPanel : Grid
    {
        private readonly double _keyboardHeight = 260;

        private bool _oneTime;

        private void UpdateSize(ReplyMarkup markup, bool inline)
        {
            if (markup is ReplyMarkupShowKeyboard keyboard && !inline && Parent is ScrollViewer scroll)
            {
                if (keyboard.ResizeKeyboard)
                {
                    scroll.Height = double.NaN;
                    scroll.MaxHeight = _keyboardHeight;
                }
                else
                {
                    scroll.Height = _keyboardHeight;
                    scroll.MaxHeight = _keyboardHeight;
                }
            }
            else if (markup is ReplyMarkupRemoveKeyboard && !inline && Parent is ScrollViewer scroll2)
            {
                scroll2.Height = 0;
                scroll2.MaxHeight = _keyboardHeight;
            }
        }

        public bool Update(MessageViewModel message, ReplyMarkup markup, bool inline = true)
        {
            //var inline = Message is TLMessage;
            //var inline = true;
            var resize = false;
            var oneTime = false;

            List<List<object>> rows = null;
            if (markup is ReplyMarkupShowKeyboard keyboardMarkup && !inline)
            {
                rows = keyboardMarkup.Rows.Select(x => x.Select(y => y as object).ToList()).ToList();
                resize = keyboardMarkup.ResizeKeyboard;
                oneTime = keyboardMarkup.OneTime;
            }
            else if (markup is ReplyMarkupInlineKeyboard inlineMarkup && inline)
            {
                rows = inlineMarkup.Rows.Select(x => x.Select(y => y as object).ToList()).ToList();

                //if (!double.IsNaN(Height))
                //{
                //    Height = double.NaN;
                //}
            }

            _oneTime = oneTime;
            Tag = message;

            UpdateSize(markup, inline);
            Children.Clear();
            RowDefinitions.Clear();

            var receipt = false;
            if (message != null && message.Content is MessageInvoice invoice)
            {
                receipt = invoice.ReceiptMessageId != 0;

                if (invoice.ExtendedMedia is not MessageExtendedMediaUnsupported and not null)
                {
                    rows = null;
                }
            }

            if (rows != null && ((inline && markup is ReplyMarkupInlineKeyboard) || (!inline && markup is ReplyMarkupShowKeyboard)))
            {
                for (int j = 0; j < rows.Count; j++)
                {
                    var row = rows[j];

                    var panel = new ReplyMarkupRow();
                    panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    panel.VerticalAlignment = VerticalAlignment.Stretch;
                    panel.Margin = new Thickness(-1, 0, -1, 0);

                    for (int i = 0; i < row.Count; i++)
                    {
                        var button = new GlyphButton();
                        button.Tag = row[i];
                        button.HorizontalAlignment = HorizontalAlignment.Stretch;
                        button.VerticalAlignment = VerticalAlignment.Stretch;
                        button.Click += Button_Click;

                        if (inline)
                        {
                            button.Style = BootStrapper.Current.Resources["ReplyInlineMarkupButtonStyle"] as Style;
                            button.Margin = new Thickness(1, 2, 1, 0);
                        }
                        else
                        {
                            button.Style = BootStrapper.Current.Resources["ReplyKeyboardMarkupButtonStyle"] as Style;
                            button.Margin = new Thickness(4, 8, 4, 0);
                            button.Height = resize ? 36 : double.NaN;
                        }

                        if (row[i] is InlineKeyboardButton inlineButton)
                        {
                            button.Content = inlineButton.Text;

                            if (inlineButton.Type is InlineKeyboardButtonTypeUrl typeUrl)
                            {
                                button.Glyph = "\uE9B7";
                                ToolTipService.SetToolTip(button, typeUrl.Url);
                            }
                            else if (inlineButton.Type is InlineKeyboardButtonTypeLoginUrl loginUrl)
                            {
                                button.Glyph = "\uE9B7";
                            }
                            else if (inlineButton.Type is InlineKeyboardButtonTypeSwitchInline)
                            {
                                button.Glyph = "\uEE35";
                            }
                            else if (inlineButton.Type is InlineKeyboardButtonTypeBuy)
                            {
                                button.Glyph = Icons.Payment16;

                                if (receipt)
                                {
                                    button.Content = Strings.Resources.PaymentReceipt;
                                }
                            }
                            else if (inlineButton.Type is InlineKeyboardButtonTypeWebApp)
                            {
                                button.Glyph = Icons.Window16;
                            }
                        }
                        else if (row[i] is KeyboardButton keyboardButton)
                        {
                            button.Content = keyboardButton.Text;

                            if (keyboardButton.Type is KeyboardButtonTypeWebApp)
                            {
                                button.Glyph = Icons.Window16;
                            }
                        }

                        if (inline)
                        {
                            var topLeft = 4d;
                            var topRight = 4d;
                            var bottomRight = 4d;
                            var bottomLeft = 4d;

                            if (j == rows.Count - 1)
                            {
                                if (i == 0)
                                {
                                    bottomLeft = CornerRadius.BottomLeft;
                                }

                                if (i == row.Count - 1)
                                {
                                    bottomRight = CornerRadius.BottomRight;
                                }
                            }

                            button.CornerRadius = new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
                        }

                        panel.Children.Add(button);
                    }

                    SetRow(panel, j);

                    RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, resize ? GridUnitType.Auto : GridUnitType.Star) });
                    Children.Add(panel);
                }

                if (Children.Count > 0 && !inline)
                {
                    Padding = new Thickness(0, 0, 0, 4);
                    return true;
                }
                else if (!inline)
                {
                    Padding = new Thickness();
                    return false;
                }
            }

            return false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Tag is KeyboardButton btn)
            {
                ButtonClick?.Invoke(this, new ReplyMarkupButtonClickEventArgs(btn, _oneTime));
            }
            else if (button.Tag is InlineKeyboardButton inlineBtn)
            {
                InlineButtonClick?.Invoke(this, new ReplyMarkupInlineButtonClickEventArgs(inlineBtn));
            }
        }

        public event EventHandler<ReplyMarkupButtonClickEventArgs> ButtonClick;
        public event EventHandler<ReplyMarkupInlineButtonClickEventArgs> InlineButtonClick;
    }

    public class ReplyMarkupRow : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var width = 0d;
            var height = 0d;

            foreach (var child in Children)
            {
                child.Measure(availableSize);
                width = Math.Max(width, child.DesiredSize.Width);
                height = Math.Max(height, child.DesiredSize.Height);
            }

            if (width * Children.Count > availableSize.Width)
            {
                width = availableSize.Width;
            }
            else
            {
                width *= Children.Count;
            }

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = 0d;

            foreach (var child in Children)
            {
                child.Arrange(new Rect(x, 0, finalSize.Width / Children.Count, finalSize.Height));
                x += finalSize.Width / Children.Count;
            }

            return finalSize;
        }
    }
}
