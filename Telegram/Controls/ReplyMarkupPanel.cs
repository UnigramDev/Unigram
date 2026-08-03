//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class ReplyMarkupButtonClickEventArgs : EventArgs
    {
        public ReplyMarkupButtonClickEventArgs(KeyboardButton button, bool oneTime)
        {
            Button = button;
            OneTime = oneTime;
        }

        public KeyboardButton Button { get; }

        public bool OneTime { get; }
    }

    public partial class ReplyMarkupInlineButtonClickEventArgs : EventArgs
    {
        public ReplyMarkupInlineButtonClickEventArgs(InlineKeyboardButton button)
        {
            Button = button;
        }

        public InlineKeyboardButton Button { get; }
    }

    public partial class ReplyMarkupPanel : Grid
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

                if (invoice.PaidMedia is not PaidMediaUnsupported and not null)
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
                    var button = new ReplyMarkupButton(item);
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    button.VerticalAlignment = VerticalAlignment.Stretch;
                    button.Margin = new Thickness(4, 4, 4, 0);
                    button.Height = resize ? 40 : double.NaN;
                    button.Content = item.Text;
                    button.Click += Button_Click;

                    var topLeft = 4;
                    var topRight = 4;
                    var bottomRight = 4;
                    var bottomLeft = 4;

                    //if (j == 0)
                    //{
                    //    if (i == 0)
                    //    {
                    //        topLeft = 24 - 8;
                    //    }

                    //    if (i == row.Count - 1)
                    //    {
                    //        topRight = 24 - 8;
                    //    }
                    //}

                    if (j == rows.Count - 1)
                    {
                        if (i == 0)
                        {
                            bottomLeft = 24 - 8;
                        }

                        if (i == row.Count - 1)
                        {
                            bottomRight = 24 - 8;
                        }
                    }

                    button.CornerRadius = new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);

                    if (item.IconCustomEmojiId != 0)
                    {
                        button.Source = new CustomEmojiFileSource(message.ClientService, item.IconCustomEmojiId);
                    }

                    if (item.Type is KeyboardButtonTypeWebApp)
                    {
                        button.Glyph = Icons.Window16;
                    }

                    switch (item.Style)
                    {
                        case ButtonStylePrimary:
                            button.Style = BootStrapper.Current.Resources["AccentReplyMarkupButtonStyle"] as Style;
                            break;
                        case ButtonStyleDanger:
                            button.Style = BootStrapper.Current.Resources["DangerReplyMarkupButtonStyle"] as Style;
                            break;
                        case ButtonStyleSuccess:
                            button.Style = BootStrapper.Current.Resources["SuccessReplyMarkupButtonStyle"] as Style;
                            break;
                    }

                    panel.Children.Add(button);
                }

                SetRow(panel, j);

                RowDefinitions.Add(1, resize ? GridUnitType.Auto : GridUnitType.Star);
                Children.Add(panel);
            }

            if (Children.Count > 0)
            {
                Padding = new Thickness(0, 4, 0, 4);
                return true;
            }

            Padding = new Thickness();
            return false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ReplyMarkupButton button)
            {
                ButtonClick?.Invoke(this, new ReplyMarkupButtonClickEventArgs(button.Button, _oneTime));
            }
        }

        public event EventHandler<ReplyMarkupButtonClickEventArgs> ButtonClick;
    }

    public partial class ReplyMarkupRow : Panel
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
