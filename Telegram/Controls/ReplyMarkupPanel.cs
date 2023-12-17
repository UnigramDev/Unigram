//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
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

        private bool _empty = true;
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
            if (_empty && (message == null || markup == null))
            {
                return false;
            }

            _empty = message == null || markup == null;

            UpdateSize(markup, inline);
            Children.Clear();
            RowDefinitions.Clear();

            if (markup is ReplyMarkupShowKeyboard keyboardMarkup && !inline)
            {
                return Update(message, keyboardMarkup);
            }
            else if (markup is ReplyMarkupInlineKeyboard inlineMarkup && inline)
            {
                return Update(message, inlineMarkup);
            }

            return false;
        }

        public bool Update(MessageViewModel message, ReplyMarkupInlineKeyboard inlineMarkup)
        {
            var rows = inlineMarkup.Rows;

            _oneTime = false;
            Tag = message;

            var receipt = false;
            if (message != null && message.Content is MessageInvoice invoice)
            {
                receipt = invoice.ReceiptMessageId != 0;

                if (invoice.ExtendedMedia is not MessageExtendedMediaUnsupported and not null)
                {
                    rows = null;
                }
            }

            if (rows == null)
            {
                return false;
            }

            for (int j = 0; j < rows.Count; j++)
            {
                var row = rows[j];

                var panel = new ReplyMarkupRow();
                panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                panel.VerticalAlignment = VerticalAlignment.Stretch;
                panel.Margin = new Thickness(-1, 0, -1, 0);

                for (int i = 0; i < row.Count; i++)
                {
                    var item = row[i];
                    var button = new GlyphButton();
                    button.Tag = item;
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    button.VerticalAlignment = VerticalAlignment.Stretch;
                    button.Click += Button_Click;

                    button.Style = BootStrapper.Current.Resources["ReplyInlineMarkupButtonStyle"] as Style;
                    button.Margin = new Thickness(1, 2, 1, 0);

                    button.Content = item.Text;

                    switch (item.Type)
                    {
                        case InlineKeyboardButtonTypeUrl typeUrl:
                            button.Glyph = "\uE9B7";
                            ToolTipService.SetToolTip(button, typeUrl.Url);
                            break;
                        case InlineKeyboardButtonTypeLoginUrl:
                            button.Glyph = "\uE9B7";
                            break;
                        case InlineKeyboardButtonTypeSwitchInline:
                            button.Glyph = "\uEE35";
                            break;
                        case InlineKeyboardButtonTypeBuy:
                            button.Glyph = Icons.Payment16;

                            if (receipt)
                            {
                                button.Content = Strings.PaymentReceipt;
                            }
                            break;
                        case InlineKeyboardButtonTypeWebApp:
                            button.Glyph = Icons.Window16;
                            break;
                    }

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

                    panel.Children.Add(button);
                }

                SetRow(panel, j);

                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                Children.Add(panel);
            }

            return false;
        }

        public bool Update(MessageViewModel message, ReplyMarkupShowKeyboard keyboardMarkup)
        {
            var rows = keyboardMarkup.Rows;
            var resize = keyboardMarkup.ResizeKeyboard;
            var oneTime = keyboardMarkup.OneTime;

            _oneTime = oneTime;
            Tag = message;

            var receipt = false;
            if (message != null && message.Content is MessageInvoice invoice)
            {
                receipt = invoice.ReceiptMessageId != 0;

                if (invoice.ExtendedMedia is not MessageExtendedMediaUnsupported and not null)
                {
                    rows = null;
                }
            }

            for (int j = 0; j < rows.Count; j++)
            {
                var row = rows[j];

                var panel = new ReplyMarkupRow();
                panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                panel.VerticalAlignment = VerticalAlignment.Stretch;
                panel.Margin = new Thickness(-1, 0, -1, 0);

                for (int i = 0; i < row.Count; i++)
                {
                    var item = row[i];
                    var button = new GlyphButton();
                    button.Tag = item;
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    button.VerticalAlignment = VerticalAlignment.Stretch;
                    button.Click += Button_Click;

                    button.Style = BootStrapper.Current.Resources["ReplyKeyboardMarkupButtonStyle"] as Style;
                    button.Margin = new Thickness(4, 8, 4, 0);
                    button.Height = resize ? 36 : double.NaN;

                    button.Content = item.Text;

                    if (item.Type is KeyboardButtonTypeWebApp)
                    {
                        button.Glyph = Icons.Window16;
                    }

                    panel.Children.Add(button);
                }

                SetRow(panel, j);

                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, resize ? GridUnitType.Auto : GridUnitType.Star) });
                Children.Add(panel);
            }

            if (Children.Count > 0)
            {
                Padding = new Thickness(0, 0, 0, 4);
                return true;
            }

            Padding = new Thickness();
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
