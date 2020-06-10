using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class ReplyMarkupButtonClickEventArgs : EventArgs
    {
        public ReplyMarkupButtonClickEventArgs(KeyboardButton button)
        {
            Button = button;
        }

        public KeyboardButton Button { get; private set; }
    }

    public class ReplyMarkupInlineButtonClickEventArgs : EventArgs
    {
        public ReplyMarkupInlineButtonClickEventArgs(InlineKeyboardButton button)
        {
            Button = button;
        }

        public InlineKeyboardButton Button { get; private set; }
    }

    public class ReplyMarkupPanel : Grid
    {
        private double _keyboardHeight = 260;

        //#region ReplyMarkup

        //public TLReplyMarkupBase ReplyMarkup
        //{
        //    get { return (TLReplyMarkupBase)GetValue(ReplyMarkupProperty); }
        //    set { SetValue(ReplyMarkupProperty, value); }
        //}

        //public static readonly DependencyProperty ReplyMarkupProperty =
        //    DependencyProperty.Register("ReplyMarkup", typeof(TLReplyMarkupBase), typeof(ReplyMarkupPanel), new PropertyMetadata(null, OnReplyMarkupChanged));

        //private static void OnReplyMarkupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    ((ReplyMarkupPanel)d).OnReplyMarkupChanged((TLReplyMarkupBase)e.NewValue, (TLReplyMarkupBase)e.OldValue);
        //}

        //#endregion

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            _keyboardHeight = args.OccludedRect.Height;
        }

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

            List<List<object>> rows = null;
            if (markup is ReplyMarkupShowKeyboard keyboardMarkup && !inline)
            {
                rows = keyboardMarkup.Rows.Select(x => x.Select(y => y as object).ToList()).ToList();
                resize = keyboardMarkup.ResizeKeyboard;
            }
            else if (markup is ReplyMarkupInlineKeyboard inlineMarkup && inline)
            {
                rows = inlineMarkup.Rows.Select(x => x.Select(y => y as object).ToList()).ToList();

                //if (!double.IsNaN(Height))
                //{
                //    Height = double.NaN;
                //}
            }

            UpdateSize(markup, inline);
            Children.Clear();
            RowDefinitions.Clear();

            var receipt = false;
            if (message != null && message.Content is MessageInvoice invoice)
            {
                receipt = invoice.ReceiptMessageId != 0;
            }

            if (rows != null && ((inline && markup is ReplyMarkupInlineKeyboard) || (!inline && markup is ReplyMarkupShowKeyboard)))
            {
                for (int j = 0; j < rows.Count; j++)
                {
                    var row = rows[j];

                    var panel = new Grid();
                    panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    panel.VerticalAlignment = VerticalAlignment.Stretch;
                    panel.Margin = new Thickness(-1, 0, -1, 0);

                    for (int i = 0; i < row.Count; i++)
                    {
                        var button = new GlyphButton();
                        button.Tag = row[i];
                        button.Margin = new Thickness(1, 2, 1, 0);
                        button.HorizontalAlignment = HorizontalAlignment.Stretch;
                        button.VerticalAlignment = VerticalAlignment.Stretch;
                        button.Click += Button_Click;

                        if (inline)
                        {
                            button.Style = App.Current.Resources["ReplyInlineMarkupButtonStyle"] as Style;
                        }
                        else
                        {
                            button.Style = App.Current.Resources["ReplyKeyboardMarkupButtonStyle"] as Style;
                        }

                        if (row[i] is InlineKeyboardButton inlineButton)
                        {
                            button.Content = inlineButton.Text;

                            if (inlineButton.Type is InlineKeyboardButtonTypeUrl typeUrl)
                            {
                                button.Glyph = "\uE143";
                                ToolTipService.SetToolTip(button, typeUrl.Url);
                            }
                            else if (inlineButton.Type is InlineKeyboardButtonTypeLoginUrl loginUrl)
                            {
                                button.Glyph = "\uE143";
                            }
                            else if (inlineButton.Type is InlineKeyboardButtonTypeSwitchInline)
                            {
                                button.Glyph = "\uEE35";
                            }
                            // TODO: ku fu???
                            //else if (row[i] is TLKeyboardButton && inline)
                            //{
                            //    button.Glyph = "\uE15F";
                            //}
                            else if (inlineButton.Type is InlineKeyboardButtonTypeBuy && receipt)
                            {
                                button.Content = Strings.Resources.PaymentReceipt;
                            }
                        }
                        else if (row[i] is KeyboardButton keyboardButton)
                        {
                            button.Content = keyboardButton.Text;
                        }

                        SetColumn(button, i);

                        panel.ColumnDefinitions.Add(new ColumnDefinition());
                        panel.Children.Add(button);

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

                        button.Radius = new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
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
                ButtonClick?.Invoke(this, new ReplyMarkupButtonClickEventArgs(btn));
            }
            else if (button.Tag is InlineKeyboardButton inlineBtn)
            {
                InlineButtonClick?.Invoke(this, new ReplyMarkupInlineButtonClickEventArgs(inlineBtn));
            }
        }

        public event EventHandler<ReplyMarkupButtonClickEventArgs> ButtonClick;
        public event EventHandler<ReplyMarkupInlineButtonClickEventArgs> InlineButtonClick;
    }
}
